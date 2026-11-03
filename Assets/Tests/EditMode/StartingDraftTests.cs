// StartingDraftTests.cs
// Tests the starting draft (Bible Section 4, rule 10): up to 5 cards, never the same sub-faction
// twice in a draw, 2 Tier 1 squads from the card's roster (sub-faction exclusives included), the
// chosen card fixes faction and sub-faction, gives 2 free squads and unlocks their types, and the
// AI gets its own draw.
// Depends on: Halobellum.Economy, Halobellum.Core, NUnit, TestMatch.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class StartingDraftTests
    {
        private static List<FactionInfo> EightSubFactions(out UnitStats exclusive)
        {
            var roster = new List<UnitStats> { TestMatch.Infantry(), TestMatch.Buggy(), TestMatch.Tank() };
            var unsc = TestMatch.Faction("UNSC", roster, "Noble", "Armored Division", "Aerospace Command");
            var covenant = TestMatch.Faction("Covenant", roster, "Banished", "Lekgolo Colony", "Unggoy Rebellion");
            var flood = TestMatch.Faction("Flood", roster, "UNSC-based", "Covenant-based");
            exclusive = TestMatch.Infantry();
            exclusive.Name = "Exclusive";
            unsc.SubFactions[0].ExclusiveUnits.Add(exclusive);
            return new List<FactionInfo> { unsc, covenant, flood };
        }

        [Test]
        public void Draw_HasFiveCards_WithDifferentSubFactions()
        {
            var cards = StartingDraft.Draw(EightSubFactions(out _), TestMatch.Rules(), new SimRandom(3));
            Assert.AreEqual(5, cards.Count);
            var seen = new HashSet<SubFactionInfo>();
            foreach (DraftCard card in cards) Assert.IsTrue(seen.Add(card.SubFaction), "A sub-faction appeared twice.");
        }

        [Test]
        public void Draw_EachCardHasTwoTierOneSquadsFromItsRoster()
        {
            foreach (int seed in new[] { 1, 2, 3, 4, 5 })
            {
                foreach (DraftCard card in StartingDraft.Draw(EightSubFactions(out _), TestMatch.Rules(), new SimRandom(seed)))
                {
                    Assert.AreEqual(2, card.Squads.Count);
                    List<UnitStats> roster = card.Faction.RosterFor(card.SubFaction);
                    foreach (UnitStats squad in card.Squads)
                    {
                        Assert.AreEqual(1, squad.Tier);
                        CollectionAssert.Contains(roster, squad);
                    }
                }
            }
        }

        private static UnitStats TierOne(string name, int cost) => new UnitStats { Name = name, Tier = 1, Cost = cost };

        [Test]
        public void Draw_EveryCardIsWorthAbout300_AndEveryPriceStillAppears()
        {
            var roster = new List<UnitStats> { TierOne("100", 100), TierOne("150a", 150), TierOne("150b", 150), TierOne("200a", 200), TierOne("200b", 200) };
            var factions = new List<FactionInfo> { TestMatch.Faction("UNSC", roster, "Noble", "Armored Division", "Aerospace Command") };
            var seen = new HashSet<int>();
            var names = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                foreach (DraftCard card in StartingDraft.Draw(factions, TestMatch.Rules(), new SimRandom(seed)))
                {
                    int value = card.Squads[0].Cost + card.Squads[1].Cost;
                    Assert.That(value, Is.InRange(250, 350), card.ToString());
                    Assert.AreNotSame(card.Squads[0], card.Squads[1]);
                    seen.Add(value);
                    foreach (UnitStats squad in card.Squads) names.Add(squad.Name);
                }
            }
            CollectionAssert.AreEquivalent(new[] { 250, 300, 350 }, seen);
            Assert.AreEqual(roster.Count, names.Count, "No unit is left out of the draft by this.");
        }

        [Test]
        public void Draw_WithNoFairPair_TakesTheClosest()
        {
            var roster = new List<UnitStats> { TierOne("a", 100), TierOne("b", 100), TierOne("c", 120) };
            var factions = new List<FactionInfo> { TestMatch.Faction("UNSC", roster, "Noble") };
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
            {
                DraftCard card = StartingDraft.Draw(factions, TestMatch.Rules(), new SimRandom(seed))[0];
                Assert.AreEqual(220, card.Squads[0].Cost + card.Squads[1].Cost, card.ToString());
            }
        }

        [Test]
        public void Draw_CanOfferSubFactionExclusiveUnits()
        {
            bool offered = false;
            for (int seed = 0; seed < 200 && !offered; seed++)
            {
                foreach (DraftCard card in StartingDraft.Draw(EightSubFactions(out UnitStats exclusive), TestMatch.Rules(), new SimRandom(seed)))
                {
                    if (card.Squads.Contains(exclusive)) offered = true;
                }
            }
            Assert.IsTrue(offered);
        }

        [Test]
        public void Draw_WithOnlyTwoSubFactions_HasTwoCards()
        {
            var cards = StartingDraft.Draw(TestMatch.StandardFactions(out _, out _, out _, out _), TestMatch.Rules(), new SimRandom(1));
            Assert.AreEqual(2, cards.Count);
            Assert.AreNotEqual(cards[0].Faction, cards[1].Faction);
        }

        [Test]
        public void Draw_IsRepeatableWithTheSameSeed()
        {
            var a = StartingDraft.Draw(EightSubFactions(out _), TestMatch.Rules(), new SimRandom(9));
            var b = StartingDraft.Draw(EightSubFactions(out _), TestMatch.Rules(), new SimRandom(9));
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].ToString(), b[i].ToString());
        }

        [Test]
        public void ChoosingACard_FixesTheFaction_GivesTwoFreeSquads_AndUnlocksThem()
        {
            var match = new Match(TestMatch.Rules(), TestRules.Default(), TestMatch.StandardFactions(out _, out _, out _, out _), 1, new DoNothingAi());
            DraftCard card = match.PlayerDraft[1];
            Assert.IsTrue(match.ChooseDraft(1).Ok);

            Assert.AreSame(card.Faction, match.Player.Faction);
            Assert.AreSame(card.SubFaction, match.Player.SubFaction);
            Assert.AreEqual(2, match.Player.Squads.Count);
            foreach (PlacedSquad squad in match.Player.Squads)
            {
                Assert.AreEqual(0, squad.PricePaid);
                Assert.IsTrue(squad.FromDraft);
                Assert.IsTrue(match.Player.Unlocked.Contains(squad.Type));
            }
            Assert.AreEqual(0, match.Player.SquadsBoughtThisRound);
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);

            // The free squads are placed in the middle of the zone (the first exactly at the middle).
            PlacedSquad first = match.Player.Squads[0];
            DeploymentGrid.Footprint(first.Type, false, match.CombatRules, out int width, out int depth);
            Assert.AreEqual((match.CombatRules.ArenaWidthSquares - width) / 2, first.Column);
            Assert.AreEqual((match.CombatRules.DeploymentDepthSquares - depth) / 2, first.Row);
            Vec2 second = DeploymentGrid.CentreInSquares(match.Player.Squads[1], match.CombatRules);
            Assert.Less(System.Math.Abs(second.X), 10f, "The second free squad is next to the first.");
            Assert.Less(System.Math.Abs(second.Y - match.CombatRules.DeploymentDepthSquares * 0.5f), 10f);
        }

        [TestCase(100, 150, 2100, Description = "The owner's example: a card worth 250.")]
        [TestCase(100, 200, 2000)]
        [TestCase(150, 200, 1900)]
        public void TheBase_StartsStrongerWithACheaperCard(int first, int second, int baseHealth)
        {
            var match = new Match(TestMatch.Rules(), TestRules.Default(), TestMatch.StandardFactions(out _, out _, out _, out _), 1, new DoNothingAi());
            DraftCard card = match.PlayerDraft[0];
            card.Squads[0].Cost = first;
            card.Squads[1].Cost = second;
            Assert.IsTrue(match.ChooseDraft(0).Ok);
            Assert.AreEqual(baseHealth, match.Player.BaseHealth);
            Assert.AreEqual(baseHealth, match.Player.StartingBaseHealth);
        }

        [Test]
        public void TheAI_GetsItsOwnDraw_AndPicksACard()
        {
            var match = new Match(TestMatch.Rules(), TestRules.Default(), TestMatch.StandardFactions(out _, out _, out _, out _), 1, new DoNothingAi());
            Assert.AreEqual(2, match.EnemyDraft.Count);
            match.ChooseDraft(0);
            Assert.IsTrue(match.Enemy.HasFaction);
            Assert.AreEqual(2, match.Enemy.Squads.Count);
            Assert.AreEqual(2000, match.Enemy.BaseHealth);
        }
    }
}
