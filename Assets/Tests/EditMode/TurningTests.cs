// TurningTests.cs
// Tests turning speed: a unit needs its TurnTime180 to turn around, drives forward along its
// facing (so it stops and turns before reversing), turns toward targets at that speed, and its main
// weapon only fires once it faces the target; other weapons fire in any direction. Turn times here
// follow the starting guideline: chaff 0.25 s, 1-square units 1 s, 2x2-square units 2 s.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class TurningTests
    {
        private CombatRules _rules;

        [SetUp]
        public void SetUp() => _rules = TestRules.Default();

        private static UnitStats Turner(float turnTime, float speed = 4f, WeaponStats weapon = null)
        {
            UnitStats stats = TestUnits.Unit(speed: speed, radius: 1f, weapon: weapon);
            stats.TurnTime180 = turnTime;
            return stats;
        }

        [Test]
        public void RotateTowards_TurnsByAtMostTheGivenAngle()
        {
            Vec2 turned = Vec2.RotateTowards(Vec2.Up, Vec2.Down, 90f);
            Assert.AreEqual(90f, Vec2.AngleDegrees(Vec2.Up, turned), 0.01f);
            Assert.AreEqual(Vec2.Right.Normalized.X, Vec2.RotateTowards(Vec2.Up, Vec2.Right, 120f).X, 1e-5f);
        }

        [TestCase(0.25f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(3f)]
        public void Unit_NeedsItsTurnTimeToReverse_AndBarelyMovesMeanwhile(float turnTime)
        {
            var unit = new SimUnit(0, Team.A, -1, Turner(turnTime), new Vec2(0f, 0f), Vec2.Up);
            unit.State = UnitState.Move;
            unit.MoveGoal = new Vec2(0f, -40f); // Straight behind it.
            var units = new List<SimUnit> { unit };
            var movement = new MovementSystem(_rules);

            int ticksToTurn = (int)System.Math.Round(turnTime * _rules.TicksPerSecond);
            for (int t = 0; t < ticksToTurn - 2; t++) movement.Step(units, _rules.TickSeconds);
            Assert.Greater(Vec2.AngleDegrees(unit.Facing, Vec2.Down), 1f, "Turned around too fast.");

            for (int t = 0; t < 3; t++) movement.Step(units, _rules.TickSeconds);
            // Once past 90 degrees it starts driving, so it curves slightly: check it faces its goal.
            Assert.AreEqual(0f, Vec2.AngleDegrees(unit.Facing, unit.MoveGoal - unit.Position), 1f, "Should face its goal after its turn time.");
            Assert.Less(unit.Position.Length, 4f * turnTime * 0.6f, "Should mostly turn on the spot, not drive away.");

            for (int t = 0; t < _rules.TicksPerSecond; t++) movement.Step(units, _rules.TickSeconds);
            Assert.Less(unit.Position.Y, -3f, "Should drive toward its goal once turned.");
        }

        [Test]
        public void InstantTurner_FacesItsMovementDirectionImmediately()
        {
            var unit = new SimUnit(0, Team.A, -1, Turner(0f), new Vec2(0f, 0f), Vec2.Up);
            unit.State = UnitState.Move;
            unit.MoveGoal = new Vec2(0f, -40f);
            new MovementSystem(_rules).Step(new List<SimUnit> { unit }, _rules.TickSeconds);
            Assert.AreEqual(Vec2.Down, unit.Facing);
        }

        [Test]
        public void MainWeapon_WaitsUntilTheUnitFacesItsTarget()
        {
            // A tank with a 2 s turn time and a target directly behind it.
            var sim = new BattleSimulation(_rules, 1);
            var tank = sim.AddUnit(Turner(2f, speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 30f, interval: 0.1f)), Team.A, new Vec2(0f, 0f));
            var target = sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, -10f));

            // (180 - 15) degrees at 90 degrees per second is about 1.83 s before the first shot.
            for (int t = 0; t < 50; t++) sim.Step(); // 1.67 s
            Assert.AreEqual(1000f, target.Health, "Fired before facing the target.");
            for (int t = 0; t < 10; t++) sim.Step(); // 2.0 s
            Assert.Less(target.Health, 1000f, "Should fire once it faces the target.");
            Assert.Less(Vec2.AngleDegrees(tank.Facing, Vec2.Down), 15.01f);
        }

        [Test]
        public void SecondaryWeapon_FiresInAnyDirection()
        {
            var stats = Turner(2f, speed: 0f);
            stats.Weapons = new[] { TestUnits.Weapon(damage: 0f, range: 5f), TestUnits.Weapon(damage: 10f, range: 30f, interval: 0.1f) };
            var sim = new BattleSimulation(_rules, 1);
            sim.AddUnit(stats, Team.A, new Vec2(0f, 0f));
            var target = sim.AddUnit(TestUnits.Unit(health: 1000f, speed: 0f, filter: TargetFilter.Air), Team.B, new Vec2(0f, -10f));

            sim.Step();
            Assert.AreEqual(990f, target.Health, 1e-3f, "A secondary weapon should not wait for the body to turn.");
        }

        [Test]
        public void ValidateRejectsNegativeTurnTime()
        {
            Assert.AreEqual(1, Turner(-1f).Validate().Count);
        }
    }
}
