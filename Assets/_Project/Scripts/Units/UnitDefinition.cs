// UnitDefinition.cs
// The data asset for one unit type (Bible Section 4, rule 1). Every unit stat lives here, edited in
// the Unity Inspector, never in code. Adding a unit means adding one of these assets
// (right-click in the Project window > Create > Halobellum > Unit Definition).
// ToStats() copies the values into plain C# UnitStats, which is what the simulation reads.
// Depends on: Halobellum.Core (UnitStats, WeaponStats, DefenseArcStats, enums), UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Unit Definition", fileName = "NewUnit")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string unitName;
        public UnitRole role;
        [Tooltip("1 to 3, or 4 for Titans.")]
        public int tier;
        [Tooltip("Credits to unlock this unit type for the rest of the match.")]
        public int unlockCost;

        [Header("Squad")]
        [Tooltip("Number of bodies deployed when one squad is bought.")]
        public int squadSize;
        [Tooltip("Price of one squad in credits.")]
        public int cost;
        [Tooltip("Damage each surviving body deals to the enemy base. Bible default: (cost / squad size) / 5.")]
        public float baseDamage;

        [Header("Survivability (per body)")]
        public float maxHealth;
        public ArmorType armorType;
        [Tooltip("Energy shield on top of health. 0 means no shield.")]
        public float maxShield;
        [Tooltip("Shield points regained per second after the regen delay without damage.")]
        public float shieldRegenPerSecond;

        [Header("Movement")]
        [Tooltip("Metres per second.")]
        public float moveSpeed;
        [Tooltip("Radius of the round collision footprint in metres.")]
        public float collisionRadius;
        [Tooltip("Air units ignore ground collision and fly in straight lines.")]
        public bool isAir;

        [Header("Combat")]
        public TargetFilter targetFilter;
        [Tooltip("The first weapon is the main weapon: its target and range decide where the unit moves and when it stops to fight. Other weapons pick their own targets.")]
        public List<WeaponDefinition> weapons = new List<WeaponDefinition>();
        [Tooltip("Only a few units (Shield Jackals, Hunters) have directional defense.")]
        public bool hasDefenseArc;
        public DefenseArcDefinition defenseArc = new DefenseArcDefinition();

        [Header("Visuals")]
        [Tooltip("Model shown for each body in the battle scene. Tinted with the side's color. If empty, a placeholder primitive sized from the collision radius is used.")]
        public GameObject prefab;

        [Header("Design notes (shown in tooltips)")]
        [TextArea] public string strongAgainst;
        [TextArea] public string weakAgainst;
        [Tooltip("The unit's two best counters (Bible Section 7) and any other notes.")]
        [TextArea] public string notes;

        /// <summary>Copies this asset's values into a new plain C# UnitStats for the simulation.</summary>
        public UnitStats ToStats()
        {
            var stats = new UnitStats
            {
                Name = string.IsNullOrEmpty(unitName) ? name : unitName,
                Role = role,
                Tier = tier,
                UnlockCost = unlockCost,
                SquadSize = squadSize,
                Cost = cost,
                BaseDamage = baseDamage,
                MaxHealth = maxHealth,
                ArmorType = armorType,
                MaxShield = maxShield,
                ShieldRegenPerSecond = shieldRegenPerSecond,
                MoveSpeed = moveSpeed,
                CollisionRadius = collisionRadius,
                IsAir = isAir,
                TargetFilter = targetFilter,
                DefenseArc = hasDefenseArc && defenseArc != null ? defenseArc.ToStats() : null,
            };

            int count = weapons?.Count ?? 0;
            stats.Weapons = new WeaponStats[count];
            for (int i = 0; i < count; i++) stats.Weapons[i] = weapons[i]?.ToStats();
            return stats;
        }
    }

    /// <summary>Inspector data for one weapon. See WeaponStats for what each value means.</summary>
    [Serializable]
    public sealed class WeaponDefinition
    {
        public string weaponName;
        public float damagePerShot;
        [Tooltip("Seconds between shots. Rate of fire = 1 / interval.")]
        public float fireInterval;
        public float range;
        [Tooltip("Artillery cannot hit targets closer than this. 0 = no minimum.")]
        public float minRange;
        [Tooltip("Metres per second. 0 = instant hit.")]
        public float projectileSpeed;
        [Tooltip("0 = single target.")]
        public float splashRadius;
        public DamageType damageType;
        [Tooltip("Uses the anti-armor row of the damage table.")]
        public bool isAntiArmor;
        [Tooltip("Can be blocked by a defense arc such as a Jackal shield.")]
        public bool canBeBlocked;

        public WeaponStats ToStats()
        {
            return new WeaponStats
            {
                Name = weaponName ?? "",
                DamagePerShot = damagePerShot,
                FireInterval = fireInterval,
                Range = range,
                MinRange = minRange,
                ProjectileSpeed = projectileSpeed,
                SplashRadius = splashRadius,
                DamageType = damageType,
                IsAntiArmor = isAntiArmor,
                CanBeBlocked = canBeBlocked,
            };
        }
    }

    /// <summary>Inspector data for an optional directional defense arc. See DefenseArcStats.</summary>
    [Serializable]
    public sealed class DefenseArcDefinition
    {
        [Tooltip("Total width of the front arc in degrees.")]
        public float arcDegrees;
        public float frontDamageMultiplier = 1f;
        public float rearDamageMultiplier = 1f;
        [Range(0f, 1f)] public float blockChance;
        public List<DamageType> blockedDamageTypes = new List<DamageType>();

        public DefenseArcStats ToStats()
        {
            return new DefenseArcStats
            {
                ArcDegrees = arcDegrees,
                FrontDamageMultiplier = frontDamageMultiplier,
                RearDamageMultiplier = rearDamageMultiplier,
                BlockChance = blockChance,
                BlockedDamageTypes = blockedDamageTypes == null ? new DamageType[0] : blockedDamageTypes.ToArray(),
            };
        }
    }
}
