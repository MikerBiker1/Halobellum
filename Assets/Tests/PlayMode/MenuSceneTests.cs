// MenuSceneTests.cs
// Plays the real menu and pause flow (M10): the MainMenu scene opens with its camera moving and its Pelicans flying, and its Scarab standing still; New Match reaches a
// working draft; pausing truly stops a battle in every phase and resumes where it was, at the same speed; the HUD and field input are
// off while paused and back after; leaving for the menu puts time right; the Sandbox's Back button goes where it came from.
// The Play page, through the plain methods its buttons call: its pages, Esc stepping back, and that a start reaches the match with what was chosen. Its buttons, card list and a real Esc
// are only pressed by -menuDemo, since a batch run draws no IMGUI.
// Depends on: Halobellum.Presentation, Halobellum.UI, Halobellum.Economy, NUnit, Unity Test Framework.

using System.Collections;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class MenuSceneTests
    {
        private MatchController _match;
        private BattleController _battle;

        [Test]
        public void ThePlayModeTests_KeepOffTheSavedSettings()
        {
            Assert.IsTrue(GameSettings.HeldInMemory, "SettingsHeldForTests holds them in memory for the whole run.");
        }

        [UnityTearDown]
        public IEnumerator LeaveNothingPaused()
        {
            GamePause.Resume();
            Assert.AreEqual(1f, Time.timeScale);
            GameFlow.TakeMatchSetup(); // These last are static, and held in memory.
            GameSettings.SkirmishBaseHealth = GameSettings.DefaultSkirmishBaseHealth;
            GameSettings.Difficulty = AiDifficulty.Normal;
            yield return null;
        }

        private static IEnumerator WaitForScene(string name)
        {
            float limit = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < limit) yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
            yield return null;
            yield return null;
        }

        private IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MatchScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            _match.NewMatch(3);
            yield return null;
        }

        private static IEnumerator LoadMenu(System.Action<MainMenuHud> found)
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            var hud = Object.FindAnyObjectByType<MainMenuHud>();
            Assert.IsNotNull(hud);
            found(hud);
        }

        private static IEnumerator WaitRealSeconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenu_Opens_WithItsPictureMoving()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenuHud>());
            var backdrop = Object.FindAnyObjectByType<MenuBackdrop>();
            Assert.IsNotNull(backdrop);
            Assert.IsNotNull(Camera.main);
            Assert.IsNull(PauseMenu.Current, "The menu has no battle to pause.");
            Assert.Greater(backdrop.flyers.Length, 2);

            Vector3 before = Camera.main.transform.position;
            yield return WaitRealSeconds(1f);
            Assert.AreNotEqual(before, Camera.main.transform.position, "The camera drifts.");

            backdrop.ApplyAt(10f);
            bool seen = false;
            foreach (MenuBackdrop.Flyer flyer in backdrop.flyers) seen |= flyer.body.gameObject.activeSelf;
            Assert.IsTrue(seen, "Some Pelican is in the air soon after the menu opens.");
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Assert.IsNotNull(renderer.sharedMaterial, renderer.name + " has no material.");
                Assert.AreNotEqual("Hidden/InternalErrorShader", renderer.sharedMaterial.shader.name, renderer.name);
            }
        }

        [UnityTest]
        public IEnumerator MainMenu_TheWalkerStandsStill_AndItsBeamStillFires()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            var backdrop = Object.FindAnyObjectByType<MenuBackdrop>();
            var parts = backdrop.walker.GetComponentsInChildren<Transform>(true);
            var places = new Vector3[parts.Length];
            var turns = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                places[i] = parts[i].position;
                turns[i] = parts[i].rotation;
            }

            yield return WaitRealSeconds(1f);
            backdrop.ApplyAt(backdrop.beamStart + 1f);
            yield return null;
            for (int i = 0; i < parts.Length; i++)
            {
                Assert.AreEqual(places[i], parts[i].position, parts[i].name + " stays where it is.");
                Assert.AreEqual(turns[i], parts[i].rotation, parts[i].name + " doesn't swing.");
            }
            backdrop.ApplyAt(backdrop.beamStart + 1f);
            Assert.IsTrue(backdrop.beam.enabled, "The beam fires.");
            Assert.AreEqual(backdrop.beamFrom.position, backdrop.beam.GetPosition(0), "From its gun.");
        }

        [UnityTest]
        public IEnumerator MainMenu_TheWalkersPanelsStayDim_UntilTheBeamFires()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            var backdrop = Object.FindAnyObjectByType<MenuBackdrop>();
            int emission = Shader.PropertyToID("_EmissionColor");
            Renderer panel = null;
            int index = -1;
            foreach (Renderer renderer in backdrop.walker.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || materials[i].name != "Scarab_1") continue;
                    panel = renderer;
                    index = i;
                }
            }
            Assert.IsNotNull(panel, "The walker has its glowing panel.");
            float own = panel.sharedMaterials[index].GetColor(emission).maxColorComponent;
            Assert.Greater(own, 1f, "The battle's Scarab keeps its bright material.");

            var block = new MaterialPropertyBlock();
            backdrop.ApplyAt(backdrop.beamStart - 1f);
            Assert.AreEqual(0f, backdrop.PanelStrength);
            panel.GetPropertyBlock(block, index);
            float atRest = block.GetColor(emission).maxColorComponent;
            Assert.Greater(atRest, 0f, "The panel has its dim glow set.");
            Assert.Less(atRest, own * 0.1f, "Dim while nothing fires.");

            backdrop.ApplyAt(backdrop.beamStart + backdrop.beamSeconds * 0.5f);
            Assert.AreEqual(1f, backdrop.PanelStrength);
            panel.GetPropertyBlock(block, index);
            Assert.Greater(block.GetColor(emission).maxColorComponent, atRest * 2f, $"It rises as the beam fires (block empty: {block.isEmpty}, {panel.name} slot {index}).");
            Assert.AreEqual(own, panel.sharedMaterials[index].GetColor(emission).maxColorComponent, "The shared material is not touched.");
        }

        [UnityTest]
        public IEnumerator LeavingTheMenu_ReachesAWorkingDraft_WithTheChosenDifficulty()
        {
            GameSettings.Difficulty = AiDifficulty.Hard;
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;

            GameFlow.NewMatch();
            yield return WaitForScene(GameFlow.MatchScene);
            var match = Object.FindAnyObjectByType<MatchController>();
            Assert.AreEqual(MatchPhase.Draft, match.Match.Phase);
            Assert.AreEqual(5, match.Match.PlayerDraft.Count);
            Assert.AreEqual(AiDifficulty.Hard, match.Difficulty);
            Assert.IsTrue(match.ChooseDraft(0).Ok);
            Assert.AreEqual(MatchPhase.Deployment, match.Match.Phase);
            GameSettings.Difficulty = AiDifficulty.Normal;
        }

        [UnityTest]
        public IEnumerator ThePlayPage_ShowsAPanelForTheSelectedMode_AndBackGoesToTheFirstPage()
        {
            MainMenuHud hud = null;
            yield return LoadMenu(found => hud = found);
            Assert.AreEqual(MainMenuHud.Page.Main, hud.CurrentPage);
            Assert.IsNull(hud.SelectedMode);

            hud.OpenPlayPage();
            Assert.AreEqual(MainMenuHud.Page.Play, hud.CurrentPage);
            Assert.IsNull(hud.SelectedMode, "No mode is chosen for the player.");

            hud.SelectMode(MatchMode.Faction);
            Assert.AreEqual(MatchMode.Faction, hud.SelectedMode);
            hud.SelectMode(MatchMode.UnlockSkirmish);
            Assert.AreEqual(MatchMode.UnlockSkirmish, hud.SelectedMode);

            hud.Back();
            Assert.AreEqual(MainMenuHud.Page.Main, hud.CurrentPage);
            Assert.IsNull(hud.SelectedMode);
        }

        [UnityTest]
        public IEnumerator Esc_StepsBack_FromTheCardList_ThenThePlayPage_AndStopsAtTheFirstPage()
        {
            MainMenuHud hud = null;
            yield return LoadMenu(found => hud = found);
            hud.OpenPlayPage();
            hud.SelectMode(MatchMode.UnlockSkirmish);
            hud.CardListOpen = true;

            Assert.IsTrue(hud.HandleEscape());
            Assert.IsFalse(hud.CardListOpen);
            Assert.AreEqual(MainMenuHud.Page.Play, hud.CurrentPage, "Only the list closes.");
            Assert.AreEqual(MatchMode.UnlockSkirmish, hud.SelectedMode);

            Assert.IsTrue(hud.HandleEscape());
            Assert.AreEqual(MainMenuHud.Page.Main, hud.CurrentPage);
            Assert.IsNull(hud.SelectedMode);

            Assert.IsFalse(hud.HandleEscape(), "There is nothing further to go back to.");
            Assert.AreEqual(MainMenuHud.Page.Main, hud.CurrentPage);
            Assert.IsFalse(hud.IsLeaving);
        }

        [UnityTest]
        public IEnumerator Start_WithNoModeSelected_GoesNowhere()
        {
            MainMenuHud hud = null;
            yield return LoadMenu(found => hud = found);
            hud.OpenPlayPage();
            hud.StartSelected();
            Assert.IsFalse(hud.IsLeaving);
        }

        [UnityTest]
        public IEnumerator AFactionStart_ReachesAFactionMatch_WithTheChosenDifficulty()
        {
            MainMenuHud hud = null;
            yield return LoadMenu(found => hud = found);
            GameSettings.Difficulty = AiDifficulty.Easy;
            GameSettings.SkirmishBaseHealth = 500;
            hud.OpenPlayPage();
            hud.SelectMode(MatchMode.Faction);
            hud.Cards = CardFilter.OnlyReinforcements;
            hud.StartSelected();
            Assert.IsTrue(hud.IsLeaving);
            yield return WaitForScene(GameFlow.MatchScene);

            var match = Object.FindAnyObjectByType<MatchController>();
            Assert.AreEqual(MatchMode.Faction, match.Match.Rules.Mode);
            Assert.AreEqual(CardFilter.AllCards, match.Match.Rules.Cards, "Faction mode takes only the difficulty.");
            Assert.AreEqual(2000, match.Match.Rules.BaseHealth);
            Assert.AreEqual(AiDifficulty.Easy, match.Difficulty);
        }

        [UnityTest]
        public IEnumerator ASkirmishStart_ReachesTheMatch_WithTheChosenHealthAndFilter()
        {
            MainMenuHud hud = null;
            yield return LoadMenu(found => hud = found);
            GameSettings.Difficulty = AiDifficulty.Hard;
            GameSettings.SkirmishBaseHealth = 500;
            hud.OpenPlayPage();
            hud.SelectMode(MatchMode.UnlockSkirmish);
            hud.Cards = CardFilter.NoReinforcements;
            hud.StartSelected();
            yield return WaitForScene(GameFlow.MatchScene);

            var match = Object.FindAnyObjectByType<MatchController>();
            Assert.AreEqual(MatchMode.UnlockSkirmish, match.Match.Rules.Mode);
            Assert.AreEqual(500, match.Match.Rules.BaseHealth);
            Assert.AreEqual(CardFilter.NoReinforcements, match.Match.Rules.Cards);
            Assert.AreEqual(AiDifficulty.Hard, match.Difficulty);
            Assert.AreEqual(MatchPhase.Draft, match.Match.Phase);
            Assert.AreEqual(5, match.Match.PlayerDraft.Count);
        }

        [UnityTest]
        public IEnumerator TheCardFilter_GoesBackToAllCards_WhenTheMenuLoads_AndTheHealthStays()
        {
            MainMenuHud hud = null;
            yield return LoadMenu(found => hud = found);
            Assert.AreEqual(CardFilter.AllCards, hud.Cards);
            hud.Cards = CardFilter.OnlyReinforcements;
            GameSettings.SkirmishBaseHealth = 700;
            hud.OpenPlayPage();

            yield return LoadMenu(found => hud = found);
            Assert.AreEqual(CardFilter.AllCards, hud.Cards);
            Assert.AreEqual(MainMenuHud.Page.Main, hud.CurrentPage);
            Assert.AreEqual(700, GameSettings.SkirmishBaseHealth);
        }

        [UnityTest]
        public IEnumerator TheSandbox_LeavesFromThePlayPage_AndComesBackToTheMenu()
        {
            MainMenuHud hud = null;
            yield return LoadMenu(found => hud = found);
            hud.OpenPlayPage();
            hud.OpenSandbox();
            yield return WaitForScene(GameFlow.SandboxScene);
            Assert.AreEqual(GameFlow.MainMenuScene, GameFlow.SandboxCameFrom);
        }

        [UnityTest]
        public IEnumerator Pausing_StopsTheBattle_AndResumesItAtTheSameSpeed()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            _battle.Speed = 2f;
            _match.Ready();
            Assert.AreEqual(MatchPhase.Battle, _match.Match.Phase);
            yield return WaitRealSeconds(0.5f);

            PauseMenu.Current.Open();
            Assert.IsTrue(GamePause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            yield return null; // A frame already under way may finish.
            int tick = _battle.Simulation.Tick;
            yield return WaitRealSeconds(1f);
            Assert.AreEqual(tick, _battle.Simulation.Tick, "No tick runs while paused.");

            PauseMenu.Current.Close();
            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(2f, _battle.Speed, "It keeps its speed.");
            yield return WaitRealSeconds(0.5f);
            Assert.Greater(_battle.Simulation.Tick, tick, "The battle runs on.");
        }

        [UnityTest]
        public IEnumerator Pausing_IsSafeInEveryPhase()
        {
            yield return LoadMatch();
            yield return PauseAndResume(); // Draft.
            Assert.AreEqual(MatchPhase.Draft, _match.Match.Phase);

            _match.ChooseDraft(0);
            yield return null;
            yield return PauseAndResume(); // Deployment.
            Assert.AreEqual(MatchPhase.Deployment, _match.Match.Phase);

            _battle.Speed = 16f;
            _match.Ready();
            yield return null;
            yield return PauseAndResume(); // Battle.
            float limit = Time.realtimeSinceStartup + 120f;
            while (_match.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < limit) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, _match.Match.Phase, "The battle ended after its pause.");

            yield return PauseAndResume(); // Result, or the end of the match.
            if (_match.Match.Phase == MatchPhase.Result)
            {
                _match.Continue();
                yield return null;
                yield return PauseAndResume(); // Cards.
            }
        }

        private static IEnumerator PauseAndResume()
        {
            PauseMenu.Current.Open();
            yield return null;
            yield return null;
            Assert.IsTrue(GamePause.IsPaused);
            PauseMenu.Current.Close();
            yield return null;
            Assert.IsFalse(GamePause.IsPaused);
        }

        [UnityTest]
        public IEnumerator WhilePaused_TheHudAndFieldInputAreOff_AndComeBack()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            var hud = Object.FindAnyObjectByType<MatchHud>();
            var input = _battle.GetComponent<DeploymentInput>();
            var selection = _battle.GetComponent<SquadSelection>();
            var camera = Camera.main.GetComponent<BattleCameraController>();
            Assert.IsNotNull(GameObject.Find(PauseMenu.HudObjectName), "The HUD object is found by this name.");

            PauseMenu.Current.Open();
            Assert.IsFalse(hud.enabled);
            Assert.IsFalse(input.enabled);
            Assert.IsFalse(selection.enabled);
            Assert.IsFalse(camera.enabled);
            yield return null;

            PauseMenu.Current.Close();
            Assert.IsTrue(hud.enabled);
            Assert.IsTrue(input.enabled);
            Assert.IsTrue(selection.enabled);
            Assert.IsTrue(camera.enabled);
        }

        [UnityTest]
        public IEnumerator LeavingWhilePaused_PutsTimeRight()
        {
            yield return LoadMatch();
            PauseMenu.Current.Open();
            yield return null;
            GameFlow.OpenMainMenu();
            yield return WaitForScene(GameFlow.MainMenuScene);
            Assert.IsFalse(GamePause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator TheSandbox_HasAPauseMenu_AndGoesBackWhereItCameFrom()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            GameFlow.OpenSandbox();
            yield return WaitForScene(GameFlow.SandboxScene);
            Assert.IsNotNull(PauseMenu.Current);
            Assert.AreEqual(GameFlow.MainMenuScene, GameFlow.SandboxCameFrom);
            GameFlow.LeaveSandbox();
            yield return WaitForScene(GameFlow.MainMenuScene);

            GameFlow.NewMatch();
            yield return WaitForScene(GameFlow.MatchScene);
            SceneManager.LoadScene(MatchHud.SandboxSceneName); // From a match.
            yield return WaitForScene(GameFlow.SandboxScene);
            Assert.AreEqual(GameFlow.MatchScene, GameFlow.SandboxCameFrom);
            GameFlow.LeaveSandbox();
            yield return WaitForScene(GameFlow.MatchScene);
        }
    }
}
