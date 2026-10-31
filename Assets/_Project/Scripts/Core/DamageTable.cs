// DamageTable.cs
// Holds the damage multipliers for every damage type against every armor type, plus the
// anti-armor row (Bible Section 6, "Damage multipliers"). The values come from the CombatRules
// data asset; this class only stores and looks them up.
// Depends on: CombatEnums.

using System;

namespace Halobellum.Core
{
    public sealed class DamageTable
    {
        private static readonly int DamageTypeCount = Enum.GetValues(typeof(DamageType)).Length;
        private static readonly int ArmorTypeCount = Enum.GetValues(typeof(ArmorType)).Length;

        private readonly float[,] _byType = new float[DamageTypeCount, ArmorTypeCount];
        private readonly float[] _antiArmor = new float[ArmorTypeCount];

        /// <summary>Multiplier for a normal (not anti-armor) weapon of this damage type against this armor.</summary>
        public float Get(DamageType damageType, ArmorType armorType) => _byType[(int)damageType, (int)armorType];

        public void Set(DamageType damageType, ArmorType armorType, float multiplier) => _byType[(int)damageType, (int)armorType] = multiplier;

        /// <summary>Multiplier for a weapon with the anti-armor tag against this armor.</summary>
        public float GetAntiArmor(ArmorType armorType) => _antiArmor[(int)armorType];

        public void SetAntiArmor(ArmorType armorType, float multiplier) => _antiArmor[(int)armorType] = multiplier;

        /// <summary>Fills one damage type row: multipliers against Unarmored, Light and Heavy.</summary>
        public void SetRow(DamageType damageType, float unarmored, float light, float heavy)
        {
            Set(damageType, ArmorType.Unarmored, unarmored);
            Set(damageType, ArmorType.Light, light);
            Set(damageType, ArmorType.Heavy, heavy);
        }

        /// <summary>Fills the anti-armor row: multipliers against Unarmored, Light and Heavy.</summary>
        public void SetAntiArmorRow(float unarmored, float light, float heavy)
        {
            SetAntiArmor(ArmorType.Unarmored, unarmored);
            SetAntiArmor(ArmorType.Light, light);
            SetAntiArmor(ArmorType.Heavy, heavy);
        }
    }
}
