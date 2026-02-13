// using System;
// using System.Reflection;
// using BepInEx;
// using HarmonyLib;
// using UnityEngine;
// using NetAttackModUtils;

// namespace NodeMasterMod
// {
//     [BepInPlugin("com.matissetec.nodemaster", "Node Master", "1.1.0")]
//     public class NodeMasterPlugin : BaseUnityPlugin
//     {
//         public static NodeMasterPlugin Instance;
//         internal static BepInEx.Logging.ManualLogSource Log;

//         void Awake()
//         {
//             Instance = this;
//             Log = Logger;
            
//             try {
//                 Harmony harmony = new Harmony("com.matissetec.nodemaster");
//                 harmony.PatchAll(); 
//                 Log.LogInfo(">>> NODE MASTER (V4 - MODUTILS) ONLINE <<<");
//             } catch (Exception e) {
//                 Log.LogError("NodeMaster Patching Failed: " + e);
//             }
//         }
//     }

//     [HarmonyPatch]
//     public static class NodeMasterPatch
//     {
//         static MethodBase TargetMethod()
//         {
//             return AccessTools.Method("BRG.UI.Desktop:Initialize");
//         }

//         [HarmonyPostfix]
//         static void Postfix()
//         {
//             ApplyGlobalModifications();
//         }

//         public static void ApplyGlobalModifications()
//         {
//             int modCount = 0;
//             int actionModCount = 0;

//             ModUtils.FindAndModifyNodes(NodeMasterPlugin.Log, (node) => {
//                 if (node == null) return;
//                 Type type = node.GetType();

//                 // 1. PRICE HACK
//                 var priceFi = AccessTools.Field(type, "_price");
//                 if (priceFi != null) priceFi.SetValue(node, 1);

//                 // 2. REMOVE PLACEMENT LIMITS
//                 var maxCountFi = AccessTools.Field(type, "_maxCount");
//                 if (maxCountFi != null) maxCountFi.SetValue(node, 99);

//                 // 3. ACTION OVERCLOCKING
//                 var actionFi = AccessTools.Field(type, "_action");
//                 if (actionFi != null) {
//                     var actionObj = actionFi.GetValue(node);
//                     if (actionObj != null) {
//                         if (ModUtils.OverclockNodeAction(actionObj, 0.2f)) actionModCount++;
//                     }
//                 }
//                 modCount++;
//             });

//             NodeMasterPlugin.Log.LogInfo("========================================");
//             NodeMasterPlugin.Log.LogInfo($"[NodeMaster] SUCCESS:");
//             NodeMasterPlugin.Log.LogInfo($" - Blueprints Modified: {modCount}");
//             NodeMasterPlugin.Log.LogInfo($" - Action Logics Overclocked: {actionModCount}");
//             NodeMasterPlugin.Log.LogInfo("========================================");
//         }
//     }
// }