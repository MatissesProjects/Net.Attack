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

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
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
            if (Term == "HYPER_SPEED_DESC") { __result = "Gives +100% Movement Speed per stack."; return false; }
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
                    DatabaseModifierPlugin.Log.LogWarning("[Selection] Forced Hijack in Message.");
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
        private float _lastSpeedBoost = -1f;
        private MonoBehaviour _movementComponent;
        private float _checkTimer = 0f;
        private float _originalSpeed = -1f;

        void Start()
        {
            foreach(var mb in GetComponents<MonoBehaviour>()) {
                if (mb.GetType().Name == "PlayerMovement") { _movementComponent = mb; break; }
            }
        }

        void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f) { _checkTimer = 0.5f; UpdateSpeedBoost(); }
        }

        void UpdateSpeedBoost()
        {
            int count = 0;
            HashSet<object> seen = new HashSet<object>();
            try {
                var asm = typeof(DatabaseUpgradeBuilder).Assembly;
                var rdcType = asm.GetType("BRG.DataManagement.RunDataController");
                var instance = rdcType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                
                if (instance != null) {
                    var methods = rdcType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var getRunMethod = methods.FirstOrDefault(m => m.Name.Contains("GetRunUpgrades"));
                    count += CountInList(getRunMethod?.Invoke(instance, null) as IList, seen);
                    
                    var getNodeMethod = methods.FirstOrDefault(m => m.Name == "GetNodes" || m.Name == "GetAllNodes");
                    count += CountInList(getNodeMethod?.Invoke(instance, null) as IList, seen);
                }
            } catch {}

            // Multiply by 10.0f for "Super Clear" effect
            float boostValue = count * 10.0f;
            if (boostValue != _lastSpeedBoost) {
                ApplySpeedBoost(boostValue);
                _lastSpeedBoost = boostValue;
            }
        }

        int CountInList(IList list, HashSet<object> seen)
        {
            if (list == null) return 0;
            int c = 0;
            foreach (var item in list) {
                if (item == null || seen.Contains(item)) continue;
                seen.Add(item);
                var fields = item.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var idFi = fields.FirstOrDefault(f => f.Name == "id" || f.Name == "_id");
                var id = idFi?.GetValue(item) as string;
                if (id != null && id.Contains(DatabaseModifierPlugin.CUSTOM_ID)) c++;
            }
            return c;
        }

        void ApplySpeedBoost(float boost)
        {
            if (_movementComponent == null) return;
            try {
                var fields = _movementComponent.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var speedFi = fields.FirstOrDefault(f => f.Name == "_speed" || f.Name == "speed" || f.Name == "_moveSpeed");
                if (speedFi != null) {
                    float currentSpeed = (float)speedFi.GetValue(_movementComponent);
                    if (_originalSpeed < 0) _originalSpeed = currentSpeed;
                    float newSpeed = _originalSpeed * (1f + boost);
                    speedFi.SetValue(_movementComponent, newSpeed);
                    DatabaseModifierPlugin.Log.LogWarning($"[CustomBehavior] SPEED: {newSpeed:F2} (Stacks: {boost/10f}, Boost: {boost*100}%)");
                }
            } catch {}
        }
    }
}