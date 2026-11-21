// HunterChargeTests.cs
// The Hunters' shield charge (D87): with nothing near, a Hunter shoots its cannon; with an enemy within 5 m it stops shooting, charges at it
// along the ground and bashes it with its shield, and keeps bashing while it stays close. A charge never jumps over what holds it up, and a
// range tech lengthens the cannon, not the bash. The numbers are test fixtures, close to the Hunter Pair's.
// Depends on: Halobellum.Combat, Halobellum.Core, TestRules, TestUnits, NUnit.

using System;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class HunterChargeTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)MathF.Round(seconds * 30f);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        private static UnitStats Hunter()
        {
            WeaponStats cannon = TestUnits.Weapon(damage: 60f, range: 16f, interval: 2f, minRange: 5f, type: DamageType.Explosive);
            cannon.Name = "Assault Cannon";
            WeaponStats bash = TestUnits.Weapon(damage: 100f, range: 2f, interval: 1.5f, type: DamageType.Melee);
            bash.Name = "Shield Bash";
            bash.HasOwnTargetFilter = true;
            bash.TargetFilter = TargetFilter.Ground;
            UnitStats unit = TestUnits.Unit("Hunter", health: 5000f, speed: 1.1f, radius: 1.2f, filter: TargetFilter.Ground, weapon: cannon);
            unit.Weapons = new[] { cannon, bash };
            unit.LeapRange = 5f;
            unit.LeapSpeed = 9f;
            unit.LeapCooldownSeconds = 3f;
            unit.LeapCharges = true;
            return unit;
        }

        // The Goliath (D142): melee only, its fist slam 240 every 2 s reaching 3 m from its centre, and a charge like the Hunters' that reaches
        // 6 m from its centre (its body is 2.4 m across the middle). Its health is a fixture.
        private static UnitStats Goliath()
        {
            WeaponStats fist = TestUnits.Weapon(damage: 240f, range: 3f, interval: 2f, type: DamageType.Melee, splash: 3.5f);
            fist.Name = "Fist Slam";
            UnitStats unit = TestUnits.Unit("Goliath", health: 5000f, speed: 1.1f, radius: 2.4f, filter: TargetFilter.Ground, weapon: fist);
            unit.LeapRange = 6f;
            unit.LeapSpeed = 9f;
            unit.LeapCooldownSeconds = 3f;
            unit.LeapCharges = true;
            return unit;
        }

        private static SimUnit Enemy(BattleSimulation sim, Vec2 position)
        {
            return sim.AddUnit(TestUnits.Unit("Enemy", health: 1000f, speed: 0f, radius: 0.5f, filter: TargetFilter.Air,
                weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, position);
        }

        [Test]
        public void WithNothingNear_ItShootsItsCannon_AndStaysPut()
        {
            var sim = NewSim();
            SimUnit hunter = sim.AddUnit(Hunter(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 10f));
            RunSeconds(sim, 1f);
            Assert.AreEqual(0, hunter.MainWeaponIndex, "The cannon.");
            Assert.IsFalse(hunter.IsLeaping);
            Assert.AreEqual(940f, enemy.Health, 0.01f, "One cannon shot.");
            Assert.AreEqual(0f, hunter.Position.Y, 0.01f, "In range: it stands and shoots.");
        }

        [Test]
        public void WithAnEnemyWithinFiveMetres_ItChargesAlongTheGround_AndBashes()
        {
            var sim = NewSim();
            SimUnit hunter = sim.AddUnit(Hunter(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 4.7f)); // 4.2 m to its edge: inside the charge, outside the bash.
            sim.Step();
            Assert.AreEqual(1, hunter.MainWeaponIndex, "The shield bash.");
            Assert.IsTrue(hunter.IsLeaping, "It charges at once.");
            Assert.Greater(hunter.Position.Y, 0.25f, "At 9 m/s, far faster than its 1.1 m/s walk.");
            RunSeconds(sim, 1f);
            Assert.IsFalse(hunter.IsLeaping, "It has reached it.");
            Assert.IsTrue(Targeting.IsInRange(hunter, hunter.Weapons[1].Stats, enemy), "Right against its target.");
            Assert.AreEqual(900f, enemy.Health, 0.01f, "One bash, and no cannon shot at something this close.");
        }

        [Test]
        public void ItKeepsBashing_WhileTheEnemyStaysClose()
        {
            var sim = NewSim();
            SimUnit hunter = sim.AddUnit(Hunter(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 2f));
            RunSeconds(sim, 5f);
            Assert.AreEqual(1, hunter.MainWeaponIndex);
            Assert.AreEqual(600f, enemy.Health, 100.01f, "A bash every 1.5 s: three or four of them in 5 s.");
            Assert.Less(enemy.Health, 750f);
        }

        [Test]
        public void ACharge_NeverJumpsOverWhatHoldsItUp()
        {
            var sim = NewSim();
            SimUnit hunter = sim.AddUnit(Hunter(), Team.A, new Vec2(0f, -60f));
            for (int x = -6; x <= 6; x++)
            {
                sim.AddUnit(TestUnits.Unit("Block", health: 1000f, speed: 0f, radius: 0.5f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)),
                    Team.A, new Vec2(x * 1.2f, -57f));
            }
            Enemy(sim, new Vec2(0f, 70f)); // Far out of sight: it marches straight ahead into the wall.
            for (int i = 0; i < 90; i++)
            {
                sim.Step();
                Assert.IsFalse(hunter.IsLeaping, $"It never jumps (tick {i}).");
            }
        }

        [Test]
        public void ARangeTech_LengthensTheCannon_NotTheBash()
        {
            var tech = new TechUpgrade { Name = "Long Fuel Rods", RangeBonus = 4f };
            UnitStats stats = UnitUpgrades.Apply(Hunter(), 1, new[] { tech });
            Assert.AreEqual(20f, stats.Weapons[0].Range, 0.001f);
            Assert.AreEqual(2f, stats.Weapons[1].Range, 0.001f, "Its bash still reaches as far as its arm.");
        }

        [Test]
        public void AMeleeOnlyCharger_DoesNotSwitchWeapons()
        {
            Assert.IsFalse(Goliath().SwitchesWeapons, "One weapon: nothing to switch between.");
        }

        [Test]
        public void AMeleeOnlyCharger_ChargesAtAnEnemyWithinItsCharge_AndSlams()
        {
            var sim = NewSim();
            SimUnit goliath = sim.AddUnit(Goliath(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 5.5f)); // 5 m from its centre to the enemy's edge: inside the 6 m charge, outside the 3 m fist.
            sim.Step();
            Assert.IsTrue(goliath.IsLeaping, "It charges at once.");
            Assert.AreEqual(0, goliath.MainWeaponIndex);
            Assert.Greater(goliath.Position.Y, 0.25f, "At 9 m/s, far faster than its 1.1 m/s walk.");
            RunSeconds(sim, 1f);
            Assert.IsFalse(goliath.IsLeaping, "It has reached it.");
            Assert.IsTrue(Targeting.IsInRange(goliath, goliath.Weapons[0].Stats, enemy), "Right against its target.");
            Assert.AreEqual(760f, enemy.Health, 0.01f, "One slam.");
        }

        [Test]
        public void AMeleeOnlyCharger_ReachCountsFromItsCentre()
        {
            var sim = NewSim();
            SimUnit goliath = sim.AddUnit(Goliath(), Team.A, Vec2.Zero);
            Enemy(sim, new Vec2(0f, 7f)); // 6.5 m from its centre to the enemy's edge: beyond the 6 m charge, though only 4.1 m beyond its body.
            sim.Step();
            Assert.IsFalse(goliath.IsLeaping);
        }

        [Test]
        public void AMeleeOnlyCharger_BeyondItsCharge_WalksAndDoesNotShoot()
        {
            var sim = NewSim();
            SimUnit goliath = sim.AddUnit(Goliath(), Team.A, Vec2.Zero);
            SimUnit enemy = Enemy(sim, new Vec2(0f, 15f));
            RunSeconds(sim, 1f);
            Assert.IsFalse(goliath.IsLeaping);
            Assert.AreEqual(1000f, enemy.Health, 0.01f, "Nothing reaches that far.");
            Assert.That(goliath.Position.Y, Is.InRange(0.5f, 1.5f), "It walks at 1.1 m/s.");
        }
    }
}
