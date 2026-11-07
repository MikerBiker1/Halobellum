// SimSpell.cs
// One spell cast into a battle (Bible Section 10, Milestone 7): which spell, whose it is and where it
// lands. BattleSimulation applies it at its delay (see SpellStats for the effects) and records the
// damage and kills it dealt, so results and "what killed what" can list it next to the units.
// Depends on: Halobellum.Core (SpellStats, Team, Vec2).

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public sealed class SimSpell
    {
        public readonly int Id;
        public readonly SpellStats Stats;
        public readonly Team Team;
        public readonly Vec2 Position;
        /// <summary>The direction of a line-shaped area (see SpellStats).</summary>
        public readonly float AngleDegrees;

        /// <summary>Effects applied so far (damage ticks, or 1 once a buff or drop has happened).</summary>
        public int TicksDone;
        public float DamageDealt;
        public int Kills;
        /// <summary>Squad id of the dropped squad(s) for drop spells, -1 before the drop.</summary>
        public int DroppedSquadId = -1;
        /// <summary>Where the latest tick landed (D83): a scattered spell's strikes, or a sweeping beam's spot. Empty for a spell that hits its whole area.</summary>
        public readonly List<Vec2> Strikes = new List<Vec2>();
        /// <summary>A scattered spell's own dice, from the battle's seed and its id, so the same battle lands the same strikes.</summary>
        public SimRandom Dice;

        public SimSpell(int id, SpellStats stats, Team team, Vec2 position, float angleDegrees = 0f)
        {
            AngleDegrees = angleDegrees;
            Id = id;
            Stats = stats;
            Team = team;
            Position = position;
        }

        /// <summary>True once every effect of the spell has happened.</summary>
        public bool IsDone => TicksDone >= (Stats.Effect == SpellEffect.Damage ? System.Math.Max(1, Stats.Ticks) : 1);

        /// <summary>True once it has nothing left to do at this battle time (smoke hangs around after it starts, by default until the battle ends).</summary>
        public bool IsOverAt(float seconds) => IsDone && (!Stats.Lingers || (Stats.DurationSeconds > 0f && seconds >= Stats.DelaySeconds + Stats.DurationSeconds));

        /// <summary>True if a body at this position touches the spell's area.</summary>
        public bool Covers(SimUnit unit) => Stats.Covers(unit.Position - Position, unit.Radius, AngleDegrees);

        /// <summary>Where a sweeping beam is, this far along its course (0 to 1).</summary>
        public Vec2 BeamAt(float along) => Stats.BeamPoint(Position, AngleDegrees, along);

        /// <summary>How far along its course a sweeping beam is at this battle time, between its first hit and its last.</summary>
        public float BeamAlongAt(float seconds)
        {
            float span = (Stats.Ticks - 1) * Stats.TickInterval;
            return span > 0f ? (seconds - Stats.DelaySeconds) / span : 0.5f;
        }
    }
}
