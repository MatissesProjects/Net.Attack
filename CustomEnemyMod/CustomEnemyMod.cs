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
    [BepInPlugin("com.matissetec.customenemy", "Custom Enemy Mod", "1.1.9")]
    public class CustomEnemyPlugin : BaseUnityPlugin
    {
        public static CustomEnemyPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public static ScriptableObject GlitchEnemyTemplate;
        public static ScriptableObject HighTierTemplate;
        public const string GLITCH_ID = "ENEMY_GLITCH_V1";

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            Harmony harmony = new Harmony("com.matissetec.customenemy");
            
            ModUtils.PatchSafe(harmony, Log, "BRG.DataManagement.DatabaseEnemyBuilder", "BuildEnemies", typeof(DatabasePatch));
            ModUtils.PatchSafe(harmony, Log, "BRG.Gameplay.Units.EnemyFactory", "BuildEnemy", typeof(FactoryPatch));

            // Manual Patch for Spawner
            try {
                var mKilled = AccessTools.Method("BRG.Gameplay.Collectables.CollectableSpawner:OnEnemyKilled");
                if (mKilled != null) {
                    harmony.Patch(mKilled, new HarmonyMethod(typeof(LootPatch), nameof(LootPatch.Prefix)));
                    Log.LogInfo("Successfully patched CollectableSpawner:OnEnemyKilled");
                }
            } catch (Exception e) { Log.LogError($"Spawner patch failed: {e.Message}"); }

            // Manual Patch for Player UI
            try {
                var mAwake = AccessTools.Method(typeof(Player), "Awake");
                if (mAwake != null) {
                    harmony.Patch(mAwake, null, new HarmonyMethod(typeof(PlayerSpawnPatch), nameof(PlayerSpawnPatch.Postfix)));
                }
            } catch {}

            StartCoroutine(LateInjectionRoutine());
            Log.LogInfo("Custom Enemy Mod v1.1.9 Loaded.");
        }

        IEnumerator LateInjectionRoutine()
        {
            yield return new WaitForSeconds(3.0f);
            if (GlitchEnemyTemplate == null) {
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

            // 1. Find a boss to use as a loot template
            foreach (var item in list) {
                var so = item as ScriptableObject;
                if (so == null) continue;
                if (so.name.ToLower().Contains("boss") || so.name.ToLower().Contains("titan")) {
                    HighTierTemplate = so;
                    break;
                }
            }
            if (HighTierTemplate == null && list.Count > 0) HighTierTemplate = list[0] as ScriptableObject;

            // 2. Create Glitch Entity
            GlitchEnemyTemplate = ModUtils.CreateTemplate<ScriptableObject>(
                list, 
                "", 
                GLITCH_ID, 
                "GLITCH_ENTITY", 
                "A corrupted process. Extremely unstable loot table."
            );

            if (GlitchEnemyTemplate != null) {
                ModUtils.SetField(GlitchEnemyTemplate, GlitchEnemyTemplate.GetType(), "maxHealth", 5000f);
                list.Add(GlitchEnemyTemplate);
                Log.LogInfo($"[CustomEnemy] Injected {GLITCH_ID}");
            }
        }

        public static T GetField<T>(object obj, string name) {
            if (obj == null) return default(T);
            var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) {
                try { return (T)f.GetValue(obj); } catch {}
            }
            return default(T);
        }
    }

    public static class LootPatch
    {
        // When OnEnemyKilled(Enemy enemy) is called
        public static void Prefix(Enemy __0) 
        {
            if (__0 == null) return;
            try {
                var soField = typeof(Enemy).GetField("_enemySO", BindingFlags.Instance | BindingFlags.NonPublic) ?? 
                              typeof(Enemy).GetField("enemySO", BindingFlags.Instance | BindingFlags.Public);
                
                if (soField == null) return;
                var currentSO = soField.GetValue(__0) as ScriptableObject;

                if (currentSO != null && (currentSO == CustomEnemyPlugin.GlitchEnemyTemplate || currentSO.name.Contains(CustomEnemyPlugin.GLITCH_ID))) {
                    CustomEnemyPlugin.Log.LogInfo("GLITCH KILLED: Hijacking loot table to BOSS tier...");
                    // Temporarily swap the SO to a Boss SO right before the spawner reads it
                    if (CustomEnemyPlugin.HighTierTemplate != null) {
                        soField.SetValue(__0, CustomEnemyPlugin.HighTierTemplate);
                    }
                }
            } catch (Exception e) {
                CustomEnemyPlugin.Log.LogError($"Loot Hijack Error: {e.Message}");
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
            
            if (__0 == CustomEnemyPlugin.GlitchEnemyTemplate || __0.name.Contains(CustomEnemyPlugin.GLITCH_ID)) {
                if (comp.gameObject.GetComponent<GlitchEnemyBehavior>() == null) {
                    comp.gameObject.AddComponent<GlitchEnemyBehavior>();
                    comp.gameObject.transform.localScale = Vector3.one * 2.5f;
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
            _timer -= Time.deltaTime;
            if (_timer <= 0) {
                if (_renderer != null) 
                    _renderer.color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 0.8f);
                transform.localScale = _baseScale * (1f + UnityEngine.Random.Range(-0.2f, 0.2f));
                _timer = 0.05f;
            }
        }
    }

    public static class PlayerSpawnPatch
    {
        public static void Postfix(Player __instance)
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
                SpawnGlitch();
            }
        }

        void SpawnGlitch()
        {
            if (CustomEnemyPlugin.GlitchEnemyTemplate == null) return;
            try {
                var spawner = UnityEngine.Object.FindObjectOfType<BRG.Gameplay.Units.EnemySpawner>();
                if (spawner != null) {
                    var mSpawn = AccessTools.Method(typeof(BRG.Gameplay.Units.EnemySpawner), "SpawnEnemy");
                    if (mSpawn != null) {
                        mSpawn.Invoke(spawner, new object[] { 
                            CustomEnemyPlugin.GlitchEnemyTemplate, 0, 
                            (Vector2)transform.position + UnityEngine.Random.insideUnitCircle.normalized * 5f, 
                            true, 1 
                        });
                    }
                }
            } catch {}
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