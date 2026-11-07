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

        private static UnitStats LandingCarrier()
        {
            UnitStats pelican = TestUnits.Unit(name: "Lander", health: 100000f, speed: 12f, air: true, radius: 2.5f, weapon: Harmless());
            pelican.Reinforcement = TestUnits.Unit(name: "Dropped Marine", health: 100000f, speed: 0f, weapon: Harmless(), squadSize: 6);
            pelican.ReinforcementDelaySeconds = 5f;
            pelican.ReinforcementDrops = 3;
            pelican.ReinforcementLandSeconds = 1f;
            return pelican;
        }

        [Test]
        public void ALandingCarrier_LandsBehindTheEnemy_LetsItsSquadOutFromUnderIt_ThreeTimes_AndGoesBackUp()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(LandingCarrier(), Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(10f, 20f));
            float lowest = 1f;
            int drops = 0;
            Vec2 firstDrop = Vec2.Zero;
            sim.Events.ReinforcementDropped += (c, squadId) =>
            {
                drops++;
                SimSquad squad = sim.SquadById(squadId);
                Assert.IsFalse(c.IsAir, "It is on the ground when they get out.");
                Assert.AreEqual(6, squad.Members.Count);
                if (drops == 1)
                {
                    firstDrop = c.Position;
                    Assert.Greater(c.Position.Y, 20f, "Behind the enemy.");
                    Assert.Less(Vec2.Distance(squad.Members[0].Position, c.Position), 16f);
                }
                Vec2 middle = Vec2.Zero;
                foreach (SimUnit member in squad.Members) middle += member.Position;
                middle = middle * (1f / squad.Members.Count);
                Assert.LessOrEqual(Vec2.Distance(middle, c.Position), c.Radius + 0.5f, "They come out from under it, its troop bay.");
                Assert.GreaterOrEqual(Vec2.Dot(middle - c.Position, -c.Facing), 0f, "Not ahead of it.");
            };
            while (sim.ElapsedSeconds < 45f && !sim.IsFinished)
            {
                sim.Step();
                lowest = System.Math.Min(lowest, carrier.Altitude);
            }
            Assert.AreEqual(3, drops);
            Assert.AreEqual(0f, lowest, 1e-3f);
            Assert.AreEqual(1f, carrier.Altitude, 1e-3f, "Back up.");
            Assert.IsTrue(carrier.IsAir);
        }

        [Test]
        public void ALandingCarrier_ThatDoesntHaveItsReinforcement_JustFlies()
        {
            UnitStats stats = LandingCarrier();
            stats.LockedReinforcement = stats.Reinforcement;
            stats.Reinforcement = null;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(stats, Team.A, new Vec2(0f, -40f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(10f, 20f));
            while (sim.ElapsedSeconds < 20f) sim.Step();
            Assert.AreEqual(2, sim.Units.Count);
            Assert.AreEqual(1f, carrier.Altitude);
        }

        [Test]
        public void ALandedCarrier_CanBeHitByGroundOnlyWeapons_AndAFlyingOneCannot()
        {
            WeaponStats groundOnly = TestUnits.Weapon(damage: 100f, range: 100f, interval: 0.2f);
            groundOnly.HasOwnTargetFilter = true;
            groundOnly.TargetFilter = TargetFilter.Ground;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit carrier = sim.AddUnit(LandingCarrier(), Team.A, new Vec2(0f, -10f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f, filter: TargetFilter.Ground, weapon: groundOnly), Team.B, new Vec2(0f, 40f));
            while (carrier.DropPhase == DropRunPhase.Flying && sim.ElapsedSeconds < 30f && !sim.IsFinished) sim.Step();
            Assert.AreEqual(100000f, carrier.Health, "Not while it flies.");
            while (carrier.DropsMade == 0 && !sim.IsFinished) sim.Step();
            while (carrier.DropPhase == DropRunPhase.Ascending && !carrier.IsAir && !sim.IsFinished) sim.Step();
            float steps = 0f;
            while (steps++ < 2f / sim.TickSeconds && !sim.IsFinished) sim.Step();
            Assert.Less(carrier.Health, 100000f, "It was shot on the ground.");
        }

        [Test]
        public void AUnitThatCantMove_StaysPut_NoMatterWhatWalksIntoIt_AndStillFires()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit pod = sim.AddUnit(TestUnits.Unit(name: "Pod", health: 100000f, speed: 0f, radius: 1.4f, weapon: TestUnits.Weapon(damage: 1f, range: 30f, interval: 0.5f)), Team.A, new Vec2(0f, -10f));
            Vec2 stood = pod.Position;
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 5f, radius: 1.4f, weapon: TestUnits.Weapon(damage: 1f, range: 1f)), Team.A, new Vec2(0f, -20f));
            SimUnit enemy = sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 5f, radius: 1.4f, weapon: TestUnits.Weapon(damage: 1f, range: 1f)), Team.B, new Vec2(0f, 20f));
            while (sim.ElapsedSeconds < 30f && !sim.IsFinished) sim.Step();
            Assert.AreEqual(stood, pod.Position);
            Assert.Less(enemy.Health, 100000f, "It fired.");
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
