// AudioSceneTests.cs
// The sound system in the real scenes (M10): the director is there and survives scene loads; each screen of a match asks for the right
// music and UI sounds for the faction; a battle asks for its weapon sounds; they pause with the battle; a click on any IMGUI control
// is heard; the volume settings are read; hundreds of shots stay within the limits and allocate nothing.
// These can only check which clip is asked for, not how it sounds. The tests run in batch mode, where the director keeps the room silent.
// Depends on: Halobellum.Presentation, Halobellum.Combat, Halobellum.Economy, NUnit, Unity Test Framework.

using System;
using System.Collections;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class AudioSceneTests
    {
        private readonly List<string> _played = new List<string>();

        private AudioDirector Director => AudioDirector.Instance;

        private void Heard(string slot) => _played.Add(slot);

        [SetUp]
        public void SetUp() => _played.Clear();

        [TearDown]
        public void TearDown()
        {
            if (Director != null) Director.SlotPlayed -= Heard;
            GUIUtility.hotControl = 0;
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            yield return null; // Let every Start() run.
            yield return null;
            Assert.IsNotNull(Director, "The audio director should exist in every scene.");
            Director.SlotPlayed -= Heard;
            Director.SlotPlayed += Heard;
        }

        private static int DraftCardOf(Match match, string faction)
        {
            for (int i = 0; i < match.PlayerDraft.Count; i++)
            {
                if (match.PlayerDraft[i].Faction.Name == faction) return i;
            }
            return -1;
        }

        /// <summary>Starts new matches until the draft offers this faction.</summary>
        private static int NewMatchOffering(MatchController controller, string faction)
        {
            for (int seed = 1; seed < 200; seed++)
            {
                controller.NewMatch(seed);
                int card = DraftCardOf(controller.Match, faction);
                if (card >= 0) return card;
            }
            Assert.Fail("No draft offered " + faction);
            return -1;
        }

        [UnityTest]
        public IEnumerator Director_SurvivesASceneLoad_AndStaysTheOnlyOne()
        {
            yield return Load("Match");
            AudioDirector first = Director;
            yield return Load("Sandbox");
            Assert.AreSame(first, Director);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<AudioDirector>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator Match_EachScreenHasItsMusicAndSounds([Values("UNSC", "Covenant", "Flood")] string faction)
        {
            string key = AudioSlots.FactionKey(faction);
            yield return Load("Match");
            var controller = UnityEngine.Object.FindAnyObjectByType<MatchController>();
            var battle = UnityEngine.Object.FindAnyObjectByType<BattleController>();
            Assert.IsNotNull(controller.GetComponent<MatchAudio>(), "The director should have attached its listener.");
            Assert.IsNotNull(battle.GetComponent<BattleAudio>());

            int card = NewMatchOffering(controller, faction);
            Assert.AreEqual(AudioSlots.MainMenuMusic, Director.Music.CurrentSlot, "The draft has no faction yet.");
            Assert.IsNull(Director.UiFaction);

            Assert.IsTrue(controller.ChooseDraft(card).Ok);
            Assert.Contains("ui_neutral_cardpick", _played, "The side is chosen after the pick is heard.");
            Assert.AreEqual($"music_{key}_deployment", Director.Music.CurrentSlot);
            Assert.AreEqual(faction, Director.UiFaction);

            // A purchase: its faction's sound, and the squad dropping in survives the preview being rebuilt.
            UnitStats unit = controller.Match.Player.Roster.Find(u => controller.Match.Player.Unlocked.Contains(u) && u.Cost <= controller.Match.Player.Credits);
            Assert.IsNotNull(unit);
            _played.Clear();
            Assert.IsTrue(controller.Buy(unit).Ok);
            Assert.Contains($"ui_{key}_buy", _played);
            Assert.Contains("sfx_drop_in", _played);
            Assert.Greater(Director.ActiveSfxVoices, 0, "Rebuilding the preview must not cut the drop's sound.");

            _played.Clear();
            controller.Sell(null);
            Assert.Contains($"ui_{key}_error", _played, "A failed action is heard as an error.");

            // The battle.
            _played.Clear();
            battle.Speed = 4f;
            controller.Ready();
            Assert.Contains($"ui_{key}_battlestart", _played);
            Assert.AreEqual($"music_{key}_battle", Director.Music.CurrentSlot);
            float deadline = Time.realtimeSinceStartup + 60f;
            while (controller.Match.Phase == MatchPhase.Battle && !_played.Exists(s => s.StartsWith("sfx_fire_")) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(_played.Exists(s => s.StartsWith("sfx_fire_")), "A battle should ask for its weapons' sounds.");

            // Pausing the battle pauses its sounds, not the music.
            battle.Speed = 0f;
            yield return null;
            yield return null;
            if (controller.Match.Phase == MatchPhase.Battle)
            {
                Assert.IsTrue(Director.BattlePaused);
                _played.Clear();
                Director.PlaySfx(AudioCatalog.ExplosionSmall);
                Assert.IsEmpty(_played, "Nothing new starts while paused.");
                Director.PlayUi(UiSound.Click);
                Assert.Contains($"ui_{key}_click", _played, "UI sounds still play.");
            }
            battle.Speed = 4f;
            yield return null;
            yield return null;
            Assert.IsFalse(Director.BattlePaused);

            // To the end of the battle: the quiet track between rounds.
            deadline = Time.realtimeSinceStartup + 120f;
            while (controller.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, controller.Match.Phase, "The battle should have finished.");
            Assert.AreEqual($"music_{key}_menu", Director.Music.CurrentSlot);
            battle.Speed = 1f;
        }

        [UnityTest]
        public IEnumerator Sandbox_FollowsBattleFactions()
        {
            BattleFactions.Set(BattleFactions.Covenant, BattleFactions.Flood);
            yield return Load("Sandbox");
            var sandbox = UnityEngine.Object.FindAnyObjectByType<SandboxController>();
            Assert.IsNotNull(sandbox.GetComponent<SandboxAudio>());
            Assert.AreEqual("music_covenant_deployment", Director.Music.CurrentSlot);
            Assert.AreEqual(BattleFactions.Covenant, Director.UiFaction);

            BattleFactions.Set(BattleFactions.Flood, BattleFactions.Unsc);
            Assert.AreEqual("music_flood_deployment", Director.Music.CurrentSlot, "A change of faction changes the music.");

            sandbox.AddSquad(Team.A, sandbox.Units[0].Stats);
            sandbox.AddSquad(Team.B, sandbox.Units[0].Stats);
            yield return null;
            yield return null;
            Assert.Contains("sfx_drop_in", _played);
            _played.Clear();
            sandbox.Run();
            yield return null;
            yield return null;
            Assert.AreEqual("music_flood_battle", Director.Music.CurrentSlot);
            Assert.Contains("ui_flood_battlestart", _played);
            BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
        }

        [UnityTest]
        public IEnumerator MainMenuScene_HasItsOwnMusic_AndNoFactionSounds()
        {
            yield return Load("Sandbox");
            Director.HandleScene(AudioSlots.MainMenuScene);
            Assert.AreEqual(AudioSlots.MainMenuMusic, Director.Music.CurrentSlot);
            Assert.IsNull(Director.UiFaction);
            Director.PlayUi(UiSound.Click);
            Assert.Contains("ui_neutral_click", _played);
            yield return Load("Sandbox");
        }

        [UnityTest]
        public IEnumerator AClickOnAnyImguiControl_IsHeard_Once()
        {
            yield return Load("Sandbox");
            yield return null;
            Director.UiFaction = BattleFactions.Flood;
            _played.Clear();
            GUIUtility.hotControl = 4242; // What an IMGUI button does when the mouse goes down on it.
            yield return null;
            yield return null;
            Assert.AreEqual(1, _played.FindAll(s => s == "ui_flood_click").Count);
            GUIUtility.hotControl = 0;
            yield return null;
            GUIUtility.hotControl = 4243;
            yield return null;
            yield return null;
            Assert.AreEqual(2, _played.FindAll(s => s == "ui_flood_click").Count);
            GUIUtility.hotControl = 0;
        }

        [UnityTest]
        public IEnumerator Volumes_AreReadFromTheSettings()
        {
            yield return Load("Sandbox");
            float was = PlayerPrefs.GetFloat(AudioDirector.KeyMusic, AudioDirector.DefaultMusic);
            try
            {
                PlayerPrefs.SetFloat(AudioDirector.KeyMusic, 0.25f);
                float deadline = Time.realtimeSinceStartup + 2f;
                while (Director.VolumeMusic != 0.25f && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(0.25f, Director.VolumeMusic, 1e-4f, "Should be picked up within about half a second.");
            }
            finally
            {
                PlayerPrefs.SetFloat(AudioDirector.KeyMusic, was);
            }
        }

        [UnityTest]
        public IEnumerator HundredsOfShots_StayWithinTheLimits_AndAllocateNothing()
        {
            yield return Load("Sandbox");
            SfxCue rifle = AudioCatalog.Fire(WeaponFeelKind.Bullet, "UNSC");
            Director.SlotPlayed -= Heard;
            for (int i = 0; i < 4; i++) yield return null;

            int droppedBefore = Director.DroppedVoices;
            Vector3 place = new Vector3(10f, 0f, 10f);
            for (int i = 0; i < 20; i++) Director.PlaySfx(rifle, place); // Warm up.
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) Director.PlaySfx(rifle, place);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "A shot should allocate nothing.");
            Assert.Greater(Director.DroppedVoices, droppedBefore + 1900, "Nearly all shots of one instant are dropped.");
            Assert.LessOrEqual(Director.ActiveSfxVoices, 20);

            // Over time, however many are asked for, no more than the limit play at once.
            for (int frame = 0; frame < 40; frame++)
            {
                for (int i = 0; i < 20; i++) Director.PlaySfx(rifle, place);
                Assert.LessOrEqual(Director.ActiveSfxVoices, 4, "The rifles' limit.");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheRoomIsSilent_InATestRun()
        {
            yield return Load("Sandbox");
            Assert.IsTrue(Director.IsMuted);
            Assert.AreEqual(0f, AudioListener.volume);
        }
    }
}
