// SpellDefinition.cs
// The data asset for one spell card (Bible Section 10, Milestone 7). A faction asset lists the spells
// it can be offered. Assets live in Data/Spells/<Faction>/. See Core/SpellStats for what each field
// does; ToStats() copies the values into plain C# (the drop unit through the shared stats cache, so a
// dropped unit is recognised like any other unit of that type).
// Create one with right-click > Create > Halobellum > Spell.
// Depends on: UnitDefinition, Halobellum.Core (SpellStats), UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Spell", fileName = "NewSpell")]
    public sealed class SpellDefinition : ScriptableObject
    {
        public string spellName;
        [TextArea] public string description;
        [Tooltip("Offensive: placed anywhere, red outline. Defensive (and buffs): only on your own side, blue outline.")]
        public bool offensive;
        public SpellEffect effect;

        [Header("Card")]
        [Tooltip("Credits paid once, when the card is taken.")]
        public int cost;
        [Tooltip("Rounds it recharges after being used.")]
        public int cooldownRounds;
        [Tooltip("How many copies a player may own (1 for high-impact spells).")]
        public int maxOwnedCopies = 1;

        [Header("Area and timing")]
        [Tooltip("Radius in metres.")]
        public float radius;
        [Tooltip("Seconds after the battle starts before it takes effect.")]
        public float delaySeconds;
        [Tooltip("The first round it can be offered (extremely impactful spells only appear late).")]
        public int minRound;
        [Tooltip("A line instead of a circle: its length in metres (the player clicks one end and aims the other). 0 = a circle of Radius.")]
        public float areaWidth;
        [Tooltip("A line: its width in metres.")]
        public float areaDepth;

        [Header("Damage (strikes and zones)")]
        public float damagePerTick;
        public DamageType damageType;
        public bool isAntiArmor;
        [Tooltip("Unarmored enemies it kills rise as infected units.")]
        public bool infectsKills;
        [Tooltip("1 = a single strike; more = a zone that hits this many times.")]
        public int ticks = 1;
        public float tickInterval;
        public TargetFilter targets = TargetFilter.Ground;

        [Header("Buff")]
        [Tooltip("Bonus shield as a share of each unit's health (0.5 = half its health).")]
        public float shieldBonusShare;
        [Tooltip("Damage multiplier for the rest of the battle (1 = none).")]
        public float damageMultiplier = 1f;

        [Header("Drop pods")]
        public UnitDefinition dropUnit;
        public int dropSquads = 1;

        [Header("Disruption (EMP)")]
        [Tooltip("Shield removed from each unit (health is not touched).")]
        public float shieldDamage;
        public float techDisableSeconds;
        [Tooltip("Armored units move this share slower (0.4 = 40% slower).")]
        public float slowShare;
        public float slowSeconds;

        [Header("Smoke")]
        [Tooltip("How long it hangs in seconds; 0 = until the battle ends.")]
        public float durationSeconds;
        [Tooltip("Weapon range of units inside is multiplied by this (0.5 = half).")]
        public float rangeMultiplier = 1f;

        public SpellStats ToStats(Dictionary<UnitDefinition, UnitStats> statsCache = null)
        {
            UnitStats drop = null;
            if (dropUnit != null)
            {
                if (statsCache == null || !statsCache.TryGetValue(dropUnit, out drop))
                {
                    drop = dropUnit.ToStats();
                    if (statsCache != null) statsCache[dropUnit] = drop;
                }
            }
            return new SpellStats
            {
                Name = string.IsNullOrEmpty(spellName) ? name : spellName,
                Description = description ?? "",
                Offensive = offensive,
                Effect = effect,
                Cost = cost,
                CooldownRounds = cooldownRounds,
                MaxOwnedCopies = maxOwnedCopies,
                Radius = radius,
                DelaySeconds = delaySeconds,
                MinRound = minRound,
                AreaWidth = areaWidth,
                AreaDepth = areaDepth,
                ShieldDamage = shieldDamage,
                TechDisableSeconds = techDisableSeconds,
                SlowShare = slowShare,
                SlowSeconds = slowSeconds,
                DurationSeconds = durationSeconds,
                RangeMultiplier = rangeMultiplier,
                DamagePerTick = damagePerTick,
                DamageType = damageType,
                IsAntiArmor = isAntiArmor,
                InfectsKills = infectsKills,
                Ticks = ticks,
                TickInterval = tickInterval,
                Targets = targets,
                ShieldBonusShare = shieldBonusShare,
                DamageMultiplier = damageMultiplier,
                DropUnit = drop,
                DropSquads = dropSquads,
            };
        }
    }
}
