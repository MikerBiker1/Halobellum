// Targeting.cs
// Target selection (Bible Section 6, Targeting): a unit always picks the closest enemy it can
// legally target, with no priorities or preferred targets. "Legal" means:
//   - the enemy is alive and on the other team,
//   - the unit's target filter allows it (ground, air or both),
//   - the enemy is not closer than the weapon's minimum range (artillery).
// Distances for range checks are measured from the shooter's centre to the target's edge, so
// large units can be hit from as far as small ones. Ties go to the lower unit id, so results
// are repeatable. The simulation calls this every 0.5 seconds, not every tick.
// Units only notice enemies within their acquisition range (3 x weapon range by default, from
// CombatRules) until they reach the enemy's deployment zone; then the range is unlimited.
// Depends on: SimUnit, Halobellum.Core (WeaponStats, Vec2).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public static class Targeting
    {
        /// <summary>Distance from the shooter's centre to the target's edge (never below 0).</summary>
        public static float EdgeDistance(SimUnit shooter, SimUnit target)
        {
            return Math.Max(0f, Vec2.Distance(shooter.Position, target.Position) - target.Radius);
        }

        /// <summary>True if the target is alive, not already at 0 health, and on the other team.</summary>
        public static bool IsTargetableEnemy(SimUnit shooter, SimUnit target)
        {
            return target != null && target.IsAlive && target.Health > 0f && target.Team != shooter.Team;
        }

        /// <summary>True if the shooter may pick this target for this weapon (filter and minimum range).</summary>
        public static bool IsLegalTarget(SimUnit shooter, WeaponStats weapon, SimUnit target)
        {
            if (!IsTargetableEnemy(shooter, target)) return false;
            if (!shooter.Stats.CanTarget(target.IsAir)) return false;
            float minRange = weapon?.MinRange ?? 0f;
            return minRange <= 0f || EdgeDistance(shooter, target) >= minRange;
        }

        /// <summary>True if the target is within the weapon's range (and outside its minimum range).</summary>
        public static bool IsInRange(SimUnit shooter, WeaponStats weapon, SimUnit target)
        {
            float distance = EdgeDistance(shooter, target);
            return distance <= weapon.Range && distance >= weapon.MinRange;
        }

        /// <summary>
        /// The closest enemy this weapon may legally target, in range or not, or null if there is none.
        /// If the result is out of range, no legal enemy is in range, so the unit should move toward it.
        /// </summary>
        /// <param name="acquisitionRange">Enemies further than this (to their edge) are not noticed at all. Infinity = no limit.</param>
        public static SimUnit FindClosestLegalTarget(SimUnit shooter, WeaponStats weapon, IReadOnlyList<SimUnit> units,
            float acquisitionRange = float.PositiveInfinity)
        {
            SimUnit best = null;
            float bestDistanceSquared = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit candidate = units[i];
                if (!IsLegalTarget(shooter, weapon, candidate)) continue;
                if (!float.IsPositiveInfinity(acquisitionRange) && EdgeDistance(shooter, candidate) > acquisitionRange) continue;

                float distanceSquared = Vec2.DistanceSquared(shooter.Position, candidate.Position);
                bool closer = distanceSquared < bestDistanceSquared;
                bool tieWithLowerId = distanceSquared == bestDistanceSquared && best != null && candidate.Id < best.Id;
                if (closer || tieWithLowerId)
                {
                    best = candidate;
                    bestDistanceSquared = distanceSquared;
                }
            }
            return best;
        }
    }
}
