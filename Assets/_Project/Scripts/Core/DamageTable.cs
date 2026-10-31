// DamageTable.cs
// Holds the damage multipliers for every damage type against every armor type, plus the
// anti-armor row (Bible Section 6, "Damage multipliers"), and the separate multipliers against
// energy shields (decision D2: shields are their own layer, independent of the armor type).
// The values come from the CombatRules data asset; this class only stores and looks them up.
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
        private readonly float[] _shield = new float[DamageTypeCount];
        private float _antiArmorShield;

        /// <summary>Multiplier for a normal (not anti-armor) weapon of this damage type against this armor.</summary>
        public float Get(DamageType damageType, ArmorType armorType) => _byType[(int)damageType, (int)armorType];

        public void Set(DamageType damageType, ArmorType armorType, float multiplier) => _byType[(int)damageType, (int)armorType] = multiplier;

        /// <summary>Multiplier for a weapon with the anti-armor tag against this armor.</summary>
        public float GetAntiArmor(ArmorType armorType) => _antiArmor[(int)armorType];

        public void SetAntiArmor(ArmorType armorType, float multiplier) => _antiArmor[(int)armorType] = multiplier;

        /// <summary>Multiplier for a normal (not anti-armor) weapon of this damage type against an energy shield.</summary>
        public float GetShield(DamageType damageType) => _shield[(int)damageType];

        public void SetShield(DamageType damageType, float multiplier) => _shield[(int)damageType] = multiplier;

        /// <summary>Multiplier for a weapon with the anti-armor tag against an energy shield.</summary>
        public float GetAntiArmorShield() => _antiArmorShield;

        public void SetAntiArmorShield(float multiplier) => _antiArmorShield = multiplier;

        /// <summary>Fills the shield row: multipliers for each damage type, then the anti-armor tag.</summary>
        public void SetShieldRow(float ballistic, float explosive, float energy, float fire, float melee, float antiArmor)
        {
            SetShield(DamageType.Ballistic, ballistic);
            SetShield(DamageType.Explosive, explosive);
            SetShield(DamageType.Energy, energy);
            SetShield(DamageType.Fire, fire);
            SetShield(DamageType.Melee, melee);
            SetAntiArmorShield(antiArmor);
        }

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
