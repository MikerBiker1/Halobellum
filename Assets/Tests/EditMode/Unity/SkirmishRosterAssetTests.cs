// SkirmishRosterAssetTests.cs
// The one faction of Unlock Skirmish on the real assets: 69 different units (70 until D116 took out the Infested Grunt), with ODST (which only lands from its spell) and the
// old placeholders left out, and 37 different cards: 22 spells and 15 general cards. A real draw deals 5 different fair pairs.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.EditorTools (HeadlessRunnerTool), Halobellum.Economy, Halobellum.Core, NUnit, UnityEditor.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.EditorTools;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class SkirmishRosterAssetTests
    {
        private static FactionInfo Skirmish()
        {
            var cache = new Dictionary<UnitDefinition, UnitStats>();
            var infos = new List<FactionInfo>();
            foreach (string asset in new[] { "UNSC_Standard", "Covenant_Standard", "Flood_Standard" })
            {
                var faction = AssetDatabase.LoadAssetAtPath<FactionDefinition>($"Assets/_Project/Data/Factions/{asset}.asset");
                Assert.IsNotNull(faction, asset);
                infos.Add(faction.ToInfo(cache));
            }
            return SkirmishFaction.Build(infos);
        }

        [Test]
        public void TheRoster_Has69DifferentUnits_WithoutOdstOrPlaceholders()
        {
            List<string> names = Skirmish().BaseRoster.Select(u => u.Name).ToList();
            Assert.AreEqual(69, names.Count);
            Assert.AreEqual(69, names.Distinct().Count());
            CollectionAssert.DoesNotContain(names, "ODST");
            List<UnitDefinition> placeholders = HeadlessRunnerTool.LoadUnitsInFolder("Assets/_Project/Data/Units/Placeholder");
            Assert.AreEqual(3, placeholders.Count);
            foreach (UnitDefinition placeholder in placeholders) CollectionAssert.DoesNotContain(names, placeholder.unitName);
        }

        [Test]
        public void TheRoster_IsInShopOrder()
        {
            List<UnitStats> roster = Skirmish().BaseRoster;
            for (int i = 1; i < roster.Count; i++)
            {
                bool inOrder = roster[i - 1].Tier < roster[i].Tier || (roster[i - 1].Tier == roster[i].Tier && roster[i - 1].Cost <= roster[i].Cost);
                Assert.IsTrue(inOrder, $"{roster[i - 1].Name} before {roster[i].Name}");
            }
        }

        [Test]
        public void TheDeck_Has37DifferentCards()
        {
            List<CardStats> cards = Skirmish().Cards;
            Assert.AreEqual(37, cards.Count);
            Assert.AreEqual(37, cards.Select(c => c.Name).Distinct().Count());
            Assert.AreEqual(22, cards.Count(c => c.Spell != null), "Spells.");
            Assert.AreEqual(15, cards.Count(c => c.Spell == null), "General cards.");
        }

        [Test]
        public void ARealDraw_DealsFiveDifferentFairPairs()
        {
            MatchRules rules = AssetDatabase.LoadAssetAtPath<MatchRulesDefinition>(MatchRulesDefinition.DefaultAssetPath).ToRules();
            rules.Mode = MatchMode.UnlockSkirmish;
            var factions = new List<FactionInfo> { Skirmish() };
            for (int seed = 0; seed < 50; seed++)
            {
                List<DraftCard> cards = StartingDraft.Draw(factions, rules, new SimRandom(seed));
                Assert.AreEqual(5, cards.Count);
                Assert.AreEqual(5, cards.Select(c => string.Join("+", c.Squads.Select(s => s.Name).OrderBy(n => n, System.StringComparer.Ordinal))).Distinct().Count(), $"Seed {seed}");
                foreach (DraftCard card in cards)
                {
                    Assert.That(card.Value, Is.InRange(rules.DraftCardValue - rules.DraftCardValueTolerance, rules.DraftCardValue + rules.DraftCardValueTolerance), card.ToString());
                    foreach (UnitStats squad in card.Squads) Assert.AreEqual(1, squad.Tier, card.ToString());
                }
            }
        }
    }
}
