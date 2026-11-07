// MenuSceneTests.cs
// Plays the real menu and pause flow (M10): the cinematic MainMenu scene (D94) opens on its battle, cutting through black between its shots, all
// three sides in it (vehicles only) and the Scarab walking and firing its beam; the classic menu kept beside it (MainMenuClassic) opens with its camera moving,
// its Pelicans flying and its Scarab standing still; New Match reaches a
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
        public IEnumerator ClassicMenu_Opens_WithItsPictureMoving()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.ClassicMainMenuScene, LoadSceneMode.Single);
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
        public IEnumerator ClassicMenu_TheWalkerStandsStill_AndItsBeamStillFires()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.ClassicMainMenuScene, LoadSceneMode.Single);
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
        public IEnumerator ClassicMenu_TheWalkersPanelsStayDim_UntilTheBeamFires()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.ClassicMainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            var backdrop = Object.FindAnyObjectByType<MenuBackdrop>();
            int emission = Shader.PropertyToID("_EmissionColor");
            // Its brightest glowing material: since D84 the Scarab's lights, red at the back and green on the cap.
            Renderer panel = null;
            int index = -1;
            float brightest = 0f;
            foreach (Renderer renderer in backdrop.walker.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || !materials[i].name.StartsWith("Scarab_") || !materials[i].HasProperty(emission)) continue;
                    float glow = materials[i].GetColor(emission).maxColorComponent;
                    if (glow <= brightest) continue;
                    brightest = glow;
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
        public IEnumerator CinematicMenu_Opens_OnItsBattle_WithTheCameraMoving()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenuHud>());
            var cinematic = Object.FindAnyObjectByType<MenuCinematic>();
            Assert.IsNotNull(cinematic, "The main menu is the cinematic battlefield (D94).");
            Assert.IsNull(Object.FindAnyObjectByType<MenuBackdrop>(), "The classic picture is in its own scene.");
            Assert.IsNotNull(Camera.main);
            Assert.AreSame(Camera.main, cinematic.view);
            Assert.IsNull(PauseMenu.Current, "The menu has no battle to pause.");
            Assert.GreaterOrEqual(cinematic.shots.Length, 4, "Several shots.");
            Assert.AreEqual(2, cinematic.figures.Length, "Vehicles only: no soldiers or Flood forms (the owner, D94); its animated bodies are its two Mantises (D97).");
            foreach (MenuCinematic.Figure figure in cinematic.figures) Assert.AreEqual("Mantis", figure.renderer.name);
            Assert.Greater(cinematic.marchers.Length, 6, "Vehicles racing across the field.");
            Assert.Greater(cinematic.flyers.Length, 8, "Aircraft.");
            Assert.Greater(cinematic.blasts.Length, 2, "Blasts.");

            Vector3 before = Camera.main.transform.position;
            yield return WaitRealSeconds(1f);
            Assert.AreNotEqual(before, Camera.main.transform.position, "The camera moves.");
            Assert.Greater(cinematic.Clock, 0.5f, "Its clock runs on real time.");
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Assert.IsNotNull(renderer.sharedMaterial, renderer.name + " has no material.");
                Assert.AreNotEqual("Hidden/InternalErrorShader", renderer.sharedMaterial.shader.name, renderer.name);
            }
        }

        [UnityTest]
        public IEnumerator CinematicMenu_CutsBetweenItsShots_ThroughBlack()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            var cinematic = Object.FindAnyObjectByType<MenuCinematic>();
            float loop = cinematic.LoopSeconds, at = 0f;
            Assert.AreEqual(1f, cinematic.FadeAt(0f), 1e-4f, "It opens from black.");
            var places = new Vector3[cinematic.shots.Length];
            for (int i = 0; i < cinematic.shots.Length; i++)
            {
                MenuCinematic.Shot shot = cinematic.shots[i];
                Assert.AreEqual(i, cinematic.ShotAt(at + shot.seconds * 0.5f, out float share), shot.name);
                Assert.AreEqual(0.5f, share, 1e-3f);
                Assert.AreEqual(0f, cinematic.FadeAt(at + shot.seconds * 0.5f), 1e-4f, shot.name + " is clear in its middle.");
                Assert.Greater(cinematic.FadeAt(at + shot.seconds - 0.05f), 0.8f, shot.name + " ends in black.");
                Assert.Greater(Vector3.Distance(shot.from, shot.to), 3f, shot.name + " moves.");
                places[i] = shot.from;
                at += shot.seconds;
            }
            Assert.AreEqual(loop, at, 1e-3f);
            Assert.AreEqual(0, cinematic.ShotAt(loop + 1f, out _), "It goes round again.");
            for (int i = 1; i < places.Length; i++) Assert.Greater(Vector3.Distance(places[i], places[i - 1]), 40f, "Each shot is somewhere else.");
        }

        [UnityTest]
        public IEnumerator CinematicMenu_HasAllThreeSides_AndItsScarabWalksAndFires()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MainMenuScene, LoadSceneMode.Single);
            yield return null;
            yield return null;
            var cinematic = Object.FindAnyObjectByType<MenuCinematic>();
            foreach (string side in new[] { "UNSC", "Covenant", "Flood" })
            {
                GameObject army = GameObject.Find(side);
                Assert.IsNotNull(army, side);
                Assert.Greater(army.GetComponentsInChildren<Renderer>().Length, 10, side + " is there in strength.");
            }
            Assert.IsNotNull(cinematic.Walker, "The Scarab has its walking legs.");

            Vector3 before = cinematic.walkerRoot.position;
            cinematic.Advance(cinematic.beamStart - cinematic.beamChargeSeconds * 0.5f - cinematic.Clock);
            Assert.That(cinematic.BeamCharge, Is.InRange(0.3f, 0.7f), "It charges first, energy gathering in its mouth (D98).");
            Assert.IsFalse(cinematic.BeamFiring);
            Assert.IsNotNull(cinematic.MouthLight, "Its mouth has its light.");
            Assert.IsTrue(cinematic.MouthLight.enabled, "Its mouth glows green as it charges.");
            cinematic.Advance(cinematic.beamChargeSeconds * 0.5f + 1f);
            yield return null;
            Assert.Greater(Vector3.Distance(before, cinematic.walkerRoot.position), 3f, "It walks across the field.");
            Assert.IsTrue(cinematic.BeamFiring, "Its beam fires.");
            Assert.AreEqual(1f, cinematic.PanelStrength, 1e-3f, "Its panels glow as it fires.");
            cinematic.Advance(cinematic.beamPeriod * 0.5f);
            Assert.IsFalse(cinematic.BeamFiring, "And stops.");
            Assert.AreEqual(0f, cinematic.BeamCharge);
            Assert.IsFalse(cinematic.MouthLight.enabled, "Its mouth goes dark.");
            Assert.AreEqual(0f, cinematic.PanelStrength);
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
