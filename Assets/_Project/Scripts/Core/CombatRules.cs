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
        /// <summary>
        /// A unit's main weapon only fires when the unit faces its target within this many degrees
        /// (the unit must turn its body to aim). 180 means it can fire in any direction.
        /// </summary>
        public float MainWeaponFacingToleranceDegrees;

        // Arena (decision D4): measured in deployment-grid squares, like Mechabellum.
        // Team A's deployment zone, then a strip of no man's land, then Team B's zone.
        /// <summary>Size of one deployment-grid square in metres.</summary>
        public float SquareSize;
        /// <summary>Arena width (left to right) in squares.</summary>
        public int ArenaWidthSquares;
        /// <summary>Depth of each army's deployment zone in squares.</summary>
        public int DeploymentDepthSquares;
        /// <summary>Squares of no man's land between the two deployment zones.</summary>
        public int NoMansLandSquares;

        /// <summary>Total length in squares, from Team A's edge to Team B's edge.</summary>
        public int ArenaLengthSquares => DeploymentDepthSquares * 2 + NoMansLandSquares;
        /// <summary>Arena width in metres. X runs from -ArenaWidth/2 to +ArenaWidth/2.</summary>
        public float ArenaWidth => ArenaWidthSquares * SquareSize;
        /// <summary>Arena length in metres. Y runs from -ArenaLength/2 (Team A's edge) to +ArenaLength/2.</summary>
        public float ArenaLength => ArenaLengthSquares * SquareSize;
        /// <summary>Distance in metres from the centre line to the front edge of each deployment zone.</summary>
        public float DeploymentZoneFront => NoMansLandSquares * SquareSize * 0.5f;

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
        /// <summary>Gap in metres between neighbouring bodies when a squad is placed in formation.</summary>
        public float FormationGap;
        /// <summary>While a squad marches in formation, each body aims this many seconds of travel ahead of its place.</summary>
        public float FormationLookaheadSeconds;

        public DamageTable DamageTable = new DamageTable();

        public float TickSeconds => TicksPerSecond > 0 ? 1f / TicksPerSecond : 0f;
    }
}
