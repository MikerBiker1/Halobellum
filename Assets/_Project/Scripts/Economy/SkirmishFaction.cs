// SkirmishFaction.cs
// Unlock Skirmish has no factions, so both sides play one made-up faction that holds everything: every unit a real faction can
// unlock (base rosters and sub-faction exclusives) and every card, each once. A plain joining of the lists would offer a card
// the factions share three times over and defeat its limit on copies. ODST stays out, since it only ever lands from its spell.
// Its one sub-faction has no units of its own: it is what the starting cards show on their ribbon.
// The name never reaches BattleFactions: the land and each unit's looks come from the real factions (MatchController).
// Depends on: FactionInfo, ShopOrder, Halobellum.Core (UnitStats, CardStats).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public static class SkirmishFaction
    {
        public const string Name = "Skirmish", CardName = "Starting squads";

        /// <summary>The one faction of an Unlock Skirmish match, built from the real ones. Its lists don't depend on their order.</summary>
        public static FactionInfo Build(IReadOnlyList<FactionInfo> factions)
        {
            var units = new Dictionary<string, UnitStats>();
            var cards = new Dictionary<string, CardStats>();
            foreach (FactionInfo faction in factions)
            {
                AddUnits(units, faction.BaseRoster);
                foreach (SubFactionInfo subFaction in faction.SubFactions) AddUnits(units, subFaction.ExclusiveUnits);
                foreach (CardStats card in faction.Cards)
                {
                    if (card != null && !cards.ContainsKey(card.Name)) cards[card.Name] = card;
                }
            }

            var roster = new List<UnitStats>(units.Values);
            roster.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            ShopOrder.Sort(roster);
            var deck = new List<CardStats>(cards.Values);
            deck.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return new FactionInfo
            {
                Name = Name, BaseRoster = roster, Cards = deck,
                SubFactions = new List<SubFactionInfo> { new SubFactionInfo { Name = CardName } },
            };
        }

        private static void AddUnits(Dictionary<string, UnitStats> units, List<UnitStats> list)
        {
            foreach (UnitStats unit in list)
            {
                if (unit != null && !units.ContainsKey(unit.Name)) units[unit.Name] = unit;
            }
        }
    }
}
