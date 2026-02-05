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
        public const string CUSTOM_ID = "HYPER_SPEED_V3_ID";
        
        // Master template to ensure consistency
        public static ScriptableObject MasterTemplate;
        
        // Global Counter for reliability
        public static int StackCount = 0;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            StackCount = 0; // Reset on load
            
            try {
                Harmony harmony = new Harmony("com.matissetec.databasemodifier");
                
                // 1. Patch Builders to find a template and register our ID
                PatchSafe(harmony, "BRG.DataManagement.DatabaseUpgradeBuilder", "BuildUpgrades", typeof(UpgradeDatabasePatch));
                
                // 2. UI Hijack
                PatchSafe(harmony, "BRG.UI.UpgradeShop", "SetupShop", typeof(ShopHijackPatch));
                
                // 3. Selection Hijack (Critical for Logic)
                PatchSafe(harmony, "BRG.Gameplay.Upgrades.RunUpgradeShopController", "OnUpgradeSelected", typeof(SelectionTrackerPatch));
                
                // Localization
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
            return true;
        }
    }

    public static class UpgradeDatabasePatch
    {
        public static void Postfix(object __instance)
        {
            try {
                if (DatabaseModifierPlugin.MasterTemplate != null) return;

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var fields = __instance.GetType().GetFields(flags);
                var listFi = fields.FirstOrDefault(f => f.Name == "_runUpgrades" || f.Name == "_gameplayUpgrades");
                var list = listFi?.GetValue(__instance) as IList;

                if (list != null && list.Count > 0) {
                    var template = list[0] as ScriptableObject;
                    if (template != null) {
                        var hijacked = UnityEngine.Object.Instantiate(template);
                        hijacked.name = "MASTER_HYPER_SPEED_TEMPLATE";
                        ApplyMetadata(hijacked, hijacked.GetType());
                        DatabaseModifierPlugin.MasterTemplate = hijacked;
                        DatabaseModifierPlugin.Log.LogWarning("Master Template Created and Cached.");
                    }
                }
            } catch {}
        }

        public static void ApplyMetadata(ScriptableObject obj, Type type)
        {
            SetField(obj, type, "id", DatabaseModifierPlugin.CUSTOM_ID);
            SetField(obj, type, "_id", DatabaseModifierPlugin.CUSTOM_ID);
            SetField(obj, type, "_name", "HYPER_SPEED_KEY");
            SetField(obj, type, "_nameKey", "HYPER_SPEED_KEY");
            SetField(obj, type, "_description", "HYPER_SPEED_DESC");
            SetField(obj, type, "_descriptionKey", "HYPER_SPEED_DESC");
            SetField(obj, type, "_tooltipkey", "HYPER_SPEED_DESC");
            SetField(obj, type, "_price", 1);
            SetField(obj, type, "_maxCount", 99);
            SetField(obj, type, "_maxUpgradeLevel", 99);
            
            foreach (var f in new[] { "icon", "sprite", "uiIcon", "upgradeIcon" }) SetField(obj, type, f, null);
            foreach (var w in new[] { "gameplayUpgrade", "nodeUpgrade", "metaUpgrade", "upgrade" }) ModifyWrapper(obj, type, w);

            SetField(obj, type, "action", null);
            SetField(obj, type, "attributeData", null);
        }

        static void ModifyWrapper(object obj, Type type, string fieldName)
        {
            var fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .FirstOrDefault(f => f.Name == fieldName || f.Name == "_" + fieldName);
            var wrapper = fi?.GetValue(obj);
            if (wrapper == null) return;
            
            Type wType = wrapper.GetType();
            foreach(var f in wType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                if (f.FieldType == typeof(float)) f.SetValue(wrapper, 0f);
                if (f.FieldType == typeof(string) && f.Name.Contains("Key")) f.SetValue(wrapper, "HYPER_SPEED_DESC");
            }
            SetField(wrapper, wType, "attributeData", null);
        }

        static bool SetField(object obj, Type type, string name, object value)
        {
            if (type == null || type == typeof(UnityEngine.Object) || type == typeof(ScriptableObject)) return false;
            var fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .FirstOrDefault(f => f.Name == name || f.Name == "_" + name);
            if (fi != null) { try { fi.SetValue(obj, value); return true; } catch {} }
            var pi = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                         .FirstOrDefault(p => p.Name == name || p.Name == "_" + name);
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
            if (upgrades == null || upgrades.Count == 0 || DatabaseModifierPlugin.MasterTemplate == null) return;

            try {
                upgrades[0] = DatabaseModifierPlugin.MasterTemplate;
                DatabaseModifierPlugin.Log.LogInfo("Hijacked Shop UI Slot 0.");
            } catch (Exception e) {
                DatabaseModifierPlugin.Log.LogError("Shop Hijack Failed: " + e.Message);
            }
        }
    }

    public static class SelectionTrackerPatch
    {
        public static void Prefix(object __instance, object[] __args)
        {
            try {
                if (__args == null || __args.Length == 0 || DatabaseModifierPlugin.MasterTemplate == null) return;
                object message = __args[0];
                if (message == null) return;

                // 1. Sync Controller List
                var listFi = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                       .FirstOrDefault(f => f.Name == "_upgradeSOs");
                var list = listFi?.GetValue(__instance) as IList;
                if (list != null && list.Count > 0) list[0] = DatabaseModifierPlugin.MasterTemplate;

                // 2. Identify Selection
                var msgFields = message.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var indexFi = msgFields.FirstOrDefault(f => f.Name.ToLower().Contains("index"));
                int selectedIndex = (indexFi != null) ? (int)indexFi.GetValue(message) : -1;

                var upgradeFi = msgFields.FirstOrDefault(f => f.Name.Contains("Upgrade") || f.FieldType.Name.Contains("SO"));
                
                if (selectedIndex == 0 || (upgradeFi != null && upgradeFi.GetValue(message) == DatabaseModifierPlugin.MasterTemplate)) {
                    if (upgradeFi != null) upgradeFi.SetValue(message, DatabaseModifierPlugin.MasterTemplate);
                    
                    // INCREMENT GLOBAL COUNTER
                    DatabaseModifierPlugin.StackCount++;
                    DatabaseModifierPlugin.Log.LogWarning($"[Selection] HYPER SPEED SELECTED! Total Stacks: {DatabaseModifierPlugin.StackCount}");
                }
            } catch (Exception e) {
                DatabaseModifierPlugin.Log.LogError($"[Selection] JIT Failed: {e.Message}");
            }
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
        private float _lastStackCount = -1f;
        private MonoBehaviour _movementComponent;
        private SpriteRenderer _spriteRenderer;
        
        private float _originalSpeed = -1f;
        private Vector3 _originalScale = Vector3.one;
        private bool _statsCaptured = false;

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
            if (!_statsCaptured || _originalSpeed <= 0) {
                CaptureOriginalStats();
            }

            // Use the Global Counter directly
            int currentStacks = DatabaseModifierPlugin.StackCount;

            if (currentStacks != _lastStackCount) {
                _lastStackCount = currentStacks;
                DatabaseModifierPlugin.Log.LogWarning($"[CustomBehavior] APPLYING STACKS: {currentStacks}");
            }

            if (_statsCaptured && currentStacks > 0) {
                ApplyEffects(currentStacks);
            }
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
                    if (val > 0) {
                        _originalSpeed = val;
                        _statsCaptured = true;
                        DatabaseModifierPlugin.Log.LogInfo($"[CustomBehavior] Base Stats Captured. Speed: {_originalSpeed}");
                    }
                }
            } catch {}
        }

        void ApplyEffects(float stacks)
        {
            // 1. SPEED (10x per stack)
            if (_movementComponent != null) {
                try {
                    var fields = _movementComponent.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var speedFi = fields.FirstOrDefault(f => f.Name == "_speed" || f.Name == "speed" || f.Name == "_moveSpeed");
                    if (speedFi != null) {
                        float newSpeed = _originalSpeed * (1f + (stacks * 10.0f));
                        speedFi.SetValue(_movementComponent, newSpeed);
                    }
                } catch {}
            }

            // 2. SCALE (Grow)
            transform.localScale = _originalScale * (1f + (stacks * 0.5f));

            // 3. COLOR (Flash Magenta)
            if (_spriteRenderer != null) {
                float t = Mathf.PingPong(Time.time * 5f, 1f);
                _spriteRenderer.color = Color.Lerp(Color.white, Color.magenta, t);
            }
        }
    }
}