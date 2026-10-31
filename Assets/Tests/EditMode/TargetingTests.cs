// TargetingTests.cs
// Tests target selection (Bible Section 4, rule 10): always the closest enemy the unit can
// legally target by target filter and minimum range, never allies or the dead, ties by id.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class TargetingTests
    {
        private readonly List<SimUnit> _units = new List<SimUnit>();

        [SetUp]
        public void SetUp() => _units.Clear();

        private SimUnit Add(Team team, float x, float y, bool air = false, TargetFilter filter = TargetFilter.Both,
            WeaponStats weapon = null, float radius = 0.5f)
        {
            var unit = new SimUnit(_units.Count, team, -1, TestUnits.Unit(air: air, filter: filter, weapon: weapon, radius: radius),
                new Vec2(x, y), team == Team.A ? Vec2.Up : Vec2.Down);
            _units.Add(unit);
            return unit;
        }

        private SimUnit Closest(SimUnit shooter) => Targeting.FindClosestLegalTarget(shooter, shooter.Stats.Weapons[0], _units);

        [Test]
        public void PicksTheClosestEnemy()
        {
            var shooter = Add(Team.A, 0f, 0f);
            Add(Team.B, 0f, 30f);
            var near = Add(Team.B, 5f, 5f);
            Add(Team.B, -20f, 0f);
            Assert.AreSame(near, Closest(shooter));
        }

        [Test]
        public void IgnoresAllies_EvenWhenCloser()
        {
            var shooter = Add(Team.A, 0f, 0f);
            Add(Team.A, 1f, 0f);
            var enemy = Add(Team.B, 10f, 0f);
            Assert.AreSame(enemy, Closest(shooter));
        }

        [Test]
        public void IgnoresDeadAndZeroHealthEnemies()
        {
            var shooter = Add(Team.A, 0f, 0f);
            var dead = Add(Team.B, 1f, 0f);
            dead.State = UnitState.Dead;
            var dying = Add(Team.B, 2f, 0f);
            dying.Health = 0f;
            var alive = Add(Team.B, 10f, 0f);
            Assert.AreSame(alive, Closest(shooter));
        }

        [Test]
        public void GroundOnlyUnit_IgnoresCloserAirUnits()
        {
            var shooter = Add(Team.A, 0f, 0f, filter: TargetFilter.Ground);
            Add(Team.B, 2f, 0f, air: true);
            var ground = Add(Team.B, 20f, 0f);
            Assert.AreSame(ground, Closest(shooter));
        }

        [Test]
        public void AirOnlyUnit_IgnoresCloserGroundUnits()
        {
            var shooter = Add(Team.A, 0f, 0f, filter: TargetFilter.Air);
            Add(Team.B, 2f, 0f);
            var air = Add(Team.B, 20f, 0f, air: true);
            Assert.AreSame(air, Closest(shooter));
        }

        [Test]
        public void BothFilter_PicksWhicheverIsClosest()
        {
            var shooter = Add(Team.A, 0f, 0f, filter: TargetFilter.Both);
            var air = Add(Team.B, 3f, 0f, air: true);
            Add(Team.B, 5f, 0f);
            Assert.AreSame(air, Closest(shooter));
        }

        [Test]
        public void MinimumRange_SkipsEnemiesTooClose()
        {
            var shooter = Add(Team.A, 0f, 0f, weapon: TestUnits.Weapon(range: 40f, minRange: 10f));
            Add(Team.B, 5f, 0f);
            var far = Add(Team.B, 25f, 0f);
            Assert.AreSame(far, Closest(shooter));
        }

        [Test]
        public void NoLegalEnemy_ReturnsNull()
        {
            var shooter = Add(Team.A, 0f, 0f, filter: TargetFilter.Ground);
            Add(Team.B, 5f, 0f, air: true);
            Add(Team.A, 1f, 0f);
            Assert.IsNull(Closest(shooter));
        }

        [Test]
        public void EqualDistance_PicksLowerId()
        {
            var shooter = Add(Team.A, 0f, 0f);
            var first = Add(Team.B, 10f, 0f);
            Add(Team.B, -10f, 0f);
            Assert.AreSame(first, Closest(shooter));
        }

        [Test]
        public void ClosestEnemy_IsChosenEvenIfAFartherOneIsMoreValuable()
        {
            // No priority rules: a cheap unit right in front soaks up the shot (Bible Section 6).
            var shooter = Add(Team.A, 0f, 0f);
            var chaff = Add(Team.B, 6f, 0f);
            var expensive = new SimUnit(_units.Count, Team.B, -1, TestUnits.Unit(health: 5000f, cost: 800), new Vec2(7f, 0f), Vec2.Down);
            _units.Add(expensive);
            Assert.AreSame(chaff, Closest(shooter));
        }

        [Test]
        public void Range_IsMeasuredToTheTargetsEdge()
        {
            var shooter = Add(Team.A, 0f, 0f);
            var bigTarget = Add(Team.B, 12f, 0f, radius: 3f);
            var weapon = TestUnits.Weapon(range: 10f);
            Assert.IsTrue(Targeting.IsInRange(shooter, weapon, bigTarget)); // 12 - 3 = 9 m to its edge.
            bigTarget.Position = new Vec2(13.5f, 0f);
            Assert.IsFalse(Targeting.IsInRange(shooter, weapon, bigTarget));
        }
    }
}
