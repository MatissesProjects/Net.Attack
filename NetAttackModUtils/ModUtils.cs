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
                    if (prefix == null && postfix == null) return;
                    harmony.Patch(method, prefix != null ? new HarmonyMethod(prefix) : null, postfix != null ? new HarmonyMethod(postfix) : null);
                    Log.LogInfo($"Successfully patched {className}:{methodName}");
                } 
            } catch (Exception e) {
                Log.LogError($"Error patching {className}:{methodName}: {e.Message}");
            }
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

        public static void ModifyWrapper(object obj, Type type, string fieldName, string descKey)
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

        public static bool SetField(object obj, Type type, string name, object value)
        {
            if (type == null || type == typeof(UnityEngine.Object) || type == typeof(ScriptableObject)) return false;
            var fi = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(f => f.Name == name || f.Name == "_" + name);
            if (fi != null) { try { fi.SetValue(obj, value); return true; } catch {} }
            var pi = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(p => p.Name == name || p.Name == "_" + name);
            if (pi != null && pi.CanWrite) { try { pi.SetValue(obj, value); return true; } catch {} }
            return SetField(obj, type.BaseType, name, value);
        }

        // --- NEW SHARED HELPERS ---

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
    }
}