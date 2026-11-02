// GameSettings.cs
// The player's settings (M10), all kept in PlayerPrefs and applied when the game starts.
//   Volume.Master, Volume.Music, Volume.Sfx, Volume.Ui  floats from 0 to 1. The sound code reads these by itself.
//   Fx.CameraShake                                      int, 1 on, 0 off. The effects code reads this by itself.
//   Video.Fullscreen, Video.Width, Video.Height         ints; -1 means never chosen, so Unity's own remembered window stands.
//   Video.Quality                                       int, a level from Project Settings > Quality; -1 means never chosen.
//   Video.VSync                                         int, 1 on, 0 off.
//   Ui.Scale                                            float, a factor on top of the HUD's scale to the window.
// A run of a test tool, or one with -screen-width and so on, keeps its window and frame rate: a saved window must not change what
// another tool measures. Benchmarks turn VSync off for themselves after this has run.
// Depends on: GameFlow (command line), UnityEngine.

using System;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class GameSettings
    {
        public const string MasterVolumeKey = "Volume.Master", MusicVolumeKey = "Volume.Music", SfxVolumeKey = "Volume.Sfx", UiVolumeKey = "Volume.Ui";
        public const string CameraShakeKey = "Fx.CameraShake";
        public const string FullscreenKey = "Video.Fullscreen", WidthKey = "Video.Width", HeightKey = "Video.Height";
        public const string QualityKey = "Video.Quality", VSyncKey = "Video.VSync";
        public const string UiScaleKey = "Ui.Scale";

        public const float DefaultMasterVolume = 1f, DefaultMusicVolume = 0.6f, DefaultSfxVolume = 1f, DefaultUiVolume = 1f;
        public const float SmallestUiScale = 0.75f, LargestUiScale = 1.25f, UiScaleStep = 0.05f;

        private static float _uiScale = -1f;

        public static float MasterVolume { get => Volume(MasterVolumeKey, DefaultMasterVolume); set => SetVolume(MasterVolumeKey, value); }
        public static float MusicVolume { get => Volume(MusicVolumeKey, DefaultMusicVolume); set => SetVolume(MusicVolumeKey, value); }
        public static float SfxVolume { get => Volume(SfxVolumeKey, DefaultSfxVolume); set => SetVolume(SfxVolumeKey, value); }
        public static float UiVolume { get => Volume(UiVolumeKey, DefaultUiVolume); set => SetVolume(UiVolumeKey, value); }

        public static bool CameraShake
        {
            get => PlayerPrefs.GetInt(CameraShakeKey, 1) != 0;
            set => PlayerPrefs.SetInt(CameraShakeKey, value ? 1 : 0);
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
                QualitySettings.vSyncCount = value ? 1 : 0;
            }
        }

        /// <summary>The factor on top of the HUD's scale to the window. Read every time the HUD draws, so it is kept in memory.</summary>
        public static float UiScale
        {
            get
            {
                if (_uiScale < 0f) _uiScale = ClampUiScale(PlayerPrefs.GetFloat(UiScaleKey, 1f));
                return _uiScale;
            }
            set
            {
                _uiScale = ClampUiScale(value);
                PlayerPrefs.SetFloat(UiScaleKey, _uiScale);
            }
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
                int saved = PlayerPrefs.GetInt(QualityKey, -1);
                return saved >= 0 && saved < QualitySettings.names.Length ? saved : QualitySettings.GetQualityLevel();
            }
            set
            {
                int level = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                PlayerPrefs.SetInt(QualityKey, level);
                QualitySettings.SetQualityLevel(level, true);
                QualitySettings.vSyncCount = VSync ? 1 : 0; // A quality level carries its own VSync.
            }
        }

        /// <summary>Changes the window and remembers it.</summary>
        public static void SetWindow(int width, int height, bool fullscreen)
        {
            PlayerPrefs.SetInt(WidthKey, width);
            PlayerPrefs.SetInt(HeightKey, height);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            Screen.SetResolution(width, height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>Puts the sound, effects and interface settings back to how the game ships. The window and quality stay as they are.</summary>
        public static void ResetToDefaults()
        {
            MasterVolume = DefaultMasterVolume;
            MusicVolume = DefaultMusicVolume;
            SfxVolume = DefaultSfxVolume;
            UiVolume = DefaultUiVolume;
            CameraShake = true;
            UiScale = 1f;
            Save();
        }

        private static float Volume(string key, float fallback) => Mathf.Clamp01(PlayerPrefs.GetFloat(key, fallback));

        private static void SetVolume(string key, float value) => PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStart()
        {
            _uiScale = -1f;
            string[] args = Environment.GetCommandLineArgs();
            if (GameFlow.IsTestRun(args)) return;

            int quality = PlayerPrefs.GetInt(QualityKey, -1);
            if (quality >= 0 && quality < QualitySettings.names.Length && quality != QualitySettings.GetQualityLevel()) QualitySettings.SetQualityLevel(quality, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            if (GameFlow.CommandLineSetsWindow(args)) return;
            int width = PlayerPrefs.GetInt(WidthKey, -1), height = PlayerPrefs.GetInt(HeightKey, -1), fullscreen = PlayerPrefs.GetInt(FullscreenKey, -1);
            if (fullscreen < 0 && width <= 0) return;
            FullScreenMode mode = fullscreen == 1 ? FullScreenMode.FullScreenWindow : fullscreen == 0 ? FullScreenMode.Windowed : Screen.fullScreenMode;
            Screen.SetResolution(width > 0 ? width : Screen.width, height > 0 ? height : Screen.height, mode);
        }
    }
}
