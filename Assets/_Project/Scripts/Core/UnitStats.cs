// UnitStats.cs
// Plain C# copies of a unit's data (stats, weapons, optional defense arc) that the simulation
// reads. They are filled from a UnitDefinition ScriptableObject (Scripts/Units) so the simulation
// never touches Unity objects. No numbers live here: every value comes from data (Bible Section 4).
// Depends on: CombatEnums.

using System.Collections.Generic;

namespace Halobellum.Core
{
    /// <summary>Which enemies a weapon is meant for, on units that switch weapons by target (ODSTs, D33).</summary>
    public enum WeaponTargetClass
    {
        /// <summary>Any enemy (normal weapons).</summary>
        Any = 0,
        /// <summary>Chaff and unarmored enemies (the ODST's SMG).</summary>
        ChaffOrUnarmored = 1,
        /// <summary>Everything tougher (the ODST's rocket launcher).</summary>
        Tougher = 2,
    }

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
        /// <summary>
        /// If true, this weapon uses its own TargetFilter instead of the unit's (for example a Hornet's
        /// bombs only hit ground units while its rockets hit both).
        /// </summary>
        public bool HasOwnTargetFilter;
        public TargetFilter TargetFilter;
        /// <summary>Damage multiplier against air units ("bonus damage against air"). 1 means no bonus.</summary>
        public float AirDamageMultiplier = 1f;
        /// <summary>
        /// Cone splash (flamethrowers): if above 0, each shot also hits every legal enemy inside a cone
        /// this many degrees wide, from the shooter out to the weapon's range, centred on the target.
        /// SplashRadius is not used for cone weapons. 0 means no cone.
        /// </summary>
        public float ConeDegrees;
        /// <summary>
        /// Limits the weapon to one class of enemy. A unit with such weapons uses one at a time: the one
        /// that fits the closest enemy it can attack.
        /// </summary>
        public WeaponTargetClass TargetClass;

        public float ShotsPerSecond => FireInterval > 0f ? 1f / FireInterval : 0f;
        public bool IsInstant => ProjectileSpeed <= 0f;
        public bool IsCone => ConeDegrees > 0f;

        /// <summary>True if this weapon, on this unit, may attack a unit that is (or is not) in the air.</summary>
        /// <summary>True if this weapon's target class fits this enemy.</summary>
        public bool FitsClass(UnitStats enemy)
        {
            if (TargetClass == WeaponTargetClass.Any) return true;
            bool light = enemy.Role == UnitRole.Chaff || enemy.ArmorType == ArmorType.Unarmored;
            return TargetClass == WeaponTargetClass.ChaffOrUnarmored ? light : !light;
        }

        public bool CanTarget(UnitStats owner, bool targetIsAir)
        {
            return HasOwnTargetFilter ? TargetFilter.Allows(targetIsAir) : owner == null || owner.CanTarget(targetIsAir);
        }

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
        /// <summary>Seconds the unit needs to turn 180 degrees; bigger units turn slower. 0 means it turns instantly.</summary>
        public float TurnTime180;
        public bool IsAir;
        public TargetFilter TargetFilter;

        public WeaponStats[] Weapons = new WeaponStats[0];
        /// <summary>Null for the many units that have no directional defense.</summary>
        public DefenseArcStats DefenseArc;

        /// <summary>
        /// Optional mid-battle drop (Pelican): a squad of this unit type (its SquadSize is the number of
        /// bodies dropped) appears under this unit once it has been alive this many seconds. Null = none.
        /// Dropped units only exist for that battle.
        /// </summary>
        public UnitStats Reinforcement;
        public float ReinforcementDelaySeconds;
        /// <summary>A drop that only works once a tech upgrade unlocks it (the Pelican's troop bay); null = none.</summary>
        public UnitStats LockedReinforcement;

        /// <summary>The tech upgrades this unit type can buy (up to 3, Bible Section 9).</summary>
        public TechUpgrade[] TechUpgrades = new TechUpgrade[0];
        /// <summary>Squad level these stats were made for (1 = base stats; see UnitUpgrades.Apply).</summary>
        public int Level = 1;

