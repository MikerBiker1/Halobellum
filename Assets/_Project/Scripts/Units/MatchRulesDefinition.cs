// MatchRulesDefinition.cs
// The data asset holding the match rules (Bible Section 5): starting credits, income and its cap,
// base health, squads and unlocks per round, sale refunds and starting draft size. There should be
// exactly one, at Assets/_Project/Data/MatchRules.asset. Change these numbers in the Inspector,
// never in code. ToRules() copies them into plain C# MatchRules.
// Depends on: Halobellum.Economy (MatchRules), Halobellum.Core (SpellStats), UnityEngine.

using Halobellum.Core;
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

        [Header("Towers (D55)")]
        [Tooltip("A level 1 tower's health (a Scorpion's shot kills it). It doubles with each level. 0 = no towers.")]
        public float towerBaseHealth = 100f;
        public float towerRadius = 3f;
        [Tooltip("How many squares wide and deep a tower stands, where ground squads can't be placed.")]
        public int towerSquares = 3;
        public int towerLevelUpCost = 100;
        [Tooltip("How many seconds its shockwave lasts at each level. There are as many levels as these.")]
        public float[] towerShockwaveSeconds = { 10f, 8f, 6f, 4f, 2f };
        [Range(0f, 1f)] public float shockwaveSpeedMultiplier = 0.7f;
        [Range(0f, 1f)] public float shockwaveDamageMultiplier = 0.7f;
        [Range(0f, 1f)] public float shockwaveFireRateMultiplier = 0.7f;

        [Header("Command Center")]
        public int extraSlotCost = 100;
        public int rapidResupplyCredits = 200;
        public int rapidResupplyPenalty = 300;
        public int eliteRecruitmentCost = 100;
        public int oilZoneCost = 150;
        [Tooltip("Metres along the line.")]
        public float oilZoneLength = 15f;
        [Tooltip("Metres across the line.")]
        public float oilZoneWidth = 4f;
        public float oilZoneSeconds = 15f;
        [Range(0f, 1f)] public float oilZoneSlowShare = 0.4f;
        public float oilZoneFireDamageMultiplier = 1.5f;

        [Header("Research Center")]
        public float researchHealthShare = 0.15f;
        public float researchDamageShare = 0.12f;
        [Tooltip("The price of each research step, for health and for damage alike. There are as many steps as these.")]
        public int[] researchCosts = { 100, 300 };

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
                TowerBaseHealth = towerBaseHealth,
                TowerRadius = towerRadius,
                TowerSquares = towerSquares,
                TowerLevelUpCost = towerLevelUpCost,
                TowerShockwaveSeconds = towerShockwaveSeconds,
                ShockwaveSpeedMultiplier = shockwaveSpeedMultiplier,
                ShockwaveDamageMultiplier = shockwaveDamageMultiplier,
                ShockwaveFireRateMultiplier = shockwaveFireRateMultiplier,
                ExtraSlotCost = extraSlotCost,
                RapidResupplyCredits = rapidResupplyCredits,
                RapidResupplyPenalty = rapidResupplyPenalty,
                EliteRecruitmentCost = eliteRecruitmentCost,
                ResearchHealthShare = researchHealthShare,
                ResearchDamageShare = researchDamageShare,
                ResearchCosts = researchCosts,
                OilZone = new SpellStats
                {
                    Name = "Oil Zone",
                    Description = "Click one end, aim the other. Oil slows everything on the ground that crosses it, and fire burns it worse.",
                    Offensive = true,
                    Effect = SpellEffect.Oil,
                    Cost = oilZoneCost,
                    AreaWidth = oilZoneLength,
                    AreaDepth = oilZoneWidth,
                    DurationSeconds = oilZoneSeconds,
                    SlowShare = oilZoneSlowShare,
                    FireDamageMultiplier = oilZoneFireDamageMultiplier,
                },
            };
        }
    }
}
