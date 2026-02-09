using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using NetAttackModUtils;
using BRG.Gameplay.Units;

namespace CustomEnemyMod
{
    [BepInPlugin("com.matissetec.customenemy", "Custom Enemy Mod", "1.1.0")]
    public class CustomEnemyPlugin : BaseUnityPlugin
    {
        public static CustomEnemyPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public static ScriptableObject GlitchEnemyTemplate;
        public const string GLITCH_ID = "ENEMY_GLITCH_V1";

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            Harmony harmony = new Harmony("com.matissetec.customenemy");
            
            // 1. Patch the database builder for early injection
            ModUtils.PatchSafe(harmony, Log, "BRG.DataManagement.DatabaseEnemyBuilder", "BuildEnemies", typeof(DatabasePatch));
            
            // 2. Patch Factory to add custom behavior
            ModUtils.PatchSafe(harmony, Log, "BRG.Gameplay.Units.EnemyFactory", "BuildEnemy", typeof(FactoryPatch));

            // 3. Apply all attribute-based patches (including PlayerSpawnPatch)
            harmony.PatchAll();

            // 4. Late Injection Fallback (if mod loaded after database built)
            StartCoroutine(LateInjectionRoutine());

            Log.LogInfo("Custom Enemy Mod v1.1.2 Loaded. Attribute patches applied.");
        }

