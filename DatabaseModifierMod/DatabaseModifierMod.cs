using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BRG.DataManagement;
using BRG.Gameplay.Units;

namespace DatabaseModifierMod
{
    [BepInPlugin("com.matissetec.databasemodifier", "Database Modifier (Custom Upgrade)", "1.5.3")]
    public class DatabaseModifierPlugin : BaseUnityPlugin
    {
        public static DatabaseModifierPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public const string SPEED_ID = "HYPER_SPEED_V3_ID";
        public const string BLADE_ID = "VORTEX_BLADE_V1_ID";
        
        public static ScriptableObject SpeedTemplate;
        public static ScriptableObject BladeTemplate;
        
        public static int SpeedStacks = 0;
        public static int BladeStacks = 0;
        public static int PlayerLevel = 1;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            SpeedStacks = 0;
            BladeStacks = 0;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.databasemodifier");
                PatchSafe(harmony, "BRG.DataManagement.DatabaseUpgradeBuilder", "BuildUpgrades", typeof(UpgradeDatabasePatch));
                PatchSafe(harmony, "BRG.UI.UpgradeShop", "SetupShop", typeof(ShopHijackPatch));
                PatchSafe(harmony, "BRG.Gameplay.Upgrades.RunUpgradeShopController", "OnUpgradeSelected", typeof(SelectionTrackerPatch));
                
                var i2Loc = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                if (i2Loc != null) {
                    var mGetTranslation = i2Loc.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                            .FirstOrDefault(m => m.Name == "GetTranslation" && m.GetParameters().Length > 0);
                    if (mGetTranslation != null) {
                        harmony.Patch(mGetTranslation, new HarmonyMethod(AccessTools.Method(typeof(StringLocPatch), "Prefix")));
                    }
                }

