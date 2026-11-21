// SquadDamageLeaders.cs
// Which squad of each side has dealt the most damage so far in a battle, counted as the Battle stats "Dealt" column counts it
// (BattleStats.Finish): a squad's own bodies, the bodies it dropped, released or raised on its side, and its seeds. Towers, spells
// and squads made during the battle never lead. The Match scene's glow under the top squads (TopSquadGlow) reads it.
// Looks only: it reads the simulation and never changes it.
// Depends on: Halobellum.Combat (BattleSimulation, SimUnit, SimSquad), Halobellum.Core (Team).

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class SquadDamageLeaders
    {
        private BattleSimulation _battle;
        private readonly Dictionary<int, int> _indexById = new Dictionary<int, int>();
        private float[] _dealt = Array.Empty<float>();
        private int _placed;

        /// <summary>Index into battle.Squads (= placed order, Team A first) of Team A's leader; -1 while none of its squads has dealt damage.</summary>
        public int TopA { get; private set; } = -1;
        /// <summary>Index into battle.Squads of Team B's leader; -1 while none of its squads has dealt damage.</summary>
        public int TopB { get; private set; } = -1;

        /// <summary>What the squad at this index of battle.Squads has dealt so far (0 for squads made during the battle).</summary>
        public float DealtBy(int index) => index >= 0 && index < _placed ? _dealt[index] : 0f;

        public void Reset()
        {
            _battle = null;
            _placed = 0;
            _indexById.Clear();
            TopA = TopB = -1;
        }

        /// <summary>Counts again. placedSquads is how many of battle.Squads were placed (both sides); the rest were made in the battle.</summary>
        public void Count(BattleSimulation battle, int placedSquads)
        {
            if (battle == null)
            {
                Reset();
                return;
            }
            if (battle != _battle)
            {
                Reset();
                _battle = battle;
                _placed = Math.Max(0, Math.Min(placedSquads, battle.Squads.Count));
                if (_dealt.Length < _placed) _dealt = new float[_placed];
                for (int i = 0; i < _placed; i++) _indexById[battle.Squads[i].Id] = i;
            }
            Array.Clear(_dealt, 0, _placed);
            IReadOnlyList<SimUnit> units = battle.Units;
            for (int u = 0; u < units.Count; u++)
            {
                SimUnit unit = units[u];
                if (unit.CreditSquadId < 0 || !_indexById.TryGetValue(unit.CreditSquadId, out int i)) continue;
                if (battle.Squads[i].Team != unit.Team) continue; // A body risen for the other side counts for nobody (BattleStats.Finish).
                _dealt[i] += unit.DamageDealt + unit.SeedDamageDealt;
            }
            TopA = Pick(Team.A, TopA);
            TopB = Pick(Team.B, TopB);
        }

        private int Pick(Team team, int current)
        {
            // The squad already shown keeps the lead on a tie, so the glow does not flicker.
            int best = current >= 0 && current < _placed && _dealt[current] > 0f ? current : -1;
            for (int i = 0; i < _placed; i++)
            {
                if (_battle.Squads[i].Team != team || _dealt[i] <= 0f) continue;
                if (best < 0 || _dealt[i] > _dealt[best]) best = i;
            }
            return best;
        }
    }
}