        IEnumerator LateInjectionRoutine()
        {
            yield return new WaitForSeconds(3.0f);
            if (GlitchEnemyTemplate == null) {
                Log.LogInfo("Late Injection: Searching for EnemyDataBase...");
                var dbType = AccessTools.TypeByName("BRG.DataBase.EnemyDataBase");
                if (dbType != null) {
                    object instance = Resources.FindObjectsOfTypeAll(dbType).FirstOrDefault();
                    if (instance == null) {
                        var fi = dbType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                       .FirstOrDefault(f => f.Name.ToLower().Contains("instance") || f.FieldType == dbType);
                        instance = fi?.GetValue(null);
                    }

                    if (instance != null) {
                        var fields = dbType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach(var f in fields) {
                            if (typeof(IList).IsAssignableFrom(f.FieldType) && f.FieldType.IsGenericType) {
                                var list = f.GetValue(instance) as IList;
                                if (list != null && list.Count > 0 && list[0].GetType().Name.Contains("EnemySO")) {
                                    InjectIntoList(list);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void InjectIntoList(IList list)
        {
            if (GlitchEnemyTemplate != null) return;

            GlitchEnemyTemplate = ModUtils.CreateTemplate<ScriptableObject>(
                list, 
                "", 
                GLITCH_ID, 
                "GLITCH_ENTITY", 
                "A corrupted process that consumes system resources."
            );

            if (GlitchEnemyTemplate != null) {
                // Set scary stats
                ModUtils.SetField(GlitchEnemyTemplate, GlitchEnemyTemplate.GetType(), "maxHealth", 1200f);
                ModUtils.SetField(GlitchEnemyTemplate, GlitchEnemyTemplate.GetType(), "moveSpeed", 4.5f);
                ModUtils.SetField(GlitchEnemyTemplate, GlitchEnemyTemplate.GetType(), "Damage", 50f);
                
                list.Add(GlitchEnemyTemplate);
                Log.LogInfo($"[CustomEnemy] Successfully injected {GLITCH_ID} into database!");
            }
        }
    }

    public static class FactoryPatch
    {
        public static void Postfix(ScriptableObject __0, object __result)
        {
            if (__result == null || __0 == null) return;
            
            var comp = __result as Component;
            if (comp == null) return;
            GameObject go = comp.gameObject;
            
            if (__0 == CustomEnemyPlugin.GlitchEnemyTemplate || __0.name.Contains(CustomEnemyPlugin.GLITCH_ID)) {
                if (go.GetComponent<GlitchEnemyBehavior>() == null) {
                    go.AddComponent<GlitchEnemyBehavior>();
                    // Make it BIG
                    go.transform.localScale = Vector3.one * 2.5f;
                }
            }
        }
    }

    public class GlitchEnemyBehavior : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private float _timer;
        private Vector3 _baseScale;

        void Start()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        void Update()
        {
            // Flash colors
            _timer -= Time.deltaTime;
            if (_timer <= 0) {
                if (_renderer != null) 
                    _renderer.color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 0.8f);
                
                // Jitter scale
                transform.localScale = _baseScale * (1f + UnityEngine.Random.Range(-0.2f, 0.2f));
                _timer = 0.05f;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "Awake")]
    public static class PlayerSpawnPatch
    {
        [HarmonyPostfix]
        static void Postfix(Player __instance)
        {
            if (__instance.gameObject.GetComponent<SpawnTestUI>() == null)
                __instance.gameObject.AddComponent<SpawnTestUI>();
        }
    }

    public class SpawnTestUI : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.T)) {
                CustomEnemyPlugin.Log.LogInfo("T key pressed! Attempting to spawn Glitch Enemy...");
                SpawnGlitch();
            }
        }

        void SpawnGlitch()
        {
            if (CustomEnemyPlugin.GlitchEnemyTemplate == null) {
                CustomEnemyPlugin.Log.LogInfo("Glitch Template not loaded yet!");
                return;
            }

            try {
                var spawner = UnityEngine.Object.FindObjectOfType<BRG.Gameplay.Units.EnemySpawner>();
                if (spawner != null) {
                    CustomEnemyPlugin.Log.LogInfo("Found EnemySpawner. Invoking SpawnEnemy...");
                    
                    var mSpawn = AccessTools.Method(typeof(BRG.Gameplay.Units.EnemySpawner), "SpawnEnemy");
                    if (mSpawn != null) {
                        Vector2 spawnPos = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * 3f;
                        
                        // Try calling the spawner
                        object result = mSpawn.Invoke(spawner, new object[] { 
                            CustomEnemyPlugin.GlitchEnemyTemplate, 
                            0, 
                            spawnPos, 
                            true, 
                            1 
                        });

                        if (result != null) {
                            CustomEnemyPlugin.Log.LogInfo($"Spawned Glitch Entity via Spawner! Result type: {result.GetType().Name}");
                        } else {
                            // Fallback: Use Factory directly
                            CustomEnemyPlugin.Log.LogWarning("Spawner returned null. Attempting direct Factory build...");
                            var factory = UnityEngine.Object.FindObjectOfType<BRG.Gameplay.Units.EnemyFactory>();
                            if (factory != null) {
                                var mBuild = AccessTools.Method(typeof(BRG.Gameplay.Units.EnemyFactory), "BuildEnemy");
                                if (mBuild != null) {
                                    // BuildEnemy(EnemySO, Vector2, Boolean, float, float, float, float, Int32)
                                    mBuild.Invoke(factory, new object[] {
                                        CustomEnemyPlugin.GlitchEnemyTemplate, spawnPos, true, 0f, 0f, 0f, 1f, 1
                                    });
                                    CustomEnemyPlugin.Log.LogInfo("Spawned Glitch Entity via direct Factory call!");
                                }
                            }
                        }
                    } else {
                        CustomEnemyPlugin.Log.LogError("Could not find SpawnEnemy method on Spawner.");
                    }
                } else {
                    CustomEnemyPlugin.Log.LogError("EnemySpawner not found in scene!");
                }
            } catch (Exception e) {
                CustomEnemyPlugin.Log.LogError($"Spawn Failed: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    public static class DatabasePatch
    {
        public static void Postfix(object __instance)
        {
            var fields = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach(var f in fields) {
                if (typeof(IList).IsAssignableFrom(f.FieldType)) {
                    var list = f.GetValue(__instance) as IList;
                    if (list != null && list.Count > 0 && list[0].GetType().Name.Contains("EnemySO")) {
                        CustomEnemyPlugin.InjectIntoList(list);
                        return;
                    }
                }
            }
        }
    }
}