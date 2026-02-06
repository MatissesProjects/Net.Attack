using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BRG.DataManagement;
using BRG.Gameplay.Units;
using NetAttackModUtils;

namespace SizeIncreaseMod
{
    [BepInPlugin("com.matissetec.sizeincrease", "Size Increase Mod", "1.0.0")]
    public class SizeIncreasePlugin : BaseUnityPlugin
    {
        public static SizeIncreasePlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public const string UPGRADE_ID = "GIANT_GROWTH_V1_ID";
        public static ScriptableObject UpgradeTemplate;
        public static int Stacks = 0;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            Stacks = 0;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.sizeincrease");
                ModUtils.PatchSafe(harmony, Log, "BRG.DataManagement.DatabaseUpgradeBuilder", "BuildUpgrades", typeof(UpgradeDatabasePatch));
                
                // Register with centralized pool
                ModUtils.RegisterModdedUpgrade(harmony, () => UpgradeTemplate, () => Stacks < 5);
                
                ModUtils.AddUpgradeSelectionTracker(harmony, (index, upgrade) => {
                    if (upgrade != null && upgrade == UpgradeTemplate) {
                        if (Stacks < 5) Stacks++;
                    }
                });

                var i2Loc = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                if (i2Loc != null) {
                    var mGetTranslation = i2Loc.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                            .FirstOrDefault(m => m.Name == "GetTranslation" && m.GetParameters().Length > 0);
                    if (mGetTranslation != null) {
                        harmony.Patch(mGetTranslation, new HarmonyMethod(AccessTools.Method(typeof(StringLocPatch), "Prefix")));
                    }
                }

                harmony.PatchAll(typeof(PlayerPatch));
                Log.LogInfo(">>> SIZE INCREASE MOD ONLINE <<<");
            } catch (Exception e) {
                Log.LogError("Patching Failed: " + e);
            }
        }
    }

    public static class StringLocPatch
    {
        public static bool Prefix(string Term, ref string __result)
        {
            if (string.IsNullOrEmpty(Term)) return true;
            if (Term == "GIANT_GROWTH_KEY") { __result = "GIANT GROWTH"; return false; }
            if (Term == "GIANT_GROWTH_DESC") { __result = "Increases player size by 50% per stack."; return false; }
            return true;
        }
    }

    public static class UpgradeDatabasePatch
    {
        public static void Postfix(object __instance)
        {
            try {
                if (SizeIncreasePlugin.UpgradeTemplate != null) return;

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var listFi = __instance.GetType().GetFields(flags).FirstOrDefault(f => f.Name == "_runUpgrades" || f.Name == "_gameplayUpgrades");
                var list = listFi?.GetValue(__instance) as IList;

                if (list != null && list.Count > 0) {
                    SizeIncreasePlugin.UpgradeTemplate = ModUtils.CreateTemplate<ScriptableObject>(list, "", SizeIncreasePlugin.UPGRADE_ID, "GIANT_GROWTH_KEY", "GIANT_GROWTH_DESC");
                    SizeIncreasePlugin.Log.LogWarning("Giant Growth Template Created.");
                }
            } catch {}
        }
    }

    public static class PlayerPatch
    {
        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        static void Postfix(Player __instance)
        {
            if (__instance.gameObject.GetComponent<SizeIncreaseBehavior>() == null)
                __instance.gameObject.AddComponent<SizeIncreaseBehavior>();
        }
    }

    public class SizeIncreaseBehavior : MonoBehaviour
    {
        private Vector3 _originalScale = Vector3.one;
        private bool _statsCaptured = false;
        private SpriteRenderer _spriteRenderer;

        void Start()
        {
            _originalScale = transform.localScale;
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            int stacks = Mathf.Min(SizeIncreasePlugin.Stacks, 5);
            if (stacks > 0) {
                transform.localScale = _originalScale * (1f + (stacks * 0.5f));
                if (_spriteRenderer != null) {
                    float t = Mathf.PingPong(Time.time * 2f, 1f);
                    _spriteRenderer.color = Color.Lerp(Color.white, Color.green, t * 0.5f);
                }
            }
        }
    }
}