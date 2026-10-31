// DamageCalculator.cs
// The damage and armor math (Bible Section 6, and decision D2 for shields).
//   base damage   = weapon damage per shot x directional multiplier
//   to the shield = base damage x shield multiplier (the shield row of the damage table)
//   to health     = base damage x armor multiplier (the row for the unit's base armor type)
// Energy shields are a separate layer with their own multipliers, independent of the armor type.
// Damage goes to the shield first. If a hit breaks the shield, the part of the base damage the
// shield did not soak up carries over to health using the armor multiplier.
// A weapon with the anti-armor tag uses the anti-armor row/value instead of its damage type's
// (decision D1), both against armor and against shields.
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

        /// <summary>The multiplier for this weapon against an energy shield.</summary>
        public static float ShieldMultiplier(DamageTable table, WeaponStats weapon)
        {
            return weapon.IsAntiArmor ? table.GetAntiArmorShield() : table.GetShield(weapon.DamageType);
        }

        /// <summary>Damage one hit deals to the health of a unit with no shield left.</summary>
        /// <param name="directionalMultiplier">From DirectionalDefense; 1 for units without a defense arc.</param>
        public static float ComputeDamage(DamageTable table, WeaponStats weapon, ArmorType armor, float directionalMultiplier = 1f)
        {
            float damage = weapon.DamagePerShot * ArmorMultiplier(table, weapon, armor) * directionalMultiplier;
            return Math.Max(0f, damage);
        }

        /// <summary>
        /// Applies one hit to a unit's shield and health, following the rules at the top of this file.
        /// Neither value goes below 0. Returns the total shield and health actually removed.
        /// </summary>
        public static float ApplyHit(DamageTable table, WeaponStats weapon, ArmorType armor, float directionalMultiplier,
            ref float shield, ref float health)
        {
            float baseDamage = weapon.DamagePerShot * directionalMultiplier;
            if (baseDamage <= 0f) return 0f;

            float removed = 0f;
            if (shield > 0f)
            {
                float shieldMultiplier = ShieldMultiplier(table, weapon);
                if (shieldMultiplier <= 0f) return 0f; // The shield fully resists this damage type.

                float shieldDamage = baseDamage * shieldMultiplier;
                if (shieldDamage < shield)
                {
                    shield -= shieldDamage;
                    return shieldDamage;
                }

                // The hit breaks the shield: work out how much base damage that took, and carry the rest over.
                baseDamage -= shield / shieldMultiplier;
                removed += shield;
                shield = 0f;
            }

            float healthDamage = Math.Min(health, Math.Max(0f, baseDamage * ArmorMultiplier(table, weapon, armor)));
            health -= healthDamage;
            return removed + healthDamage;
        }
    }
}
