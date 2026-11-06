// TechUpgrade.cs
// Squad levels and tech upgrades (Bible Sections 5 and 9, Milestone 6), as plain C#:
//   - TechUpgrade: one tech for one unit type, bought with credits, applying to every squad of that
//     type for the rest of the match. Each effect is a multiplier (1 = no change) or a bonus (0 = no
//     change), so one class covers armor plating, range, rate of fire, special weapons and anti-air.
//   - UnitUpgrades.Apply: a copy of a unit's base stats with its squad level and owned techs applied.
//     Level L multiplies base health and weapon damage by L (the Section 5 table: 1x, 2x ... 5x).
// The copy keeps the unit's name, so visuals and results still group it with its unit type.
// Depends on: UnitStats, WeaponStats.

using System;
using System.Collections.Generic;

namespace Halobellum.Core
{
    public sealed class TechUpgrade
    {
        public string Name = "";
        public string Description = "";
        public int Cost;

        public float HealthMultiplier = 1f;
        public float DamageMultiplier = 1f;
        /// <summary>Rate of fire multiplier: 1.25 = 25% more shots per second (fire interval divided by it).</summary>
        public float FireRateMultiplier = 1f;
        /// <summary>Metres added to every weapon's range.</summary>
        public float RangeBonus;
        public float MoveSpeedMultiplier = 1f;
        /// <summary>Energy shield points added (a unit without a shield gets one, regenerating at ShieldRegenBonus).</summary>
        public float ShieldBonus;
        public float ShieldRegenBonus;
        /// <summary>Metres added to splash radius (weapons that already splash).</summary>
        public float SplashRadiusBonus;
        /// <summary>Degrees added to cone weapons (flamethrowers).</summary>
        public float ConeDegreesBonus;
        /// <summary>Extra damage multiplier against air units (anti-air upgrades).</summary>
        public float AirDamageMultiplier = 1f;
        /// <summary>Lets the unit's weapons shoot air units too (anti-air upgrades).</summary>
        public bool AddsAirTargeting;
        /// <summary>Switches on the unit's mid-battle drop (the Pelican's troop bay).</summary>
        public bool UnlocksReinforcement;

        /// <summary>A short list of what it does, for the HUD (e.g. "+25% health, +3 m range").</summary>
        public string EffectsText()
        {
            var parts = new List<string>();
            if (HealthMultiplier != 1f) parts.Add(Percent(HealthMultiplier) + " health");
            if (DamageMultiplier != 1f) parts.Add(Percent(DamageMultiplier) + " damage");
            if (FireRateMultiplier != 1f) parts.Add(Percent(FireRateMultiplier) + " rate of fire");
            if (RangeBonus != 0f) parts.Add($"+{RangeBonus:0.#}\u00A0m range");
            if (MoveSpeedMultiplier != 1f) parts.Add(Percent(MoveSpeedMultiplier) + " speed");
            if (ShieldBonus != 0f) parts.Add($"+{ShieldBonus:0} shield");
            if (SplashRadiusBonus != 0f) parts.Add($"+{SplashRadiusBonus:0.#}\u00A0m splash");
            if (ConeDegreesBonus != 0f) parts.Add($"+{ConeDegreesBonus:0} degree cone");
            if (AirDamageMultiplier != 1f) parts.Add(Percent(AirDamageMultiplier) + " damage vs air");
            if (AddsAirTargeting) parts.Add("can shoot air");
            if (UnlocksReinforcement) parts.Add("drops troops mid-battle");
            return string.Join(", ", parts);
        }

        private static string Percent(float multiplier)
        {
            int change = (int)MathF.Round((multiplier - 1f) * 100f);
            return (change >= 0 ? "+" : "") + change + "%";
        }

        public List<string> Validate(string unitName)
        {
            var problems = new List<string>();
            string label = $"{unitName} tech '{Name}'";
            if (string.IsNullOrEmpty(Name)) problems.Add($"{unitName}: a tech upgrade has no name.");
            if (Cost < 0) problems.Add($"{label}: cost cannot be negative.");
            if (HealthMultiplier <= 0f || DamageMultiplier <= 0f || FireRateMultiplier <= 0f || MoveSpeedMultiplier <= 0f || AirDamageMultiplier <= 0f)
                problems.Add($"{label}: multipliers must be above 0.");
            return problems;
        }
    }

    public static class UnitUpgrades
    {
        public const int MaxLevel = 5;
        public const int MaxTechsPerUnit = 3;

        /// <summary>
        /// The unit's stats at this squad level with these techs. Returns the base stats themselves when
        /// nothing changes (level 1, no techs), otherwise a new copy.
        /// </summary>
        public static UnitStats Apply(UnitStats baseStats, int level, IReadOnlyList<TechUpgrade> techs)
        {
            return Apply(baseStats, level, techs, null, null);
        }

        /// <summary>
        /// The same, also with the squad's item and the army's buffs (decision D33). Melee weapons (3 m
        /// or less) are not "ranged": the army's range bonus skips them. If it has techs, the result
        /// remembers the stats without them (WithoutTechs), which an EMP switches to for a while.
        /// </summary>
        public static UnitStats Apply(UnitStats baseStats, int level, IReadOnlyList<TechUpgrade> techs, ItemStats item, ArmyBuffs buffs)
        {
            level = Math.Max(1, Math.Min(MaxLevel, level));
            bool hasTechs = techs != null && techs.Count > 0;
            if (level == 1 && !hasTechs && item == null && (buffs == null || buffs.IsNeutral)) return baseStats;

            UnitStats stats = ApplyLevelAndTechs(baseStats, level, techs);
            ApplyItemAndBuffs(stats, item, buffs);
            if (hasTechs)
            {
                UnitStats withoutTechs = ApplyLevelAndTechs(baseStats, level, null);
                ApplyItemAndBuffs(withoutTechs, item, buffs);
                stats.WithoutTechs = withoutTechs;
            }
            return stats;
        }

