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

namespace HyperSpeedMod
{
    [BepInPlugin("com.matissetec.hyperspeed", "Hyper Speed Mod", "1.0.0")]
    public class HyperSpeedPlugin : BaseUnityPlugin
    {
        public static HyperSpeedPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public const string SPEED_ID = "HYPER_SPEED_V3_ID";
        public static ScriptableObject SpeedTemplate;
        public static int SpeedStacks = 0;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            SpeedStacks = 0;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.hyperspeed");
                ModUtils.PatchSafe(harmony, Log, "BRG.DataManagement.DatabaseUpgradeBuilder", "BuildUpgrades", typeof(UpgradeDatabasePatch));
                ModUtils.PatchSafe(harmony, Log, "BRG.UI.UpgradeShop", "SetupShop", typeof(ShopHijackPatch));
                ModUtils.PatchSafe(harmony, Log, "BRG.Gameplay.Upgrades.RunUpgradeShopController", "OnUpgradeSelected", typeof(SelectionTrackerPatch));
                
                var i2Loc = AccessTools.TypeByName("I2.Loc.LocalizationManager");
                if (i2Loc != null) {
                    var mGetTranslation = i2Loc.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                            .FirstOrDefault(m => m.Name == "GetTranslation" && m.GetParameters().Length > 0);
                    if (mGetTranslation != null) {
                        harmony.Patch(mGetTranslation, new HarmonyMethod(AccessTools.Method(typeof(StringLocPatch), "Prefix")));
                    }
                }

                harmony.PatchAll(typeof(PlayerPatch));
                Log.LogInfo(">>> HYPER SPEED MOD ONLINE <<<");
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
            if (Term == "HYPER_SPEED_KEY") { __result = "HYPER SPEED V3"; return false; }
            if (Term == "HYPER_SPEED_DESC") { __result = "Gives +1000% Movement Speed and Grows Player."; return false; }
            return true;
        }
    }

    public static class UpgradeDatabasePatch
    {
        public static void Postfix(object __instance)
        {
            try {
                if (HyperSpeedPlugin.SpeedTemplate != null) return;

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var listFi = __instance.GetType().GetFields(flags).FirstOrDefault(f => f.Name == "_runUpgrades" || f.Name == "_gameplayUpgrades");
                var list = listFi?.GetValue(__instance) as IList;

                if (list != null && list.Count > 0) {
                    var template = list[0] as ScriptableObject;
                    if (template != null) {
                        HyperSpeedPlugin.SpeedTemplate = UnityEngine.Object.Instantiate(template);
                        ModUtils.ApplyMetadata(HyperSpeedPlugin.SpeedTemplate, HyperSpeedPlugin.SPEED_ID, "HYPER_SPEED_KEY", "HYPER_SPEED_DESC");
                        HyperSpeedPlugin.Log.LogWarning("Hyper Speed Template Created.");
                    }
                }
            } catch {}
        }
    }

    public static class ShopHijackPatch
    {
        public static void Prefix(object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            var upgrades = __args[0] as IList;
            if (upgrades == null || upgrades.Count < 1) return;

            try {
                // Hijack Slot 0
                if (HyperSpeedPlugin.SpeedStacks < 5) upgrades[0] = HyperSpeedPlugin.SpeedTemplate;
            } catch {}
        }
    }

    public static class SelectionTrackerPatch
    {
        public static void Prefix(object __instance, object[] __args)
        {
            try {
                if (__args == null || __args.Length == 0) return;
                object message = __args[0];
                if (message == null) return;

                var listFi = __instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(f => f.Name == "_upgradeSOs");
                var list = listFi?.GetValue(__instance) as IList;
                if (list != null && list.Count >= 1) {
                    if (HyperSpeedPlugin.SpeedTemplate != null) list[0] = HyperSpeedPlugin.SpeedTemplate;
                }

                var msgFields = message.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var indexFi = msgFields.FirstOrDefault(f => f.Name.ToLower().Contains("index"));
                int selectedIndex = (indexFi != null) ? (int)indexFi.GetValue(message) : -1;
                var upgradeFi = msgFields.FirstOrDefault(f => f.Name.Contains("Upgrade") || f.FieldType.Name.Contains("SO"));

                if (selectedIndex == 0 || (upgradeFi != null && upgradeFi.GetValue(message) == HyperSpeedPlugin.SpeedTemplate)) {
                    if (HyperSpeedPlugin.SpeedStacks < 5) HyperSpeedPlugin.SpeedStacks++;
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
            if (__instance.gameObject.GetComponent<HyperSpeedBehavior>() == null)
                __instance.gameObject.AddComponent<HyperSpeedBehavior>();
        }
    }

    public class HyperSpeedBehavior : MonoBehaviour
    {
        private MonoBehaviour _movementComponent;
        private SpriteRenderer _spriteRenderer;
        
        private float _originalSpeed = -1f;
        private Vector3 _originalScale = Vector3.one;
        private bool _statsCaptured = false;

        void Start()
        {
            foreach(var mb in GetComponents<MonoBehaviour>()) {
                if (mb.GetType().Name == "PlayerMovement") { _movementComponent = mb; break; }
            }
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _originalScale = transform.localScale;
        }

        void Update()
        {
            if (!_statsCaptured || _originalSpeed <= 0) CaptureOriginalStats();

            int speedStacks = Mathf.Min(HyperSpeedPlugin.SpeedStacks, 5);
            if (_statsCaptured && speedStacks > 0) ApplySpeedEffects(speedStacks);
        }

        void CaptureOriginalStats()
        {
            if (_movementComponent == null) return;
            try {
                var type = _movementComponent.GetType();
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var speedFi = fields.FirstOrDefault(f => f.Name == "_speed" || f.Name == "speed" || f.Name == "_moveSpeed");
                if (speedFi != null) {
                    float val = (float)speedFi.GetValue(_movementComponent);
                    if (val > 0) { _originalSpeed = val; _statsCaptured = true; }
                }
            } catch {}
        }

        void ApplySpeedEffects(int stacks)
        {
            if (_movementComponent != null) {
                var fields = _movementComponent.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var speedFi = fields.FirstOrDefault(f => f.Name == "_speed" || f.Name == "speed" || f.Name == "_moveSpeed");
                if (speedFi != null) speedFi.SetValue(_movementComponent, _originalSpeed * (1f + (stacks * 10.0f)));
            }
            transform.localScale = _originalScale * (1f + (stacks * 0.5f));
            if (_spriteRenderer != null) {
                float t = Mathf.PingPong(Time.time * 5f, 1f);
                _spriteRenderer.color = Color.Lerp(Color.white, Color.magenta, t);
            }
        }
    }
}