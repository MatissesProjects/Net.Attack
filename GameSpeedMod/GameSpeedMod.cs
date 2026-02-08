using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using NetAttackModUtils;
using BRG.Gameplay.Units;

namespace GameSpeedMod
{
    [BepInPlugin("com.matissetec.gamespeed", "Game Speed Mod", "1.3.0")]
    public class GameSpeedPlugin : BaseUnityPlugin
    {
        public static GameSpeedPlugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        public static float TargetTimeScale = 1f;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            
            Harmony harmony = new Harmony("com.matissetec.gamespeed");
            harmony.PatchAll(typeof(GameSpeedPlugin));
            
            Log.LogInfo("Game Speed Mod v1.3.0 Loaded! Attaching to Player...");
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        [HarmonyPostfix]
        static void PlayerAwake_Patch(Player __instance)
        {
            if (__instance.gameObject.GetComponent<GameSpeedBehavior>() == null)
            {
                __instance.gameObject.AddComponent<GameSpeedBehavior>();
                Log.LogInfo("GameSpeedBehavior attached to Player.");
            }
        }
    }

    public class GameSpeedBehavior : MonoBehaviour
    {
        private bool _isCoding = false;
        private float _codingTimer = 0f;
        private bool _hasLoggedGUI = false;
        private float _displayTimer = 0f;

        void Update()
        {
            // Handle Input - Using Alpha keys and Numpad
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) {
                GameSpeedPlugin.TargetTimeScale = 1f;
                _displayTimer = 3.0f;
                GameSpeedPlugin.Log.LogInfo("GameSpeed: Target set to 1x");
            }
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) {
                GameSpeedPlugin.TargetTimeScale = 2f;
                _displayTimer = 3.0f;
                GameSpeedPlugin.Log.LogInfo("GameSpeed: Target set to 2x");
            }
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) {
                GameSpeedPlugin.TargetTimeScale = 3f;
                _displayTimer = 3.0f;
                GameSpeedPlugin.Log.LogInfo("GameSpeed: Target set to 3x");
            }

            // Countdown display timer
            if (_displayTimer > 0) _displayTimer -= Time.unscaledDeltaTime;

            // Update coding state (ModUtils helper)
            _isCoding = ModUtils.IsCodingScreenActive(_isCoding, ref _codingTimer);

            // Apply TimeScale only if not paused
            if (Time.timeScale > 0f)
            {
                if (Mathf.Abs(Time.timeScale - GameSpeedPlugin.TargetTimeScale) > 0.01f)
                {
                    Time.timeScale = GameSpeedPlugin.TargetTimeScale;
                }
            }
        }

        void OnGUI()
        {
            if (_displayTimer <= 0) return;

            if (!_hasLoggedGUI) {
                GameSpeedPlugin.Log.LogInfo("GameSpeedBehavior: OnGUI first run.");
                _hasLoggedGUI = true;
            }

            // Always visible for debugging as requested
            float width = 160f;
            float height = 50f;
            float x = Screen.width - width - 40f;
            float y = 90f;

            GUI.depth = -100;

            // Background shadow/glow
            GUI.color = new Color(0, 0.8f, 1f, 0.3f);
            GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), Texture2D.whiteTexture);

            // Main Background
            GUI.color = new Color(0, 0, 0, 0.9f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            // Top accent bar
            GUI.color = new Color(0, 1f, 1f, 1f);
            GUI.DrawTexture(new Rect(x, y, width, 3), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 16;
            style.normal.textColor = new Color(0, 1f, 1f, 1f);

            string text = $">> SPEED: {GameSpeedPlugin.TargetTimeScale:0.0}X <<";
            GUI.Label(new Rect(x, y, width, height), text, style);
            
            style.fontSize = 10;
            style.alignment = TextAnchor.LowerRight;
            style.normal.textColor = new Color(0, 1f, 1f, 0.6f);
            GUI.Label(new Rect(x, y, width - 10, height - 5), "DRIVE_SPEED_ACTIVE", style);
        }
    }
}