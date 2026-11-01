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
        /// <summary>Income for round 1 (Bible: 400). Later rounds add IncomeIncreasePerRound each.</summary>
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
        /// Income for a round: BaseIncome in round 1, then IncomeIncreasePerRound more each round,
        /// never more than IncomeCap. With the Bible values: 400, 600, 800 ... capped at 1600.
        /// </summary>
        public int IncomeForRound(int round)
        {
            int income = BaseIncome + IncomeIncreasePerRound * Math.Max(0, round - 1);
            return Math.Min(IncomeCap, income);
        }
    }
}
