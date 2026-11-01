// SpellStats.cs
// Plain C# copy of one spell card (Bible Section 10, Milestone 7), filled from a SpellDefinition data
// asset (Scripts/Units). A spell is bought once from a card offer, kept for the whole match and
// recharges for CooldownRounds rounds after each use. It is placed during deployment and takes effect
// DelaySeconds after the battle starts:
//   - Damage: hits the caster's enemies inside Radius, Ticks times, TickInterval seconds apart
//     (1 tick = a strike, more = a zone such as a plasma storm), through the normal damage table.
//   - Buff: the caster's units inside Radius get a bonus shield (a share of their health) and/or a
//     damage multiplier for the rest of the battle.
//   - DropSquad: DropSquads squads of DropUnit land at the spot for that battle only (ODST pods,
//     Banshee Ambush).
//   - Disrupt: an EMP. Removes ShieldDamage of every unit's shield, switches off its tech upgrades
//     for TechDisableSeconds and slows armored units by SlowShare for SlowSeconds.
//   - Smoke: units inside have their weapon range multiplied by RangeMultiplier, for the rest of the
//     battle (or only DurationSeconds, if above 0).
// Offensive spells can be placed anywhere on the field (red outline) and can hurt the caster's own
// units too (decision D33); defensive spells and buffs go only in the caster's own deployment zone
// (blue outline) and only help the caster. An area is a circle (Radius), or a line when AreaWidth is
// above 0: AreaWidth long and AreaDepth wide, at any angle (the player clicks one end and aims the other,
// decision D35). No numbers live here: every value is data.
// Depends on: UnitStats, WeaponStats, CombatEnums.

using System.Collections.Generic;

namespace Halobellum.Core
{
    public enum SpellEffect
    {
        Damage = 0,
        Buff = 1,
        DropSquad = 2,
        Disrupt = 3,
        Smoke = 4,
    }

    public sealed class SpellStats
    {
        public string Name = "";
        public string Description = "";
        /// <summary>Offensive: placed anywhere (red outline). Otherwise only in the caster's own zone (blue outline).</summary>
        public bool Offensive;
        public SpellEffect Effect;
        /// <summary>Credits paid once, when the card is taken.</summary>
        public int Cost;
        /// <summary>Rounds it recharges after being used (it can be used again CooldownRounds + 1 rounds later).</summary>
        public int CooldownRounds;
        /// <summary>How many copies a player may own; at the maximum it is no longer offered.</summary>
        public int MaxOwnedCopies = 1;
        /// <summary>Radius of the area in metres.</summary>
        public float Radius;
        /// <summary>Seconds after the battle starts before the first effect.</summary>
        public float DelaySeconds;
        /// <summary>The first round it can be offered (extremely impactful spells only appear late).</summary>
        public int MinRound;
        /// <summary>A line-shaped area: its length in metres (0 = a circle of Radius).</summary>
        public float AreaWidth;
        /// <summary>A line-shaped area: its width in metres.</summary>
        public float AreaDepth;

        // Damage spells.
        public float DamagePerTick;
        public DamageType DamageType;
        public bool IsAntiArmor;
        /// <summary>Number of hits (1 = a single strike).</summary>
        public int Ticks = 1;
        public float TickInterval;
        /// <summary>Which enemies it hits: ground, air or both.</summary>
        public TargetFilter Targets = TargetFilter.Ground;
        /// <summary>Unarmored enemies it kills rise as infected units (Flood spells, D38).</summary>
        public bool InfectsKills;

        // Buffs.
        /// <summary>Bonus shield given to each unit, as a share of its maximum health (0.5 = half its health).</summary>
        public float ShieldBonusShare;
        /// <summary>Damage multiplier for the units' weapons for the rest of the battle (1 = none).</summary>
        public float DamageMultiplier = 1f;

        // Drop squads.
        public UnitStats DropUnit;
        public int DropSquads = 1;

        // Disrupt (EMP).
        /// <summary>Shield removed from each unit (health is not touched).</summary>
        public float ShieldDamage;
        public float TechDisableSeconds;
        /// <summary>Armored units move this share slower (0.4 = 40% slower).</summary>
        public float SlowShare;
        public float SlowSeconds;

        // Smoke.
        /// <summary>How long it hangs; 0 = until the battle ends.</summary>
        public float DurationSeconds;
        /// <summary>Weapon range of units inside is multiplied by this (0.5 = half).</summary>
        public float RangeMultiplier = 1f;

        public bool IsRectangle => AreaWidth > 0f;

        /// <summary>True if a body at this offset from the area's centre touches the area. angleDegrees: the direction of a line.</summary>
        public bool Covers(Vec2 offset, float bodyRadius, float angleDegrees)
        {
            if (!IsRectangle) return offset.Length - bodyRadius <= Radius;
            Vec2 local = offset.Rotated(-angleDegrees); // Along the line (X) and across it (Y).
            return System.Math.Abs(local.X) <= AreaWidth * 0.5f + bodyRadius && System.Math.Abs(local.Y) <= AreaDepth * 0.5f + bodyRadius;
        }

