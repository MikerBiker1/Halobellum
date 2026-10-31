// MatchSceneTests.cs
// Plays the real Match scene (Milestone 3): starting draft, deployment preview (the AI's army stays
// hidden), buying and placing a squad, a full round (deployment, battle, result, next round with
// income), and full matches against the do-nothing AI that are won and lost.
// Depends on: Halobellum.Presentation, Halobellum.Economy, Halobellum.Core, NUnit, Unity Test Framework.

using System.Collections;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class MatchSceneTests
    {
        private MatchController _match;
        private BattleController _battle;

        private IEnumerator LoadMatch(int seed = 3)
        {
            yield return SceneManager.LoadSceneAsync("Match", LoadSceneMode.Single);
            yield return null;
            _match = Object.FindAnyObjectByType<MatchController>();
            _battle = Object.FindAnyObjectByType<BattleController>();
            Assert.IsNotNull(_match, "The Match scene has no MatchController.");
            _match.NewMatch(seed);
            yield return null;
        }

        private static int CountViews()
        {
            GameObject units = GameObject.Find("Units");
            return units == null ? 0 : units.transform.childCount;
        }

        private IEnumerator PlayRound()
        {
            _battle.Speed = 16f;
            _match.Ready();
            float deadline = Time.realtimeSinceStartup + 120f;
            while (_match.Match.Phase == MatchPhase.Battle && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreNotEqual(MatchPhase.Battle, _match.Match.Phase, "The battle did not finish in time.");
        }

        [UnityTest]
        public IEnumerator Draft_ThenDeployment_ShowsOnlyThePlayersSquads()
        {
            yield return LoadMatch();
            Assert.AreEqual(MatchPhase.Draft, _match.Match.Phase);
            Assert.AreEqual(2, _match.Match.PlayerDraft.Count, "Standard UNSC and Standard Covenant.");

            _match.ChooseDraft(0);
            yield return null;
            Assert.AreEqual(MatchPhase.Deployment, _match.Match.Phase);
            int playerBodies = 0;
            foreach (PlacedSquad squad in _match.Match.Player.Squads) playerBodies += squad.Type.SquadSize;
            Assert.AreEqual(playerBodies, CountViews(), "Only the player's squads are shown during deployment.");
            Assert.IsFalse(_battle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator BuyingAndPlacing_AddsTheSquad_AndSelectsIt()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            UnitStats type = m.Player.Squads[0].Type;
            int creditsBefore = m.Player.Credits;
            int viewsBefore = CountViews();

            Assert.IsTrue(DeploymentGrid.FindFreeSpot(m.Player, type, false, m.CombatRules, out int column, out int row));
            _match.BeginPlacing(type);
            Assert.IsTrue(_match.IsPlacing);
            Assert.IsTrue(_match.PlaceAt(column, row).Ok, _match.Message);
            yield return null;

            Assert.IsFalse(_match.IsPlacing);
            Assert.AreEqual(creditsBefore - type.Cost, m.Player.Credits);
            Assert.AreEqual(viewsBefore + type.SquadSize, CountViews());
            Assert.IsNotNull(_match.SelectedPlacedSquad, "The new squad should be selected.");
            Assert.AreEqual(column, _match.SelectedPlacedSquad.Column);
        }

        [UnityTest]
        public IEnumerator FullRound_BattleThenResultThenNextRoundWithIncome()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            int creditsBefore = m.Player.Credits;

            yield return PlayRound();
            Assert.AreEqual(MatchPhase.Result, m.Phase);
            Assert.IsNotNull(m.LastSummary);
            Assert.AreEqual(m.Player.Squads.Count + m.Enemy.Squads.Count, _battle.Simulation.Squads.Count, "The battle reveals the AI's army.");

            _match.Continue();
            yield return null;
            Assert.AreEqual(MatchPhase.Deployment, m.Phase);
            Assert.AreEqual(2, m.Round);
            Assert.AreEqual(creditsBefore + m.LastSummary.IncomeAwarded, m.Player.Credits);
        }

        [UnityTest]
        public IEnumerator FullMatch_CanBeLost()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            while (m.Player.Squads.Count > 0) _match.Sell(m.Player.Squads[0]);

            int rounds = 0;
            while (m.Phase != MatchPhase.MatchOver && rounds < 200)
            {
                yield return PlayRound();
                if (m.Phase == MatchPhase.Result) _match.Continue();
                rounds++;
            }
            Assert.AreEqual(Team.B, m.Winner);
        }

        [UnityTest]
        public IEnumerator FullMatch_CanBeWon()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            m.Enemy.Squads.Clear();   // The do-nothing AI's free squads leave...
            m.Enemy.BaseHealth = 1;   // ...and its base is nearly gone: any survivor finishes it.

            yield return PlayRound();
            Assert.AreEqual(MatchPhase.MatchOver, m.Phase);
            Assert.AreEqual(Team.A, m.Winner);
        }
    }
}
