// SliceMechanicsTests.cs
// Tests the combat mechanics added for the slice rosters (Milestone 4): weapons with their own
// target filter (a Hornet's bombs only hit ground units), bonus damage against air (Wolverine,
// Vampire), cone splash (Flamethrower Trooper) and the timed mid-battle drop (Pelican).
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit, TestUnits, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SliceMechanicsTests
    {
        private static WeaponStats Harmless() => TestUnits.Weapon(damage: 0f, range: 0.5f);

        /// <summary>A unit that never moves and never hurts anyone (a target dummy).</summary>
        private static UnitStats Dummy(bool air = false, float health = 10000f) =>
            TestUnits.Unit(name: air ? "Air Dummy" : "Dummy", health: health, speed: 0f, air: air, weapon: Harmless());

        // ------------------------------------------------------------------ per-weapon target filter

        [Test]
        public void WeaponWithItsOwnFilter_OverridesTheUnitsFilter()
        {
            WeaponStats bombs = TestUnits.Weapon();
            bombs.HasOwnTargetFilter = true;
            bombs.TargetFilter = TargetFilter.Ground;
            WeaponStats rockets = TestUnits.Weapon();
            UnitStats hornet = TestUnits.Unit(air: true, filter: TargetFilter.Both, weapon: rockets);
            hornet.Weapons = new[] { rockets, bombs };

            Assert.IsTrue(rockets.CanTarget(hornet, targetIsAir: true), "Without its own filter a weapon uses the unit's.");
            Assert.IsTrue(bombs.CanTarget(hornet, targetIsAir: false));
            Assert.IsFalse(bombs.CanTarget(hornet, targetIsAir: true), "Bombs only hit ground units.");

            var units = new List<SimUnit>
            {
                new SimUnit(0, Team.A, -1, hornet, new Vec2(0f, 0f), Vec2.Up),
                new SimUnit(1, Team.B, -1, Dummy(air: true), new Vec2(0f, 3f), Vec2.Down),
                new SimUnit(2, Team.B, -1, Dummy(), new Vec2(0f, 8f), Vec2.Down),
            };
            Assert.AreSame(units[1], Targeting.FindClosestLegalTarget(units[0], rockets, units), "Rockets pick the closest unit, air or not.");
            Assert.AreSame(units[2], Targeting.FindClosestLegalTarget(units[0], bombs, units), "Bombs skip the closer air unit.");
        }

        [Test]
        public void SplashFollowsTheWeaponsFilter()
        {
            WeaponStats bomb = TestUnits.Weapon(damage: 50f, range: 10f, splash: 5f);
            bomb.HasOwnTargetFilter = true;
            bomb.TargetFilter = TargetFilter.Ground;
            UnitStats bomber = TestUnits.Unit(speed: 0f, filter: TargetFilter.Both, weapon: bomb);

            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(bomber, Team.A, new Vec2(0f, 0f));
            SimUnit ground = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 6f));
            SimUnit air = sim.AddUnit(Dummy(air: true), Team.B, new Vec2(1f, 6f));
            sim.Step();

            Assert.Less(ground.Health, ground.Stats.MaxHealth, "The ground target is hit.");
            Assert.AreEqual(air.Stats.MaxHealth, air.Health, "Ground-only splash does not hit the air unit next to it.");
        }

        // ------------------------------------------------------------------ bonus damage against air

        [TestCase(true, 30f)]
        [TestCase(false, 10f)]
        public void AirDamageMultiplier_OnlyAppliesToAirTargets(bool targetIsAir, float expectedDamage)
        {
            WeaponStats rockets = TestUnits.Weapon(damage: 10f, range: 10f);
            rockets.AirDamageMultiplier = 3f;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: rockets), Team.A, new Vec2(0f, 0f));
            SimUnit target = sim.AddUnit(Dummy(air: targetIsAir), Team.B, new Vec2(0f, 5f));
            sim.Step();
            Assert.AreEqual(target.Stats.MaxHealth - expectedDamage, target.Health, 1e-3f);
        }

        // ------------------------------------------------------------------ cone splash

        [Test]
        public void ConeWeapon_HitsEnemiesInsideTheCone_UpToItsRange()
        {
            WeaponStats flamer = TestUnits.Weapon(damage: 10f, range: 7f, type: DamageType.Fire);
            flamer.ConeDegrees = 60f;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: flamer), Team.A, new Vec2(0f, 0f));
            SimUnit direct = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 4f));
            SimUnit inside = sim.AddUnit(Dummy(), Team.B, new Vec2(1.5f, 5f));   // About 17 degrees off: inside.
            SimUnit outside = sim.AddUnit(Dummy(), Team.B, new Vec2(5f, 4f));    // About 51 degrees off: outside.
            SimUnit tooFar = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 9f));     // In the cone, past the range.
            sim.Step();

            float hit = direct.Stats.MaxHealth - 15f; // Fire does 1.5x to unarmored.
            Assert.AreEqual(hit, direct.Health, 1e-3f);
            Assert.AreEqual(hit, inside.Health, 1e-3f, "Inside the cone.");
            Assert.AreEqual(outside.Stats.MaxHealth, outside.Health, "Outside the cone's angle.");
            Assert.AreEqual(tooFar.Stats.MaxHealth, tooFar.Health, "Beyond the weapon's range.");
        }

        // ------------------------------------------------------------------ mid-battle drop

        private static UnitStats Carrier(float delay, int bodies = 6)
        {
            UnitStats pelican = TestUnits.Unit(name: "Carrier", health: 500f, speed: 0f, air: true, weapon: Harmless());
            pelican.Reinforcement = TestUnits.Unit(name: "Dropped Marine", health: 60f, speed: 0f, weapon: Harmless(), squadSize: bodies);
            pelican.ReinforcementDelaySeconds = delay;
            return pelican;
        }

        [Test]
        public void Reinforcement_DropsOneSquadUnderTheCarrier_AfterItsDelay()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(Carrier(delay: 1f), Team.A, new Vec2(10f, -20f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 100f));
            int dropEvents = 0;
            sim.Events.ReinforcementDropped += (c, squadId) => dropEvents++;

            while (sim.ElapsedSeconds < 0.9f) sim.Step();
            Assert.AreEqual(2, sim.Units.Count, "Nothing dropped before the delay.");

            while (sim.ElapsedSeconds < 1.5f) sim.Step();
            List<SimUnit> dropped = sim.Units.Where(u => u.Stats.Name == "Dropped Marine").ToList();
            Assert.AreEqual(6, dropped.Count);
            Assert.AreEqual(1, dropEvents, "Dropped exactly once.");
            Assert.IsTrue(dropped.All(u => u.Team == Team.A && u.SquadId == dropped[0].SquadId && u.SquadId >= 0));
            Assert.IsTrue(dropped.All(u => Vec2.Distance(u.Position, carrier.Position) < 4f), "They land under the carrier.");
            Assert.IsTrue(sim.SquadById(dropped[0].SquadId).IsIntact, "The dropped squad forms up like any other.");

            while (sim.ElapsedSeconds < 5f) sim.Step();
            Assert.AreEqual(8, sim.Units.Count, "Still only one drop.");
        }

        [Test]
        public void Reinforcement_NotDroppedIfTheCarrierDiedFirst()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(Carrier(delay: 2f), Team.A, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(health: 10000f, speed: 0f, weapon: TestUnits.Weapon(damage: 1000f, range: 20f)), Team.B, new Vec2(0f, 10f));
            while (!sim.IsFinished && sim.ElapsedSeconds < 3f) sim.Step();
            Assert.IsFalse(carrier.IsAlive);
            Assert.IsFalse(sim.Units.Any(u => u.Stats.Name == "Dropped Marine"));
        }

        [Test]
        public void DroppedUnits_CountInTheBattleResult()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(Carrier(delay: 0.5f, bodies: 4), Team.A, new Vec2(0f, -20f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 100f));
            BattleResult result = sim.Run(); // Nobody can hurt anybody: runs to the time limit.

            UnitTypeResult marines = result.TeamA.UnitTypes.Single(t => t.UnitName == "Dropped Marine");
            Assert.AreEqual(4, marines.Deployed);
            Assert.AreEqual(4, marines.Survivors, "Dropped units still alive at the end deal base damage too.");
            Assert.AreEqual(5, result.TeamA.Survivors);
        }

        [Test]
        public void Validate_ChecksTheNewFields()
        {
            UnitStats unit = TestUnits.Unit();
            unit.Weapons[0].AirDamageMultiplier = -1f;
            unit.Weapons[0].ConeDegrees = 400f;
            unit.Reinforcement = TestUnits.Unit(squadSize: 0);
            unit.ReinforcementDelaySeconds = -1f;
            List<string> problems = unit.Validate();
            Assert.IsTrue(problems.Any(p => p.Contains("against air")));
            Assert.IsTrue(problems.Any(p => p.Contains("cone")));
            Assert.IsTrue(problems.Any(p => p.Contains("reinforcement delay")));
            Assert.IsTrue(problems.Any(p => p.Contains("reinforcement:") && p.Contains("squad size")));

            UnitStats copy = Carrier(1f).Clone();
            Assert.IsNotNull(copy.Reinforcement);
            Assert.AreNotSame(Carrier(1f).Reinforcement, copy.Reinforcement);
        }
    }
}
