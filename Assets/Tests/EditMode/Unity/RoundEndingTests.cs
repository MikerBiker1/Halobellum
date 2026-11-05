// RoundEndingTests.cs
// The nodes of light at the end of a round: who sends them, how they move, and when the round's window may open.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class RoundEndingTests
    {
        /// <summary>When each node hits, by its damage to the base.</summary>
        private static float HitTime(RoundEnding ending, float damage)
        {
            RoundEnding.Node node = ending.Nodes.First(n => n.Damage == damage);
            return node.Starts + node.Seconds;
        }

        private static BattleSimulation Battle(params (Team team, float damage, bool alive)[] units)
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            float x = 0f;
            foreach ((Team team, float damage, bool alive) in units)
            {
                UnitStats stats = TestUnits.Unit(health: 10f, speed: 0f);
                stats.BaseDamage = damage;
                SimUnit unit = sim.AddUnit(stats, team, new Vec2(x += 5f, team == Team.A ? -10f : 10f));
                if (!alive) unit.State = UnitState.Dead;
            }
            return sim;
        }

        private static RoundEnding Ending(BattleSimulation sim) => new RoundEnding(sim, unit => new Vector3(unit.Position.X, 0f, unit.Position.Y));

        [Test]
        public void EverySurvivor_SendsANode_AtTheOtherBase()
        {
            RoundEnding ending = Ending(Battle((Team.A, 10f, true), (Team.A, 5f, false), (Team.B, 20f, true)));
            Assert.AreEqual(2, ending.Nodes.Count, "Not the dead.");
            Assert.AreEqual(Team.B, ending.Nodes.Single(n => n.Damage == 10f).Target);
            Assert.AreEqual(Team.A, ending.Nodes.Single(n => n.Damage == 20f).Target);
        }

        [Test]
        public void TheBaseGoesDownHitByHit_WithANumberForEach()
        {
            RoundEnding ending = Ending(Battle((Team.A, 10f, true), (Team.A, 25f, true), (Team.A, 1.1111f, true)));
            int hits = 0;
            ending.Hit += _ => hits++;
            Assert.AreEqual(0, ending.HitsTaken(Team.B));
            int last = 0;
            for (int i = 0; i < 200 && !ending.IsDone; i++)
            {
                ending.Tick(0.02f);
                Assert.GreaterOrEqual(ending.HitsTaken(Team.B), last);
                last = ending.HitsTaken(Team.B);
                if (hits > 0 && hits < 3) Assert.Greater(ending.Numbers.Count, 0);
            }
            Assert.IsTrue(ending.IsDone);
            Assert.AreEqual(3, hits);
            Assert.AreEqual(36, ending.HitsTaken(Team.B), "Rounded as the round's result rounds it.");
            Assert.AreEqual(0, ending.HitsTaken(Team.A));
        }

        [Test]
        public void TheBiggerUnitsHit_AfterTheSmallOnes_WithAPause_AndEachTitanAfterALongerOne()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            float x = 0f;
            foreach ((float damage, UnitRole role) in new[] { (160f, UnitRole.Titan), (45f, UnitRole.Tank), (1f, UnitRole.Chaff), (250f, UnitRole.Titan), (2f, UnitRole.Chaff), (100f, UnitRole.Tank) })
            {
                UnitStats stats = TestUnits.Unit(health: 10f, speed: 0f);
                stats.BaseDamage = damage;
                stats.Role = role;
                sim.AddUnit(stats, Team.A, new Vec2(x += 5f, -10f));
            }
            RoundEnding ending = Ending(sim);
            foreach (RoundEnding.Node node in ending.Nodes) Assert.GreaterOrEqual(node.Starts, 0f, "No node leaves before the ending begins.");

            float small = Mathf.Max(HitTime(ending, 1f), HitTime(ending, 2f));
            Assert.GreaterOrEqual(Mathf.Min(HitTime(ending, 45f), HitTime(ending, 100f)), small + RoundEnding.BigUnitPause - 1e-4f);
            Assert.LessOrEqual(HitTime(ending, 45f), HitTime(ending, 100f) + 1e-4f, "Smallest first within a run, give or take.");
            float big = Mathf.Max(HitTime(ending, 45f), HitTime(ending, 100f));
            Assert.AreEqual(big + RoundEnding.TitanPause, HitTime(ending, 160f), 1e-3f);
            Assert.AreEqual(big + 2f * RoundEnding.TitanPause, HitTime(ending, 250f), 1e-3f, "Each Titan on its own.");
            Assert.Greater(RoundEnding.TitanPause, RoundEnding.BigUnitPause);
        }

        [Test]
        public void ALoneUnit_LeavesAtOnce()
        {
            foreach (float damage in new[] { 1f, 45f })
            {
                RoundEnding ending = Ending(Battle((Team.A, damage, true)));
                Assert.AreEqual(0f, ending.Nodes[0].Starts, 1e-3f);
            }
        }

        [Test]
        public void TheWindow_WaitsASecondAfterTheLastHit()
        {
            RoundEnding ending = Ending(Battle((Team.A, 10f, true)));
            float lastHit = -1f;
            ending.Hit += _ => lastHit = ending.Clock;
            while (lastHit < 0f) ending.Tick(0.01f);
            Assert.IsFalse(ending.IsDone);
            ending.Tick(RoundEnding.PauseSeconds - 0.05f);
            Assert.IsFalse(ending.IsDone);
            ending.Tick(0.06f);
            Assert.IsTrue(ending.IsDone);
        }

        [Test]
        public void WithNobodyLeft_TheWindowStillWaitsItsSecond()
        {
            RoundEnding ending = Ending(Battle((Team.A, 10f, false), (Team.B, 10f, false)));
            Assert.AreEqual(0, ending.Nodes.Count);
            Assert.IsFalse(ending.IsDone);
            ending.Tick(RoundEnding.PauseSeconds + 0.01f);
            Assert.IsTrue(ending.IsDone);
        }

        [Test]
        public void ANode_StartsSlowly_ThenGathersSpeedFast()
        {
            Assert.AreEqual(0f, RoundEnding.Ease(0f));
            Assert.AreEqual(1f, RoundEnding.Ease(1f));
            Assert.Less(RoundEnding.Ease(0.5f), 0.1f, "Half its time gets it less than a tenth of the way.");
            Assert.Greater(RoundEnding.Ease(1f) - RoundEnding.Ease(0.9f), 10f * (RoundEnding.Ease(0.3f) - RoundEnding.Ease(0.2f)));
        }

        [Test]
        public void BiggerUnits_MakeBiggerHits()
        {
            Assert.Less(RoundEnding.SizeOf(1.1f), 0.1f);
            Assert.Less(RoundEnding.SizeOf(10f), RoundEnding.SizeOf(100f));
            Assert.AreEqual(1f, RoundEnding.SizeOf(250f), 1e-4f);
            Assert.AreEqual(1f, RoundEnding.SizeOf(1000f));

            RoundEnding ending = Ending(Battle((Team.A, 10f, true), (Team.B, 200f, true)));
            ending.Tick(5f);
            // Both bars were hit; the one hit by the bigger unit shakes harder for as long as it shakes.
            Assert.AreEqual(2, ending.Nodes.Count(n => n.Landed));
        }

        [TestCase(10f, "-10")]
        [TestCase(1.1111f, "-1.1")]
        [TestCase(0.6667f, "-0.7")]
        [TestCase(160f, "-160")]
        [TestCase(33.3333f, "-33.3")]
        public void TheNumber_ShowsTheHit(float damage, string text) => Assert.AreEqual(text, RoundEnding.HitText(damage));
    }
}
