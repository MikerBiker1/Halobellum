// MatchReplayTests.cs
// Plays a battle log again in the real Match scene (D106): a short match is played through the MatchController and logged as the scene logs it,
// then the scene is loaded afresh and MatchReplay plays the log through the controller, as the clicks did, to the same armies and battles.
// Depends on: Halobellum.Presentation (MatchController, MatchReplay), Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class MatchReplayTests
    {
        private const int Seed = 5;

        private static IEnumerator LoadMatch()
        {
            yield return SceneManager.LoadSceneAsync("Match", LoadSceneMode.Single);
            yield return null;
        }

        private static IEnumerator Battle(MatchController controller, MatchLog log)
        {
            Object.FindAnyObjectByType<BattleController>().Speed = 16f;
            controller.Ready();
            log.BattleStarted(controller.Match);
            float deadline = Time.realtimeSinceStartup + 120f;
            while (controller.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, controller.Match.Phase, "The battle did not finish in time.");
            log.BattleFinished(controller.Match.LastSummary);
            controller.Continue();
            log.CardsOffered(controller.Match);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALoggedMatch_ReplaysInTheScene_ThroughTheControllersOwnActions()
        {
            // Two rounds played through the controller, each action logged with the detail the controller writes.
            yield return LoadMatch();
            var controller = Object.FindAnyObjectByType<MatchController>();
            controller.NewMatch(Seed, difficulty: AiDifficulty.Normal);
            yield return null;
            var lines = new List<string>();
            var log = new MatchLog(lines.Add);
            Assert.IsTrue(controller.ChooseDraft(0).Ok);
            log.Started(controller.Match, "Normal", new System.DateTime(2026, 11, 7, 20, 0, 0), 0, Application.version);
            Match m = controller.Match;

            UnitStats type = m.Player.Squads[0].Type;
            log.PlayerAction(m, "Buy", type.Name, controller.Buy(type));
            PlacedSquad bought = m.Player.Squads.Last();
            controller.BeginMoving(bought);
            log.PlayerAction(m, "PlaceAt", MatchLog.MoveDetail(bought, 20, 1, false), controller.PlaceAt(20, 1));
            bool before = bought.Rotated90;
            controller.SelectSquad(bought);
            controller.ToggleRotation();
            log.PlayerAction(m, "ToggleRotation", MatchLog.TurnDetail(bought, bought.Rotated90), bought.Rotated90 != before ? ActionResult.Success() : ActionResult.Fail(controller.Message));
            yield return Battle(controller, log);

            if (m.Phase == MatchPhase.Cards) log.PlayerAction(m, "SkipCards", null, controller.SkipCards());
            log.PlayerAction(m, "Buy", type.Name, controller.Buy(type));
            yield return Battle(controller, log);
            var recorded = m.Player.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row, s.Rotated90)).ToList();
            var recordedEnemy = m.Enemy.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row)).ToList();
            int playerBase = m.Player.BaseHealth, enemyBase = m.Enemy.BaseHealth;

            // The scene afresh, and the replay.
            yield return LoadMatch();
            controller = Object.FindAnyObjectByType<MatchController>();
            var replay = controller.gameObject.AddComponent<MatchReplay>();
            replay.Lines = lines;
            replay.LogName = "test";
            replay.BattleSpeed = 16f;
            replay.StepSeconds = 0f;
            float until = Time.realtimeSinceStartup + 240f;
            while (!replay.Finished && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(replay.Finished, "The replay ran to its end: " + replay.Status);
            CollectionAssert.IsEmpty(replay.Replay.Drift, "Replayed exactly:\n" + string.Join("\n", replay.Replay.Drift));
            m = controller.Match;
            CollectionAssert.AreEqual(recorded, m.Player.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row, s.Rotated90)).ToList());
            CollectionAssert.AreEqual(recordedEnemy, m.Enemy.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row)).ToList(), "The AI answered as it did.");
            Assert.AreEqual(playerBase, m.Player.BaseHealth);
            Assert.AreEqual(enemyBase, m.Enemy.BaseHealth);
        }
    }
}
