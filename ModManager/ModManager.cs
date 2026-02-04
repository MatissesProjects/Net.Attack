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
            
            foreach (Transform child in _modsMenuContainer.transform) {
                UnityEngine.Object.Destroy(child.gameObject);
            }

            PopulateModList();
        }

        static void PopulateModList()
        {
            // 1. TOP MARGIN (Spacer)
            CreateSpacer(30);

            // 2. HEADER
            var header = CloneButton("Header", "--- MOD MANAGER ---");
            SetButtonColor(header.gameObject, Color.yellow);
            RemoveClick(header.gameObject);
            SetButtonIcon(header.gameObject, IconMode.Hidden); // Hide Icon
            
            // 3. HEADER GAP (Spacer)
            CreateSpacer(20);

            // 4. MOD LIST
            foreach (var file in NetAttackModLoader.Instance.CachedActive)
            {
                CreateModToggle(Path.GetFileName(file), true);
            }
            foreach (var file in NetAttackModLoader.Instance.CachedDisabled)
            {
                CreateModToggle(Path.GetFileName(file), false);
            }

            // 5. BOTTOM MARGIN (Spacer)
            CreateSpacer(40);

            // 6. BACK BUTTON
            var backBtn = CloneButton("Back", "<< BACK");
            SetButtonColor(backBtn.gameObject, Color.white);
            SetButtonIcon(backBtn.gameObject, IconMode.Rotated90CCW); // Back Arrow (Rotated)
            
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

            UpdateToggleVisuals(btnObj, startEnabled);

            SetButtonAction(btnObj, () => {
                bool isNowEnabled = NetAttackModLoader.Instance.ToggleModFile(fileName);
                UpdateToggleVisuals(btnObj, isNowEnabled);
                
                var enforcer = btnObj.GetComponent<TextEnforcer>();
                string newStatus = isNowEnabled ? "[ON]" : "[OFF]";
                if (enforcer != null) enforcer.UpdateNow($"{newStatus} {fileName} (RESTART)");
            });
        }

        static void UpdateToggleVisuals(GameObject btn, bool enabled)
        {
            SetButtonColor(btn, enabled ? Color.cyan : new Color(1f, 0.4f, 0.4f));
            SetButtonIcon(btn, enabled ? IconMode.Normal : IconMode.Flipped); // If NOT enabled, flip upside down
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
                // If it anchors to all corners (0,0 to 1,1), it's likely a background.
                if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one) continue;

                // HEURISTIC 2: Ignore Wide Elements (Bars, Underlines)
                // If it spans more than 80% of the width, it's likely a decoration, not an icon.
                if ((rt.anchorMax.x - rt.anchorMin.x) > 0.8f) continue;

                if (mode == IconMode.Hidden)
                {
                    img.enabled = false;
                }
                else
                {
                    img.enabled = true;

                    // 1. Capture Original Position (So we don't drift)
                    var posTracker = img.GetComponent<IconOriginalPos>();
                    if (posTracker == null) posTracker = img.gameObject.AddComponent<IconOriginalPos>();
                    posTracker.CaptureIfNeeded(img.rectTransform);

                    // 2. Rotate
                    float zRot = 0;
                    if (mode == IconMode.Flipped) zRot = 180;
                    else if (mode == IconMode.Rotated90CCW) zRot = 90;

                    img.transform.localEulerAngles = new Vector3(0, 0, zRot);

                    // 3. Adjust Position (Move up by height if flipped)
                    if (mode == IconMode.Flipped)
                    {
                        // Shift up by full height to counteract the flip relative to pivot
                        float height = img.rectTransform.rect.height;
                        img.rectTransform.anchoredPosition = posTracker.OriginalAnchoredPosition + new Vector2(0, height);
                    }
                    else if (mode == IconMode.Rotated90CCW)
                    {
                        // Shift right by half height (to center the new width)
                        // Shift up by half width (to bring bottom to baseline)
                        float h = img.rectTransform.rect.height;
                        float w = img.rectTransform.rect.width;
                        img.rectTransform.anchoredPosition = posTracker.OriginalAnchoredPosition + new Vector2(h * 0.5f, w * 0.5f);
                    }
                    else
                    {
                        // Restore original
                        img.rectTransform.anchoredPosition = posTracker.OriginalAnchoredPosition;
                    }
                }
            }
        }

        static void CreateSpacer(float height)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(_modsMenuContainer.transform, false);
            var le = spacer.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

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