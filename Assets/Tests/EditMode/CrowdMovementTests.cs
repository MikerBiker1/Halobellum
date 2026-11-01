// CrowdMovementTests.cs
// Crowd movement fixes asked for by the project owner after playing (decision D30):
//   - slower units step aside for a faster friendly unit coming from behind, so a fast vehicle is not
//     stuck behind a marching chaff squad, and the vehicle drives straight through while they split;
//   - big units (tanks, Titans) never make way for smaller ones: those go around them;
//   - a fighting front line edges forward while friendly units are stuck behind it, so a second squad
//     of the same unit gets into range instead of jiggling behind the first.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class CrowdMovementTests
    {
        /// <summary>An enemy that soaks damage forever and never shoots ground units.</summary>
        private static UnitStats Wall() => TestUnits.Unit(name: "Wall", health: 1e9f, speed: 0f, filter: TargetFilter.Air, radius: 1f);

        [Test]
        public void ChaffSquad_StepsAside_ForAFasterVehicleBehindIt()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats chaff = TestUnits.Unit(name: "Chaff", speed: 2f, radius: 0.5f, squadSize: 18, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 1f, range: 12f));
            UnitStats vehicle = TestUnits.Unit(name: "Vehicle", speed: 9f, radius: 1.2f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 1f, range: 10f));
            vehicle.TurnTime180 = 1f;
            sim.AddSquad(chaff, Team.A, new Vec2(0f, -40f));
            SimUnit car = sim.AddUnit(vehicle, Team.A, new Vec2(0f, -46f));
            sim.AddUnit(Wall(), Team.B, new Vec2(0f, 55f));

            float widestSwerve = 0f, widestDrift = 0f;
            for (int i = 0; i < 5 * 30; i++)
            {
                sim.Step();
                widestSwerve = System.Math.Max(widestSwerve, System.Math.Abs(car.Facing.X));
                widestDrift = System.Math.Max(widestDrift, System.Math.Abs(car.Position.X));
            }

            float chaffFront = sim.Units.Where(u => u.Stats == chaff).Max(u => u.Position.Y);
            Assert.Greater(car.Position.Y, chaffFront + 2f, "The faster vehicle got past the chaff squad in front of it.");
            Assert.Less(widestSwerve, 0.15f, "It keeps heading straight for its target (no turning left and right).");
            Assert.Less(widestDrift, 0.5f, "It stays on its line while the chaff splits.");
        }

        [Test]
        public void BigSlowTank_DoesNotMakeWay_FasterSmallUnitsGoAroundIt()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats tank = TestUnits.Unit(name: "Tank", speed: 2f, radius: 2f, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 1f, range: 12f));
            tank.TurnTime180 = 2f;
            UnitStats infantry = TestUnits.Unit(name: "Infantry", speed: 4f, radius: 0.5f, squadSize: 12, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 1f, range: 10f));
            SimUnit big = sim.AddUnit(tank, Team.A, new Vec2(0f, -40f));
            sim.AddSquad(infantry, Team.A, new Vec2(0f, -45f));
            sim.AddUnit(Wall(), Team.B, new Vec2(0f, 55f));

            float widestDrift = 0f, strongestYield = 0f;
            for (int i = 0; i < 6 * 30; i++)
            {
                sim.Step();
                widestDrift = System.Math.Max(widestDrift, System.Math.Abs(big.Position.X));
                strongestYield = System.Math.Max(strongestYield, big.YieldSteer.Length);
            }

            Assert.AreEqual(0f, strongestYield, "The tank is never asked to make way for smaller units.");
            Assert.Less(widestDrift, 0.25f, "It keeps its line (only jostled a little as infantry squeeze past).");
            int passed = sim.Units.Count(u => u.Stats == infantry && u.Position.Y > big.Position.Y);
            Assert.GreaterOrEqual(passed, 10, "The faster infantry went around it.");
        }

        [Test]
        public void FightingFrontLine_EdgesForward_SoTheSquadBehindGetsIntoRange()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats infantry = TestUnits.Unit(name: "Infantry", speed: 3f, radius: 0.5f, squadSize: 12, filter: TargetFilter.Ground,
                weapon: TestUnits.Weapon(damage: 1f, range: 8f));
            for (int i = -2; i <= 2; i++) sim.AddUnit(Wall(), Team.B, new Vec2(i * 2.2f, 4f));
            int front = sim.AddSquad(infantry, Team.A, new Vec2(0f, -12f));
            int rear = sim.AddSquad(infantry, Team.A, new Vec2(0f, -17f));

            for (int i = 0; i < 12 * 30; i++) sim.Step();

            int frontFiring = sim.Units.Count(u => u.SquadId == front && u.State == UnitState.Attack);
            int rearFiring = sim.Units.Count(u => u.SquadId == rear && u.State == UnitState.Attack);
            Assert.AreEqual(12, frontFiring, "The front squad keeps fighting while it edges forward.");
            Assert.GreaterOrEqual(rearFiring, 10, "Most of the squad behind got into range.");
            float closest = sim.Units.Where(u => u.SquadId == front).Min(u => DistanceToWall(sim, u));
            Assert.Greater(closest, 1.5f, "Edging forward stops well before walking into the enemy.");
        }

        private static float DistanceToWall(BattleSimulation sim, SimUnit unit) =>
            sim.Units.Where(u => u.Team == Team.B).Min(w => Vec2.Distance(unit.Position, w.Position) - w.Radius - unit.Radius);
    }
}
