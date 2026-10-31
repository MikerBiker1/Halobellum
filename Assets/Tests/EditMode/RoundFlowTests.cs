// RoundFlowTests.cs
// Tests round flow and base damage (Bible Section 4, rule 10): deployment, battle and result phases;
// survivors on both sides damage the enemy base at the same time; the match ends when a base falls
// (lower base loses if both fall; an exact tie replays the round); the battlefield resets every
// round; and a full match against the do-nothing AI can be won and lost.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch.

using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class RoundFlowTests
    {
        private static RoundSummary PlayBattle(Match match)
        {
            BattleSimulation battle = match.StartBattle();
            battle.Run();
            return match.FinishBattle();
        }

        [Test]
        public void Phases_GoDraftDeploymentBattleResultDeployment()
        {
            var match = new Match(TestMatch.Rules(), TestRules.Default(), TestMatch.StandardFactions(out _, out _, out _, out _), 1, new DoNothingAi());
            Assert.AreEqual(MatchPhase.Draft, match.Phase);
            match.ChooseDraft(0);
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
            match.StartBattle();
            Assert.AreEqual(MatchPhase.Battle, match.Phase);
            match.CurrentBattle.Run();
            match.FinishBattle();
            Assert.AreEqual(MatchPhase.Result, match.Phase);
            match.ContinueAfterResult();
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
            Assert.AreEqual(2, match.Round);
        }

        [Test]
        public void Battle_HasThe90SecondLimit()
        {
            var match = TestMatch.Started(out _, out _, out _, out _);
            BattleSimulation battle = match.StartBattle();
            battle.Run();
            Assert.LessOrEqual(battle.ElapsedSeconds, 90f + 1e-3f);
        }

        [Test]
        public void SurvivorsOnBothSides_DamageTheEnemyBase_AtTheSameTime()
        {
            // Two armies that cannot hurt each other: everyone survives to the time limit.
            var match = TestMatch.Started(out UnitStats infantry, out UnitStats buggy, out _, out _);
            infantry.TargetFilter = TargetFilter.Air;
            buggy.TargetFilter = TargetFilter.Air;
            float perSide = 0f;
            foreach (PlacedSquad squad in match.Player.Squads) perSide += squad.Type.BaseDamage * squad.Type.SquadSize;

            RoundSummary summary = PlayBattle(match);
            Assert.IsTrue(summary.Battle.TimedOut);
            Assert.AreEqual((int)System.MathF.Round(perSide), summary.BaseDamageByA);
            Assert.AreEqual(summary.BaseDamageByA, summary.BaseDamageByB, "Both draft cards here have the same squads.");
            Assert.AreEqual(2000 - summary.BaseDamageByB, match.Player.BaseHealth);
            Assert.AreEqual(2000 - summary.BaseDamageByA, match.Enemy.BaseHealth);
        }

        [Test]
        public void AWipedOutSide_DealsNoBaseDamage()
        {
            var match = TestMatch.Started(out _, out _, out _, out _);
            match.Player.Squads.Clear(); // The player brings nothing.
            RoundSummary summary = PlayBattle(match);
            Assert.AreEqual(0, summary.BaseDamageByA);
            Assert.Greater(summary.BaseDamageByB, 0);
            Assert.AreEqual(2000, match.Enemy.BaseHealth);
        }

        [Test]
        public void RoundReset_EverySquadIsBackInPlaceNextRound()
        {
            var match = TestMatch.Started(out _, out _, out _, out _);
            int bodies = match.BuildBattle(includeEnemy: true).Units.Count;
            PlayBattle(match);
            Assert.Less(match.CurrentBattle.Result.TeamA.Survivors + match.CurrentBattle.Result.TeamB.Survivors, bodies, "Some units should die.");
            match.ContinueAfterResult();
            BattleSimulation next = match.BuildBattle(includeEnemy: true);
            Assert.AreEqual(bodies, next.Units.Count, "Destroyed units are remade at full health in their places.");
            foreach (SimUnit unit in next.Units) Assert.AreEqual(unit.Stats.MaxHealth, unit.Health);
        }

        [Test]
        public void BaseAtZero_LosesTheMatch()
        {
            var match = TestMatch.Started(out _, out _, out _, out _);
            match.Player.Squads.Clear();
            match.Player.BaseHealth = 1;
            RoundSummary summary = PlayBattle(match);
            Assert.AreEqual(Team.B, summary.MatchWinner);
            Assert.AreEqual(MatchPhase.MatchOver, match.Phase);
            Assert.AreEqual(Team.B, match.Winner);
        }

        [Test]
        public void BothBasesFall_TheLowerOneLoses()
        {
            var match = TestMatch.Started(out UnitStats infantry, out UnitStats buggy, out _, out _);
            infantry.TargetFilter = TargetFilter.Air; // Nobody can fight: equal damage both ways.
            buggy.TargetFilter = TargetFilter.Air;
            match.Player.BaseHealth = 5;
            match.Enemy.BaseHealth = 10;
            RoundSummary summary = PlayBattle(match);
            Assert.Less(match.Player.BaseHealth, match.Enemy.BaseHealth);
            Assert.LessOrEqual(match.Enemy.BaseHealth, 0);
            Assert.AreEqual(Team.B, summary.MatchWinner);
        }

        [Test]
        public void ExactTie_ReplaysTheRound()
        {
            var match = TestMatch.Started(out UnitStats infantry, out UnitStats buggy, out _, out _);
            infantry.TargetFilter = TargetFilter.Air;
            buggy.TargetFilter = TargetFilter.Air;
            match.Player.BaseHealth = 10;
            match.Enemy.BaseHealth = 10;
            int creditsBefore = match.Player.Credits;

            RoundSummary summary = PlayBattle(match);
            Assert.IsTrue(summary.Replay);
            Assert.IsNull(summary.MatchWinner);
            Assert.AreEqual(10, match.Player.BaseHealth, "Damage is undone.");
            Assert.AreEqual(10, match.Enemy.BaseHealth);
            Assert.AreEqual(creditsBefore, match.Player.Credits, "No income for a replayed round.");

            match.ContinueAfterResult();
            Assert.AreEqual(1, match.Round);
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
        }

        [Test]
        public void FullMatch_AgainstTheDoNothingAI_CanBeWon()
        {
            var match = TestMatch.Started(out UnitStats infantry, out _, out _, out _, seed: 5);
            int rounds = 0;
            while (match.Phase != MatchPhase.MatchOver && rounds < 60)
            {
                // Spend: two infantry squads per round while there is room and money.
                for (int i = 0; i < 2; i++)
                {
                    if (DeploymentGrid.FindFreeSpot(match.Player, infantry, false, match.CombatRules, out int column, out int row))
                        match.Buy(Team.A, infantry, column, row, false);
                }
                PlayBattle(match);
                match.ContinueAfterResult();
                rounds++;
            }
            Assert.AreEqual(Team.A, match.Winner, $"Expected to win within 60 rounds (after {rounds}).");
            Assert.LessOrEqual(match.Enemy.BaseHealth, 0);
        }

        [Test]
        public void FullMatch_AgainstTheDoNothingAI_CanBeLost()
        {
            // The player sells their starting squads and never buys anything.
            var match = TestMatch.Started(out _, out _, out _, out _, seed: 5);
            while (match.Player.Squads.Count > 0) match.Sell(Team.A, match.Player.Squads[0].Id);
            int rounds = 0;
            while (match.Phase != MatchPhase.MatchOver && rounds < 200)
            {
                PlayBattle(match);
                match.ContinueAfterResult();
                rounds++;
            }
            Assert.AreEqual(Team.B, match.Winner);
            Assert.LessOrEqual(match.Player.BaseHealth, 0);
        }

        [Test]
        public void SameMatchSeedAndActions_GiveTheSameMatch()
        {
            string Play(int seed)
            {
                var match = TestMatch.Started(out UnitStats infantry, out _, out _, out _, seed: seed);
                match.Buy(Team.A, infantry, 0, 5, false);
                RoundSummary a = PlayBattle(match);
                match.ContinueAfterResult();
                RoundSummary b = PlayBattle(match);
                return a.Battle.ToSummaryLine() + " | " + b.Battle.ToSummaryLine();
            }
            Assert.AreEqual(Play(11), Play(11));
        }

        [Test]
        public void KillSummary_CountsWhatKilledWhat()
        {
            var match = TestMatch.Started(out _, out _, out _, out _);
            RoundSummary summary = PlayBattle(match);
            int kills = 0;
            foreach (KillCount count in summary.Kills) kills += count.Kills;
            Assert.AreEqual(summary.Battle.TeamA.Kills + summary.Battle.TeamB.Kills, kills);
        }
    }
}
