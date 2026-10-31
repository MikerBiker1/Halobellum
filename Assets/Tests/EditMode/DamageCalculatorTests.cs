// DamageCalculatorTests.cs
// Tests the damage and armor math (Bible Section 4, rule 10): damage table lookups for every
// damage type and armor type, the anti-armor tag, and shields absorbing damage before health.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class DamageCalculatorTests
    {
        private DamageTable _table;

        [SetUp]
        public void SetUp()
        {
            _table = TestRules.Default().DamageTable;
        }

        // Every cell of the Bible's damage table: 100 damage per shot times the multiplier.
        [TestCase(DamageType.Ballistic, ArmorType.Unarmored, 100f)]
        [TestCase(DamageType.Ballistic, ArmorType.Light, 60f)]
        [TestCase(DamageType.Ballistic, ArmorType.Heavy, 25f)]
        [TestCase(DamageType.Explosive, ArmorType.Unarmored, 100f)]
        [TestCase(DamageType.Explosive, ArmorType.Light, 100f)]
        [TestCase(DamageType.Explosive, ArmorType.Heavy, 100f)]
        [TestCase(DamageType.Energy, ArmorType.Unarmored, 100f)]
        [TestCase(DamageType.Energy, ArmorType.Light, 100f)]
        [TestCase(DamageType.Energy, ArmorType.Heavy, 75f)]
        [TestCase(DamageType.Fire, ArmorType.Unarmored, 150f)]
        [TestCase(DamageType.Fire, ArmorType.Light, 80f)]
        [TestCase(DamageType.Fire, ArmorType.Heavy, 30f)]
        [TestCase(DamageType.Melee, ArmorType.Unarmored, 100f)]
        [TestCase(DamageType.Melee, ArmorType.Light, 70f)]
        [TestCase(DamageType.Melee, ArmorType.Heavy, 30f)]
        public void ComputeDamage_UsesDamageTypeRow(DamageType type, ArmorType armor, float expected)
        {
            var weapon = TestUnits.Weapon(damage: 100f, type: type);
            Assert.AreEqual(expected, DamageCalculator.ComputeDamage(_table, weapon, armor), 1e-4f);
        }

        [TestCase(ArmorType.Unarmored, 50f)]
        [TestCase(ArmorType.Light, 120f)]
        [TestCase(ArmorType.Heavy, 150f)]
        public void ComputeDamage_AntiArmorTag_UsesAntiArmorRowInsteadOfTypeRow(ArmorType armor, float expected)
        {
            // A ballistic weapon would only do 25 to Heavy armor; the anti-armor tag replaces that row.
            var weapon = TestUnits.Weapon(damage: 100f, type: DamageType.Ballistic, antiArmor: true);
            Assert.AreEqual(expected, DamageCalculator.ComputeDamage(_table, weapon, armor), 1e-4f);
        }

        [Test]
        public void ComputeDamage_AppliesDirectionalMultiplier()
        {
            var weapon = TestUnits.Weapon(damage: 100f, type: DamageType.Energy);
            Assert.AreEqual(37.5f, DamageCalculator.ComputeDamage(_table, weapon, ArmorType.Heavy, 0.5f), 1e-4f);
        }

        [Test]
        public void ComputeDamage_ChangingTheTableChangesTheResult()
        {
            _table.Set(DamageType.Ballistic, ArmorType.Heavy, 0.5f);
            var weapon = TestUnits.Weapon(damage: 100f, type: DamageType.Ballistic);
            Assert.AreEqual(50f, DamageCalculator.ComputeDamage(_table, weapon, ArmorType.Heavy), 1e-4f);
        }

        [Test]
        public void ApplyDamage_NoShield_HitsHealth()
        {
            float shield = 0f, health = 100f;
            float dealt = DamageCalculator.ApplyDamage(30f, ref shield, ref health);
            Assert.AreEqual(70f, health, 1e-4f);
            Assert.AreEqual(30f, dealt, 1e-4f);
        }

        [Test]
        public void ApplyDamage_ShieldAbsorbsFirst()
        {
            float shield = 50f, health = 100f;
            DamageCalculator.ApplyDamage(30f, ref shield, ref health);
            Assert.AreEqual(20f, shield, 1e-4f);
            Assert.AreEqual(100f, health, 1e-4f);
        }

        [Test]
        public void ApplyDamage_ExcessOverflowsFromShieldToHealth()
        {
            float shield = 20f, health = 100f;
            DamageCalculator.ApplyDamage(50f, ref shield, ref health);
            Assert.AreEqual(0f, shield, 1e-4f);
            Assert.AreEqual(70f, health, 1e-4f);
        }

        [Test]
        public void ApplyDamage_HealthStopsAtZero_AndReportsOnlyRealDamage()
        {
            float shield = 0f, health = 10f;
            float dealt = DamageCalculator.ApplyDamage(50f, ref shield, ref health);
            Assert.AreEqual(0f, health);
            Assert.AreEqual(10f, dealt, 1e-4f);
        }

        [Test]
        public void ApplyDamage_ZeroOrNegative_DoesNothing()
        {
            float shield = 5f, health = 10f;
            Assert.AreEqual(0f, DamageCalculator.ApplyDamage(-5f, ref shield, ref health));
            Assert.AreEqual(5f, shield);
            Assert.AreEqual(10f, health);
        }
    }
}
