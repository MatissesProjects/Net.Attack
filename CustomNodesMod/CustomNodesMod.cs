using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using NetAttackModUtils;

namespace CustomNodesMod
{
    [BepInPlugin("com.matissetec.customnodes", "Custom Nodes Mod", "1.0.0")]
    public class CustomNodesPlugin : BaseUnityPlugin
    {
        public static CustomNodesPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.customnodes");
                harmony.PatchAll(); 
                
                // Inspect NodeDataBase
                ModUtils.PatchSafe(harmony, Log, "BRG.DataBase.NodeDataBase", "LoadDatabase", typeof(NodeDatabasePatch));

                Log.LogInfo(">>> CUSTOM NODES MOD ONLINE <<<");
            } catch (Exception e) {
                Log.LogError("CustomNodes Patching Failed: " + e);
            }
        }
    }

    public static class NodeDatabasePatch
    {
        // Intercept database loading to potentially inject our own nodes
        public static void Postfix(object __instance)
        {
            CustomNodesPlugin.Log.LogInfo("[CustomNodes] NodeDataBase Loaded. Inspecting...");
            try {
                var fields = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach(var f in fields) {
                    var val = f.GetValue(__instance);
                    if (val is IList list) {
                        CustomNodesPlugin.Log.LogInfo($"[CustomNodes] Found List {f.Name} with {list.Count} items.");
                    } else if (val is IDictionary dict) {
                        CustomNodesPlugin.Log.LogInfo($"[CustomNodes] Found Dictionary {f.Name} with {dict.Count} items.");
                    }
                }
            } catch (Exception e) {
                CustomNodesPlugin.Log.LogError($"[CustomNodes] Error inspecting database: {e}");
            }
        }
    }
}