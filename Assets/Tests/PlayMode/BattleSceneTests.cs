// BattleSceneTests.cs
// Plays the real Battle scene (Milestone 2): placeholder units spawn from data for every body,
// move, fight and die through the simulation; a full 300 vs 300 battle played in the scene ends
// with exactly the same result as the headless runner for the same setup and seed; selecting a
// squad shows its selection rings.
// Depends on: Halobellum.Presentation, Halobellum.Combat, Halobellum.Units, NUnit, Unity Test Framework.

using System.Collections;
using Halobellum.Combat;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class BattleSceneTests
    {
        private const string SceneName = "Battle";

        private static IEnumerator LoadBattle()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null; // Let every Start() run.
        }

        [UnityTest]
        public IEnumerator Scene_SpawnsOneViewPerBody_AndUnitsMove()
        {
            yield return LoadBattle();
            var battle = Object.FindAnyObjectByType<BattleController>();
            Assert.IsNotNull(battle, "The Battle scene has no BattleController.");
            Assert.IsNotNull(battle.Simulation, "The battle did not start (check the Console for data errors).");

            Transform units = GameObject.Find("Units").transform;
            Assert.AreEqual(battle.Simulation.Units.Count, units.childCount);
            Assert.AreEqual(600, units.childCount, "The test battle should be 300 vs 300.");
            Assert.IsNotNull(units.GetChild(0).Find("Body/Mesh"), "Unit views should be spawned from the unit's prefab.");

            var start = new Vector3[units.childCount];
            for (int i = 0; i < start.Length; i++) start[i] = units.GetChild(i).position;
            battle.Speed = 4f;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (battle.Simulation.Tick < 60 && Time.realtimeSinceStartup < deadline) yield return null; // 2 s of battle.
            yield return null;

            // Some units start in range and hold position, but most walk toward the enemy.
            int moved = 0;
            for (int i = 0; i < start.Length; i++) if (Vector3.Distance(start[i], units.GetChild(i).position) > 0.5f) moved++;
            Assert.Greater(moved, start.Length / 2, "Unit views should follow the simulation.");
        }

        [UnityTest]
        public IEnumerator FullBattleInTheScene_MatchesTheHeadlessRunner()
        {
            yield return LoadBattle();
            var battle = Object.FindAnyObjectByType<BattleController>();
            battle.Speed = 16f;

            float deadline = Time.realtimeSinceStartup + 300f;
            while (!battle.Simulation.IsFinished && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(battle.Simulation.IsFinished, "The battle did not finish in time.");

            // Dead units' views are hidden once their wrecks have burned out (M10); living units' views are shown.
            Transform units = GameObject.Find("Units").transform;
            int alive = battle.CountAlive(Halobellum.Core.Team.A) + battle.CountAlive(Halobellum.Core.Team.B);
            int shown = 0;
            deadline = Time.realtimeSinceStartup + 30f;
            do
            {
                yield return null;
                shown = 0;
                for (int i = 0; i < units.childCount; i++) if (units.GetChild(i).gameObject.activeSelf) shown++;
            }
            while (shown != alive && Time.realtimeSinceStartup < deadline);
            Assert.AreEqual(alive, shown);
            Assert.Less(alive, 600, "Nobody died.");

            BattleSetupDefinition setup = battle.BattleSetupAsset;
            BattleResult expected = HeadlessBattleRunner.RunBattle(battle.CombatRulesAsset.ToRules(), setup.BuildArmyA(), setup.BuildArmyB(),
                setup.BuildLayoutSettings(), setup.seed);
            Assert.AreEqual(expected.ToSummaryLine(), battle.Simulation.Result.ToSummaryLine());
        }

        [UnityTest]
        public IEnumerator SelectingASquad_ShowsRingsOnlyOnThatSquad()
        {
            yield return LoadBattle();
            var battle = Object.FindAnyObjectByType<BattleController>();
            var selection = battle.GetComponent<SquadSelection>();
            int squadId = battle.Simulation.Units[0].SquadId;

            selection.Select(squadId);
            yield return null;

            Transform units = GameObject.Find("Units").transform;
            for (int i = 0; i < units.childCount; i++)
            {
                bool inSquad = battle.Simulation.Units[i].SquadId == squadId;
                Assert.AreEqual(inSquad, units.GetChild(i).Find("Selection Ring").gameObject.activeSelf, units.GetChild(i).name);
            }
            Assert.IsNotNull(selection.FirstAliveOfSelected());

            selection.Select(-1);
            yield return null;
            Assert.IsFalse(units.GetChild(0).Find("Selection Ring").gameObject.activeSelf);
        }
    }
}
