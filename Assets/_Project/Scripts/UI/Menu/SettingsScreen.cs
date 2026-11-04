// SettingsScreen.cs
// The settings panel (M10), one and the same from the main menu and from the pause menu: four volumes, the screen, window or fullscreen, resolution,
// quality, VSync, UI scale, camera shake and the frame-rate counter. The Screen row lists the PC's screens by name and size and marks the main one (GameDisplays moves the window); with one screen it just says so. Each change is applied and written to PlayerPrefs at once (GameSettings); the file on disk
// is saved when the panel closes. The owner of the panel calls Open when it opens, and Draw in OnGUI until Draw says it is done.
// Depends on: GameDisplays, GameSettings, MenuStyle, Halobellum.Core (DisplayChoice), UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public sealed class SettingsScreen
    {
        /// <summary>The virtual size of the panel. Menus fit their scale to this.</summary>
        public const float PanelWidth = 760f, PanelHeight = 774f;

        private const float Margin = 34f, RowHeight = 44f, LabelWidth = 230f;

        /// <summary>For a test tool: the widest text of the Screen row, and the room it has between the arrows, in panel units.</summary>
        public static float WidestScreenLabel { get; private set; }
        public const float ScreenLabelRoom = PanelWidth - 2f * Margin - LabelWidth - 2f * MenuStyle.StepperArrowWidth;

        private static readonly string[] WindowLabels = { "Windowed", "Fullscreen" };
        private static readonly string[] OffOn = { "Off", "On" };

        private readonly List<Vector2Int> _sizes = new List<Vector2Int>();
        private List<DisplayOption> _displays = new List<DisplayOption>();
        private readonly List<string> _displayLabels = new List<string>();
        private int _displayIndex, _listedForMoves = -1;
        private float _lookAgainAt;

        /// <summary>Call when the panel opens: lists the PC's screens, and the window sizes the screen the game is on can show.</summary>
        public void Open()
        {
            _listedForMoves = GameDisplays.MovesFinished;
            _displays = GameDisplays.List();
            _displayLabels.Clear();
            _displayIndex = GameDisplays.CurrentIn(_displays);
            int biggestWidth = _displayIndex >= 0 ? _displays[_displayIndex].Width : int.MaxValue;
            int biggestHeight = _displayIndex >= 0 ? _displays[_displayIndex].Height : int.MaxValue;

            _sizes.Clear();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (size.x <= biggestWidth && size.y <= biggestHeight && !_sizes.Contains(size)) _sizes.Add(size);
            }
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!_sizes.Contains(current)) _sizes.Add(current);
            _sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        /// <summary>Saves the settings to disk; call when the panel closes by any way other than its Back button.</summary>
        public void Close() => GameSettings.Save();

        /// <summary>Draws the panel in the middle of a screen of this virtual size. Returns true when the player has finished with it.</summary>
        public bool Draw(float screenWidth, float screenHeight)
        {
            var panel = new Rect((screenWidth - PanelWidth) * 0.5f, (screenHeight - PanelHeight) * 0.5f, PanelWidth, PanelHeight);
            MenuStyle.Panel(panel);
            GUI.Label(new Rect(panel.x + Margin, panel.y + 14f, panel.width - 2f * Margin, 48f), "SETTINGS", WithLeft(MenuStyle.Heading));

            float y = panel.y + 66f;
            y = Section(panel, y, "SOUND");
            y = VolumeRow(panel, y, "Master", GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
            y = VolumeRow(panel, y, "Music", GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
            y = VolumeRow(panel, y, "Effects", GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);
            y = VolumeRow(panel, y, "Interface", GameSettings.UiVolume, v => GameSettings.UiVolume = v);

            y = Section(panel, y + 4f, "DISPLAY");
            y = ScreenRow(panel, y);
            y = WindowRows(panel, y);
            y = QualityRow(panel, y);
            bool vSync = MenuStyle.Choice(ControlRect(panel, y, "VSync"), "VSync", OffOn, GameSettings.VSync ? 1 : 0) == 1;
            if (vSync != GameSettings.VSync) GameSettings.VSync = vSync;
            y += RowHeight;

            y = Section(panel, y + 4f, "INTERFACE AND EFFECTS");
            int step = MenuStyle.Stepper(ControlRect(panel, y, "UI scale"), "UI scale", $"{GameSettings.UiScale * 100f:0}%");
            if (step != 0) GameSettings.UiScale += step * GameSettings.UiScaleStep;
            y += RowHeight;
            bool shake = MenuStyle.Choice(ControlRect(panel, y, "Camera shake"), "Camera shake", OffOn, GameSettings.CameraShake ? 1 : 0) == 1;
            if (shake != GameSettings.CameraShake) GameSettings.CameraShake = shake;
            y += RowHeight;
            bool frameRate = MenuStyle.Choice(ControlRect(panel, y, "Frame rate counter"), "Frame rate counter", OffOn, GameSettings.ShowFrameRate ? 1 : 0) == 1;
            if (frameRate != GameSettings.ShowFrameRate) GameSettings.ShowFrameRate = frameRate;

            if (MenuStyle.SmallFramedButton(new Rect(panel.x + Margin, panel.yMax - 58f, 190f, 40f), "Reset to defaults", false)) GameSettings.ResetToDefaults();
            if (MenuStyle.SmallFramedButton(new Rect(panel.xMax - Margin - 190f, panel.yMax - 58f, 190f, 40f), "Back", false))
            {
                GameSettings.Save();
                return true;
            }
            return false;
        }

        private static GUIStyle _headingLeft;

        private static GUIStyle WithLeft(GUIStyle style)
        {
            if (_headingLeft == null) _headingLeft = new GUIStyle(style) { alignment = TextAnchor.MiddleLeft };
            return _headingLeft;
        }

        private static float Section(Rect panel, float y, string title)
        {
            GUI.Label(new Rect(panel.x + Margin, y, panel.width - 2f * Margin, 30f), title, MenuStyle.Section);
            MenuStyle.Fill(new Rect(panel.x + Margin, y + 29f, panel.width - 2f * Margin, 1f), MenuStyle.Edge);
            return y + 34f;
        }

        /// <summary>Writes a row's label and gives the rect for its control.</summary>
        private static Rect ControlRect(Rect panel, float y, string label)
        {
            GUI.Label(new Rect(panel.x + Margin, y, LabelWidth, RowHeight - 6f), label, MenuStyle.Body);
            return new Rect(panel.x + Margin + LabelWidth, y + 2f, panel.width - 2f * Margin - LabelWidth, RowHeight - 10f);
        }

        private static float VolumeRow(Rect panel, float y, string label, float value, System.Action<float> set)
        {
            Rect control = ControlRect(panel, y, label);
            float changed = MenuStyle.Slider(new Rect(control.x, control.y, control.width - 70f, control.height), value);
            if (!Mathf.Approximately(changed, value)) set(changed);
            GUI.Label(new Rect(control.xMax - 64f, control.y, 64f, control.height), $"{changed * 100f:0}%", MenuStyle.BodyRight);
            return y + RowHeight;
        }

        private float ScreenRow(Rect panel, float y)
        {
            // A move has ended: the sizes are those of another screen now. A window dragged to another screen shows in a moment too.
            if (_listedForMoves != GameDisplays.MovesFinished) Open();
            else if (Time.unscaledTime >= _lookAgainAt)
            {
                _lookAgainAt = Time.unscaledTime + 0.5f;
                _displayIndex = GameDisplays.CurrentIn(_displays);
            }
            if (_displayLabels.Count != _displays.Count) MakeDisplayLabels();

            Rect control = ControlRect(panel, y, "Screen");
            if (_displays.Count < 2)
            {
                GUI.Label(control, _displays.Count == 1 ? "Only one: " + _displayLabels[0] : "Only one screen", MenuStyle.SmallButton);
                return y + RowHeight;
            }
            int shown = Mathf.Max(0, _displayIndex);
            int step = MenuStyle.Stepper(control, "Screen", _displayLabels[shown]);
            if (step != 0 && !GameDisplays.Moving) GameDisplays.ChooseByPlayer(_displays, (shown + step + _displays.Count) % _displays.Count);
            return y + RowHeight;
        }

        /// <summary>The row's text for each screen, its name cut short if it would not fit between the arrows.</summary>
        private void MakeDisplayLabels()
        {
            _displayLabels.Clear();
            WidestScreenLabel = 0f;
            var measure = new GUIContent();
            float room = ScreenLabelRoom - 8f;
            for (int i = 0; i < _displays.Count; i++)
            {
                string label = DisplayChoice.Label(_displays, i);
                measure.text = label;
                for (int limit = _displays[i].Name?.Length ?? 0; limit > 6 && MenuStyle.SmallButton.CalcSize(measure).x > room; limit--)
                {
                    label = DisplayChoice.Label(_displays, i, limit);
                    measure.text = label;
                }
                WidestScreenLabel = Mathf.Max(WidestScreenLabel, MenuStyle.SmallButton.CalcSize(measure).x);
                _displayLabels.Add(label);
            }
        }

        private float WindowRows(Rect panel, float y)
        {
            bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            int wanted = MenuStyle.Choice(ControlRect(panel, y, "Window"), "Window", WindowLabels, fullscreen ? 1 : 0);
            if (wanted == 1 != fullscreen) GameSettings.SetWindow(Screen.width, Screen.height, wanted == 1);
            y += RowHeight;

            int index = _sizes.IndexOf(new Vector2Int(Screen.width, Screen.height));
            int step = MenuStyle.Stepper(ControlRect(panel, y, "Resolution"), "Resolution", $"{Screen.width} x {Screen.height}");
            if (step != 0 && _sizes.Count > 0)
            {
                // A window dragged to some other size sits between the listed ones: go to the nearest in that direction.
                if (index < 0) index = step > 0 ? -1 : _sizes.Count;
                Vector2Int size = _sizes[Mathf.Clamp(index + step, 0, _sizes.Count - 1)];
                GameSettings.SetWindow(size.x, size.y, wanted == 1);
            }
            return y + RowHeight;
        }

        private static float QualityRow(Rect panel, float y)
        {
            string[] names = QualitySettings.names;
            var labels = new string[names.Length];
            for (int i = 0; i < names.Length; i++) labels[i] = names[i] == "Mobile" ? "Low" : names[i] == "PC" ? "High" : names[i];
            int current = GameSettings.QualityLevel;
            int wanted = MenuStyle.Choice(ControlRect(panel, y, "Quality"), "Quality", labels, current);
            if (wanted != current) GameSettings.QualityLevel = wanted;
            return y + RowHeight;
        }
    }
}
