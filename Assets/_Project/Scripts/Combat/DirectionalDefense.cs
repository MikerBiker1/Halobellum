// DirectionalDefense.cs
// Optional directional defense (Bible Section 6). Most units have none and always take normal
// damage. A unit whose data has a defense arc checks where a hit comes from:
//   - inside the arc (centred on the unit's facing): front damage multiplier, and a blockable
//     projectile of a listed damage type may be blocked entirely (BlockChance, seeded random roll);
//   - outside the arc: rear damage multiplier.
// Example: Shield Jackal, 120 degree arc, blocks 80% of ballistic projectiles from the front.
// Example: Hunter, 240 degree arc, low front multiplier, high rear multiplier.
// Only the projectile's direct target can block it; splash damage is never blocked.
// Depends on: Halobellum.Core (Vec2, DefenseArcStats, WeaponStats, SimRandom).

using Halobellum.Core;

namespace Halobellum.Combat
{
    public struct DefenseResult
    {
        /// <summary>True if the hit was stopped by the defense arc and deals no damage.</summary>
        public bool Blocked;
        /// <summary>Multiplier to pass to DamageCalculator.ComputeDamage.</summary>
        public float DamageMultiplier;
        /// <summary>True if the hit arrived from inside the defense arc.</summary>
        public bool FromFront;

        public static DefenseResult Normal => new DefenseResult { Blocked = false, DamageMultiplier = 1f, FromFront = true };
    }

    public static class DirectionalDefense
    {
        // Small tolerance so a hit exactly on the arc edge counts as front.
        private const float EdgeToleranceDegrees = 0.01f;

        /// <summary>True if a hit coming from sourcePosition arrives inside the arc around the defender's facing.</summary>
        public static bool IsInsideArc(Vec2 defenderPosition, Vec2 facing, Vec2 sourcePosition, float arcDegrees)
        {
            if (arcDegrees >= 360f) return true;
            if (arcDegrees <= 0f) return false;

            Vec2 toSource = sourcePosition - defenderPosition;
            if (toSource.LengthSquared < 1e-8f) return true; // Hit from the unit's own position: treat as front.

            float angle = Vec2.AngleDegrees(facing, toSource);
            return angle <= arcDegrees * 0.5f + EdgeToleranceDegrees;
        }

        /// <summary>
        /// Works out how a defense arc changes one hit. Units without an arc (arc is null) always get
        /// DefenseResult.Normal. The random generator is only used when a block is actually possible,
        /// so the same battle and seed always roll the same way.
        /// </summary>
        /// <param name="isDirectHit">True for the projectile's own target, false for splash victims.</param>
        public static DefenseResult Evaluate(DefenseArcStats arc, Vec2 defenderPosition, Vec2 facing,
            Vec2 sourcePosition, WeaponStats weapon, bool isDirectHit, SimRandom random)
        {
            if (arc == null || arc.ArcDegrees <= 0f) return DefenseResult.Normal;

            bool front = IsInsideArc(defenderPosition, facing, sourcePosition, arc.ArcDegrees);
            if (!front)
            {
                return new DefenseResult { Blocked = false, DamageMultiplier = arc.RearDamageMultiplier, FromFront = false };
            }

            bool canBlock = isDirectHit && weapon.CanBeBlocked && arc.BlockChance > 0f && arc.Blocks(weapon.DamageType);
            bool blocked = canBlock && random.Chance(arc.BlockChance);
            return new DefenseResult { Blocked = blocked, DamageMultiplier = arc.FrontDamageMultiplier, FromFront = true };
        }
    }
}
