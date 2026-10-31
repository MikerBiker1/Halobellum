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

        public DamageTable DamageTable = new DamageTable();

        public float TickSeconds => TicksPerSecond > 0 ? 1f / TicksPerSecond : 0f;
    }
}
