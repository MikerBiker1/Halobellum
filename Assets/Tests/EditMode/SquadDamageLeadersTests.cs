// SquadDamageLeadersTests.cs
// Tests which squad of each side leads in damage dealt this battle (the glow under the top squads): nobody before damage lands;
// the squad with the most leads its side; the squad already leading keeps it on a tie; bodies a squad brought and its seeds count
// for it, a body on the other side counts for nobody; squads made in the battle never lead; the leader's number is the Battle
// stats "Dealt" column's; a new battle starts again.
// Depends on: Halobellum.Combat, Halobellum.Economy, Halobellum.Core, NUnit, TestRules, TestUnits, TestMatch.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SquadDamageLeadersTests
    {
        /// <summary>A battle with two Team A squads and one Team B squad, nothing run.</summary>
        private static BattleSimulation TwoAgainstOne()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats unit = TestUnits.Unit("Rifle", speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 5f));
            sim.AddSquad(unit, Team.A, new Vec2(-10f, -20f));
            sim.AddSquad(unit, Team.A, new Vec2(10f, -20f));
            sim.AddSquad(unit, Team.B, new Vec2(0f, 20f));
            return sim;
        }

        private static SimUnit FirstBody(BattleSimulation sim, int squadIndex) => sim.Squads[squadIndex].Members[0];

        [Test]
        public void NoSquadLeads_BeforeAnyDamage()
        {
            BattleSimulation sim = TwoAgainstOne();
            var leaders = new SquadDamageLeaders();
            leaders.Count(sim, sim.Squads.Count);
            Assert.AreEqual(-1, leaders.TopA);
            Assert.AreEqual(-1, leaders.TopB);
        }

        [Test]
        public void TheSquadWithTheMostDamage_LeadsItsSide()
        {
            BattleSimulation sim = TwoAgainstOne();
            FirstBody(sim, 0).DamageDealt = 10f;
            FirstBody(sim, 1).DamageDealt = 30f;
            FirstBody(sim, 2).DamageDealt = 5f;
            var leaders = new SquadDamageLeaders();
            leaders.Count(sim, sim.Squads.Count);
            Assert.AreEqual(1, leaders.TopA);
            Assert.AreEqual(2, leaders.TopB);
            Assert.AreEqual(30f, leaders.DealtBy(1), 0.001f);
        }

        [Test]
        public void ATie_KeepsTheSquadAlreadyShown_ThenALeadMovesIt()
        {
            BattleSimulation sim = TwoAgainstOne();
            var leaders = new SquadDamageLeaders();
            FirstBody(sim, 0).DamageDealt = 30f;
            leaders.Count(sim, sim.Squads.Count);
            Assert.AreEqual(0, leaders.TopA);
            FirstBody(sim, 1).DamageDealt = 30f;
            leaders.Count(sim, sim.Squads.Count);
            Assert.AreEqual(0, leaders.TopA, "On a tie the squad already glowing keeps it.");
            FirstBody(sim, 1).DamageDealt = 31f;
            leaders.Count(sim, sim.Squads.Count);
            Assert.AreEqual(1, leaders.TopA, "Once passed, the lead moves.");
        }

        [Test]
        public void BodiesItBrought_AndSeedDamage_CountForTheSquad_AndABodyOnTheOtherSide_CountsForNobody()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats unit = TestUnits.Unit("Rifle", speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 5f));
            int a1 = sim.AddSquad(unit, Team.A, new Vec2(-10f, -20f));
            sim.AddSquad(unit, Team.B, new Vec2(0f, 20f));
            sim.AddSquad(unit, Team.A, new Vec2(10f, -20f)); // Brought into the battle by A1 (a drop or a spawn).
            SimUnit dropped = FirstBody(sim, 2);
            dropped.CreditSquadId = a1;
            dropped.DamageDealt = 50f;
            FirstBody(sim, 0).SeedDamageDealt = 20f;
            FirstBody(sim, 1).DamageDealt = 5f;

            var leaders = new SquadDamageLeaders();
            leaders.Count(sim, 2);
            Assert.AreEqual(70f, leaders.DealtBy(0), 0.001f);
            Assert.AreEqual(0, leaders.TopA);
            Assert.AreEqual(1, leaders.TopB);

            sim.AddSquad(unit, Team.B, new Vec2(5f, 20f)); // Risen for the other side, credited to A1.
            SimUnit risen = FirstBody(sim, 3);
            risen.CreditSquadId = a1;
            risen.DamageDealt = 999f;
            leaders.Count(sim, 2);
            Assert.AreEqual(70f, leaders.DealtBy(0), 0.001f, "A body on the other side counts for nobody.");
            Assert.AreEqual(1, leaders.TopB);
        }

        [Test]
        public void SquadsMadeInTheBattle_NeverLead()
        {
            BattleSimulation sim = TwoAgainstOne();
            FirstBody(sim, 0).DamageDealt = 10f;
            UnitStats unit = TestUnits.Unit("Spawn", speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 5f));
            sim.AddSquad(unit, Team.A, new Vec2(0f, -25f));
            FirstBody(sim, 3).DamageDealt = 500f; // Credited to its own (made in battle) squad.
            var leaders = new SquadDamageLeaders();
            leaders.Count(sim, 3);
            Assert.AreEqual(0, leaders.TopA);
            Assert.AreEqual(0f, leaders.DealtBy(3));
        }

        [Test]
        public void TheLeader_MatchesTheBattleStatsDealtColumn()
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            Assert.IsTrue(match.Buy(Team.A, infantry, 0, 5, false).Ok);
            BattleSimulation battle = match.StartBattle();
            BattleStats stats = match.BattleHistory.Single();
            battle.Run();
            match.FinishBattle();

            var leaders = new SquadDamageLeaders();
            leaders.Count(battle, match.Player.Squads.Count + match.Enemy.Squads.Count);
            foreach (Team team in new[] { Team.A, Team.B })
            {
                var squads = stats.Side(team, byReceived: false).Where(l => l.Kind == BattleStatsKind.Squad).ToList();
                float most = squads.Count > 0 ? squads.Max(l => l.DamageDealt) : 0f;
                int top = team == Team.A ? leaders.TopA : leaders.TopB;
                if (most <= 0f)
                {
                    Assert.AreEqual(-1, top, $"Team {team}: nobody dealt damage, nobody leads.");
                    continue;
                }
                Assert.GreaterOrEqual(top, 0, $"Team {team}");
                Assert.AreEqual(team, battle.Squads[top].Team);
                Assert.AreEqual(most, leaders.DealtBy(top), 0.5f, $"Team {team}: the leader dealt what the window's top line says.");
            }
            Assert.GreaterOrEqual(leaders.TopA, 0, "The player's squads dealt damage in this battle.");
        }

        [Test]
        public void ANewBattle_StartsAgain()
        {
            BattleSimulation first = TwoAgainstOne();
            FirstBody(first, 0).DamageDealt = 10f;
            FirstBody(first, 2).DamageDealt = 10f;
            var leaders = new SquadDamageLeaders();
            leaders.Count(first, first.Squads.Count);
            Assert.AreEqual(0, leaders.TopA);
            Assert.AreEqual(2, leaders.TopB);

            BattleSimulation second = TwoAgainstOne();
            leaders.Count(second, second.Squads.Count);
            Assert.AreEqual(-1, leaders.TopA);
            Assert.AreEqual(-1, leaders.TopB);

            leaders.Count(null, 0);
            Assert.AreEqual(-1, leaders.TopA);
        }
    }
}
