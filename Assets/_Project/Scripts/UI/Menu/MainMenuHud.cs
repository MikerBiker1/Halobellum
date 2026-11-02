// MainMenuHud.cs
// The main menu's screen (M10), in the MainMenu scene over MenuBackdrop's picture: the title, New Match with the AI's difficulty next to it,
// Sandbox, Settings and Quit. It fades in from black and fades out before a scene change. Esc closes Settings. It scales to any window
// (HudScale.ApplyFit). The difficulty is the one the draft screen also shows: MatchController reads it from the same PlayerPrefs key.
// Depends on: GameFlow, GameSettings, MatchController (difficulty key), MenuStyle, SettingsScreen, Halobellum.Economy, Unity Input System.

using System;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.UI
{
    public sealed class MainMenuHud : MonoBehaviour
    {
        public const string Title = "HALOBELLUM";

        private const float FadeInSeconds = 1.6f, FadeOutSeconds = 0.35f;
        private const float ButtonWidth = 400f, ButtonHeight = 64f, ButtonGap = 6f;
        private static readonly string[] Difficulties = { "Easy", "Normal", "Hard" };

        private readonly SettingsScreen _settings = new SettingsScreen();
        private bool _showSettings;
        private float _shownAt;
        private Action _leaving;
        private float _leavingAt;

        /// <summary>True while the settings panel is showing (for the test tool).</summary>
        public bool ShowingSettings => _showSettings;

        private void Start() => _shownAt = Time.unscaledTime;

        private void Update()
        {
            if (_leaving != null && Time.unscaledTime - _leavingAt >= FadeOutSeconds)
            {
                Action leave = _leaving;
                _leaving = null;
                leave();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (_showSettings && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseSettings();
        }

        private void CloseSettings()
        {
            _settings.Close();
            _showSettings = false;
        }

        private void Leave(Action action)
        {
            if (_leaving != null) return;
            _leaving = action;
            _leavingAt = Time.unscaledTime;
        }

        private void OnGUI()
        {
            MenuStyle.Ensure();
            float scale = HudScale.ApplyFit(Mathf.Max(1120f, SettingsScreen.PanelWidth + 20f), SettingsScreen.PanelHeight + 20f);
            float width = HudScale.ScaledWidth(scale), height = HudScale.ScaledHeight(scale);

            if (_showSettings)
            {
                MenuStyle.Fill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, 0.35f));
                if (_settings.Draw(width, height)) _showSettings = false;
            }
            else
            {
                DrawMain(width, height);
            }
            DrawFade(width, height);
        }

        private void DrawMain(float width, float height)
        {
            // A soft darkness on the left, for the title and buttons to stand on; the picture stays clear on the right.
            MenuStyle.FadeRight(new Rect(0f, 0f, width * 0.68f, height), new Color(0.015f, 0.015f, 0.03f, 0.86f));

            float left = Mathf.Max(56f, width * 0.05f);
            float top = height * 0.13f;
            float titleWidth = MenuStyle.SpacedText(left, top, Title, MenuStyle.Title, 7f, Color.white);
            float underTitle = top + MenuStyle.Title.CalcSize(new GUIContent("H")).y + 6f;
            MenuStyle.FadeRight(new Rect(left, underTitle, titleWidth, 2f), MenuStyle.Accent);

            float y = Mathf.Max(underTitle + 40f, height * 0.42f);
            bool enabled = _leaving == null;
            GUI.enabled = enabled;

            var newMatch = new Rect(left, y, ButtonWidth, ButtonHeight);
            if (MenuStyle.MenuButton(newMatch, "New Match")) Leave(GameFlow.NewMatch);
            DrawDifficulty(new Rect(newMatch.xMax + 18f, y + 12f, 330f, ButtonHeight - 24f));
            y += ButtonHeight + ButtonGap;

            if (MenuStyle.MenuButton(new Rect(left, y, ButtonWidth, ButtonHeight), "Sandbox")) Leave(GameFlow.OpenSandbox);
            y += ButtonHeight + ButtonGap;

            if (MenuStyle.MenuButton(new Rect(left, y, ButtonWidth, ButtonHeight), "Settings"))
            {
                _settings.Open();
                _showSettings = true;
            }
            y += ButtonHeight + ButtonGap;

            if (MenuStyle.MenuButton(new Rect(left, y, ButtonWidth, ButtonHeight), "Quit")) Leave(GameFlow.QuitGame);
            GUI.enabled = true;
        }

        /// <summary>The AI's difficulty, next to New Match.</summary>
        private static void DrawDifficulty(Rect rect)
        {
            int current = Mathf.Clamp(PlayerPrefs.GetInt(MatchController.DifficultyKey, (int)AiDifficulty.Normal), 0, Difficulties.Length - 1);
            GUI.Label(new Rect(rect.x, rect.y - 24f, rect.width, 22f), "AI difficulty", MenuStyle.Small);
            int wanted = MenuStyle.Choice(rect, "Difficulty", Difficulties, current);
            if (wanted != current) PlayerPrefs.SetInt(MatchController.DifficultyKey, wanted);
        }

        private void DrawFade(float width, float height)
        {
            float alpha = 1f - Mathf.Clamp01((Time.unscaledTime - _shownAt) / FadeInSeconds);
            alpha *= alpha;
            if (_leaving != null) alpha = Mathf.Max(alpha, Mathf.Clamp01((Time.unscaledTime - _leavingAt) / FadeOutSeconds));
            if (alpha > 0f) MenuStyle.Fill(new Rect(0f, 0f, width, height), new Color(0f, 0f, 0f, alpha));
        }
    }
}
