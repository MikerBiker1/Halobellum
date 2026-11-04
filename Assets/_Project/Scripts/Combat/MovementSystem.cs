// MovementSystem.cs
// Free movement in continuous 2D space (Bible Section 6, Movement). Each tick, every unit in the
// Move state steers toward its MoveGoal; there is no grid during battle. Steering combines:
//   - a pull toward the goal at the unit's move speed (never stepping past the goal),
//   - separation: a push away from bodies it is touching or nearly touching,
//   - avoidance: a sideways swerve around bodies ahead that are stopped or slower, so units
//     flow around allies and faster units overtake slower ones,
//   - yielding (decision D30): a sideways step out of the way of a friendly unit coming from
//     behind that is much faster (YieldSpeedRatio) and at least as big, so chaff parts for a fast
//     vehicle; the vehicle drives straight on, neither swerving around nor bracing against units
//     that are making way for it. Big units (tanks, Titans) never make way for smaller ones,
//     which go around them,
//   - turning: units turn at their own speed (TurnTime180; bigger units turn slower) and drive
//     forward along their facing, so a unit facing the wrong way stops and turns first.
// Fighting units normally stand still; one that is edging forward to make room for a friend stuck
// behind it (SimUnit.Advancing, set by BattleSimulation) steers like a walker at reduced speed.
// After moving, overlapping bodies are pushed apart (bodies cannot overlap). A unit that is
// standing and fighting is not pushed by units walking past it; between two walkers the smaller
// body gives way more (shares by body area). Air units ignore ground units entirely, fly in
// straight lines, and only keep apart from other air units. A unit on a leap (D39) flies straight to
// where it will land, over every body on the way; it lands among them and is pushed apart then.
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
            AskSlowerUnitsToYield(units);
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                _newFacings[i] = unit.Facing;
                if (unit.IsAlive && unit.IsLeaping)
                {
                    _newVelocities[i] = LeapVelocity(unit, dt, ref _newFacings[i]);
                    continue;
                }
                bool steers = unit.State == UnitState.Move || (unit.Advancing && unit.State == UnitState.Attack);
                _newVelocities[i] = unit.IsAlive && steers ? SteeringVelocity(unit, dt, out _newFacings[i]) : Vec2.Zero;
            }

            // 2. Move and turn.
            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.IsAlive) continue;
                Vec2 velocity = _newVelocities[i];
                unit.Velocity = velocity;
                if (velocity.LengthSquared > 1e-8f) unit.Position += velocity * dt;
                if (unit.IsLeaping && Vec2.DistanceSquared(unit.Position, unit.LeapTo) < 1e-4f) unit.IsLeaping = false; // Landed.

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
                // A leap that reaches the edge of the arena ends there.
                if (units[i].IsAlive && ClampToArena(units[i])) units[i].IsLeaping = false;
            }
        }

        /// <summary>Straight at the landing spot at leap speed, never past it.</summary>
        private static Vec2 LeapVelocity(SimUnit unit, float dt, ref Vec2 facing)
        {
            Vec2 toLanding = unit.LeapTo - unit.Position;
            float distance = toLanding.Length;
            if (distance < 1e-4f) return Vec2.Zero;
            facing = toLanding / distance;
            return facing * Math.Min(unit.Stats.LeapSpeed, distance / dt);
        }

        /// <summary>True if two bodies can touch: both on the ground or both in the air, and neither on a leap.</summary>
        private static bool CanTouch(SimUnit a, SimUnit b) => a.IsAir == b.IsAir && !a.IsLeaping && !b.IsLeaping && !a.NotArrived && !b.NotArrived;

        private Vec2 SteeringVelocity(SimUnit unit, float dt, out Vec2 facing)
        {
            facing = unit.Facing;
            float speed = unit.MoveSpeedNow * (unit.State == UnitState.Attack ? _rules.AdvanceSpeedMultiplier : 1f);
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
                if (other == unit || !CanTouch(unit, other)) continue;

                Vec2 offset = unit.Position - other.Position;
                float gap = offset.Length;
                float minDistance = unit.Radius + other.Radius + padding;

                // A walking unit that is making way for us steps aside on its own: we hold our line.
                bool givesWay = other.State == UnitState.Move && MakesWayFor(other, unit);
                if (gap < minDistance)
                {
                    MarkContact(unit, other);
                    if (!givesWay)
                    {
                        Vec2 away = gap > 1e-5f ? offset / gap : PairDirection(unit, other);
                        separation += away * ((minDistance - gap) / minDistance);
                    }
                }

                if (unit.IsAir || givesWay) continue; // Air units fly straight; they only keep apart from each other.
                if (unit.SquadId >= 0 && unit.SquadId == other.SquadId) continue; // Squadmates move together; no swerving around each other.

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
                else side = -1f; // Dead ahead: always keep right, so two units walking at each other pass instead of blocking.

                float urgency = 1f - ahead / reach;
                float closeness = 1f - MathF.Abs(lateral) / minDistance;
                avoidance += direction.Perpendicular * (side * urgency * closeness);
            }

            Vec2 steer = direction + separation * _rules.SeparationWeight + avoidance * _rules.AvoidanceWeight + unit.YieldSteer * _rules.YieldWeight;
            Vec2 velocity = steer.ClampLength(1f) * maxSpeed;
            velocity = ApplyTurning(unit, velocity, dt, out facing);
            return RemovePushIntoContacts(unit, velocity, padding);
        }

        /// <summary>
        /// Decision D30: every walking ground unit looks along its own way (as far as its avoidance
        /// look-ahead) for friendly ground units at least YieldSpeedRatio times slower that stand in its
        /// path, and asks them to step aside (SimUnit.YieldSteer): sideways, to the side they are
        /// already on (left when dead ahead, since the faster unit keeps right), more urgently the
        /// closer they are and the more squarely in its path. Only units fast enough to overtake
        /// someone look, so slow crowds cost nothing extra.
        /// </summary>
        private void AskSlowerUnitsToYield(List<SimUnit> units)
        {
            float slowest = float.MaxValue;
            foreach (SimUnit unit in units)
            {
                unit.YieldSteer = Vec2.Zero;
                if (unit.IsAlive && !unit.IsAir && unit.MoveSpeedNow > 0f) slowest = Math.Min(slowest, unit.MoveSpeedNow);
            }
            if (_rules.YieldSpeedRatio <= 0f || slowest == float.MaxValue) return;

            float padding = _rules.SeparationPadding;
            foreach (SimUnit unit in units)
            {
                if (!unit.IsAlive || unit.IsAir || unit.IsLeaping || (unit.State != UnitState.Move && !unit.Advancing)) continue;
                float speed = unit.MoveSpeedNow;
                if (speed < slowest * _rules.YieldSpeedRatio) continue; // Nobody here is slow enough to make way for it.
                Vec2 way = unit.MoveGoal - unit.Position;
                float length = way.Length;
                if (length < 1e-4f) continue;
                way /= length;

                float lookahead = speed * _rules.AvoidanceLookaheadSeconds;
                _grid.Query(unit.Position, lookahead + unit.Radius + _grid.MaxRadius + padding, _neighbors);
                foreach (SimUnit other in _neighbors)
                {
                    if (other == unit || !MakesWayFor(other, unit)) continue;
                    Vec2 offset = other.Position - unit.Position;
                    float minDistance = unit.Radius + other.Radius + padding;
                    float reach = lookahead + minDistance;
                    float ahead = Vec2.Dot(offset, way);
                    float across = Vec2.Cross(way, offset); // Positive: it is on our left.
                    if (ahead <= 0f || ahead >= reach || MathF.Abs(across) >= minDistance) continue;

                    float side = across >= -1e-4f ? 1f : -1f;
                    other.YieldSteer += way.Perpendicular * (side * (1f - ahead / reach) * (1f - MathF.Abs(across) / minDistance));
                }
            }
        }

        /// <summary>
        /// Decision D30: true if `slower` makes way for `faster`: friendly ground units, the faster one at
        /// least YieldSpeedRatio times faster and at least as big. Big units (tanks, Titans) never make
        /// way for smaller ones; those go around them.
        /// </summary>
        private bool MakesWayFor(SimUnit slower, SimUnit faster)
        {
            return _rules.YieldSpeedRatio > 0f && slower != faster && slower.Stats.MoveSpeed > 0f && slower.Team == faster.Team && !slower.IsAir && !faster.IsAir && !slower.IsLeaping
                && faster.Radius >= slower.Radius && faster.MoveSpeedNow >= slower.MoveSpeedNow * _rules.YieldSpeedRatio;
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
        /// slides along the body instead. This keeps crowds from crushing together. Against a walking
        /// unit that is making way for it (D30), it keeps heading straight: a smaller one is simply
        /// shoved aside (a vehicle through infantry), a same-size one makes it slow down instead of
        /// sliding sideways while it steps aside.
        /// Uses the neighbor list filled by SteeringVelocity.
        /// </summary>
        private Vec2 RemovePushIntoContacts(SimUnit unit, Vec2 velocity, float padding)
        {
            foreach (SimUnit other in _neighbors)
            {
                if (other == unit || !CanTouch(unit, other)) continue;

                Vec2 toOther = other.Position - unit.Position;
                float gap = toOther.Length;
                if (gap < 1e-5f || gap >= unit.Radius + other.Radius + padding) continue;

                Vec2 normal = toOther / gap;
                float otherAway = Math.Max(0f, Vec2.Dot(other.Velocity, normal));
                float along = Vec2.Dot(velocity, normal);
                float into = along - otherAway;
                if (into <= 0f) continue;
                if (other.State == UnitState.Move && MakesWayFor(other, unit))
                {
                    if (other.Radius < unit.Radius) continue; // Shove a smaller body aside.
                    velocity *= otherAway / along; // Same size: slow down, keep heading.
                }
                else velocity -= normal * into;
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
                    if (other.Id <= unit.Id || !CanTouch(unit, other)) continue;

                    Vec2 offset = unit.Position - other.Position;
                    float gap = offset.Length;
                    float minDistance = unit.Radius + other.Radius;
                    if (gap >= minDistance) continue;

                    MarkContact(unit, other);
                    Vec2 normal = gap > 1e-5f ? offset / gap : PairDirection(unit, other);
                    float overlap = minDistance - gap;
                    PushShares(unit, other, out float unitShare, out float otherShare);
                    unit.Position += normal * (overlap * unitShare);
                    other.Position -= normal * (overlap * otherShare);
                }
            }
        }

        /// <summary>Records that two touching bodies belong to different squads (this breaks squad formations).</summary>
        private static void MarkContact(SimUnit a, SimUnit b)
        {
            if (a.SquadId == b.SquadId && a.SquadId >= 0) return;
            a.TouchedOtherSquad = true;
            b.TouchedOtherSquad = true;
        }

        /// <summary>
        /// How much of an overlap each body moves. A unit standing still (fighting or idle) is not
        /// pushed by a unit that is walking, so walkers slide around it. Two standing units move
        /// equally; between two walkers the smaller body gives way more (shares by body area), so a
        /// vehicle shoulders through infantry instead of being held by it (D30).
        /// </summary>
        private static void PushShares(SimUnit a, SimUnit b, out float aShare, out float bShare)
        {
            if (a.Stats.MoveSpeed <= 0f || b.Stats.MoveSpeed <= 0f) // Nothing moves a tower or an Archer pod.
            {
                aShare = a.Stats.MoveSpeed <= 0f ? 0f : 1f;
                bShare = b.Stats.MoveSpeed <= 0f ? 0f : 1f;
                return;
            }
            bool aAnchored = a.State != UnitState.Move;
            bool bAnchored = b.State != UnitState.Move;
            if (aAnchored && !bAnchored) { aShare = 0f; bShare = 1f; }
            else if (!aAnchored && bAnchored) { aShare = 1f; bShare = 0f; }
            else if (aAnchored) { aShare = 0.5f; bShare = 0.5f; }
            else
            {
                float aArea = a.Radius * a.Radius, bArea = b.Radius * b.Radius;
                aShare = bArea / (aArea + bArea);
                bShare = 1f - aShare;
            }
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

        /// <summary>Keeps the unit inside the arena. Returns true if it had to be moved back.</summary>
        private bool ClampToArena(SimUnit unit)
        {
            // Aircraft that can flank fly in the flank zones too, beyond the field's edges (D61). One landing there stays put, so this asks what it is, not whether it's up.
            float beyond = unit.Stats.CanFlank && unit.Stats.IsAir ? (_rules.FlankGapColumns + _rules.FlankZoneColumns) * _rules.SquareSize : 0f;
            float halfWidth = _rules.ArenaWidth * 0.5f + beyond - unit.Radius;
            float halfLength = _rules.ArenaLength * 0.5f - unit.Radius;
            float x = Math.Clamp(unit.Position.X, -halfWidth, halfWidth);
            float y = Math.Clamp(unit.Position.Y, -halfLength, halfLength);
            bool moved = x != unit.Position.X || y != unit.Position.Y;
            unit.Position = new Vec2(x, y);
            return moved;
        }
    }
}
