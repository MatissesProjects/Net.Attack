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

        void Awake()
        {
            Instance = this;
            PluginsPath = Paths.PluginPath;
            DisabledPath = Path.Combine(Paths.BepInExRootPath, "plugins_disabled");
            if (!Directory.Exists(DisabledPath)) Directory.CreateDirectory(DisabledPath);

            RefreshCache();
            Harmony.CreateAndPatchAll(typeof(DesktopNativePatch));
            Logger.LogInfo("Mod Loader Ready (Icon Alignment Fix).");
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

    // --- HELPERS ---
    
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

    // NEW: Remembers the original position so we don't lose it when moving icons
    public class IconOriginalPos : MonoBehaviour 
    {
        public Vector2 OriginalAnchoredPosition;
        public bool Captured = false;

        public void CaptureIfNeeded(RectTransform rt)
        {
            if (!Captured)
            {
                OriginalAnchoredPosition = rt.anchoredPosition;
                Captured = true;
            }
        }
    }

    [HarmonyPatch(typeof(BRG.UI.Desktop), "Initialize")] 
    public static class DesktopNativePatch
    {
        enum IconMode { Normal, Flipped, Hidden, Rotated90CCW }

        static GameObject _mainMenuContainer;
        static GameObject _modsMenuContainer;
        static MonoBehaviour _buttonTemplate; 
        static Type _cachedBtnType;

        static Type GetButtonType()
        {
            if (_cachedBtnType != null) return _cachedBtnType;
            _cachedBtnType = AccessTools.TypeByName("BRG.UI.Primitives.BRG_Button") ?? 
                             AccessTools.TypeByName("BRG.UI.BRG_Button") ?? 
                             AccessTools.TypeByName("BRG_Button");
            return _cachedBtnType;
        }

        [HarmonyPostfix]
        static void Postfix(BRG.UI.Desktop __instance)
        {
            var tutorialBtn = AccessTools.Field(typeof(BRG.UI.Desktop), "_tutorialButton").GetValue(__instance) as MonoBehaviour;
            if (tutorialBtn == null) return;
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
            btn.transform.SetAsLastSibling(); 

            SetButtonText(btn, "MODS");
            SetButtonIcon(btn, IconMode.Normal); // Normal Thumbs Up

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
            
            // 1. DISABLE INHERITED LAYOUTS (Crucial to prevent overlapping children)
            var vlgInherited = _modsMenuContainer.GetComponent<VerticalLayoutGroup>();
            if (vlgInherited != null) vlgInherited.enabled = false;
            var hlgInherited = _modsMenuContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlgInherited != null) hlgInherited.enabled = false;
            var glgInherited = _modsMenuContainer.GetComponent<GridLayoutGroup>();
            if (glgInherited != null) glgInherited.enabled = false;
            var csfInherited = _modsMenuContainer.GetComponent<ContentSizeFitter>();
            if (csfInherited != null) csfInherited.enabled = false;

            // 2. FORCE FULL SCREEN
            RectTransform containerRT = _modsMenuContainer.GetComponent<RectTransform>();
            if (containerRT != null) {
                containerRT.anchorMin = Vector2.zero; containerRT.anchorMax = Vector2.one;
                containerRT.sizeDelta = Vector2.zero;
                containerRT.anchoredPosition = Vector2.zero;
            }

            foreach (Transform child in _modsMenuContainer.transform) {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            // 3. BACKGROUND (Make it look like a real window)
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(_modsMenuContainer.transform, false);
            RectTransform bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f); // Deep dark blue

            // --- CREATE SCROLL VIEW ---
            GameObject scrollView = new GameObject("Scroll View");
            scrollView.transform.SetParent(_modsMenuContainer.transform, false);
            RectTransform svRect = scrollView.AddComponent<RectTransform>();
            svRect.anchorMin = new Vector2(0.05f, 0.2f); // 5% margin, room for Back button
            svRect.anchorMax = new Vector2(0.95f, 0.95f);
            svRect.sizeDelta = Vector2.zero;
            
            var svImage = scrollView.AddComponent<Image>();
            svImage.color = new Color(0, 0, 0, 0.5f);

            var scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 45f;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            RectTransform vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            vpRect.pivot = new Vector2(0, 1);
            viewport.AddComponent<RectMask2D>();
            var vpImage = viewport.AddComponent<Image>();
            vpImage.color = new Color(0,0,0,0);
            scrollRect.viewport = vpRect;

            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1); contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 300);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 15;
            vlg.padding = new RectOffset(30, 30, 30, 30);

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRect;

            PopulateModList(content.transform);
        }

        static void PopulateModList(Transform container)
        {
            // HEADER
            var header = CloneButton("Header", "--- MOD MANAGER ---", container);
            SetButtonColor(header.gameObject, Color.yellow);
            RemoveClick(header.gameObject);
            SetButtonIcon(header.gameObject, IconMode.Hidden);
            AddLayoutElement(header.gameObject, 80);
            
            CreateSpacer(20, container);

            // MOD LIST - Sorted Alphabetically
            var activeSorted = NetAttackModLoader.Instance.CachedActive.OrderBy(f => Path.GetFileName(f)).ToList();
            var disabledSorted = NetAttackModLoader.Instance.CachedDisabled.OrderBy(f => Path.GetFileName(f)).ToList();

            foreach (var file in activeSorted)
            {
                CreateModToggle(Path.GetFileName(file), true, container);
            }
            foreach (var file in disabledSorted)
            {
                CreateModToggle(Path.GetFileName(file), false, container);
            }

            // BACK BUTTON (Outside scroll area)
            var backBtn = CloneButton("Back", "<< BACK", _modsMenuContainer.transform);
            SetButtonIcon(backBtn.gameObject, IconMode.Hidden); // HIDE GIANT ICON
            
            RectTransform rt = backBtn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.05f); // Center bottom
            rt.anchorMax = new Vector2(0.5f, 0.05f);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(300, 80);

            SetButtonColor(backBtn.gameObject, Color.white);
            
            SetButtonAction(backBtn.gameObject, () => {
                _modsMenuContainer.SetActive(false);
                _mainMenuContainer.SetActive(true);
            });
        }

        static void CreateModToggle(string fileName, bool startEnabled, Transform container)
        {
            string statusText = startEnabled ? "[ON]" : "[OFF]";
            var btnComp = CloneButton(fileName, $"{statusText} {fileName}", container);
            GameObject btnObj = btnComp.gameObject;

            AddLayoutElement(btnObj, 50); // Important for VerticalLayoutGroup
            UpdateToggleVisuals(btnObj, startEnabled);
            HideAllIcons(btnObj); // NEW: Extra aggressive wipe

            SetButtonAction(btnObj, () => {
                bool isNowEnabled = NetAttackModLoader.Instance.ToggleModFile(fileName);
                UpdateToggleVisuals(btnObj, isNowEnabled);
                HideAllIcons(btnObj); // Re-wipe after any state change
                
                var enforcer = btnObj.GetComponent<TextEnforcer>();
                string newStatus = isNowEnabled ? "[ON]" : "[OFF]";
                if (enforcer != null) enforcer.UpdateNow($"{newStatus} {fileName} (RESTART)");
            });
        }

        static void HideAllIcons(GameObject btn)
        {
            // 1. Wipe internal references via reflection
            Type btnType = GetButtonType();
            if (btnType != null) {
                var comp = btn.GetComponent(btnType);
                if (comp != null) {
                    var fields = btnType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    foreach(var f in fields) {
                        if (f.Name.ToLower().Contains("icon")) {
                            try {
                                var val = f.GetValue(comp) as MonoBehaviour;
                                if (val != null) {
                                    val.gameObject.SetActive(false);
                                    var rt = val.GetComponent<RectTransform>();
                                    if (rt != null) rt.anchoredPosition = new Vector2(10000, 10000); // MOVE OFF SCREEN
                                }
                                f.SetValue(comp, null); 
                            } catch {}
                        }
                    }
                }
            }

            // 2. Hide only Image components, but keep text and backgrounds
            var images = btn.GetComponentsInChildren<Image>(true);
            foreach (var img in images) {
                if (img.gameObject == btn) continue;
                
                // Keep backgrounds
                string n = img.name.ToLower();
                if (n.Contains("bg") || n.Contains("background") || n.Contains("frame") || n.Contains("highlight") || n.Contains("glow")) continue;
                
                // Safety: NEVER hide an object that has a text component on it
                if (img.GetComponent<TMPro.TMP_Text>() != null || img.GetComponent<Text>() != null) continue;

                img.gameObject.SetActive(false);
                img.rectTransform.anchoredPosition = new Vector2(10000, 10000); // MOVE OFF SCREEN
            }
        }

        static void AddLayoutElement(GameObject obj, float minHeight)
        {
            var le = obj.GetComponent<LayoutElement>();
            if (le == null) le = obj.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            le.preferredHeight = minHeight;
        }

        static void UpdateToggleVisuals(GameObject btn, bool enabled)
        {
            SetButtonColor(btn, enabled ? Color.cyan : new Color(1f, 0.4f, 0.4f));
            SetButtonIcon(btn, IconMode.Hidden);
        }

        // --- IMPROVED ICON LOGIC ---
        static void SetButtonIcon(GameObject btn, IconMode mode)
        {
            var images = btn.GetComponentsInChildren<Image>(true);
            
            foreach (var img in images)
            {
                if (img.gameObject == btn) continue; 
                
                // IGNORE HIGHLIGHTS / DECORATIONS / BACKGROUNDS
                string name = img.name.ToLower();
                if (name.Contains("highlight") || name.Contains("glow") || name.Contains("hover") || name.Contains("bg") || name.Contains("background")) continue;

                RectTransform rt = img.rectTransform;

                // HEURISTIC 1: Ignore Full-Stretch Backgrounds
                if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one) continue;

                // HEURISTIC 2: Ignore Wide Elements
                if ((rt.anchorMax.x - rt.anchorMin.x) > 0.8f) continue;

                if (mode == IconMode.Hidden)
                {
                    img.gameObject.SetActive(false);
                    img.rectTransform.anchoredPosition = new Vector2(10000, 10000); // MOVE OFF SCREEN
                }
                else
                {
                    img.gameObject.SetActive(true);
                    img.enabled = true;

                    // 1. Capture Original Position
                    var posTracker = img.GetComponent<IconOriginalPos>();
                    if (posTracker == null) posTracker = img.gameObject.AddComponent<IconOriginalPos>();
                    posTracker.CaptureIfNeeded(img.rectTransform);

                    // 2. Rotate
                    float zRot = 0;
                    if (mode == IconMode.Flipped) zRot = 180;
                    else if (mode == IconMode.Rotated90CCW) zRot = 90;

                    img.transform.localEulerAngles = new Vector3(0, 0, zRot);

                    // 3. Adjust Position
                    if (mode == IconMode.Flipped)
                    {
                        float height = img.rectTransform.rect.height;
                        img.rectTransform.anchoredPosition = posTracker.OriginalAnchoredPosition + new Vector2(0, height);
                    }
                    else if (mode == IconMode.Rotated90CCW)
                    {
                        float h = img.rectTransform.rect.height;
                        float w = img.rectTransform.rect.width;
                        img.rectTransform.anchoredPosition = posTracker.OriginalAnchoredPosition + new Vector2(h * 0.5f, w * 0.5f);
                    }
                    else
                    {
                        img.rectTransform.anchoredPosition = posTracker.OriginalAnchoredPosition;
                    }
                }
            }
        }

        static void CreateSpacer(float height, Transform container)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(container, false);
            var le = spacer.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        static MonoBehaviour CloneButton(string name, string text, Transform parent)
        {
            GameObject newObj = UnityEngine.Object.Instantiate(_buttonTemplate.gameObject, parent);
            newObj.name = "Btn_" + name;
            SetButtonText(newObj, text);
            Type btnType = GetButtonType();
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
            Type btnType = GetButtonType();
            if (btnType == null) return;
            var btnComp = btnObj.GetComponent(btnType) as MonoBehaviour;
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
            Type btnType = GetButtonType();
            if (btnType == null) return;
            var comp = btnObj.GetComponent(btnType);
            if (comp == null) return;
            var setMethod = AccessTools.Method(btnType, "SetCallback", new Type[] { typeof(Action) });
            if (action == null) action = () => { }; 
            setMethod?.Invoke(comp, new object[] { action });
        }
    }
}