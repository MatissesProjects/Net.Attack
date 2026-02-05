using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BRG;
using BRG.Gameplay;
using BRG.Gameplay.Units;
using BRG.Gameplay.Upgrades; 
using BRG.MessageSystem;
using BRG.DataManagement;
using BRG.Utils;
using NetAttackModUtils;

namespace SingularityMod
{
    [BepInPlugin("com.matissetec.singularity", "Singularity Vortex Mod", "1.9.0")]
    public class SingularityPlugin : BaseUnityPlugin
    {
        public static SingularityPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public static bool IsActive = false; 
        public static bool IsOverheated = false;

        public static float MaxEnergy = 100f;
        public static float BaseDrain = 8f;         
        public static float DrainPerEnemy = 3.0f;   
        public static float RechargeRate = 6f;     
        public static float BaseSafeRadius = 4.5f;  
        public static float MinSafeRadius = 1.5f;   

        public static float BlastPush = .5f;     
        public static float BlastDamage = .5f;
        public static float MeltdownSelfDamage = 25f;

        public static bool IsInShop = false;
        public static bool IsInCodingScreen = false;
        public static int PlayerLevel = 1;
        public static int SingularityLevel = 0;

        public const string UPGRADE_ID = "biSKigvSH0meTM/+oJv8ig";

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            Harmony harmony = new Harmony("com.matissetec.singularity");
            harmony.PatchAll(typeof(SingularityPlugin));
            
            ModUtils.PatchSafe(harmony, Log, "BRG.UI.UpgradeShop", "SetupShop", typeof(ShopHijacker));
            ModUtils.PatchSafe(harmony, Log, "BRG.Gameplay.Upgrades.RunUpgradeShopController", "OnUpgradeSelected", typeof(UpgradePurchaseHook));
            
            Log.LogInfo(">>> SINGULARITY MOD ONLINE <<<");
        }

        [HarmonyPatch]
        class ShopHijacker
        {
            static ScriptableObject _cachedUpgrade;
            static void Prefix(ref object[] __args)
            {
                try {
                    if (__args == null || __args.Length == 0) return;
                    if (_cachedUpgrade == null) {
                        Type type = AccessTools.TypeByName("MetagameUpgradeSO");
                        var all = Resources.FindObjectsOfTypeAll(type);
                        foreach (var obj in all) {
                            if ((string)AccessTools.Field(type, "id").GetValue(obj) == UPGRADE_ID) {
                                _cachedUpgrade = (ScriptableObject)obj;
                                break;
                            }
                        }
                    }
                    if (_cachedUpgrade != null) {
                        Type upgradeType = AccessTools.TypeByName("MetagameUpgradeSO");
                        Array newArray = Array.CreateInstance(upgradeType, 1);
                        newArray.SetValue(_cachedUpgrade, 0);
                        __args[0] = newArray;
                    }
                } catch {}
            }
        }

        [HarmonyPatch]
        class UpgradePurchaseHook
        {
            static void Prefix(object __instance)
            {
                try {
                    var field = AccessTools.Field(__instance.GetType(), "_upgradeSOs");
                    var list = field.GetValue(__instance) as IList;
                    if (list != null && list.Count > 0) {
                        SingularityPlugin.SingularityLevel++;
                        var behavior = UnityEngine.Object.FindObjectOfType<SingularityBehavior>();
                        if (behavior != null) behavior.Invoke("ApplyUpgrades", 0.1f);
                    }
                } catch {}
            }
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix] 
        static void PlayerAwake_Patch(Player __instance)
        {
            if (__instance.gameObject.GetComponent<SingularityBehavior>() == null)
                __instance.gameObject.AddComponent<SingularityBehavior>();
        }

