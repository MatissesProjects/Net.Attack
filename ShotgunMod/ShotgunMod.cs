using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ShotgunMod
{
    [BepInPlugin("com.matissetec.shotgunmod", "Shotgun Mod", "1.0.0")]
    public class ShotgunPlugin : BaseUnityPlugin
    {
        public static ShotgunPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(ShotgunPatch));
            Log.LogInfo("Shotgun Mod Loaded.");
        }
    }

    [HarmonyPatch]
    public static class ShotgunPatch
    {
        // Target: BRG.NodeSystem.Actions.ProjectileAction:Execute
        // We use string reflection to be safe
        static MethodBase TargetMethod()
        {
            return AccessTools.Method("BRG.NodeSystem.Actions.ProjectileAction:Execute");
        }

        [HarmonyPrefix]
        static void Prefix(object __instance)
        {
            try 
            {
                // 1. Identify if this Action belongs to the PLAYER
                // Actions usually have a 'Unit' or 'Owner' property.
                // We'll check for the field '_unit' or property 'Unit'.
                var type = __instance.GetType();
                FieldInfo unitField = AccessTools.Field(type, "_unit") ?? AccessTools.Field(type, "unit");
                
                object unitObj = null;
                if (unitField != null) unitObj = unitField.GetValue(__instance);

                // If we found a unit, check if it's the Player
                if (unitObj != null && unitObj.GetType().Name.Contains("Player"))
                {
                    // 2. APPLY COST (Self Damage)
                    // We'll use reflection to find HealthComponent and TakeDamage
                    var healthComp = AccessTools.Property(unitObj.GetType(), "Health")?.GetValue(unitObj) 
                                     ?? AccessTools.Field(unitObj.GetType(), "_health")?.GetValue(unitObj);
                    
                    if (healthComp == null)
                    {
                        // Try GetComponent if unit is a Component
                        if (unitObj is Component c)
                        {
                            // We assume the type name is "HealthComponent" based on AllClasses.txt
                            var hcType = AccessTools.TypeByName("BRG.Gameplay.Units.HealthComponent");
                            if (hcType != null)
                                healthComp = c.GetComponent(hcType);
                        }
                    }

                    if (healthComp != null)
                    {
                        // Deduct 2 HP per shot
                        // Method: TakeDamage(Vector2 dir, float amount, out bool died, bool ignoreInvincibility, bool ignoreShield, bool ignoreBlock)
                        // Signature might vary, let's look for one that takes float
                        var takeDmg = AccessTools.Method(healthComp.GetType(), "TakeDamage", 
                            new Type[] { typeof(Vector2), typeof(float), typeof(bool).MakeByRefType(), typeof(bool), typeof(bool), typeof(bool) });

                        if (takeDmg != null)
                        {
                            object[] args = new object[] { Vector2.zero, 2.0f, false, true, false, false };
                            takeDmg.Invoke(healthComp, args);
                           // ShotgunPlugin.Log.LogInfo("Shotgun Cost Applied: 2 HP");
                        }
                    }

                    // 3. SPAWN EXTRA PROJECTILES (The Shotgun Effect)
                    // We need the Projectile Prefab.
                    // Access field '_projectilePrefab' or similar.
                    var prefabField = AccessTools.Field(type, "_projectilePrefab") ?? AccessTools.Field(type, "projectilePrefab");
                    var prefab = prefabField?.GetValue(__instance) as GameObject;

                    if (prefab != null && unitObj is MonoBehaviour mb)
                    {
                        // Spawn 4 extra bullets
                        int pellets = 4;
                        float spreadAngle = 30f; // Total spread degrees

                        Vector3 spawnPos = mb.transform.position;
                        Quaternion baseRot = mb.transform.rotation;

                        // Identify the firing point/direction if possible.
                        // Often actions use the unit's forward or mouse position.
                        // We'll assume the Unit's rotation is the aim direction for now.

                        for (int i = 0; i < pellets; i++)
                        {
                            // Calculate spread
                            float angle = Mathf.Lerp(-spreadAngle / 2, spreadAngle / 2, (float)i / (pellets - 1));
                            
                            // Add some randomness
                            angle += Random.Range(-5f, 5f);

                            Quaternion rot = baseRot * Quaternion.Euler(0, 0, angle);

                            // Instantiate
                            var extraProj = UnityEngine.Object.Instantiate(prefab, spawnPos, rot);
                            
                            // We need to initialize the projectile.
                            // Usually there is a "Setup" or "Initialize" method.
                            // Or just activating it might be enough if it has Start() logic.
                            
                            // Check for "ProjectileMovement" component
                            // var pm = extraProj.GetComponent("ProjectileMovement");
                            // If it needs setup (damage, owner), we might be missing it here.
                            // But usually prefabs have default values.
                            
                            // Safety: Destroy after 5 seconds to prevent leaks if logic fails
                            UnityEngine.Object.Destroy(extraProj, 5.0f);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Suppress errors to avoid console spam during gameplay
                 ShotgunPlugin.Log.LogError("Shotgun Error: " + e.Message);
            }
        }
    }
}