        private static void ApplyItemAndBuffs(UnitStats stats, ItemStats item, ArmyBuffs buffs)
        {
            if (item != null)
            {
                stats.MaxHealth *= item.HealthMultiplier;
                stats.MoveSpeed *= item.MoveSpeedMultiplier;
                stats.LifestealShare = Math.Max(stats.LifestealShare, item.LifestealShare);
                stats.HealPerSecondShare = item.HealPerSecondShare;
                if (stats.DeathBurst != null) stats.DeathBurst.Damage *= item.DamageMultiplier;
                foreach (WeaponStats weapon in stats.Weapons)
                {
                    weapon.DamagePerShot *= item.DamageMultiplier;
                    weapon.Range += item.RangeBonus;
                }
            }
            if (buffs != null)
            {
                stats.MaxHealth *= buffs.HealthMultiplier;
                foreach (WeaponStats weapon in stats.Weapons)
                {
                    weapon.DamagePerShot *= buffs.DamageMultiplier;
                    if (weapon.Range > MeleeRange) weapon.Range += buffs.RangeBonus;
                }
                if (stats.DeathBurst != null) stats.DeathBurst.Damage *= buffs.DamageMultiplier;
                if (!buffs.IsNeutral) BuffWhatItBrings(stats, buffs);
            }
        }

        /// <summary>What a unit drops, releases or leaves behind fights in the same army, so it has the army's bonuses too.</summary>
        private static void BuffWhatItBrings(UnitStats stats, ArmyBuffs buffs)
        {
            if (stats.Reinforcement != null) stats.Reinforcement = Apply(stats.Reinforcement, 1, null, null, buffs);
            if (stats.LockedReinforcement != null) stats.LockedReinforcement = Apply(stats.LockedReinforcement, 1, null, null, buffs);
            if (stats.SpawnedUnit != null) stats.SpawnedUnit = Apply(stats.SpawnedUnit, 1, null, null, buffs);
            if (stats.AlternateSpawnedUnit != null) stats.AlternateSpawnedUnit = Apply(stats.AlternateSpawnedUnit, 1, null, null, buffs);
            if (stats.DeathBurst == null || stats.DeathBurst.Spawns.Length == 0) return;
            var spawns = new SpawnEntry[stats.DeathBurst.Spawns.Length];
            for (int i = 0; i < spawns.Length; i++)
            {
                SpawnEntry spawn = stats.DeathBurst.Spawns[i];
                spawns[i] = new SpawnEntry { Unit = Apply(spawn.Unit, 1, null, null, buffs), Bodies = spawn.Bodies };
            }
            stats.DeathBurst.Spawns = spawns;
        }

        /// <summary>Weapons reaching this far or less count as melee, not ranged.</summary>
        public const float MeleeRange = 3f;

        private static UnitStats ApplyLevelAndTechs(UnitStats baseStats, int level, IReadOnlyList<TechUpgrade> techs)
        {
            UnitStats stats = baseStats.Clone();
            stats.Level = level;
            stats.MaxHealth *= level;
            foreach (WeaponStats weapon in stats.Weapons) weapon.DamagePerShot *= level;
            if (stats.DeathBurst != null) stats.DeathBurst.Damage *= level;

            if (techs == null) return stats;
            foreach (TechUpgrade tech in techs)
            {
                if (tech == null) continue;
                stats.MaxHealth *= tech.HealthMultiplier;
                stats.MoveSpeed *= tech.MoveSpeedMultiplier;
                if (tech.ShieldBonus != 0f)
                {
                    stats.MaxShield += tech.ShieldBonus;
                    stats.ShieldRegenPerSecond += tech.ShieldRegenBonus;
                }
                if (stats.DeathBurst != null) stats.DeathBurst.Damage *= tech.DamageMultiplier;
                foreach (WeaponStats weapon in stats.Weapons)
                {
                    weapon.DamagePerShot *= tech.DamageMultiplier;
                    weapon.FireInterval /= tech.FireRateMultiplier;
                    weapon.Range += tech.RangeBonus;
                    if (weapon.SplashRadius > 0f) weapon.SplashRadius += tech.SplashRadiusBonus;
                    if (weapon.ConeDegrees > 0f) weapon.ConeDegrees = Math.Min(360f, weapon.ConeDegrees + tech.ConeDegreesBonus);
                    weapon.AirDamageMultiplier *= tech.AirDamageMultiplier;
                    if (tech.AddsAirTargeting && !weapon.CanTarget(stats, targetIsAir: true))
                    {
                        weapon.HasOwnTargetFilter = true;
                        weapon.TargetFilter = TargetFilter.Both;
                    }
                }
                if (tech.AddsAirTargeting && stats.TargetFilter == TargetFilter.Ground) stats.TargetFilter = TargetFilter.Both;
                if (tech.UnlocksReinforcement && stats.LockedReinforcement != null) stats.Reinforcement = stats.LockedReinforcement;
            }
            return stats;
        }
    }
}
