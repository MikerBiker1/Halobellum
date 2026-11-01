// MatchRulesDefinition.cs
// The data asset holding the match rules (Bible Section 5): starting credits, income and its cap,
// base health, squads and unlocks per round, sale refunds and starting draft size. There should be
// exactly one, at Assets/_Project/Data/MatchRules.asset. Change these numbers in the Inspector,
// never in code. ToRules() copies them into plain C# MatchRules.
// Depends on: Halobellum.Economy (MatchRules), UnityEngine.

using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Match Rules", fileName = "MatchRules")]
    public sealed class MatchRulesDefinition : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/_Project/Data/MatchRules.asset";

        [Header("Credits")]
        public int startingCredits;
        [Tooltip("Income for round 1 (the same as the starting credits: round 1 is paid by them; after round 1 comes the round-2 income).")]
        public int baseIncome;
        [Tooltip("Extra income per round after the first.")]
        public int incomeIncreasePerRound;
        [Tooltip("Income per round never exceeds this. Banked credits have no limit.")]
        public int incomeCap;

        [Header("Bases")]
        public int baseHealth;

        [Header("Per round")]
        public int newSquadsPerRound;
        public int unlocksPerRound;

        [Header("Selling (share of the price refunded)")]
        [Range(0f, 1f)] public float refundBeforeBattle;
        [Range(0f, 1f)] public float refundAfterBattle;

        [Header("Starting draft")]
        public int draftCardCount;
        public int draftSquadsPerCard;

        [Header("Squad leveling")]
        [Tooltip("XP bar size as a share of the squad's base price (2 = kill twice your own price's worth of enemies, D26). Same at every level.")]
        public float xpBarPerSquadCost = 2f;
        [Tooltip("Level-up price as a share of the squad's base price (Bible: 0.5).")]
        [Range(0f, 2f)] public float levelUpCostShare = 0.5f;

        [Header("Cards (Milestone 7)")]
        [Tooltip("First round with a card offer. 0 = no cards.")]
        public int firstCardRound = 2;
        [Tooltip("First reinforcement offer, then every other round; the other rounds offer spells, items, economy cards and buffs.")]
        public int reinforcementFirstRound = 4;
        public int cardsPerOffer = 3;
        public int spellSkipBonus = 50;
        public int reinforcementSkipBonus = 100;
        [Tooltip("Reinforcement budget = base + per round x round number.")]
        public int reinforcementBudgetBase = 200;
        public int reinforcementBudgetPerRound = 50;
        public int reinforcementMaxLevel = 3;
        [Tooltip("The possible reinforcement level goes up by 1 every this many rounds.")]
        public int reinforcementLevelEveryRounds = 4;

        public MatchRules ToRules()
        {
            return new MatchRules
            {
                StartingCredits = startingCredits,
                BaseIncome = baseIncome,
                IncomeIncreasePerRound = incomeIncreasePerRound,
                IncomeCap = incomeCap,
                BaseHealth = baseHealth,
                NewSquadsPerRound = newSquadsPerRound,
                UnlocksPerRound = unlocksPerRound,
                RefundBeforeBattle = refundBeforeBattle,
                RefundAfterBattle = refundAfterBattle,
                DraftCardCount = draftCardCount,
                DraftSquadsPerCard = draftSquadsPerCard,
                XpBarPerSquadCost = xpBarPerSquadCost,
                LevelUpCostShare = levelUpCostShare,
                FirstCardRound = firstCardRound,
                ReinforcementFirstRound = reinforcementFirstRound,
                CardsPerOffer = cardsPerOffer,
                SpellSkipBonus = spellSkipBonus,
                ReinforcementSkipBonus = reinforcementSkipBonus,
                ReinforcementBudgetBase = reinforcementBudgetBase,
                ReinforcementBudgetPerRound = reinforcementBudgetPerRound,
                ReinforcementMaxLevel = reinforcementMaxLevel,
                ReinforcementLevelEveryRounds = reinforcementLevelEveryRounds,
            };
        }
    }
}