        [HarmonyPatch(typeof(Enemy), "OnDisable")]
        [HarmonyPrefix]
        static void EnemyDeath_Patch(Enemy __instance)
        {
            if (!IsActive || IsOverheated || IsInShop || IsInCodingScreen) return;
            var behavior = UnityEngine.Object.FindObjectOfType<SingularityBehavior>();
            if (behavior == null) return;

            float energyCost = 10f;
            if (behavior.CurrentEnergy >= energyCost) {
                var healthComp = __instance.GetComponent<HealthComponent>();
                if (healthComp != null && healthComp.CurrentHealth <= 0) {
                    behavior.ConsumeEnergy(energyCost);
                    SingularityBehavior.HealPlayer(2f); 
                }
            }
        }
    }

    public class SingularityBehavior : MonoBehaviour
    {
        public float CurrentEnergy = 100f;
        private float _currentDrainRate = 0f;
        private float _scanTimer = 0f;
        private float _currentRadius = 4.0f;
        private float _maxRadius = 4.5f;
        private float _minRadius = 1.5f;
        private float _blastDamage = 0.5f;

        private List<Enemy> _nearbyEnemies = new List<Enemy>();
        private HealthComponent _myHealth; 
        private GameObject _fieldVisual;
        private ParticleSystem _particleSystem;

        private float _uiUpdateTimer = 0f;
        private float _movementHoldTimer = 0f; 
        private int _lastLevel = -1;

        void Start()
        {
            _myHealth = GetComponent<HealthComponent>();
            CreateVisuals();
            ApplyUpgrades();
        }

        void ApplyUpgrades()
        {
            int totalUpgrades = 0;
            try {
                var asm = typeof(RunUpgradeShopController).Assembly;
                var rdcType = asm.GetType("BRG.DataManagement.RunDataController");
                if (rdcType != null) {
                    var instance = rdcType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (instance != null) {
                        var list = rdcType.GetMethod("GetRunUpgrades")?.Invoke(instance, null) as IList;
                        if (list != null) totalUpgrades = list.Count;
                    }
                }
            } catch {}

            int finalLevel = totalUpgrades + SingularityPlugin.SingularityLevel;
            if (finalLevel != _lastLevel) {
                _lastLevel = finalLevel;
                _maxRadius = SingularityPlugin.BaseSafeRadius * (1f + (finalLevel * 0.10f));
                _blastDamage = SingularityPlugin.BlastDamage + (finalLevel * 2.0f);
            }
        }

        void CreateVisuals()
        {
            _fieldVisual = new GameObject("SingularityParticles");
            _fieldVisual.transform.SetParent(this.transform, false);
            _fieldVisual.transform.localPosition = Vector3.zero;
            
            _particleSystem = _fieldVisual.AddComponent<ParticleSystem>();
            var renderer = _fieldVisual.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            var main = _particleSystem.main;
            main.startLifetime = 1.0f;
            main.startSpeed = 0f; 
            main.startSize = 0.2f;
            main.maxParticles = 1000;
            main.simulationSpace = ParticleSystemSimulationSpace.World; 

            var emission = _particleSystem.emission;
            emission.rateOverTime = 50f;

            var shape = _particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            
            var col = _particleSystem.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.cyan, 0.0f), new GradientColorKey(Color.blue, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
            _fieldVisual.SetActive(false);
        }

        public static void HealPlayer(float amount)
        {
            var p = UnityEngine.Object.FindObjectOfType<Player>();
            if (p != null) {
                var h = p.GetComponent<HealthComponent>();
                if (h != null) h.Heal(amount, true);
            }
        }

        public void ConsumeEnergy(float amount)
        {
            CurrentEnergy = Mathf.Max(0, CurrentEnergy - amount);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.U)) {
                SingularityPlugin.SingularityLevel++;
                ApplyUpgrades();
            }

            SingularityPlugin.IsInCodingScreen = ModUtils.IsCodingScreenActive(SingularityPlugin.IsInCodingScreen, ref _movementHoldTimer);

            _uiUpdateTimer -= Time.deltaTime;
            if (_uiUpdateTimer <= 0f) {
                SingularityPlugin.IsInShop = ModUtils.IsShopOpen();
                SingularityPlugin.PlayerLevel = ModUtils.GetPlayerLevel(this);
                ApplyUpgrades(); 
                _uiUpdateTimer = 1.0f;
            }

            if (SingularityPlugin.IsInShop || SingularityPlugin.IsInCodingScreen) {
                if (_fieldVisual != null && _fieldVisual.activeSelf) _fieldVisual.SetActive(false);
                return;
            }

            HandleInput();
            ManageEnergy();
            UpdateVisuals();

            if (!SingularityPlugin.IsActive) return;

            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f) {
                _nearbyEnemies.Clear();
                var all = UnityEngine.Object.FindObjectsOfType<Enemy>();
                Vector3 myPos = transform.position;
                float scanRange = _currentRadius * 2.0f; 
                foreach (var e in all) {
                    if (e != null && Vector3.Distance(myPos, e.transform.position) <= scanRange)
                        _nearbyEnemies.Add(e);
                }
                _scanTimer = 0.2f;
            }

            float pullForce = (25f + SingularityPlugin.PlayerLevel * 2f) * Time.deltaTime; 
            float orbitForce = 20f * Time.deltaTime;

            foreach (var enemy in _nearbyEnemies) {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                Vector3 toPlayer = transform.position - enemy.transform.position;
                float dist = toPlayer.magnitude;
                Vector3 dirToPlayer = toPlayer.normalized;
                Vector3 orbitDir = Vector3.Cross(dirToPlayer, Vector3.forward);

                if (dist > _currentRadius) {
                    enemy.transform.position += (dirToPlayer * pullForce) + (orbitDir * (orbitForce * 0.3f));
                } else {
                    enemy.transform.position -= (dirToPlayer * (pullForce * 0.5f)); 
                    enemy.transform.position += (orbitDir * orbitForce); 
                }
            }
        }

        void HandleInput()
        {
            if (SingularityPlugin.IsOverheated) {
                SingularityPlugin.IsActive = false;
                return;
            }
            if (Input.GetKey(KeyCode.G)) SingularityPlugin.IsActive = true;
            if (Input.GetKeyUp(KeyCode.G)) {
                if (SingularityPlugin.IsActive) {
                    DischargeBlast();
                    SingularityPlugin.IsActive = false;
                }
            }
        }

        void ManageEnergy()
        {
            if (SingularityPlugin.IsActive) {
                float burden = _nearbyEnemies.Count * SingularityPlugin.DrainPerEnemy;
                _currentDrainRate = SingularityPlugin.BaseDrain + burden;
                CurrentEnergy -= _currentDrainRate * Time.deltaTime;
                float energyPct = CurrentEnergy / SingularityPlugin.MaxEnergy;
                _currentRadius = Mathf.Lerp(_minRadius, _maxRadius, energyPct);
                if (CurrentEnergy <= 0f) TriggerMeltdown(); 
            } else {
                float recharge = SingularityPlugin.RechargeRate + (SingularityPlugin.PlayerLevel * 0.5f);
                CurrentEnergy = Mathf.Min(SingularityPlugin.MaxEnergy, CurrentEnergy + recharge * Time.deltaTime);
                if (CurrentEnergy >= SingularityPlugin.MaxEnergy) SingularityPlugin.IsOverheated = false;
            }
        }

        void UpdateVisuals()
        {
            if (_fieldVisual == null || _particleSystem == null) return;
            bool visible = SingularityPlugin.IsActive || (SingularityPlugin.IsOverheated && CurrentEnergy < SingularityPlugin.MaxEnergy);
            if (_fieldVisual.activeSelf != visible) _fieldVisual.SetActive(visible);

            if (visible) {
                var shape = _particleSystem.shape;
                shape.radius = _currentRadius;
                var emission = _particleSystem.emission;
                emission.rateOverTime = 20f + (_currentRadius * 10f); 

                Color baseColor = Color.cyan;
                if (SingularityPlugin.IsOverheated) baseColor = Color.red;
                else if (CurrentEnergy < 30f) baseColor = Color.Lerp(Color.red, Color.yellow, Mathf.PingPong(Time.time * 5f, 1f));

                baseColor.a = 0.5f; 
                var main = _particleSystem.main;
                main.startColor = baseColor;
            }
        }

        void DischargeBlast()
        {
            foreach (var enemy in _nearbyEnemies) {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.z = 0; 
                if (toEnemy.sqrMagnitude < 0.001f) toEnemy = Vector3.right; 
                Vector3 dir = toEnemy.normalized;
                float dist = toEnemy.magnitude;
                
                float power = SingularityPlugin.BlastPush + (SingularityPlugin.SingularityLevel * 0.2f);
                float pushDist = Mathf.Clamp(power * (3.0f / (dist + 1f)), 2.0f, 8.0f);
                enemy.transform.position += dir * pushDist; 

                var h = enemy.GetComponent<HealthComponent>();
                if (h != null) {
                    bool died;
                    h.TakeDamage(dir, _blastDamage, out died, true, false, false);
                }
            }
        }

        void TriggerMeltdown()
        {
            CurrentEnergy = 0f;
            SingularityPlugin.IsActive = false;
            SingularityPlugin.IsOverheated = true;
            if (_myHealth != null) {
                bool died;
                _myHealth.TakeDamage(Vector2.zero, SingularityPlugin.MeltdownSelfDamage, out died, true, false, false);
            }
            DischargeBlast(); 
        }
        
        void OnGUI()
        {
            if (SingularityPlugin.IsInShop || SingularityPlugin.IsInCodingScreen) return;
            float width = 300f;
            float height = 12f; 
            float padding = 8f;
            float x = 20f;
            float y = 20f;

            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.4f);
            GUI.DrawTexture(new Rect(x - padding, y - padding, width + padding*2, height + padding*2), Texture2D.whiteTexture);

            float pct = CurrentEnergy / SingularityPlugin.MaxEnergy;
            Color barColor = Color.cyan;
            if (SingularityPlugin.IsOverheated) barColor = Color.red;
            else if (!SingularityPlugin.IsActive) barColor = Color.grey;
            else if (CurrentEnergy < 30f) barColor = Color.yellow;
            
            barColor.a = 0.6f; 
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(x, y, width * pct, height), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperLeft;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 10;
            
            string status = SingularityPlugin.IsActive ? "ACTIVE" : "OFF";
            if (SingularityPlugin.IsOverheated) status = "OVERHEATED";
            int displayTier = _lastLevel >= 0 ? _lastLevel : 0;
            string text = $"VORTEX [{status}] TIER {displayTier}: {(int)CurrentEnergy}%";
            GUI.Label(new Rect(x, y - 18f, width, 20f), text, style);
        }
    }
}