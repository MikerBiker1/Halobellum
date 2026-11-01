// MatchupTable.cs
// How often each unit type beats each other unit type in equal-value battles, as measured by the
// counter web (Halobellum > Headless Runner > Run Counter Web). The Hard AI uses it to pick real
// counters instead of only the Section 7 role table. It is general game knowledge (the same for
// every match), not information about the opponent's current army.
// Filled from Data/AI/MatchupTable.asset (MatchupTableDefinition); tests build their own.
// Depends on: Halobellum.Core (UnitStats).

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class MatchupTable
    {
        private readonly Dictionary<(string, string), float> _winRates = new Dictionary<(string, string), float>();

        public int Count => _winRates.Count;

        /// <summary>Records that attacker beats defender this share of the time (0 to 1).</summary>
        public void Set(string attacker, string defender, float winRate)
        {
            _winRates[(attacker, defender)] = winRate;
            _strength.Clear();
        }

        private readonly Dictionary<string, float> _strength = new Dictionary<string, float>();

        /// <summary>A unit's average win rate against every unit it was measured against (0.5 if unknown).</summary>
        public float OverallStrength(UnitStats unit)
        {
            if (unit == null) return 0.5f;
            if (_strength.TryGetValue(unit.Name, out float cached)) return cached;
            float sum = 0f;
            int count = 0;
            foreach (var pair in _winRates)
            {
                if (pair.Key.Item1 != unit.Name) continue;
                sum += pair.Value;
                count++;
            }
            float strength = count > 0 ? sum / count : 0.5f;
            _strength[unit.Name] = strength;
            return strength;
        }

        /// <summary>How often attacker beats defender (0 to 1), or null if the pair was never measured.</summary>
        public float? WinRate(UnitStats attacker, UnitStats defender)
        {
            if (attacker == null || defender == null) return null;
            if (attacker.Name == defender.Name) return 0.5f;
            return _winRates.TryGetValue((attacker.Name, defender.Name), out float rate) ? rate : (float?)null;
        }
    }
}
