// MatchRules.cs
// The match rules from Bible Section 5 (starting credits, income and its cap, base health, squads
// and unlocks per round, sale refunds, starting draft size). The values come from the Match Rules
// data asset (Data/MatchRules.asset) through MatchRulesDefinition.ToRules(); tests build their own.
// Depends on: nothing (plain C#).

using System;

namespace Halobellum.Economy
{
    public sealed class MatchRules
    {
        /// <summary>Credits each player has at the start of round 1 (Bible: 200).</summary>
        public int StartingCredits;
        /// <summary>Income for round 1, the same as the starting credits (decision D29: 200). Later rounds add IncomeIncreasePerRound each.</summary>
        public int BaseIncome;
        /// <summary>Extra income per round after the first (Bible: 200).</summary>
        public int IncomeIncreasePerRound;
        /// <summary>Income per round never exceeds this (Bible: 1600). Banked credits have no limit.</summary>
        public int IncomeCap;
        /// <summary>Each base's starting health (Bible: 2000).</summary>
        public int BaseHealth;
        /// <summary>New squads a player may buy per round (Bible: 2).</summary>
        public int NewSquadsPerRound;
        /// <summary>Unit unlocks a player may make per round (Bible: 1).</summary>
        public int UnlocksPerRound;
        /// <summary>Share of the price refunded when selling a squad before the battle of the round it was bought (Bible: 100%).</summary>
        public float RefundBeforeBattle;
        /// <summary>Share of the price refunded when selling a squad after that (Bible: 50%). Free draft squads always sell for this share of their cost.</summary>
        public float RefundAfterBattle;
        /// <summary>Cards in the starting draft (Bible: 5), fewer if there are not enough sub-factions.</summary>
        public int DraftCardCount;
        /// <summary>Free Tier 1 squads on each draft card (Bible: 2).</summary>
        public int DraftSquadsPerCard;
        /// <summary>
        /// Size of the XP bar as a share of the squad's base price (decision D26: 2 = a squad must kill twice
        /// its price's worth of enemies to fill it). The same at every level (Bible Section 5).
        /// </summary>
        public float XpBarPerSquadCost = 2f;
        /// <summary>Level-up price as a share of the squad's base price (Bible: half).</summary>
        public float LevelUpCostShare = 0.5f;

        // Cards (Bible Section 10, Milestone 7).
        /// <summary>First round with a card offer (Bible: 2); offers then alternate spells / reinforcements. 0 = no cards.</summary>
        public int FirstCardRound = 2;
        /// <summary>Cards per offer (Bible: 3).</summary>
        public int CardsPerOffer = 3;
        /// <summary>Credits for skipping a spell offer (Bible: 50).</summary>
        public int SpellSkipBonus = 50;
        /// <summary>Credits for skipping a reinforcement offer (Bible: 100).</summary>
        public int ReinforcementSkipBonus = 100;
        /// <summary>Reinforcement budget: base plus this much per round number (Bible Section 18: 200 + 50 per round).</summary>
        public int ReinforcementBudgetBase = 200;
        public int ReinforcementBudgetPerRound = 50;
        /// <summary>Highest level a reinforcement card can have (decision D32).</summary>
        public int ReinforcementMaxLevel = 3;
        /// <summary>The possible reinforcement level goes up by 1 every this many rounds: level 1 in rounds 1-4, up to 2 in 5-8 ... (D32).</summary>
        public int ReinforcementLevelEveryRounds = 4;

        public bool CardsEnabled => FirstCardRound > 0;

        /// <summary>The reinforcement budget of a round.</summary>
        public int ReinforcementBudget(int round) => ReinforcementBudgetBase + ReinforcementBudgetPerRound * round;

        /// <summary>The highest level a reinforcement card can have in this round.</summary>
        public int ReinforcementLevelCap(int round) =>
            Math.Max(1, Math.Min(ReinforcementMaxLevel, 1 + (round - 1) / Math.Max(1, ReinforcementLevelEveryRounds)));

        /// <summary>
        /// Income for a round: BaseIncome in round 1, then IncomeIncreasePerRound more each round,
        /// never more than IncomeCap. With the Bible values: 200, 400, 600 ... capped at 1600. Round 1 is
        /// covered by the starting credits; the income paid after round N is IncomeForRound(N + 1).
        /// </summary>
        public int IncomeForRound(int round)
        {
            int income = BaseIncome + IncomeIncreasePerRound * Math.Max(0, round - 1);
            return Math.Min(IncomeCap, income);
        }
    }
}
