// MenuDemo.cs
// Test tool for built games (M10): walks through the whole game flow with nobody at the keyboard, and saves a picture of each screen.
// It does nothing unless the game is started with "-menuDemo <screenshot folder>". It presses the same buttons a player would (through
// MenuStyle.Button) and sends a real Esc through the Input System. Add "-menuDemoShort" to look only at the menu and settings.
//   The menu, three moments apart; Settings; back; New Match; the draft; a card; Esc in deployment; Settings from there; back; Resume;
//   Ready; Esc in battle, checking that no tick runs while paused and that ticks run again after Resume; the result; a forced end of
//   match and its Main Menu button; the menu again; Sandbox; Esc; its Back button; Quit.
// It writes "MenuDemo: ..." lines in the player's log, one for each thing checked, and ends with how many went wrong. It never changes
// a setting, because PlayerPrefs are shared by every build on this PC: in a test run GameSettings holds them in memory, and logs at the end whether the saved ones are as they were.
// With "-menuDemoUiScale 1.25" it looks at every screen at that UI scale, without saving it.
// With "-menuDemoWatch" it presses nothing: for 90 seconds it logs and pictures every change of scene, pause, settings panel, window or
// setting, so that clicks sent from outside the game (a real mouse, say) can be checked. Settings changed that way last only as long as the run.
// Depends on: GameFlow, GamePause, MatchController, SandboxController, BattleController, PauseMenu, MainMenuHud, MenuStyle, Input System.

