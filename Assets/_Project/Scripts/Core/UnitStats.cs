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
        /// <summary>
        /// Lobbed shot (artillery, D37): flies in an arc to the spot where the target is heading when it
        /// is fired, and only damages what is inside the splash when it lands. It can miss.
        /// </summary>
        public bool Lobbed;
        /// <summary>
        /// Beam (Scarab, D37): if above 0, the weapon charges, then fires a beam for this many seconds at
        /// one spot, hitting everything within SplashRadius every FireInterval seconds. 0 means no beam.
        /// </summary>
        public float BeamSeconds;
        /// <summary>Seconds a beam charges before it fires.</summary>
        public float BeamChargeSeconds;
        /// <summary>Seconds a beam rests after firing, before it can charge again.</summary>
        public float BeamRestSeconds;
        /// <summary>How fast a firing beam's spot follows its target, or moves to a new one, in metres per second.</summary>
        public float BeamSweepSpeed;
        /// <summary>
        /// Seed (the Seeder, D38): if above 0, a hit plants a seed in the target for this many seconds.
        /// A seeded unit takes damage over time, is slowed, and rises as a Flood unit if it dies seeded,
        /// whatever its armor. Shields that are up stop it; Titans and Flood cannot be seeded.
        /// </summary>
        public float SeedSeconds;
        /// <summary>Share of its maximum health a seeded unit loses per second.</summary>
        public float SeedDamageSharePerSecond;
        /// <summary>A seeded unit moves this share slower (0.3 = 30% slower).</summary>
        public float SeedSlowShare;
        /// <summary>
        /// Hit slow (the Gremlin's EMP, M10): if above 0, a direct hit slows the target for this many seconds.
        /// Titans shrug it off. A new hit starts the time again; the strongest slow in force applies.
        /// </summary>
        public float HitSlowSeconds;
        /// <summary>A unit slowed by a hit moves this share slower (0.4 = 40% slower).</summary>
        public float HitSlowShare;
        /// <summary>
        /// Wind-up (the Spartan Laser): if above 0, the weapon charges for this many seconds once it is ready and aimed at a target in range,
        /// and the shot goes at the end, at the target it locked on to (or at its current one if that one died or left its range; with
        /// neither in range, the shot is spent). Firing on, it keeps to one shot per FireInterval: the next wind-up starts while it reloads.
        /// Only the first shot at a new target comes later. A shockwave's slower fire stretches it; an EMP or death cancels it. 0 = none.
        /// </summary>
        public float WindUpSeconds;

        public float ShotsPerSecond => FireInterval > 0f ? 1f / FireInterval : 0f;
        public bool IsInstant => ProjectileSpeed <= 0f;
        public bool IsCone => ConeDegrees > 0f;
        public bool IsBeam => BeamSeconds > 0f;
        public bool HasWindUp => WindUpSeconds > 0f;

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

    /// <summary>Bodies of a unit type spawned during a battle (they only exist for that battle).</summary>
    public sealed class SpawnEntry
    {
        public UnitStats Unit;
        public int Bodies;
    }

    /// <summary>What a Seeder's seed turns a unit into when the unit dies seeded: the Flood's own version of it, from a Flood roster (D76).</summary>
    public sealed class SeedForm
    {
        /// <summary>The name of the unit it takes (as in its stats).</summary>
        public string From;
        /// <summary>The Flood unit it rises as.</summary>
        public UnitStats Becomes;
    }

    /// <summary>What a unit does when it dies, however it dies: an explosion and units released on the spot (D38).</summary>
    /// <summary>A side's two towers (D55). The Command Center stands on its owner's left, the Research Center on the right.</summary>
    public enum TowerKind
    {
        CommandCenter = 0,
        ResearchCenter = 1,
    }

    /// <summary>What a side's whole army suffers when one of its towers falls (D55). A second tower adds its seconds, not its severity.</summary>
    public sealed class ShockwaveStats
    {
        public TowerKind Kind;
        public float Seconds;
        public float SpeedMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float FireRateMultiplier = 1f;
        /// <summary>What the army takes while it suffers this: above 1, it takes more.</summary>
        public float DamageTakenMultiplier = 1f;
    }

    public sealed class DeathBurstStats
    {
        public float Damage;
        public float Radius;
        public DamageType DamageType;
        public bool IsAntiArmor;
        public SpawnEntry[] Spawns = new SpawnEntry[0];

        /// <summary>The burst as a weapon, so it goes through the same damage table as any shot. It hits ground units only.</summary>
        public WeaponStats AsWeapon() => new WeaponStats
        {
            Name = "Death burst", DamagePerShot = Damage, DamageType = DamageType, IsAntiArmor = IsAntiArmor,
            HasOwnTargetFilter = true, TargetFilter = TargetFilter.Ground, SplashRadius = Radius,
        };

        public DeathBurstStats Clone() => (DeathBurstStats)MemberwiseClone();
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
        /// <summary>It can deploy in the flank zones beside the enemy's area (D61).</summary>
        public bool CanFlank;
        /// <summary>How many times it drops its squad in a battle, ReinforcementDelaySeconds apart at least.</summary>
        public int ReinforcementDrops = 1;
        /// <summary>
        /// Above 0, it doesn't drop from the air where it is: it flies behind the enemy's lines (unless ReinforcementFirstDropSeconds has it start down sooner), takes this many seconds to come down, lets its squad out
        /// behind it, and takes as long to go back up (the Pelican, D60). On the ground, weapons that can't hit air can hit it. Once its drops are made, it fights.
        /// </summary>
        public float ReinforcementLandSeconds;
        /// <summary>
        /// Above 0, a landing carrier's first squad is out this many seconds after it comes onto the field, wherever it has got to: it starts down in time, without
        /// reaching the enemy's lines (the Pelican: 5). If it comes on later than that, it starts down as it arrives. 0 = the first drop is like the others.
        /// </summary>
        public float ReinforcementFirstDropSeconds;

        /// <summary>
        /// How many tiny forms one body stands for (Infection Forms: 5). They move and act as one body.
        /// Health and damage are for the whole body; it shows and deals less as its forms die, and an area
        /// hit (splash, cone, beam, spell) hurts every form in it.
        /// </summary>
        public int FormsPerBody = 1;
        /// <summary>A Flood unit: it cannot be infected or seeded (D38).</summary>
        public bool IsFlood;
        /// <summary>Unarmored enemies that die within melee range of this unit rise as infected units (Infection Forms).</summary>
        public bool Infects;
        /// <summary>Infection Forms cannot infect this unit even though its body is Unarmored (Spartans).</summary>
        public bool ImmuneToInfection;
        /// <summary>Null for units without one.</summary>
        public DeathBurstStats DeathBurst;
        /// <summary>Set on a tower (D55): what its fall does to its own side. Null on every unit.</summary>
        public ShockwaveStats Shockwave;
        /// <summary>A tower stands, fires nothing and can be shot like any ground target, but it isn't part of the army: it doesn't keep a battle going or count at round end.</summary>
        public bool IsTower => Shockwave != null;
        /// <summary>The unit kills itself when its main weapon fires, to set off its death burst (Carrier Form, Infected Pelican).</summary>
        public bool SelfDestructs;
        /// <summary>The unit heads for the thickest group of enemies instead of the closest one (Infected Pelican).</summary>
        public bool SeeksDensestCluster;
        /// <summary>Released again and again during the battle (Abomination); SquadSize is the bodies each time. Null = none.</summary>
        public UnitStats SpawnedUnit;
        public float SpawnIntervalSeconds;
        /// <summary>When set, every other release is this instead, so the two take turns (the Spawner's Infection Forms and Infested Elites, D79). SquadSize is its bodies.</summary>
        public UnitStats AlternateSpawnedUnit;
        /// <summary>Friendly units within this many metres get the aura below. 0 = no aura.</summary>
        public float AuraRadius;
        public float AuraSpeedMultiplier = 1f;
        /// <summary>Share of its maximum health a unit in the aura regains per second.</summary>
        public float AuraHealPerSecondShare;
        /// <summary>
        /// Leap (Infection Forms, D39): it pounces on a target within this many metres of its edge, and
        /// (a unit in the air, D46) falls out of the sky at it instead, past other air units, and dies on landing.
        /// jumps this far over anything that holds it up. It flies over bodies and cannot attack in the air. 0 = never.
        /// </summary>
        public float LeapRange;
        /// <summary>Metres per second in the air.</summary>
        public float LeapSpeed;
        /// <summary>Seconds from one leap to the next.</summary>
        public float LeapCooldownSeconds;
        /// <summary>The unit this one was before it was infected, or null.</summary>
        public UnitStats InfectedFrom;

        /// <summary>The tech upgrades this unit type can buy (up to 3, Bible Section 9).</summary>
        public TechUpgrade[] TechUpgrades = new TechUpgrade[0];
        /// <summary>Squad level these stats were made for (1 = base stats; see UnitUpgrades.Apply).</summary>
        public int Level = 1;

        public bool HasShield => MaxShield > 0f;
        /// <summary>Turning speed in degrees per second, or infinity for units that turn instantly.</summary>
        public float TurnRateDegreesPerSecond => TurnTime180 > 0f ? 180f / TurnTime180 : float.PositiveInfinity;
        public bool HasDefenseArc => DefenseArc != null && DefenseArc.ArcDegrees > 0f;
        public bool HasReinforcement => Reinforcement != null && Reinforcement.SquadSize > 0;
        /// <summary>The number of individuals a squad holds, as the player counts them (50 Infection Forms in 10 bodies).</summary>
        public int FormsInSquad => SquadSize * System.Math.Max(1, FormsPerBody);

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
                    if (w.Lobbed && (w.ProjectileSpeed <= 0f || w.SplashRadius <= 0f)) problems.Add($"{wl}: a lobbed shot needs a projectile speed and a splash radius.");
                    if (w.IsBeam && w.SplashRadius <= 0f) problems.Add($"{wl}: a beam needs a splash radius.");
                    if (w.IsBeam && w.ProjectileSpeed > 0f) problems.Add($"{wl}: a beam cannot have a projectile speed.");
                    if (w.SeedSeconds < 0f || w.SeedDamageSharePerSecond < 0f || w.SeedSlowShare < 0f || w.SeedSlowShare > 1f) problems.Add($"{wl}: seed values are out of range.");
                    if (w.HitSlowSeconds < 0f || w.HitSlowShare < 0f || w.HitSlowShare > 1f) problems.Add($"{wl}: hit slow values are out of range.");
                    if (w.BeamSeconds < 0f || w.BeamChargeSeconds < 0f || w.BeamRestSeconds < 0f || w.BeamSweepSpeed < 0f) problems.Add($"{wl}: beam values cannot be negative.");
                    if (w.WindUpSeconds < 0f) problems.Add($"{wl}: wind-up cannot be negative.");
                    if (w.HasWindUp && w.IsBeam) problems.Add($"{wl}: a beam charges by its own values and cannot have a wind-up.");
                }
            }
            if (FormsPerBody < 1) problems.Add($"{label}: forms per body must be 1 or more.");
            if (LeapRange < 0f || LeapCooldownSeconds < 0f || (LeapRange > 0f && LeapSpeed <= 0f)) problems.Add($"{label}: a leap needs a range and a speed above 0.");
            if (SelfDestructs && DeathBurst == null) problems.Add($"{label}: a unit that self-destructs needs a death burst.");
            if (SpawnedUnit != null && SpawnIntervalSeconds <= 0f) problems.Add($"{label}: a spawn needs an interval above 0.");
            if (LifestealShare < 0f) problems.Add($"{label}: lifesteal cannot be negative.");
            if (AuraRadius < 0f || AuraHealPerSecondShare < 0f || AuraSpeedMultiplier <= 0f) problems.Add($"{label}: aura values are out of range.");
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
                if (ReinforcementDrops < 1) problems.Add($"{label}: it must drop its reinforcement at least once.");
                if (ReinforcementLandSeconds < 0f) problems.Add($"{label}: landing time cannot be negative.");
                if (ReinforcementFirstDropSeconds < 0f) problems.Add($"{label}: first drop time cannot be negative.");
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
        /// <summary>
        /// True if the unit uses one weapon at a time, chosen by its target: the ODST's two, by what they are for, and a unit whose weapons split
        /// air from ground (the Swarm Form's spat bone at air, its bite on the ground, D76). Each weapon only notices enemies within three times its
        /// range, so the long one's targets win unless something is right beside it.
        /// </summary>
        public bool SwitchesWeapons
        {
            get
            {
                if (Weapons == null) return false;
                bool airOnly = false, groundOnly = false;
                foreach (WeaponStats weapon in Weapons)
                {
                    if (weapon == null) continue;
                    if (weapon.TargetClass != WeaponTargetClass.Any) return true;
                    if (!weapon.HasOwnTargetFilter) continue;
                    if (weapon.TargetFilter == TargetFilter.Air) airOnly = true;
                    else if (weapon.TargetFilter == TargetFilter.Ground) groundOnly = true;
                }
                return airOnly && groundOnly;
            }
        }

        /// <summary>What its seeds turn a unit into when it dies seeded (the Seeder, D76). A seeded unit with no form here does not rise from the seed.</summary>
        public SeedForm[] SeedForms;

        /// <summary>Share of the damage dealt that comes back as health (from its data, such as the Hunter Captain's siphon beam, or from items).</summary>
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
            copy.DeathBurst = DeathBurst?.Clone();
            copy.TechUpgrades = TechUpgrades == null ? new TechUpgrade[0] : (TechUpgrade[])TechUpgrades.Clone(); // Techs are shared, read-only data.
            return copy;
        }
    }
}
