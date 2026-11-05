// GameSettings.cs
// The player's settings (M10), all kept in PlayerPrefs and applied when the game starts.
//   Volume.Master, Volume.Music, Volume.Sfx, Volume.Ui  floats from 0 to 1. The sound code reads these by itself.
//   Fx.CameraShake                                      int, 1 on, 0 off. The effects code reads this by itself.
//   Video.Fullscreen, Video.Width, Video.Height         ints; -1 means never chosen, so Unity's own remembered window stands.
//   Video.Screen                                        string, the chosen screen in DisplayChoice's saved form; empty means never chosen, so the game starts on Windows's main screen.
//   Video.Quality                                       int, a level from Project Settings > Quality; -1 means never chosen.
//   Video.VSync                                         int, 1 on, 0 off.
//   Ui.Scale                                            float, a factor on top of the HUD's scale to the window.
//   Halobellum.AiDifficulty                             int, 0 Easy, 1 Normal, 2 Hard.
//   Skirmish.BaseHealth                                 int, Unlock Skirmish's base health from the Play page, 100 to 10000 in steps of 100.
//   Ui.ShowFrameRate                                    int, 1 shows the frame-rate counter in battle, 0 hides it.
//   Log.BattleLog                                       int, 1 writes a battle log of each game played (D68), 0 writes none (D69).
// A run of a test tool, or one with -screen-width and so on, keeps its window and frame rate: a saved window must not change what
// another tool measures. Benchmarks turn VSync off for themselves after this has run.
// A run of a test tool also holds the settings in memory (HoldInMemory): it starts from what the player saved, and what it changes, by a click or the K key
// that reaches its window, say, lasts only as long as the run, because PlayerPrefs are shared by every build on this PC. It logs at the end whether the saved
// settings are as they were. The quality level Unity itself remembers is put back as the run ends.
// The sound code keeps reading the saved volumes, so a change to a volume in a test run is not heard.
// Unity's own remembered window can't be held from here: it writes -screen-width and so on to the same PlayerPrefs before any game code runs. Only a build of
// its own keeps a test tool away from the player's window: see -testBuild in PlayerBuildTool.
// Depends on: GameFlow (command line), GameDisplays (the screen), MatchController (difficulty key), Halobellum.Economy, UnityEngine.

