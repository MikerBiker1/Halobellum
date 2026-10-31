// FactionDefinition.cs
// The data asset for one faction (Bible Section 8): its base roster and its sub-factions, each with
// its own exclusive units added on top of the base roster. Factions feed the starting draft.
// Until the real sub-factions are built, each faction has one temporary "Standard" sub-faction with
// no exclusive units (Bible Section 5). Create one with right-click > Create > Halobellum > Faction.
// Depends on: UnitDefinition, Halobellum.Economy (FactionInfo), Halobellum.Core, UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Faction", fileName = "NewFaction")]
    public sealed class FactionDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class SubFactionEntry
        {
            public string subFactionName;
            [Tooltip("Units only this sub-faction can use, on top of the base roster.")]
            public List<UnitDefinition> exclusiveUnits = new List<UnitDefinition>();
        }

        public string factionName;
        public Color color = Color.white;
        public List<UnitDefinition> baseRoster = new List<UnitDefinition>();
        public List<SubFactionEntry> subFactions = new List<SubFactionEntry>();

        /// <summary>
        /// Copies this faction into plain C# for the match. statsCache makes every unit asset turn into
        /// one shared UnitStats object, so the same unit is recognised everywhere in the match.
        /// </summary>
        public FactionInfo ToInfo(Dictionary<UnitDefinition, UnitStats> statsCache)
        {
            var info = new FactionInfo { Name = string.IsNullOrEmpty(factionName) ? name : factionName };
            foreach (UnitDefinition unit in baseRoster)
            {
                if (unit != null) info.BaseRoster.Add(StatsFor(unit, statsCache));
            }
            foreach (SubFactionEntry entry in subFactions)
            {
                var sub = new SubFactionInfo { Name = entry.subFactionName };
                foreach (UnitDefinition unit in entry.exclusiveUnits)
                {
                    if (unit != null) sub.ExclusiveUnits.Add(StatsFor(unit, statsCache));
                }
                info.SubFactions.Add(sub);
            }
            return info;
        }

        private static UnitStats StatsFor(UnitDefinition unit, Dictionary<UnitDefinition, UnitStats> cache)
        {
            if (!cache.TryGetValue(unit, out UnitStats stats))
            {
                stats = unit.ToStats();
                cache[unit] = stats;
            }
            return stats;
        }
    }
}
