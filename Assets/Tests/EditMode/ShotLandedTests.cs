// ShotLandedTests.cs
// The simulation says where each shot lands and which weapon fired it (M10), so that sound can tell a shell from the coaxial gun beside it.
// Depends on: Halobellum.Combat, Halobellum.Core, TestUnits, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class ShotLandedTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        [Test]
        public void EachWeapon_ReportsItsOwnShots()
        {
            WeaponStats shell = TestUnits.Weapon(damage: 5f, range: 30f, interval: 1f, projectileSpeed: 40f);
            WeaponStats gun = TestUnits.Weapon(damage: 1f, range: 30f, interval: 0.25f);
            UnitStats tank = TestUnits.Unit(speed: 0f, weapon: shell);
            tank.Weapons = new[] { shell, gun };

            var sim = NewSim();
            var fired = new Dictionary<WeaponStats, int> { { shell, 0 }, { gun, 0 } };
            var landed = new Dictionary<WeaponStats, int> { { shell, 0 }, { gun, 0 } };
            sim.Events.WeaponFired += (attacker, index, target) => fired[attacker.Weapons[index].Stats]++;
            sim.Events.ShotLanded += (attacker, weapon, point) => landed[weapon]++;

            sim.AddUnit(tank, Team.A, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 1f)), Team.B, new Vec2(0f, 10f));
            for (int i = 0; i < 100; i++) sim.Step();

            Assert.Greater(landed[gun], landed[shell], "The gun fires more often than the shell.");
            Assert.AreEqual(fired[gun], landed[gun], "An instant shot lands at once.");
            Assert.LessOrEqual(landed[shell], fired[shell], "A shell may still be in the air.");
            Assert.Greater(landed[shell], 0);
        }

        [Test]
        public void ASplashShot_LandsOnce_HoweverManyItHits()
        {
            var sim = NewSim();
            int landed = 0, damaged = 0;
            sim.Events.ShotLanded += (attacker, weapon, point) => landed++;
            sim.Events.UnitDamaged += (target, attacker, amount) => damaged++;

            WeaponStats rocket = TestUnits.Weapon(damage: 1f, range: 30f, interval: 1000f, projectileSpeed: 40f, splash: 5f);
            sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: rocket), Team.A, new Vec2(0f, 0f));
            for (int i = 0; i < 3; i++) sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(i * 1.2f, 10f));
            for (int i = 0; i < 60; i++) sim.Step();

            Assert.AreEqual(1, landed);
            Assert.AreEqual(3, damaged);
        }

        [Test]
        public void AShot_WhoseTargetDiedInFlight_StillLands()
        {
            var sim = NewSim();
            int landed = 0, hurtByRocket = 0;
            WeaponStats rocket = TestUnits.Weapon(damage: 1f, range: 50f, interval: 1000f, projectileSpeed: 5f);
            SimUnit shooter = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: rocket), Team.A, new Vec2(0f, 0f));
            sim.Events.ShotLanded += (attacker, weapon, point) => landed++;
            sim.Events.UnitDamaged += (target, attacker, amount) => { if (attacker == shooter) hurtByRocket++; };
            int fired = 0;
            sim.Events.WeaponFired += (attacker, index, target) => fired++;
            SimUnit victim = sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 20f));
            sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(30f, 40f)); // So that the battle goes on.
            for (int i = 0; i < 100 && fired == 0; i++) sim.Step();
            Assert.AreEqual(1, fired);
            victim.Health = 0f; // Dead with the shot on its way.
            for (int i = 0; i < 200 && landed == 0; i++) sim.Step();

            Assert.AreEqual(1, landed, "The shot goes off where the target was.");
            Assert.AreEqual(0, hurtByRocket);
        }
    }
}
