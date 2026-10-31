// DamageCalculator.cs
// The damage and armor math (Bible Section 6). Works out how much damage one hit deals and how it
// is split between a unit's energy shield and its health.
//   damage = weapon damage per shot x armor multiplier x directional multiplier
// The armor multiplier comes from the damage table: the anti-armor row if the weapon has the
// anti-armor tag, otherwise the row for the weapon's damage type. A shielded unit uses its base
// armor type for the multiplier; the shield is simply absorbed first and any excess hits health.
// Depends on: Halobellum.Core (DamageTable, WeaponStats, ArmorType).

using System;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public static class DamageCalculator
    {
        /// <summary>The damage table multiplier for this weapon against this armor type.</summary>
        public static float ArmorMultiplier(DamageTable table, WeaponStats weapon, ArmorType armor)
        {
            return weapon.IsAntiArmor ? table.GetAntiArmor(armor) : table.Get(weapon.DamageType, armor);
        }

        /// <summary>Final damage of one hit, before it is split between shield and health.</summary>
        /// <param name="directionalMultiplier">From DirectionalDefense; 1 for units without a defense arc.</param>
        public static float ComputeDamage(DamageTable table, WeaponStats weapon, ArmorType armor, float directionalMultiplier = 1f)
        {
            float damage = weapon.DamagePerShot * ArmorMultiplier(table, weapon, armor) * directionalMultiplier;
            return Math.Max(0f, damage);
        }

        /// <summary>
        /// Applies damage to a shield and health pool: the shield absorbs first, the rest hits health.
        /// Health never goes below 0. Returns how much damage was actually removed in total.
        /// </summary>
        public static float ApplyDamage(float damage, ref float shield, ref float health)
        {
            if (damage <= 0f) return 0f;

            float toShield = Math.Min(shield, damage);
            shield -= toShield;
            float remaining = damage - toShield;

            float toHealth = Math.Min(health, remaining);
            health -= toHealth;
            return toShield + toHealth;
        }
    }
}
