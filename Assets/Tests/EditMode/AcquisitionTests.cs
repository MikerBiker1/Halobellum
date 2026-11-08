// AcquisitionTests.cs
// Tests the acquisition range: units only notice enemies within 3 x their weapon range; with
// nothing in sight they march straight ahead (Team A up, Team B down); once inside the enemy's
// deployment zone they notice enemies at any distance. Two units walking at each other pass.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class AcquisitionTests
    {
        private CombatRules _rules;

        [SetUp]
        public void SetUp() => _rules = TestRules.Default();

        private static UnitStats Walker(float range) => TestUnits.Unit(speed: 4f, weapon: TestUnits.Weapon(damage: 1f, range: range));

        private static UnitStats Dummy() => TestUnits.Unit(health: 100000f, speed: 0f, filter: TargetFilter.Air);

        private static void StepSeconds(BattleSimulation sim, float seconds)
        {
            int ticks = (int)System.Math.Round(seconds / sim.TickSeconds);
            for (int i = 0; i < ticks; i++) sim.Step();
        }

        [Test]
        public void NothingInSight_MarchesStraightAhead()
        {
            var sim = new BattleSimulation(_rules, 1);
            var unit = sim.AddUnit(Walker(12f), Team.A, new Vec2(0f, -60f));
            sim.AddUnit(Dummy(), Team.B, new Vec2(50f, 60f));

            StepSeconds(sim, 2f);
            Assert.IsNull(unit.MoveTarget);
            Assert.AreEqual(UnitState.Move, unit.State);
            Assert.AreEqual(0f, unit.Position.X, 0.01f, "Should march straight ahead, not toward the far enemy.");
            Assert.Greater(unit.Position.Y, -54f);
        }

        [Test]
        public void TeamB_MarchesDown()
        {
            var sim = new BattleSimulation(_rules, 1);
            var unit = sim.AddUnit(Walker(12f), Team.B, new Vec2(0f, 60f));
            sim.AddUnit(Dummy(), Team.A, new Vec2(-50f, -60f));
            StepSeconds(sim, 2f);
            Assert.Less(unit.Position.Y, 54f);
            Assert.AreEqual(0f, unit.Position.X, 0.01f);
        }

        [Test]
        public void EnemyWithinThreeTimesRange_IsAcquired_FurtherIsNot()
        {
            var sim = new BattleSimulation(_rules, 1);
            var near = sim.AddUnit(Walker(12f), Team.A, new Vec2(-30f, -40f));
            var far = sim.AddUnit(Walker(12f), Team.A, new Vec2(30f, -40f));
            var enemyNear = sim.AddUnit(Dummy(), Team.B, new Vec2(-30f, -40f + 35f)); // 34.5 m to its edge: within 36 m.
            sim.AddUnit(Dummy(), Team.B, new Vec2(30f, -40f + 38f));                  // 37.5 m: beyond 36 m.

            sim.Step();
            Assert.AreSame(enemyNear, near.MoveTarget);
            Assert.IsNull(far.MoveTarget);
        }

        [Test]
        public void EveryUnit_NoticesEnemiesFrom30m_HoweverShortItsReach()
        {
            // D108: a claw's 1.5 m reach x 3 would be 4.5 m; the rules' minimum, 30 m, holds.
            var sim = new BattleSimulation(_rules, 1);
            var near = sim.AddUnit(Walker(1.5f), Team.A, new Vec2(-30f, -40f));
            var far = sim.AddUnit(Walker(1.5f), Team.A, new Vec2(30f, -40f));
            var enemyNear = sim.AddUnit(Dummy(), Team.B, new Vec2(-30f, -40f + 29f)); // 28.5 m to its edge.
            sim.AddUnit(Dummy(), Team.B, new Vec2(30f, -40f + 32f));                  // 31.5 m.

            sim.Step();
            Assert.AreSame(enemyNear, near.MoveTarget);
            Assert.IsNull(far.MoveTarget);
        }

        [Test]
        public void EnteringTheEnemyZone_MakesAcquisitionUnlimited()
        {
            // Range 5 (acquisition 30 m, the minimum) and an enemy far away to the side: nothing in sight until the zone.
            var sim = new BattleSimulation(_rules, 1);
            var unit = sim.AddUnit(Walker(5f), Team.A, new Vec2(-70f, -6f));
            var enemy = sim.AddUnit(Dummy(), Team.B, new Vec2(70f, 70f));

            StepSeconds(sim, 0.5f);
            Assert.IsFalse(unit.UnlimitedAcquisition);
            Assert.IsNull(unit.MoveTarget);

            StepSeconds(sim, 2.5f); // Crosses y = +2 m (the enemy zone's front edge) after about 2 s.
            Assert.IsTrue(unit.UnlimitedAcquisition);
            Assert.AreSame(enemy, unit.MoveTarget);
        }

        [Test]
        public void TwoUnitsWalkingAtEachOther_PassEachOther()
        {
            var sim = new BattleSimulation(_rules, 1);
            var a = sim.AddUnit(TestUnits.Unit(speed: 4f, filter: TargetFilter.Air), Team.A, new Vec2(0f, -10f));
            var b = sim.AddUnit(TestUnits.Unit(speed: 4f, filter: TargetFilter.Air), Team.B, new Vec2(0f, 10f));
            StepSeconds(sim, 8f);
            Assert.Greater(a.Position.Y, 2f, "Team A unit should get past into the enemy zone.");
            Assert.Less(b.Position.Y, -2f, "Team B unit should get past into the enemy zone.");
        }

        // D115 (the owner, 2026-11-08): Infested Ghosts, a ram below and plasma bolts that reach air, stood still while Infested Hornets outranged them.

        private static UnitStats Hornet() => TestUnits.Unit(name: "Hornet", health: 100000f, speed: 0f, air: true, weapon: TestUnits.Weapon(damage: 0f, range: 1f));

        [Test]
        public void AUnitWhoseMainWeaponCantHitAir_GoesAfterItWithItsOtherWeapon()
        {
            UnitStats ghost = TestUnits.Unit(name: "Ghost", speed: 4f, filter: TargetFilter.Ground, weapon: TestUnits.Weapon(damage: 5f, range: 1.5f, type: DamageType.Melee));
            WeaponStats bolts = TestUnits.Weapon(damage: 1f, range: 9f);
            bolts.HasOwnTargetFilter = true;
            bolts.TargetFilter = TargetFilter.Both;
            ghost.Weapons = new[] { ghost.Weapons[0], bolts };
            var sim = new BattleSimulation(_rules, 1);
            SimUnit unit = sim.AddUnit(ghost, Team.A, new Vec2(0f, 30f));
            SimUnit hornet = sim.AddUnit(Hornet(), Team.B, new Vec2(20f, 40f));
            unit.UnlimitedAcquisition = true;
            StepSeconds(sim, 6f);
            Assert.AreSame(hornet, unit.MoveTarget);
            Assert.LessOrEqual(Targeting.EdgeDistance(unit, hornet), 9f, "It came within its bolts' reach.");
            Assert.Less(hornet.Health, 100000f, "And shot at it.");
        }

        [Test]
        public void AUnitThatCantHitAirAtAll_StillWalksToIt_AsTheLastThingItDoes()
        {
            UnitStats tank = TestUnits.Unit(name: "Tank", speed: 4f, filter: TargetFilter.Ground, weapon: TestUnits.Weapon(damage: 5f, range: 10f));
            var sim = new BattleSimulation(_rules, 1);
            SimUnit unit = sim.AddUnit(tank, Team.A, new Vec2(0f, 30f));
            SimUnit hornet = sim.AddUnit(Hornet(), Team.B, new Vec2(20f, 45f));
            unit.UnlimitedAcquisition = true;
            Vec2 start = unit.Position;
            StepSeconds(sim, 3f);
            Assert.AreSame(hornet, unit.MoveTarget);
            Assert.Less(Vec2.Distance(unit.Position, hornet.Position), Vec2.Distance(start, hornet.Position) - 5f, "It walks toward the air unit instead of standing idle.");
        }
    }
}
