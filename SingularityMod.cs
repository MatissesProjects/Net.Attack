using System;
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

namespace TestLibrary
{
    [BepInPlugin("com.yourname.singularity_v9", "Singularity Vortex", "1.9.0")]
    public class SingularityPlugin : BaseUnityPlugin, IMessageReceiver
    {
        public static SingularityPlugin Instance;
        
        // --- TUNED SETTINGS ---
        public static bool IsActive = false; 
        public static bool IsOverheated = false;

        public static float MaxEnergy = 100f;
        public static float BaseDrain = 8f;         
        public static float DrainPerEnemy = 3.0f;   
        public static float RechargeRate = 6f;     
        
        // DYNAMIC RADIUS
        public static float BaseSafeRadius = 4.5f;  
        public static float MinSafeRadius = 1.5f;   
        
        // ABILITIES
        public static float BlastPush = .5f;     
        public static float BlastDamage = .5f;
        public static float MeltdownSelfDamage = 25f; 

        // STATE
        public static bool IsInShop = false;
        public static bool IsInCodingScreen = false;
        public static int PlayerLevel = 1;

        void Awake()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(typeof(SingularityPlugin));
            Logger.LogInfo(">>> SINGULARITY V9: STABLE EDITION <<<");
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix] 
        static void PlayerAwake_Patch(Player __instance)
        {
            if (__instance.gameObject.GetComponent<SingularityBehavior>() == null)
            {
                __instance.gameObject.AddComponent<SingularityBehavior>();
            }
        }

        [HarmonyPatch(typeof(Enemy), "OnDisable")]
        [HarmonyPrefix]
        static void EnemyDeath_Patch(Enemy __instance)
        {
            if (!IsActive || IsOverheated || IsInShop || IsInCodingScreen) return;

            var behavior = FindObjectOfType<SingularityBehavior>();
            if (behavior == null) return;

            float energyCost = 10f;
            if (behavior.CurrentEnergy >= energyCost)
            {
                var healthComp = __instance.GetComponent<HealthComponent>();
                if (healthComp != null && healthComp.CurrentHealth <= 0)
                {
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
        
        private List<Enemy> _nearbyEnemies = new List<Enemy>();
        private HealthComponent _myHealth; 
        
        // Visuals
        private GameObject _fieldVisual;
        private ParticleSystem _particleSystem;

        // Reflection Cache
        private static FieldInfo _fiOnShopChangedMsg;
        private float _uiUpdateTimer = 0f; // Throttle UI checks
        private float _movementHoldTimer = 0f; // Heuristic: Time spending moving

        void Start()
        {
            _myHealth = GetComponent<HealthComponent>();
            CreateVisuals();
        }

        void CreateVisuals()
        {
            _fieldVisual = new GameObject("SingularityParticles");
            _fieldVisual.transform.SetParent(this.transform, false);
            _fieldVisual.transform.localPosition = Vector3.zero;
            
            _particleSystem = _fieldVisual.AddComponent<ParticleSystem>();
            var renderer = _fieldVisual.GetComponent<ParticleSystemRenderer>();
            
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                renderer.material = new Material(shader);

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
            var p = FindObjectOfType<Player>();
            if (p != null)
            {
                var h = p.GetComponent<HealthComponent>();
                if (h != null) h.Heal(amount, true);
            }
        }

        public void ConsumeEnergy(float amount)
        {
            CurrentEnergy -= amount;
            if (CurrentEnergy < 0) CurrentEnergy = 0;
        }

        void Update()
        {
            // --- HEURISTIC: Coding Screen Detection ---
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.E))
            {
                SingularityPlugin.IsInCodingScreen = true;
            }

            bool isMoving = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
            if (isMoving)
            {
                _movementHoldTimer += Time.deltaTime;
                if (_movementHoldTimer > 1.0f) // Threshold to confirm "Play Mode"
                {
                    SingularityPlugin.IsInCodingScreen = false;
                }
            }
            else
            {
                _movementHoldTimer = 0f;
            }

            // Throttle UI/Reflection checks to prevent lag/crashes
            _uiUpdateTimer -= Time.deltaTime;
            if (_uiUpdateTimer <= 0f)
            {
                UpdateUIStates();
                _uiUpdateTimer = 0.2f; // Check 5 times per second
            }

            if (SingularityPlugin.IsInShop || SingularityPlugin.IsInCodingScreen)
            {
                if (Input.GetKeyDown(KeyCode.G))
                {
                    Debug.Log($"[Singularity] Input Blocked! Shop: {SingularityPlugin.IsInShop}, Coding: {SingularityPlugin.IsInCodingScreen}");
                }

                if (_fieldVisual != null && _fieldVisual.activeSelf) _fieldVisual.SetActive(false);
                return;
            }

            HandleInput();
            ManageEnergy();
            UpdateVisuals();

            if (!SingularityPlugin.IsActive) return;

            // 1. SCAN
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _nearbyEnemies.Clear();
                var all = FindObjectsOfType<Enemy>();
                Vector3 myPos = transform.position;
                float scanRange = _currentRadius * 2.0f; 

                foreach (var e in all)
                {
                    if (e != null && Vector3.Distance(myPos, e.transform.position) <= scanRange)
                        _nearbyEnemies.Add(e);
                }
                _scanTimer = 0.2f;
            }

            // 2. VORTEX PHYSICS
            float pullForce = (25f + SingularityPlugin.PlayerLevel * 2f) * Time.deltaTime; 
            float orbitForce = 20f * Time.deltaTime;

            foreach (var enemy in _nearbyEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;

                Vector3 toPlayer = transform.position - enemy.transform.position;
                float dist = toPlayer.magnitude;
                Vector3 dirToPlayer = toPlayer.normalized;
                Vector3 orbitDir = Vector3.Cross(dirToPlayer, Vector3.forward);

                if (dist > _currentRadius)
                {
                    enemy.transform.position += (dirToPlayer * pullForce) + (orbitDir * (orbitForce * 0.3f));
                }
                else
                {
                    enemy.transform.position -= (dirToPlayer * (pullForce * 0.5f)); 
                    enemy.transform.position += (orbitDir * orbitForce); 
                }
            }
        }

