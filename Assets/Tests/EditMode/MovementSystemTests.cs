// MovementSystemTests.cs
// Tests free continuous-2D movement (Bible Section 6): moving at the right speed, never
// overlapping, steering around a stopped unit, faster units overtaking slower ones, air units
// ignoring ground units, staying in the arena, the spatial grid, and repeatable results.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class MovementSystemTests
    {
        private CombatRules _rules;
        private float _dt;

        [SetUp]
        public void SetUp()
        {
            _rules = TestRules.Default();
            _dt = _rules.TickSeconds;
        }

        private static SimUnit Walker(int id, Vec2 position, Vec2 goal, float speed = 5f, float radius = 0.5f, bool air = false)
        {
            var unit = new SimUnit(id, Team.A, -1, TestUnits.Unit(speed: speed, radius: radius, air: air), position, Vec2.Up);
            unit.State = UnitState.Move;
            unit.MoveGoal = goal;
            return unit;
        }

        private static SimUnit Standing(int id, Vec2 position, float radius = 0.5f)
        {
            var unit = new SimUnit(id, Team.B, -1, TestUnits.Unit(speed: 0f, radius: radius), position, Vec2.Down);
            unit.State = UnitState.Attack;
            return unit;
        }

        private void Run(MovementSystem movement, List<SimUnit> units, float seconds)
        {
            int ticks = (int)(seconds * _rules.TicksPerSecond + 0.5f);
            for (int i = 0; i < ticks; i++) movement.Step(units, _dt);
        }

        private static float WorstOverlap(List<SimUnit> units)
        {
            float worst = 0f;
            for (int i = 0; i < units.Count; i++)
            for (int j = i + 1; j < units.Count; j++)
            {
                if (units[i].IsAir != units[j].IsAir) continue;
                float overlap = units[i].Radius + units[j].Radius - Vec2.Distance(units[i].Position, units[j].Position);
                if (overlap > worst) worst = overlap;
            }
            return worst;
        }

        [Test]
        public void Unit_MovesTowardGoal_AtItsSpeed()
        {
            var unit = Walker(0, new Vec2(0f, -50f), new Vec2(0f, 50f), speed: 6f);
            var units = new List<SimUnit> { unit };
            Run(new MovementSystem(_rules), units, 1f);
            Assert.AreEqual(-44f, unit.Position.Y, 0.05f);
            Assert.AreEqual(0f, unit.Position.X, 0.001f);
            Assert.AreEqual(1f, unit.Facing.Y, 0.001f);
        }

        [Test]
        public void Unit_StopsAtGoal_WithoutOvershooting()
        {
            var unit = Walker(0, new Vec2(0f, 0f), new Vec2(0f, 1f), speed: 10f);
            var units = new List<SimUnit> { unit };
            Run(new MovementSystem(_rules), units, 1f);
            Assert.AreEqual(1f, unit.Position.Y, 0.001f);
        }

        [Test]
        public void IdleAndAttackingUnits_DoNotMoveOnTheirOwn()
        {
            var unit = Standing(0, new Vec2(3f, 3f));
            unit.MoveGoal = new Vec2(30f, 30f);
            var units = new List<SimUnit> { unit };
            Run(new MovementSystem(_rules), units, 1f);
            Assert.AreEqual(new Vec2(3f, 3f), unit.Position);
        }

        [Test]
        public void OverlappingUnits_ArePushedApart()
        {
            var a = Walker(0, new Vec2(0f, 0f), new Vec2(0f, 0f));
            var b = Walker(1, new Vec2(0.1f, 0f), new Vec2(0.1f, 0f));
            var c = Walker(2, new Vec2(0f, 0f), new Vec2(0f, 0f)); // exactly on top of a
            var units = new List<SimUnit> { a, b, c };
            Run(new MovementSystem(_rules), units, 0.5f);
            Assert.Less(WorstOverlap(units), 0.01f);
        }

        [Test]
        public void Crowd_ConvergingOnOnePoint_NeverOverlaps()
        {
            var units = new List<SimUnit>();
            int id = 0;
            for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
            {
                units.Add(Walker(id++, new Vec2(-20f + x * 4f, -40f + y * 2f), new Vec2(0f, 10f), speed: 4f + (x % 3)));
            }
            // 100 walkers all pressing toward the same point for 15 seconds is a worst-case crush
            // (in battle, units stop once a target is in range). Bodies may squeeze by at most
            // 10% of a body's width (radius 0.5 m, so 0.1 m).
            var movement = new MovementSystem(_rules);
            for (int t = 0; t < 30 * 15; t++)
            {
                movement.Step(units, _dt);
                Assert.Less(WorstOverlap(units), 0.1f, $"Overlap at tick {t}");
            }
        }

        [Test]
        public void Walker_SteersAroundAStandingUnit_WithoutPushingIt()
        {
            var blocker = Standing(1, new Vec2(0f, 0f), radius: 2f);
            var walker = Walker(0, new Vec2(0f, -10f), new Vec2(0f, 10f), speed: 5f);
            var units = new List<SimUnit> { walker, blocker };
            var movement = new MovementSystem(_rules);
            float closest = float.MaxValue;
            for (int t = 0; t < 30 * 6; t++)
            {
                movement.Step(units, _dt);
                closest = System.Math.Min(closest, Vec2.Distance(walker.Position, blocker.Position));
            }
            Assert.AreEqual(new Vec2(0f, 0f), blocker.Position, "A fighting unit must not be pushed by walkers.");
            Assert.GreaterOrEqual(closest, 2.5f - 0.01f, "Walker overlapped the blocker.");
            Assert.AreEqual(10f, walker.Position.Y, 0.1f, "Walker should get past the blocker to its goal.");
        }

        [Test]
        public void FasterUnit_OvertakesSlowerUnitAhead()
        {
            var slow = Walker(0, new Vec2(0f, 0f), new Vec2(0f, 70f), speed: 2f);
            var fast = Walker(1, new Vec2(0f, -3f), new Vec2(0f, 70f), speed: 8f);
            var units = new List<SimUnit> { slow, fast };
            Run(new MovementSystem(_rules), units, 5f);
            Assert.Greater(fast.Position.Y, slow.Position.Y + 5f);
            Assert.Less(WorstOverlap(units), 0.01f);
        }

        [Test]
        public void AirUnit_FliesStraightOverGroundUnits()
        {
            var ground = Standing(1, new Vec2(0f, 0f), radius: 3f);
            var air = Walker(0, new Vec2(0f, -10f), new Vec2(0f, 10f), speed: 5f, air: true);
            var units = new List<SimUnit> { air, ground };
            var movement = new MovementSystem(_rules);
            for (int t = 0; t < 30 * 5; t++)
            {
                movement.Step(units, _dt);
                Assert.AreEqual(0f, air.Position.X, 1e-4f, "Air unit left its straight line.");
            }
            Assert.AreEqual(10f, air.Position.Y, 0.01f);
            Assert.AreEqual(new Vec2(0f, 0f), ground.Position);
        }

        [Test]
        public void AirUnits_StillKeepApartFromEachOther()
        {
            var a = Walker(0, new Vec2(0f, 0f), new Vec2(0f, 0f), air: true);
            var b = Walker(1, new Vec2(0.2f, 0f), new Vec2(0.2f, 0f), air: true);
            var units = new List<SimUnit> { a, b };
            Run(new MovementSystem(_rules), units, 0.5f);
            Assert.Less(WorstOverlap(units), 0.01f);
        }

        [Test]
        public void Units_StayInsideTheArena()
        {
            var unit = Walker(0, new Vec2(0f, 0f), new Vec2(500f, 500f), speed: 20f, radius: 1f);
            var units = new List<SimUnit> { unit };
            Run(new MovementSystem(_rules), units, 10f);
            Assert.AreEqual(_rules.ArenaWidth / 2f - 1f, unit.Position.X, 1e-3f);
            Assert.AreEqual(_rules.ArenaLength / 2f - 1f, unit.Position.Y, 1e-3f);
        }

        [Test]
        public void DeadUnits_AreIgnored()
        {
            var dead = Standing(1, new Vec2(0f, 0f), radius: 2f);
            dead.State = UnitState.Dead;
            var walker = Walker(0, new Vec2(0f, -5f), new Vec2(0f, 5f));
            var units = new List<SimUnit> { walker, dead };
            Run(new MovementSystem(_rules), units, 3f);
            Assert.AreEqual(0f, walker.Position.X, 1e-4f, "Walker swerved around a dead unit.");
            Assert.AreEqual(5f, walker.Position.Y, 0.01f);
        }

        [Test]
        public void SpatialGrid_QueryMatchesBruteForce()
        {
            var rng = new SimRandom(5);
            float halfWidth = _rules.ArenaWidth / 2f, halfLength = _rules.ArenaLength / 2f;
            var units = new List<SimUnit>();
            for (int i = 0; i < 300; i++)
            {
                var p = new Vec2(rng.Range(-halfWidth, halfWidth), rng.Range(-halfLength, halfLength));
                units.Add(Walker(i, p, p));
            }
            units[10].State = UnitState.Dead;
            var grid = new SpatialHashGrid(_rules.ArenaWidth, _rules.ArenaLength, _rules.SpatialCellSize);
            grid.Build(units);

            var found = new List<SimUnit>();
            for (int q = 0; q < 50; q++)
            {
                var center = new Vec2(rng.Range(-halfWidth, halfWidth), rng.Range(-halfLength, halfLength));
                float radius = rng.Range(0.5f, 15f);
                grid.Query(center, radius, found);
                int expected = 0;
                foreach (var u in units) if (u.IsAlive && Vec2.Distance(u.Position, center) <= radius) expected++;
                Assert.AreEqual(expected, found.Count);
                foreach (var u in found) Assert.IsTrue(u.IsAlive);
            }
        }

        [Test]
        public void SameSetup_GivesIdenticalPositions()
        {
            List<SimUnit> Build()
            {
                var list = new List<SimUnit>();
                for (int i = 0; i < 60; i++) list.Add(Walker(i, new Vec2(-15f + (i % 10) * 3f, -30f + (i / 10) * 2f), new Vec2(0f, 20f), speed: 3f + i % 4));
                return list;
            }
            var first = Build();
            var second = Build();
            var m1 = new MovementSystem(_rules);
            var m2 = new MovementSystem(_rules);
            for (int t = 0; t < 300; t++)
            {
                m1.Step(first, _dt);
                m2.Step(second, _dt);
            }
            for (int i = 0; i < first.Count; i++) Assert.AreEqual(first[i].Position, second[i].Position);
        }
    }
}
