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

namespace VortexBladeMod
{
    [BepInPlugin("com.matissetec.vortexblade", "Vortex Blade Mod", "1.0.0")]
    public class VortexBladePlugin : BaseUnityPlugin
    {
        public static VortexBladePlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        
        public const string BLADE_ID = "VORTEX_BLADE_V1_ID";
        public static ScriptableObject BladeTemplate;
        public static int BladeStacks = 0;
        public static int PlayerLevel = 1;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            BladeStacks = 0;
            
            try {
                Harmony harmony = new Harmony("com.matissetec.vortexblade");
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
                Log.LogInfo(">>> VORTEX BLADE MOD ONLINE <<<");
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
            if (Term == "VORTEX_BLADE_KEY") { __result = "VORTEX BLADE"; return false; }
            if (Term == "VORTEX_BLADE_DESC") { __result = "Spectral blades orbit you, slicing through enemies."; return false; }
            return true;
        }
    }

    public static class UpgradeDatabasePatch
    {
        public static void Postfix(object __instance)
        {
            try {
                if (VortexBladePlugin.BladeTemplate != null) return;

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var listFi = __instance.GetType().GetFields(flags).FirstOrDefault(f => f.Name == "_runUpgrades" || f.Name == "_gameplayUpgrades");
                var list = listFi?.GetValue(__instance) as IList;

                if (list != null && list.Count > 0) {
                    var template = list[0] as ScriptableObject;
                    if (template != null) {
                        VortexBladePlugin.BladeTemplate = UnityEngine.Object.Instantiate(template);
                        ModUtils.ApplyMetadata(VortexBladePlugin.BladeTemplate, VortexBladePlugin.BLADE_ID, "VORTEX_BLADE_KEY", "VORTEX_BLADE_DESC");
                        VortexBladePlugin.Log.LogWarning("Vortex Blade Template Created.");
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
            if (upgrades == null || upgrades.Count < 2) return;

            try {
                // Hijack Slot 1 (keeping original logic for now)
                if (VortexBladePlugin.BladeStacks < 5) upgrades[1] = VortexBladePlugin.BladeTemplate;
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
                if (list != null && list.Count >= 2) {
                    if (VortexBladePlugin.BladeTemplate != null) list[1] = VortexBladePlugin.BladeTemplate;
                }

                var msgFields = message.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var indexFi = msgFields.FirstOrDefault(f => f.Name.ToLower().Contains("index"));
                int selectedIndex = (indexFi != null) ? (int)indexFi.GetValue(message) : -1;
                var upgradeFi = msgFields.FirstOrDefault(f => f.Name.Contains("Upgrade") || f.FieldType.Name.Contains("SO"));

                if (selectedIndex == 1 || (upgradeFi != null && upgradeFi.GetValue(message) == VortexBladePlugin.BladeTemplate)) {
                    if (VortexBladePlugin.BladeStacks < 5) VortexBladePlugin.BladeStacks++;
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
            if (__instance.gameObject.GetComponent<VortexBladeBehavior>() == null)
                __instance.gameObject.AddComponent<VortexBladeBehavior>();
        }
    }

    public class VortexBladeBehavior : MonoBehaviour
    {
        private List<GameObject> _blades = new List<GameObject>();
        private float _orbitAngle = 0f;
        private float _damageTimer = 0f;

        void Update()
        {
            UpdatePlayerLevel();
            UpdateBlades(Mathf.Min(VortexBladePlugin.BladeStacks, 5));
        }

        void UpdatePlayerLevel()
        {
            try {
                var p = GetComponent<Player>();
                if (p != null) {
                    var pi = typeof(Player).GetProperty("Level", BindingFlags.Public | BindingFlags.Instance);
                    if (pi != null) VortexBladePlugin.PlayerLevel = (int)pi.GetValue(p);
                }
            } catch {}
        }

        void UpdateBlades(int targetCount)
        {
            while (_blades.Count < targetCount) {
                GameObject b = new GameObject("VortexBlade_" + _blades.Count);
                
                var lr = b.AddComponent<LineRenderer>();
                lr.startWidth = 0.25f; lr.endWidth = 0.05f; 
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = new Color(0, 1, 1, 0.8f); lr.endColor = new Color(0, 0.5f, 1f, 0.4f);
                lr.positionCount = 2;
                
                var ps = b.AddComponent<ParticleSystem>();
                var psr = b.GetComponent<ParticleSystemRenderer>();
                if (psr == null) psr = b.AddComponent<ParticleSystemRenderer>();
                
                Shader pShader = Shader.Find("Particles/Standard Unlit");
                if (pShader == null) pShader = Shader.Find("Sprites/Default");
                psr.material = new Material(pShader);

                try {
                    var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                    var circle = sprites.FirstOrDefault(s => s.name.ToLower().Contains("circle") || s.name.ToLower().Contains("glow") || s.name.ToLower() == "knob");
                    if (circle != null) psr.material.mainTexture = circle.texture;
                } catch {}

                var main = ps.main;
                main.startColor = new Color(0.2f, 1f, 1f, 0.4f); 
                main.startSize = 0.3f; 
                main.startSpeed = 0.5f; 
                main.startLifetime = 0.2f; 
                main.maxParticles = 500;
                main.simulationSpace = ParticleSystemSimulationSpace.World; 
                
                var em = ps.emission; 
                em.rateOverTime = 200f; 
                
                var sh = ps.shape; 
                sh.shapeType = ParticleSystemShapeType.Sphere; 
                sh.radius = 0.6f; 

                var col = ps.colorOverLifetime;
                col.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.cyan, 0.0f), new GradientColorKey(Color.white, 0.5f), new GradientColorKey(Color.blue, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.4f, 0.0f), new GradientAlphaKey(0.25f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) }
                );
                col.color = grad;
                
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                AnimationCurve curve = new AnimationCurve();
                curve.AddKey(0.0f, 0.5f);
                curve.AddKey(0.5f, 1.0f);
                curve.AddKey(1.0f, 0.0f);
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

                _blades.Add(b);
            }

            if (targetCount <= 0) return;

            _orbitAngle += Time.deltaTime * 150f;
            float pulse = 1f + (Mathf.Sin(Time.time * 15f) * 0.2f); 

            // Damage Tick Logic
            bool isDamageTick = false;
            _damageTimer -= Time.deltaTime;
            if (_damageTimer <= 0) {
                isDamageTick = true;
                _damageTimer = 0.25f * Mathf.Pow(0.85f, targetCount - 1);
            }

            for (int i = 0; i < _blades.Count; i++) {
                float angle = _orbitAngle + (i * (360f / _blades.Count));
                float rad = angle * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * 2.5f;
                _blades[i].transform.position = pos;
                
                var lr = _blades[i].GetComponent<LineRenderer>();
                Vector3 dir = (pos - transform.position).normalized;
                
                lr.SetPosition(0, pos - dir * 0.4f * pulse);
                lr.SetPosition(1, pos + dir * 0.8f * pulse); 
                lr.startWidth = 0.3f * pulse;
                
                float alphaPulse = 0.6f + (Mathf.Sin(Time.time * 20f) * 0.3f);
                lr.startColor = new Color(0, 1, 1, alphaPulse);
                lr.endColor = new Color(0, 0.5f, 1f, alphaPulse * 0.5f);

                if (isDamageTick) {
                    DamageNearbyEnemies(pos, 1.4f);
                }
            }
        }

        void DamageNearbyEnemies(Vector3 pos, float range)
        {
            foreach (var e in FindObjectsOfType<Enemy>()) {
                if (e != null && Vector3.Distance(e.transform.position, pos) <= range) {
                    var h = e.GetComponent<HealthComponent>();
                    if (h != null) {
                        bool died;
                        float damage = (10f + (VortexBladePlugin.BladeStacks * 5f)) * (1f + (VortexBladePlugin.PlayerLevel * 0.2f));
                        
                        Vector3 pushDir = Vector3.zero;
                        if (VortexBladePlugin.BladeStacks >= 3) {
                            pushDir = (e.transform.position - pos).normalized;
                        }

                        h.TakeDamage(pushDir, damage, out died, true, false, false);
                    }
                }
            }
        }
    }
}