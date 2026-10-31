// MovementSystem.cs
// Free movement in continuous 2D space (Bible Section 6, Movement). Each tick, every unit in the
// Move state steers toward its MoveGoal; there is no grid during battle. Steering combines:
//   - a pull toward the goal at the unit's move speed (never stepping past the goal),
//   - separation: a push away from bodies it is touching or nearly touching,
//   - avoidance: a sideways swerve around bodies ahead that are stopped or slower, so units
//     flow around allies and faster units overtake slower ones,
//   - turning: units turn at their own speed (TurnTime180; bigger units turn slower) and drive
//     forward along their facing, so a unit facing the wrong way stops and turns first.
// After moving, overlapping bodies are pushed apart (bodies cannot overlap). A unit that is
// standing and fighting is not pushed by units walking past it. Air units ignore ground units
// entirely, fly in straight lines, and only keep apart from other air units.
// Units are kept inside the arena. Unity only displays the resulting positions.
// Depends on: SimUnit, SpatialHashGrid, Halobellum.Core (CombatRules, Vec2).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public sealed class MovementSystem
    {
        private readonly CombatRules _rules;
        private readonly SpatialHashGrid _grid;
        private readonly List<SimUnit> _neighbors = new List<SimUnit>();
        private Vec2[] _newVelocities = new Vec2[0];
        private Vec2[] _newFacings = new Vec2[0];

        public MovementSystem(CombatRules rules)
        {
            _rules = rules;
            _grid = new SpatialHashGrid(rules.ArenaWidth, rules.ArenaLength, rules.SpatialCellSize);
        }

        /// <summary>The neighbor grid, rebuilt every Step. Other systems may query it after a Step.</summary>
        public SpatialHashGrid Grid => _grid;

        /// <summary>Advances all units by one tick of dt seconds.</summary>
        public void Step(List<SimUnit> units, float dt)
        {
            if (_newVelocities.Length < units.Count)
            {
                int size = Math.Max(units.Count, _newVelocities.Length * 2);
                _newVelocities = new Vec2[size];
                _newFacings = new Vec2[size];
            }

            // 1. Decide every velocity from the same snapshot of positions, so list order doesn't matter.
            _grid.Build(units);
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                _newFacings[i] = unit.Facing;
                _newVelocities[i] = unit.IsAlive && unit.State == UnitState.Move ? SteeringVelocity(unit, dt, out _newFacings[i]) : Vec2.Zero;
            }

            // 2. Move and turn.
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.IsAlive) continue;
                Vec2 velocity = _newVelocities[i];
                unit.Velocity = velocity;
                if (velocity.LengthSquared > 1e-8f) unit.Position += velocity * dt;

                if (unit.Stats.TurnTime180 > 0f) unit.Facing = _newFacings[i];
                else if (velocity.LengthSquared > 1e-8f) unit.Facing = velocity.Normalized; // Instant turners face where they move.
            }

            // 3. Push overlapping bodies apart, then keep everyone inside the arena.
            for (int iteration = 0; iteration < _rules.CollisionIterations; iteration++)
            {
                _grid.Build(units);
                ResolveOverlaps(units);
            }
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].IsAlive) ClampToArena(units[i]);
            }
        }

        private Vec2 SteeringVelocity(SimUnit unit, float dt, out Vec2 facing)
        {
            facing = unit.Facing;
            float speed = unit.Stats.MoveSpeed;
            Vec2 toGoal = unit.MoveGoal - unit.Position;
            float distance = toGoal.Length;
            if (speed <= 0f || distance < 1e-4f) return Vec2.Zero;

            Vec2 direction = toGoal / distance;
            // Arrive: never step past the goal in one tick.
            float maxSpeed = Math.Min(speed, distance / dt);

            float lookahead = speed * _rules.AvoidanceLookaheadSeconds;
            float padding = _rules.SeparationPadding;
            _grid.Query(unit.Position, unit.Radius + _grid.MaxRadius + Math.Max(lookahead, padding), _neighbors);

            Vec2 separation = Vec2.Zero;
            Vec2 avoidance = Vec2.Zero;
            foreach (SimUnit other in _neighbors)
            {
                if (other == unit || other.IsAir != unit.IsAir) continue;

                Vec2 offset = unit.Position - other.Position;
                float gap = offset.Length;
                float minDistance = unit.Radius + other.Radius + padding;

                if (gap < minDistance)
                {
                    Vec2 away = gap > 1e-5f ? offset / gap : PairDirection(unit, other);
                    separation += away * ((minDistance - gap) / minDistance);
                }

                if (unit.IsAir) continue; // Air units fly straight; they only keep apart from each other.

                // Avoidance: swerve around a body ahead of us that is stopped or slower than us.
                Vec2 toOther = -offset;
                float ahead = Vec2.Dot(toOther, direction);
                float reach = lookahead + minDistance;
                if (ahead <= 0f || ahead > reach) continue;
                if (Vec2.Dot(other.Velocity, direction) >= speed) continue; // Moving away as fast as us.

                float lateral = Vec2.Cross(direction, toOther); // Positive: the other body is on our left.
                if (MathF.Abs(lateral) >= minDistance) continue; // Not in our path.

                float side;
                if (lateral > 1e-4f) side = -1f;       // Obstacle on the left: swerve right.
                else if (lateral < -1e-4f) side = 1f;  // Obstacle on the right: swerve left.
                else side = unit.Id % 2 == 0 ? 1f : -1f; // Dead ahead: pick a side by id, so it is repeatable.

                float urgency = 1f - ahead / reach;
                float closeness = 1f - MathF.Abs(lateral) / minDistance;
                avoidance += direction.Perpendicular * (side * urgency * closeness);
            }

            Vec2 steer = direction + separation * _rules.SeparationWeight + avoidance * _rules.AvoidanceWeight;
            Vec2 velocity = steer.ClampLength(1f) * maxSpeed;
            velocity = ApplyTurning(unit, velocity, dt, out facing);
            return RemovePushIntoContacts(unit, velocity, padding);
        }

        /// <summary>
        /// Limits how fast a unit turns (its TurnTime180): it turns its facing toward the direction it
        /// wants to go, and only drives forward along its facing, slowing down the further it still has
        /// to turn (a unit facing the wrong way stops and turns first). Instant turners are unchanged.
        /// </summary>
        private static Vec2 ApplyTurning(SimUnit unit, Vec2 velocity, float dt, out Vec2 facing)
        {
            facing = unit.Facing;
            if (unit.Stats.TurnTime180 <= 0f) return velocity;

            float wantedSpeed = velocity.Length;
            if (wantedSpeed < 1e-6f) return Vec2.Zero;
            Vec2 wantedDirection = velocity / wantedSpeed;

            facing = Vec2.RotateTowards(unit.Facing, wantedDirection, unit.Stats.TurnRateDegreesPerSecond * dt);
            float alignment = Math.Max(0f, Vec2.Dot(facing, wantedDirection));
            return facing * (wantedSpeed * alignment);
        }

        /// <summary>
        /// Stops a walker from pressing into bodies it is already touching: the part of its velocity
        /// aimed into a touching body (beyond how fast that body is moving away) is removed, so it
        /// slides along the body instead. This keeps crowds from crushing together.
        /// Uses the neighbor list filled by SteeringVelocity.
        /// </summary>
        private Vec2 RemovePushIntoContacts(SimUnit unit, Vec2 velocity, float padding)
        {
            foreach (SimUnit other in _neighbors)
            {
                if (other == unit || other.IsAir != unit.IsAir) continue;

                Vec2 toOther = other.Position - unit.Position;
                float gap = toOther.Length;
                if (gap < 1e-5f || gap >= unit.Radius + other.Radius + padding) continue;

                Vec2 normal = toOther / gap;
                float otherAway = Math.Max(0f, Vec2.Dot(other.Velocity, normal));
                float into = Vec2.Dot(velocity, normal) - otherAway;
                if (into > 0f) velocity -= normal * into;
            }
            return velocity;
        }

        private void ResolveOverlaps(List<SimUnit> units)
        {
            float maxRadius = _grid.MaxRadius;
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.IsAlive) continue;

                _grid.Query(unit.Position, unit.Radius + maxRadius, _neighbors);
                foreach (SimUnit other in _neighbors)
                {
                    // Handle each pair once, and never mix air with ground.
                    if (other.Id <= unit.Id || other.IsAir != unit.IsAir) continue;

                    Vec2 offset = unit.Position - other.Position;
                    float gap = offset.Length;
                    float minDistance = unit.Radius + other.Radius;
                    if (gap >= minDistance) continue;

                    Vec2 normal = gap > 1e-5f ? offset / gap : PairDirection(unit, other);
                    float overlap = minDistance - gap;
                    PushShares(unit, other, out float unitShare, out float otherShare);
                    unit.Position += normal * (overlap * unitShare);
                    other.Position -= normal * (overlap * otherShare);
                }
            }
        }

        /// <summary>
        /// How much of an overlap each body moves. A unit standing still (fighting or idle) is not
        /// pushed by a unit that is walking, so walkers slide around it. Otherwise both move equally.
        /// </summary>
        private static void PushShares(SimUnit a, SimUnit b, out float aShare, out float bShare)
        {
            bool aAnchored = a.State != UnitState.Move;
            bool bAnchored = b.State != UnitState.Move;
            if (aAnchored && !bAnchored) { aShare = 0f; bShare = 1f; }
            else if (!aAnchored && bAnchored) { aShare = 1f; bShare = 0f; }
            else { aShare = 0.5f; bShare = 0.5f; }
        }

        /// <summary>A repeatable push direction for two bodies at exactly the same spot (from b toward a).</summary>
        private static Vec2 PairDirection(SimUnit a, SimUnit b)
        {
            int low = Math.Min(a.Id, b.Id);
            int high = Math.Max(a.Id, b.Id);
            float angle = (low * 73 + high * 151) % 360;
            Vec2 direction = Vec2.Right.Rotated(angle);
            return a.Id < b.Id ? direction : -direction;
        }

        private void ClampToArena(SimUnit unit)
        {
            float halfWidth = _rules.ArenaWidth * 0.5f - unit.Radius;
            float halfLength = _rules.ArenaLength * 0.5f - unit.Radius;
            float x = Math.Clamp(unit.Position.X, -halfWidth, halfWidth);
            float y = Math.Clamp(unit.Position.Y, -halfLength, halfLength);
            unit.Position = new Vec2(x, y);
        }
    }
}
