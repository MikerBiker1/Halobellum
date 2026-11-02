// StartingDraft.cs
// The starting draft (Bible Section 5): before round 1 each player is offered up to 5 cards. Each
// card shows a faction, a sub-faction and 2 random Tier 1 squads from that sub-faction's roster
// (base roster plus exclusive units). The same sub-faction never appears twice in one draw, and with the
// game's 8 sub-factions a faction may not appear at all.
// Picking a card fixes the faction and sub-faction for the match; the 2 squads are free and their
// unit types start unlocked. Draws use the match's seeded random generator.
// Depends on: FactionInfo, MatchRules, Halobellum.Core (UnitStats, SimRandom).

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class DraftCard
    {
        public FactionInfo Faction;
        public SubFactionInfo SubFaction;
        public List<UnitStats> Squads = new List<UnitStats>();

        public override string ToString() => $"{Faction.Name} / {SubFaction.Name}: {string.Join(", ", Squads.ConvertAll(s => s.Name))}";
    }

    public static class StartingDraft
    {
        /// <summary>Draws one player's draft cards.</summary>
        public static List<DraftCard> Draw(IReadOnlyList<FactionInfo> factions, MatchRules rules, SimRandom random)
        {
            // Every (faction, sub-faction) pair that has at least one Tier 1 unit can appear once.
            var options = new List<DraftCard>();
            foreach (FactionInfo faction in factions)
            {
                foreach (SubFactionInfo subFaction in faction.SubFactions)
                {
                    if (TierOneUnits(faction, subFaction).Count > 0) options.Add(new DraftCard { Faction = faction, SubFaction = subFaction });
                }
            }

            var cards = new List<DraftCard>();
            while (cards.Count < rules.DraftCardCount && options.Count > 0)
            {
                int pick = (int)(random.NextFloat() * options.Count);
                DraftCard card = options[pick];
                options.RemoveAt(pick);
                card.Squads = PickSquads(TierOneUnits(card.Faction, card.SubFaction), rules.DraftSquadsPerCard, random);
                cards.Add(card);
            }
            return cards;
        }

        public static List<UnitStats> TierOneUnits(FactionInfo faction, SubFactionInfo subFaction)
        {
            return faction.RosterFor(subFaction).FindAll(unit => unit.Tier == 1);
        }

        /// <summary>Random squads, all different unit types while there are enough types to choose from.</summary>
        private static List<UnitStats> PickSquads(List<UnitStats> pool, int count, SimRandom random)
        {
            var picks = new List<UnitStats>();
            var remaining = new List<UnitStats>(pool);
            for (int i = 0; i < count; i++)
            {
                if (remaining.Count == 0) remaining.AddRange(pool); // Not enough types: allow repeats.
                int index = (int)(random.NextFloat() * remaining.Count);
                picks.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            return picks;
        }
    }
}
