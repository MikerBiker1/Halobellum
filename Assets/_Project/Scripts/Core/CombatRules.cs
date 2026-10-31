// CombatRules.cs
// Global battle settings that are not tied to one unit: simulation tick rate, how often units
// re-pick targets, shield regen delay, battle time limit, arena size and the damage table.
// The values come from the CombatRules data asset (Data/CombatRules.asset) through
// CombatRulesDefinition.ToRules(); tests build their own.
// Depends on: DamageTable.

namespace Halobellum.Core
{
    public sealed class CombatRules
    {
        /// <summary>Fixed simulation steps per second (Bible: 30).</summary>
        public int TicksPerSecond;
        /// <summary>Seconds between target re-evaluations (Bible: 0.5).</summary>
        public float RetargetIntervalSeconds;
        /// <summary>Seconds without damage before an energy shield starts regenerating (Bible: 5).</summary>
        public float ShieldRegenDelaySeconds;
        /// <summary>A battle ends after this many seconds (Bible: 90).</summary>
        public float BattleTimeLimitSeconds;

        /// <summary>Arena size in metres, left to right.</summary>
        public float ArenaWidth;
        /// <summary>Arena size in metres, from Team A's edge to Team B's edge. Each team owns half.</summary>
        public float ArenaLength;

        // Movement tuning (how units steer and keep apart). Technical values, not unit balance.
        /// <summary>Size of one neighbor-search grid cell in metres.</summary>
        public float SpatialCellSize;
        /// <summary>Extra gap in metres that walking units try to keep between bodies.</summary>
        public float SeparationPadding;
        /// <summary>How strongly walking units push away from bodies they are touching.</summary>
        public float SeparationWeight;
        /// <summary>How strongly walking units swerve around stopped or slower bodies ahead.</summary>
        public float AvoidanceWeight;
        /// <summary>How far ahead units look for obstacles, in seconds of travel at their speed.</summary>
        public float AvoidanceLookaheadSeconds;
        /// <summary>Overlap-resolution passes per tick. More passes means less overlap in crowds.</summary>
        public int CollisionIterations;

        public DamageTable DamageTable = new DamageTable();

        public float TickSeconds => TicksPerSecond > 0 ? 1f / TicksPerSecond : 0f;
    }
}
