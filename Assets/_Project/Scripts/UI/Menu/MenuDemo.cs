// MenuDemo.cs
// Test tool for built games (M10): walks through the whole game flow with nobody at the keyboard, and saves a picture of each screen.
// It does nothing unless the game is started with "-menuDemo <screenshot folder>". It presses the same buttons a player would (through
// MenuStyle.Button) and sends a real Esc through the Input System: an event posted inside the game, so nothing reaches the PC's real keyboard or mouse. Add "-menuDemoShort" to look only at the menu, settings and the Play page.
//   The menu, three moments apart; Settings and its frame-rate switch; back; the Play page, its panels, stepper and card list, and Esc through them; Faction mode and Start; the draft;
//   a card; Esc in deployment; Settings from there; back; Resume; Ready; the frame-rate counter off and on; Esc in battle, checking that no tick runs while paused and that ticks run again
//   after Resume; the result; a forced end of match and its Main Menu button; the menu again; Sandbox by way of the Play page; Esc; its Back button; an Unlock Skirmish match with
//   settings picked on the panel, its unlock page and Esc there; the menu again; Quit. A button it can't find ends the run at once, as a problem.
// It writes "MenuDemo: ..." lines in the player's log, one for each thing checked, and ends with how many went wrong. It never changes
// a setting, because PlayerPrefs are shared by every build on this PC: in a test run GameSettings holds them in memory, and logs at the end whether the saved ones are as they were.
// With "-menuDemoSounds" it checks the small sounds instead, with no real mouse or keyboard: a pointer of its own goes over the buttons of the menu, Settings and
// the pause menu (it never presses them), and in a match it selects, moves and turns a squad and takes a spell back. A squad with no room to turn must sound the
// error instead, and is then turned from a spot with room, so two checks more are logged in those runs. Add "-audioCapture out.wav" to record it.
// With "-menuDemoUiScale 1.25" it looks at every screen at that UI scale, without saving it.
// With "-menuDemoWatch" it presses nothing: for 90 seconds it logs and pictures every change of scene, pause, settings panel, window or
// setting, so that clicks sent from outside the game (a real mouse, say) can be checked. Settings changed that way last only as long as the run.
// Depends on: GameFlow, GamePause, MatchController, SandboxController, BattleController, PauseMenu, MainMenuHud, MenuStyle, AudioDirector, UiAudio, Input System.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Halobellum.Core;
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
        private const string SoundsFlag = "-menuDemoSounds";
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
                string now = $"scene {SceneManager.GetActiveScene().name}, paused {GamePause.IsPaused}, settings {menu != null && menu.ShowingSettings}, page {(menu != null ? menu.CurrentPage.ToString() : "none")}, mode {(menu != null ? menu.SelectedMode?.ToString() : null)}, " +
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), SoundsFlag) >= 0)
            {
                yield return Sounds();
                Finish();
                Application.Quit();
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

            // The frame-rate counter's switch, held in memory like every setting in a test run.
            yield return Press("Frame rate counter/On");
            Check("the frame-rate switch turns on", GameSettings.ShowFrameRate);
            yield return Wait(0.2f);
            yield return Shot("04a_settings_frame_rate_on");
            yield return Press("Frame rate counter/Off");
            Check("and off", !GameSettings.ShowFrameRate);

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
            yield return PlayPage();
            if (_short)
            {
                Finish();
                yield break;
            }

            yield return Press("Play");
            yield return Press("Faction mode");
            yield return Press("Start");
            yield return WaitForScene(GameFlow.MatchScene);
            yield return Wait(1f);
            var match = FindAnyObjectByType<MatchController>();
            var battle = FindAnyObjectByType<BattleController>();
            Check("Faction mode starts a Faction match", match != null && match.Match != null && match.Match.Rules.Mode == MatchMode.Faction);
            Check("Start reaches a working draft", match != null && match.Match != null && match.Match.Phase == MatchPhase.Draft && match.Match.PlayerDraft.Count > 0);
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
            GameSettings.ShowFrameRate = false;
            yield return Wait(2f);
            yield return Shot("09b_battle_frame_rate_off");
            GameSettings.ShowFrameRate = true;
            yield return Wait(0.5f);
            yield return Shot("09c_battle_frame_rate_on");
            GameSettings.ShowFrameRate = false;
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

            yield return Press("Play");
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

            // Back in by the old road: from a match to the Sandbox and back to the match.
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
            yield return SkirmishMatch();

            GameFlow.OpenMainMenu();
            yield return WaitForScene(GameFlow.MainMenuScene);
            yield return Wait(1f);
            var again = FindAnyObjectByType<MainMenuHud>();
            Check("the card filter is back at All cards when the menu loads", again.Cards == CardFilter.AllCards);
            Check("no mode is selected when the menu loads", again.CurrentPage == MainMenuHud.Page.Main && again.SelectedMode == null);
            Check("the base health and difficulty are remembered", GameSettings.SkirmishBaseHealth == 1600 && GameSettings.Difficulty == AiDifficulty.Hard);
            Finish();
            yield return Press("Quit");
        }

        /// <summary>The Play page's own controls, through its buttons and Esc: it ends back on the first page.</summary>
        private IEnumerator PlayPage()
        {
            InputSettings.BackgroundBehavior kept = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; // So that a posted Esc counts in a window without focus.
            var menu = FindAnyObjectByType<MainMenuHud>();

            yield return PostEscape();
            Check("Esc on the first page does nothing", menu.CurrentPage == MainMenuHud.Page.Main && !menu.IsLeaving);
            yield return Press("Play");
            Check("Play opens its page", menu.CurrentPage == MainMenuHud.Page.Play && menu.SelectedMode == null);
            yield return Wait(0.2f);
            yield return Shot("04c_play_page");
            yield return Press("Faction mode");
            Check("Faction mode shows its panel", menu.SelectedMode == MatchMode.Faction);
            yield return Wait(0.2f);
            yield return Shot("04d_faction_mode");

            yield return Press("Unlock Skirmish mode");
            Check("Unlock Skirmish mode shows its panel", menu.SelectedMode == MatchMode.UnlockSkirmish);
            AiDifficulty difficulty = GameSettings.Difficulty;
            int health = GameSettings.SkirmishBaseHealth;
            GameSettings.SkirmishBaseHealth = 1500;
            yield return Press("Base health/>");
            Check("the stepper goes up by 100", GameSettings.SkirmishBaseHealth == 1600);
            yield return Press("Base health/<");
            yield return Press("Base health/<");
            Check("and down by 100", GameSettings.SkirmishBaseHealth == 1400);
            GameSettings.SkirmishBaseHealth = MatchSetup.LeastBaseHealth;
            yield return Press("Base health/<");
            Check("it stops at the least", GameSettings.SkirmishBaseHealth == MatchSetup.LeastBaseHealth);
            GameSettings.SkirmishBaseHealth = MatchSetup.MostBaseHealth;
            yield return Press("Base health/>");
            Check("and at the most", GameSettings.SkirmishBaseHealth == MatchSetup.MostBaseHealth);
            GameSettings.SkirmishBaseHealth = 1500;
            yield return Press("Difficulty/Hard");
            Check("the panel sets the difficulty", GameSettings.Difficulty == AiDifficulty.Hard);
            yield return Wait(0.2f);
            yield return Shot("04e_skirmish_mode");

            Check("the card filter starts at All cards", menu.Cards == CardFilter.AllCards);
            yield return Press("Cards/open");
            Check("its box opens the list", menu.CardListOpen);
            yield return Wait(0.2f);
            yield return Shot("04f_card_list");
            yield return PressNothing("Start");
            yield return PressNothing("Back");
            yield return PressNothing("Difficulty/Easy");
            yield return Press("Cards/outside");
            Check("a click elsewhere closes the list and changes nothing", !menu.CardListOpen && menu.Cards == CardFilter.AllCards && GameSettings.Difficulty == AiDifficulty.Hard);

            yield return Press("Cards/open");
            yield return Press("Cards/open");
            Check("its box closes the list again", !menu.CardListOpen);
            yield return Press("Cards/open");
            yield return Press("Cards/No reinforcement cards");
            Check("a line of the list picks it and closes the list", !menu.CardListOpen && menu.Cards == CardFilter.NoReinforcements);
            yield return Wait(0.2f);
            yield return Shot("04g_card_list_picked");

            yield return Press("Cards/open");
            yield return PostEscape();
            Check("Esc closes the list first, and stays on the page", !menu.CardListOpen && menu.CurrentPage == MainMenuHud.Page.Play && menu.SelectedMode == MatchMode.UnlockSkirmish);
            yield return PostEscape();
            Check("the next Esc goes back to the first page", menu.CurrentPage == MainMenuHud.Page.Main && menu.SelectedMode == null);
            Check("what was picked is kept until the menu loads again", menu.Cards == CardFilter.NoReinforcements);
            yield return Press("Play");
            yield return Press("Back");
            Check("Back goes to the first page", menu.CurrentPage == MainMenuHud.Page.Main);

            // Back to as it was, so that the tour goes on from the player's own settings.
            GameSettings.SkirmishBaseHealth = health;
            GameSettings.Difficulty = difficulty;
            InputSystem.settings.backgroundBehavior = kept;
        }

        /// <summary>An Unlock Skirmish match with settings picked on the panel, as far as its unlock page, and Esc there.</summary>
        private IEnumerator SkirmishMatch()
        {
            InputSettings.BackgroundBehavior kept = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            yield return Press("Play");
            yield return Press("Unlock Skirmish mode");
            GameSettings.SkirmishBaseHealth = 1500;
            yield return Press("Base health/>");
            yield return Press("Difficulty/Hard");
            yield return Press("Cards/open");
            yield return Press("Cards/Only reinforcement cards");
            yield return Wait(0.2f);
            yield return Shot("20_skirmish_panel");
            yield return Press("Start");
            yield return WaitForScene(GameFlow.MatchScene);
            yield return Wait(1f);

            var match = FindAnyObjectByType<MatchController>();
            MatchRules rules = match.Match.Rules;
            Check("Start reaches an Unlock Skirmish draft", rules.Mode == MatchMode.UnlockSkirmish && match.Match.Phase == MatchPhase.Draft);
            Check($"with the health picked (it has {rules.BaseHealth})", rules.BaseHealth == 1600);
            Check($"with the card filter picked (it has {rules.Cards})", rules.Cards == CardFilter.OnlyReinforcements);
            Check($"with the difficulty picked (it has {match.Difficulty})", match.Difficulty == AiDifficulty.Hard);
            yield return Shot("21_skirmish_draft");
            Check("the first card can be chosen", match.ChooseDraft(0).Ok);
            yield return Wait(1f);
            yield return Shot("22_skirmish_deployment");

            var hud = FindAnyObjectByType<MatchHud>();
            yield return Press(MatchHud.UnlockUnitsLabel);
            yield return Wait(0.2f);
            Check("the Unlock units button opens the page", hud.IsUnlockPageOpen);
            yield return Shot("23_skirmish_unlock_page");
            yield return PostEscape();
            Check("Esc closes the unlock page", !hud.IsUnlockPageOpen);
            Check("and doesn't pause", !GamePause.IsPaused);
            yield return Shot("24_skirmish_page_closed");
            yield return PostEscape();
            Check("the next Esc pauses", GamePause.IsPaused);
            yield return PostEscape();
            Check("and the next one resumes", !GamePause.IsPaused);

            InputSystem.settings.backgroundBehavior = kept;
        }

        /// <summary>Goes through what makes a small sound, and counts what is heard.</summary>
        private IEnumerator Sounds()
        {
            var heard = new List<string>();
            var buttons = new Dictionary<string, Rect>();
            AudioDirector.Instance.SlotPlayed += heard.Add;
            MenuStyle.ButtonDrawnForTest += (key, rect) => buttons[key] = rect;
            int Hovers() => heard.FindAll(slot => slot.EndsWith("_hover", StringComparison.Ordinal)).Count;

            // A pointer in a corner, standing still, while the menu opens.
            UiAudio.PointerByTest = Vector2.one;
            yield return Wait(3f);
            Check("no hover sound with the pointer standing still", Hovers() == 0);
            yield return Sweep("the main menu", buttons, new[] { "Play", "Settings", "Quit" }, Hovers);
            yield return Shot("s1_menu");

            // Resting on a button: not heard again, not even when the scene it is in opens again under it.
            UiAudio.PointerByTest = buttons["Settings"].center;
            yield return Wait(0.3f);
            int before = Hovers();
            yield return Wait(2f);
            Check("a pointer resting on a button is heard no more", Hovers() == before);
            GameFlow.OpenMainMenu();
            yield return Wait(0.5f);
            yield return WaitForScene(GameFlow.MainMenuScene);
            yield return Wait(2.5f);
            Check("nothing is heard when the menu opens under a pointer on a button", Hovers() == before);

            yield return Press("Settings");
            yield return Wait(0.5f);
            Check("nothing is heard when Settings opens under a pointer", Hovers() == before);
            buttons.Clear();
            yield return Wait(0.2f);
            // Only hovers: Reset to defaults is never pressed.
            yield return Sweep("Settings", buttons, new[] { "Back", "Reset to defaults", "VSync/On", "VSync/Off" }, Hovers);
            yield return Shot("s2_settings");
            UiAudio.PointerByTest = Vector2.one;
            yield return Wait(0.1f);
            yield return Press("Back");
            yield return Wait(0.3f);

            yield return Press("Play");
            yield return Press("Unlock Skirmish mode");
            buttons.Clear();
            yield return Wait(0.2f);
            yield return Sweep("the Play page", buttons, new[] { "Faction mode", "Unlock Skirmish mode", "Sandbox", "Back", "Difficulty/Normal", "Base health/>", "Cards/open", "Start" }, Hovers);
            yield return Shot("s2b_play_page");
            UiAudio.PointerByTest = Vector2.one;
            yield return Wait(0.1f);
            yield return Press("Faction mode");
            yield return Press("Start");
            yield return WaitForScene(GameFlow.MatchScene);
            yield return Wait(1f);
            var match = FindAnyObjectByType<MatchController>();
            match.ChooseDraft(0);
            yield return Wait(1f);
            Match game = match.Match;
            UnitStats unit = game.Player.Roster.Find(u => game.Player.Unlocked.Contains(u) && u.Cost <= game.Player.Credits);
            yield return Heard("buying", heard, () => match.Buy(unit), "_buy", 1);
            while (match.IsDropping) yield return null;
            yield return Wait(0.5f);
            Check("a bought squad being selected as it lands is silent", !heard.Exists(slot => slot.EndsWith("_click", StringComparison.Ordinal)));
            var selection = match.GetComponent<SquadSelection>();
            int selected = selection.SelectedSquadId;
            selection.Select(-1);
            yield return Heard("selecting a squad", heard, () => selection.SelectByPlayer(selected), "_click", 1);
            // The draft is random, so the bought squad may have landed where the squads beside it leave no room to turn. The game is right
            // to refuse that and sound its error, so that is what is checked then, and the turn is tried again from a spot with room.
            PlacedSquad squad = match.SelectedPlacedSquad;
            bool roomToTurn = HasRoomToTurn(game.Player, squad, game.CombatRules);
            if (!roomToTurn)
            {
                Debug.Log($"MenuDemo: {squad.Type.Name} has no room to turn at column {squad.Column}, row {squad.Row}, so its turn should be refused.");
                yield return Heard("turning a squad with no room", heard, () => match.ToggleRotation(), "_error", 1);
                Debug.Log($"MenuDemo: the game said: {match.Message}");
                roomToTurn = FindRoomToTurn(game.Player, squad, game.CombatRules, out int roomyColumn, out int roomyRow);
                Check("a spot with room to turn was found", roomToTurn);
                if (roomToTurn)
                {
                    match.BeginMoving(squad);
                    match.PlaceAt(roomyColumn, roomyRow);
                }
            }
            if (roomToTurn) yield return Heard("turning a squad", heard, () => match.ToggleRotation(), "_click", 1);
            match.BeginMoving(squad);
            yield return Heard("turning the ghost", heard, () => match.ToggleRotation(), "_click", 1);
            match.ToggleRotation();
            yield return Wait(0.3f);
            int column = -1, row = -1;
            for (int c = 0; c < 40 && column < 0; c++)
            {
                for (int r = 0; r < 40 && column < 0; r++)
                {
                    if ((c != squad.Column || r != squad.Row) && match.CanPlaceAt(c, r))
                    {
                        column = c;
                        row = r;
                    }
                }
            }
            Check("a spot to move to was found", column >= 0);
            yield return Heard("moving a squad", heard, () => match.PlaceAt(column, row), "_skip", 1);
            yield return Shot("s3_moved");

            // A spell of its own, for this match only.
            var spell = new OwnedSpell { Id = 9001, Stats = new SpellStats { Name = "Sound check" }, IsPlaced = true };
            game.Player.Spells.Add(spell);
            yield return Heard("taking a spell back", heard, () => match.UnplaceSpell(spell), "_sell", 1);
            game.Player.Spells.Remove(spell);

            PauseMenu.Current.Open();
            yield return Wait(0.4f);
            buttons.Clear();
            yield return Wait(0.2f);
            yield return Sweep("the pause menu", buttons, new[] { "Resume", "Settings", "Quit to Main Menu", "Quit Game" }, Hovers);
            yield return Shot("s4_pause");
            UiAudio.PointerByTest = null;
            PauseMenu.Current.Close();
            yield return Wait(0.3f);
        }

        /// <summary>True if this squad can be turned where it stands.</summary>
        public static bool HasRoomToTurn(PlayerState player, PlacedSquad squad, CombatRules rules) =>
            DeploymentGrid.CanPlace(player, squad.Type, squad.Column, squad.Row, !squad.Rotated90, rules, squad.Id);

        /// <summary>The first spot where this squad could stand and also turn. Returns false if the zone has none.</summary>
        public static bool FindRoomToTurn(PlayerState player, PlacedSquad squad, CombatRules rules, out int column, out int row)
        {
            for (row = 0; row < rules.DeploymentDepthSquares; row++)
            {
                for (column = 0; column < rules.ArenaWidthSquares; column++)
                {
                    if (DeploymentGrid.CanPlace(player, squad.Type, column, row, false, rules, squad.Id)
                        && DeploymentGrid.CanPlace(player, squad.Type, column, row, true, rules, squad.Id)) return true;
                }
            }
            column = row = -1;
            return false;
        }

        /// <summary>Moves the pointer onto each of these buttons in turn: each must be heard exactly once.</summary>
        private IEnumerator Sweep(string where, Dictionary<string, Rect> buttons, string[] keys, Func<int> hovers)
        {
            foreach (string key in keys)
            {
                if (!buttons.TryGetValue(key, out Rect rect))
                {
                    Check($"{key} is drawn on {where}", false);
                    continue;
                }
                UiAudio.PointerByTest = Vector2.one;
                yield return Wait(0.15f);
                int before = hovers();
                UiAudio.PointerByTest = rect.center;
                yield return Wait(0.5f);
                Check($"going over {key} on {where} is heard once (heard {hovers() - before})", hovers() - before == 1);
            }
        }

        /// <summary>Does something and checks how many times a slot with this ending is heard for it.</summary>
        private IEnumerator Heard(string what, List<string> heard, Action action, string ending, int count)
        {
            yield return Wait(0.7f);
            heard.Clear();
            action();
            yield return Wait(0.3f);
            int found = heard.FindAll(slot => slot.EndsWith(ending, StringComparison.Ordinal)).Count;
            Check($"{what} sounds {ending} (heard {string.Join(", ", heard)})", found == count);
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

        private IEnumerator Press(string label)
        {
            MenuStyle.PressedByTest = label;
            float limit = Time.realtimeSinceStartup + 3f;
            while (MenuStyle.PressedByTest == label && Time.realtimeSinceStartup < limit) yield return null;
            if (MenuStyle.PressedByTest == label)
            {
                // The tour can't go on from a screen it didn't reach, so it ends here rather than in a timeout further on.
                MenuStyle.PressedByTest = null;
                Check("found a button called " + label, false);
                Finish();
                Application.Quit();
                StopAllCoroutines();
                yield break;
            }
            yield return null;
        }

        /// <summary>Tries a button that should not take a press now, because it is drawn greyed out behind the card list.</summary>
        private IEnumerator PressNothing(string label)
        {
            MenuStyle.PressedByTest = label;
            yield return Wait(0.3f);
            Check($"{label} takes no press while the list is open", MenuStyle.PressedByTest == label);
            MenuStyle.PressedByTest = null;
        }

        /// <summary>A press and release of Esc, as an event posted to the Input System inside the game.</summary>
        private static IEnumerator PostEscape()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) yield break;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        /// <summary>A real press of Esc, through the Input System. If the window has no focus it never arrives: then it opens the menu directly, and says so.</summary>
        private IEnumerator PressEscape()
        {
            bool before = GamePause.IsPaused;
            yield return PostEscape();
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
