// AudioFlowTests.cs
// Sound at the edges of a battle (M10): the game's pause holds the battle's sounds, leaving from the pause leaves nothing behind, a
// stinger doesn't follow the player off its screen, and the Sandbox's sound goes by the armies really placed.
// These can only check what is asked for and what is playing, not how it sounds. The tests run in batch mode, where the room stays silent.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
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