        public bool HasShield => MaxShield > 0f;
        /// <summary>Turning speed in degrees per second, or infinity for units that turn instantly.</summary>
        public float TurnRateDegreesPerSecond => TurnTime180 > 0f ? 180f / TurnTime180 : float.PositiveInfinity;
        public bool HasDefenseArc => DefenseArc != null && DefenseArc.ArcDegrees > 0f;
        public bool HasReinforcement => Reinforcement != null && Reinforcement.SquadSize > 0;

        /// <summary>True if this unit's target filter allows attacking a unit that is (or is not) in the air.</summary>
        public bool CanTarget(bool targetIsAir) => TargetFilter.Allows(targetIsAir);

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
            if (TurnTime180 < 0f) problems.Add($"{label}: turn time cannot be negative.");
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
                    if (w.AirDamageMultiplier < 0f) problems.Add($"{wl}: damage against air cannot be negative.");
                    if (w.ConeDegrees < 0f || w.ConeDegrees > 360f) problems.Add($"{wl}: cone must be 0 to 360 degrees.");
                }
            }
            if (DefenseArc != null)
            {
                if (DefenseArc.ArcDegrees < 0f || DefenseArc.ArcDegrees > 360f) problems.Add($"{label}: defense arc must be 0 to 360 degrees.");
                if (DefenseArc.BlockChance < 0f || DefenseArc.BlockChance > 1f) problems.Add($"{label}: block chance must be 0 to 1.");
                if (DefenseArc.FrontDamageMultiplier < 0f || DefenseArc.RearDamageMultiplier < 0f) problems.Add($"{label}: defense multipliers cannot be negative.");
            }
            UnitStats drop = Reinforcement ?? LockedReinforcement;
            if (drop != null)
            {
                if (ReinforcementDelaySeconds < 0f) problems.Add($"{label}: reinforcement delay cannot be negative.");
                foreach (string problem in drop.Validate()) problems.Add($"{label} reinforcement: {problem}");
            }
            if (TechUpgrades != null)
            {
                if (TechUpgrades.Length > UnitUpgrades.MaxTechsPerUnit) problems.Add($"{label}: at most {UnitUpgrades.MaxTechsPerUnit} tech upgrades.");
                foreach (TechUpgrade tech in TechUpgrades)
                {
                    if (tech == null) problems.Add($"{label}: a tech upgrade slot is empty.");
                    else problems.AddRange(tech.Validate(label));
                }
            }
            return problems;
        }

        /// <summary>A deep copy, so changes to one copy never affect another.</summary>
        /// <summary>True if the unit uses one weapon at a time, chosen by its target (ODSTs).</summary>
        public bool SwitchesWeapons
        {
            get
            {
                if (Weapons == null) return false;
                foreach (WeaponStats weapon in Weapons) if (weapon != null && weapon.TargetClass != WeaponTargetClass.Any) return true;
                return false;
            }
        }

        /// <summary>Share of the damage dealt that comes back as health (from items).</summary>
        public float LifestealShare;
        /// <summary>Share of its maximum health a body recovers per second (from items).</summary>
        public float HealPerSecondShare;
        /// <summary>
        /// The same unit without its tech upgrades (level, item and army buffs kept), or null if it has no
        /// techs. An EMP switches a unit to these stats for a while.
        /// </summary>
        public UnitStats WithoutTechs;

        public UnitStats Clone()
        {
            var copy = (UnitStats)MemberwiseClone();
            copy.Weapons = new WeaponStats[Weapons?.Length ?? 0];
            for (int i = 0; i < copy.Weapons.Length; i++) copy.Weapons[i] = Weapons[i]?.Clone();
            copy.DefenseArc = DefenseArc?.Clone();
            copy.Reinforcement = Reinforcement?.Clone();
            copy.LockedReinforcement = LockedReinforcement?.Clone();
            copy.TechUpgrades = TechUpgrades == null ? new TechUpgrade[0] : (TechUpgrade[])TechUpgrades.Clone(); // Techs are shared, read-only data.
            return copy;
        }
    }
}
