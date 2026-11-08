// TechUpgradeDefinition.cs
// The data asset for one tech upgrade (Bible Section 9): a name, a description, a credit price and
// its effects. A unit asset lists up to 4 of these in its Tech Upgrades slots; once bought, a tech
// applies to every squad of that unit type for the rest of the match. Assets live in
// Data/Upgrades/<Faction>/. Effects left at 1 (multipliers) or 0 (bonuses) change nothing.
// ToStats() copies the values into plain C# TechUpgrade.
// Depends on: Halobellum.Core (TechUpgrade), UnityEngine.

using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Tech Upgrade", fileName = "NewTechUpgrade")]
    public sealed class TechUpgradeDefinition : ScriptableObject
    {
        public string techName;
        [TextArea] public string description;
        [Tooltip("Credits to research it (once per match, for every squad of the unit type).")]
        public int cost;

        [Header("Multipliers (1 = no change)")]
        public float healthMultiplier = 1f;
        public float damageMultiplier = 1f;
        [Tooltip("1.25 = 25% more shots per second.")]
        public float fireRateMultiplier = 1f;
        public float moveSpeedMultiplier = 1f;
        [Tooltip("Extra damage multiplier against air units.")]
        public float airDamageMultiplier = 1f;

        [Header("Bonuses (0 = no change)")]
        [Tooltip("Metres added to every weapon's range.")]
        public float rangeBonus;
        [Tooltip("Energy shield points added.")]
        public float shieldBonus;
        public float shieldRegenBonus;
        [Tooltip("Metres added to splash radius (weapons that already splash).")]
        public float splashRadiusBonus;
        [Tooltip("Degrees added to cone weapons (flamethrowers).")]
        public float coneDegreesBonus;

        [Header("Special")]
        [Tooltip("Lets the unit's weapons shoot air units.")]
        public bool addsAirTargeting;
        [Tooltip("Switches on the unit's mid-battle drop (e.g. the Pelican's troop bay).")]
        public bool unlocksReinforcement;
        [Tooltip("Shots added to each attack of the main weapon (Double Shot). 0 = none.")]
        public int extraShotsPerAttack;
        [Tooltip("Seconds between the shots of one attack.")]
        public float burstGapSeconds;
        [Tooltip("The main weapon's reload is multiplied by this (1.2 = 20% longer).")]
        public float reloadTimeMultiplier = 1f;

        public TechUpgrade ToStats()
        {
            return new TechUpgrade
            {
                Name = string.IsNullOrEmpty(techName) ? name : techName,
                Description = description ?? "",
                Cost = cost,
                HealthMultiplier = healthMultiplier,
                DamageMultiplier = damageMultiplier,
                FireRateMultiplier = fireRateMultiplier,
                MoveSpeedMultiplier = moveSpeedMultiplier,
                AirDamageMultiplier = airDamageMultiplier,
                RangeBonus = rangeBonus,
                ShieldBonus = shieldBonus,
                ShieldRegenBonus = shieldRegenBonus,
                SplashRadiusBonus = splashRadiusBonus,
                ConeDegreesBonus = coneDegreesBonus,
                AddsAirTargeting = addsAirTargeting,
                UnlocksReinforcement = unlocksReinforcement,
                ExtraShotsPerAttack = extraShotsPerAttack,
                BurstGapSeconds = burstGapSeconds,
                ReloadTimeMultiplier = reloadTimeMultiplier <= 0f ? 1f : reloadTimeMultiplier,
            };
        }
    }
}
