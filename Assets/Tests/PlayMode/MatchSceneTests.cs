// MatchSceneTests.cs
// Plays the real Match scene (Milestone 3): starting draft, deployment preview (the AI's army stays
// hidden), buying a squad (placed in the middle, dropped in, then selected), a full round
// (deployment, battle, result, next round with income), the AI difficulty choice, and full
// matches that are won, lost, and played to the end against the Normal AI (Milestone 5).
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
        public IEnumerator Buying_PlacesTheSquadInTheMiddle_DropsItIn_ThenSelectsIt()
        {
            yield return LoadMatch();
            _match.ChooseDraft(0);
            yield return null;
            Match m = _match.Match;
            UnitStats type = m.Player.Squads[0].Type;
            int creditsBefore = m.Player.Credits;
            int viewsBefore = CountViews();

            Assert.IsTrue(DeploymentGrid.FindFreeSpot(m.Player, type, false, m.CombatRules, out int column, out int row));
            Assert.IsTrue(_match.Buy(type).Ok, _match.Message);
            yield return null;

            Assert.AreEqual(creditsBefore - type.Cost, m.Player.Credits);
            Assert.AreEqual(viewsBefore + type.SquadSize, CountViews());
            PlacedSquad bought = m.Player.Squads[m.Player.Squads.Count - 1];
            Assert.AreEqual(column, bought.Column);
            Assert.AreEqual(row, bought.Row);
            Assert.IsTrue(_match.IsDropping);
            Assert.IsNull(_match.SelectedPlacedSquad, "Selected only once it lands.");
            Transform units = GameObject.Find("Units").transform;
            Assert.Greater(units.GetChild(units.childCount - 1).position.y, 50f, "A bought squad starts high above the field.");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_match.IsDropping && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.IsFalse(_match.IsDropping);
            Assert.Less(units.GetChild(units.childCount - 1).position.y, 0.5f, "It has landed on its spot.");
            Assert.AreSame(bought, _match.SelectedPlacedSquad, "The new squad is selected when it lands.");
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
        public IEnumerator Difficulty_IsChosenOnTheDraftScreen_AndLockedAfterward()
        {
            yield return LoadMatch();
            _match.SetDifficulty(AiDifficulty.Hard);
            Assert.AreEqual(AiDifficulty.Hard, _match.Difficulty);
            Assert.AreEqual("Hard AI", _match.Match.EnemyAi.Label);
            _match.ChooseDraft(0);
            _match.SetDifficulty(AiDifficulty.Easy);
            Assert.AreEqual("Hard AI", _match.Match.EnemyAi.Label, "The difficulty cannot change once the match started.");
            _match.SetDifficulty(AiDifficulty.Normal); // Leave the remembered setting at the default for other tests.
        }

        [UnityTest]
        public IEnumerator FullMatch_AgainstTheNormalAi_IsPlayedToTheEnd()
        {
            yield return LoadMatch(seed: 21);
            _match.SetDifficulty(AiDifficulty.Normal);
            Match m = _match.Match;
            var playerAi = new RuleBasedAi(AiDifficulty.Normal, 22); // Plays the human's side.
            _match.ChooseDraft(playerAi.ChooseDraftCard(new AiView(m, Team.A), m.PlayerDraft));
            yield return null;
            Assert.AreEqual("Normal AI", m.EnemyAi.Label);

            int rounds = 0;
            while (m.Phase != MatchPhase.MatchOver && rounds < 40)
            {
                playerAi.Deploy(new AiView(m, Team.A));
                yield return PlayRound();
                Assert.Greater(m.Enemy.Squads.Count, 0, "The AI fields an army.");
                if (m.Phase == MatchPhase.Result) _match.Continue();
                rounds++;
            }
            Assert.AreEqual(MatchPhase.MatchOver, m.Phase, $"No winner after {rounds} rounds.");
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
