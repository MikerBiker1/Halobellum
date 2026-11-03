// GameFlow.cs
// Which scene the game goes to, and when (M10). A built game starts in the MainMenu scene; New Match, Sandbox and Quit to Main
// Menu all come through here, so that the scene names live in one place and leaving a scene always un-pauses first.
// The test tools (-autoplay, -sandboxDemo, -gallery, -crowdBench, -benchmark) skip the menu: as soon as MainMenu has loaded, the
// game moves on to the scene that tool has always run in. -menuDemo and -screenDemo stay in the menu, because the menu is what they test.
// It also remembers where the Sandbox was opened from, for the Sandbox's Back button.
// Depends on: GamePause, SandboxAutoplay, CrowdBenchmark and AudioCapture (their flags), UnityEngine.

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.Presentation
{
    public static class GameFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string MatchScene = "Match";
        public const string SandboxScene = "Sandbox";
        public const string BattleScene = "Battle";

        public const string AutoplayFlag = "-autoplay";
        public const string GalleryFlag = "-gallery";
        public const string BenchmarkFlag = "-benchmark";
        public const string MenuDemoFlag = "-menuDemo";
        public const string ScreenDemoFlag = "-screenDemo";
        public const string CardsDemoFlag = "-cardsDemo";
        public const string TerrainGalleryFlag = "-terrainGallery";

        private static string _lastScene;

        /// <summary>The scene the Sandbox's Back button returns to.</summary>
        public static string SandboxCameFrom { get; private set; } = MatchScene;

        /// <summary>What the Sandbox's Back button says.</summary>
        public static string SandboxBackLabel => SandboxCameFrom == MainMenuScene ? "Main menu" : "Back to match";

        /// <summary>The scene a test tool on this command line runs in, or null to show the main menu as usual.</summary>
        public static string DirectSceneFor(string[] args)
        {
            if (Has(args, SandboxAutoplay.Flag) || Has(args, GalleryFlag)) return SandboxScene;
            if (Has(args, AutoplayFlag) || Has(args, CrowdBenchmark.Flag) || Has(args, CardsDemoFlag)) return MatchScene;
            if (Has(args, BenchmarkFlag)) return BattleScene;
            return null;
        }

        /// <summary>True for a run that measures frame times: nothing extra is attached to the scene.</summary>
        public static bool IsMeasuring(string[] args) => Has(args, CrowdBenchmark.Flag) || Has(args, BenchmarkFlag);

        /// <summary>True for a run of any test tool: the player's saved settings are not applied, and nothing it does reaches them (GameSettings).</summary>
        public static bool IsTestRun(string[] args) => DirectSceneFor(args) != null || Has(args, MenuDemoFlag) || Has(args, ScreenDemoFlag) || Has(args, TerrainGalleryFlag) || Has(args, AudioCapture.Flag);

        /// <summary>True if the command line itself sets the window, which the saved settings must not undo.</summary>
        public static bool CommandLineSetsWindow(string[] args)
        {
            return Has(args, "-screen-width") || Has(args, "-screen-height") || Has(args, "-screen-fullscreen") || Has(args, "-popupwindow") || Has(args, "-monitor");
        }

        public static void OpenMainMenu() => Load(MainMenuScene);

        public static void NewMatch() => Load(MatchScene);

        public static void OpenSandbox() => Load(SandboxScene);

        /// <summary>The Sandbox's Back button: to the match or the menu, whichever it was opened from.</summary>
        public static void LeaveSandbox() => Load(SandboxCameFrom);

        public static void QuitGame()
        {
            GamePause.Resume();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private static void Load(string scene)
        {
            GamePause.Resume();
            SceneManager.LoadScene(scene);
        }

        private static bool Has(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            _lastScene = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GamePause.Resume();
            if (scene.name == SandboxScene && _lastScene != null) SandboxCameFrom = _lastScene;
            bool firstScene = _lastScene == null;
            _lastScene = scene.name;

            // Only the scene a build starts in hands over, so that a tool can still come back to the menu later.
            if (!firstScene || scene.name != MainMenuScene) return;
            string direct = DirectSceneFor(Environment.GetCommandLineArgs());
            if (direct != null) SceneManager.LoadScene(direct);
        }
    }
}
