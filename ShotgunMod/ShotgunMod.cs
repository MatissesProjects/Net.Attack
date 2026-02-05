using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BRG.Gameplay.Units; 
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
            
            try {
                Harmony harmony = new Harmony("com.matissetec.shotgunmod");
                harmony.PatchAll(); 
                Log.LogInfo(">>> SHOTGUN MOD (V7) RECURSIVE ONLINE <<<");
            } catch (Exception e) {
                Log.LogError("Shotgun Patching Failed: " + e);
            }
        }
    }

    [HarmonyPatch]
    public static class ShotgunActionPatch
    {
        private static bool _isInternal = false;

        static IEnumerable<MethodBase> TargetMethods()
        {
            string[] actionTypes = {
                "BRG.NodeSystem.Actions.ProjectileAction",
                "BRG.NodeSystem.Actions.ShotgunProjectileAction",
                "BRG.NodeSystem.Actions.LineProjectileAction",
                "BRG.NodeSystem.Actions.ThrowProjectileAction",
                "BRG.NodeSystem.Actions.CircleProjectileAction",
                "BRG.NodeSystem.Actions.BeamAction",
                "BRG.NodeSystem.Actions.LaserAction"
            };

            foreach (var typeName in actionTypes)
            {
                var method = AccessTools.Method(typeName + ":Execute");
                if (method != null) yield return method;
            }
        }

        [HarmonyPrefix]
        static void Prefix(object __instance, object[] __args)
        {
            if (_isInternal) return;

            try 
            {
                var type = __instance.GetType();

                // 1. IDENTIFY PLAYER
                var acField = AccessTools.Field(type, "_actionController");
                if (acField == null) return;
                
                var ac = acField.GetValue(__instance) as MonoBehaviour;
                if (ac == null) return;
                
                bool isPlayer = ac.name.Contains("Player") || ac.GetComponentInParent<Player>() != null;
                if (!isPlayer) return;

                // 2. FIRST START CHECK (Prevent suicide on continuous actions)
                var fsField = AccessTools.Field(type, "_firstStart");
                if (fsField != null && !(bool)fsField.GetValue(__instance)) return;

                // 3. TRIGGER SHOTGUN BURST (Recursive firing)
                _isInternal = true;
                try {
                    ApplyShotgunCost(ac.gameObject);
                    
                    int extraShots = 4;
                    float spread = 30f;
                    var executeMethod = AccessTools.Method(type, "Execute");

                    if (executeMethod != null)
                    {
                        ShotgunPlugin.Log.LogInfo($"Shotgun: Bursting {type.Name}");
                        
                        Quaternion originalRot = ac.transform.rotation;

                        for (int i = 0; i < extraShots; i++)
                        {
                            float angle = Mathf.Lerp(-spread/2, spread/2, (float)i / (extraShots-1));
                            angle += Random.Range(-5f, 5f);

                            // TEMPORARILY ROTATE THE FIRING UNIT
                            ac.transform.rotation = originalRot * Quaternion.Euler(0, 0, angle);

                            // CALL THE ORIGINAL EXECUTE LOGIC
                            // This ensures the game handles speed, damage, and visuals correctly.
                            var result = executeMethod.Invoke(__instance, __args);
                            
                            // If it's a coroutine (standard for actions), start it
                            if (result is IEnumerator coroutine)
                            {
                                ac.StartCoroutine(coroutine);
                            }
                        }

                        // RESTORE ROTATION
                        ac.transform.rotation = originalRot;
                    }
                } finally {
                    _isInternal = false;
                }
            }
            catch (Exception e)
            {
                ShotgunPlugin.Log.LogError("Shotgun V7 Error: " + e.Message);
                _isInternal = false;
            }
        }

        static void ApplyShotgunCost(GameObject playerGO)
        {
            try {
                var hc = playerGO.GetComponentInChildren<HealthComponent>() ?? playerGO.GetComponentInParent<HealthComponent>();
                if (hc != null)
                {
                    // Find TakeDamage with any vector/float signature
                    var methods = hc.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo target = null;
                    foreach (var m in methods) {
                        if (m.Name == "TakeDamage") {
                            var p = m.GetParameters();
                            if (p.Length >= 2 && p[1].ParameterType == typeof(float)) {
                                target = m;
                                break;
                            }
                        }
                    }

                    if (target != null) {
                        var pCount = target.GetParameters().Length;
                        object[] args = new object[pCount];
                        args[0] = Vector3.zero;
                        args[1] = 2.0f; // Cost
                        if (pCount > 2) args[2] = false; // died ref
                        for (int i=3; i<pCount; i++) args[i] = true; // ignores
                        target.Invoke(hc, args);
                    } else {
                        playerGO.SendMessage("Heal", -2.0f, SendMessageOptions.DontRequireReceiver);
                    }
                }
            } catch {}
        }
    }
}