        void UpdateUIStates()
        {
            try
            {
                // 1. SHOP STATE
                if (_fiOnShopChangedMsg == null)
                {
                    _fiOnShopChangedMsg = typeof(RunUpgradeShopController).GetField("_onShopChangedMessage", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                // Safety check for SceneReferences access
                RunUpgradeShopController shop = null;
                if (SceneReferences.Instance != null) 
                    shop = SceneReferences.Instance.RunUpgradeShopController;

                if (shop != null && _fiOnShopChangedMsg != null)
                {
                    object messageValue = _fiOnShopChangedMsg.GetValue(shop);
                    if (messageValue != null)
                    {
                        FieldInfo field = messageValue.GetType().GetField("IsShopOpen", BindingFlags.Instance | BindingFlags.Public);
                        if (field != null)
                        {
                            SingularityPlugin.IsInShop = (bool)field.GetValue(messageValue);
                        }
                    }
                }
                else
                {
                    SingularityPlugin.IsInShop = false;
                }

                // 2. CODING SCREEN STATE
                // CodingScreenController coding = FindObjectOfType<CodingScreenController>();
                // SingularityPlugin.IsInCodingScreen = (coding != null && coding.gameObject.activeInHierarchy);
                // SingularityPlugin.IsInCodingScreen = false; // Bypass flawed check - Handled in Update() heuristic

                // 3. PLAYER LEVEL SCALING
                Player p = FindObjectOfType<Player>();
                if (p != null)
                {
                    PropertyInfo pi = typeof(Player).GetProperty("Level", BindingFlags.Public | BindingFlags.Instance);
                    if (pi != null) SingularityPlugin.PlayerLevel = (int)pi.GetValue(p);
                }
            }
            catch (Exception)
            {
                // Suppress errors to prevent game crash loop
                // SingularityPlugin.IsInShop = false; 
            }
        }

        void HandleInput()
        {
            if (SingularityPlugin.IsOverheated)
            {
                if (Input.GetKeyDown(KeyCode.G)) Debug.Log("[Singularity] Input Blocked: OVERHEATED");
                SingularityPlugin.IsActive = false;
                return;
            }

            // Only use G key
            if (Input.GetKey(KeyCode.G))
            {
                if (!SingularityPlugin.IsActive) Debug.Log("[Singularity] G Pressed - ACTIVATING");
                SingularityPlugin.IsActive = true;
            }

            if (Input.GetKeyUp(KeyCode.G))
            {
                if (SingularityPlugin.IsActive)
                {
                    Debug.Log("[Singularity] G Released - DISCHARGE");
                    DischargeBlast();
                    SingularityPlugin.IsActive = false;
                }
            }
        }

        void ManageEnergy()
        {
            if (SingularityPlugin.IsActive)
            {
                float burden = _nearbyEnemies.Count * SingularityPlugin.DrainPerEnemy;
                _currentDrainRate = SingularityPlugin.BaseDrain + burden;

                CurrentEnergy -= _currentDrainRate * Time.deltaTime;

                float energyPct = CurrentEnergy / SingularityPlugin.MaxEnergy;
                _currentRadius = Mathf.Lerp(SingularityPlugin.MinSafeRadius, SingularityPlugin.BaseSafeRadius, energyPct);

                if (CurrentEnergy <= 0f)
                {
                    TriggerMeltdown(); 
                }
            }
            else
            {
                // Recharge faster at higher levels
                float recharge = SingularityPlugin.RechargeRate + (SingularityPlugin.PlayerLevel * 0.5f);
                CurrentEnergy += recharge * Time.deltaTime;
                
                if (CurrentEnergy >= SingularityPlugin.MaxEnergy)
                {
                    CurrentEnergy = SingularityPlugin.MaxEnergy;
                    SingularityPlugin.IsOverheated = false;
                }
            }
        }

        void UpdateVisuals()
        {
            if (_fieldVisual == null || _particleSystem == null) return;

            bool visible = SingularityPlugin.IsActive || (SingularityPlugin.IsOverheated && CurrentEnergy < SingularityPlugin.MaxEnergy);
            
            if (_fieldVisual.activeSelf != visible)
                _fieldVisual.SetActive(visible);

            if (visible)
            {
                var shape = _particleSystem.shape;
                shape.radius = _currentRadius;

                var emission = _particleSystem.emission;
                emission.rateOverTime = 20f + (_currentRadius * 10f); 

                Color baseColor = Color.cyan;
                if (SingularityPlugin.IsOverheated) baseColor = Color.red;
                else if (CurrentEnergy < 30f) 
                {
                     baseColor = Color.Lerp(Color.red, Color.yellow, Mathf.PingPong(Time.time * 5f, 1f));
                }

                baseColor.a = 0.5f; 
                var main = _particleSystem.main;
                main.startColor = baseColor;
            }
        }

        void DischargeBlast()
        {
            foreach (var enemy in _nearbyEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;

                Vector3 toEnemy = enemy.transform.position - transform.position;
                toEnemy.z = 0; 

                if (toEnemy.sqrMagnitude < 0.001f) toEnemy = Vector3.right; 
                
                Vector3 dir = toEnemy.normalized;
                float dist = toEnemy.magnitude;
                
                // SCALE: Blast push increases with level
                float power = SingularityPlugin.BlastPush + (SingularityPlugin.PlayerLevel * 0.2f);
                float pushDist = Mathf.Clamp(power * (3.0f / (dist + 1f)), 2.0f, 8.0f);
                
                enemy.transform.position += dir * pushDist; 

                var h = enemy.GetComponent<HealthComponent>();
                if (h != null)
                {
                    bool died;
                    // SCALE: Damage increases with level
                    float dmg = SingularityPlugin.BlastDamage + (SingularityPlugin.PlayerLevel * 2f);
                    h.TakeDamage(dir, dmg, out died, true, false, false);
                }
            }
        }

        void TriggerMeltdown()
        {
            CurrentEnergy = 0f;
            SingularityPlugin.IsActive = false;
            SingularityPlugin.IsOverheated = true;

            if (_myHealth != null)
            {
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
            
            string text = $"VORTEX [{status}] LVL {SingularityPlugin.PlayerLevel}: {(int)CurrentEnergy}%";
            GUI.Label(new Rect(x, y - 18f, width, 20f), text, style);
        }
    }
}