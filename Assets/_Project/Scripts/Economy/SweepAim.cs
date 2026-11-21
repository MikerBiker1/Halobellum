// SweepAim.cs
// Where the AI aims a sweeping beam (the Glassing Beam, D144). The beam starts DelaySeconds into the battle and
// runs along its line over its ticks, so aiming where the enemy was deployed misses: by then both armies have
// walked toward each other (the owner, 2026-11-19: "The AI doesn't predict the movements of units when using the
// glassing beam, it should."). So it forecasts each squad's centre at every tick of the beam, the way the battle
// moves it: it walks toward the closest enemy squad one of its weapons can hit until that squad is in range, or
// straight ahead when there is none. It then tries lines that put a tick right on a forecast enemy squad, in 16
// directions, and keeps the one whose ticks land on the most enemy value minus the caster's own value (the beam
// burns its own troops too, D33). No line worth more than nothing: it holds the beam.
// Depends on: SpellStats, UnitStats, WeaponStats, Vec2.

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public static class SweepAim
    {
        /// <summary>One squad on the field before the battle, as the caster sees it.</summary>
        public struct Squad
        {
            public Vec2 Position;
            public UnitStats Type;
            public float Value;
            /// <summary>True for the caster's enemy.</summary>
            public bool Enemy;
            /// <summary>The way toward the other side's edge (its walk when nothing is in reach).</summary>
            public Vec2 Forward;
        }

        private const float StepSeconds = 0.25f;
        /// <summary>A squad's bodies stand around its centre: a tick this close still catches some (as RuleBasedAi.DensestSpot).</summary>
        private const float SpreadMetres = 3f;
        private const int Directions = 16;

        /// <summary>
        /// The best line for this sweeping spell against these squads: its centre and direction, as Match.PlaceSpell takes
        /// them. canAimAt says whether a centre may be used. False when no line hits more enemy than own value.
        /// </summary>
        public static bool Aim(SpellStats spell, IReadOnlyList<Squad> squads, Func<Vec2, bool> canAimAt, out Vec2 centre, out float angleDegrees)
        {
            centre = default;
            angleDegrees = 0f;
            int ticks = Math.Max(1, spell.Ticks);
            Vec2[][] at = Forecast(squads, spell, ticks);
            var along = new float[ticks];
            for (int k = 0; k < ticks; k++) along[k] = spell.BeamAlong(k + 1);

            float best = 0f;
            for (int d = 0; d < Directions; d++)
            {
                float angle = d * 360f / Directions;
                Vec2 direction = Vec2.Right.Rotated(angle);
                for (int i = 0; i < squads.Count; i++)
                {
                    if (!squads[i].Enemy || !spell.Targets.Allows(squads[i].Type.IsAir)) continue;
                    for (int k = 0; k < ticks; k++)
                    {
                        // The line whose k-th tick lands on this squad where it will be then.
                        Vec2 candidate = at[k][i] - direction * (spell.AreaWidth * (along[k] - 0.5f));
                        float score = Score(spell, squads, at, along, candidate, direction);
                        if (score <= best || !canAimAt(candidate)) continue;
                        best = score;
                        centre = candidate;
                        angleDegrees = angle;
                    }
                }
            }
            return best > 0f;
        }

        /// <summary>What a line's ticks land on: enemy value minus the caster's own, counted once per tick that catches a squad.</summary>
        private static float Score(SpellStats spell, IReadOnlyList<Squad> squads, Vec2[][] at, float[] along, Vec2 centre, Vec2 direction)
        {
            float reach = spell.Radius + SpreadMetres;
            float reachSquared = reach * reach;
            float score = 0f;
            for (int k = 0; k < along.Length; k++)
            {
                Vec2 spot = centre + direction * (spell.AreaWidth * (along[k] - 0.5f));
                for (int i = 0; i < squads.Count; i++)
                {
                    if (!spell.Targets.Allows(squads[i].Type.IsAir) || Vec2.DistanceSquared(spot, at[k][i]) > reachSquared) continue;
                    score += squads[i].Enemy ? squads[i].Value : -squads[i].Value;
                }
            }
            return score;
        }

        /// <summary>Each squad's forecast centre at each tick of the beam (tick k at DelaySeconds + k * TickInterval).</summary>
        public static Vec2[][] Forecast(IReadOnlyList<Squad> squads, SpellStats spell, int ticks)
        {
            int n = squads.Count;
            var now = new Vec2[n];
            var next = new Vec2[n];
            for (int i = 0; i < n; i++) now[i] = squads[i].Position;
            var at = new Vec2[ticks][];
            float time = 0f;
            for (int k = 0; k < ticks; k++)
            {
                float until = spell.DelaySeconds + k * spell.TickInterval;
                while (time < until - 1e-4f)
                {
                    float dt = Math.Min(StepSeconds, until - time);
                    for (int i = 0; i < n; i++) next[i] = Step(squads, now, i, dt);
                    (now, next) = (next, now);
                    time += dt;
                }
                at[k] = (Vec2[])now.Clone();
            }
            return at;
        }

        /// <summary>One squad's centre after dt seconds: toward the closest enemy squad it can hit, stopping once that squad is in range.</summary>
        private static Vec2 Step(IReadOnlyList<Squad> squads, Vec2[] now, int i, float dt)
        {
            UnitStats type = squads[i].Type;
            float speed = type.MoveSpeed * dt;
            if (speed <= 0f) return now[i];
            float closest = float.MaxValue, range = 0f;
            int target = -1;
            for (int j = 0; j < squads.Count; j++)
            {
                if (squads[j].Enemy == squads[i].Enemy) continue;
                float reach = Reach(type, squads[j].Type.IsAir);
                if (reach < 0f) continue;
                float distance = Vec2.Distance(now[i], now[j]);
                if (distance < closest)
                {
                    closest = distance;
                    range = reach;
                    target = j;
                }
            }
            if (target < 0) return now[i] + squads[i].Forward * speed;
            if (closest <= range) return now[i];
            return now[i] + (now[target] - now[i]).Normalized * Math.Min(speed, closest - range);
        }

        /// <summary>The longest range of this unit's weapons that can hit a unit in (or not in) the air, or -1 if none can.</summary>
        private static float Reach(UnitStats type, bool targetIsAir)
        {
            float reach = -1f;
            foreach (WeaponStats weapon in type.Weapons)
            {
                if (weapon != null && weapon.CanTarget(type, targetIsAir)) reach = Math.Max(reach, weapon.Range);
            }
            return reach;
        }
    }
}
