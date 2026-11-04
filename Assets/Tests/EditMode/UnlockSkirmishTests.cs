// UnlockSkirmishTests.cs
// The rules of Unlock Skirmish mode on made-up data: the one faction holds every unit and card once, in an order that doesn't
// depend on its sources; the draft deals 5 different fair pairs; base health follows the setting, with a floor; the card filter
// picks the kind of every offer; any roster unit can be unlocked; and the AI chooses its starting card without knowing the player's.
// There is deliberately no test comparing the Faction draft with the mode at its default: both sides would run the same code.
// Faction mode is compared before and after with a tournament run instead.
// Depends on: Halobellum.Economy, Halobellum.Core, NUnit, TestMatch, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class UnlockSkirmishTests
    {
        private static UnitStats Unit(string name, int tier = 1, int cost = 100, int unlock = 0)
        {
            UnitStats unit = TestMatch.Infantry();
            unit.Name = name;
            unit.Tier = tier;
            unit.Cost = cost;
            unit.UnlockCost = unlock;
            return unit;
        }

        private static CardStats Card(string name) => new CardStats { Name = name };

        /// <summary>Two factions that share a unit and a card, and a spell whose squads are in no roster.</summary>
        private static List<FactionInfo> TwoFactions()
        {
            FactionInfo alpha = TestMatch.Faction("Alpha", new List<UnitStats> { Unit("Marine"), Unit("Tank", 2, 300, 50) }, "One");
            alpha.SubFactions[0].ExclusiveUnits.Add(Unit("Spartan", 1, 150));
            alpha.Cards.Add(Card("Smoke"));
            alpha.Cards.Add(Card("Nuke"));
            alpha.Cards.Add(CardStats.FromSpell(new SpellStats { Name = "Pods", DropUnit = Unit("Trooper") }));
            FactionInfo beta = TestMatch.Faction("Beta", new List<UnitStats> { Unit("Marine"), Unit("Grunt") }, "Two", "Three");
            beta.SubFactions[1].ExclusiveUnits.Add(Unit("Walker", 3, 450, 225));
            beta.SubFactions[1].ExclusiveUnits.Add(Unit("Tank", 2, 300, 50));
            beta.Cards.Add(Card("Smoke"));
            beta.Cards.Add(Card("Spore"));
            return new List<FactionInfo> { alpha, beta };
        }

        private static string[] Names(IEnumerable<UnitStats> units) => units.Select(u => u.Name).ToArray();

        // ------------------------------------------------------------------ the one faction

        [Test]
        public void Build_HoldsEveryUnitAndCardOnce_InShopOrder()
        {
            FactionInfo skirmish = SkirmishFaction.Build(TwoFactions());
            Assert.AreEqual(SkirmishFaction.Name, skirmish.Name);
            CollectionAssert.AreEqual(new[] { "Grunt", "Marine", "Spartan", "Tank", "Walker" }, Names(skirmish.BaseRoster), "By tier, then price, then name. Trooper only lands from its spell.");
            CollectionAssert.AreEqual(new[] { "Nuke", "Pods", "Smoke", "Spore" }, skirmish.Cards.Select(c => c.Name).ToArray());
            Assert.AreEqual(1, skirmish.SubFactions.Count);
            Assert.AreEqual(SkirmishFaction.CardName, skirmish.SubFactions[0].Name);
            Assert.IsEmpty(skirmish.SubFactions[0].ExclusiveUnits);
        }

        [Test]
        public void Build_DoesNotDependOnTheOrderOfItsSources()
        {
            List<FactionInfo> reversed = TwoFactions();
            reversed.Reverse();
            foreach (FactionInfo faction in reversed)
            {
                faction.BaseRoster.Reverse();
                faction.SubFactions.Reverse();
                faction.Cards.Reverse();
            }
            FactionInfo a = SkirmishFaction.Build(TwoFactions()), b = SkirmishFaction.Build(reversed);
            CollectionAssert.AreEqual(Names(a.BaseRoster), Names(b.BaseRoster));
            CollectionAssert.AreEqual(a.Cards.Select(c => c.Name).ToArray(), b.Cards.Select(c => c.Name).ToArray());
        }

        [Test]
        public void ShopOrder_SortsByTierThenPrice_AndTiesKeepTheirOrder()
        {
            var units = new List<UnitStats> { Unit("Second", 2, 200), Unit("Zed", 1, 150), Unit("Alpha", 1, 150), Unit("First", 1, 100), Unit("Dear", 1, 400) };
            ShopOrder.Sort(units);
            CollectionAssert.AreEqual(new[] { "First", "Zed", "Alpha", "Dear", "Second" }, Names(units));
        }

        // ------------------------------------------------------------------ the draft

        private static MatchRules SkirmishRules()
        {
            MatchRules rules = TestMatch.Rules();
            rules.Mode = MatchMode.UnlockSkirmish;
            return rules;
        }

        /// <summary>The one faction, with enough Tier 1 units for many fair pairs, and a Tier 2 that must never be dealt.</summary>
        private static List<FactionInfo> DraftPool()
        {
            var roster = new List<UnitStats>
            {
                Unit("A", 1, 100), Unit("B", 1, 100), Unit("C", 1, 150), Unit("D", 1, 150), Unit("E", 1, 150), Unit("F", 1, 200), Unit("G", 1, 200), Unit("H", 1, 200),
                Unit("Big", 2, 150, 50),
            };
            FactionInfo skirmish = SkirmishFaction.Build(new List<FactionInfo> { TestMatch.Faction("Real", roster, "One") });
            return new List<FactionInfo> { skirmish };
        }

        [Test]
        public void Draw_DealsFiveFairPairs_NoneTwice()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                List<DraftCard> cards = StartingDraft.Draw(DraftPool(), SkirmishRules(), new SimRandom(seed));
                Assert.AreEqual(5, cards.Count);
                var pairs = new HashSet<string>();
                foreach (DraftCard card in cards)
                {
                    Assert.AreEqual(SkirmishFaction.Name, card.Faction.Name);
                    Assert.AreEqual(SkirmishFaction.CardName, card.SubFaction.Name);
                    Assert.AreEqual(2, card.Squads.Count);
                    Assert.AreNotSame(card.Squads[0], card.Squads[1]);
                    Assert.That(card.Value, Is.InRange(250, 350), card.ToString());
                    foreach (UnitStats squad in card.Squads) Assert.AreEqual(1, squad.Tier, card.ToString());
                    string[] names = Names(card.Squads).OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
                    Assert.IsTrue(pairs.Add(string.Join("+", names)), $"Seed {seed} dealt {card} twice.");
                }
            }
        }

        [Test]
        public void Draw_IsRepeatableWithTheSameSeed()
        {
            string[] Dealt(int seed) => StartingDraft.Draw(DraftPool(), SkirmishRules(), new SimRandom(seed)).Select(c => c.ToString()).ToArray();
            CollectionAssert.AreEqual(Dealt(7), Dealt(7));
            CollectionAssert.AreNotEqual(Dealt(7), Dealt(8));
        }

        [Test]
        public void Draw_FromASmallPool_StillDealsEveryCard()
        {
            var roster = new List<UnitStats> { Unit("A", 1, 150), Unit("B", 1, 150) };
            var factions = new List<FactionInfo> { SkirmishFaction.Build(new List<FactionInfo> { TestMatch.Faction("Real", roster, "One") }) };
            Assert.AreEqual(5, StartingDraft.Draw(factions, SkirmishRules(), new SimRandom(1)).Count, "One pair is all there is, so it repeats.");
        }

        [Test]
        public void Draw_WithNoTierOneUnits_DealsNothing()
        {
            var roster = new List<UnitStats> { Unit("Big", 2, 300, 50) };
            var factions = new List<FactionInfo> { SkirmishFaction.Build(new List<FactionInfo> { TestMatch.Faction("Real", roster, "One") }) };
            Assert.IsEmpty(StartingDraft.Draw(factions, SkirmishRules(), new SimRandom(1)));
        }

        // ------------------------------------------------------------------ base health

        [TestCase(100, 250, 105)]
        [TestCase(100, 300, 100)]
        [TestCase(100, 350, 95)]
        [TestCase(1000, 250, 1050)]
        [TestCase(1000, 300, 1000)]
        [TestCase(1000, 350, 950)]
        [TestCase(2000, 250, 2100, Description = "Today's game.")]
        [TestCase(2000, 300, 2000)]
        [TestCase(2000, 350, 1900)]
        [TestCase(10000, 250, 10500, Description = "5% of the setting for 50 credits, at any setting.")]
        [TestCase(10000, 300, 10000)]
        [TestCase(10000, 350, 9500)]
        public void BaseHealth_FollowsTheSettingAndTheCard(int setting, int cardValue, int expected)
        {
            MatchRules rules = TestMatch.Rules();
            new MatchSetup { Mode = MatchMode.UnlockSkirmish, BaseHealth = setting }.ApplyTo(rules);
            Assert.AreEqual(expected, rules.StartingBaseHealth(cardValue));
        }

        [TestCase(-50, 100)]
        [TestCase(1, 100)]
        [TestCase(149, 100)]
        [TestCase(150, 200)]
        [TestCase(1234, 1200)]
        [TestCase(10001, 10000)]
        [TestCase(99999, 10000)]
        public void BaseHealth_IsClampedAndSnapped(int setting, int expected)
        {
            MatchRules rules = TestMatch.Rules();
            new MatchSetup { BaseHealth = setting }.ApplyTo(rules);
            Assert.AreEqual(expected, rules.BaseHealth);
        }

        [Test]
        public void NoBaseHealthSetting_LeavesTheRulesAsTheyAre()
        {
            MatchRules rules = TestMatch.Rules();
            new MatchSetup().ApplyTo(rules);
            Assert.AreEqual(MatchMode.Faction, rules.Mode);
            Assert.AreEqual(CardFilter.AllCards, rules.Cards);
            Assert.AreEqual(2000, rules.BaseHealth);
            Assert.AreEqual(2f, rules.DraftBaseHealthPerCredit);
        }

        [Test]
        public void StartingBaseHealth_NeverFallsBelowOne()
        {
            MatchRules rules = TestMatch.Rules();
            new MatchSetup { BaseHealth = 100 }.ApplyTo(rules);
            Assert.AreEqual(1, rules.StartingBaseHealth(5000), "However much the card is worth, the match doesn't start lost.");
        }

        // ------------------------------------------------------------------ the card filter

        [TestCase(CardFilter.AllCards, new[] { CardKind.General, CardKind.General, CardKind.Reinforcement, CardKind.General, CardKind.Reinforcement, CardKind.General, CardKind.Reinforcement })]
        [TestCase(CardFilter.NoReinforcements, new[] { CardKind.General, CardKind.General, CardKind.General, CardKind.General, CardKind.General, CardKind.General, CardKind.General })]
        [TestCase(CardFilter.OnlyReinforcements, new[] { CardKind.Reinforcement, CardKind.Reinforcement, CardKind.Reinforcement, CardKind.Reinforcement, CardKind.Reinforcement, CardKind.Reinforcement, CardKind.Reinforcement })]
        public void KindForRound_FollowsTheFilter(CardFilter filter, CardKind[] fromRoundTwo)
        {
            MatchRules rules = TestMatch.RulesWithCards();
            rules.Cards = filter;
            for (int i = 0; i < fromRoundTwo.Length; i++) Assert.AreEqual(fromRoundTwo[i], CardDeck.KindForRound(rules, i + 2), $"Round {i + 2}");
            Assert.IsFalse(CardDeck.HasOffer(rules, 1), "Round 1 has no offer, whatever the filter.");
            Assert.IsTrue(CardDeck.HasOffer(rules, 2));
        }

        // ------------------------------------------------------------------ unlocking

        private static Match SkirmishMatch(IMatchAi ai = null)
        {
            var factions = new List<FactionInfo> { SkirmishFaction.Build(TwoFactions()) };
            return new Match(SkirmishRules(), TestRules.Default(), factions, 1, ai ?? new DoNothingAi());
        }

        [Test]
        public void AnyRosterUnit_CanBeUnlocked()
        {
            Match match = SkirmishMatch();
            Assert.IsTrue(match.ChooseDraft(0).Ok);
            match.Player.Credits = 100000;
            CollectionAssert.AreEquivalent(new[] { "Grunt", "Marine", "Spartan", "Tank", "Walker" }, Names(match.Player.Roster), "Units of both real factions, of every sub-faction.");
            foreach (UnitStats unit in match.Player.Roster)
            {
                if (match.Player.Unlocked.Contains(unit)) continue;
                match.Player.UnlocksThisRound = 0;
                ActionResult result = match.Unlock(Team.A, unit);
                Assert.IsTrue(result.Ok, result.Message);
            }
            Assert.AreEqual(5, match.Player.Unlocked.Count);
        }

        [Test]
        public void TheUnlockError_SaysWhatFitsTheMode()
        {
            Match skirmish = SkirmishMatch();
            skirmish.ChooseDraft(0);
            ActionResult result = skirmish.Unlock(Team.A, Unit("Trooper"));
            Assert.IsFalse(result.Ok);
            Assert.AreEqual("Trooper cannot be unlocked here.", result.Message);

            var match = new Match(TestMatch.Rules(), TestRules.Default(), TwoFactions(), 1, new DoNothingAi());
            match.ChooseDraft(0);
            result = match.Unlock(Team.A, Unit("Trooper"));
            Assert.IsFalse(result.Ok);
            StringAssert.Contains("not in your faction's roster", result.Message);
        }

        // ------------------------------------------------------------------ the AI's choice

        /// <summary>Notes what the AI could see when it chose its starting card.</summary>
        private sealed class SpyAi : IMatchAi
        {
            public Match Match;
            public int SeenOpponentHealth = -1, SeenOpponentArmy = -1, OpponentSquads = -1;
            public string SeenOpponentFaction = "not asked";

            public int ChooseDraftCard(AiView view, IReadOnlyList<DraftCard> cards)
            {
                SeenOpponentHealth = view.OpponentBaseHealth;
                SeenOpponentArmy = view.SeenOpponentArmy.Count;
                SeenOpponentFaction = view.SeenOpponentFaction;
                OpponentSquads = Match.Player.Squads.Count;
                return 0;
            }

            public int ChooseCard(AiView view, CardOffer offer) => -1;

            public void Deploy(AiView view)
            {
            }

            public string Label => "Spy";
        }

        [TestCase(MatchMode.Faction)]
        [TestCase(MatchMode.UnlockSkirmish)]
        public void TheAi_ChoosesItsCard_WithoutKnowingThePlayers(MatchMode mode)
        {
            var spy = new SpyAi();
            MatchRules rules = TestMatch.Rules();
            rules.Mode = mode;
            List<FactionInfo> factions = mode == MatchMode.UnlockSkirmish ? new List<FactionInfo> { SkirmishFaction.Build(TwoFactions()) } : TwoFactions();
            spy.Match = new Match(rules, TestRules.Default(), factions, 1, spy);
            Assert.IsTrue(spy.Match.ChooseDraft(0).Ok);

            Assert.AreEqual(0, spy.SeenOpponentHealth, "The player's card, which sets this, isn't applied yet.");
            Assert.AreEqual(0, spy.SeenOpponentArmy);
            Assert.IsNull(spy.SeenOpponentFaction);
            Assert.AreEqual(0, spy.OpponentSquads);
            Assert.AreEqual(2, spy.Match.Player.Squads.Count, "Both cards are applied after that.");
            Assert.AreEqual(2, spy.Match.Enemy.Squads.Count);
        }

        [Test]
        public void ChoosingACard_NumbersThePlayersSquadsFirst()
        {
            Match match = SkirmishMatch();
            match.ChooseDraft(0);
            CollectionAssert.AreEqual(new[] { 1, 2 }, match.Player.Squads.Select(s => s.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 3, 4 }, match.Enemy.Squads.Select(s => s.Id).ToArray());
        }
    }
}
