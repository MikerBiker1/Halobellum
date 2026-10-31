// SimSquad.cs
// Formation cohesion for one squad during a battle. A squad of several bodies starts "intact": its
// bodies advance together, each keeping its place in the formation (its offset from the squad's
// centre) instead of every body running straight at its own closest enemy. The formation breaks for
// the rest of the battle as soon as ANY of these happens (per the project owner):
//   - its bodies' closest enemies belong to different enemy squads (they would split up),
//   - one of its bodies touches a body of another squad (collision),
//   - one of its bodies engages in combat (attacks, fires, or is hit).
// After that, its bodies move and fight individually. Single-body squads are never in formation.
// The formation keeps its orientation; it moves without rotating.
// Depends on: SimUnit, Halobellum.Core (Vec2, Team).

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public sealed class SimSquad
    {
        public readonly int Id;
        public readonly Team Team;
        public readonly List<SimUnit> Members = new List<SimUnit>();

        /// <summary>Each member's place in the formation, relative to the squad's centre (same order as Members).</summary>
        public readonly List<Vec2> Offsets = new List<Vec2>();

        /// <summary>True while the squad still moves in formation.</summary>
        public bool IsIntact { get; private set; }

        /// <summary>Set when a member fires or is hit; breaks the formation at the next tick.</summary>
        public bool Engaged;

        /// <summary>Why the formation broke, for tests and debugging (empty while intact).</summary>
        public string BreakReason { get; private set; } = "";

        public SimSquad(int id, Team team)
        {
            Id = id;
            Team = team;
        }

        /// <summary>Records every member's offset from the squad centre. Called once, when the battle starts.</summary>
        public void FormUp()
        {
            Offsets.Clear();
            Vec2 centre = Vec2.Zero;
            foreach (SimUnit member in Members) centre += member.Position;
            if (Members.Count > 0) centre /= Members.Count;
            foreach (SimUnit member in Members) Offsets.Add(member.Position - centre);
            IsIntact = Members.Count > 1;
            BreakReason = IsIntact ? "" : "single body";
        }

        public void Break(string reason)
        {
            if (!IsIntact) return;
            IsIntact = false;
            BreakReason = reason;
        }

        /// <summary>
        /// The squad's centre worked out from where its living members actually are (each member's
        /// position minus its formation offset, averaged), so the formation follows its bodies.
        /// </summary>
        public Vec2 Anchor()
        {
            Vec2 sum = Vec2.Zero;
            int alive = 0;
            for (int i = 0; i < Members.Count; i++)
            {
                if (!Members[i].IsAlive) continue;
                sum += Members[i].Position - Offsets[i];
                alive++;
            }
            return alive > 0 ? sum / alive : Vec2.Zero;
        }
    }
}
