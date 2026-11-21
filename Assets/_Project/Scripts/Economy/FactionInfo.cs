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
        /// <summary>One line for the draft card: what this sub-faction is about.</summary>
        public string Description = "";
        public List<UnitStats> ExclusiveUnits = new List<UnitStats>();
        /// <summary>Units of the base roster this sub-faction can't have. Only the tournament's trials set it today.</summary>
        public List<UnitStats> ExcludedUnits = new List<UnitStats>();
        /// <summary>Base roster units this sub-faction buys at its own price (the owner, D158: the Hunter Pair at 400 for the Lekgolo Colony).</summary>
        public List<UnitCost> UnitCosts = new List<UnitCost>();
        private Dictionary<UnitStats, UnitStats> _priced;

        /// <summary>
        /// The unit as this sub-faction buys it: a copy at its own price if it has one (always the same copy, so a roster keeps one type for it),
        /// otherwise the unit itself.
        /// </summary>
        public UnitStats PricedFor(UnitStats unit)
        {
            if (unit == null || UnitCosts == null || UnitCosts.Count == 0) return unit;
            foreach (UnitCost price in UnitCosts)
            {
                if (price == null || price.Unit != unit.Name) continue;
                lock (UnitCosts) // The tournament plays matches on many threads with the same faction data: one copy for all of them.
                {
                    _priced ??= new Dictionary<UnitStats, UnitStats>();
                    if (!_priced.TryGetValue(unit, out UnitStats copy))
                    {
                        copy = unit.CopyAtCost(price.Cost);
                        _priced[unit] = copy;
                    }
                    return copy;
                }
            }
            return unit;
        }
    }

    /// <summary>A sub-faction's own price for a base roster unit (D158).</summary>
    public sealed class UnitCost
    {
        public string Unit = "";
        public int Cost;
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
                if (subFaction.ExcludedUnits != null) foreach (UnitStats unit in subFaction.ExcludedUnits) roster.Remove(unit);
                for (int i = 0; i < roster.Count; i++) roster[i] = subFaction.PricedFor(roster[i]); // Its own prices (D158).
                foreach (UnitStats unit in subFaction.ExclusiveUnits)
                {
                    if (!roster.Contains(unit)) roster.Add(unit);
                }
            }
            return roster;
        }
    }
}
