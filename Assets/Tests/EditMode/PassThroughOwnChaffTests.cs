// PassThroughOwnChaffTests.cs
// D146: the Carrier Form walks through its own side's chaff (its Infection Forms crowding a target) instead of being held back behind it,
// so it reaches the enemy and bursts. Without the flag the same wall holds it back.

using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class PassThroughOwnChaffTests
    {
        private static bool BurstsWithinTenSeconds(bool passesThrough)
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats carrier = TestUnits.Unit(name: "Carrier", health: 1000f, speed: 2f, radius: 0.8f, filter: TargetFilter.Ground, weapon: TestUnits.Weapon(damage: 0f, range: 1.5f));
            carrier.SelfDestructs = true;
            carrier.PassesThroughOwnChaff = passesThrough;
            // A packed wall of its own chaff that stands still and shoots only air, between it and a harmless enemy that stands still.
            UnitStats chaff = TestUnits.Unit(name: "Forms", health: 1000f, speed: 0f, radius: 0.8f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(range: 1f));
            chaff.Role = UnitRole.Chaff;
            UnitStats target = TestUnits.Unit(name: "Hull", health: 100000f, speed: 0f, radius: 2f, filter: TargetFilter.Air, weapon: TestUnits.Weapon(range: 1f));
            target.Role = UnitRole.Tank;
            SimUnit walker = sim.AddUnit(carrier, Team.A, new Vec2(0f, 0f));
            for (float x = -12f; x <= 12f; x += 1.6f)
            {
                sim.AddUnit(chaff, Team.A, new Vec2(x, 6f));
                sim.AddUnit(chaff, Team.A, new Vec2(x + 0.8f, 7.4f));
            }
            sim.AddUnit(target, Team.B, new Vec2(0f, 11f));
            int ticks = (int)System.Math.Round(10f / sim.TickSeconds);
            for (int i = 0; i < ticks && walker.IsAlive; i++) sim.Step();
            return !walker.IsAlive;
        }

        [Test]
        public void ACarrier_WalksThroughItsOwnChaff_AndBurstsOnTheEnemyBehindIt()
        {
            Assert.IsFalse(BurstsWithinTenSeconds(passesThrough: false), "The test's premise: the wall holds an ordinary unit back.");
            Assert.IsTrue(BurstsWithinTenSeconds(passesThrough: true), "Through its own chaff to the enemy, and it bursts.");
        }
    }
}
