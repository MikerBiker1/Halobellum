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

        // ---- Energy shields (decision D2): their own multipliers, independent of armor.

        [TestCase(DamageType.Ballistic, 75f)]
        [TestCase(DamageType.Explosive, 100f)]
        [TestCase(DamageType.Energy, 150f)]
        [TestCase(DamageType.Fire, 50f)]
        [TestCase(DamageType.Melee, 100f)]
        public void ApplyHit_Shield_UsesShieldRow_NotArmor(DamageType type, float expectedShieldDamage)
        {
            // Heavy armor would change these numbers if armor applied to shields; it must not.
            float shield = 1000f, health = 100f;
            float removed = DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 100f, type: type), ArmorType.Heavy, 1f, ref shield, ref health);
            Assert.AreEqual(1000f - expectedShieldDamage, shield, 1e-3f);
            Assert.AreEqual(100f, health);
            Assert.AreEqual(expectedShieldDamage, removed, 1e-3f);
        }

        [Test]
        public void ApplyHit_Shield_AntiArmorTagUsesAntiArmorShieldValue()
        {
            float shield = 1000f, health = 100f;
            var weapon = TestUnits.Weapon(damage: 100f, type: DamageType.Energy, antiArmor: true);
            DamageCalculator.ApplyHit(_table, weapon, ArmorType.Heavy, 1f, ref shield, ref health);
            Assert.AreEqual(950f, shield, 1e-3f);
        }

        [Test]
        public void ApplyHit_NoShield_UsesArmorMultiplier()
        {
            float shield = 0f, health = 100f;
            float removed = DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 40f, type: DamageType.Ballistic), ArmorType.Light, 1f, ref shield, ref health);
            Assert.AreEqual(76f, health, 1e-3f); // 40 x 0.6
            Assert.AreEqual(24f, removed, 1e-3f);
        }

        [Test]
        public void ApplyHit_BreakingTheShield_CarriesRemainingBaseDamageOverWithArmorMultiplier()
        {
            // Energy 100 base vs a 30-point shield: x1.5 means the shield soaks 20 base damage.
            // The other 80 base damage hits Heavy armor at x0.75 = 60.
            float shield = 30f, health = 500f;
            float removed = DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 100f, type: DamageType.Energy), ArmorType.Heavy, 1f, ref shield, ref health);
            Assert.AreEqual(0f, shield);
            Assert.AreEqual(440f, health, 1e-3f);
            Assert.AreEqual(90f, removed, 1e-3f);
        }

        [Test]
        public void ApplyHit_BreakingTheShield_WithAWeakDamageType()
        {
            // Fire 100 base vs a 20-point shield: x0.5 means the shield soaks 40 base damage.
            // The other 60 base damage hits an Unarmored body at x1.5 = 90.
            float shield = 20f, health = 200f;
            DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 100f, type: DamageType.Fire), ArmorType.Unarmored, 1f, ref shield, ref health);
            Assert.AreEqual(0f, shield);
            Assert.AreEqual(110f, health, 1e-3f);
        }

        [Test]
        public void ApplyHit_ExactlyBreakingTheShield_LeavesHealthUntouched()
        {
            float shield = 75f, health = 100f;
            DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 100f, type: DamageType.Ballistic), ArmorType.Unarmored, 1f, ref shield, ref health);
            Assert.AreEqual(0f, shield);
            Assert.AreEqual(100f, health, 1e-3f);
        }

        [Test]
        public void ApplyHit_DirectionalMultiplierScalesBaseDamage()
        {
            float shield = 0f, health = 1000f;
            DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 100f, type: DamageType.Explosive), ArmorType.Heavy, 0.2f, ref shield, ref health);
            Assert.AreEqual(980f, health, 1e-3f);
        }

        [Test]
        public void ApplyHit_HealthStopsAtZero_AndReportsOnlyRealDamage()
        {
            float shield = 0f, health = 10f;
            float removed = DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 50f), ArmorType.Unarmored, 1f, ref shield, ref health);
            Assert.AreEqual(0f, health);
            Assert.AreEqual(10f, removed, 1e-4f);
        }

        [Test]
        public void ApplyHit_ZeroDamage_DoesNothing()
        {
            float shield = 5f, health = 10f;
            Assert.AreEqual(0f, DamageCalculator.ApplyHit(_table, TestUnits.Weapon(damage: 0f), ArmorType.Unarmored, 1f, ref shield, ref health));
            Assert.AreEqual(5f, shield);
            Assert.AreEqual(10f, health);
        }
    }
}
