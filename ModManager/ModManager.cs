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
    [BepInPlugin("com.matissetec.modloader", "NetAttack Mod Loader", "3.3.1")]
    public class NetAttackModLoader : BaseUnityPlugin
    {
        public static NetAttackModLoader Instance;
        public string PluginsPath;
        public string DisabledPath;

        public List<string> CachedActive = new List<string>();
        public List<string> CachedDisabled = new List<string>();

        private readonly string[] protectedMods = { "ModManager.dll", "NetAttackModUtils.dll" };

        void Awake()
        {
            Instance = this;
            PluginsPath = Paths.PluginPath;
            DisabledPath = Path.Combine(Paths.BepInExRootPath, "plugins_disabled");
            if (!Directory.Exists(DisabledPath)) Directory.CreateDirectory(DisabledPath);

            RefreshCache();
            Harmony.CreateAndPatchAll(typeof(DesktopNativePatch));
            Logger.LogInfo("Mod Loader Restored to Stable Version.");
        }

        public void RefreshCache()
        {
            CachedActive = Directory.GetFiles(PluginsPath, "*.dll")
                .Where(f => !protectedMods.Contains(Path.GetFileName(f)))
                .ToList();
            CachedDisabled = Directory.GetFiles(DisabledPath, "*.dll")
                .Where(f => !protectedMods.Contains(Path.GetFileName(f)))
                .ToList();
        }

        public bool ToggleModFile(string fullPath)
        {
            try {
                string fileName = Path.GetFileName(fullPath);
                string activePath = Path.Combine(PluginsPath, fileName);
                string disabledPath = Path.Combine(DisabledPath, fileName);
                string targetPath = fullPath.Contains(DisabledPath) ? activePath : disabledPath;

                if (File.Exists(fullPath)) {
                    File.Move(fullPath, targetPath);
                    RefreshCache();
                    return true;
                }
            } catch {}
            return false;
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
            try {
                var tutorialBtn = AccessTools.Field(typeof(BRG.UI.Desktop), "_tutorialButton").GetValue(__instance) as MonoBehaviour;
                if (tutorialBtn == null) return;
                if (tutorialBtn.transform.parent.Find("Btn_OpenMods") != null) return;

                _buttonTemplate = tutorialBtn;
                _mainMenuContainer = tutorialBtn.transform.parent.gameObject;

                // Create MODS button
                GameObject btn = UnityEngine.Object.Instantiate(tutorialBtn.gameObject, _mainMenuContainer.transform);
                btn.name = "Btn_OpenMods";
                SetButtonText(btn, "MODS");
                SetButtonAction(btn, () => {
                    _mainMenuContainer.SetActive(false);
                    OpenModsScreen();
                });
            } catch {}
        }

        static void OpenModsScreen()
        {
            if (_modsMenuContainer == null) {
                _modsMenuContainer = new GameObject("ModManagerPanel");
                _modsMenuContainer.transform.SetParent(_mainMenuContainer.transform.parent, false);
                var rt = _modsMenuContainer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                // Alpha adjusted to see game background
                _modsMenuContainer.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
            }
            
            _modsMenuContainer.SetActive(true);
            foreach (Transform child in _modsMenuContainer.transform) UnityEngine.Object.Destroy(child.gameObject);

            // 1. Title
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_modsMenuContainer.transform, false);
            var titleRT = titleObj.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 0.95f); titleRT.anchorMax = new Vector2(0.5f, 0.95f);
            titleRT.pivot = new Vector2(0.5f, 1f); titleRT.sizeDelta = new Vector2(600, 50);
            var titleText = titleObj.AddComponent<TMPro.TextMeshProUGUI>();
            titleText.text = "--- MOD MANAGER ---";
            titleText.alignment = TMPro.TextAlignmentOptions.Center;
            titleText.color = Color.yellow;
            titleText.fontSize = 32;

            // 2. Scroll View Structure
            GameObject sv = new GameObject("ScrollView");
            sv.transform.SetParent(_modsMenuContainer.transform, false);
            var svRT = sv.AddComponent<RectTransform>();
            svRT.anchorMin = new Vector2(0.1f, 0.25f); svRT.anchorMax = new Vector2(0.9f, 0.9f);
            svRT.sizeDelta = Vector2.zero;
            
            var svImg = sv.AddComponent<Image>();
            svImg.color = new Color(1, 1, 1, 0.05f);
            svImg.raycastTarget = true; 

            var scrollRect = sv.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(sv.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0,0,0,0);
            vpImg.raycastTarget = true;
            scrollRect.viewport = vpRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 100);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            vlg.spacing = 10; vlg.padding = new RectOffset(20, 20, 20, 20);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;

            // Populate Mod List
            var allFiles = NetAttackModLoader.Instance.CachedActive.Concat(NetAttackModLoader.Instance.CachedDisabled)
                .OrderBy(f => Path.GetFileName(f)).ToList();

            foreach (var file in allFiles) {
                bool isActive = NetAttackModLoader.Instance.CachedActive.Contains(file);
                string fileName = Path.GetFileName(file);
                GameObject row = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, content.transform);
                SetButtonText(row, $"{(isActive ? "[ON]" : "[OFF]")} {fileName}");
                
                var le = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
                le.minHeight = 60; le.preferredHeight = 60;

                SetButtonAction(row, () => {
                    NetAttackModLoader.Instance.ToggleModFile(file);
                    OpenModsScreen(); 
                });
                
                HideAllIcons(row);
            }

            // Back button
            GameObject back = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, _modsMenuContainer.transform);
            var backRT = back.GetComponent<RectTransform>();
            backRT.anchorMin = new Vector2(0.5f, 0.1f); backRT.anchorMax = new Vector2(0.5f, 0.1f);
            backRT.pivot = new Vector2(0.5f, 0.5f); backRT.anchoredPosition = Vector2.zero;
            backRT.sizeDelta = new Vector2(300, 80);
            
            // Fix icon for the BACK button
            foreach (var img in back.GetComponentsInChildren<Image>(true)) {
                if (img.gameObject == back) continue;
                string n = img.name.ToLower();
                if (n.Contains("bg")) continue;
                
                if (n.Contains("thumb") || n.Contains("icon")) {
                    img.enabled = true;
                    img.gameObject.SetActive(true);
                    img.rectTransform.anchoredPosition = new Vector2(-100, 0); 
                    img.transform.localEulerAngles = new Vector3(0, 0, 90); 
                } else {
                    // Hide everything else (giant orange bars, highlights)
                    img.rectTransform.anchoredPosition = new Vector2(10000, 10000);
                    img.enabled = false;
                }
            }

            SetButtonText(back, "BACK");
            SetButtonAction(back, () => {
                _modsMenuContainer.SetActive(false);
                _mainMenuContainer.SetActive(true);
            });
        }

        static void HideAllIcons(GameObject btn) {
            foreach (var img in btn.GetComponentsInChildren<Image>(true)) {
                if (img.gameObject != btn) {
                    string n = img.name.ToLower();
                    if (!n.Contains("bg") && !n.Contains("background")) {
                        img.rectTransform.anchoredPosition = new Vector2(10000, 10000);
                        img.enabled = false;
                    }
                }
            }
        }

        static void SetButtonText(GameObject obj, string text) {
            var tmp = obj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
            var txt = obj.GetComponentInChildren<Text>();
            if (txt != null) txt.text = text;
        }

        static void SetButtonAction(GameObject obj, Action action) {
            Type t = _buttonTemplate.GetType();
            var comp = obj.GetComponent(t);
            AccessTools.Method(t, "SetCallback", new Type[] { typeof(Action) })?.Invoke(comp, new object[] { action });
        }
    }
}