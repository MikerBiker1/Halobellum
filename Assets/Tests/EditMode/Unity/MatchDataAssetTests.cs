// MatchDataAssetTests.cs
// Loads the real match data: Data/MatchRules.asset must hold the Bible's starting values (same as
// the TestMatch fixture), and the factions in Data/Factions must load with a roster and at least
// one sub-faction, and give a playable starting draft.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.Economy, NUnit, UnityEditor.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class MatchDataAssetTests
    {
        [Test]
        public void MatchRulesAsset_MatchesBibleStartingValues()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MatchRulesDefinition>(MatchRulesDefinition.DefaultAssetPath);
            Assert.IsNotNull(asset, "Missing " + MatchRulesDefinition.DefaultAssetPath);
            MatchRules actual = asset.ToRules();
            MatchRules expected = TestMatch.Rules();
            Assert.AreEqual(expected.StartingCredits, actual.StartingCredits);
            Assert.AreEqual(expected.BaseIncome, actual.BaseIncome);
            Assert.AreEqual(expected.IncomeIncreasePerRound, actual.IncomeIncreasePerRound);
            Assert.AreEqual(expected.IncomeCap, actual.IncomeCap);
            Assert.AreEqual(expected.BaseHealth, actual.BaseHealth);
            Assert.AreEqual(expected.NewSquadsPerRound, actual.NewSquadsPerRound);
            Assert.AreEqual(expected.UnlocksPerRound, actual.UnlocksPerRound);
            Assert.AreEqual(expected.RefundBeforeBattle, actual.RefundBeforeBattle, 1e-5f);
            Assert.AreEqual(expected.RefundAfterBattle, actual.RefundAfterBattle, 1e-5f);
            Assert.AreEqual(expected.DraftCardCount, actual.DraftCardCount);
            Assert.AreEqual(expected.DraftSquadsPerCard, actual.DraftSquadsPerCard);
            Assert.AreEqual(expected.XpBarPerSquadCost, actual.XpBarPerSquadCost, 1e-5f);
            Assert.AreEqual(expected.LevelUpCostShare, actual.LevelUpCostShare, 1e-5f, "Bible: level-ups cost half the base price.");
            MatchRules cards = TestMatch.RulesWithCards();
            Assert.AreEqual(cards.FirstCardRound, actual.FirstCardRound, "Bible: card offers from round 2.");
            Assert.AreEqual(cards.CardsPerOffer, actual.CardsPerOffer);
            Assert.AreEqual(cards.SpellSkipBonus, actual.SpellSkipBonus);
            Assert.AreEqual(cards.ReinforcementSkipBonus, actual.ReinforcementSkipBonus);
            Assert.AreEqual(cards.ReinforcementBudgetBase, actual.ReinforcementBudgetBase);
            Assert.AreEqual(cards.ReinforcementBudgetPerRound, actual.ReinforcementBudgetPerRound);
            Assert.AreEqual(cards.ReinforcementMaxLevel, actual.ReinforcementMaxLevel);
            Assert.AreEqual(cards.ReinforcementLevelEveryRounds, actual.ReinforcementLevelEveryRounds);
        }

        [Test]
        public void Factions_LoadWithRosters_AndGiveAPlayableDraft()
        {
            var factions = AssetDatabase.FindAssets("t:" + nameof(FactionDefinition), new[] { "Assets/_Project/Data/Factions" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FactionDefinition>)
                .ToList();
            Assert.GreaterOrEqual(factions.Count, 2, "Expected at least Standard UNSC and Standard Covenant.");

            var cache = new Dictionary<UnitDefinition, UnitStats>();
            var infos = factions.Select(f => f.ToInfo(cache)).ToList();
            foreach (FactionInfo info in infos)
            {
                Assert.IsNotEmpty(info.BaseRoster, info.Name);
                Assert.IsNotEmpty(info.SubFactions, info.Name);
            }

            List<DraftCard> cards = StartingDraft.Draw(infos, TestMatch.Rules(), new SimRandom(1));
            Assert.AreEqual(infos.Sum(i => i.SubFactions.Count), cards.Count);
            foreach (DraftCard card in cards) Assert.AreEqual(2, card.Squads.Count);
        }
    }
}
