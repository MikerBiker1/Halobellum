// SimSpell.cs
// One spell cast into a battle (Bible Section 10, Milestone 7): which spell, whose it is and where it
// lands. BattleSimulation applies it at its delay (see SpellStats for the effects) and records the
// damage and kills it dealt, so results and "what killed what" can list it next to the units.
// Depends on: Halobellum.Core (SpellStats, Team, Vec2).

using Halobellum.Core;

namespace Halobellum.Combat
{
    public sealed class SimSpell
    {
        public readonly int Id;
        public readonly SpellStats Stats;
        public readonly Team Team;
        public readonly Vec2 Position;

        /// <summary>Effects applied so far (damage ticks, or 1 once a buff or drop has happened).</summary>
        public int TicksDone;
        public float DamageDealt;
        public int Kills;
        /// <summary>Squad id of the dropped squad(s) for drop spells, -1 before the drop.</summary>
        public int DroppedSquadId = -1;

        public SimSpell(int id, SpellStats stats, Team team, Vec2 position)
        {
            Id = id;
            Stats = stats;
            Team = team;
            Position = position;
        }

        /// <summary>True once every effect of the spell has happened.</summary>
        public bool IsDone => TicksDone >= (Stats.Effect == SpellEffect.Damage ? System.Math.Max(1, Stats.Ticks) : 1);
    }
}
