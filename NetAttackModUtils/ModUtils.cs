using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BRG.Gameplay.Units;
using BRG;

namespace NetAttackModUtils
{
    public static class ModUtils
    {
        public static void PatchSafe(Harmony harmony, BepInEx.Logging.ManualLogSource Log, string className, string methodName, Type patchType)
        {
            try {
                var method = AccessTools.Method(className + ":" + methodName);
                if (method != null) {
                    MethodInfo prefix = patchType.GetMethod("Prefix");
                    MethodInfo postfix = patchType.GetMethod("Postfix");
                    if (prefix == null && postfix == null) {
                        Log.LogWarning($"Patch failed: No Prefix or Postfix found in {patchType.Name}");
                        return;
                    }
                    harmony.Patch(method, prefix != null ? new HarmonyMethod(prefix) : null, postfix != null ? new HarmonyMethod(postfix) : null);
                    Log.LogInfo($"Successfully patched {className}:{methodName}");
                } else {
                    Log.LogWarning($"Patch failed: Method {className}:{methodName} not found.");
                } 
            } catch (Exception e) {
                Log.LogError($"Error patching {className}:{methodName}: {e.Message}");
            }
        }

        public static void ApplyMetadata(ScriptableObject obj, string id, string nameKey, string descKey, string incKey = null)
        {
            Type type = obj.GetType();
            SetField(obj, type, "id", id);
            SetField(obj, type, "_id", id);
            SetField(obj, type, "_name", nameKey);
            SetField(obj, type, "_nameKey", nameKey);
            SetField(obj, type, "_description", descKey);
            SetField(obj, type, "_descriptionKey", descKey);
            SetField(obj, type, "_tooltipkey", descKey);
            
            string[] levelFields = { 
                "_level", "level", "_currentLevel", "currentLevel", "_buyCount", "buyCount", 
                "_upgradeLevel", "upgradeLevel", "_count", "count", "_stack", "stack", 
                "_stacks", "stacks", "_unlocked", "unlocked", "_isUnlocked", "isUnlocked",
                "_purchased", "purchased", "_isPurchased", "isPurchased"
            };
            foreach (var f in levelFields) SetField(obj, type, f, 0);
            
            SetField(obj, type, "_price", 1);
            SetField(obj, type, "_basePrice", 1);
            SetField(obj, type, "_maxCount", 10);
            SetField(obj, type, "_maxUpgradeLevel", 10);
            SetField(obj, type, "_isActive", true);
            
            foreach (var w in new[] { "gameplayUpgrade", "nodeUpgrade", "metaUpgrade", "upgrade" }) ModifyWrapper(obj, type, w, nameKey, descKey, incKey);

            SetField(obj, type, "action", null);
            SetField(obj, type, "attributeData", null);
            SetField(obj, type, "_attributeData", null);
        }

        public static void ModifyWrapper(object obj, Type type, string fieldName, string nameKey, string descKey, string incKey = null)
        {
            var fi = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(f => f.Name == fieldName || f.Name == "_" + fieldName);
            var wrapper = fi?.GetValue(obj);
            if (wrapper == null) return;
            
            Type wType = wrapper.GetType();
            foreach(var f in wType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
                if (f.FieldType == typeof(float)) f.SetValue(wrapper, 0f);
                if (f.FieldType == typeof(int)) f.SetValue(wrapper, 0);
                if (f.FieldType.IsEnum) { try { f.SetValue(wrapper, 0); } catch {} }
                
                if (f.FieldType == typeof(string) && f.Name.Contains("Key")) {
                    string n = f.Name.ToLower();
                    if (n.Contains("name")) f.SetValue(wrapper, nameKey);
                    else if (incKey != null && (n.Contains("value") || n.Contains("inc"))) f.SetValue(wrapper, incKey);
                    else f.SetValue(wrapper, descKey);
                }
            }
            SetField(wrapper, wType, "attributeData", null);
            SetField(wrapper, wType, "_attributeData", null);
        }

        public static bool SetField(object obj, Type type, string name, object value)
        {
            if (type == null || type == typeof(UnityEngine.Object) || type == typeof(ScriptableObject)) return false;
            var fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(f => f.Name == name || f.Name == "_" + name);
            if (fi != null) { try { fi.SetValue(obj, value); return true; } catch {} }
            var pi = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.Name == name || p.Name == "_" + name);
            if (pi != null && pi.CanWrite) { try { pi.SetValue(obj, value); return true; } catch {} }
            return SetField(obj, type.BaseType, name, value);
        }

