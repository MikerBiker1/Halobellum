// TestUnits.cs
// Builds small UnitStats objects for tests. These numbers are test fixtures only, not game balance:
// real unit stats live in UnitDefinition assets under Assets/_Project/Data/Units.
// Depends on: Halobellum.Core.

using Halobellum.Core;

namespace Halobellum.Tests
{
    public static class TestUnits
    {
        public static WeaponStats Weapon(float damage = 10f, float range = 10f, DamageType type = DamageType.Ballistic,
            float interval = 1f, float projectileSpeed = 0f, float splash = 0f, float minRange = 0f,
            bool antiArmor = false, bool blockable = false)
        {
            return new WeaponStats
            {
                Name = "Test Weapon",
                DamagePerShot = damage,
                FireInterval = interval,
                Range = range,
                MinRange = minRange,
                ProjectileSpeed = projectileSpeed,
                SplashRadius = splash,
                DamageType = type,
                IsAntiArmor = antiArmor,
                CanBeBlocked = blockable,
            };
        }

        public static UnitStats Unit(string name = "Test Unit", float health = 100f, ArmorType armor = ArmorType.Unarmored,
            float speed = 5f, float radius = 0.5f, bool air = false, TargetFilter filter = TargetFilter.Both,
            WeaponStats weapon = null, float shield = 0f, float shieldRegen = 0f, int squadSize = 1, int cost = 100)
        {
            return new UnitStats
            {
                Name = name,
                Role = UnitRole.Chaff,
                Tier = 1,
                SquadSize = squadSize,
                Cost = cost,
                BaseDamage = 1f,
                MaxHealth = health,
                ArmorType = armor,
                MaxShield = shield,
                ShieldRegenPerSecond = shieldRegen,
                MoveSpeed = speed,
                CollisionRadius = radius,
                IsAir = air,
                TargetFilter = filter,
                Weapons = new[] { weapon ?? Weapon() },
            };
        }
    }
}