using System;
using System.Collections;
using System.Globalization;
using System.IO;
using Halobellum.Economy;
using Halobellum.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Halobellum.UI
{
    public sealed class MenuDemo : MonoBehaviour
    {
        private const string ShortFlag = "-menuDemoShort";
        private const string WatchFlag = "-menuDemoWatch";
        private const string UiScaleFlag = "-menuDemoUiScale";
        private const float WatchSeconds = 90f;

        private string _folder;
        private bool _short;
        private int _problems;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, GameFlow.MenuDemoFlag);
            if (index < 0) return;
            var host = new GameObject("Menu Demo");
            DontDestroyOnLoad(host);
            var demo = host.AddComponent<MenuDemo>();
            demo._folder = index + 1 < args.Length ? args[index + 1] : "MenuShots";
            demo._short = Array.IndexOf(args, ShortFlag) >= 0;
            Directory.CreateDirectory(demo._folder);
            int scale = Array.IndexOf(args, UiScaleFlag);
            if (scale >= 0 && scale + 1 < args.Length && float.TryParse(args[scale + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float factor))
                GameSettings.UseUiScaleForThisRun(factor);
        }

        private IEnumerator Watch()
        {
            // What the clicks do to a setting stays in memory (GameSettings). The window and quality really change, so they go back at the end.
            int startWidth = Screen.width, startHeight = Screen.height;
            FullScreenMode startMode = Screen.fullScreenMode;
            int startQuality = QualitySettings.GetQualityLevel();

            string last = null;
            float until = Time.realtimeSinceStartup + WatchSeconds;
            int number = 0;
            while (Time.realtimeSinceStartup < until)
            {
                var menu = FindAnyObjectByType<MainMenuHud>();
                string now = $"scene {SceneManager.GetActiveScene().name}, paused {GamePause.IsPaused}, settings {menu != null && menu.ShowingSettings}, " +
                             $"window {Screen.width}x{Screen.height} {Screen.fullScreenMode}, quality {QualitySettings.GetQualityLevel()}, vsync {QualitySettings.vSyncCount}, " +
                             $"music {GameSettings.MusicVolume:0.00}, ui {GameSettings.UiScale:0.00}, shake {GameSettings.CameraShake}";
                if (now != last)
                {
                    last = now;
                    Debug.Log($"MenuDemo watch: {Time.realtimeSinceStartup:0.0} s: {now}");
                    yield return Wait(0.3f);
                    yield return Shot($"watch_{number++:00}");
                }
                yield return null;
            }

            QualitySettings.SetQualityLevel(startQuality, true);
            Screen.SetResolution(startWidth, startHeight, startMode);
            yield return Wait(1f);
            Application.Quit();
        }

        private IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), WatchFlag) >= 0)
            {
                yield return Watch();
                yield break;
            }
            Check("the game starts in the main menu", SceneManager.GetActiveScene().name == GameFlow.MainMenuScene);
            yield return Wait(3.5f);
            yield return Shot("01_menu_a");
            yield return Wait(6f);
            yield return Shot("02_menu_b");
            yield return Wait(9f);
            yield return Shot("03_menu_c");

            yield return Press("Settings");
            yield return Wait(0.4f);
            Check("Settings opens from the menu", FindAnyObjectByType<MainMenuHud>().ShowingSettings);
            yield return Shot("04_settings");

            // The Low quality level, without saving it: it must still draw the whole picture.
            int quality = QualitySettings.GetQualityLevel();
            if (QualitySettings.names.Length > 1)
            {
                QualitySettings.SetQualityLevel(0, true);
                yield return Wait(1f);
                yield return Shot("04b_settings_low_quality");
                QualitySettings.SetQualityLevel(quality, true);
                QualitySettings.vSyncCount = 0;
                yield return Wait(0.3f);
            }
            yield return Press("Back");
            yield return Wait(0.3f);
            Check("Back closes Settings", !FindAnyObjectByType<MainMenuHud>().ShowingSettings);
            if (_short)
            {
                Finish();
                yield break;
            }

            yield return Press("New Match");
            yield return WaitForScene(GameFlow.MatchScene);
            yield return Wait(1f);
            var match = FindAnyObjectByType<MatchController>();
            var battle = FindAnyObjectByType<BattleController>();
            Check("New Match reaches a working draft", match != null && match.Match != null && match.Match.Phase == MatchPhase.Draft && match.Match.PlayerDraft.Count > 0);
            yield return Shot("05_draft");
            yield return PauseAndLook("draft", "06");
            match.ChooseDraft(0);
            yield return Wait(1f);
            Check("a draft card leads to deployment", match.Match.Phase == MatchPhase.Deployment);
            yield return Shot("07_deployment_corner_button");

            yield return PressEscape();
            Check("Esc pauses deployment", GamePause.IsPaused && Time.timeScale == 0f);
            yield return Shot("08_pause_deployment");
            yield return Press("Settings");
            yield return Wait(0.3f);
            yield return Shot("09_settings_in_game");
            yield return Press("Back");
            yield return Wait(0.2f);
            yield return Press("Resume");
            yield return Wait(0.2f);
            Check("Resume ends the pause", !GamePause.IsPaused && Time.timeScale == 1f);

            match.Ready();
            yield return null;
            Check("Ready starts the battle", match.Match.Phase == MatchPhase.Battle);
            battle.Speed = 2f;
            yield return Wait(2f);
            yield return PressEscape();
            int tick = battle.Simulation.Tick;
            yield return Wait(2f);
            Check("no tick runs while paused in battle", GamePause.IsPaused && battle.Simulation.Tick == tick);
            Check("the battle sounds pause with it", AudioDirector.Instance != null && AudioDirector.Instance.BattlePaused);
            yield return Shot("10_pause_battle");
            yield return PressEscape();
            Check("Esc again resumes", !GamePause.IsPaused);
            Check("the battle sounds come back", AudioDirector.Instance != null && !AudioDirector.Instance.BattlePaused);
            yield return Wait(1f);
            Check("the battle runs on after the pause", battle.Simulation.IsFinished || battle.Simulation.Tick > tick);
            Check("the battle keeps its speed", Mathf.Approximately(battle.Speed, 2f));

            // To the end of the match: any survivor finishes the enemy's base, and if none is left the enemy finishes ours.
            battle.Speed = 16f;
            match.Match.Enemy.BaseHealth = 1;
            match.Match.Player.BaseHealth = 1;
            float limit = Time.realtimeSinceStartup + 120f;
            while (match.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            yield return Wait(0.5f);
            if (match.Match.Phase != MatchPhase.MatchOver)
            {
                yield return Shot("11_result");
                yield return PressEscape();
                yield return Shot("11_pause_result");
                yield return PressEscape();
            }
            else
            {
                yield return PauseAndLook("match over", "11");
                yield return Shot("12_match_over");
                yield return Press("Main Menu");
                yield return WaitForScene(GameFlow.MainMenuScene);
                Check("the end of the match's Main Menu button works", SceneManager.GetActiveScene().name == GameFlow.MainMenuScene);
            }

            if (SceneManager.GetActiveScene().name != GameFlow.MainMenuScene)
            {
                yield return PressEscape();
                yield return Press("Quit to Main Menu");
                yield return Wait(0.3f);
                yield return Shot("12_confirm_leave");
                yield return Press("Yes, leave");
                yield return WaitForScene(GameFlow.MainMenuScene);
                Check("Quit to Main Menu works", SceneManager.GetActiveScene().name == GameFlow.MainMenuScene);
            }
            Check("time runs in the menu after a match", Time.timeScale == 1f && !GamePause.IsPaused);
            yield return Wait(2.5f);
            yield return Shot("13_menu_again");

            yield return Press("Sandbox");
            yield return WaitForScene(GameFlow.SandboxScene);
            yield return Wait(1f);
            Check("the Sandbox opens from the menu", FindAnyObjectByType<SandboxController>() != null);
            Check("its Back button says it goes to the menu", GameFlow.SandboxBackLabel == "Main menu");
            yield return Shot("14_sandbox");
            yield return PressEscape();
            Check("Esc pauses the Sandbox", GamePause.IsPaused);
            yield return Shot("15_pause_sandbox");
            yield return PressEscape();
            GameFlow.LeaveSandbox();
            yield return WaitForScene(GameFlow.MainMenuScene);
            Check("the Sandbox's Back button returns to the menu", SceneManager.GetActiveScene().name == GameFlow.MainMenuScene);

            // Back in by the old road: from the draft to the Sandbox and back to the match.
            GameFlow.NewMatch();
            yield return WaitForScene(GameFlow.MatchScene);
            yield return Wait(0.5f);
            SceneManager.LoadScene(MatchHud.SandboxSceneName);
            yield return WaitForScene(GameFlow.SandboxScene);
            Check("opened from a match, its Back button says so", GameFlow.SandboxBackLabel == "Back to match");
            yield return Wait(0.5f);
            GameFlow.LeaveSandbox();
            yield return WaitForScene(GameFlow.MatchScene);
            Check("and it returns to the match", SceneManager.GetActiveScene().name == GameFlow.MatchScene);

            GameFlow.OpenMainMenu();
            yield return WaitForScene(GameFlow.MainMenuScene);
            yield return Wait(1f);
            Finish();
            yield return Press("Quit");
        }

        /// <summary>Pauses by Esc on this screen, checks and pictures it, and resumes by Esc.</summary>
        private IEnumerator PauseAndLook(string where, string number)
        {
            yield return PressEscape();
            Check("Esc pauses in " + where, GamePause.IsPaused);
            yield return Shot($"{number}_pause_{where.Replace(' ', '_')}");
            yield return PressEscape();
            Check("Esc resumes in " + where, !GamePause.IsPaused);
        }

        private void Finish()
        {
            Debug.Log($"MenuDemo finished: {_problems} problems.");
            if (_short) Application.Quit();
        }

        private void Check(string what, bool ok)
        {
            Debug.Log($"MenuDemo: {(ok ? "ok" : "PROBLEM")}: {what}");
            if (!ok) _problems++;
        }

        private static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

        private IEnumerator WaitForScene(string scene)
        {
            float limit = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().name != scene && Time.realtimeSinceStartup < limit) yield return null;
            yield return null;
            if (SceneManager.GetActiveScene().name != scene) Check("reached " + scene, false);
        }

        private static IEnumerator Press(string label)
        {
            MenuStyle.PressedByTest = label;
            float limit = Time.realtimeSinceStartup + 3f;
            while (MenuStyle.PressedByTest == label && Time.realtimeSinceStartup < limit) yield return null;
            if (MenuStyle.PressedByTest == label)
            {
                MenuStyle.PressedByTest = null;
                Debug.Log("MenuDemo: PROBLEM: found no button called " + label);
            }
            yield return null;
        }

        /// <summary>A real press of Esc, through the Input System. If the window has no focus it never arrives: then it opens the menu directly, and says so.</summary>
        private IEnumerator PressEscape()
        {
            bool before = GamePause.IsPaused;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
            }
            if (GamePause.IsPaused == before)
            {
                Debug.Log("MenuDemo: Esc did not arrive (no keyboard focus?), so the pause menu was called directly.");
                if (before) PauseMenu.Current.Close();
                else PauseMenu.Current.Open();
                yield return null;
            }
            yield return Wait(0.3f);
        }

        private IEnumerator Shot(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            yield return null;
            yield return null;
            yield return Wait(0.15f);
        }
    }
}
