// WindUpTests.cs
// A weapon with a wind-up (the Spartan Laser, 2.5 s): the first shot at a target comes that long after the weapon is ready and aimed,
// at the target it locked on to; firing on, it keeps one shot per fire interval; it shoots another target in range if the locked one
// died, or spends the shot if none is; a shockwave's slower fire stretches it; an EMP or death cancels it; it is raised as an event;
// and a weapon without one fires exactly as before.
// Depends on: Halobellum.Combat, Halobellum.Core, TestUnits, TestRules, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class WindUpTests
    {
        private static BattleSimulation NewSim() => new BattleSimulation(TestRules.Default(), 1);

        private static WeaponStats Laser(float windUp = 2.5f, float interval = 4f, float damage = 10f, float range = 24f)
        {
            WeaponStats laser = TestUnits.Weapon(damage: damage, range: range, interval: interval);
            laser.Name = "Spartan Laser";
            laser.WindUpSeconds = windUp;
            return laser;
        }

        /// <summary>A target that neither moves nor shoots back, with a lot of health.</summary>
        private static UnitStats Dummy(float health = 100000f) => TestUnits.Unit(health: health, speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 1f));

        /// <summary>The battle times (in seconds) of each shot the first unit fires, over this long.</summary>
        private static List<float> ShotTimes(BattleSimulation sim, SimUnit shooter, float seconds)
        {
            var times = new List<float>();
            sim.Events.WeaponFired += (attacker, index, target) => { if (attacker == shooter) times.Add(sim.ElapsedSeconds); };
            int ticks = (int)(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
            return times;
        }

        [Test]
        public void TheFirstShot_ComesAfterTheWindUp_AndFiringOnKeepsItsRate()
        {
            var sim = NewSim();
            SimUnit spartan = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser()), Team.A, Vec2.Zero);
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            List<float> shots = ShotTimes(sim, spartan, 15f);

            Assert.GreaterOrEqual(shots.Count, 3);
            Assert.AreEqual(2.5f, shots[0], 0.1f, "In range from the start: the shot goes 2.5 s in.");
            for (int i = 1; i < shots.Count; i++) Assert.AreEqual(4f, shots[i] - shots[i - 1], 0.05f, "One shot per fire interval: each wind-up overlaps the reload.");
        }

        [Test]
        public void AWeaponWithoutOne_FiresAsSoonAsItCan()
        {
            var sim = NewSim();
            SimUnit gunner = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser(windUp: 0f)), Team.A, Vec2.Zero);
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            List<float> shots = ShotTimes(sim, gunner, 9f);
            Assert.Less(shots[0], 0.2f);
            Assert.AreEqual(4f, shots[1] - shots[0], 0.05f);
        }

        [Test]
        public void TheWindUp_IsRaised_WithItsTarget_BeforeTheShot()
        {
            var sim = NewSim();
            SimUnit spartan = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser()), Team.A, Vec2.Zero);
            SimUnit target = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            float windUpAt = -1f, firedAt = -1f, said = 0f;
            SimUnit locked = null;
            bool chargingSeen = false;
            sim.Events.WeaponWindingUp += (attacker, index, aim, seconds) =>
            {
                if (windUpAt >= 0f) return;
                windUpAt = sim.ElapsedSeconds;
                locked = aim;
                said = seconds;
            };
            sim.Events.WeaponFired += (attacker, index, aim) => { if (firedAt < 0f) firedAt = sim.ElapsedSeconds; };
            var windUps = new List<WindUpShot>();
            for (int i = 0; i < 120; i++)
            {
                sim.Step();
                sim.GetWindUps(windUps);
                if (windUps.Count == 1 && windUps[0].Attacker == spartan && windUps[0].Charge > 0.3f && windUps[0].Charge < 0.7f) chargingSeen = true;
            }
            Assert.AreSame(target, locked);
            Assert.AreEqual(2.5f, said, 1e-4f);
            Assert.AreEqual(2.5f, firedAt - windUpAt, 0.05f);
            Assert.IsTrue(chargingSeen, "GetWindUps shows the charge as it grows.");
        }

        [Test]
        public void TheLockedTargetDead_TheShotGoesAtAnotherInRange()
        {
            var sim = NewSim();
            SimUnit spartan = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser(damage: 50f)), Team.A, Vec2.Zero);
            SimUnit first = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            SimUnit second = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 14f));
            SimUnit hit = null;
            sim.Events.WeaponFired += (attacker, index, target) => { if (attacker == spartan && hit == null) hit = target; };
            for (int i = 0; i < 30; i++) sim.Step(); // 1 s into the wind-up.
            Assert.IsTrue(spartan.Weapons[0].WindingUp);
            first.Health = 0f;
            for (int i = 0; i < 60 && hit == null; i++) sim.Step();
            Assert.AreSame(second, hit);
            Assert.Less(second.Health, 100000f);
        }

        [Test]
        public void WithNothingLeftInRange_TheShotIsSpent()
        {
            var sim = NewSim();
            SimUnit spartan = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser()), Team.A, Vec2.Zero);
            SimUnit only = sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 90f)); // Out of range, so that the battle goes on.
            int fired = 0;
            sim.Events.WeaponFired += (attacker, index, target) => { if (attacker == spartan) fired++; };
            for (int i = 0; i < 30; i++) sim.Step();
            only.Health = 0f;
            for (int i = 0; i < 60; i++) sim.Step();
            Assert.AreEqual(0, fired, "Nothing in range when the wind-up ended: no shot.");
            Assert.IsFalse(spartan.Weapons[0].WindingUp);
            Assert.Greater(spartan.Weapons[0].Cooldown, 0f, "The spent shot reloads as a shot does.");
        }

        [Test]
        public void AnEmp_CancelsTheWindUp_AndItStartsAgain()
        {
            var sim = NewSim();
            SimUnit spartan = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser()), Team.A, Vec2.Zero);
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            var shots = new List<float>();
            sim.Events.WeaponFired += (attacker, index, target) => { if (attacker == spartan) shots.Add(sim.ElapsedSeconds); };
            // It goes off 1.5 s in, 1 s before the shot would have gone.
            sim.AddSpell(new SpellStats { Name = "EMP", Effect = SpellEffect.Disrupt, Radius = 5f, DelaySeconds = 1.5f }, Team.B, Vec2.Zero);
            for (int i = 0; i < 40; i++) sim.Step();
            Assert.IsTrue(spartan.Weapons[0].WindingUp);
            for (int i = 0; i < 150 && shots.Count == 0; i++) sim.Step();
            Assert.AreEqual(1, shots.Count);
            Assert.Greater(shots[0], 3.4f, "The charge started again from nothing after the EMP.");
        }

        [Test]
        public void Dying_CancelsTheWindUp()
        {
            var sim = NewSim();
            SimUnit spartan = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser()), Team.A, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit(speed: 0f), Team.A, new Vec2(5f, -5f)); // So that the battle goes on.
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            int fired = 0;
            sim.Events.WeaponFired += (attacker, index, target) => { if (attacker == spartan) fired++; };
            for (int i = 0; i < 30; i++) sim.Step();
            spartan.Health = 0f;
            for (int i = 0; i < 90; i++) sim.Step();
            Assert.AreEqual(0, fired);
            Assert.IsFalse(spartan.Weapons[0].WindingUp);
            var windUps = new List<WindUpShot>();
            sim.GetWindUps(windUps);
            Assert.AreEqual(0, windUps.Count);
        }

        [Test]
        public void AShockwave_StretchesTheWindUp()
        {
            var sim = NewSim();
            SimUnit spartan = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: Laser()), Team.A, Vec2.Zero);
            var tower = new UnitStats
            {
                Name = "Tower", SquadSize = 1, MaxHealth = 100f, ArmorType = ArmorType.Light, CollisionRadius = 3f, ImmuneToInfection = true,
                Shockwave = new ShockwaveStats { Kind = TowerKind.CommandCenter, Seconds = 30f, FireRateMultiplier = 0.5f },
            };
            SimUnit fallen = sim.AddUnit(tower, Team.A, new Vec2(50f, -50f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(0f, 10f));
            fallen.Health = 0f;
            List<float> shots = ShotTimes(sim, spartan, 7f);
            Assert.AreEqual(5f, shots[0], 0.1f, "Half the rate of fire: twice the wind-up.");
        }

        [Test]
        public void AWindUp_IsValidated()
        {
            UnitStats unit = TestUnits.Unit(weapon: Laser(windUp: -1f));
            Assert.IsTrue(unit.Validate().Exists(p => p.Contains("wind-up")));
            WeaponStats beam = Laser();
            beam.BeamSeconds = 1f;
            beam.SplashRadius = 1f;
            Assert.IsTrue(TestUnits.Unit(weapon: beam).Validate().Exists(p => p.Contains("wind-up")));
            Assert.AreEqual(0, TestUnits.Unit(weapon: Laser()).Validate().Count);
        }

        [Test]
        public void TheSameBattle_PlaysOutTheSame()
        {
            BattleResult Play()
            {
                var sim = NewSim();
                for (int i = 0; i < 3; i++) sim.AddUnit(TestUnits.Unit(health: 300f, speed: 3f, weapon: Laser(damage: 120f)), Team.A, new Vec2(i * 2f, 0f));
                for (int i = 0; i < 8; i++) sim.AddUnit(TestUnits.Unit(health: 80f, speed: 3f, weapon: TestUnits.Weapon(damage: 4f, range: 8f, interval: 0.5f)), Team.B, new Vec2(i * 1.5f, 30f));
                return sim.Run();
            }
            BattleResult a = Play(), b = Play();
            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.DurationSeconds, b.DurationSeconds);
            Assert.AreEqual(a.TeamA.Survivors, b.TeamA.Survivors);
            Assert.AreEqual(a.TeamB.Survivors, b.TeamB.Survivors);
        }
    }
}
