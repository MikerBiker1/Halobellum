// GameFlow.cs
// Which scene the game goes to, and when (M10). A built game starts in the MainMenu scene; New Match, Sandbox and Quit to Main
// Menu all come through here, so that the scene names live in one place and leaving a scene always un-pauses first.
// The test tools (-autoplay, -sandboxDemo, -gallery, -crowdBench, -benchmark) and a battle log's replay (-replayLog, D106) skip the menu: as soon as MainMenu has loaded, the
// game moves on to the scene that tool has always run in. -menuDemo and -screenDemo stay in the menu, because the menu is what they test.
// It also remembers where the Sandbox was opened from, for the Sandbox's Back button.
// There are two main menus (D94): the cinematic battlefield, MainMenu, where a build starts, and the classic one from before it, MainMenuClassic,
// which the player can choose in Settings; then the game goes there instead, straight away from the start and whenever it returns to the menu.
// A run of a test tool always has the cinematic one.
// A setup chosen for the next match waits here for the Match scene, which takes it once, so that any other load of the scene
// (a test tool, a test) always gets Faction mode.
// A LAN match (M11) waits here the same way: the LAN Game page hands over the connection to the other game, this game's team and
// (on the host) the match settings (LanLaunch); the Match scene takes it once and its LanMatch driver builds the match from it.
// New game after a LAN match hands a new launch over the same open connection and loads the Match scene again (LanMatch.StartRematch).
// -lanDemo (a LAN test run, both sides played by the AI) is a test run: the saved settings stay untouched, so two copies on one PC
// don't overwrite each other's. -windowSlot sets the window from the command line (WindowSlot).
// Depends on: GamePause, SandboxAutoplay, CrowdBenchmark, AudioCapture and MatchReplay (their flags), Halobellum.Economy (MatchSetup),
//             Halobellum.Net (ILanChannel, LanMatchSettings, LanDemoArgs), Halobellum.Core (Team), UnityEngine.