                harmony.PatchAll(typeof(PlayerPatch));
                Log.LogInfo(">>> DATABASE MODIFIER V1.5.3 ONLINE <<<");
            } catch (Exception e) {
                Log.LogError("Patching Failed: " + e);
            }
        }

        void PatchSafe(Harmony harmony, string className, string methodName, Type patchType)
        {
            try {
                var method = AccessTools.Method(className + ":" + methodName);
                if (method != null) {
                    MethodInfo prefix = patchType.GetMethod("Prefix");
                    MethodInfo postfix = patchType.GetMethod("Postfix");
                    if (prefix == null && postfix == null) return;
                    harmony.Patch(method, prefix != null ? new HarmonyMethod(prefix) : null, postfix != null ? new HarmonyMethod(postfix) : null);
                    Log.LogInfo($"Successfully patched {className}:{methodName}");
                } 
            } catch (Exception e) {
                Log.LogError($"Error patching {className}:{methodName}: {e.Message}");
            }
        }
    }

    public static class StringLocPatch
    {
        public static bool Prefix(string Term, ref string __result)
        {
            if (string.IsNullOrEmpty(Term)) return true;
            if (Term == "HYPER_SPEED_KEY") { __result = "HYPER SPEED V3"; return false; }
            if (Term == "HYPER_SPEED_DESC") { __result = "Gives +1000% Movement Speed and Grows Player."; return false; }
            if (Term == "VORTEX_BLADE_KEY") { __result = "VORTEX BLADE"; return false; }
            if (Term == "VORTEX_BLADE_DESC") { __result = "Spectral blades orbit you, slicing through enemies."; return false; }
            return true;
        }
    }

    public static class UpgradeDatabasePatch
    {
        public static void Postfix(object __instance)
        {
            try {
                if (DatabaseModifierPlugin.SpeedTemplate != null) return;

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var listFi = __instance.GetType().GetFields(flags).FirstOrDefault(f => f.Name == "_runUpgrades" || f.Name == "_gameplayUpgrades");
                var list = listFi?.GetValue(__instance) as IList;

                if (list != null && list.Count > 0) {
                    var template = list[0] as ScriptableObject;
                    if (template != null) {
                        DatabaseModifierPlugin.SpeedTemplate = UnityEngine.Object.Instantiate(template);
                        ApplyMetadata(DatabaseModifierPlugin.SpeedTemplate, DatabaseModifierPlugin.SPEED_ID, "HYPER_SPEED_KEY", "HYPER_SPEED_DESC");
                        
                        DatabaseModifierPlugin.BladeTemplate = UnityEngine.Object.Instantiate(template);
                        ApplyMetadata(DatabaseModifierPlugin.BladeTemplate, DatabaseModifierPlugin.BLADE_ID, "VORTEX_BLADE_KEY", "VORTEX_BLADE_DESC");
                        
                        DatabaseModifierPlugin.Log.LogWarning("Master Templates Created.");
                    }
                }
            } catch {}
        }

        public static void ApplyMetadata(ScriptableObject obj, string id, string nameKey, string descKey)
        {
            Type type = obj.GetType();
            SetField(obj, type, "id", id);
            SetField(obj, type, "_id", id);
            SetField(obj, type, "_name", nameKey);
            SetField(obj, type, "_nameKey", nameKey);
            SetField(obj, type, "_description", descKey);
            SetField(obj, type, "_descriptionKey", descKey);
            SetField(obj, type, "_tooltipkey", descKey);
            
            string[] levelFields = { "_level", "level", "_currentLevel", "currentLevel", "_buyCount", "buyCount", "_upgradeLevel", "upgradeLevel" };
            foreach (var f in levelFields) SetField(obj, type, f, 0);
            
            SetField(obj, type, "_price", 1);
            SetField(obj, type, "_basePrice", 1);
            SetField(obj, type, "_maxCount", 5);
            SetField(obj, type, "_maxUpgradeLevel", 5);
            
            foreach (var w in new[] { "gameplayUpgrade", "nodeUpgrade", "metaUpgrade", "upgrade" }) ModifyWrapper(obj, type, w, descKey);

            SetField(obj, type, "action", null);
            SetField(obj, type, "attributeData", null);
        }

        static void ModifyWrapper(object obj, Type type, string fieldName, string descKey)
        {
            var fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(f => f.Name == fieldName || f.Name == "_" + fieldName);
            var wrapper = fi?.GetValue(obj);
            if (wrapper == null) return;
            
            Type wType = wrapper.GetType();
            foreach(var f in wType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (f.FieldType == typeof(float)) f.SetValue(wrapper, 0f);
                if (f.FieldType == typeof(string) && f.Name.Contains("Key")) f.SetValue(wrapper, descKey);
            }
            SetField(wrapper, wType, "attributeData", null);
        }

        static bool SetField(object obj, Type type, string name, object value)
        {
            if (type == null || type == typeof(UnityEngine.Object) || type == typeof(ScriptableObject)) return false;
            var fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(f => f.Name == name || f.Name == "_" + name);
            if (fi != null) { try { fi.SetValue(obj, value); return true; } catch {} }
            var pi = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.Name == name || p.Name == "_" + name);
            if (pi != null && pi.CanWrite) { try { pi.SetValue(obj, value); return true; } catch {} }
            return SetField(obj, type.BaseType, name, value);
        }
    }

    public static class ShopHijackPatch
    {
        public static void Prefix(object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            var upgrades = __args[0] as IList;
            if (upgrades == null || upgrades.Count < 2) return;

            try {
                if (DatabaseModifierPlugin.SpeedStacks < 5) upgrades[0] = DatabaseModifierPlugin.SpeedTemplate;
                if (DatabaseModifierPlugin.BladeStacks < 5) upgrades[1] = DatabaseModifierPlugin.BladeTemplate;
            } catch {}
        }
    }

    public static class SelectionTrackerPatch
    {
        public static void Prefix(object __instance, object[] __args)
        {
            try {
                if (__args == null || __args.Length == 0) return;
                object message = __args[0];
                if (message == null) return;

                var listFi = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(f => f.Name == "_upgradeSOs");
                var list = listFi?.GetValue(__instance) as IList;
                if (list != null && list.Count >= 2) {
                    if (DatabaseModifierPlugin.SpeedTemplate != null) list[0] = DatabaseModifierPlugin.SpeedTemplate;
                    if (DatabaseModifierPlugin.BladeTemplate != null) list[1] = DatabaseModifierPlugin.BladeTemplate;
                }

                var msgFields = message.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var indexFi = msgFields.FirstOrDefault(f => f.Name.ToLower().Contains("index"));
                int selectedIndex = (indexFi != null) ? (int)indexFi.GetValue(message) : -1;
                var upgradeFi = msgFields.FirstOrDefault(f => f.Name.Contains("Upgrade") || f.FieldType.Name.Contains("SO"));

                if (selectedIndex == 0 || (upgradeFi != null && upgradeFi.GetValue(message) == DatabaseModifierPlugin.SpeedTemplate)) {
                    if (DatabaseModifierPlugin.SpeedStacks < 5) DatabaseModifierPlugin.SpeedStacks++;
                } else if (selectedIndex == 1 || (upgradeFi != null && upgradeFi.GetValue(message) == DatabaseModifierPlugin.BladeTemplate)) {
                    if (DatabaseModifierPlugin.BladeStacks < 5) DatabaseModifierPlugin.BladeStacks++;
                }
            } catch {}
        }
    }

    public static class PlayerPatch
    {
        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        static void Postfix(Player __instance)
        {
            if (__instance.gameObject.GetComponent<CustomUpgradeBehavior>() == null)
                __instance.gameObject.AddComponent<CustomUpgradeBehavior>();
        }
    }

    public class CustomUpgradeBehavior : MonoBehaviour
    {
        private MonoBehaviour _movementComponent;
        private SpriteRenderer _spriteRenderer;
        
        private float _originalSpeed = -1f;
        private Vector3 _originalScale = Vector3.one;
        private bool _statsCaptured = false;

        private List<GameObject> _blades = new List<GameObject>();
        private float _orbitAngle = 0f;
        private float _damageTimer = 0f;

        void Start()
        {
            foreach(var mb in GetComponents<MonoBehaviour>()) {
                if (mb.GetType().Name == "PlayerMovement") { _movementComponent = mb; break; }
            }
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;
        }

        void Update()
        {
            if (!_statsCaptured || _originalSpeed <= 0) CaptureOriginalStats();

            int speedStacks = Mathf.Min(DatabaseModifierPlugin.SpeedStacks, 5);
            if (_statsCaptured && speedStacks > 0) ApplySpeedEffects(speedStacks);

            UpdatePlayerLevel();
            UpdateBlades(Mathf.Min(DatabaseModifierPlugin.BladeStacks, 5));
        }

        void UpdatePlayerLevel()
        {
            try {
                var p = GetComponent<Player>();
                if (p != null) {
                    var pi = typeof(Player).GetProperty("Level", BindingFlags.Public | BindingFlags.Instance);
                    if (pi != null) DatabaseModifierPlugin.PlayerLevel = (int)pi.GetValue(p);
                }
            } catch {}
        }

        void CaptureOriginalStats()
        {
            if (_movementComponent == null) return;
            try {
                var type = _movementComponent.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var speedFi = fields.FirstOrDefault(f => f.Name == "_speed" || f.Name == "speed" || f.Name == "_moveSpeed");
                if (speedFi != null) {
                    float val = (float)speedFi.GetValue(_movementComponent);
                    if (val > 0) { _originalSpeed = val; _statsCaptured = true; }
                }
            } catch {}
        }

        void ApplySpeedEffects(int stacks)
        {
            if (_movementComponent != null) {
                var fields = _movementComponent.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var speedFi = fields.FirstOrDefault(f => f.Name == "_speed" || f.Name == "speed" || f.Name == "_moveSpeed");
                if (speedFi != null) speedFi.SetValue(_movementComponent, _originalSpeed * (1f + (stacks * 10.0f)));
            }
            transform.localScale = _originalScale * (1f + (stacks * 0.5f));
            if (_spriteRenderer != null) {
                float t = Mathf.PingPong(Time.time * 5f, 1f);
                _spriteRenderer.color = Color.Lerp(Color.white, Color.magenta, t);
            }
        }

        void UpdateBlades(int targetCount)
        {
            while (_blades.Count < targetCount) {
                GameObject b = new GameObject("VortexBlade_" + _blades.Count);
                
                var lr = b.AddComponent<LineRenderer>();
                lr.startWidth = 0.25f; lr.endWidth = 0.05f; 
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = new Color(0, 1, 1, 0.8f); lr.endColor = new Color(0, 0.5f, 1f, 0.4f);
                lr.positionCount = 2;
                
                var ps = b.AddComponent<ParticleSystem>();
                var psr = b.GetComponent<ParticleSystemRenderer>();
                if (psr == null) psr = b.AddComponent<ParticleSystemRenderer>();
                
                // Try to find a better shader for glow, fallback to default
                Shader pShader = Shader.Find("Particles/Standard Unlit");
                if (pShader == null) pShader = Shader.Find("Sprites/Default");
                psr.material = new Material(pShader);

                // Use a circle/glow sprite if available
                try {
                    var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                    var circle = sprites.FirstOrDefault(s => s.name.ToLower().Contains("circle") || s.name.ToLower().Contains("glow") || s.name.ToLower() == "knob");
                    if (circle != null) psr.material.mainTexture = circle.texture;
                } catch {}

                var main = ps.main;
                // Bright colors for fake glow, kept at lower alpha
                main.startColor = new Color(0.2f, 1f, 1f, 0.4f); 
                main.startSize = 0.3f; 
                main.startSpeed = 0.5f; 
                main.startLifetime = 0.2f; 
                main.maxParticles = 500;
                main.simulationSpace = ParticleSystemSimulationSpace.World; 
                
                var em = ps.emission; 
                em.rateOverTime = 200f; 
                
                var sh = ps.shape; 
                sh.shapeType = ParticleSystemShapeType.Sphere; 
                sh.radius = 0.6f; 

                var col = ps.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.cyan, 0.0f), new GradientColorKey(Color.white, 0.5f), new GradientColorKey(Color.blue, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.4f, 0.0f), new GradientAlphaKey(0.25f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) }
                );
                col.color = grad;
                
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(0.0f, 0.5f);
                curve.AddKey(0.5f, 1.0f);
                curve.AddKey(1.0f, 0.0f);
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

                _blades.Add(b);
            }

            if (targetCount <= 0) return;

            _orbitAngle += Time.deltaTime * 150f;
            float pulse = 1f + (Mathf.Sin(Time.time * 15f) * 0.2f); 

            // Damage Tick Logic
            bool isDamageTick = false;
            _damageTimer -= Time.deltaTime;
            if (_damageTimer <= 0) {
                isDamageTick = true;
                // Interval: 250ms base, -15% per stack (compounding)
                _damageTimer = 0.25f * Mathf.Pow(0.85f, targetCount - 1);
            }

            for (int i = 0; i < _blades.Count; i++) {
                float angle = _orbitAngle + (i * (360f / _blades.Count));
                float rad = angle * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * 2.5f;
                _blades[i].transform.position = pos;
                
                var lr = _blades[i].GetComponent<LineRenderer>();
                Vector3 dir = (pos - transform.position).normalized;
                
                lr.SetPosition(0, pos - dir * 0.4f * pulse);
                lr.SetPosition(1, pos + dir * 0.8f * pulse); 
                lr.startWidth = 0.3f * pulse;
                
                // Pulsating Alpha (Back to Normal)
                float alphaPulse = 0.6f + (Mathf.Sin(Time.time * 20f) * 0.3f);
                lr.startColor = new Color(0, 1, 1, alphaPulse);
                lr.endColor = new Color(0, 0.5f, 1f, alphaPulse * 0.5f);

                if (isDamageTick) {
                    DamageNearbyEnemies(pos, 1.4f);
                }
            }
        }

        void DamageNearbyEnemies(Vector3 pos, float range)
        {
            foreach (var e in FindObjectsOfType<Enemy>()) {
                if (e != null && Vector3.Distance(e.transform.position, pos) <= range) {
                    var h = e.GetComponent<HealthComponent>();
                    if (h != null) {
                        bool died;
                        // Scaled damage per hit
                        float damage = (10f + (DatabaseModifierPlugin.BladeStacks * 5f)) * (1f + (DatabaseModifierPlugin.PlayerLevel * 0.2f));
                        
                        // Only apply knockback if we have at least 3 stacks
                        Vector3 pushDir = Vector3.zero;
                        if (DatabaseModifierPlugin.BladeStacks >= 3) {
                            pushDir = (e.transform.position - pos).normalized;
                        }

                        h.TakeDamage(pushDir, damage, out died, true, false, false);
                    }
                }
            }
        }
    }
}