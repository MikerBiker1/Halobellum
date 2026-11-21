// PelicanFirstDropTests.cs
// Tests a landing carrier's drops (the Pelican): it comes down where it is, never flying behind the enemy's lines (D143); the first squad is out at
// ReinforcementFirstDropSeconds (5 s) wherever the carrier is, the next ones on the delay after it (15 and 25 s), a late flank arrival starts down as it arrives,
// the squad always stands on the field in formation, with the first-drop time at 0 the first squad is out at the delay, and an EMP holds the squad back.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit, TestUnits, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class PelicanFirstDropTests
    {
        private const float OneOrTwoTicks = 0.1f;

        private static WeaponStats Harmless() => TestUnits.Weapon(damage: 0f, range: 0.5f);

        /// <summary>The Pelican's numbers with its Troop Bay tech, on a body that nothing can kill.</summary>
        private static UnitStats Pelican(float firstDrop = 5f)
        {
            UnitStats pelican = TestUnits.Unit(name: "Pelican", health: 100000f, speed: 6f, air: true, radius: 2.5f, weapon: Harmless());
            pelican.CanFlank = true;
            pelican.Reinforcement = TestUnits.Unit(name: "Marine", health: 100000f, speed: 3f, radius: 0.5f, weapon: Harmless(), squadSize: 6);
            pelican.ReinforcementDelaySeconds = 10f;
            pelican.ReinforcementDrops = 3;
            pelican.ReinforcementLandSeconds = 1.5f;
            pelican.ReinforcementFirstDropSeconds = firstDrop;
            return pelican;
        }

        private static UnitStats Dummy() => TestUnits.Unit(name: "Dummy", health: 100000f, speed: 0f, weapon: Harmless());

        /// <summary>Runs the battle until this time and gives the time of each drop.</summary>
        private static List<float> DropTimes(BattleSimulation sim, float until)
        {
            var times = new List<float>();
            sim.Events.ReinforcementDropped += (carrier, squadId) => times.Add(sim.ElapsedSeconds);
            while (sim.ElapsedSeconds < until && !sim.IsFinished) sim.Step();
            return times;
        }

        // ------------------------------------------------------------------ an EMP switches off the tech that opens the bay

        [Test]
        public void AnEmp_HoldsTheSquadBack_UntilItIsOver()
        {
            UnitStats pelican = Pelican();
            pelican.WithoutTechs = pelican.Clone();
            pelican.WithoutTechs.Reinforcement = null;

            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(pelican, Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f));
            carrier.TechsDisabledUntil = 7f;
            List<float> drops = DropTimes(sim, 8f);
            Assert.AreEqual(1, drops.Count, "It comes out once the EMP is over.");
            Assert.AreEqual(7f, drops[0], OneOrTwoTicks);
        }

        /// <summary>The older route to the same landing: a carrier whose bay really comes from its tech, on the 10 s delay rather than the 5 s first drop.</summary>
        [Test]
        public void AnEmp_HoldsTheSquadBack_OnTheLandingAfterTheDelay_WithTheBayFromARealTech()
        {
            UnitStats bare = Pelican(firstDrop: 0f);
            bare.LockedReinforcement = bare.Reinforcement;
            bare.Reinforcement = null;
            UnitStats withBay = UnitUpgrades.Apply(bare, 1, new[] { new TechUpgrade { Name = "Troop Bay", UnlocksReinforcement = true } }, null, null);
            Assert.IsTrue(withBay.HasReinforcement);
            Assert.IsFalse(withBay.WithoutTechs.HasReinforcement, "An EMP takes the bay away.");

            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(withBay, Team.A, new Vec2(0f, 8.5f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 0f));
            carrier.TechsDisabledUntil = 13f; // It touches down at 10 s, with the EMP still on.
            List<float> drops = DropTimes(sim, 14f);
            Assert.AreEqual(1, drops.Count, "It comes out once the EMP is over.");
            Assert.AreEqual(13f, drops[0], OneOrTwoTicks);
        }

        // ------------------------------------------------------------------ on time, anywhere

        [TestCase(0f, -40f, Team.A)]        // The middle of its own area.
        [TestCase(-77f, -79f, Team.A)]      // Its own back corner.
        [TestCase(77f, -79f, Team.A)]       // The other back corner.
        [TestCase(0f, 0f, Team.A)]          // No man's land.
        [TestCase(88f, 40f, Team.A)]        // A flank zone, beside the enemy's area.
        [TestCase(77f, 79f, Team.B)]        // Team B's back corner.
        [TestCase(-88f, -40f, Team.B)]      // Team B's flank zone.
        public void TheFirstSquad_IsOutAtFiveSeconds_WhereverTheCarrierIs(float x, float y, Team team)
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Pelican(), team, new Vec2(x, y));
            sim.AddUnit(Dummy(), team == Team.A ? Team.B : Team.A, new Vec2(0f, team == Team.A ? 70f : -70f));
            List<float> drops = DropTimes(sim, 6f);
            Assert.AreEqual(1, drops.Count, "One drop in the first 6 seconds.");
            Assert.AreEqual(5f, drops[0], OneOrTwoTicks);
        }

        [Test]
        public void TheCarrier_StartsDown_InTimeForTheSquadToBeOutAtFiveSeconds()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(Pelican(), Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f));
            while (sim.ElapsedSeconds < 3.4f) sim.Step();
            Assert.AreEqual(DropRunPhase.Flying, carrier.DropPhase, "Still in the air at 3.4 s.");
            while (sim.ElapsedSeconds < 3.6f) sim.Step();
            Assert.AreEqual(DropRunPhase.Descending, carrier.DropPhase, "On its way down by 3.6 s.");
            Vec2 at = carrier.Position;
            Vec2 droppedAt = Vec2.Zero;
            sim.Events.ReinforcementDropped += (c, squadId) => droppedAt = c.Position; // After that its own Marines can nudge it on the ground.
            while (carrier.DropsMade == 0) sim.Step();
            Assert.AreEqual(0f, Vec2.Distance(at, droppedAt), 0.05f, "It comes down where it is.");
        }

        [Test]
        public void AFirstDropShorterThanTheLanding_StartsDownAtOnce()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Pelican(firstDrop: 1f), Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f));
            List<float> drops = DropTimes(sim, 3f);
            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(1.5f, drops[0], OneOrTwoTicks, "The landing still takes as long as it takes.");
        }

        [Test]
        public void ALateFlankArrival_StartsDownAsItArrives_AndDropsAtElevenAndAHalfSeconds()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            int squadId = sim.AddSquad(Pelican(), Team.A, new Vec2(88f, 40f));
            sim.DelayArrival(sim.SquadById(squadId), 10f);
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f));
            List<float> drops = DropTimes(sim, 12f);
            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(11.5f, drops[0], OneOrTwoTicks);
        }

        // ------------------------------------------------------------------ on the field, in formation

        /// <summary>The Marines' spacing, in the formation SquadFormation gives 6 bodies: 2 rows of 3.</summary>
        private static void AssertInFormation(IReadOnlyList<SimUnit> squad, CombatRules rules)
        {
            float spacing = 2f * squad[0].Radius + rules.FormationGap;
            Assert.AreEqual(6, squad.Count);
            Assert.AreEqual(2f * spacing, squad.Max(u => u.Position.X) - squad.Min(u => u.Position.X), 1e-2f, "Three abreast, not squeezed against the edge.");
            Assert.AreEqual(spacing, squad.Max(u => u.Position.Y) - squad.Min(u => u.Position.Y), 1e-2f, "Two rows, not squeezed against the edge.");
        }

        private static void AssertOnTheField(IEnumerable<SimUnit> bodies, CombatRules rules)
        {
            foreach (SimUnit body in bodies)
            {
                Assert.LessOrEqual(System.Math.Abs(body.Position.X) + body.Radius, rules.ArenaWidth * 0.5f + 1e-2f, "Off the side of the field.");
                Assert.LessOrEqual(System.Math.Abs(body.Position.Y) + body.Radius, rules.ArenaLength * 0.5f + 1e-2f, "Off the end of the field.");
            }
        }

        [TestCase(88f, 40f, Team.A, 10f)]    // Starts down over the flank zone, as it arrives.
        [TestCase(-88f, 40f, Team.A, 10f)]
        [TestCase(-88f, -40f, Team.B, 10f)]
        [TestCase(77f, -79f, Team.A, 0f)]    // Its own back corner: the squad comes out behind it, off the end of the field.
        [TestCase(-77f, 79f, Team.B, 0f)]
        public void TheSquad_LandsOnTheField_InFormation_AndStaysOnIt(float x, float y, Team team, float arrivesAt)
        {
            CombatRules rules = TestRules.Default();
            var sim = new BattleSimulation(rules, 1);
            int squadId = sim.AddSquad(Pelican(), team, new Vec2(x, y));
            if (arrivesAt > 0f) sim.DelayArrival(sim.SquadById(squadId), arrivesAt);
            sim.AddUnit(Dummy(), team == Team.A ? Team.B : Team.A, new Vec2(0f, team == Team.A ? 70f : -70f));
            IReadOnlyList<SimUnit> squad = null;
            sim.Events.ReinforcementDropped += (carrier, dropped) =>
            {
                squad = sim.SquadById(dropped).Members;
                AssertOnTheField(squad, rules);
                AssertInFormation(squad, rules);
            };
            float until = arrivesAt + 8f;
            while (sim.ElapsedSeconds < until && !sim.IsFinished) sim.Step();
            Assert.IsNotNull(squad, "It dropped.");
            AssertOnTheField(squad, rules);
        }

        [Test]
        public void ACarrierLandingOverAFlankZone_StaysWhereItIs()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            int squadId = sim.AddSquad(Pelican(), Team.A, new Vec2(88f, 40f));
            SimUnit carrier = sim.SquadById(squadId).Members[0];
            sim.DelayArrival(sim.SquadById(squadId), 10f);
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f));
            while (carrier.DropsMade == 0 && sim.ElapsedSeconds < 13f && !sim.IsFinished)
            {
                sim.Step();
                Assert.AreEqual(88f, carrier.Position.X, 0.5f, "On the ground it isn't dragged back onto the field.");
            }
            Assert.AreEqual(1, carrier.DropsMade);
        }

        // ------------------------------------------------------------------ the other drops

        [Test]
        public void LaterDrops_ComeOnTheDelay_WhereTheCarrierIs_NeverBehindTheEnemy()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(Pelican(), Team.A, new Vec2(0f, -40f));
            SimUnit enemy = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 20f));
            var times = new List<float>();
            var where = new List<Vec2>();
            sim.Events.ReinforcementDropped += (c, squadId) =>
            {
                times.Add(sim.ElapsedSeconds);
                where.Add(c.Position);
            };
            float deepest = float.MinValue;
            while (sim.ElapsedSeconds < 40f && !sim.IsFinished)
            {
                sim.Step();
                deepest = System.Math.Max(deepest, carrier.Position.Y);
            }
            Assert.AreEqual(3, times.Count);
            Assert.AreEqual(5f, times[0], OneOrTwoTicks);
            Assert.AreEqual(15f, times[1], OneOrTwoTicks, "10 s after the first, as the Covenant Phantom's.");
            Assert.AreEqual(25f, times[2], OneOrTwoTicks);
            Assert.Less(deepest, enemy.Position.Y + enemy.Radius + 8f, "It never flew to the old landing spot, 8 m behind the enemy.");
        }

        [Test]
        public void WithTheFirstDropAtZero_TheFirstSquadIsOutAtTheDelay_WhereTheCarrierIs()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Pelican(firstDrop: 0f), Team.A, new Vec2(0f, -70f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 60f));
            List<float> drops = DropTimes(sim, 11f);
            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual(10f, drops[0], OneOrTwoTicks, "Down in time for the 10 s delay, wherever it is.");
        }

        [Test]
        public void WithoutTheTech_ThereIsNoDrop()
        {
            UnitStats stats = Pelican();
            stats.LockedReinforcement = stats.Reinforcement;
            stats.Reinforcement = null;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(stats, Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 70f));
            List<float> drops = DropTimes(sim, 20f);
            Assert.AreEqual(0, drops.Count);
            Assert.AreEqual(2, sim.Units.Count);
            Assert.AreEqual(1f, carrier.Altitude, "It never came down.");
        }
    }
}
