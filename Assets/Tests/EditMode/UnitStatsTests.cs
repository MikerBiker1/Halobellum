// UnitStatsTests.cs
// Checks the plain C# unit data: target filters, validation of broken data, and deep copies.
// Depends on: Halobellum.Core, NUnit.

using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class UnitStatsTests
    {
        [Test]
        public void TargetFilter_GroundOnly_CannotTargetAir()
        {
            var unit = TestUnits.Unit(filter: TargetFilter.Ground);
            Assert.IsTrue(unit.CanTarget(targetIsAir: false));
            Assert.IsFalse(unit.CanTarget(targetIsAir: true));
        }

        [Test]
        public void TargetFilter_AirOnly_CannotTargetGround()
        {
            var unit = TestUnits.Unit(filter: TargetFilter.Air);
            Assert.IsFalse(unit.CanTarget(targetIsAir: false));
            Assert.IsTrue(unit.CanTarget(targetIsAir: true));
        }

        [Test]
        public void TargetFilter_Both_TargetsEverything()
        {
            var unit = TestUnits.Unit(filter: TargetFilter.Both);
            Assert.IsTrue(unit.CanTarget(false));
            Assert.IsTrue(unit.CanTarget(true));
        }

        [Test]
        public void Validate_GoodUnit_HasNoProblems()
        {
            Assert.IsEmpty(TestUnits.Unit().Validate());
        }

        [Test]
        public void Validate_BrokenUnit_ReportsEachProblem()
        {
            var unit = TestUnits.Unit(health: 0f, radius: 0f);
            unit.Weapons[0].FireInterval = 0f;
            var problems = unit.Validate();
            Assert.AreEqual(3, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void Validate_MinRangeNotBelowRange_IsAProblem()
        {
            var unit = TestUnits.Unit(weapon: TestUnits.Weapon(range: 10f, minRange: 10f));
            Assert.AreEqual(1, unit.Validate().Count);
        }

        [Test]
        public void Clone_IsDeep()
        {
            var unit = TestUnits.Unit();
            unit.DefenseArc = new DefenseArcStats { ArcDegrees = 120f, BlockedDamageTypes = new[] { DamageType.Ballistic } };
            var copy = unit.Clone();
            copy.Weapons[0].DamagePerShot = 999f;
            copy.DefenseArc.BlockedDamageTypes[0] = DamageType.Fire;
            Assert.AreEqual(10f, unit.Weapons[0].DamagePerShot);
            Assert.AreEqual(DamageType.Ballistic, unit.DefenseArc.BlockedDamageTypes[0]);
        }

        [Test]
        public void Weapon_ShotsPerSecond_IsInverseOfInterval()
        {
            Assert.AreEqual(4f, TestUnits.Weapon(interval: 0.25f).ShotsPerSecond, 1e-5f);
        }
    }
}
