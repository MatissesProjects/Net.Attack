using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetAttackModLoader
{
    [BepInPlugin("com.matissetec.modloader", "NetAttack Mod Loader", "3.1.0")]
    public class NetAttackModLoader : BaseUnityPlugin
    {
        public static NetAttackModLoader Instance;
        public string PluginsPath;
        public string DisabledPath;

        // PERFORMANCE FIX: Cache the lists so we don't read files during menu transitions
        public List<string> CachedActive = new List<string>();
        public List<string> CachedDisabled = new List<string>();

        void Awake()
        {
            Instance = this;
            PluginsPath = Paths.PluginPath;
            DisabledPath = Path.Combine(Paths.BepInExRootPath, "plugins_disabled");
            if (!Directory.Exists(DisabledPath)) Directory.CreateDirectory(DisabledPath);

            // Load files ONCE at startup (prevents lag later)
            RefreshCache();

            Harmony.CreateAndPatchAll(typeof(DesktopNativePatch));
            Logger.LogInfo("Mod Loader Ready (Performance Mode).");
        }

        public void RefreshCache()
        {
            CachedActive = Directory.GetFiles(PluginsPath, "*.dll")
                .Where(f => !Path.GetFileName(f).Contains("NetAttackModLoader"))
                .ToList();
            
            CachedDisabled = Directory.GetFiles(DisabledPath, "*.dll").ToList();
        }

        public bool ToggleModFile(string fileName)
        {
            string activePath = Path.Combine(PluginsPath, fileName);
            string disabledPath = Path.Combine(DisabledPath, fileName);

            try
            {
                if (File.Exists(activePath))
                {
                    File.Move(activePath, disabledPath);
                    // Update cache manually so we don't need to re-scan
                    CachedActive.RemoveAll(x => Path.GetFileName(x) == fileName);
                    CachedDisabled.Add(disabledPath);
                    return false; 
                }
                else if (File.Exists(disabledPath))
                {
                    File.Move(disabledPath, activePath);
                    CachedDisabled.RemoveAll(x => Path.GetFileName(x) == fileName);
                    CachedActive.Add(activePath);
                    return true;
                }
            }
            catch (Exception e) { Logger.LogError($"Toggle failed: {e.Message}"); }
            return false;
        }
    }

    // --- TEXT ENFORCER (Prevents "Tutorial" overwrite) ---
    public class TextEnforcer : MonoBehaviour
    {
        public string TargetText;
        private TMPro.TextMeshProUGUI _tmp;
        private UnityEngine.UI.Text _txt;

        void Start()
        {
            _tmp = GetComponentInChildren<TMPro.TextMeshProUGUI>();
            _txt = GetComponentInChildren<UnityEngine.UI.Text>();
            UpdateNow(TargetText);
        }

        void LateUpdate()
        {
            if (_tmp != null && _tmp.text != TargetText) _tmp.text = TargetText;
            if (_txt != null && _txt.text != TargetText) _txt.text = TargetText;
        }
        
        public void UpdateNow(string text)
        {
            TargetText = text;
            if (_tmp != null) _tmp.text = text;
            if (_txt != null) _txt.text = text;
        }
    }

    [HarmonyPatch(typeof(BRG.UI.Desktop), "Initialize")] 
    public static class DesktopNativePatch
    {
        static GameObject _mainMenuContainer;
        static GameObject _modsMenuContainer;
        static MonoBehaviour _buttonTemplate; 

        [HarmonyPostfix]
        static void Postfix(BRG.UI.Desktop __instance)
        {
            var tutorialBtn = AccessTools.Field(typeof(BRG.UI.Desktop), "_tutorialButton").GetValue(__instance) as MonoBehaviour;
            if (tutorialBtn == null) return;

            // LAG GUARD: Check if we already injected in this scene to prevent double-work
            // We search the container for our specific button name
            if (tutorialBtn.transform.parent.Find("Btn_OpenMods") != null) return;

            _buttonTemplate = tutorialBtn;
            _mainMenuContainer = tutorialBtn.transform.parent.gameObject;

            CreateModsButton(tutorialBtn);
            CreateModsScreen();
        }

        static void CreateModsButton(MonoBehaviour template)
        {
            GameObject btn = UnityEngine.Object.Instantiate(template.gameObject, _mainMenuContainer.transform);
            btn.name = "Btn_OpenMods";
            
            // STYLE FIX: Move to Bottom
            btn.transform.SetAsLastSibling(); 

            SetButtonText(btn, "MODS");
            SetButtonAction(btn, () => {
                _mainMenuContainer.SetActive(false);
                _modsMenuContainer.SetActive(true);
            });
        }

        static void CreateModsScreen()
        {
            _modsMenuContainer = UnityEngine.Object.Instantiate(_mainMenuContainer, _mainMenuContainer.transform.parent);
            _modsMenuContainer.name = "Container_Mods";
            _modsMenuContainer.SetActive(false);
            
            foreach (Transform child in _modsMenuContainer.transform) {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            PopulateModList();
        }

        static void PopulateModList()
        {
            // HEADER
            var header = CloneButton("Header", "--- MOD MANAGER ---");
            SetButtonColor(header.gameObject, Color.yellow);
            RemoveClick(header.gameObject);

            // PERFORMANCE FIX: Use Cached Lists (RAM) instead of Directory.GetFiles (Disk)
            
            // 1. ACTIVE
            foreach (var file in NetAttackModLoader.Instance.CachedActive)
            {
                CreateModToggle(Path.GetFileName(file), true);
            }

            // 2. DISABLED
            foreach (var file in NetAttackModLoader.Instance.CachedDisabled)
            {
                CreateModToggle(Path.GetFileName(file), false);
            }

            // BACK BUTTON
            var backBtn = CloneButton("Back", "<< BACK");
            SetButtonColor(backBtn.gameObject, Color.white);
            
            // Ensure Back button is always at the bottom too
            backBtn.transform.SetAsLastSibling();
            
            SetButtonAction(backBtn.gameObject, () => {
                _modsMenuContainer.SetActive(false);
                _mainMenuContainer.SetActive(true);
            });
        }

        static void CreateModToggle(string fileName, bool startEnabled)
        {
            string statusText = startEnabled ? "[ON]" : "[OFF]";
            var btnComp = CloneButton(fileName, $"{statusText} {fileName}");
            GameObject btnObj = btnComp.gameObject;

            SetButtonColor(btnObj, startEnabled ? Color.cyan : new Color(1f, 0.4f, 0.4f));

            SetButtonAction(btnObj, () => {
                bool isNowEnabled = NetAttackModLoader.Instance.ToggleModFile(fileName);
                
                // Visual Update Only (No List Rebuild)
                string newStatus = isNowEnabled ? "[ON]" : "[OFF]";
                Color newColor = isNowEnabled ? Color.cyan : new Color(1f, 0.4f, 0.4f);
                
                SetButtonColor(btnObj, newColor);
                
                var enforcer = btnObj.GetComponent<TextEnforcer>();
                if (enforcer != null) enforcer.UpdateNow($"{newStatus} {fileName} (RESTART)");
            });
        }

        // --- HELPERS ---

        static MonoBehaviour CloneButton(string name, string text)
        {
            GameObject newObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, _modsMenuContainer.transform);
            newObj.name = "Btn_" + name;
            SetButtonText(newObj, text);
            Type btnType = AccessTools.TypeByName("BRG.UI.BRG_Button") ?? AccessTools.TypeByName("BRG_Button");
            return newObj.GetComponent(btnType) as MonoBehaviour;
        }

        static void SetButtonText(GameObject btnObj, string text)
        {
            var enforcer = btnObj.GetComponent<TextEnforcer>();
            if (enforcer == null) enforcer = btnObj.AddComponent<TextEnforcer>();
            enforcer.UpdateNow(text);
        }

        static void RemoveClick(GameObject btnObj)
        {
            var btnComp = btnObj.GetComponent("BRG_Button") as MonoBehaviour;
            if (btnComp) UnityEngine.Object.Destroy(btnComp);
        }

        static void SetButtonColor(GameObject btnObj, Color c)
        {
            var tmps = btnObj.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var t in tmps) t.color = c;
            var txts = btnObj.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (var t in txts) t.color = c;
        }

        static void SetButtonAction(GameObject btnObj, Action action)
        {
            Type btnType = AccessTools.TypeByName("BRG.UI.BRG_Button") ?? AccessTools.TypeByName("BRG_Button");
            if (btnType == null) return;
            var comp = btnObj.GetComponent(btnType);
            if (comp == null) return;
            var setMethod = AccessTools.Method(btnType, "SetCallback", new Type[] { typeof(Action) });
            if (action == null) action = () => { }; 
            setMethod?.Invoke(comp, new object[] { action });
        }
    }
}