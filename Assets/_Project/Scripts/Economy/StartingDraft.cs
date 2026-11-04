// StartingDraft.cs
// The starting draft (Bible Section 5): before round 1 each player is offered up to 5 cards. Each
// card shows a faction, a sub-faction and 2 random Tier 1 squads from that sub-faction's roster
// (base roster plus exclusive units), worth about the same on every card (D58). The same sub-faction never appears twice in one draw, and with the
// game's 8 sub-factions a faction may not appear at all.
// Picking a card fixes the faction and sub-faction for the match; the 2 squads are free and their
// unit types start unlocked. Draws use the match's seeded random generator.
// In Unlock Skirmish there is one made-up faction (SkirmishFaction), so every card shows it, and what differs is the squads:
// a fair pair from all its Tier 1 units, the same pair not twice in a draw if that can be helped.
// Depends on: FactionInfo, MatchRules, Halobellum.Core (UnitStats, SimRandom).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class DraftCard
    {
        public FactionInfo Faction;
        public SubFactionInfo SubFaction;
        public List<UnitStats> Squads = new List<UnitStats>();

        /// <summary>What its squads are worth together.</summary>
        public int Value
        {
            get
            {
                int value = 0;
                foreach (UnitStats squad in Squads) value += squad.Cost;
                return value;
            }
        }

        public override string ToString() => $"{Faction.Name} / {SubFaction.Name}: {string.Join(", ", Squads.ConvertAll(s => s.Name))}";
    }

    public static class StartingDraft
    {
        /// <summary>Draws one player's draft cards.</summary>
        public static List<DraftCard> Draw(IReadOnlyList<FactionInfo> factions, MatchRules rules, SimRandom random)
        {
            if (rules.Mode == MatchMode.UnlockSkirmish) return DrawSkirmish(factions, rules, random);

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
                List<UnitStats> pool = TierOneUnits(card.Faction, card.SubFaction);
                card.Squads = rules.DraftSquadsPerCard == 2 ? PickFairPair(pool, rules, random) : PickSquads(pool, rules.DraftSquadsPerCard, random);
                cards.Add(card);
            }
            return cards;
        }

        /// <summary>Unlock Skirmish's cards: all of the one faction, each with its own fair pair of squads.</summary>
        private static List<DraftCard> DrawSkirmish(IReadOnlyList<FactionInfo> factions, MatchRules rules, SimRandom random)
        {
            var cards = new List<DraftCard>();
            if (factions.Count == 0 || factions[0].SubFactions.Count == 0) return cards;
            FactionInfo faction = factions[0];
            SubFactionInfo subFaction = faction.SubFactions[0];
            List<UnitStats> pool = TierOneUnits(faction, subFaction);
            if (pool.Count == 0) return cards;

            while (cards.Count < rules.DraftCardCount)
            {
                List<UnitStats> squads = PickFairPair(pool, rules, random);
                for (int retry = 0; retry < MaxPairRetries && HasPair(cards, squads); retry++) squads = PickFairPair(pool, rules, random);
                cards.Add(new DraftCard { Faction = faction, SubFaction = subFaction, Squads = squads });
            }
            return cards;
        }

        /// <summary>How often a draw tries again for a pair no other card has. A small pool may not have enough.</summary>
        private const int MaxPairRetries = 20;

        private static bool HasPair(List<DraftCard> cards, List<UnitStats> squads)
        {
            foreach (DraftCard card in cards)
            {
                if (card.Squads.Contains(squads[0]) && card.Squads.Contains(squads[1])) return true;
            }
            return false;
        }

        public static List<UnitStats> TierOneUnits(FactionInfo faction, SubFactionInfo subFaction)
        {
            return faction.RosterFor(subFaction).FindAll(unit => unit.Tier == 1);
        }

        /// <summary>
        /// Two different unit types worth about the card's value (the owner: no card of 200 next to a card of 400): a random pair within
        /// the tolerance, or, in a roster that has none, the pair closest to it.
        /// </summary>
        private static List<UnitStats> PickFairPair(List<UnitStats> pool, MatchRules rules, SimRandom random)
        {
            if (pool.Count < 2) return PickSquads(pool, 2, random);
            var fair = new List<(UnitStats, UnitStats)>();
            int closest = int.MaxValue;
            for (int a = 0; a < pool.Count; a++)
            {
                for (int b = a + 1; b < pool.Count; b++)
                {
                    int off = Math.Max(0, Math.Abs(pool[a].Cost + pool[b].Cost - rules.DraftCardValue) - rules.DraftCardValueTolerance);
                    if (off > closest) continue;
                    if (off < closest) fair.Clear();
                    closest = off;
                    fair.Add((pool[a], pool[b]));
                }
            }
            (UnitStats first, UnitStats second) = fair[(int)(random.NextFloat() * fair.Count)];
            return new List<UnitStats> { first, second };
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
