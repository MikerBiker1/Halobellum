// FactionInfo.cs
// Plain C# description of a faction for the match: its base roster and its sub-factions, each of
// which adds its own exclusive units on top of the base roster (Bible Section 8.4). Built from the
// Faction data assets (Data/Factions) by FactionDefinition.ToInfo().
// Depends on: Halobellum.Core (UnitStats).

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class SubFactionInfo
    {
        public string Name = "";
        public List<UnitStats> ExclusiveUnits = new List<UnitStats>();
    }

    public sealed class FactionInfo
    {
        public string Name = "";
        public List<UnitStats> BaseRoster = new List<UnitStats>();
        public List<SubFactionInfo> SubFactions = new List<SubFactionInfo>();
        /// <summary>The spells, items, economy cards and buffs this faction can be offered (Bible Section 10).</summary>
        public List<CardStats> Cards = new List<CardStats>();

        /// <summary>Every unit type a player of this faction and sub-faction can unlock: base roster plus exclusives.</summary>
        public List<UnitStats> RosterFor(SubFactionInfo subFaction)
        {
            var roster = new List<UnitStats>(BaseRoster);
            if (subFaction != null)
            {
                foreach (UnitStats unit in subFaction.ExclusiveUnits)
                {
                    if (!roster.Contains(unit)) roster.Add(unit);
                }
            }
            return roster;
        }
    }
}
