// MatchupTableDefinition.cs
// The data asset holding the measured unit-vs-unit win rates (Data/AI/MatchupTable.asset) that the
// Hard AI uses to pick counters. Do not edit it by hand: Halobellum > Headless Runner > Run Counter
// Web rewrites it after every run, so it always matches the current unit data.
// ToTable() copies it into plain C# MatchupTable.
// Depends on: Halobellum.Economy (MatchupTable), UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Matchup Table", fileName = "MatchupTable")]
    public sealed class MatchupTableDefinition : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/_Project/Data/AI/MatchupTable.asset";

        [Serializable]
        public sealed class Entry
        {
            public string attacker;
            public string defender;
            [Range(0f, 1f)] public float winRate;
        }

        [Tooltip("When and how the table was measured.")]
        public string source;
        public List<Entry> entries = new List<Entry>();

        public MatchupTable ToTable()
        {
            var table = new MatchupTable();
            foreach (Entry entry in entries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.attacker)) table.Set(entry.attacker, entry.defender, entry.winRate);
            }
            return table;
        }
    }
}