using System;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Halobellum.Presentation
{
    /// <summary>What the Match scene needs to start a LAN match (M11): the connection, this game's team and, on the host, the settings.</summary>
    public sealed class LanLaunch
    {
        /// <summary>The connection to the other game, handshake done (a LanSession, or on the guest the session with the host's MatchStart put back).</summary>
        public ILanChannel Channel;
        /// <summary>Team A on the host, Team B on the guest.</summary>
        public Team LocalTeam = Team.A;
        /// <summary>The host's seed and skirmish settings; null on the guest (the host's MatchStart carries them).</summary>
        public LanMatchSettings Settings;
        /// <summary>The other player's name, for the top bar.</summary>
        public string OpponentName = "";
        /// <summary>A -lanDemo run: the AI plays this side, battles run fast and the game quits after the match.</summary>
        public bool Demo;
        /// <summary>-lanDemoMatches: how many matches a demo plays on this connection through New game before it quits.</summary>
        public int DemoMatches = 1;
        /// <summary>-lanDemoSeed (host): the k-th new game uses this seed plus k; null for a fresh seed each time.</summary>
        public int? DemoSeed;
        /// <summary>Which match this is on the connection: 1 from the menu, 2 after the first New game, and so on.</summary>
        public int MatchNumber = 1;

        public bool IsHost => LocalTeam == Team.A;
    }

    public static class GameFlow
    {
        /// <summary>The scene a build starts in: it reads the unit stats files, then opens the main menu (BootLoader).</summary>
        public const string BootScene = "Boot";
        public const string MainMenuScene = "MainMenu";
        public const string ClassicMainMenuScene = "MainMenuClassic";
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
        public const string TutorialDemoFlag = "-tutorialDemo";
        public const string LanDemoFlag = LanDemoArgs.DemoFlag;
        public const string WindowSlotFlag = LanDemoArgs.WindowSlotFlag;

        private static string _lastScene;
        private static MatchSetup _pendingSetup;
        private static bool _tutorialPending;
        private static LanLaunch _pendingLan;

        /// <summary>The scene the Sandbox's Back button returns to.</summary>
        public static string SandboxCameFrom { get; private set; } = MatchScene;

        /// <summary>What the Sandbox's Back button says.</summary>
        public static string SandboxBackLabel => IsMainMenu(SandboxCameFrom) ? "Main menu" : "Back to match";

        /// <summary>True for either main menu (D94).</summary>
        public static bool IsMainMenu(string scene) => scene == MainMenuScene || scene == ClassicMainMenuScene;

        /// <summary>The main menu the player chose in Settings: the cinematic one unless they chose the classic, and always it in a test run (D94).</summary>
        public static string MenuSceneToOpen => MenuSceneFor(GameSettings.ClassicMenu, Environment.GetCommandLineArgs());

        /// <summary>The main menu to open with this choice in Settings and this command line.</summary>
        public static string MenuSceneFor(bool classic, string[] args) => classic && !IsTestRun(args) ? ClassicMainMenuScene : MainMenuScene;

        /// <summary>The scene a test tool on this command line runs in, or null to show the main menu as usual.</summary>
        public static string DirectSceneFor(string[] args)
        {
            if (Has(args, SandboxAutoplay.Flag) || Has(args, GalleryFlag)) return SandboxScene;
            if (Has(args, AutoplayFlag) || Has(args, CrowdBenchmark.Flag) || Has(args, CardsDemoFlag) || Has(args, MatchReplay.LogFlag)) return MatchScene;
            if (Has(args, BenchmarkFlag)) return BattleScene;
            return null;
        }

        /// <summary>True for a run that measures frame times: nothing extra is attached to the scene.</summary>
        public static bool IsMeasuring(string[] args) => Has(args, CrowdBenchmark.Flag) || Has(args, BenchmarkFlag);

        /// <summary>True for a run of any test tool: the player's saved settings are not applied, and nothing it does reaches them (GameSettings).</summary>
        public static bool IsTestRun(string[] args) => DirectSceneFor(args) != null || Has(args, MenuDemoFlag) || Has(args, ScreenDemoFlag) || Has(args, TerrainGalleryFlag) || Has(args, TutorialDemoFlag) || Has(args, AudioCapture.Flag) || Has(args, LanDemoFlag);

        /// <summary>True if the command line itself sets the window, which the saved settings must not undo.</summary>
        public static bool CommandLineSetsWindow(string[] args)
        {
            return Has(args, "-screen-width") || Has(args, "-screen-height") || Has(args, "-screen-fullscreen") || Has(args, "-popupwindow") || Has(args, "-monitor") || Has(args, WindowSlotFlag);
        }

        public static void OpenMainMenu() => Load(MenuSceneToOpen);

        public static void NewMatch()
        {
            ForgetLan();
            Load(MatchScene);
        }

        /// <summary>Starts a match with this setup.</summary>
        public static void NewSkirmish(MatchSetup setup)
        {
            ForgetLan();
            _pendingSetup = setup;
            Load(MatchScene);
        }

        /// <summary>Starts a LAN match (M11): the Match scene takes the launch once (TakeLanMatch).</summary>
        public static void StartLanMatch(LanLaunch launch)
        {
            SetLanLaunch(launch);
            Load(MatchScene);
        }

        /// <summary>Leaves a LAN launch for the next load of the Match scene, without loading it (StartLanMatch does both).</summary>
        public static void SetLanLaunch(LanLaunch launch)
        {
            if (launch == null || launch.Channel == null) throw new ArgumentException("A LAN launch needs its connection.", nameof(launch));
            if (_pendingLan != launch) ForgetLan();
            _pendingSetup = null;
            _tutorialPending = false;
            _pendingLan = launch;
        }

        /// <summary>The LAN launch waiting for the Match scene, or null. It can only be taken once.</summary>
        public static LanLaunch TakeLanMatch()
        {
            LanLaunch launch = _pendingLan;
            _pendingLan = null;
            return launch;
        }

        /// <summary>A launch nobody took is closed, so its connection does not hang on.</summary>
        private static void ForgetLan()
        {
            _pendingLan?.Channel?.Close();
            _pendingLan = null;
        }

        /// <summary>The setup waiting for the Match scene, or null. It can only be taken once.</summary>
        public static MatchSetup TakeMatchSetup()
        {
            MatchSetup setup = _pendingSetup;
            _pendingSetup = null;
            return setup;
        }

        /// <summary>Starts the tutorial's scripted match (D77).</summary>
        public static void StartTutorial()
        {
            ForgetLan();
            _pendingSetup = null;
            _tutorialPending = true;
            Load(MatchScene);
        }

        /// <summary>True once, for the Match scene, if the tutorial was asked for.</summary>
        public static bool TakeTutorial()
        {
            bool pending = _tutorialPending;
            _tutorialPending = false;
            return pending;
        }

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
            _pendingSetup = null;
            _tutorialPending = false;
            _pendingLan = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GamePause.Resume();
            if (scene.name == BootScene) return; // It hands over to the main menu, which is still the first scene for what follows.
            if (scene.name == SandboxScene && _lastScene != null) SandboxCameFrom = _lastScene;
            bool firstScene = _lastScene == null;
            _lastScene = scene.name;

            // Only the scene a build starts in hands over, so that a tool can still come back to the menu later.
            if (!firstScene || scene.name != MainMenuScene) return;
            string direct = DirectSceneFor(Environment.GetCommandLineArgs());
            if (direct != null) SceneManager.LoadScene(direct);
            else if (MenuSceneToOpen != MainMenuScene) SceneManager.LoadScene(MenuSceneToOpen); // The player chose the classic menu (D94).
        }
    }
}
