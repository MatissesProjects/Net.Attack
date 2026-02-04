using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
// We use reflection for types to avoid strict dependency if assembly changes, 
// but we can try to use the namespace if available.
// using BRG.DataManagement; 

namespace DatabaseModifierMod
{
    [BepInPlugin("com.matissetec.databasemodifier", "Database Modifier", "1.0.0")]
    public class DatabaseModifierPlugin : BaseUnityPlugin
    {
        public static DatabaseModifierPlugin Instance;

        void Awake()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(typeof(UpgradeDatabasePatch));
            Logger.LogInfo("Database Modifier Mod Loaded.");
        }

        // We patch BuildUpgrades to intercept the list generation
        [HarmonyPatch]
        public static class UpgradeDatabasePatch
        {
            static MethodBase TargetMethod()
            {
                // Using string-based lookup for robustness against assembly changes
                return AccessTools.Method("BRG.DataManagement.DatabaseUpgradeBuilder:BuildUpgrades");
            }

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                Instance.Logger.LogInfo(">>> Postfix: BuildUpgrades finished. Inspecting lists...");
                
                // Use reflection to access private fields of DatabaseUpgradeBuilder
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var type = __instance.GetType();
                
                var gameplayField = type.GetField("_gameplayUpgrades", flags);
                var runField = type.GetField("_runUpgrades", flags);

                IList gameplayUpgrades = gameplayField?.GetValue(__instance) as IList;
                IList runUpgrades = runField?.GetValue(__instance) as IList;

                InspectList(gameplayUpgrades, "Gameplay Upgrades");
                InspectList(runUpgrades, "Run Upgrades");

                // --- MODIFICATION SECTION ---
                // Here we demonstrate modifying the database.
                // Example: Duplicate the first item in 'Run Upgrades' to increase its drop chance (if weight is uniform)
                // or just to prove we can write to the list.
                
                if (runUpgrades != null && runUpgrades.Count > 0)
                {
                    Instance.Logger.LogWarning("[DatabaseModifier] Applying Test Modification: Duplicating first Run Upgrade...");
                    try {
                        object firstItem = runUpgrades[0];
                        
                        // NOTE: If the list is a fixed-size array, this will fail. 
                        // If it's a List<T>, it will work. 
                        // Most DB builders use List<T> during construction.
                        if (runUpgrades.IsFixedSize)
                        {
                            Instance.Logger.LogWarning("[DatabaseModifier] List is fixed size, cannot add directly. " +
                                "You would need to replace the array with a larger one via reflection.");
                        }
                        else
                        {
                            runUpgrades.Add(firstItem);
                            Instance.Logger.LogInfo($"[DatabaseModifier] Modification Successful. New Count: {runUpgrades.Count}");
                            Instance.Logger.LogInfo($"[DatabaseModifier] Added copy of: {firstItem}");
                        }

                    } catch (Exception e) {
                        Instance.Logger.LogError($"[DatabaseModifier] Modification Failed: {e}");
                    }
                }
            }

            static void InspectList(IList list, string label)
            {
                if (list == null)
                {
                    Instance.Logger.LogWarning($"{label}: List is null.");
                    return;
                }

                Instance.Logger.LogInfo($"{label}: Found {list.Count} items.");
                
                // List the first 5 items to show content
                for (int i = 0; i < Math.Min(5, list.Count); i++)
                {
                    var item = list[i];
                    if (item == null) continue;
                    
                    string info = item.ToString();
                    
                    // Try to find an "id" or "name" field for better logging
                    try {
                        // Check for 'id' field (common in BRG data)
                        var idField = AccessTools.Field(item.GetType(), "id") ?? AccessTools.Field(item.GetType(), "Id");
                        if (idField != null) 
                        {
                            var val = idField.GetValue(item);
                            if (val != null) info += $" [ID: {val}]";
                        }
                        
                        // Check for 'name' property (Unity Object)
                        var nameProp = item.GetType().GetProperty("name"); 
                        if (nameProp != null)
                        {
                            var val = nameProp.GetValue(item, null);
                            if (val != null) info += $" [Name: {val}]";
                        }

                    } catch {}

                    Instance.Logger.LogInfo($" - Item {i}: {info}");
                }
            }
        }
    }
}
