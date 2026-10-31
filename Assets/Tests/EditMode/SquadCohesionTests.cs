// SquadCohesionTests.cs
// Tests squad formation cohesion: a squad marches in formation and only breaks it when ANY of
// these happens: its bodies target different enemy squads, it touches another squad, or it
// engages in combat (attacks, fires or is hit). Single-body squads never use formations.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SquadCohesionTests
    {
        private CombatRules _rules;

        [SetUp]
        public void SetUp() => _rules = TestRules.Default();

        private static UnitStats Rifle(float range = 12f) =>
            TestUnits.Unit(name: "Rifle", health: 60f, speed: 4f, radius: 0.5f, squadSize: 12,
                weapon: TestUnits.Weapon(damage: 6f, range: range, interval: 0.5f));

        /// <summary>A far-away enemy that cannot shoot back, to march toward.</summary>
        private static UnitStats Dummy() => TestUnits.Unit(name: "Dummy", health: 100000f, speed: 0f, filter: TargetFilter.Air);

        private static float WorstOffsetError(SimSquad squad)
        {
            Vec2 anchor = squad.Anchor();
            float worst = 0f;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                float error = Vec2.Distance(squad.Members[i].Position, anchor + squad.Offsets[i]);
                if (error > worst) worst = error;
            }
            return worst;
        }

        [Test]
        public void Squad_MarchesInFormation_WhileNothingBreaksIt()
        {
            var sim = new BattleSimulation(_rules, 1);
            int id = sim.AddSquad(Rifle(), Team.A, new Vec2(0f, -60f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f), sim.NewSquadId());
            SimSquad squad = sim.SquadById(id);
            Vec2 start = new Vec2(0f, -60f);

            for (int t = 0; t < 30 * 5; t++) sim.Step();

            Assert.IsTrue(squad.IsIntact, squad.BreakReason);
            Assert.Greater(Vec2.Distance(squad.Anchor(), start), 15f, "The squad should have advanced.");
            Assert.Less(WorstOffsetError(squad), 0.5f, "Every body should still be in its place in the formation.");
        }

        [Test]
        public void Squad_BreaksFormation_WhenItEngagesInCombat()
        {
            var sim = new BattleSimulation(_rules, 1);
            int id = sim.AddSquad(Rifle(), Team.A, new Vec2(0f, -10f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f), sim.NewSquadId());
            SimSquad squad = sim.SquadById(id);

            do { sim.Step(); } while (squad.IsIntact && sim.Tick < 30 * 4);
            Assert.IsFalse(squad.IsIntact);
            Assert.AreEqual("engaged in combat", squad.BreakReason);
        }

        [Test]
        public void Squad_BreaksFormation_WhenHitFromOutOfItsRange()
        {
            var sim = new BattleSimulation(_rules, 1);
            int id = sim.AddSquad(Rifle(range: 5f), Team.A, new Vec2(0f, -20f));
            var sniper = TestUnits.Unit(name: "Sniper", health: 100000f, speed: 0f, weapon: TestUnits.Weapon(damage: 1f, range: 60f, interval: 0.2f));
            sim.AddUnit(sniper, Team.B, new Vec2(0f, 20f), sim.NewSquadId());
            SimSquad squad = sim.SquadById(id);

            sim.Step(); // The sniper hits a body this tick.
            sim.Step(); // The squad notices.
            Assert.IsFalse(squad.IsIntact);
            Assert.AreEqual("engaged in combat", squad.BreakReason);
        }

        [Test]
        public void Squad_BreaksFormation_WhenItsBodiesTargetDifferentSquads()
        {
            // Two enemy squads ahead-left and ahead-right, both within acquisition range (3 x 12 m) but out of range.
            var sim = new BattleSimulation(_rules, 1);
            int id = sim.AddSquad(Rifle(), Team.A, new Vec2(0f, -20f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(-20f, 5f), sim.NewSquadId());
            sim.AddUnit(Dummy(), Team.B, new Vec2(20f, 5f), sim.NewSquadId());
            SimSquad squad = sim.SquadById(id);

            sim.Step();
            Assert.IsFalse(squad.IsIntact);
            Assert.AreEqual("targets in different squads", squad.BreakReason);
        }

        [Test]
        public void Squad_BreaksFormation_WhenItTouchesAnotherSquad()
        {
            // A friendly squad stands in the way of a marching squad.
            var sim = new BattleSimulation(_rules, 1);
            int marching = sim.AddSquad(Rifle(), Team.A, new Vec2(0f, -60f));
            var blocker = TestUnits.Unit(name: "Wall", health: 1000f, speed: 0f, radius: 0.5f, squadSize: 12, filter: TargetFilter.Air);
            sim.AddSquad(blocker, Team.A, new Vec2(0f, -45f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f), sim.NewSquadId());
            SimSquad squad = sim.SquadById(marching);

            do { sim.Step(); } while (squad.IsIntact && sim.Tick < 30 * 8);
            Assert.IsFalse(squad.IsIntact);
            Assert.AreEqual("collided with another squad", squad.BreakReason);
        }

        [Test]
        public void SingleBodySquads_NeverUseFormations()
        {
            var sim = new BattleSimulation(_rules, 1);
            int id = sim.AddSquad(TestUnits.Unit(squadSize: 1), Team.A, new Vec2(0f, -60f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f), sim.NewSquadId());
            sim.Step();
            Assert.IsFalse(sim.SquadById(id).IsIntact);
        }

        [Test]
        public void SquadIds_AreUniqueAcrossAddSquadAndTheRunnerLayout()
        {
            var sim = new BattleSimulation(_rules, 1);
            int first = sim.AddSquad(Rifle(), Team.A, new Vec2(-20f, -30f));
            ArmyLayout.Place(sim, _rules, new ArmySetup("A").Add(Rifle(), 2), Team.A,
                new RunnerSettings { FrontLineDistance = 10f, SquadGap = 3f }, new SimRandom(1));
            int last = sim.AddSquad(Rifle(), Team.B, new Vec2(20f, 30f));
            Assert.AreEqual(4, sim.Squads.Count);
            Assert.AreNotEqual(first, last);
            foreach (SimSquad squad in sim.Squads) Assert.AreEqual(12, squad.Members.Count);
        }
    }
}
