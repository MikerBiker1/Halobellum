// SiphonLaserTests.cs
// The Hunter Captain's siphon laser is a held 1 s beam (the owner, D91): 10 hits over the second, then 1 s of rest, about the damage over time
// of the old shots (96 a second); it heals the Captain as before; and it stops when the Captain turns to its shield bash. The numbers are test
// fixtures, the Captain's.
// Depends on: Halobellum.Combat, Halobellum.Core, TestRules, TestUnits, NUnit.

using System;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SiphonLaserTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)MathF.Round(seconds * 30f);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        private static WeaponStats Laser()
        {
            WeaponStats laser = TestUnits.Weapon(damage: 19.5f, range: 15f, interval: 0.1f, minRange: 5f, type: DamageType.Energy);
            laser.Name = "Siphon Laser";
            laser.SplashRadius = 0.3f;
            laser.BeamSeconds = 1f;
            laser.BeamChargeSeconds = 0f;
            laser.BeamRestSeconds = 1f;
            laser.BeamSweepSpeed = 30f;
            return laser;
        }

        private static UnitStats Captain(bool withBash)
        {
            WeaponStats laser = Laser();
            UnitStats unit = TestUnits.Unit("Hunter Captain", health: 5000f, speed: 0f, radius: 1.4f, filter: TargetFilter.Ground, weapon: laser);
            if (withBash)
            {
                WeaponStats bash = TestUnits.Weapon(damage: 120f, range: 2.2f, interval: 1.5f, type: DamageType.Melee);
                bash.Name = "Shield Bash";
                bash.HasOwnTargetFilter = true;
                bash.TargetFilter = TargetFilter.Ground;
                unit.Weapons = new[] { laser, bash };
                unit.LeapRange = 5f;
                unit.LeapSpeed = 9f;
                unit.LeapCooldownSeconds = 3f;
                unit.LeapCharges = true;
            }
            unit.LifestealShare = 0.3f;
            Assert.IsEmpty(unit.Validate(), "A valid beam.");
            return unit;
        }

        private static SimUnit Target(BattleSimulation sim, Vec2 position)
        {
            return sim.AddUnit(TestUnits.Unit("Target", health: 5000f, speed: 0f, radius: 0.5f, filter: TargetFilter.Air,
                weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, position);
        }

        [Test]
        public void ItBurnsForOneSecond_InTenHits_ThenRests()
        {
            var sim = NewSim();
            SimUnit captain = sim.AddUnit(Captain(false), Team.A, Vec2.Zero);
            SimUnit target = Target(sim, new Vec2(0f, 10f));
            RunSeconds(sim, 1.1f);
            Assert.AreEqual(5000f - 10f * 19.5f, target.Health, 0.01f, "Ten hits in its first second.");
            Assert.AreEqual(BeamPhase.Off, captain.Weapons[0].BeamPhase, "Then it rests.");
            RunSeconds(sim, 0.8f);
            Assert.AreEqual(5000f - 10f * 19.5f, target.Health, 0.01f, "Nothing while it rests.");
        }

        [Test]
        public void OverTime_ItDealsWhatTheOldShotsDid()
        {
            var sim = NewSim();
            sim.AddUnit(Captain(false), Team.A, Vec2.Zero);
            SimUnit target = Target(sim, new Vec2(0f, 10f));
            RunSeconds(sim, 20f);
            float perSecond = (5000f - target.Health) / 20f;
            Assert.AreEqual(96f, perSecond, 6f, "The old laser: 72 every 0.75 s.");
        }

        [Test]
        public void ItStillHealsTheCaptain()
        {
            var sim = NewSim();
            SimUnit captain = sim.AddUnit(Captain(false), Team.A, Vec2.Zero);
            captain.Health = 4000f;
            Target(sim, new Vec2(0f, 10f));
            RunSeconds(sim, 1.1f);
            Assert.AreEqual(4000f + 0.3f * 10f * 19.5f, captain.Health, 0.5f, "30% of what it burns comes back.");
        }

        [Test]
        public void ItStops_WhenTheCaptainTurnsToItsBash()
        {
            var sim = NewSim();
            SimUnit captain = sim.AddUnit(Captain(true), Team.A, Vec2.Zero);
            // It walks in from 12 m: burnt on the way, then inside 5 m it draws the charge and the bash.
            SimUnit walker = sim.AddUnit(TestUnits.Unit("Walker", health: 5000f, speed: 4f, radius: 0.5f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 0f, range: 0.5f)), Team.B, new Vec2(0f, 12f));
            bool burnt = false;
            for (int i = 0; i < 150 && captain.MainWeaponIndex == 0; i++)
            {
                sim.Step();
                burnt |= captain.Weapons[0].BeamPhase == BeamPhase.Firing;
            }
            Assert.IsTrue(burnt, "The laser burnt it on its way in.");
            Assert.AreEqual(1, captain.MainWeaponIndex, "The bash, once it is close.");
            sim.Step();
            Assert.AreEqual(BeamPhase.Off, captain.Weapons[0].BeamPhase, "No laser while it bashes.");
            Assert.Less(walker.Health, 5000f);
        }
    }
}
