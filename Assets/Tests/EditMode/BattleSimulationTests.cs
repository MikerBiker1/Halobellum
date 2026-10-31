// BattleSimulationTests.cs
// Tests the fixed-timestep battle simulation: tick timing, the 90 second limit, re-evaluating
// targets every 0.5 s (and at once when a target dies), moving into range then attacking,
// instant and projectile hits, splash, shields, directional blocking in battle, simultaneous
// deaths, events, results, and seeded repeatability.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class BattleSimulationTests
    {
        private CombatRules _rules;

        [SetUp]
        public void SetUp() => _rules = TestRules.Default();

        private BattleSimulation NewSim(int seed = 1) => new BattleSimulation(_rules, seed);

        private static void StepSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks && !sim.IsFinished; i++) sim.Step();
        }

        [Test]
        public void Runs_ThirtyTicksPerSecond()
        {
            var sim = NewSim();
            sim.AddUnit(TestUnits.Unit(filter: TargetFilter.Air), Team.A, new Vec2(0f, -10f));
            sim.AddUnit(TestUnits.Unit(filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));
            StepSeconds(sim, 1f);
            Assert.AreEqual(30, sim.Tick);
            Assert.AreEqual(1f, sim.ElapsedSeconds, 1e-4f);
        }

        [Test]
        public void Battle_EndsAtTheTimeLimit_WhenNobodyCanFight()
        {
            // Two ground units that can only shoot air: neither can ever attack.
            var sim = NewSim();
            sim.AddUnit(TestUnits.Unit(filter: TargetFilter.Air), Team.A, new Vec2(0f, -10f));
            sim.AddUnit(TestUnits.Unit(filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));
            BattleResult result = sim.Run();
            Assert.IsTrue(result.TimedOut);
            Assert.AreEqual(90 * 30, result.Ticks);
            Assert.AreEqual(BattleOutcome.Draw, result.Outcome);
            Assert.AreEqual(UnitState.Idle, sim.Units[0].State);
        }

        [Test]
        public void Unit_MovesTowardEnemy_ThenStopsAndAttacksInRange()
        {
            var sim = NewSim();
            var weapon = TestUnits.Weapon(damage: 1f, range: 10f);
            var mover = sim.AddUnit(TestUnits.Unit(speed: 5f, weapon: weapon), Team.A, new Vec2(0f, -20f));
            var target = sim.AddUnit(TestUnits.Unit(health: 10000f, speed: 0f), Team.B, new Vec2(0f, 20f));

            sim.Step();
            Assert.AreEqual(UnitState.Move, mover.State);
            Assert.AreSame(target, mover.MoveTarget);

            StepSeconds(sim, 10f);
            Assert.AreEqual(UnitState.Attack, mover.State);
            float edgeDistance = Targeting.EdgeDistance(mover, target);
            Assert.LessOrEqual(edgeDistance, 10f);
            Assert.Greater(edgeDistance, 8.5f, "Should stop about at max range, not walk right up to the target.");
            Assert.Less(target.Health, 10000f);
        }

        [Test]
        public void Targets_AreReEvaluatedEveryHalfSecond_NotEveryTick()
        {
            var sim = NewSim();
            var shooter = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: TestUnits.Weapon(damage: 0f, range: 30f)), Team.A, new Vec2(0f, 0f));
            var far = sim.AddUnit(TestUnits.Unit(speed: 0f), Team.B, new Vec2(0f, 20f));
            var later = sim.AddUnit(TestUnits.Unit(speed: 0f), Team.B, new Vec2(0f, 60f));

            sim.Step(); // Tick 1: picks the closest enemy.
            Assert.AreSame(far, shooter.MoveTarget);

            later.Position = new Vec2(0f, 5f); // A closer enemy appears.
            for (int t = 1; t < 14; t++) sim.Step(); // Still within the first 0.5 s.
            Assert.AreSame(far, shooter.MoveTarget, "Retargeted before 0.5 s passed.");

            StepSeconds(sim, 0.1f);
            Assert.AreSame(later, shooter.MoveTarget, "Did not retarget after 0.5 s.");
        }

        [Test]
        public void Targets_AreReplacedImmediately_WhenTheTargetDies()
        {
            var sim = NewSim();
            var shooter = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: TestUnits.Weapon(damage: 100f, range: 30f, interval: 0.1f)), Team.A, new Vec2(0f, 0f));
            var first = sim.AddUnit(TestUnits.Unit(health: 50f, speed: 0f), Team.B, new Vec2(0f, 10f));
            var second = sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f), Team.B, new Vec2(0f, 20f));

            sim.Step(); // Kills the first enemy with one instant shot.
            Assert.AreEqual(UnitState.Dead, first.State);
            sim.Step();
            Assert.AreSame(second, shooter.MoveTarget);
        }

        [Test]
        public void InstantWeapon_DamagesImmediately_AndRespectsFireInterval()
        {
            var sim = NewSim();
            sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 30f, interval: 1f)), Team.A, new Vec2(0f, 0f));
            var target = sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));

            sim.Step();
            Assert.AreEqual(990f, target.Health, 1e-3f);
            StepSeconds(sim, 2.95f); // Shots at 0 s, 1 s and 2 s.
            Assert.AreEqual(970f, target.Health, 1e-3f);
        }

        [Test]
        public void Projectile_TakesTimeToArrive()
        {
            var sim = NewSim();
            sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 30f, interval: 100f, projectileSpeed: 10f)), Team.A, new Vec2(0f, 0f));
            var target = sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));

            sim.Step();
            Assert.AreEqual(1, sim.ProjectileCount);
            StepSeconds(sim, 0.8f);
            Assert.AreEqual(1000f, target.Health, "Hit before the projectile could arrive.");
            StepSeconds(sim, 0.4f);
            Assert.AreEqual(990f, target.Health, 1e-3f);
            Assert.AreEqual(0, sim.ProjectileCount);
        }

        [Test]
        public void Splash_HitsNearbyEnemies_ButNotAllies()
        {
            var sim = NewSim();
            var weapon = TestUnits.Weapon(damage: 10f, range: 30f, interval: 100f, splash: 3f, type: DamageType.Explosive);
            sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: weapon), Team.A, new Vec2(0f, 0f));
            var ally = sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.A, new Vec2(1.5f, 10f));
            var target = sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));
            var nearby = sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(-2f, 10f));
            var outside = sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 15f));

            sim.Step();
            Assert.AreEqual(90f, target.Health, 1e-3f);
            Assert.AreEqual(90f, nearby.Health, 1e-3f);
            Assert.AreEqual(100f, outside.Health);
            Assert.AreEqual(100f, ally.Health);
        }

        [Test]
        public void Shield_AbsorbsFirst_ThenRegeneratesAfterTheDelay()
        {
            var sim = NewSim();
            var shooter = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: TestUnits.Weapon(damage: 30f, range: 30f, interval: 100f, type: DamageType.Explosive)), Team.A, new Vec2(0f, 0f));
            var target = sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, shield: 50f, shieldRegen: 10f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));

            sim.Step();
            Assert.AreEqual(20f, target.Shield, 1e-3f);
            Assert.AreEqual(100f, target.Health);

            StepSeconds(sim, 4.5f);
            Assert.AreEqual(20f, target.Shield, 1e-3f, "Regenerated before the 5 s delay.");
            StepSeconds(sim, 2f);
            Assert.Greater(target.Shield, 20f);
            StepSeconds(sim, 10f);
            Assert.AreEqual(50f, target.Shield, 1e-3f, "Shield should refill to its maximum and stop.");
        }

        [Test]
        public void DefenseArc_BlocksFrontalShots_ButNotShotsFromBehind()
        {
            UnitStats Shielded() => TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air);
            var blocker = Shielded();
            blocker.DefenseArc = new DefenseArcStats
            {
                ArcDegrees = 120f, FrontDamageMultiplier = 1f, RearDamageMultiplier = 1f, BlockChance = 1f,
                BlockedDamageTypes = new[] { DamageType.Ballistic },
            };
            var gun = TestUnits.Weapon(damage: 10f, range: 30f, interval: 100f, blockable: true);

            // Team B faces down (toward -Y), so a Team A shooter below it is in front.
            var sim = NewSim();
            sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: gun), Team.A, new Vec2(0f, 0f));
            var front = sim.AddUnit(blocker, Team.B, new Vec2(0f, 10f));
            int blocks = 0;
            sim.Events.AttackBlocked += (t, a) => blocks++;
            sim.Step();
            Assert.AreEqual(100f, front.Health);
            Assert.AreEqual(1, blocks);

            // Same setup with the shooter behind the shielded unit.
            var sim2 = NewSim();
            sim2.AddUnit(TestUnits.Unit(speed: 0f, weapon: gun), Team.A, new Vec2(0f, 20f));
            var back = sim2.AddUnit(blocker, Team.B, new Vec2(0f, 10f));
            sim2.Step();
            Assert.AreEqual(90f, back.Health, 1e-3f);
        }

        [Test]
        public void UnitsThatKillEachOtherInTheSameTick_BothDie_AndItIsADraw()
        {
            var sim = NewSim();
            var gun = TestUnits.Weapon(damage: 100f, range: 30f);
            sim.AddUnit(TestUnits.Unit(health: 50f, speed: 0f, weapon: gun), Team.A, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(health: 50f, speed: 0f, weapon: gun), Team.B, new Vec2(0f, 10f));
            BattleResult result = sim.Run();
            Assert.AreEqual(1, result.Ticks);
            Assert.AreEqual(0, result.TeamA.Survivors);
            Assert.AreEqual(0, result.TeamB.Survivors);
            Assert.AreEqual(BattleOutcome.Draw, result.Outcome);
            Assert.IsFalse(result.TimedOut);
        }

        [Test]
        public void Events_ReportSpawnsShotsDamageDeathsTicksAndTheEnd()
        {
            var sim = NewSim();
            int spawned = 0, fired = 0, damaged = 0, died = 0, ticks = 0, ended = 0;
            SimUnit reportedKiller = null;
            sim.Events.UnitSpawned += u => spawned++;
            sim.Events.WeaponFired += (a, w, t) => fired++;
            sim.Events.UnitDamaged += (t, a, d) => damaged++;
            sim.Events.UnitDied += (u, k) => { died++; reportedKiller = k; };
            sim.Events.Ticked += t => ticks++;
            sim.Events.BattleEnded += r => ended++;

            var killer = sim.AddUnit(TestUnits.Unit(speed: 0f, weapon: TestUnits.Weapon(damage: 40f, range: 30f, interval: 0.5f)), Team.A, new Vec2(0f, 0f));
            sim.AddUnit(TestUnits.Unit(health: 100f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));
            BattleResult result = sim.Run();

            Assert.AreEqual(2, spawned);
            Assert.AreEqual(3, fired);
            Assert.AreEqual(3, damaged);
            Assert.AreEqual(1, died);
            Assert.AreSame(killer, reportedKiller);
            Assert.AreEqual(result.Ticks, ticks);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(1, killer.Kills);
            Assert.AreEqual(100f, killer.DamageDealt, 1e-3f, "Overkill damage should not count.");
        }

        [Test]
        public void Result_CountsSurvivorsAndTheirBaseDamage()
        {
            var sim = NewSim();
            var strong = TestUnits.Unit(name: "Strong", health: 1000f, speed: 0f, weapon: TestUnits.Weapon(damage: 100f, range: 30f, interval: 0.2f));
            strong.BaseDamage = 60f;
            var weak = TestUnits.Unit(name: "Weak", health: 100f, speed: 0f, weapon: TestUnits.Weapon(damage: 1f, range: 30f));
            sim.AddUnit(strong, Team.A, new Vec2(0f, 0f));
            sim.AddUnit(weak, Team.B, new Vec2(-2f, 10f));
            sim.AddUnit(weak, Team.B, new Vec2(2f, 10f));
            BattleResult result = sim.Run();

            Assert.AreEqual(BattleOutcome.TeamAWins, result.Outcome);
            Assert.AreEqual(1, result.TeamA.Survivors);
            Assert.AreEqual(60f, result.TeamA.SurvivorBaseDamage, 1e-3f);
            Assert.AreEqual(0, result.TeamB.Survivors);
            Assert.AreEqual(2, result.TeamA.Kills);
            Assert.AreEqual("Strong", result.TeamA.UnitTypes[0].UnitName);
            Assert.AreEqual(1, result.TeamA.UnitTypes.Count);
            Assert.AreEqual(2, result.TeamB.UnitTypes[0].Deployed);
        }

        [Test]
        public void AddSquad_PlacesEveryBodyWithoutOverlap_AndMirrorsTeamB()
        {
            var sim = NewSim();
            var marine = TestUnits.Unit(radius: 0.5f, squadSize: 18);
            sim.AddSquad(marine, Team.A, new Vec2(0f, -30f));
            sim.AddSquad(marine, Team.B, new Vec2(0f, 30f), rotated90: true);
            Assert.AreEqual(36, sim.Units.Count);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 18; i++)
            {
                var p = sim.Units[i].Position;
                minX = System.Math.Min(minX, p.X); maxX = System.Math.Max(maxX, p.X);
                minY = System.Math.Min(minY, p.Y); maxY = System.Math.Max(maxY, p.Y);
            }
            Assert.Greater(maxX - minX, (maxY - minY) * 1.5f, "An unrotated 18-body squad should be wider than deep.");

            for (int i = 0; i < sim.Units.Count; i++)
            for (int j = i + 1; j < sim.Units.Count; j++)
                Assert.GreaterOrEqual(Vec2.Distance(sim.Units[i].Position, sim.Units[j].Position), 1f - 1e-4f);

            Assert.AreEqual(Vec2.Up, sim.Units[0].Facing);
            Assert.AreEqual(Vec2.Down, sim.Units[18].Facing);
        }

        private static BattleResult RunArmies(CombatRules rules, int seed, out List<Vec2> finalPositions)
        {
            var sim = new BattleSimulation(rules, seed);
            var rifle = TestUnits.Unit(name: "Rifle", health: 60f, speed: 4f, radius: 0.5f, squadSize: 12,
                weapon: TestUnits.Weapon(damage: 6f, range: 12f, interval: 0.5f, blockable: true));
            var shieldWall = TestUnits.Unit(name: "ShieldWall", health: 90f, speed: 3.5f, radius: 0.6f, squadSize: 6,
                weapon: TestUnits.Weapon(damage: 8f, range: 8f, interval: 0.8f, type: DamageType.Energy));
            shieldWall.DefenseArc = new DefenseArcStats
            {
                ArcDegrees = 120f, FrontDamageMultiplier = 1f, RearDamageMultiplier = 1f, BlockChance = 0.5f,
                BlockedDamageTypes = new[] { DamageType.Ballistic },
            };
            var mortar = TestUnits.Unit(name: "Mortar", health: 200f, speed: 2f, radius: 1f, squadSize: 2,
                weapon: TestUnits.Weapon(damage: 20f, range: 30f, minRange: 6f, interval: 2f, projectileSpeed: 15f, splash: 2.5f, type: DamageType.Explosive));

            sim.AddSquad(rifle, Team.A, new Vec2(-10f, -25f));
            sim.AddSquad(rifle, Team.A, new Vec2(10f, -25f));
            sim.AddSquad(mortar, Team.A, new Vec2(0f, -40f));
            sim.AddSquad(shieldWall, Team.B, new Vec2(-8f, 25f));
            sim.AddSquad(shieldWall, Team.B, new Vec2(8f, 25f));
            sim.AddSquad(rifle, Team.B, new Vec2(0f, 35f));
            BattleResult result = sim.Run();

            finalPositions = new List<Vec2>();
            foreach (var u in sim.Units) finalPositions.Add(u.Position);
            return result;
        }

        [Test]
        public void SameArmiesAndSeed_GiveTheSameResult()
        {
            BattleResult first = RunArmies(_rules, 2024, out var firstPositions);
            BattleResult second = RunArmies(_rules, 2024, out var secondPositions);

            Assert.AreEqual(first.Ticks, second.Ticks);
            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.TeamA.Survivors, second.TeamA.Survivors);
            Assert.AreEqual(first.TeamB.Survivors, second.TeamB.Survivors);
            Assert.AreEqual(first.TeamA.DamageDealt, second.TeamA.DamageDealt);
            Assert.AreEqual(first.TeamB.DamageDealt, second.TeamB.DamageDealt);
            CollectionAssert.AreEqual(firstPositions, secondPositions);
        }

        [Test]
        public void MixedBattle_FinishesWithAWinnerAndSensibleTotals()
        {
            BattleResult result = RunArmies(_rules, 7, out _);
            Assert.IsFalse(result.TimedOut, "This small fight should end well before 90 s.");
            Assert.AreNotEqual(BattleOutcome.Draw, result.Outcome);
            Assert.AreEqual(26, result.TeamA.Deployed);
            Assert.AreEqual(24, result.TeamB.Deployed);
            Assert.Greater(result.TeamA.DamageDealt, 0f);
            Assert.Greater(result.TeamB.DamageDealt, 0f);
            Assert.AreEqual(result.TeamA.Kills, result.TeamB.Deployed - result.TeamB.Survivors);
            Assert.AreEqual(result.TeamB.Kills, result.TeamA.Deployed - result.TeamA.Survivors);
        }
    }
}
