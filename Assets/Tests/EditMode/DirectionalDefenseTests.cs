// DirectionalDefenseTests.cs
// Tests optional directional defense (Bible Section 4, rule 10): arc edges, front and rear
// multipliers, block chance against blockable ballistic projectiles, and seeded repeatability.
// Arc shapes mirror the Bible's Shield Jackal (120 degrees, 80% ballistic block) and Hunter
// (240 degrees); the Hunter multipliers here are test values only.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class DirectionalDefenseTests
    {
        private static readonly Vec2 Origin = Vec2.Zero;
        private static readonly Vec2 FacingUp = Vec2.Up;

        private static DefenseArcStats JackalArc() => new DefenseArcStats
        {
            ArcDegrees = 120f,
            FrontDamageMultiplier = 1f,
            RearDamageMultiplier = 1f,
            BlockChance = 0.8f,
            BlockedDamageTypes = new[] { DamageType.Ballistic },
        };

        private static DefenseArcStats HunterArc() => new DefenseArcStats
        {
            ArcDegrees = 240f,
            FrontDamageMultiplier = 0.2f,
            RearDamageMultiplier = 1.5f,
            BlockChance = 0f,
        };

        /// <summary>A point 10 m away from the origin at the given angle from straight up (clockwise or not doesn't matter).</summary>
        private static Vec2 SourceAt(float degreesFromFacing) => Vec2.Up.Rotated(degreesFromFacing) * 10f;

        [TestCase(0f, true)]
        [TestCase(59f, true)]
        [TestCase(60f, true)]
        [TestCase(-60f, true)]
        [TestCase(61f, false)]
        [TestCase(-61f, false)]
        [TestCase(180f, false)]
        public void IsInsideArc_120Degrees(float angle, bool expectedInside)
        {
            Assert.AreEqual(expectedInside, DirectionalDefense.IsInsideArc(Origin, FacingUp, SourceAt(angle), 120f));
        }

        [TestCase(119f, true)]
        [TestCase(-119f, true)]
        [TestCase(121f, false)]
        [TestCase(180f, false)]
        public void IsInsideArc_240Degrees(float angle, bool expectedInside)
        {
            Assert.AreEqual(expectedInside, DirectionalDefense.IsInsideArc(Origin, FacingUp, SourceAt(angle), 240f));
        }

        [Test]
        public void IsInsideArc_FollowsFacing()
        {
            // Facing right: a hit from the right is front, a hit from above is outside a 120 degree arc.
            Assert.IsTrue(DirectionalDefense.IsInsideArc(Origin, Vec2.Right, new Vec2(10f, 0f), 120f));
            Assert.IsFalse(DirectionalDefense.IsInsideArc(Origin, Vec2.Right, new Vec2(0f, 10f), 120f));
        }

        [Test]
        public void NoArc_AlwaysNormalDamage()
        {
            var result = DirectionalDefense.Evaluate(null, Origin, FacingUp, SourceAt(180f),
                TestUnits.Weapon(blockable: true), true, new SimRandom(1));
            Assert.IsFalse(result.Blocked);
            Assert.AreEqual(1f, result.DamageMultiplier);
        }

        [Test]
        public void Hunter_FrontUsesFrontMultiplier_RearUsesRearMultiplier()
        {
            var weapon = TestUnits.Weapon(type: DamageType.Energy);
            var rng = new SimRandom(1);
            var front = DirectionalDefense.Evaluate(HunterArc(), Origin, FacingUp, SourceAt(100f), weapon, true, rng);
            var rear = DirectionalDefense.Evaluate(HunterArc(), Origin, FacingUp, SourceAt(170f), weapon, true, rng);
            Assert.IsTrue(front.FromFront);
            Assert.AreEqual(0.2f, front.DamageMultiplier, 1e-5f);
            Assert.IsFalse(rear.FromFront);
            Assert.AreEqual(1.5f, rear.DamageMultiplier, 1e-5f);
        }

        [Test]
        public void Hunter_FullHit_CombinesWithDamageTable()
        {
            var table = TestRules.Default().DamageTable;
            var weapon = TestUnits.Weapon(damage: 100f, type: DamageType.Explosive);
            var rear = DirectionalDefense.Evaluate(HunterArc(), Origin, FacingUp, SourceAt(180f), weapon, true, new SimRandom(1));
            Assert.AreEqual(150f, DamageCalculator.ComputeDamage(table, weapon, ArmorType.Heavy, rear.DamageMultiplier), 1e-4f);
        }

        [Test]
        public void Jackal_BlocksAbout80PercentOfFrontalBallisticProjectiles()
        {
            var weapon = TestUnits.Weapon(type: DamageType.Ballistic, blockable: true);
            var rng = new SimRandom(12345);
            int blocked = 0;
            const int shots = 20000;
            for (int i = 0; i < shots; i++)
            {
                if (DirectionalDefense.Evaluate(JackalArc(), Origin, FacingUp, SourceAt(30f), weapon, true, rng).Blocked) blocked++;
            }
            Assert.AreEqual(0.8, blocked / (double)shots, 0.02);
        }

        [Test]
        public void Jackal_NeverBlocksFromBehind()
        {
            var weapon = TestUnits.Weapon(type: DamageType.Ballistic, blockable: true);
            var rng = new SimRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(DirectionalDefense.Evaluate(JackalArc(), Origin, FacingUp, SourceAt(150f), weapon, true, rng).Blocked);
            }
        }

        [Test]
        public void Jackal_NeverBlocksUnlistedDamageTypes()
        {
            var weapon = TestUnits.Weapon(type: DamageType.Energy, blockable: true);
            var rng = new SimRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(DirectionalDefense.Evaluate(JackalArc(), Origin, FacingUp, SourceAt(0f), weapon, true, rng).Blocked);
            }
        }

        [Test]
        public void Jackal_NeverBlocksWeaponsWithoutTheBlockableFlag()
        {
            var weapon = TestUnits.Weapon(type: DamageType.Ballistic, blockable: false);
            var rng = new SimRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(DirectionalDefense.Evaluate(JackalArc(), Origin, FacingUp, SourceAt(0f), weapon, true, rng).Blocked);
            }
        }

        [Test]
        public void Jackal_NeverBlocksSplashDamage()
        {
            var weapon = TestUnits.Weapon(type: DamageType.Ballistic, blockable: true, splash: 3f);
            var rng = new SimRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                Assert.IsFalse(DirectionalDefense.Evaluate(JackalArc(), Origin, FacingUp, SourceAt(0f), weapon, false, rng).Blocked);
            }
        }

        [Test]
        public void BlockRolls_AreRepeatableWithTheSameSeed()
        {
            var weapon = TestUnits.Weapon(type: DamageType.Ballistic, blockable: true);
            var first = new SimRandom(99);
            var second = new SimRandom(99);
            for (int i = 0; i < 500; i++)
            {
                bool a = DirectionalDefense.Evaluate(JackalArc(), Origin, FacingUp, SourceAt(0f), weapon, true, first).Blocked;
                bool b = DirectionalDefense.Evaluate(JackalArc(), Origin, FacingUp, SourceAt(0f), weapon, true, second).Blocked;
                Assert.AreEqual(a, b, $"Roll {i} differed");
            }
        }
    }
}
