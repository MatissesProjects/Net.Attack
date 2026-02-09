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

namespace BossRushMod
{
    [BepInPlugin("com.matissetec.bossrush", "Boss Rush Mod", "1.9.0")]
    public class BossRushPlugin : BaseUnityPlugin
    {
        public static BossRushPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public static bool IsBossRushActive = false;
        public static List<ScriptableObject> BossEnemies = new List<ScriptableObject>();

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            Harmony harmony = new Harmony("com.matissetec.bossrush");
            
            // Hook DB Builder
            ModUtils.PatchSafe(harmony, Log, "BRG.DataManagement.DatabaseEnemyBuilder", "BuildEnemies", typeof(DatabasePatch));

            // Hook Spawn methods
            harmony.PatchAll(typeof(FactoryPatch));
            
            // Attach UI
            harmony.PatchAll(typeof(BossRushPlugin));
            
            // Fallback: If DB already built, find it manually
            StartCoroutine(ForceScanRoutine());
            
            Log.LogInfo("Boss Rush Mod v1.9.0 Loaded.");
        }

        IEnumerator ForceScanRoutine()
        {
            yield return new WaitForSeconds(3.0f);
            if (BossEnemies.Count == 0) {
                Log.LogInfo("ForceScan: Attempting to access EnemyDataBase directly...");
                ScanDatabaseDirectly();
            }
        }

        void ScanDatabaseDirectly()
        {
            try {
                var dbType = AccessTools.TypeByName("BRG.DataBase.EnemyDataBase");
                if (dbType == null) return;

                // Find Instance
                var instanceProp = dbType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object instance = null;
                if (instanceProp != null) instance = instanceProp.GetValue(null);
                
                // If no property, check fields
                if (instance == null) {
                    var instanceFi = dbType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                           .FirstOrDefault(f => f.Name.ToLower().Contains("instance") || f.FieldType == dbType);
                    if (instanceFi != null) instance = instanceFi.GetValue(null);
                }

                // If still null, try finding object
                if (instance == null) instance = Resources.FindObjectsOfTypeAll(dbType).FirstOrDefault();

                if (instance != null) {
                    // Find the list
                    var fields = dbType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach(var f in fields) {
                        if (typeof(IList).IsAssignableFrom(f.FieldType) && f.FieldType.IsGenericType) {
                            var list = f.GetValue(instance) as IList;
                            if (list != null && list.Count > 0) {
                                // Check if items look like enemies
                                var first = list[0];
                                if (first != null && first.GetType().Name.Contains("EnemySO")) {
                                    ProcessEnemyList(list);
                                    return;
                                }
                            }
                        }
                    }
                } else {
                    Log.LogWarning("ForceScan: EnemyDataBase Instance not found.");
                }
            } catch (Exception e) {
                Log.LogError($"ForceScan Error: {e.Message}");
            }
        }

        public static void ProcessEnemyList(IList list)
        {
            if (list == null) return;
            Log.LogInfo($"Scanning {list.Count} entries from Database...");
            
            BossEnemies.Clear();
            foreach (var obj in list) {
                var so = obj as ScriptableObject;
                if (so == null) continue;

                string id = GetField<string>(so, "id") ?? GetField<string>(so, "_id") ?? so.name;
                
                // HP Search
                float hp = 0;
                var fields = so.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach(var f in fields) {
                    // Use the exact field name we found in the dump earlier: "_ufle12jhs77_Health"
                    if (f.Name.Contains("Health")) {
                        try { hp = (float)f.GetValue(so); if (hp > 0) break; } catch {}
                    }
                }

                // Criteria
                bool isBoss = (hp >= 400f) || 
                              id.ToLower().Contains("boss") || 
                              id.ToLower().Contains("tier3") || 
                              id.ToLower().Contains("titan") || 
                              id.ToLower().Contains("construct");

                if (isBoss) {
                    BossEnemies.Add(so);
                    Log.LogInfo($"[BOSS] {id} (HP:{hp})");
                }
            }
            Log.LogInfo($"Pool Size: {BossEnemies.Count}");
        }

