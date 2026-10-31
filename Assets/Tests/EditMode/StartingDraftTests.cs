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