        /// <summary>
        /// A line from one end toward a point: where its centre is and which way it points (its length is
        /// always AreaWidth, the point only gives the direction).
        /// </summary>
        public Vec2 LineFrom(Vec2 start, Vec2 towards, out float angleDegrees)
        {
            Vec2 way = towards - start;
            Vec2 direction = way.LengthSquared > 1e-6f ? way.Normalized : Vec2.Right;
            angleDegrees = (float)(System.Math.Atan2(direction.Y, direction.X) * 180.0 / System.Math.PI);
            return start + direction * (AreaWidth * 0.5f);
        }

        private WeaponStats _weapon;

        /// <summary>The spell's damage as a weapon, so it goes through the same damage table as any shot.</summary>
        public WeaponStats AsWeapon()
        {
            if (_weapon == null)
            {
                _weapon = new WeaponStats
                {
                    Name = Name, DamagePerShot = DamagePerTick, DamageType = DamageType, IsAntiArmor = IsAntiArmor,
                    HasOwnTargetFilter = true, TargetFilter = Targets,
                };
            }
            return _weapon;
        }

        /// <summary>Seconds from the battle start to its last effect.</summary>
        public float LastEffectSeconds => DelaySeconds + (Effect == SpellEffect.Damage ? (System.Math.Max(1, Ticks) - 1) * TickInterval : 0f);

        /// <summary>A short line describing what it does, for card and tooltip text.</summary>
        public string EffectsText()
        {
            switch (Effect)
            {
                case SpellEffect.Damage:
                    string type = IsAntiArmor ? "anti-armor" : DamageType.ToString();
                    string hits = Ticks > 1 ? $"{DamagePerTick:0} {type} damage x{Ticks} over {(Ticks - 1) * TickInterval:0.#} s" : $"{DamagePerTick:0} {type} damage";
                    return $"{hits} in {Radius:0} m ({TargetsText()})";
                case SpellEffect.Buff:
                    var parts = new List<string>();
                    if (ShieldBonusShare > 0f) parts.Add($"+{ShieldBonusShare * 100f:0}% health as shield");
                    if (DamageMultiplier > 1f) parts.Add($"+{(DamageMultiplier - 1f) * 100f:0}% damage");
                    return $"{string.Join(", ", parts)} for your units in {Radius:0} m";
                case SpellEffect.Disrupt:
                    return $"{ShieldDamage:0} damage to shields, disables techs for {TechDisableSeconds:0} s, armored units {SlowShare * 100f:0}% slower for {SlowSeconds:0} s, in {Radius:0} m";
                case SpellEffect.Smoke:
                    string lasts = DurationSeconds > 0f ? $"for {DurationSeconds:0} s" : "for the whole round";
                    return $"Smoke in a {AreaDepth:0} x {AreaWidth:0} m line {lasts}: units inside have {(1f - RangeMultiplier) * 100f:0}% less range";
                default:
                    return $"{DropSquads} x {DropUnit?.Name ?? "?"} squad{(DropSquads == 1 ? "" : "s")} land for this battle";
            }
        }

        private string TargetsText() => Targets == TargetFilter.Both ? "ground and air" : Targets == TargetFilter.Air ? "air only" : "ground only";

        /// <summary>Problems with the data, empty if valid.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            string label = string.IsNullOrEmpty(Name) ? "(unnamed spell)" : Name;
            if (string.IsNullOrEmpty(Name)) problems.Add("Spell has no name.");
            if (Cost < 0) problems.Add($"{label}: cost cannot be negative.");
            if (CooldownRounds < 0) problems.Add($"{label}: cooldown cannot be negative.");
            if (MaxOwnedCopies < 1) problems.Add($"{label}: max owned copies must be at least 1.");
            if (DelaySeconds < 0f) problems.Add($"{label}: delay cannot be negative.");
            switch (Effect)
            {
                case SpellEffect.Damage:
                    if (Radius <= 0f) problems.Add($"{label}: radius must be above 0.");
                    if (DamagePerTick <= 0f) problems.Add($"{label}: damage must be above 0.");
                    if (Ticks < 1) problems.Add($"{label}: at least 1 tick.");
                    if (Ticks > 1 && TickInterval <= 0f) problems.Add($"{label}: a zone needs a tick interval above 0.");
                    break;
                case SpellEffect.Buff:
                    if (Radius <= 0f) problems.Add($"{label}: radius must be above 0.");
                    if (Offensive) problems.Add($"{label}: buffs are defensive (placed on your own side).");
                    if (ShieldBonusShare <= 0f && DamageMultiplier <= 1f) problems.Add($"{label}: a buff needs a shield bonus or a damage multiplier above 1.");
                    break;
                case SpellEffect.DropSquad:
                    if (DropUnit == null) problems.Add($"{label}: no unit to drop.");
                    if (DropSquads < 1) problems.Add($"{label}: drops at least 1 squad.");
                    break;
                case SpellEffect.Disrupt:
                    if (Radius <= 0f) problems.Add($"{label}: radius must be above 0.");
                    if (ShieldDamage <= 0f && TechDisableSeconds <= 0f && SlowShare <= 0f) problems.Add($"{label}: a disruption needs an effect.");
                    break;
                case SpellEffect.Smoke:
                    if (AreaWidth <= 0f && Radius <= 0f) problems.Add($"{label}: smoke needs an area.");
                    if (RangeMultiplier <= 0f || RangeMultiplier >= 1f) problems.Add($"{label}: smoke must reduce range.");
                    break;
            }
            return problems;
        }
    }
}
