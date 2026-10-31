// UnitStats.cs
// Plain C# copies of a unit's data (stats, weapons, optional defense arc) that the simulation
// reads. They are filled from a UnitDefinition ScriptableObject (Scripts/Units) so the simulation
// never touches Unity objects. No numbers live here: every value comes from data (Bible Section 4).
// Depends on: CombatEnums.

using System.Collections.Generic;

namespace Halobellum.Core
{
    /// <summary>One weapon on a unit (Bible Section 6, "Weapon stats").</summary>
    public sealed class WeaponStats
    {
        public string Name = "";
        public float DamagePerShot;
        /// <summary>Seconds between shots. Rate of fire (shots per second) is 1 / FireInterval.</summary>
        public float FireInterval;
        public float Range;
        /// <summary>Targets closer than this cannot be attacked (artillery). 0 means no minimum.</summary>
        public float MinRange;
        /// <summary>Projectile travel speed in metres per second. 0 or less means the hit is instant.</summary>
        public float ProjectileSpeed;
        /// <summary>Radius of splash damage around the impact point. 0 means single target only.</summary>
        public float SplashRadius;
        public DamageType DamageType;
        /// <summary>Anti-armor tag: uses the anti-armor row of the damage table instead of the damage type row.</summary>
        public bool IsAntiArmor;
        /// <summary>Whether this weapon's projectiles can be blocked by a defense arc (for example a Jackal shield).</summary>
        public bool CanBeBlocked;

        public float ShotsPerSecond => FireInterval > 0f ? 1f / FireInterval : 0f;
        public bool IsInstant => ProjectileSpeed <= 0f;

        public WeaponStats Clone() => (WeaponStats)MemberwiseClone();
    }

    /// <summary>
    /// Optional directional defense (Bible Section 6). Attacks arriving from inside the arc, centred
    /// on the unit's facing, use the front multiplier and can be blocked; others use the rear multiplier.
    /// </summary>
    public sealed class DefenseArcStats
    {
        /// <summary>Total width of the front arc in degrees (for example 120 for Shield Jackals).</summary>
        public float ArcDegrees;
        public float FrontDamageMultiplier;
        public float RearDamageMultiplier;
        /// <summary>Chance (0 to 1) to block a blockable projectile of a listed damage type arriving inside the arc.</summary>
        public float BlockChance;
        public DamageType[] BlockedDamageTypes = new DamageType[0];

        public bool Blocks(DamageType type)
        {
            if (BlockedDamageTypes == null) return false;
            for (int i = 0; i < BlockedDamageTypes.Length; i++)
            {
                if (BlockedDamageTypes[i] == type) return true;
            }
            return false;
        }

        public DefenseArcStats Clone()
        {
            var copy = (DefenseArcStats)MemberwiseClone();
            copy.BlockedDamageTypes = BlockedDamageTypes == null ? new DamageType[0] : (DamageType[])BlockedDamageTypes.Clone();
            return copy;
        }
    }

    /// <summary>All stats of one unit type. Health and damage are per body; cost is per squad.</summary>
    public sealed class UnitStats
    {
        public string Name = "";
        public UnitRole Role;
        public int Tier;
        public int UnlockCost;
        public int SquadSize;
        public int Cost;
        /// <summary>Damage each surviving body deals to the enemy base at round end.</summary>
        public float BaseDamage;

        public float MaxHealth;
        public ArmorType ArmorType;
        /// <summary>Energy shield points on top of health. 0 means no shield.</summary>
        public float MaxShield;
        /// <summary>Shield points regained per second once the unit has gone long enough without damage.</summary>
        public float ShieldRegenPerSecond;

        /// <summary>Metres per second.</summary>
        public float MoveSpeed;
        /// <summary>Radius of the unit's round collision footprint in metres.</summary>
        public float CollisionRadius;
        public bool IsAir;
        public TargetFilter TargetFilter;

        public WeaponStats[] Weapons = new WeaponStats[0];
        /// <summary>Null for the many units that have no directional defense.</summary>
        public DefenseArcStats DefenseArc;

        public bool HasShield => MaxShield > 0f;
        public bool HasDefenseArc => DefenseArc != null && DefenseArc.ArcDegrees > 0f;

        /// <summary>True if this unit's target filter allows attacking a unit that is (or is not) in the air.</summary>
        public bool CanTarget(bool targetIsAir)
        {
            switch (TargetFilter)
            {
                case TargetFilter.Ground: return !targetIsAir;
                case TargetFilter.Air: return targetIsAir;
                default: return true;
            }
        }

        /// <summary>Lists problems that would break the simulation. An empty list means the data is usable.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            string label = string.IsNullOrEmpty(Name) ? "(unnamed unit)" : Name;
            if (string.IsNullOrEmpty(Name)) problems.Add("Unit has no name.");
            if (SquadSize < 1) problems.Add($"{label}: squad size must be at least 1.");
            if (MaxHealth <= 0f) problems.Add($"{label}: health must be above 0.");
            if (MaxShield < 0f) problems.Add($"{label}: shield cannot be negative.");
            if (MoveSpeed < 0f) problems.Add($"{label}: move speed cannot be negative.");
            if (CollisionRadius <= 0f) problems.Add($"{label}: collision size must be above 0.");
            if (Weapons == null || Weapons.Length == 0) problems.Add($"{label}: needs at least one weapon.");
            else
            {
                for (int i = 0; i < Weapons.Length; i++)
                {
                    WeaponStats w = Weapons[i];
                    string wl = $"{label} weapon {i} ({w?.Name})";
                    if (w == null) { problems.Add($"{label}: weapon {i} is missing."); continue; }
                    if (w.FireInterval <= 0f) problems.Add($"{wl}: fire interval must be above 0.");
                    if (w.Range <= 0f) problems.Add($"{wl}: range must be above 0.");
                    if (w.MinRange < 0f || w.MinRange >= w.Range) problems.Add($"{wl}: minimum range must be 0 or more and below range.");
                    if (w.DamagePerShot < 0f) problems.Add($"{wl}: damage cannot be negative.");
                    if (w.SplashRadius < 0f) problems.Add($"{wl}: splash radius cannot be negative.");
                }
            }
            if (DefenseArc != null)
            {
                if (DefenseArc.ArcDegrees < 0f || DefenseArc.ArcDegrees > 360f) problems.Add($"{label}: defense arc must be 0 to 360 degrees.");
                if (DefenseArc.BlockChance < 0f || DefenseArc.BlockChance > 1f) problems.Add($"{label}: block chance must be 0 to 1.");
                if (DefenseArc.FrontDamageMultiplier < 0f || DefenseArc.RearDamageMultiplier < 0f) problems.Add($"{label}: defense multipliers cannot be negative.");
            }
            return problems;
        }

        /// <summary>A deep copy, so changes to one copy never affect another.</summary>
        public UnitStats Clone()
        {
            var copy = (UnitStats)MemberwiseClone();
            copy.Weapons = new WeaponStats[Weapons?.Length ?? 0];
            for (int i = 0; i < copy.Weapons.Length; i++) copy.Weapons[i] = Weapons[i]?.Clone();
            copy.DefenseArc = DefenseArc?.Clone();
            return copy;
        }
    }
}
