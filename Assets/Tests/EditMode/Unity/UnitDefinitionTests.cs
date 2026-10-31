// UnitDefinitionTests.cs
// Checks that a UnitDefinition ScriptableObject copies every value into plain UnitStats.
// Needs the Unity engine (ScriptableObject), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.Core, NUnit, UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class UnitDefinitionTests
    {
        private UnitDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<UnitDefinition>();
            _definition.unitName = "Test Trooper";
            _definition.role = UnitRole.Tank;
            _definition.tier = 2;
            _definition.unlockCost = 50;
            _definition.squadSize = 3;
            _definition.cost = 250;
            _definition.baseDamage = 16f;
            _definition.maxHealth = 400f;
            _definition.armorType = ArmorType.Heavy;
            _definition.maxShield = 50f;
            _definition.shieldRegenPerSecond = 10f;
            _definition.moveSpeed = 4f;
            _definition.collisionRadius = 1.5f;
            _definition.turnTime180 = 2f;
            _definition.isAir = true;
            _definition.targetFilter = TargetFilter.Air;
            _definition.weapons = new List<WeaponDefinition>
            {
                new WeaponDefinition
                {
                    weaponName = "Cannon", damagePerShot = 80f, fireInterval = 2f, range = 20f, minRange = 5f,
                    projectileSpeed = 30f, splashRadius = 2f, damageType = DamageType.Explosive,
                    isAntiArmor = true, canBeBlocked = true,
                },
            };
            _definition.hasDefenseArc = true;
            _definition.defenseArc = new DefenseArcDefinition
            {
                arcDegrees = 240f, frontDamageMultiplier = 0.2f, rearDamageMultiplier = 1.5f, blockChance = 0.8f,
                blockedDamageTypes = new List<DamageType> { DamageType.Ballistic },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void ToStats_CopiesUnitValues()
        {
            UnitStats s = _definition.ToStats();
            Assert.AreEqual("Test Trooper", s.Name);
            Assert.AreEqual(UnitRole.Tank, s.Role);
            Assert.AreEqual(2, s.Tier);
            Assert.AreEqual(50, s.UnlockCost);
            Assert.AreEqual(3, s.SquadSize);
            Assert.AreEqual(250, s.Cost);
            Assert.AreEqual(16f, s.BaseDamage);
            Assert.AreEqual(400f, s.MaxHealth);
            Assert.AreEqual(ArmorType.Heavy, s.ArmorType);
            Assert.AreEqual(50f, s.MaxShield);
            Assert.AreEqual(10f, s.ShieldRegenPerSecond);
            Assert.AreEqual(4f, s.MoveSpeed);
            Assert.AreEqual(1.5f, s.CollisionRadius);
            Assert.AreEqual(2f, s.TurnTime180);
            Assert.IsTrue(s.IsAir);
            Assert.AreEqual(TargetFilter.Air, s.TargetFilter);
            Assert.IsEmpty(s.Validate());
        }

        [Test]
        public void ToStats_CopiesWeapons()
        {
            WeaponStats w = _definition.ToStats().Weapons[0];
            Assert.AreEqual("Cannon", w.Name);
            Assert.AreEqual(80f, w.DamagePerShot);
            Assert.AreEqual(2f, w.FireInterval);
            Assert.AreEqual(20f, w.Range);
            Assert.AreEqual(5f, w.MinRange);
            Assert.AreEqual(30f, w.ProjectileSpeed);
            Assert.AreEqual(2f, w.SplashRadius);
            Assert.AreEqual(DamageType.Explosive, w.DamageType);
            Assert.IsTrue(w.IsAntiArmor);
            Assert.IsTrue(w.CanBeBlocked);
        }

        [Test]
        public void ToStats_CopiesDefenseArc_WhenEnabled()
        {
            DefenseArcStats arc = _definition.ToStats().DefenseArc;
            Assert.IsNotNull(arc);
            Assert.AreEqual(240f, arc.ArcDegrees);
            Assert.AreEqual(0.2f, arc.FrontDamageMultiplier);
            Assert.AreEqual(1.5f, arc.RearDamageMultiplier);
            Assert.AreEqual(0.8f, arc.BlockChance);
            Assert.IsTrue(arc.Blocks(DamageType.Ballistic));
            Assert.IsFalse(arc.Blocks(DamageType.Energy));
        }

        [Test]
        public void ToStats_NoDefenseArc_WhenDisabled()
        {
            _definition.hasDefenseArc = false;
            Assert.IsNull(_definition.ToStats().DefenseArc);
            Assert.IsFalse(_definition.ToStats().HasDefenseArc);
        }

        [Test]
        public void ToStats_ReturnsIndependentCopies()
        {
            UnitStats first = _definition.ToStats();
            first.Weapons[0].DamagePerShot = 1f;
            Assert.AreEqual(80f, _definition.ToStats().Weapons[0].DamagePerShot);
        }
    }
}
