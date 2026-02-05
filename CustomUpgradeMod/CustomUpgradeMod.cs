using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using BRG.DataManagement;
using BRG.Gameplay;
using BRG.Gameplay.Units;

namespace CustomUpgradeMod
{
    [BepInPlugin("com.matissetec.customupgrade", "Custom Upgrade Mod", "1.0.0")]
    public class CustomUpgradePlugin : BaseUnityPlugin
    {
        public static CustomUpgradePlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        public const string CUSTOM_UPGRADE_ID = "custom_mega_speed_boost";
        public static int UpgradeLevel = 0;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(CustomUpgradePlugin));
            Harmony.CreateAndPatchAll(typeof(UpgradeInjectionPatch));
            Log.LogInfo("Custom Upgrade Mod Loaded.");
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        static void PlayerAwake_Patch(Player __instance)
        {
            if (__instance.gameObject.GetComponent<CustomUpgradeBehavior>() == null)
            {
                __instance.gameObject.AddComponent<CustomUpgradeBehavior>();
            }
        }

        [HarmonyPatch]
        public static class UpgradeInjectionPatch
        {
            static MethodBase TargetMethod() => AccessTools.Method("BRG.DataManagement.DatabaseUpgradeBuilder:BuildUpgrades");

            [HarmonyPostfix]
            static void Postfix(object __instance)
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var runField = __instance.GetType().GetField("_runUpgrades", flags);
                IList runUpgrades = runField?.GetValue(__instance) as IList;

                if (runUpgrades != null)
                {
                    try {
                        InjectCustomUpgrade(runUpgrades);
                    } catch (Exception e) {
                        CustomUpgradePlugin.Log.LogError($"Failed to inject custom upgrade: {e}");
                    }
                }
            }

            static void InjectCustomUpgrade(IList list)
            {
                // Check if already injected
                Type upgradeType = AccessTools.TypeByName("MetagameUpgradeSO");
                foreach (var item in list)
                {
                    if (item == null) continue;
                    var id = AccessTools.Field(upgradeType, "id")?.GetValue(item) as string;
                    if (id == CUSTOM_UPGRADE_ID) return;
                }

                // Find an existing upgrade to clone (e.g., Collection Radius)
                object template = null;
                const string TEMPLATE_ID = "biSKigvSH0meTM/+oJv8ig"; // Collection Radius

                foreach (var item in list)
                {
                    if (item == null) continue;
                    var id = AccessTools.Field(upgradeType, "id")?.GetValue(item) as string;
                    if (id == TEMPLATE_ID)
                    {
                        template = item;
                        break;
                    }
                }

                if (template == null && list.Count > 0) template = list[0];

                if (template != null)
                {
                    // Clone the template
                    var newUpgrade = Instantiate((ScriptableObject)template);
                    
                    // Set custom properties
                    AccessTools.Field(upgradeType, "id").SetValue(newUpgrade, CUSTOM_UPGRADE_ID);
                    
                    // Unity Object name (important for some internal lookups)
                    newUpgrade.name = "MegaSpeedUpgrade";

                    // Try to set display name and description if fields exist
                    // We use common names found in many games
                    var nameField = AccessTools.Field(upgradeType, "_name") ?? AccessTools.Field(upgradeType, "displayName") ?? AccessTools.Field(upgradeType, "Name");
                    if (nameField != null) nameField.SetValue(newUpgrade, "OVERCLOCK SNEAKERS");

                    var descField = AccessTools.Field(upgradeType, "_description") ?? AccessTools.Field(upgradeType, "description") ?? AccessTools.Field(upgradeType, "Description");
                    if (descField != null) descField.SetValue(newUpgrade, "Increases movement speed by 25% per stack.");

                    var priceField = AccessTools.Field(upgradeType, "_price") ?? AccessTools.Field(upgradeType, "price");
                    if (priceField != null) priceField.SetValue(newUpgrade, 50);

                    var rarityField = AccessTools.Field(upgradeType, "_rarity") ?? AccessTools.Field(upgradeType, "rarity");
                    if (rarityField != null) rarityField.SetValue(newUpgrade, 0); // 0 = Common/Frequent

                    list.Insert(0, newUpgrade);
                    CustomUpgradePlugin.Log.LogInfo("Successfully injected 'OVERCLOCK SNEAKERS' upgrade.");
                }
            }
        }
    }

    public class CustomUpgradeBehavior : MonoBehaviour
    {
        private float _lastSpeedBoost = 0f;
        private MonoBehaviour _movementComponent;
        private float _checkTimer = 0f;
        private float _originalSpeed = -1f;

        void Start()
        {
            // PlayerMovement is usually on the same GO or a child
            _movementComponent = GetComponent<MonoBehaviour>(); // Fallback
            var all = GetComponents<MonoBehaviour>();
            foreach(var mb in all) {
                if (mb.GetType().Name == "PlayerMovement") {
                    _movementComponent = mb;
                    break;
                }
            }
        }

        void Update()
        {
            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f)
            {
                _checkTimer = 1.0f; // Check every second
                UpdateSpeedBoost();
            }
        }

        void UpdateSpeedBoost()
        {
            int count = 0;
            try {
                var asm = typeof(BRG.DataManagement.DatabaseUpgradeBuilder).Assembly;
                var rdcType = asm.GetType("BRG.DataManagement.RunDataController");
                if (rdcType != null) {
                    var instance = rdcType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (instance != null) {
                        var list = rdcType.GetMethod("GetRunUpgrades")?.Invoke(instance, null) as IList;
                        if (list != null) {
                            Type upgradeType = AccessTools.TypeByName("MetagameUpgradeSO");
                            var idField = AccessTools.Field(upgradeType, "id");
                            foreach (var item in list) {
                                if (item == null) continue;
                                var id = idField.GetValue(item) as string;
                                if (id == CustomUpgradePlugin.CUSTOM_UPGRADE_ID) {
                                    count++;
                                }
                            }
                        }
                    }
                }
            } catch {}

            float boost = count * 0.25f;
            if (boost != _lastSpeedBoost)
            {
                ApplySpeedBoost(boost);
                _lastSpeedBoost = boost;
            }
        }

        void ApplySpeedBoost(float boost)
        {
            if (_movementComponent == null) return;

            try {
                var type = _movementComponent.GetType();
                var speedFi = AccessTools.Field(type, "_speed") ?? AccessTools.Field(type, "speed") ?? AccessTools.Field(type, "_moveSpeed");
                
                if (speedFi != null) {
                    float currentSpeed = (float)speedFi.GetValue(_movementComponent);
                    
                    if (_originalSpeed < 0) {
                        _originalSpeed = currentSpeed;
                    }

                    float newSpeed = _originalSpeed * (1f + boost);
                    speedFi.SetValue(_movementComponent, newSpeed);
                    CustomUpgradePlugin.Log.LogInfo($"[CustomUpgrade] Speed Updated! Boost: {boost*100}% | New Speed: {newSpeed:F2}");
                } else {
                    CustomUpgradePlugin.Log.LogWarning("[CustomUpgrade] Could not find speed field on " + type.Name);
                }
            } catch (Exception e) {
                CustomUpgradePlugin.Log.LogError($"[CustomUpgrade] Speed Apply Error: {e.Message}");
            }
        }
    }
}