        public static int GetPlayerLevel(MonoBehaviour component)
        {
            try {
                var p = component.GetComponent<Player>();
                if (p == null) p = UnityEngine.Object.FindObjectOfType<Player>();
                if (p != null) {
                    var pi = typeof(Player).GetProperty("Level", BindingFlags.Public | BindingFlags.Instance);
                    if (pi != null) return (int)pi.GetValue(p);
                }
            } catch {}
            return 1;
        }

        private static FieldInfo _fiOnShopChangedMsg;
        public static bool IsShopOpen()
        {
            try {
                if (_fiOnShopChangedMsg == null) {
                    _fiOnShopChangedMsg = typeof(BRG.Gameplay.Upgrades.RunUpgradeShopController).GetField("_onShopChangedMessage", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                var shop = BRG.Gameplay.SceneReferences.Instance?.RunUpgradeShopController;
                if (shop != null && _fiOnShopChangedMsg != null) {
                    object msg = _fiOnShopChangedMsg.GetValue(shop);
                    if (msg != null) {
                        var fi = msg.GetType().GetField("IsShopOpen", BindingFlags.Instance | BindingFlags.Public);
                        if (fi != null) return (bool)fi.GetValue(msg);
                    }
                }
            } catch {}
            return false;
        }

        public static bool IsCodingScreenActive(bool currentStatus, ref float holdTimer)
        {
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.E)) return true;

            bool isMoving = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);
            if (isMoving) {
                holdTimer += Time.deltaTime;
                if (holdTimer > 1.0f) return false;
            } else {
                holdTimer = 0f;
            }
            return currentStatus; 
        }

        public static void FindAndModifyNodes(BepInEx.Logging.ManualLogSource Log, Action<ScriptableObject> modificationCallback)
        {
            try {
                Type nodeType = AccessTools.TypeByName("NodeSO");
                if (nodeType == null) return;

                var allNodes = Resources.FindObjectsOfTypeAll(nodeType);
                if (allNodes.Length == 0) return;

                foreach (var obj in allNodes) {
                    if (obj == null) continue;
                    modificationCallback?.Invoke(obj as ScriptableObject);
                }
            } catch (Exception e) {
                Log.LogError($"[ModUtils] Node Scan Error: {e.Message}");
            }
        }

        public static bool OverclockNodeAction(object actionObj, float speedMultiplier = 0.2f)
        {
             bool modified = false;
            try {
                var fields = actionObj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach(var f in fields) {
                    if (f.FieldType == typeof(float)) {
                        string name = f.Name.ToLower();
                        if (name.Contains("delay") || name.Contains("cooldown") || name.Contains("wait") || name.Contains("duration")) {
                            float val = (float)f.GetValue(actionObj);
                            if (val > 0.01f) {
                                f.SetValue(actionObj, Mathf.Max(0.02f, val * speedMultiplier));
                                modified = true;
                            }
                        }
                    }
                }
            } catch {}
            return modified;
        }

        // Backward compatibility for mods compiled with older versions
        public static T CreateTemplate<T>(IList list, string templateId, string newId, string name, string desc) where T : ScriptableObject
        {
            return CreateTemplate<T>(list, templateId, newId, name, desc, null);
        }

        public static T CreateTemplate<T>(IList list, string templateId, string newId, string name, string desc, string inc) where T : ScriptableObject
        {
            object template = null;
            Type type = typeof(T);
            var idField = AccessTools.Field(type, "id") ?? AccessTools.Field(type, "_id");

            foreach (var item in list) {
                if (item == null) continue;
                var id = idField?.GetValue(item) as string;
                if (id == templateId) {
                    template = item;
                    break;
                }
            }

            if (template == null && list.Count > 0) template = list[0];
            if (template == null) return null;

            T newObj = UnityEngine.Object.Instantiate((T)template);
            newObj.name = newId;
            ApplyMetadata(newObj, newId, name, desc, inc);
            return newObj;
        }

        public static object FindInList(IList list, string id, Type type = null)
        {
            if (list == null) return null;
            if (type == null && list.Count > 0 && list[0] != null) type = list[0].GetType();
            if (type == null) return null;

            var idField = AccessTools.Field(type, "id") ?? AccessTools.Field(type, "_id");
            foreach (var item in list) {
                if (item == null) continue;
                if (idField?.GetValue(item) as string == id) return item;
            }
            return null;
        }

        public static bool SetActionField(ScriptableObject node, string fieldName, object value)
        {
            try {
                Type nodeType = node.GetType();
                var actionFi = AccessTools.Field(nodeType, "_action");
                if (actionFi != null) {
                    var action = actionFi.GetValue(node);
                    if (action != null) {
                        return SetField(action, action.GetType(), fieldName, value);
                    }
                }
            } catch {}
            return false;
        }

        public static void DumpFields(BepInEx.Logging.ManualLogSource Log, object obj, string label = "Dump")
        {
            if (obj == null) { Log.LogInfo($"[{label}] Object is null"); return; }
            try {
                Type type = obj.GetType();
                Log.LogInfo($"--- {label} (Type: {type.Name}) ---");
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                    object val = "null";
                    try { val = f.GetValue(obj); } catch {}
                    Log.LogInfo($"{f.Name} ({f.FieldType.Name}): {val}");
                }
            } catch (Exception e) {
                Log.LogError($"Error dumping fields for {label}: {e.Message}");
            }
        }

        public static void InspectEnum(BepInEx.Logging.ManualLogSource Log, string enumName)
        {
            try {
                Type enumType = AccessTools.TypeByName(enumName);
                if (enumType != null && enumType.IsEnum) {
                    Log.LogInfo($"--- Enum Inspection: {enumName} ---");
                    foreach (var name in Enum.GetNames(enumType)) {
                        Log.LogInfo($"{name} = {(int)Enum.Parse(enumType, name)}");
                    }
                }
            } catch (Exception e) {
                Log.LogError($"Error inspecting enum {enumName}: {e.Message}");
            }
        }

        // --- CLEAN CENTRALIZED SHOP SYSTEM ---

        private static List<HijackData> _upgradeRegistry = new List<HijackData>();
        private static List<HijackData> _nodeRegistry = new List<HijackData>();
        private static Action<int, object> _onUpgradeSelected;
        private static System.Random _rnd = new System.Random();
        
        private class HijackData { public Func<ScriptableObject> GetTemplate; public Func<bool> ShouldInject; }

        public static void RegisterModdedUpgrade(Harmony harmony, Func<ScriptableObject> getTemplate, Func<bool> shouldInject)
        {
            if (_upgradeRegistry.Count == 0) {
                var method = AccessTools.Method("BRG.UI.UpgradeShop:SetupShop");
                if (method != null) harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(ModUtils), nameof(UpgradeShopPrefix))));
            }
            _upgradeRegistry.Add(new HijackData { GetTemplate = getTemplate, ShouldInject = shouldInject });
            BepInEx.Logging.Logger.CreateLogSource("ModUtils").LogInfo($"Registered Upgrade Hijack. Total: {_upgradeRegistry.Count}");
        }

        private static void UpgradeShopPrefix(object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            var upgrades = __args[0] as IList;
            if (upgrades == null || upgrades.Count == 0) return;

            // 1. Collect all eligible templates (unique)
            var eligible = new List<ScriptableObject>();
            foreach (var mod in _upgradeRegistry) {
                if (mod.ShouldInject()) {
                    var template = mod.GetTemplate();
                    if (template != null && !eligible.Contains(template)) {
                        eligible.Add(template);
                    }
                }
            }

            if (eligible.Count == 0) return;

            // 2. Pick a random subset (at most 2)
            int numToInject = Math.Min(eligible.Count, 2);
            var toInject = eligible.OrderBy(x => _rnd.Next()).Take(numToInject).ToList();

            // 3. Inject into random unique slots
            var slots = Enumerable.Range(0, upgrades.Count).OrderBy(x => _rnd.Next()).Take(numToInject).ToList();
            for (int i = 0; i < toInject.Count; i++) {
                upgrades[slots[i]] = toInject[i];
            }
        }

        public static void AddUpgradeSelectionTracker(Harmony harmony, Action<int, object> onSelected)
        {
            if (_onUpgradeSelected == null) {
                var method = AccessTools.Method("BRG.Gameplay.Upgrades.RunUpgradeShopController:OnUpgradeSelected");
                if (method != null) harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(ModUtils), nameof(SelectionPrefix))));
            }
            _onUpgradeSelected += onSelected;
        }

        private static void SelectionPrefix(object __instance, object[] __args)
        {
            try {
                if (__args == null || __args.Length == 0) return;
                object message = __args[0];
                if (message == null) return;

                var msgFields = message.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var indexFi = msgFields.FirstOrDefault(f => f.Name.ToLower().Contains("index"));
                int selectedIndex = (indexFi != null) ? (int)indexFi.GetValue(message) : -1;
                
                var upgradeFi = msgFields.FirstOrDefault(f => f.Name.Contains("Upgrade") || f.FieldType.Name.Contains("SO"));
                object selectedUpgrade = (upgradeFi != null) ? upgradeFi.GetValue(message) : null;

                _onUpgradeSelected?.Invoke(selectedIndex, selectedUpgrade);
            } catch {}
        }

        public static void AddNodeShopHijack(Harmony harmony, Func<ScriptableObject> getTemplate, Func<bool> shouldInject)
        {
            if (_nodeRegistry.Count == 0) {
                var method = AccessTools.Method("BRG.NodeSystem.Shop:SpawnNewNodes");
                if (method != null) harmony.Patch(method, new HarmonyMethod(AccessTools.Method(typeof(ModUtils), nameof(NodeShopPrefix))));
            }
            _nodeRegistry.Add(new HijackData { GetTemplate = getTemplate, ShouldInject = shouldInject });
            BepInEx.Logging.Logger.CreateLogSource("ModUtils").LogInfo($"Registered Node Hijack. Total: {_nodeRegistry.Count}");
        }

        private static void NodeShopPrefix(object __instance)
        {
            try {
                var log = BepInEx.Logging.Logger.CreateLogSource("ModUtils_Shop");
                
                if (_nodeRegistry.Count == 0) {
                    log.LogWarning("NodeShopPrefix: Registry is empty!");
                    return;
                }

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                
                // 1. Get the ShopNodeDataSO sub-object
                var dataField = __instance.GetType().GetField("_shopNodeDataSO", flags);
                var dataSO = dataField?.GetValue(__instance);
                if (dataSO == null) {
                    log.LogWarning("NodeShopPrefix: _shopNodeDataSO not found on Shop instance.");
                    return;
                }

                // 2. Find the Deck, Hand, and master nodes array
                var soType = dataSO.GetType();
                var deckFi = soType.GetField("_Deck", flags) ?? soType.GetField("_deck", flags);
                var handFi = soType.GetField("_Hand", flags) ?? soType.GetField("_hand", flags);
                var masterFi = soType.GetField("_nodes", flags);

                var deck = deckFi?.GetValue(dataSO) as IList;
                var hand = handFi?.GetValue(dataSO) as IList;

                if (deck == null) {
                    log.LogWarning("NodeShopPrefix: Could not find _Deck list in ShopNodeDataSO.");
                    return;
                }

                // 3. Inject into Deck (the source of truth for the current shop session)
                foreach (var hijack in _nodeRegistry) {
                    if (hijack.ShouldInject()) {
                        var template = hijack.GetTemplate();
                        if (template != null) {
                            // Ensure it's in the Deck
                            bool inDeck = false;
                            foreach (var item in deck) if (item == template) { inDeck = true; break; }
                            
                            if (!inDeck) {
                                deck.Insert(0, template);
                                log.LogInfo($"Injected node into Deck: {template.name}");
                            } else {
                                // log.LogInfo($"Node {template.name} already in Deck.");
                            }
                        } else {
                            log.LogWarning("Node Hijack Template is NULL!");
                        }
                    } else {
                        // log.LogInfo("Node Hijack ShouldInject returned false.");
                    }
                }

                // 4. Update the master array if necessary to prevent character filtering
                if (masterFi != null) {
                    var masterArray = masterFi.GetValue(dataSO) as Array;
                    if (masterArray != null) {
                        // Check if our nodes are in the master array
                        foreach (var hijack in _nodeRegistry) {
                            var template = hijack.GetTemplate();
                            if (template == null) continue;
                            
                            bool found = false;
                            for (int i = 0; i < masterArray.Length; i++) {
                                if (masterArray.GetValue(i) == template) { found = true; break; }
                            }

                            if (!found) {
                                // Expand array and add
                                var newArray = Array.CreateInstance(masterArray.GetType().GetElementType(), masterArray.Length + 1);
                                Array.Copy(masterArray, newArray, masterArray.Length);
                                newArray.SetValue(template, masterArray.Length);
                                masterFi.SetValue(dataSO, newArray);
                                log.LogInfo($"Added {template.name} to ShopNodeDataSO master array.");
                            }
                        }
                    }
                }
            } catch (Exception e) {
                var log = BepInEx.Logging.Logger.CreateLogSource("ModUtils_Shop_Err");
                log.LogError($"Error in NodeShopPrefix: {e.Message}");
            }
        }
    }
}