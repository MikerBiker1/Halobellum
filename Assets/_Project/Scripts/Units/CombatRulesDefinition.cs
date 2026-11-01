// CombatRulesDefinition.cs
// The data asset holding global battle rules: tick rate, retarget interval, shield regen delay,
// battle time limit, arena size and the damage multiplier table (Bible Sections 4 to 6).
// There should be exactly one, at Assets/_Project/Data/CombatRules.asset. Change balance numbers
// here in the Inspector, never in code. ToRules() copies the values into plain C# CombatRules.
// Depends on: Halobellum.Core (CombatRules, DamageTable, enums), UnityEngine.

using System;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Combat Rules", fileName = "CombatRules")]
    public sealed class CombatRulesDefinition : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/_Project/Data/CombatRules.asset";

        [Header("Simulation timing")]
        [Tooltip("Fixed simulation steps per second.")]
        public int ticksPerSecond;
        [Tooltip("Seconds between target re-evaluations.")]
        public float retargetIntervalSeconds;
        [Tooltip("Seconds without damage before shields start to regenerate.")]
        public float shieldRegenDelaySeconds;
        [Tooltip("Battles end after this many seconds.")]
        public float battleTimeLimitSeconds;
        [Tooltip("The main weapon fires only when the unit faces its target within this many degrees. 180 = any direction.")]
        public float mainWeaponFacingToleranceDegrees;
        [Tooltip("Units notice enemies within weapon range x this. With nothing in sight they march ahead; inside the enemy's zone the range is unlimited.")]
        public float acquisitionRangeMultiplier;

        [Header("Arena (deployment-grid squares)")]
        [Tooltip("Size of one deployment-grid square in metres.")]
        public float squareSize;
        [Tooltip("Arena width, left to right, in squares.")]
        public int arenaWidthSquares;
        [Tooltip("Depth of each army's deployment zone in squares.")]
        public int deploymentDepthSquares;
        [Tooltip("Squares of no man's land between the two deployment zones.")]
        public int noMansLandSquares;

        [Header("Movement tuning")]
        [Tooltip("Neighbor-search grid cell size in metres.")]
        public float spatialCellSize;
        [Tooltip("Extra gap in metres walking units try to keep between bodies.")]
        public float separationPadding;
        [Tooltip("How strongly walking units push away from bodies they touch.")]
        public float separationWeight;
        [Tooltip("How strongly walking units swerve around stopped or slower bodies ahead.")]
        public float avoidanceWeight;
        [Tooltip("How far ahead units look for obstacles, in seconds of travel.")]
        public float avoidanceLookaheadSeconds;
        [Tooltip("Overlap-resolution passes per tick.")]
        public int collisionIterations;
        [Tooltip("Gap in metres between neighbouring bodies when a squad is placed in formation.")]
        public float formationGap;
        [Tooltip("Marching bodies (in formation, or advancing with nothing in sight) aim this many seconds of travel ahead.")]
        public float formationLookaheadSeconds;
        [Tooltip("A walking unit steps aside for a friendly unit behind it that is at least this many times faster (0 = never).")]
        public float yieldSpeedRatio;
        [Tooltip("How strongly a walking unit steps aside for a faster friendly unit behind it.")]
        public float yieldWeight;
        [Tooltip("A fighting unit with a blocked friend right behind it edges toward its target at this share of its speed, still firing (0 = never).")]
        public float advanceSpeedMultiplier;
        [Tooltip("It stops edging forward once its target is within this share of its main weapon's range.")]
        [Range(0f, 1f)] public float advanceRangeShare;

        [Header("Infection (Bible Section 8.3)")]
        [Tooltip("An Unarmored unit that dies within this many metres of an Infection Form rises as an infected unit.")]
        public float infectionRange = 2f;
        [Tooltip("Seconds until a killed unit rises.")]
        public float infectionDelaySeconds = 2f;
        [Tooltip("An infected unit's health, as a share of what the unit had.")]
        public float infectedHealthShare = 0.6f;
        [Tooltip("An infected unit's melee damage per second, as a share of the damage per second of the unit's weapons.")]
        public float infectedDamageShare = 1f;
        public float infectedSpeedMultiplier = 1.5f;
        public float infectedMeleeRange = 1.2f;
        public float infectedMeleeInterval = 1f;

        [Header("Damage multipliers (vs Unarmored, Light, Heavy)")]
        public DamageRow ballistic = new DamageRow();
        public DamageRow explosive = new DamageRow();
        public DamageRow energy = new DamageRow();
        public DamageRow fire = new DamageRow();
        public DamageRow melee = new DamageRow();
        [Tooltip("Used instead of the damage type row when a weapon has the anti-armor tag.")]
        public DamageRow antiArmor = new DamageRow();

        [Header("Energy shield multipliers (separate layer, independent of armor)")]
        public ShieldRow shieldMultipliers = new ShieldRow();

        /// <summary>Copies this asset's values into a new plain C# CombatRules for the simulation.</summary>
        public CombatRules ToRules()
        {
            var rules = new CombatRules
            {
                TicksPerSecond = ticksPerSecond,
                RetargetIntervalSeconds = retargetIntervalSeconds,
                ShieldRegenDelaySeconds = shieldRegenDelaySeconds,
                BattleTimeLimitSeconds = battleTimeLimitSeconds,
                MainWeaponFacingToleranceDegrees = mainWeaponFacingToleranceDegrees,
                AcquisitionRangeMultiplier = acquisitionRangeMultiplier,
                SquareSize = squareSize,
                ArenaWidthSquares = arenaWidthSquares,
                DeploymentDepthSquares = deploymentDepthSquares,
                NoMansLandSquares = noMansLandSquares,
                SpatialCellSize = spatialCellSize,
                SeparationPadding = separationPadding,
                SeparationWeight = separationWeight,
                AvoidanceWeight = avoidanceWeight,
                AvoidanceLookaheadSeconds = avoidanceLookaheadSeconds,
                CollisionIterations = collisionIterations,
                FormationGap = formationGap,
                FormationLookaheadSeconds = formationLookaheadSeconds,
                YieldSpeedRatio = yieldSpeedRatio,
                YieldWeight = yieldWeight,
                AdvanceSpeedMultiplier = advanceSpeedMultiplier,
                AdvanceRangeShare = advanceRangeShare,
                InfectionRange = infectionRange,
                InfectionDelaySeconds = infectionDelaySeconds,
                InfectedHealthShare = infectedHealthShare,
                InfectedDamageShare = infectedDamageShare,
                InfectedSpeedMultiplier = infectedSpeedMultiplier,
                InfectedMeleeRange = infectedMeleeRange,
                InfectedMeleeInterval = infectedMeleeInterval,
            };

            DamageTable table = rules.DamageTable;
            table.SetRow(DamageType.Ballistic, ballistic.unarmored, ballistic.light, ballistic.heavy);
            table.SetRow(DamageType.Explosive, explosive.unarmored, explosive.light, explosive.heavy);
            table.SetRow(DamageType.Energy, energy.unarmored, energy.light, energy.heavy);
            table.SetRow(DamageType.Fire, fire.unarmored, fire.light, fire.heavy);
            table.SetRow(DamageType.Melee, melee.unarmored, melee.light, melee.heavy);
            table.SetAntiArmorRow(antiArmor.unarmored, antiArmor.light, antiArmor.heavy);
            ShieldRow s = shieldMultipliers;
            table.SetShieldRow(s.ballistic, s.explosive, s.energy, s.fire, s.melee, s.antiArmor);
            return rules;
        }

        /// <summary>Multipliers against energy shields for each damage type and the anti-armor tag.</summary>
        [Serializable]
        public sealed class ShieldRow
        {
            public float ballistic;
            public float explosive;
            public float energy;
            public float fire;
            public float melee;
            public float antiArmor;
        }

        /// <summary>One row of the damage table: multipliers against each armor type.</summary>
        [Serializable]
        public sealed class DamageRow
        {
            public float unarmored;
            public float light;
            public float heavy;
        }
    }
}
