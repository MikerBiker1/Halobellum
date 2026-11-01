// Cards.cs
// Spell and reinforcement cards (Bible Section 10, Milestone 7).
//   - Offers come between rounds, every round from MatchRules.FirstCardRound (2), alternating
//     spells (first) and reinforcements. A player takes 1 of up to 3 cards or skips for a bonus
//     (50 for spells, 100 for reinforcements).
//   - Spell offers draw from the faction's spells the player does not own the maximum of yet (fewer
//     than 3 eligible = fewer cards). A spell is paid once and kept (OwnedSpell); it recharges for
//     CooldownRounds rounds after each use.
//   - Reinforcement offers: per card a random unit type from the player's roster at a random level
//     (up to MatchRules.ReinforcementLevelCap), copied until its worth reaches the round's budget;
//     the player pays whatever the worth exceeds the budget (ReinforcementCard.Price).
// Depends on: MatchRules, PlayerState, Halobellum.Core (SpellStats, UnitStats, SimRandom, Vec2).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public enum CardKind
    {
        Spell = 0,
        Reinforcement = 1,
    }

    /// <summary>One reinforcement card: some squads of one unit type at one level, priced against the round's budget.</summary>
    public sealed class ReinforcementCard
    {
        public UnitStats Type;
        public int Level = 1;
        public int Squads = 1;
        public int Budget;

        /// <summary>One squad's worth: its base price plus half of it per level above 1 (Bible Section 10).</summary>
        public int SquadWorth => Type.Cost + Type.Cost * (Level - 1) / 2;
        public int TotalWorth => SquadWorth * Squads;
        /// <summary>Credits to pay: what the total worth exceeds the budget (0 when exactly on budget).</summary>
        public int Price => Math.Max(0, TotalWorth - Budget);

        /// <summary>Adds copies of the squad until the total worth reaches or passes the budget (a squad already over it stays one squad).</summary>
        public static ReinforcementCard Make(UnitStats type, int level, int budget)
        {
            var card = new ReinforcementCard { Type = type, Level = Math.Max(1, level), Budget = budget };
            if (card.SquadWorth > 0)
            {
                while (card.TotalWorth < budget) card.Squads++;
            }
            return card;
        }
    }

    /// <summary>The cards one player is offered at the start of a round.</summary>
    public sealed class CardOffer
    {
        public CardKind Kind;
        public int Round;
        public List<SpellStats> Spells = new List<SpellStats>();
        public List<ReinforcementCard> Reinforcements = new List<ReinforcementCard>();
        /// <summary>Credits for skipping this offer.</summary>
        public int SkipBonus;

        public int Count => Kind == CardKind.Spell ? Spells.Count : Reinforcements.Count;

        /// <summary>Credits taking card `index` costs.</summary>
        public int PriceOf(int index) => Kind == CardKind.Spell ? Spells[index].Cost : Reinforcements[index].Price;

        public string NameOf(int index) => Kind == CardKind.Spell ? Spells[index].Name : Reinforcements[index].Type.Name;
    }

    /// <summary>A spell a player owns: ready from ReadyRound, and placed (or not) for the current round.</summary>
    public sealed class OwnedSpell
    {
        public int Id;
        public SpellStats Stats;
        /// <summary>The first round it can be used again.</summary>
        public int ReadyRound;
        public bool IsPlaced;
        /// <summary>Where it is placed, in battlefield metres (only while IsPlaced).</summary>
        public Vec2 Position;

        public bool IsReady(int round) => ReadyRound <= round;
        /// <summary>Rounds until it can be used again (0 = ready).</summary>
        public int RoundsLeft(int round) => Math.Max(0, ReadyRound - round);
    }

    public static class CardDeck
    {
        /// <summary>Whether a round has an offer, and of which kind: spells in round 2, then alternating.</summary>
        public static bool HasOffer(MatchRules rules, int round) => rules.CardsEnabled && round >= rules.FirstCardRound;

        public static CardKind KindForRound(MatchRules rules, int round) =>
            (round - rules.FirstCardRound) % 2 == 0 ? CardKind.Spell : CardKind.Reinforcement;

        /// <summary>A fresh offer for this player and round, drawn with the match's random numbers.</summary>
        public static CardOffer Draw(PlayerState player, MatchRules rules, int round, SimRandom random)
        {
            var offer = new CardOffer { Round = round, Kind = KindForRound(rules, round) };
            if (offer.Kind == CardKind.Spell)
            {
                offer.SkipBonus = rules.SpellSkipBonus;
                var eligible = new List<SpellStats>();
                foreach (SpellStats spell in player.Faction?.Spells ?? new List<SpellStats>())
                {
                    if (spell != null && player.CopiesOwned(spell) < spell.MaxOwnedCopies && !eligible.Contains(spell)) eligible.Add(spell);
                }
                offer.Spells.AddRange(PickDistinct(eligible, rules.CardsPerOffer, random));
            }
            else
            {
                offer.SkipBonus = rules.ReinforcementSkipBonus;
                int budget = rules.ReinforcementBudget(round);
                int levelCap = rules.ReinforcementLevelCap(round);
                foreach (UnitStats type in PickDistinct(player.Roster, rules.CardsPerOffer, random))
                {
                    offer.Reinforcements.Add(ReinforcementCard.Make(type, random.RangeInt(1, levelCap + 1), budget));
                }
            }
            return offer;
        }

        /// <summary>Up to `count` different items in random order.</summary>
        private static List<T> PickDistinct<T>(List<T> items, int count, SimRandom random)
        {
            var pool = new List<T>(items);
            var picked = new List<T>();
            while (picked.Count < count && pool.Count > 0)
            {
                int index = random.RangeInt(0, pool.Count);
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked;
        }
    }
}
