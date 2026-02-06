using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using NetAttackModUtils;

namespace NodeExpansionPack
{
    [BepInPlugin("com.matissetec.nodeexpansion", "Node Expansion Pack", "1.1.0")]
    public class NodeExpansionPlugin : BaseUnityPlugin
    {
        public static NodeExpansionPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        public const string MEGA_NODE_ID = "node_mega_processor";
        
        public static Dictionary<string, ScriptableObject> InjectedNodes = new Dictionary<string, ScriptableObject>();

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.nodeexpansion");
                ModUtils.PatchSafe(harmony, Log, "BRG.DataManagement.DatabaseNodeBuilder", "LoadDatabase", typeof(NodeDatabasePatch));
                
                // Hijack node shop to show MEGA PROCESSOR
                ModUtils.AddNodeShopHijack(harmony, 
                    () => InjectedNodes.ContainsKey(MEGA_NODE_ID) ? InjectedNodes[MEGA_NODE_ID] : null, 
                    () => true);

                Log.LogInfo(">>> NODE EXPANSION PACK ONLINE <<<");
            } catch (Exception e) {
                Log.LogError("NodeExpansion Patching Failed: " + e);
            }
        }
    }

    public static class NodeDatabasePatch
    {
        public static void Postfix(object __instance)
        {
            try {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var nodesField = __instance.GetType().GetField("_allNodes", flags);
                if (nodesField == null) nodesField = __instance.GetType().GetFields(flags).FirstOrDefault(f => f.Name.ToLower().Contains("nodes") && f.FieldType.Name.Contains("List"));
                
                IList nodesList = nodesField?.GetValue(__instance) as IList;
                if (nodesList != null) {
                    InjectCustomNodes(nodesList);
                }
            } catch (Exception e) {
                NodeExpansionPlugin.Log.LogError($"[NodeExpansion] Injection Failed: {e.Message}");
            }
        }

        static void InjectCustomNodes(IList list)
        {
            Type nodeType = AccessTools.TypeByName("NodeSO");

            // 1. MEGA PROCESSOR
            if (ModUtils.FindInList(list, NodeExpansionPlugin.MEGA_NODE_ID, nodeType) == null) {
                var node = ModUtils.CreateTemplate<ScriptableObject>(list, "ForAction", NodeExpansionPlugin.MEGA_NODE_ID, "MEGA PROCESSOR", "A highly advanced processor node with 0 delay.");
                if (node != null) {
                    ModUtils.SetField(node, nodeType, "_price", 1);
                    ModUtils.SetField(node, nodeType, "_maxCount", 99);
                    ModUtils.SetActionField(node, "Iterations", 100);
                    ModUtils.OverclockNodeAction(AccessTools.Field(nodeType, "_action").GetValue(node), 0f);
                    
                    list.Add(node);
                    NodeExpansionPlugin.InjectedNodes[NodeExpansionPlugin.MEGA_NODE_ID] = node;
                    NodeExpansionPlugin.Log.LogInfo("[NodeExpansion] Injected MEGA PROCESSOR.");
                }
            }

            // 2. INSTA-KILL PROCESSOR
            if (ModUtils.FindInList(list, "node_instakill", nodeType) == null) {
                var node = ModUtils.CreateTemplate<ScriptableObject>(list, "DamageAction", "node_instakill", "INSTA-KILL PROCESSOR", "Deals 999,999 damage instantly.");
                if (node != null) {
                    ModUtils.SetField(node, nodeType, "_price", 500);
                    ModUtils.SetField(node, nodeType, "_maxCount", 5);
                    ModUtils.SetActionField(node, "Damage", 999999f);
                    list.Add(node);
                    NodeExpansionPlugin.InjectedNodes["node_instakill"] = node;
                    NodeExpansionPlugin.Log.LogInfo("[NodeExpansion] Injected INSTA-KILL PROCESSOR.");
                }
            }

            // 3. TURBO TRIGGER
            if (ModUtils.FindInList(list, "node_turbo_trigger", nodeType) == null) {
                var node = ModUtils.CreateTemplate<ScriptableObject>(list, "Start_OnAttackAction", "node_turbo_trigger", "TURBO TRIGGER", "Fires at 5x normal speed.");
                if (node != null) {
                    ModUtils.SetField(node, nodeType, "_price", 100);
                    ModUtils.SetActionField(node, "cooldown", 0.02f);
                    list.Add(node);
                    NodeExpansionPlugin.InjectedNodes["node_turbo_trigger"] = node;
                    NodeExpansionPlugin.Log.LogInfo("[NodeExpansion] Injected TURBO TRIGGER.");
                }
            }
        }
    }
}