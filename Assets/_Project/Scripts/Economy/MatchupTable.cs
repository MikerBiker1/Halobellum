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
        private readonly Dictionary<(string, string), float> _margins = new Dictionary<(string, string), float>();

        /// <summary>
        /// How much a win's margin counts against its rate in Score (D154, the owner: "This is a much better counter than this unit even if both
        /// are 100% winrate against it"). Half and half: a sure win with the army nearly whole is 1, a sure win by the last body about 0.75.
        /// </summary>
        public const float MarginWeight = 0.5f;

        public int Count => _winRates.Count;

        /// <summary>Records that attacker beats defender this share of the time (0 to 1).</summary>
        public void Set(string attacker, string defender, float winRate)
        {
            _winRates[(attacker, defender)] = winRate;
            _strength.Clear();
        }

        private readonly Dictionary<string, float> _strength = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _scoreStrength = new Dictionary<string, float>();

        /// <summary>Records by how much attacker beats defender, from -1 to 1: the share of its army left less the defender's (the counter web's margin, D154).</summary>
        public void SetMargin(string attacker, string defender, float margin)
        {
            _margins[(attacker, defender)] = margin;
            _scoreStrength.Clear();
        }

        public int MarginCount => _margins.Count;

        /// <summary>By how much attacker beats defender (-1 to 1), or null if not measured.</summary>
        public float? Margin(UnitStats attacker, UnitStats defender)
        {
            if (attacker == null || defender == null) return null;
            if (attacker.Name == defender.Name) return 0f;
            return _margins.TryGetValue((attacker.Name, defender.Name), out float margin) ? margin : (float?)null;
        }

        /// <summary>
        /// The win rate with the margin read in (D154), 0 to 1: (1 - MarginWeight) of the rate and MarginWeight of the margin moved onto 0 to 1.
        /// Two sure wins are told apart by how much of the army each keeps. The rate alone if the pair has no margin; null if never measured.
        /// </summary>
        public float? Score(UnitStats attacker, UnitStats defender)
        {
            float? rate = WinRate(attacker, defender);
            if (!rate.HasValue) return null;
            float? margin = Margin(attacker, defender);
            if (!margin.HasValue) return rate;
            return (1f - MarginWeight) * rate.Value + MarginWeight * 0.5f * (1f + margin.Value);
        }

        /// <summary>A unit's average Score against every unit it was measured against (0.5 if unknown): how much it helps against anything (D154).</summary>
        public float OverallScore(UnitStats unit)
        {
            if (unit == null) return 0.5f;
            if (_scoreStrength.TryGetValue(unit.Name, out float cached)) return cached;
            float sum = 0f;
            int count = 0;
            foreach (var pair in _winRates)
            {
                if (pair.Key.Item1 != unit.Name) continue;
                float rate = pair.Value;
                sum += _margins.TryGetValue(pair.Key, out float margin) ? (1f - MarginWeight) * rate + MarginWeight * 0.5f * (1f + margin) : rate;
                count++;
            }
            float strength = count > 0 ? sum / count : 0.5f;
            _scoreStrength[unit.Name] = strength;
            return strength;
        }

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
