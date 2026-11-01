// WeaponMechanicsTests.cs
// Lobbed shots and beams (decision D37).
// Depends on: Halobellum.Combat, Halobellum.Core, TestRules, TestUnits, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class WeaponMechanicsTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static void RunSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.MathF.Round(seconds * 30f);
            for (int i = 0; i < ticks; i++) sim.Step();
        }

        private static WeaponStats Mortar(float splash = 1f)
        {
            WeaponStats weapon = TestUnits.Weapon(damage: 10f, range: 40f, interval: 100f, projectileSpeed: 20f, splash: splash);
            weapon.Lobbed = true;
            return weapon;
        }

        private static WeaponStats Beam(float damagePerHit = 40f, float sweep = 2f)
        {
            WeaponStats weapon = TestUnits.Weapon(damage: damagePerHit, range: 40f, interval: 0.1f, splash: 2f);
            weapon.BeamChargeSeconds = 2f;
            weapon.BeamSeconds = 3f;
            weapon.BeamRestSeconds = 3f;
            weapon.BeamSweepSpeed = sweep;
            return weapon;
        }

        private static SimUnit Shooter(BattleSimulation sim, WeaponStats weapon)
        {
            return sim.AddUnit(TestUnits.Unit(health: 100000f, speed: 0f, weapon: weapon), Team.A, Vec2.Zero);
        }

        private static SimUnit Target(BattleSimulation sim, Vec2 position, float health = 100000f, float speed = 0f)
        {
            // A harmless target: it has a weapon too short and too weak to matter.
            return sim.AddUnit(TestUnits.Unit(health: health, speed: speed, weapon: TestUnits.Weapon(damage: 0f, range: 0.1f)), Team.B, position);
        }

        // ------------------------------------------------------------------ lobbed shots

        [Test]
        public void LobbedShot_LeadsATargetWalkingStraight()
        {
            var sim = NewSim();
            Shooter(sim, Mortar());
            SimUnit target = Target(sim, new Vec2(0f, 30f), speed: 5f);
            RunSeconds(sim, 2.5f);
            Assert.Less(target.Health, 100000f, "The shot lands where the target walked to. Aimed at where it was, it would land 7 m behind.");
        }

        [Test]
        public void LobbedShot_MissesATargetThatLeavesTheSpot()
        {
            var sim = NewSim();
            Shooter(sim, Mortar());
            SimUnit target = Target(sim, new Vec2(0f, 30f));
            sim.Step();
            Assert.AreEqual(1, sim.ProjectileCount);
            target.Position = new Vec2(0f, 37f);
            RunSeconds(sim, 2.5f);
            Assert.AreEqual(0, sim.ProjectileCount, "The shot has landed.");
            Assert.AreEqual(100000f, target.Health, "It lands on the old spot and does not follow.");
        }

        [Test]
        public void LobbedShot_DamagesWhateverIsAtTheSpot()
        {
            var sim = NewSim();
            Shooter(sim, Mortar(splash: 2f));
            SimUnit target = Target(sim, new Vec2(0f, 30f));
            SimUnit bystander = Target(sim, new Vec2(1.5f, 31f));
            sim.Step();
            target.Position = new Vec2(0f, 38f);
            RunSeconds(sim, 2.5f);
            Assert.AreEqual(100000f, target.Health, "The target has left.");
            Assert.AreEqual(100000f - 10f, bystander.Health, 1e-3f, "Whoever stands there takes the blast.");
        }

        [Test]
        public void LobbedShot_ReportsItsArc_AndAFlatShotDoesNot()
        {
            var sim = NewSim();
            Shooter(sim, Mortar());
            Target(sim, new Vec2(0f, 30f));
            RunSeconds(sim, 0.75f);
            var shots = new List<ShotInFlight>();
            sim.GetProjectiles(shots);
            Assert.AreEqual(1, shots.Count);
            Assert.Greater(shots[0].ArcLength, 25f);
            Assert.AreEqual(0.5f, shots[0].Progress, 0.05f);

            var flat = NewSim();
            Shooter(flat, TestUnits.Weapon(damage: 10f, range: 40f, interval: 100f, projectileSpeed: 20f));
            Target(flat, new Vec2(0f, 30f));
            RunSeconds(flat, 0.5f);
            flat.GetProjectiles(shots);
            Assert.AreEqual(0f, shots[0].ArcLength, "A normal shot stays flat.");
        }

        [Test]
        public void LobbedShot_NeedsASpeedAndASplash_InTheData()
        {
            UnitStats unit = TestUnits.Unit(weapon: Mortar(splash: 0f));
            CollectionAssert.IsNotEmpty(unit.Validate());
        }

        // ------------------------------------------------------------------ beams

        [Test]
        public void Beam_ChargesTwoSeconds_ThenDealsItsFullDamageOverThree()
        {
            var sim = NewSim();
            Shooter(sim, Beam());
            SimUnit target = Target(sim, new Vec2(0f, 20f));
            RunSeconds(sim, 1.9f);
            Assert.AreEqual(100000f, target.Health, "Still charging.");
            RunSeconds(sim, 3.2f);
            Assert.AreEqual(100000f - 30 * 40f, target.Health, 1e-2f, "400 damage per second for 3 seconds: 30 hits of 40.");
        }

        [Test]
        public void Beam_RestsAfterFiring_ThenChargesAgain()
        {
            var sim = NewSim();
            Shooter(sim, Beam());
            SimUnit target = Target(sim, new Vec2(0f, 20f));
            RunSeconds(sim, 5.2f);
            float afterOne = target.Health;
            RunSeconds(sim, 4.6f); // 3 s of rest and 2 s of charge end at about 10 s.
            Assert.AreEqual(afterOne, target.Health, 1e-2f, "No damage while resting and charging.");
            RunSeconds(sim, 3.4f);
            Assert.AreEqual(afterOne - 30 * 40f, target.Health, 1e-2f, "The second beam.");
        }

        [Test]
        public void Beam_KeepsFiring_AfterItsTargetDies()
        {
            var sim = NewSim();
            WeaponStats weapon = Beam(sweep: 0f);
            Shooter(sim, weapon);
            Target(sim, new Vec2(0f, 20f), health: 100f);
            RunSeconds(sim, 2.5f);
            var beams = new List<BeamShot>();
            sim.GetBeams(beams);
            Assert.AreEqual(1, beams.Count, "The target is dead, but the beam goes on.");
            Assert.IsTrue(beams[0].Firing);
            Assert.AreEqual(20f, beams[0].Point.Y, 1e-3f);
        }

        [Test]
        public void Beam_DriftsSlowlyToTheNextTarget_AfterItsTargetDies()
        {
            var sim = NewSim();
            Shooter(sim, Beam(sweep: 2f));
            SimUnit first = Target(sim, new Vec2(0f, 20f), health: 40f);
            SimUnit second = Target(sim, new Vec2(30f, 20f));
            RunSeconds(sim, 2.1f);
            Assert.LessOrEqual(first.Health, 0f, "The first hit kills it.");
            RunSeconds(sim, 2f);
            var beams = new List<BeamShot>();
            sim.GetBeams(beams);
            Assert.AreEqual(4f, beams[0].Point.X, 0.5f, "2 m/s for about 2 seconds toward the next one, not a jump.");
            Assert.AreEqual(100000f, second.Health, "It has not arrived yet.");
        }

        [Test]
        public void Beam_HitsEveryoneInTheArea_ButNotOutsideIt()
        {
            var sim = NewSim();
            Shooter(sim, Beam(sweep: 0f));
            SimUnit target = Target(sim, new Vec2(0f, 20f));
            SimUnit near = Target(sim, new Vec2(1.5f, 20f));
            SimUnit far = Target(sim, new Vec2(6f, 20f));
            RunSeconds(sim, 2.5f);
            Assert.Less(target.Health, 100000f);
            Assert.Less(near.Health, 100000f);
            Assert.AreEqual(100000f, far.Health);
        }

        [Test]
        public void Beam_CanBeOutrun_ByAFastTarget()
        {
            var sim = NewSim();
            Shooter(sim, Beam(sweep: 2f));
            SimUnit target = Target(sim, new Vec2(0f, 20f));
            RunSeconds(sim, 2.04f);
            target.Position = new Vec2(10f, 20f); // Out of the spot in one jump; the beam only moves 2 m/s.
            RunSeconds(sim, 1f);
            float health = target.Health;
            RunSeconds(sim, 0.5f);
            Assert.AreEqual(health, target.Health, 1e-3f);
            var beams = new List<BeamShot>();
            sim.GetBeams(beams);
            Assert.AreEqual(3f, beams[0].Point.X, 0.5f, "It is following, at its own speed.");
        }

        [Test]
        public void Beam_EndsWithItsShooter()
        {
            var sim = NewSim();
            SimUnit shooter = Shooter(sim, Beam());
            Target(sim, new Vec2(0f, 20f));
            RunSeconds(sim, 2.5f);
            var beams = new List<BeamShot>();
            sim.GetBeams(beams);
            Assert.AreEqual(1, beams.Count);
            shooter.Health = 0f;
            sim.Step();
            sim.GetBeams(beams);
            Assert.AreEqual(0, beams.Count);
        }

        [Test]
        public void Beam_NeedsASplash_InTheData()
        {
            WeaponStats weapon = Beam();
            weapon.SplashRadius = 0f;
            CollectionAssert.IsNotEmpty(TestUnits.Unit(weapon: weapon).Validate());
        }
    }
}
