// MatchRules.cs
// The match rules from Bible Section 5 (starting credits, income and its cap, base health, squads
// and unlocks per round, sale refunds, starting draft size). The values come from the Match Rules
// data asset (Data/MatchRules.asset) through MatchRulesDefinition.ToRules(); tests build their own.
// A MatchSetup can then change a match's own copy (the mode, base health and card filter); the defaults here are Faction mode.
// Depends on: Halobellum.Core (SpellStats).

using System;
using Halobellum.Core;

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
        /// <summary>What a card's 2 squads are worth together, in credits (the owner: about 300).</summary>
        public int DraftCardValue = 300;
        /// <summary>How far from that a card may be. In a roster with no such pair, the closest pair is taken.</summary>
        public int DraftCardValueTolerance = 50;
        /// <summary>Base health a side gives up per credit its starting card is worth above DraftCardValue, or gains per credit below it (the owner: 100 per 50, D59).</summary>
        public float DraftBaseHealthPerCredit = 2f;
        /// <summary>
        /// Size of the XP bar as a share of the squad's base price (decision D26: 2 = a squad must kill twice
        /// its price's worth of enemies to fill it). The same at every level (Bible Section 5).
        /// </summary>
        public float XpBarPerSquadCost = 2f;
        /// <summary>Level-up price as a share of the squad's base price (Bible: half).</summary>
        public float LevelUpCostShare = 0.5f;

        // Cards (Bible Section 10, Milestone 7).
        /// <summary>First round with a card offer (Bible: 2). 0 = no cards.</summary>
        public int FirstCardRound = 2;
        /// <summary>First reinforcement offer (decision D32: round 4), then every other round; the other rounds offer general cards.</summary>
        public int ReinforcementFirstRound = 4;
        /// <summary>Cards per offer (Bible: 3).</summary>
        public int CardsPerOffer = 3;
        /// <summary>Credits for skipping a general offer (spells, items, economy, buffs; Bible: 50).</summary>
        public int SpellSkipBonus = 50;
        /// <summary>Credits for skipping a reinforcement offer (Bible: 100).</summary>
        public int ReinforcementSkipBonus = 100;
        /// <summary>Reinforcement budget: base plus this much per round number (Bible Section 18: 200 + 50 per round).</summary>
        public int ReinforcementBudgetBase = 200;
        public int ReinforcementBudgetPerRound = 50;
        /// <summary>Highest level a reinforcement card can have (decision D32).</summary>
        public int ReinforcementMaxLevel = 3;
        /// <summary>The highest tier a reinforcement card can bring: no Titans (the owner, D60).</summary>
        public int ReinforcementMaxTier = 3;
        /// <summary>The possible reinforcement level goes up by 1 every this many rounds: level 1 in rounds 1-4, up to 2 in 5-8 ... (D32).</summary>
        public int ReinforcementLevelEveryRounds = 4;

        // Towers (D55). No health means a match without towers, as tests that aren't about them play.
        /// <summary>A level 1 tower's health (the owner: a Scorpion's shot kills it). It doubles with each level.</summary>
        public float TowerBaseHealth;
        public float TowerRadius = 3f;
        /// <summary>How many squares wide and deep a tower stands, where ground squads can't be placed.</summary>
        public int TowerSquares = 3;
        public int TowerLevelUpCost = 100;
        /// <summary>How long its shockwave lasts at each level; there are as many levels as these.</summary>
        public float[] TowerShockwaveSeconds = { 10f, 8f, 6f, 4f, 2f };
        public float ShockwaveSpeedMultiplier = 0.6f;
        public float ShockwaveDamageMultiplier = 0.6f;
        public float ShockwaveFireRateMultiplier = 0.6f;
        public float ShockwaveDamageTakenMultiplier = 1.4f;
        /// <summary>
        /// XP for destroying a tower, by its level: a level 1 squad of the tier of the same number, at the middle of that tier's price band; level 5 a
        /// level 2 Titan, worth half as much again (the owner, D59).
        /// </summary>
        public float[] TowerXpByLevel = { 150f, 275f, 450f, 800f, 1200f };
        public int ExtraSlotCost = 100;
        /// <summary>The spell the Command Center sells; its price is its Cost. It is used once.</summary>
        public SpellStats OilZone;
        public int RapidResupplyCredits = 200;
        public int RapidResupplyPenalty = 300;
        public int EliteRecruitmentCost = 100;
        public float ResearchHealthShare = 0.15f;
        public float ResearchDamageShare = 0.12f;
        /// <summary>The price of each research step, for health and for damage alike; there are as many steps as these.</summary>
        public int[] ResearchCosts = { 100, 300 };

        // The match's mode (MatchSetup). The defaults keep Faction mode as it has always played.
        /// <summary>Faction mode, or Unlock Skirmish (no factions: any unit can be unlocked).</summary>
        public MatchMode Mode;
        /// <summary>Which cards the offers bring.</summary>
        public CardFilter Cards;
        /// <summary>Unlock Skirmish: what the Hard AI adds to a flyer's score for being able to flank (starting guess).</summary>
        public float AiSkirmishFlankBonus = 0.75f;
        /// <summary>Unlock Skirmish: how much better a locked unit must score than an owned one before the AI unlocks it (starting guess).</summary>
        public float AiSkirmishUnlockMargin = 0.25f;
        /// <summary>A tournament trial of where the Hard AI puts its carriers; the game never sets it.</summary>
        public CarrierPlacement AiCarrierPlacement;

        public bool HasTowers => TowerBaseHealth > 0f;
        public int TowerMaxLevel => TowerShockwaveSeconds.Length;

        public bool CardsEnabled => FirstCardRound > 0;

        /// <summary>A side's base health at the start of a match, from what its starting card is worth (D59).</summary>
        public int StartingBaseHealth(int cardValue) => Math.Max(1, BaseHealth - (int)Math.Round((cardValue - DraftCardValue) * DraftBaseHealthPerCredit));

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
