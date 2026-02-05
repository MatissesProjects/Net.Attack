using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace NodeMasterMod
{
    [BepInPlugin("com.matissetec.nodemaster", "Node Master", "1.0.0")]
    public class NodeMasterPlugin : BaseUnityPlugin
    {
        public static NodeMasterPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.nodemaster");
                harmony.PatchAll(); 
                Log.LogInfo(">>> NODE MASTER (V3) DEEP SCAN ONLINE <<<");
            } catch (Exception e) {
                Log.LogError("NodeMaster Patching Failed: " + e);
            }
        }
    }

    [HarmonyPatch]
    public static class NodeMasterPatch
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method("BRG.UI.Desktop:Initialize");
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            ApplyGlobalModifications();
        }

        public static void ApplyGlobalModifications()
        {
            try {
                Type nodeType = AccessTools.TypeByName("NodeSO");
                if (nodeType == null) return;

                var allNodes = Resources.FindObjectsOfTypeAll(nodeType);
                if (allNodes.Length == 0) return;

                NodeMasterPlugin.Log.LogInfo("========================================");
                NodeMasterPlugin.Log.LogInfo($"[NodeMaster] DEEP SCANNING {allNodes.Length} NODES");
                NodeMasterPlugin.Log.LogInfo("========================================");

                int modCount = 0;
                int actionModCount = 0;

                foreach (var obj in allNodes)
                {
                    if (obj == null) continue;
                    var type = obj.GetType();

                    // 1. PRICE HACK (Verified field: _price)
                    var priceFi = AccessTools.Field(type, "_price");
                    if (priceFi != null) priceFi.SetValue(obj, 1);

                    // 2. REMOVE PLACEMENT LIMITS (Verified field: _maxCount)
                    var maxCountFi = AccessTools.Field(type, "_maxCount");
                    if (maxCountFi != null) maxCountFi.SetValue(obj, 99);

                    // 3. ACTION OVERCLOCKING (Deep Discovery)
                    // We look into the '_action' field which holds the logic (ActionBase)
                    var actionFi = AccessTools.Field(type, "_action");
                    if (actionFi != null)
                    {
                        var actionObj = actionFi.GetValue(obj);
                        if (actionObj != null)
                        {
                            if (ModifyActionStats(actionObj)) actionModCount++;
                        }
                    }

                    modCount++;
                }

                NodeMasterPlugin.Log.LogInfo($"[NodeMaster] SUCCESS:");
                NodeMasterPlugin.Log.LogInfo($" - Blueprints Modified: {modCount}");
                NodeMasterPlugin.Log.LogInfo($" - Action Logics Overclocked: {actionModCount}");
                NodeMasterPlugin.Log.LogInfo("========================================");
            }
            catch (Exception e) {
                NodeMasterPlugin.Log.LogError("NodeMaster Execution Error: " + e.Message);
            }
        }

        private static bool ModifyActionStats(object action)
        {
            bool modified = false;
            try {
                // Actions (ActionBase) have various delay/cooldown fields.
                // We scan for any float that looks like a timer.
                var fields = action.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                foreach(var f in fields)
                {
                    if (f.FieldType == typeof(float))
                    {
                        string name = f.Name.ToLower();
                        if (name.Contains("delay") || name.Contains("cooldown") || name.Contains("wait") || name.Contains("duration"))
                        {
                            float val = (float)f.GetValue(action);
                            // Skip very small values or 0
                            if (val > 0.01f)
                            {
                                // Slash time by 80% (5x speed)
                                f.SetValue(action, Mathf.Max(0.02f, val * 0.2f));
                                modified = true;
                            }
                        }
                    }
                }
            } catch {}
            return modified;
        }
    }
}
