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
        public const string CUSTOM_UPGRADE_ID = "custom_mega_speed_boost_v3";

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.databasemodifier");
                
                // DB Builders
                PatchSafe(harmony, "BRG.DataManagement.DatabaseUpgradeBuilder", "BuildUpgrades", typeof(UpgradeDatabasePatch));
                
                // UI / Shop
                PatchSafe(harmony, "BRG.UI.UpgradeShop", "SetupShop", typeof(ShopHijackPatch));
                PatchSafe(harmony, "BRG.Gameplay.Upgrades.RunUpgradeShopController", "OnUpgradeSelected", typeof(SelectionTrackerPatch));
                
                // Localization: Simplify to avoid Ambiguous Match
                var i2Loc = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                if (i2Loc != null) {
                    var mGetTranslation = i2Loc.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                            .FirstOrDefault(m => m.Name == "GetTranslation" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string));
                    
                    if (mGetTranslation != null) {
                        harmony.Patch(mGetTranslation, new HarmonyMethod(AccessTools.Method(typeof(StringLocPatch), "Prefix")));
                        Log.LogInfo("Successfully patched LocalizationManager:GetTranslation");
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
            if (Term == "HYPER_SPEED_KEY") {
                __result = "HYPER SPEED V3";
                return false;
            }
            if (Term == "HYPER_SPEED_DESC" || Term == "HYPER_SPEED_TOOLTIP") {
                __result = "Gives +100% Movement Speed per stack.";
                return false;
            }
            return true;
        }
    }

    public static class UpgradeDatabasePatch
    {
        public static void Postfix(object __instance, MethodBase __originalMethod)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var type = __instance.GetType();
            string[] fieldsToTry = { "_runUpgrades", "_gameplayUpgrades" }; 

            foreach (var fieldName in fieldsToTry)
            {
                try {
                    var fields = type.GetFields(flags);
                    var field = fields.FirstOrDefault(f => f.Name == fieldName);
                    if (field == null) continue;
                    IList list = field.GetValue(__instance) as IList;
                    if (list == null || list.Count == 0) continue;
                    ModifyList(fieldName, field, __instance, list);
                } catch (Exception e) {
                    DatabaseModifierPlugin.Log.LogError($"Failed to modify {fieldName}: {e.Message}");
                }
            }
        }

        public static void ModifyList(string fieldName, FieldInfo field, object __instance, IList list)
        {
            object template = null;
            foreach (var item in list) if (item != null) { template = item; break; }
            if (template == null) return;

            Type itemType = template.GetType();
            var newObj = UnityEngine.Object.Instantiate((ScriptableObject)template);
            newObj.name = "HyperOverride_" + fieldName;

            ApplyMetadata(newObj, itemType, fieldName);

            if (list.IsFixedSize) {
                if (fieldName.Contains("gameplay") || fieldName.Contains("all")) return;
                var newArray = Array.CreateInstance(itemType, 1);
                newArray.SetValue(newObj, 0);
                field.SetValue(__instance, newArray);
            } else {
                list.Insert(0, newObj);
            }
        }

        public static void ApplyMetadata(ScriptableObject obj, Type type, string context)
        {
            string newId = DatabaseModifierPlugin.CUSTOM_UPGRADE_ID + "_" + context;
            string newKey = "HYPER_SPEED_KEY";
            string newDescKey = "HYPER_SPEED_DESC";

            // IDs
            SetField(obj, type, "id", newId);
            SetField(obj, type, "_id", newId);
            
            // Names
            SetField(obj, type, "_name", newKey);
            SetField(obj, type, "name", newKey);
            SetField(obj, type, "_localizedName", newKey);
            SetField(obj, type, "_nameKey", newKey);

            // Descriptions
            SetField(obj, type, "_description", newDescKey);
            SetField(obj, type, "_descriptionKey", newDescKey);
            SetField(obj, type, "_tooltipkey", newDescKey);
            SetField(obj, type, "_tooltipKey", newDescKey);

            // Mechanics
            SetField(obj, type, "_price", 1);
            SetField(obj, type, "_basePrice", 1);
            SetField(obj, type, "_maxCount", 99);
            SetField(obj, type, "_maxUpgradeLevel", 99);
            
            // Visuals
            string[] iconFields = { "icon", "sprite", "uiIcon", "uIIcon", "upgradeIcon" };
            foreach (var f in iconFields) SetField(obj, type, f, null);

            // Try all known wrapper names
            string[] wrappers = { "gameplayUpgrade", "nodeUpgrade", "metaUpgrade", "metaGameplayUpgrade", "metaNodeUpgrade", "upgrade", "data" };
            foreach (var w in wrappers) ModifyWrapper(obj, type, w);

            // Clear extra data
            SetField(obj, type, "action", null);
            SetField(obj, type, "attribute", 0);
            SetField(obj, type, "attributeType", 0);
            SetField(obj, type, "attributeData", null);
            SetField(obj, type, "_attributeData", null);
        }

        static void ModifyWrapper(object obj, Type type, string fieldName)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = type.GetFields(flags);
            var fi = fields.FirstOrDefault(f => f.Name == fieldName || f.Name == "_" + fieldName);
            if (fi == null) return;
            
            var wrapper = fi.GetValue(obj);
            if (wrapper == null) return;
            
            Type wType = wrapper.GetType();
            
            // Neutralize ALL float fields in the wrapper to be sure
            var wFields = wType.GetFields(flags);
            foreach(var f in wFields) {
                if (f.FieldType == typeof(float)) f.SetValue(wrapper, 0f);
            }

            // Neutralize increases specifically
            SetField(wrapper, wType, "BaseIncrease", 0f);
            SetField(wrapper, wType, "IncreasePerLevel", 0f);
            SetField(wrapper, wType, "IncreaseType", 0); 

            // Clear text-generating fields in the wrapper
            string[] textFields = { "attributeKey", "locKey", "descKey", "locaKey", "_attributeKey", "_locKey", "_descKey", "_locaKey", "attributeDescriptionKey" };
            foreach (var f in textFields) SetField(wrapper, wType, f, "HYPER_SPEED_DESC");
            
            // Clear Attribute Data
            SetField(wrapper, wType, "attributeData", null);
            SetField(wrapper, wType, "_attributeData", null);
        }

        static bool SetField(object obj, Type type, string name, object value)
        {
            if (type == null || type == typeof(UnityEngine.Object) || type == typeof(ScriptableObject) || type == typeof(object)) return false;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            var fields = type.GetFields(flags);
            var fi = fields.FirstOrDefault(f => f.Name == name || f.Name == "_" + name);
            if (fi != null) {
                try { fi.SetValue(obj, value); return true; } catch {}
            }
            
            var props = type.GetProperties(flags);
            var pi = props.FirstOrDefault(p => p.Name == name || p.Name == "_" + name);
            if (pi != null && pi.CanWrite) {
                try { pi.SetValue(obj, value); return true; } catch {}
            }
            
            return SetField(obj, type.BaseType, name, value);
        }
    }

    public static class ShopHijackPatch
    {
        public static void Prefix(object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            
            var upgrades = __args[0] as IList;
            if (upgrades == null || upgrades.Count == 0) return;

            DatabaseModifierPlugin.Log.LogWarning($"!!! SHOP SETUP HIJACK PREFIX (count: {upgrades.Count}) !!!");

            try {
                var first = upgrades[0] as ScriptableObject;
                if (first != null) {
                    Type t = first.GetType();
                    
                    var hijacked = UnityEngine.Object.Instantiate(first);
                    hijacked.name = "HyperShopHijack";
                    
                    UpgradeDatabasePatch.ApplyMetadata(hijacked, t, "SHOP_HIJACK");

                    // 1. Modify UI
                    upgrades[0] = hijacked;
                    Array newArray = Array.CreateInstance(t, upgrades.Count);
                    for (int i = 0; i < upgrades.Count; i++) newArray.SetValue(upgrades[i], i);
                    __args[0] = newArray;

                    // 2. Modify Controller
                    SyncControllerList(hijacked);

                    DatabaseModifierPlugin.Log.LogInfo("Hijacked Slot 0 successfully.");
                }
            } catch (Exception e) {
                DatabaseModifierPlugin.Log.LogError("Shop Hijack Failed: " + e.Message);
            }
        }

        static void SyncControllerList(ScriptableObject hijacked)
        {
            try {
                var sceneRefType = AccessTools.TypeByName("BRG.Gameplay.SceneReferences");
                var instProp = sceneRefType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                var sceneRef = instProp?.GetValue(null);
                
                if (sceneRef != null) {
                    var controllerFi = sceneRefType.GetField("RunUpgradeShopController", BindingFlags.Public | BindingFlags.Instance);
                    var controller = controllerFi?.GetValue(sceneRef);
                    
                    if (controller != null) {
                        var listFi = AccessTools.Field(controller.GetType(), "_upgradeSOs");
                        var list = listFi?.GetValue(controller) as IList;
                        
                        if (list != null && list.Count > 0) {
                            list[0] = hijacked;
                            DatabaseModifierPlugin.Log.LogWarning("Synced Controller list with hijacked item.");
                        }
                    }
                }
            } catch {}
        }
    }

    public static class SelectionTrackerPatch
    {
        public static void Postfix(object __instance, object[] __args)
        {
            try {
                if (__args == null || __args.Length == 0) return;
                object message = __args[0];
                if (message == null) return;

                var msgType = message.GetType();
                var msgFields = msgType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                object selected = null;
                var upgradeFi = msgFields.FirstOrDefault(f => f.Name.Contains("Upgrade") || f.FieldType.Name.Contains("SO"));
                if (upgradeFi != null) selected = upgradeFi.GetValue(message);

                if (selected == null) {
                    var indexFi = msgFields.FirstOrDefault(f => f.Name.ToLower().Contains("index"));
                    if (indexFi != null) {
                        int index = (int)indexFi.GetValue(message);
                        var instFields = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var listFi = instFields.FirstOrDefault(f => f.Name == "_upgradeSOs");
                        var list = listFi?.GetValue(__instance) as IList;
                        if (list != null && index >= 0 && index < list.Count) selected = list[index];
                    }
                }

                if (selected != null) {
                    var idFi = selected.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .FirstOrDefault(f => f.Name == "id" || f.Name == "_id");
                    var id = idFi?.GetValue(selected) as string;

                    if (id != null && id.Contains(DatabaseModifierPlugin.CUSTOM_UPGRADE_ID)) {
                        DatabaseModifierPlugin.Log.LogWarning($"[Selection] CUSTOM UPGRADE SELECTED: {id}. Forcing injection...");
                        
                        var asm = typeof(DatabaseUpgradeBuilder).Assembly;
                        var rdcType = asm.GetType("BRG.DataManagement.RunDataController");
                        var instance = rdcType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                        
                        if (instance != null) {
                            var methods = rdcType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            var getRunMethod = methods.FirstOrDefault(m => m.Name.Contains("GetRunUpgrades"));
                            var runUpgrades = getRunMethod?.Invoke(instance, null) as IList;
                            
                            if (runUpgrades != null) {
                                runUpgrades.Add(selected);
                                DatabaseModifierPlugin.Log.LogInfo($"[Selection] Successfully injected. New Count: {runUpgrades.Count}");
                            } else {
                                // Try field injection if method fails
                                var fields = rdcType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                var runFi = fields.FirstOrDefault(f => f.Name == "_runUpgrades" || f.Name == "runUpgrades");
                                var runList = runFi?.GetValue(instance) as IList;
                                if (runList != null) {
                                    runList.Add(selected);
                                    DatabaseModifierPlugin.Log.LogInfo($"[Selection] Successfully injected via FIELD. New Count: {runList.Count}");
                                }
                            }
                        }
                    }
                }
            } catch (Exception e) {
                DatabaseModifierPlugin.Log.LogError($"[Selection] Hook Error: {e.Message}");
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
            {
                __instance.gameObject.AddComponent<CustomUpgradeBehavior>();
                DatabaseModifierPlugin.Log.LogInfo("Added CustomUpgradeBehavior to Player.");
            }
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
            var all = GetComponents<MonoBehaviour>();
            foreach(var mb in all) {
                if (mb.GetType().Name == "PlayerMovement") {
                    _movementComponent = mb;
                    break;
                }
            }
            DatabaseModifierPlugin.Log.LogInfo($"[CustomBehavior] Started. Movement found: {(_movementComponent != null)}");
        }

        void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f)
            {
                _checkTimer = 2.0f;
                UpdateSpeedBoost();
            }
        }

        void UpdateSpeedBoost()
        {
            int count = 0;
            try {
                var asm = typeof(DatabaseUpgradeBuilder).Assembly;
                var rdcType = asm.GetType("BRG.DataManagement.RunDataController");
                var instance = rdcType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                
                if (instance != null) {
                    var methods = rdcType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var getRunMethod = methods.FirstOrDefault(m => m.Name.Contains("GetRunUpgrades"));
                    var upgradeList = getRunMethod?.Invoke(instance, null) as IList;
                    count += CountInList(upgradeList, "RunUpgrades");
                    
                    var getNodeMethod = methods.FirstOrDefault(m => m.Name == "GetNodes" || m.Name == "GetAllNodes");
                    var nodeList = getNodeMethod?.Invoke(instance, null) as IList;
                    count += CountInList(nodeList, "Nodes");
                }
            } catch {}

            float boost = count * 1.0f; 
            if (boost != _lastSpeedBoost)
            {
                DatabaseModifierPlugin.Log.LogWarning($"[CustomBehavior] BOOST CHANGE: {count} stacks -> {boost*100}% speed boost.");
                ApplySpeedBoost(boost);
                _lastSpeedBoost = boost;
            }
        }

        int CountInList(IList list, string label)
        {
            if (list == null) return 0;
            int c = 0;
            foreach (var item in list) {
                if (item == null) continue;
                var itemType = item.GetType();
                var fields = itemType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var idFi = fields.FirstOrDefault(f => f.Name == "id" || f.Name == "_id");
                if (idFi != null) {
                    var id = idFi.GetValue(item) as string;
                    if (id != null) {
                        if (id.Contains(DatabaseModifierPlugin.CUSTOM_UPGRADE_ID)) {
                            c++;
                            DatabaseModifierPlugin.Log.LogInfo($"[CustomBehavior] Found custom upgrade in {label}: {id}");
                        }
                    }
                }
            }
            return c;
        }

        void ApplySpeedBoost(float boost)
        {
            if (_movementComponent == null) return;
            try {
                var type = _movementComponent.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var speedFi = fields.FirstOrDefault(f => f.Name == "_speed" || f.Name == "speed" || f.Name == "_moveSpeed");
                if (speedFi != null) {
                    float currentSpeed = (float)speedFi.GetValue(_movementComponent);
                    if (_originalSpeed < 0) _originalSpeed = currentSpeed;
                    float newSpeed = _originalSpeed * (1f + boost);
                    speedFi.SetValue(_movementComponent, newSpeed);
                    DatabaseModifierPlugin.Log.LogWarning($"[CustomBehavior] SPEED APPLIED: {newSpeed:F2} (Base: {_originalSpeed:F2}, Boost: {boost*100}%)");
                }
            } catch (Exception e) {
                DatabaseModifierPlugin.Log.LogError($"[CustomBehavior] Failed to apply speed: {e.Message}");
            }
        }
    }
}