        public static T GetField<T>(object obj, string name) {
            if (obj == null) return default(T);
            var f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) {
                try { return (T)f.GetValue(obj); } catch {}
            }
            return default(T);
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        static void PlayerAwake_Patch(Player __instance)
        {
            if (__instance.gameObject.GetComponent<BossRushBehavior>() == null)
                __instance.gameObject.AddComponent<BossRushBehavior>();
        }
    }

    public static class DatabasePatch
    {
        public static void Postfix(object __instance)
        {
            try {
                // Try to find the list field dynamically
                var fields = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach(var f in fields) {
                    if (typeof(IList).IsAssignableFrom(f.FieldType)) {
                        var list = f.GetValue(__instance) as IList;
                        if (list != null && list.Count > 0) {
                            if (list[0].GetType().Name.Contains("EnemySO")) {
                                BossRushPlugin.ProcessEnemyList(list);
                                return;
                            }
                        }
                    }
                }
            } catch (Exception e) {
                BossRushPlugin.Log.LogError($"DatabasePatch Error: {e}");
            }
        }
    }

    [HarmonyPatch]
    public static class FactoryPatch 
    {
        [HarmonyPatch(typeof(BRG.Gameplay.Units.EnemyFactory), "BuildEnemy")]
        [HarmonyPrefix]
        public static void Prefix_BuildEnemy(ref ScriptableObject __0) 
        {
            ApplyReplacement(ref __0, "BuildEnemy");
        }

        [HarmonyPatch(typeof(BRG.Gameplay.Units.EnemySpawner), "SpawnEnemy")]
        [HarmonyPrefix]
        public static void Prefix_SpawnEnemy(ref ScriptableObject __0) 
        {
            ApplyReplacement(ref __0, "SpawnEnemy");
        }

        private static void ApplyReplacement(ref ScriptableObject enemySO, string source)
        {
            if (!BossRushPlugin.IsBossRushActive || BossRushPlugin.BossEnemies.Count == 0) return;
            if (enemySO == null) return;

            if (BossRushPlugin.BossEnemies.Contains(enemySO)) return;

            var randomBoss = BossRushPlugin.BossEnemies[UnityEngine.Random.Range(0, BossRushPlugin.BossEnemies.Count)];
            BossRushPlugin.Log.LogInfo($"BOSS RUSH [{source}]: Swapping {enemySO.name} for {randomBoss.name}");
            
            enemySO = randomBoss;
        }
    }

    public class BossRushBehavior : MonoBehaviour
    {
        private float _uiTimer = 0f;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) {
                BossRushPlugin.IsBossRushActive = !BossRushPlugin.IsBossRushActive;
                _uiTimer = 3.0f;
                BossRushPlugin.Log.LogInfo($"Boss Rush Active: {BossRushPlugin.IsBossRushActive}");
            }
            if (_uiTimer > 0) _uiTimer -= Time.unscaledDeltaTime;
        }

        void OnGUI()
        {
            if (_uiTimer <= 0 && !BossRushPlugin.IsBossRushActive) return;

            float width = 240f;
            float height = 60f;
            float x = Screen.width / 2f - width / 2f;
            float y = 120f;

            GUI.depth = -200;
            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            GUI.color = BossRushPlugin.IsBossRushActive ? Color.red : Color.gray;
            GUI.DrawTexture(new Rect(x, y, width, 2), Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 22,
                normal = { textColor = BossRushPlugin.IsBossRushActive ? Color.red : Color.white }
            };

            string text = BossRushPlugin.IsBossRushActive ? "!!! BOSS RUSH !!!" : "BOSS RUSH: OFF";
            GUI.Label(new Rect(x, y, width, height), text, style);
            
            if (BossRushPlugin.IsBossRushActive) {
                style.fontSize = 10;
                style.normal.textColor = new Color(1, 0, 0, 0.6f);
                GUI.Label(new Rect(x, y + height - 15, width, 15), $"POOL SIZE: {BossRushPlugin.BossEnemies.Count}", style);
            }
        }
    }
}