using System;
using System.Collections.Generic;
using System.Text;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class GameSettings
    {
        public const string MasterVolumeKey = "Volume.Master", MusicVolumeKey = "Volume.Music", SfxVolumeKey = "Volume.Sfx", UiVolumeKey = "Volume.Ui";
        public const string CameraShakeKey = "Fx.CameraShake";
        public const string FullscreenKey = "Video.Fullscreen", WidthKey = "Video.Width", HeightKey = "Video.Height";
        public const string QualityKey = "Video.Quality", VSyncKey = "Video.VSync", ScreenKey = "Video.Screen";
        public const string UiScaleKey = "Ui.Scale", ShowFrameRateKey = "Ui.ShowFrameRate";
        public const string SkirmishBaseHealthKey = "Skirmish.BaseHealth";
        public const string BattleLogKey = "Log.BattleLog";

        public const float DefaultMasterVolume = 1f, DefaultMusicVolume = 0.6f, DefaultSfxVolume = 1f, DefaultUiVolume = 1f;
        public const float SmallestUiScale = 0.75f, LargestUiScale = 1.25f, UiScaleStep = 0.05f;
        public const int DefaultSkirmishBaseHealth = 2000;

        private static readonly string[] SavedFloatKeys = { MasterVolumeKey, MusicVolumeKey, SfxVolumeKey, UiVolumeKey, UiScaleKey };
        private static readonly string[] SavedIntKeys = { CameraShakeKey, FullscreenKey, WidthKey, HeightKey, QualityKey, VSyncKey, MatchController.DifficultyKey, SkirmishBaseHealthKey, ShowFrameRateKey, BattleLogKey };

        private static readonly string[] SavedStringKeys = { ScreenKey };

        private static float _uiScale = -1f;
        /// <summary>The frame-rate setting as last read: 0 off, 1 on, -1 not read yet.</summary>
        private static int _showFrameRate = -1;
        /// <summary>What a hold has changed so far, by key, or null when nothing is held.</summary>
        private static Dictionary<string, object> _held;
        private static bool _heldStartedEmpty;
        private static string _savedAtStart;
        private static int _qualityAtStart;

        /// <summary>True while changes stay in memory.</summary>
        public static bool HeldInMemory => _held != null;

        /// <summary>From now on nothing here reaches PlayerPrefs. startEmpty: also read as if nothing had ever been saved (for tests of the defaults).</summary>
        public static void HoldInMemory(bool startEmpty = false)
        {
            _held = new Dictionary<string, object>();
            _heldStartedEmpty = startEmpty;
            _uiScale = -1f;
            _showFrameRate = -1;
        }

        /// <summary>Forgets what the hold changed: the saved settings count again.</summary>
        public static void ReleaseHold()
        {
            _held = null;
            _heldStartedEmpty = false;
            _uiScale = -1f;
            _showFrameRate = -1;
        }

        public static float MasterVolume { get => Volume(MasterVolumeKey, DefaultMasterVolume); set => SetVolume(MasterVolumeKey, value); }
        public static float MusicVolume { get => Volume(MusicVolumeKey, DefaultMusicVolume); set => SetVolume(MusicVolumeKey, value); }
        public static float SfxVolume { get => Volume(SfxVolumeKey, DefaultSfxVolume); set => SetVolume(SfxVolumeKey, value); }
        public static float UiVolume { get => Volume(UiVolumeKey, DefaultUiVolume); set => SetVolume(UiVolumeKey, value); }

        public static bool CameraShake
        {
            get => GetInt(CameraShakeKey, 1) != 0;
            set => SetInt(CameraShakeKey, value ? 1 : 0);
        }

        public static bool VSync
        {
            get => GetInt(VSyncKey, 1) != 0;
            set
            {
                SetInt(VSyncKey, value ? 1 : 0);
                QualitySettings.vSyncCount = value ? 1 : 0;
            }
        }

        /// <summary>The factor on top of the HUD's scale to the window. Read every time the HUD draws, so it is kept in memory.</summary>
        public static float UiScale
        {
            get
            {
                if (_uiScale < 0f) _uiScale = ClampUiScale(GetFloat(UiScaleKey, 1f));
                return _uiScale;
            }
            set
            {
                _uiScale = ClampUiScale(value);
                SetFloat(UiScaleKey, _uiScale);
            }
        }

        /// <summary>Shows the frame-rate counter in battle. Read every time the battle's HUD draws, so it is kept in memory.</summary>
        public static bool ShowFrameRate
        {
            get
            {
                if (_showFrameRate < 0) _showFrameRate = GetInt(ShowFrameRateKey, 0) != 0 ? 1 : 0;
                return _showFrameRate == 1;
            }
            set
            {
                _showFrameRate = value ? 1 : 0;
                SetInt(ShowFrameRateKey, _showFrameRate);
            }
        }

        /// <summary>Writes a battle log of each game played (D68). On unless the player turns it off (D69).</summary>
        public static bool BattleLog
        {
            get => GetInt(BattleLogKey, 1) != 0;
            set => SetInt(BattleLogKey, value ? 1 : 0);
        }

        /// <summary>Unlock Skirmish's base health, always inside the limits and on a whole step.</summary>
        public static int SkirmishBaseHealth
        {
            get => MatchSetup.SnapBaseHealth(GetInt(SkirmishBaseHealthKey, DefaultSkirmishBaseHealth));
            set => SetInt(SkirmishBaseHealthKey, MatchSetup.SnapBaseHealth(value));
        }

        /// <summary>The AI's difficulty, on the menu and the draft screen alike.</summary>
        public static AiDifficulty Difficulty
        {
            get => (AiDifficulty)Mathf.Clamp(GetInt(MatchController.DifficultyKey, (int)AiDifficulty.Normal), 0, 2);
            set => SetInt(MatchController.DifficultyKey, (int)value);
        }

        /// <summary>Uses this UI scale until the game closes, without saving it (a test tool's way to look at every screen at another size).</summary>
        public static void UseUiScaleForThisRun(float scale) => _uiScale = ClampUiScale(scale);

        public static float ClampUiScale(float scale)
        {
            if (float.IsNaN(scale)) return 1f;
            return Mathf.Clamp(Mathf.Round(scale / UiScaleStep) * UiScaleStep, SmallestUiScale, LargestUiScale);
        }

        /// <summary>The quality level in use: the saved one if it still exists, otherwise the project's.</summary>
        public static int QualityLevel
        {
            get
            {
                int saved = GetInt(QualityKey, -1);
                return saved >= 0 && saved < QualitySettings.names.Length ? saved : QualitySettings.GetQualityLevel();
            }
            set
            {
                int level = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                SetInt(QualityKey, level);
                QualitySettings.SetQualityLevel(level, true);
                QualitySettings.vSyncCount = VSync ? 1 : 0; // A quality level carries its own VSync.
            }
        }

        /// <summary>The screen the player chose, in DisplayChoice's saved form, or empty if never. GameDisplays moves the window.</summary>
        public static string ChosenScreen
        {
            get
            {
                if (_held != null && _held.TryGetValue(ScreenKey, out object value)) return (string)value;
                return _heldStartedEmpty ? "" : PlayerPrefs.GetString(ScreenKey, "");
            }
            set
            {
                if (_held != null) _held[ScreenKey] = value;
                else PlayerPrefs.SetString(ScreenKey, value);
            }
        }

        /// <summary>Changes the window and remembers it.</summary>
        public static void SetWindow(int width, int height, bool fullscreen)
        {
            SetInt(WidthKey, width);
            SetInt(HeightKey, height);
            SetInt(FullscreenKey, fullscreen ? 1 : 0);
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        public static void Save()
        {
            if (_held == null) PlayerPrefs.Save();
        }

        /// <summary>Puts the sound, effects and interface settings back to how the game ships. The window and quality stay as they are.</summary>
        public static void ResetToDefaults()
        {
            MasterVolume = DefaultMasterVolume;
            MusicVolume = DefaultMusicVolume;
            SfxVolume = DefaultSfxVolume;
            UiVolume = DefaultUiVolume;
            CameraShake = true;
            UiScale = 1f;
            ShowFrameRate = false;
            BattleLog = true;
            SkirmishBaseHealth = DefaultSkirmishBaseHealth;
            Save();
        }

        private static float Volume(string key, float fallback) => Mathf.Clamp01(GetFloat(key, fallback));

        private static void SetVolume(string key, float value) => SetFloat(key, Mathf.Clamp01(value));

        private static float GetFloat(string key, float fallback)
        {
            if (_held != null && _held.TryGetValue(key, out object value)) return (float)value;
            return _heldStartedEmpty ? fallback : PlayerPrefs.GetFloat(key, fallback);
        }

        private static int GetInt(string key, int fallback)
        {
            if (_held != null && _held.TryGetValue(key, out object value)) return (int)value;
            return _heldStartedEmpty ? fallback : PlayerPrefs.GetInt(key, fallback);
        }

        private static void SetFloat(string key, float value)
        {
            if (_held != null) _held[key] = value;
            else PlayerPrefs.SetFloat(key, value);
        }

        private static void SetInt(string key, int value)
        {
            if (_held != null) _held[key] = value;
            else PlayerPrefs.SetInt(key, value);
        }

        /// <summary>The saved settings as they stand, for comparing.</summary>
        private static string SavedValues()
        {
            var text = new StringBuilder();
            foreach (string key in SavedFloatKeys) text.Append(key).Append('=').Append(PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key).ToString("R") : "none").Append(' ');
            foreach (string key in SavedIntKeys) text.Append(key).Append('=').Append(PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key).ToString() : "none").Append(' ');
            foreach (string key in SavedStringKeys) text.Append(key).Append('=').Append(PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : "none").Append(' ');
            return text.ToString();
        }

        /// <summary>A test tool's run: changes stay in memory, and the quality level goes back when the run ends.</summary>
        private static void StartTestRun()
        {
            HoldInMemory();
            _savedAtStart = SavedValues();
            _qualityAtStart = QualitySettings.GetQualityLevel();
            Debug.Log("GameSettings: a test run: the settings are held in memory.");
            Application.quitting += EndTestRun;
        }

        private static void EndTestRun()
        {
            QualitySettings.SetQualityLevel(_qualityAtStart, false);
            string now = SavedValues();
            Debug.Log(now == _savedAtStart ? "GameSettings: this test run changed no saved setting." : $"GameSettings: PROBLEM: the saved settings changed in a test run: was {_savedAtStart}, now {now}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStart()
        {
            _uiScale = -1f;
            _showFrameRate = -1;
            string[] args = Environment.GetCommandLineArgs();
            if (GameFlow.IsTestRun(args))
            {
                StartTestRun();
                return;
            }

            int quality = PlayerPrefs.GetInt(QualityKey, -1);
            if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(quality, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            if (GameFlow.CommandLineSetsWindow(args)) return;
            int width = PlayerPrefs.GetInt(WidthKey, -1), height = PlayerPrefs.GetInt(HeightKey, -1), fullscreen = PlayerPrefs.GetInt(FullscreenKey, -1);
            FullScreenMode mode = fullscreen == 1 ? FullScreenMode.FullScreenWindow : fullscreen == 0 ? FullScreenMode.Windowed : Screen.fullScreenMode;
            GameDisplays.GoToChosenAtStart(ChosenScreen, width > 0 ? width : Screen.width, height > 0 ? height : Screen.height, mode, fullscreen >= 0 || width > 0);
        }
    }
}
