// AudioFlowTests.cs
// Sound at the edges of a battle (M10): the game's pause holds the battle's sounds, leaving from the pause leaves nothing behind, a
// stinger doesn't follow the player off its screen, the Sandbox's sound goes by the armies really placed, what is done to a squad each round is heard
// softly, and menu buttons sound when the pointer goes over them (with a test's pointer, never the real mouse).
// These can only check what is asked for and what is playing, not how it sounds. The tests run in batch mode, where the room stays silent.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class AudioFlowTests
    {
        private readonly List<string> _played = new List<string>();

        private static AudioDirector Director => AudioDirector.Instance;

        private void Heard(string slot) => _played.Add(slot);

        [TearDown]
        public void TearDown()
        {
            GamePause.Resume();
            UiAudio.PointerByTest = null;
            MenuStyle.ButtonDrawnForTest -= NoteButton;
            if (Director != null) Director.SlotPlayed -= Heard;
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Director.SlotPlayed -= Heard;
            Director.SlotPlayed += Heard;
            _played.Clear();
        }

        private static UnitStats UnitNamed(SandboxController sandbox, string name)
        {
            SandboxController.SandboxUnit unit = sandbox.Units.Find(u => u.Stats.Name == name);
            Assert.IsNotNull(unit.Stats, name);
            return unit.Stats;
        }

        /// <summary>A Sandbox battle of tanks, running with its engines heard.</summary>
        private IEnumerator TankBattle()
        {
            yield return Load("Sandbox");
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Scorpion")).Ok);
            Assert.IsTrue(sandbox.AddSquad(Team.B, UnitNamed(sandbox, "Scorpion")).Ok);
            yield return null;
            sandbox.Run();
            float deadline = Time.realtimeSinceStartup + 20f;
            while (!_played.Contains("sfx_hum_engine") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Contains("sfx_hum_engine", _played);
        }

        private int Count(string slot) => _played.FindAll(s => s == slot).Count;

        /// <summary>How loud a UI sound was asked to be, of the player's UI volume.</summary>
        private static float UiLevelOf(string slot)
        {
            foreach (AudioSource source in Director.GetComponents<AudioSource>())
            {
                if (source.isPlaying && source.clip != null && source.clip.name == slot) return source.volume / (Director.VolumeMaster * Director.VolumeUi);
            }
            Assert.Fail(slot + " is not playing.");
            return 0f;
        }

        /// <summary>A match in deployment with one squad bought and landed. Returns the squad's faction key in key.</summary>
        private IEnumerator MatchWithASquad(string faction)
        {
            yield return Load("Match");
            var match = Object.FindAnyObjectByType<MatchController>();
            for (int seed = 1; seed < 200 && _key == null; seed++)
            {
                match.NewMatch(seed);
                for (int i = 0; i < match.Match.PlayerDraft.Count && _key == null; i++)
                {
                    if (match.Match.PlayerDraft[i].Faction.Name != faction) continue;
                    Assert.IsTrue(match.ChooseDraft(i).Ok);
                    _key = AudioSlots.FactionKey(faction);
                }
            }
            PlayerState player = match.Match.Player;
            Assert.IsTrue(match.Buy(player.Roster.Find(u => player.Unlocked.Contains(u) && u.Cost <= player.Credits)).Ok);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (match.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
        }

        private string _key;

        [SetUp]
        public void ForgetTheFaction() => _key = null;

        [UnityTest]
        public IEnumerator SelectingAndMovingASquad_AreHeardSoftly_AndNothingElseIs()
        {
            yield return MatchWithASquad(BattleFactions.Unsc);
            var match = Object.FindAnyObjectByType<MatchController>();
            var selection = match.GetComponent<SquadSelection>();
            Assert.AreEqual(0, Count($"ui_{_key}_click"), "A bought squad is selected when it lands: that isn't the player's doing.");
            int squad = selection.SelectedSquadId;
            Assert.GreaterOrEqual(squad, 0);

            selection.Select(-1);
            selection.Select(squad);
            Assert.AreEqual(0, Count($"ui_{_key}_click"), "Select alone also runs for each preview rebuilt.");

            selection.Select(-1);
            selection.SelectByPlayer(squad);
            Assert.AreEqual(1, Count($"ui_{_key}_click"), "The player's click on a squad.");
            Assert.Less(UiLevelOf($"ui_{_key}_click"), 1f);
            selection.SelectByPlayer(squad);
            Assert.AreEqual(1, Count($"ui_{_key}_click"), "Clicking it again changes nothing.");
            selection.SelectByPlayer(-1);
            Assert.AreEqual(1, Count($"ui_{_key}_click"), "Clearing it is silent.");
            selection.SelectByPlayer(squad);
            _played.Clear();

            // Turning where it stands, which rebuilds the preview and selects it again: one sound, no more.
            match.ToggleRotation();
            yield return null;
            Assert.AreEqual(1, Count($"ui_{_key}_click"), match.Message);
            Assert.AreEqual(1, _played.Count, string.Join(", ", _played));

            // Moving: turning the ghost, then setting it down.
            _played.Clear();
            PlacedSquad placed = match.SelectedPlacedSquad;
            match.BeginMoving(placed);
            Assert.IsEmpty(_played, "Picking it up has its button's click only.");
            string message = match.Message;
            match.ToggleRotation();
            Assert.AreEqual(1, Count($"ui_{_key}_click"), "The ghost turning.");
            Assert.AreEqual(message, match.Message, "The placing message stays.");
            match.ToggleRotation();
            _played.Clear();
            Assert.IsFalse(match.PlaceAt(-50, -50).Ok);
            Assert.AreEqual(1, Count($"ui_{_key}_error"), "A failure is still heard.");
            _played.Clear();
            int column = -1, row = -1;
            for (int c = 0; c < 40 && column < 0; c++)
            {
                for (int r = 0; r < 40 && column < 0; r++)
                {
                    if ((c != placed.Column || r != placed.Row) && match.CanPlaceAt(c, r))
                    {
                        column = c;
                        row = r;
                    }
                }
            }
            Assert.GreaterOrEqual(column, 0);
            Assert.IsTrue(match.PlaceAt(column, row).Ok, match.Message);
            Assert.AreEqual(1, Count($"ui_{_key}_skip"), string.Join(", ", _played));
            Assert.AreEqual(1, _played.Count, "Only that: " + string.Join(", ", _played));
            Assert.Less(UiLevelOf($"ui_{_key}_skip"), 1f);
        }

        /// <summary>The first card of the draft and the first squad the player can buy, as -menuDemoSounds does, without drawing them.</summary>
        private static PlacedSquad DraftAndBuy(MatchController match, int seed)
        {
            match.NewMatch(seed);
            Match game = match.Match;
            Assert.IsTrue(game.ChooseDraft(0).Ok, $"Seed {seed}.");
            PlayerState player = game.Player;
            UnitStats unit = player.Roster.Find(u => player.Unlocked.Contains(u) && u.Cost <= player.Credits);
            Assert.IsNotNull(unit, $"Seed {seed}: nothing to buy.");
            Assert.IsTrue(game.Buy(Team.A, unit).Ok, $"Seed {seed}.");
            return player.Squads[player.Squads.Count - 1];
        }

        // What -menuDemoSounds meets in some of its runs: the draft is random, and the bought squad can land with no room to turn. The game
        // must refuse that and sound its error, and the tool must find a spot where the turn works, whichever draft it is.
        [UnityTest]
        public IEnumerator TurningWithNoRoom_SoundsTheError_AndTheToolFindsRoom()
        {
            yield return Load("Match");
            var match = Object.FindAnyObjectByType<MatchController>();
            int blockedSeed = -1;
            for (int seed = 1; seed <= 200; seed++)
            {
                PlacedSquad bought = DraftAndBuy(match, seed);
                Match game = match.Match;
                if (MenuDemo.HasRoomToTurn(game.Player, bought, game.CombatRules)) continue;
                if (blockedSeed < 0) blockedSeed = seed;
                Assert.IsTrue(MenuDemo.FindRoomToTurn(game.Player, bought, game.CombatRules, out _, out _), $"Seed {seed}: {bought.Type.Name} has no spot to turn in.");
            }
            if (blockedSeed < 0) Assert.Inconclusive("No draft in 200 leaves the bought squad without room to turn, so there is nothing to check.");

            match.NewMatch(blockedSeed);
            Assert.IsTrue(match.ChooseDraft(0).Ok, match.Message);
            PlayerState player = match.Match.Player;
            Assert.IsTrue(match.Buy(player.Roster.Find(u => player.Unlocked.Contains(u) && u.Cost <= player.Credits)).Ok, match.Message);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (match.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            string key = AudioSlots.FactionKey(player.Faction.Name);

            PlacedSquad squad = match.SelectedPlacedSquad;
            Assert.IsNotNull(squad, "A bought squad is selected when it lands.");
            bool rotated = squad.Rotated90;
            _played.Clear();
            match.ToggleRotation();
            Assert.AreEqual(1, Count($"ui_{key}_error"), string.Join(", ", _played));
            Assert.AreEqual(0, Count($"ui_{key}_click"), string.Join(", ", _played));
            Assert.AreEqual(rotated, squad.Rotated90, "A refused turn changes nothing.");

            Assert.IsTrue(MenuDemo.FindRoomToTurn(player, squad, match.Match.CombatRules, out int column, out int row));
            match.BeginMoving(squad);
            Assert.IsTrue(match.PlaceAt(column, row).Ok, match.Message);
            _played.Clear();
            match.ToggleRotation();
            yield return null;
            Assert.AreEqual(1, Count($"ui_{key}_click"), match.Message);
            Assert.AreEqual(0, Count($"ui_{key}_error"), match.Message);
            Assert.AreNotEqual(rotated, squad.Rotated90, "From there it does turn.");
        }

        [UnityTest]
        public IEnumerator TakingASpellBack_IsHeardSoftly_AndEachFactionHasItsOwn([Values("UNSC", "Covenant", "Flood")] string faction)
        {
            yield return MatchWithASquad(faction);
            var match = Object.FindAnyObjectByType<MatchController>();
            var spell = new OwnedSpell { Id = 9001, Stats = new SpellStats { Name = "Test" }, IsPlaced = true };
            match.Match.Player.Spells.Add(spell);
            _played.Clear();
            Assert.IsTrue(match.UnplaceSpell(spell).Ok, match.Message);
            Assert.AreEqual(1, Count($"ui_{_key}_sell"), string.Join(", ", _played));
            Assert.Less(UiLevelOf($"ui_{_key}_sell"), 1f);
            Assert.IsFalse(spell.IsPlaced);
        }

        [UnityTest]
        public IEnumerator Sandbox_MovingAndTurning_SoundAsInAMatch()
        {
            yield return Load("Sandbox");
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Marine")).Ok);
            yield return null;
            string key = AudioSlots.FactionKey(BattleFactions.TeamA);
            _played.Clear();

            PlacedSquad squad = sandbox.ArmyA.Squads[0];
            sandbox.BeginMoving(squad);
            sandbox.ToggleRotation();
            Assert.AreEqual(1, Count($"ui_{key}_click"), "The ghost turning.");
            Assert.IsFalse(sandbox.PlaceAt(-50, -50).Ok);
            Assert.AreEqual(1, Count($"ui_{key}_error"), "A spot it can't go to.");
            _played.Clear();
            int column = -1, row = -1;
            for (int c = 0; c < 40 && column < 0; c++)
            {
                for (int r = 0; r < 40 && column < 0; r++)
                {
                    if ((c != squad.Column || r != squad.Row) && sandbox.CanPlaceAt(c, r))
                    {
                        column = c;
                        row = r;
                    }
                }
            }
            Assert.IsTrue(sandbox.PlaceAt(column, row).Ok, sandbox.Message);
            Assert.AreEqual(1, Count($"ui_{key}_skip"), string.Join(", ", _played));
            yield return null;
            Assert.AreEqual(0, Count("sfx_drop_in"), "Moved, not new.");

            // A new squad placed by hand drops in, and doesn't sound moved as well. (After the shortest gap between two drops, which has its own limit.)
            yield return new WaitForSecondsRealtime(0.2f);
            _played.Clear();
            sandbox.BeginPlacing(UnitNamed(sandbox, "Marine"));
            bool placed = false;
            for (int c = 0; c < 40 && !placed; c++)
            {
                for (int r = 0; r < 40 && !placed; r++)
                {
                    if (sandbox.CanPlaceAt(c, r)) placed = sandbox.PlaceAt(c, r).Ok;
                }
            }
            sandbox.CancelPlacement();
            yield return null;
            Assert.IsTrue(placed);
            Assert.AreEqual(1, Count("sfx_drop_in"));
            Assert.AreEqual(0, Count($"ui_{key}_skip"));
        }

        [UnityTest]
        public IEnumerator AScarabsBeam_IsHeard_WhileHunterCaptainsFireTheirLasers()
        {
            yield return Load("Sandbox");
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Scarab")).Ok);
            for (int i = 0; i < 4; i++) Assert.IsTrue(sandbox.AddSquad(Team.B, UnitNamed(sandbox, "Hunter Captain")).Ok);
            yield return null;
            Object.FindAnyObjectByType<BattleController>().Speed = 3f;
            sandbox.Run();
            _played.Clear();

            // A beam started after the lasers have begun, which is when it used to find their group full.
            float deadline = Time.realtimeSinceStartup + 60f;
            bool heard = false;
            while (!heard && Time.realtimeSinceStartup < deadline)
            {
                int laser = _played.IndexOf("sfx_fire_laser");
                heard = laser >= 0 && _played.LastIndexOf("sfx_fire_beam") > laser;
                yield return null;
            }
            Assert.Greater(Count("sfx_fire_laser"), 3, "The lasers should be firing.");
            Assert.IsTrue(heard, "The beam should be heard over them.");
            Object.FindAnyObjectByType<BattleController>().Speed = 1f;
        }

        private readonly Dictionary<string, Rect> _buttons = new Dictionary<string, Rect>();

        private void NoteButton(string key, Rect rect) => _buttons[key] = rect;

        private int Hovers => _played.FindAll(s => s.EndsWith("_hover", System.StringComparison.Ordinal)).Count;

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator MenuButtons_SoundOnceWhenThePointerMovesOntoThem_AndNeverOtherwise()
        {
            UiAudio.PointerByTest = Vector2.one;
            MenuStyle.ButtonDrawnForTest += NoteButton;
            _buttons.Clear();
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            Director.SlotPlayed -= Heard;
            Director.SlotPlayed += Heard;
            _played.Clear();
            yield return Frames(30);
            Assert.AreEqual(0, Hovers, "A pointer standing still.");
            // A batch run draws no IMGUI at all, so this says something only in the Editor's Test Runner. In a built game, -menuDemo -menuDemoSounds checks the same on real screens.
            if (Application.isBatchMode && _buttons.Count == 0) Assert.Ignore("Batch mode draws no IMGUI.");
            Assert.IsTrue(_buttons.ContainsKey("Sandbox") && _buttons.ContainsKey("Settings"), "The menu should have drawn its buttons.");

            UiAudio.PointerByTest = _buttons["Sandbox"].center;
            yield return Frames(3);
            Assert.AreEqual(1, Hovers, "It moved onto a button.");
            yield return Frames(120);
            Assert.AreEqual(1, Hovers, "Resting on it, not every frame.");
            Assert.AreEqual(1, _played.Count, string.Join(", ", _played));

            UiAudio.PointerByTest = _buttons["Settings"].center;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(2, Hovers, "On to the next one.");

            // The scene opens again under the pointer, which is resting on a button.
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return Frames(60);
            Assert.AreEqual(2, Hovers, "The scene opening under it makes no sound.");
        }

        [UnityTest]
        public IEnumerator TheGamesPause_HoldsTheBattlesSounds_AndResumeBringsThemBack()
        {
            yield return TankBattle();
            Assert.IsFalse(Director.BattlePaused);
            Assert.IsTrue(Director.AnyHumPlaying, "The Scorpions' engines.");

            GamePause.Pause();
            yield return null;
            yield return null;
            Assert.IsTrue(Director.BattlePaused, "Esc pauses without touching the battle's speed.");
            Assert.IsFalse(Director.AnyHumPlaying, "The engines stop with the battle.");

            _played.Clear();
            Director.PlaySfx(AudioCatalog.ExplosionSmall);
            Assert.IsEmpty(_played, "Nothing new starts while paused.");
            Director.PlayUi(UiSound.Click);
            Assert.AreEqual(1, _played.Count, "The pause menu's own sounds still play.");

            GamePause.Resume();
            yield return null;
            yield return null;
            Assert.IsFalse(Director.BattlePaused);
            Assert.IsTrue(Director.AnyHumPlaying, "The engines come back.");
        }

        [UnityTest]
        public IEnumerator LeavingFromThePause_LeavesNoSoundBehind()
        {
            yield return TankBattle();
            GamePause.Pause();
            yield return null;
            yield return null;
            Assert.IsTrue(Director.BattlePaused);

            _played.Clear();
            GameFlow.OpenMainMenu();
            yield return null;
            yield return null;
            Assert.IsFalse(Director.BattlePaused);
            Assert.AreEqual(AudioSlots.MainMenuMusic, Director.Music.CurrentSlot);
            for (int i = 0; i < 20; i++)
            {
                Assert.IsFalse(Director.AnyHumPlaying, "An engine held by the pause must not start again over the menu.");
                yield return null;
            }
            Assert.IsFalse(_played.Contains("sfx_hum_engine"));
        }

        [UnityTest]
        public IEnumerator AStinger_StopsWhenItsSceneIsLeft_AndTheMenuMusicComesBack()
        {
            yield return Load("Match");
            MusicDeck music = Director.Music;
            music.PlayStinger(AudioSlots.Stinger(BattleFactions.Covenant, true));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (music.Duck > 0.25f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.LessOrEqual(music.Duck, 0.25f, "The track ducks under the stinger.");

            yield return Load(AudioSlots.MainMenuScene);
            Assert.IsFalse(music.StingerOn);
            deadline = Time.realtimeSinceStartup + 2f;
            while (music.Duck < 1f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1f, music.Duck, "The menu's music is back up well before the 8 seconds of the stinger.");
        }

        [UnityTest]
        public IEnumerator AStinger_StopsOnANewMatchInTheSameScene()
        {
            yield return Load("Match");
            var controller = Object.FindAnyObjectByType<MatchController>();
            var audio = controller.GetComponent<MatchAudio>();
            // Getting to the end of a real match takes minutes; MatchSceneTests does that. This is the moment it ends.
            typeof(MatchAudio).GetField("_lastPhase", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(audio, MatchPhase.MatchOver);
            Director.Music.PlayStinger(AudioSlots.Stinger(BattleFactions.Unsc, false));
            Assert.IsTrue(Director.Music.StingerOn);

            controller.NewMatch();
            Assert.IsFalse(Director.Music.StingerOn);
        }

        [UnityTest]
        public IEnumerator Sandbox_GoesByTheArmies_NotByWhatTheLastMatchLeft()
        {
            BattleFactions.Set(BattleFactions.Covenant, BattleFactions.Flood); // What a match might leave behind.
            yield return Load("Sandbox");
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            Assert.AreEqual(BattleFactions.Unsc, BattleFactions.TeamA, "An empty side has the default.");
            Assert.AreEqual(BattleFactions.Covenant, BattleFactions.TeamB);
            Assert.AreEqual("music_unsc_deployment", Director.Music.CurrentSlot);

            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Combat Form")).Ok);
            Assert.AreEqual(BattleFactions.Flood, BattleFactions.TeamA);
            Assert.AreEqual("music_flood_deployment", Director.Music.CurrentSlot);
            Assert.AreEqual(BattleFactions.Flood, Director.UiFaction);

            // A sub-faction's unit counts for its faction, and placeholder units for none.
            Assert.IsTrue(sandbox.AddSquad(Team.B, UnitNamed(sandbox, "Spartan Sniper")).Ok);
            Assert.AreEqual(BattleFactions.Unsc, BattleFactions.TeamB);
            sandbox.Clear(Team.B);
            Assert.IsTrue(sandbox.AddSquad(Team.B, UnitNamed(sandbox, "Chopper")).Ok);
            Assert.AreEqual(BattleFactions.Covenant, BattleFactions.TeamB);
            Assert.IsTrue(sandbox.AddSquad(Team.B, UnitNamed(sandbox, "Placeholder Infantry")).Ok);
            Assert.IsTrue(sandbox.AddSquad(Team.B, UnitNamed(sandbox, "Placeholder Infantry")).Ok);
            Assert.AreEqual(BattleFactions.Covenant, BattleFactions.TeamB);

            // A copy over the other side can keep the count of squads the same: the faction must still follow.
            sandbox.CopyToOtherSide(Team.A);
            Assert.AreEqual(BattleFactions.Flood, BattleFactions.TeamB);

            // The most common wins.
            sandbox.Clear(Team.A);
            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Combat Form")).Ok);
            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Marine")).Ok);
            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Marine")).Ok);
            Assert.AreEqual(BattleFactions.Unsc, BattleFactions.TeamA);

            yield return null;
            yield return null;
            _played.Clear();
            sandbox.Run();
            yield return null;
            yield return null;
            Assert.AreEqual("music_unsc_battle", Director.Music.CurrentSlot);
            Assert.Contains("ui_unsc_battlestart", _played);
        }

        [UnityTest]
        public IEnumerator FloodInTheSandbox_SoundsLikeFlood()
        {
            yield return Load("Sandbox");
            var sandbox = Object.FindAnyObjectByType<SandboxController>();
            Assert.IsTrue(sandbox.AddSquad(Team.A, UnitNamed(sandbox, "Combat Form")).Ok);
            Assert.IsTrue(sandbox.AddSquad(Team.B, UnitNamed(sandbox, "Marine")).Ok);
            yield return null;
            Object.FindAnyObjectByType<BattleController>().Speed = 4f;
            sandbox.Run();
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!_played.Contains("sfx_fire_bullet_flood") && !_played.Contains("sfx_fire_melee_flood") && Time.realtimeSinceStartup < deadline) yield return null;
            Object.FindAnyObjectByType<BattleController>().Speed = 1f;
            Assert.IsTrue(_played.Contains("sfx_fire_bullet_flood") || _played.Contains("sfx_fire_melee_flood"), "Team A is Flood, whichever match came before.");
            Assert.IsFalse(_played.Contains("sfx_fire_melee"), "Not the plain UNSC swing.");
        }
    }
}
