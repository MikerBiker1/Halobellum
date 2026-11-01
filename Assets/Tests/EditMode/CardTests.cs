// CardTests.cs
// Cards (Bible Section 10, Milestone 7): offers from round 2 alternating spells and reinforcements,
// skip bonuses, spell prices, maximum copies, cooldowns ("a 1-round cooldown can be used every other
// round"), placement rules (offensive anywhere, defensive only on your own side), spells cast when the
// battle starts and hidden from the opponent, reinforcement budget and pricing (the Bible's examples),
// reinforcement squads (unlock, outside the squad limit, 0 XP, movable, refunds), spell kills in the
// round summary, and the AI taking cards and casting spells in full matches.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class CardTests
    {
        private SpellStats _strike, _nuke, _dome, _pods;
        private UnitStats _infantry, _buggy, _tank, _walker;

        private List<FactionInfo> Factions()
        {
            List<FactionInfo> factions = TestMatch.StandardFactions(out _infantry, out _buggy, out _tank, out _walker);
            _strike = new SpellStats { Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Cost = 100, CooldownRounds = 1, MaxOwnedCopies = 2, Radius = 5f, DelaySeconds = 1f, DamagePerTick = 200f, DamageType = DamageType.Explosive };
            _nuke = new SpellStats { Name = "Nuke", Offensive = true, Effect = SpellEffect.Damage, Cost = 300, CooldownRounds = 4, MaxOwnedCopies = 1, Radius = 12f, DelaySeconds = 3f, DamagePerTick = 2000f, DamageType = DamageType.Explosive };
            _dome = new SpellStats { Name = "Dome", Effect = SpellEffect.Buff, Cost = 100, CooldownRounds = 1, MaxOwnedCopies = 1, Radius = 8f, ShieldBonusShare = 0.5f };
            _pods = new SpellStats { Name = "Pods", Offensive = true, Effect = SpellEffect.DropSquad, Cost = 150, CooldownRounds = 2, MaxOwnedCopies = 1, DropUnit = _infantry, DelaySeconds = 0.5f };
            foreach (FactionInfo faction in factions) faction.Spells.AddRange(new[] { _strike, _nuke, _dome, _pods });
            return factions;
        }

        private Match NewMatch(IMatchAi enemy = null, int seed = 1)
        {
            var match = new Match(TestMatch.RulesWithCards(), TestRules.Default(), Factions(), seed, enemy ?? new DoNothingAi());
            match.ChooseDraft(0);
            return match;
        }

        /// <summary>Plays the battle and goes on to the next round (the card screen, from round 2).</summary>
        private static void PlayRound(Match match)
        {
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();
        }

        private static void SkipIfOffered(Match match)
        {
            if (match.Phase == MatchPhase.Cards) Assert.IsTrue(match.SkipCards(Team.A).Ok);
        }

        // ------------------------------------------------------------------ offers

        [Test]
        public void Offers_StartInRound2WithSpells_ThenAlternate()
        {
            Match match = NewMatch();
            Assert.AreEqual(MatchPhase.Deployment, match.Phase, "No cards in round 1.");
            Assert.IsNull(match.Player.Offer);

            PlayRound(match);
            Assert.AreEqual(MatchPhase.Cards, match.Phase, "Round 2 starts with the card screen.");
            Assert.AreEqual(CardKind.Spell, match.Player.Offer.Kind);
            Assert.AreEqual(3, match.Player.Offer.Count);
            Assert.IsFalse(match.Buy(Team.A, _infantry).Ok, "No deployment before the card is decided.");
            match.SkipCards(Team.A);

            PlayRound(match);
            Assert.AreEqual(CardKind.Reinforcement, match.Player.Offer.Kind, "Round 3: reinforcements.");
            match.SkipCards(Team.A);
            PlayRound(match);
            Assert.AreEqual(CardKind.Spell, match.Player.Offer.Kind, "Round 4: spells again.");
        }

        [Test]
        public void Skipping_GivesTheBonus_50ForSpells_100ForReinforcements()
        {
            Match match = NewMatch();
            PlayRound(match);
            int before = match.Player.Credits;
            Assert.IsTrue(match.SkipCards(Team.A).Ok);
            Assert.AreEqual(before + 50, match.Player.Credits);
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
            Assert.IsFalse(match.SkipCards(Team.A).Ok, "Only once.");

            PlayRound(match);
            before = match.Player.Credits;
            match.SkipCards(Team.A);
            Assert.AreEqual(before + 100, match.Player.Credits);
        }

        [Test]
        public void TakingASpell_PaysItsPrice_AndItIsReadyThisRound()
        {
            Match match = NewMatch();
            PlayRound(match);
            CardOffer offer = match.Player.Offer;
            int index = offer.Spells.IndexOf(_strike);
            if (index < 0) index = 0;
            SpellStats taken = offer.Spells[index];
            int before = match.Player.Credits;
            Assert.IsTrue(match.ChooseCard(Team.A, index).Ok);
            Assert.AreEqual(before - taken.Cost, match.Player.Credits);
            OwnedSpell owned = match.Player.Spells.Single();
            Assert.AreSame(taken, owned.Stats);
            Assert.IsTrue(owned.IsReady(match.Round));
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
        }

        [Test]
        public void ACardYouCannotAfford_CannotBeTaken()
        {
            Match match = NewMatch();
            PlayRound(match);
            match.Player.Credits = 10;
            Assert.IsFalse(match.ChooseCard(Team.A, 0).Ok);
            Assert.AreEqual(MatchPhase.Cards, match.Phase, "Still deciding.");
        }

        [Test]
        public void SpellsAtTheirMaximum_AreNoLongerOffered_AndOffersCanHaveFewerCards()
        {
            Match match = NewMatch();
            match.Player.Spells.Add(new OwnedSpell { Id = 100, Stats = _nuke });
            match.Player.Spells.Add(new OwnedSpell { Id = 101, Stats = _dome });
            match.Player.Spells.Add(new OwnedSpell { Id = 102, Stats = _strike }); // Strike allows 2 copies: still offered.
            PlayRound(match);
            CardOffer offer = match.Player.Offer;
            CollectionAssert.AreEquivalent(new[] { _strike, _pods }, offer.Spells, "Only 2 eligible spells: 2 cards.");
        }

        // ------------------------------------------------------------------ cooldowns and placement

        [Test]
        public void ASpellWithA1RoundCooldown_CanBeUsedEveryOtherRound()
        {
            Match match = NewMatch();
            var strike = new OwnedSpell { Id = 100, Stats = _strike, ReadyRound = 1 };
            match.Player.Spells.Add(strike);
            Vec2 target = new Vec2(0f, 30f);

            Assert.IsTrue(match.PlaceSpell(Team.A, strike.Id, target).Ok, "Round 1: ready.");
            BattleSimulation battle = match.StartBattle();
            Assert.AreEqual(1, battle.Spells.Count, "Cast when the battle starts.");
            Assert.AreEqual(target.X, battle.Spells[0].Position.X, 1e-4f);
            Assert.IsFalse(strike.IsPlaced, "The placement is used up.");
            battle.Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            SkipIfOffered(match);

            Assert.AreEqual(2, match.Round);
            Assert.IsFalse(strike.IsReady(match.Round), "Round 2: recharging.");
            Assert.IsFalse(match.PlaceSpell(Team.A, strike.Id, target).Ok);
            PlayRound(match);
            SkipIfOffered(match);
            Assert.IsTrue(strike.IsReady(match.Round), "Round 3: ready again.");
            Assert.IsTrue(match.PlaceSpell(Team.A, strike.Id, target).Ok);
        }

        [Test]
        public void DefensiveSpells_OnlyOnYourOwnSide_OffensiveSpellsAnywhere()
        {
            Match match = NewMatch();
            var dome = new OwnedSpell { Id = 100, Stats = _dome };
            var strike = new OwnedSpell { Id = 101, Stats = _strike };
            match.Player.Spells.Add(dome);
            match.Player.Spells.Add(strike);
            CombatRules rules = match.CombatRules;
            Vec2 ownZone = new Vec2(0f, -30f), enemyZone = new Vec2(0f, 30f), noMansLand = new Vec2(0f, 0f), outside = new Vec2(0f, rules.ArenaLength);

            Assert.IsTrue(match.CanPlaceSpell(Team.A, dome, ownZone));
            Assert.IsFalse(match.CanPlaceSpell(Team.A, dome, enemyZone));
            Assert.IsFalse(match.CanPlaceSpell(Team.A, dome, noMansLand));
            Assert.IsTrue(match.CanPlaceSpell(Team.A, strike, enemyZone));
            Assert.IsTrue(match.CanPlaceSpell(Team.A, strike, ownZone));
            Assert.IsFalse(match.CanPlaceSpell(Team.A, strike, outside));
            Assert.IsTrue(match.CanPlaceSpell(Team.B, new OwnedSpell { Stats = _dome }, enemyZone) == false, "Not Team B's spell.");

            Assert.IsFalse(match.PlaceSpell(Team.A, dome.Id, enemyZone).Ok);
            Assert.IsTrue(match.PlaceSpell(Team.A, dome.Id, ownZone).Ok);
            Assert.IsTrue(match.PlaceSpell(Team.A, dome.Id, ownZone + new Vec2(5f, 0f)).Ok, "Placing again moves it.");
            Assert.AreEqual(5f, dome.Position.X, 1e-4f);
            Assert.IsTrue(match.UnplaceSpell(Team.A, dome.Id).Ok);
            Assert.IsFalse(dome.IsPlaced);
            Assert.IsTrue(dome.IsReady(match.Round), "Taking it back keeps it ready.");
        }

        [Test]
        public void PlacedSpells_AreHiddenFromTheOpponent_UntilTheBattle()
        {
            Match match = NewMatch();
            var strike = new OwnedSpell { Id = 100, Stats = _strike };
            match.Player.Spells.Add(strike);
            match.PlaceSpell(Team.A, strike.Id, new Vec2(0f, 30f));
            var enemyView = new AiView(match, Team.B);
            Assert.IsEmpty(enemyView.Spells, "The AI only sees its own spells.");
            Assert.IsFalse(enemyView.PlaceSpell(strike, new Vec2(0f, -30f)).Ok, "And cannot use the player's.");
        }

        // ------------------------------------------------------------------ reinforcements

        [Test]
        public void ReinforcementPricing_FollowsTheBiblesExamples()
        {
            UnitStats warthog = TestUnits.Unit(name: "Warthog", cost: 200);
            UnitStats grunt = TestUnits.Unit(name: "Grunt", cost: 100);
            UnitStats walker = TestUnits.Unit(name: "Walker", cost: 450);

            ReinforcementCard twoWarthogs = ReinforcementCard.Make(warthog, 1, 300);
            Assert.AreEqual(2, twoWarthogs.Squads, "300 budget: a level 1 Warthog (200) becomes 2 Warthogs (400).");
            Assert.AreEqual(100, twoWarthogs.Price);

            ReinforcementCard grunts = ReinforcementCard.Make(grunt, 2, 400);
            Assert.AreEqual(150, grunts.SquadWorth, "A level 2 squad is worth its price plus half.");
            Assert.AreEqual(3, grunts.Squads, "400 budget: 3 level-2 Grunt squads (450).");
            Assert.AreEqual(50, grunts.Price);

            ReinforcementCard exact = ReinforcementCard.Make(grunt, 1, 300);
            Assert.AreEqual(0, exact.Price, "Exactly the budget: free.");

            ReinforcementCard over = ReinforcementCard.Make(walker, 1, 300);
            Assert.AreEqual(1, over.Squads, "A single squad already over the budget stays one squad.");
            Assert.AreEqual(150, over.Price);
        }

        [Test]
        public void ReinforcementBudget_AndLevels_GrowWithTheRounds()
        {
            MatchRules rules = TestMatch.RulesWithCards();
            Assert.AreEqual(350, rules.ReinforcementBudget(3), "200 + 50 per round.");
            Assert.AreEqual(1, rules.ReinforcementLevelCap(3));
            Assert.AreEqual(2, rules.ReinforcementLevelCap(5));
            Assert.AreEqual(3, rules.ReinforcementLevelCap(9));
            Assert.AreEqual(3, rules.ReinforcementLevelCap(30), "Capped at level 3.");
        }

        [Test]
        public void TakingAReinforcement_UnlocksTheUnit_PlacesMovableSquads_OutsideTheLimits()
        {
            Match match = NewMatch();
            PlayRound(match);
            match.SkipCards(Team.A);
            PlayRound(match); // Round 3: reinforcements.
            CardOffer offer = match.Player.Offer;
            Assert.AreEqual(CardKind.Reinforcement, offer.Kind);
            match.Player.Credits = 5000;
            match.Player.UnlocksThisRound = match.Rules.UnlocksPerRound; // Already used this round's unlock.
            int index = offer.Reinforcements.FindIndex(c => !match.Player.Unlocked.Contains(c.Type));
            if (index < 0) index = 0;
            ReinforcementCard card = offer.Reinforcements[index];
            int squadsBefore = match.Player.Squads.Count;

            Assert.IsTrue(match.ChooseCard(Team.A, index).Ok);
            Assert.AreEqual(5000 - card.Price, match.Player.Credits);
            Assert.IsTrue(match.Player.Unlocked.Contains(card.Type), "It unlocks the unit, even past the unlock limit.");
            var added = match.Player.Squads.Skip(squadsBefore).ToList();
            Assert.AreEqual(card.Squads, added.Count);
            foreach (PlacedSquad squad in added)
            {
                Assert.AreSame(card.Type, squad.Type);
                Assert.AreEqual(card.Level, squad.Level);
                Assert.AreEqual(0f, squad.Xp, "Starts with 0 XP.");
                Assert.IsTrue(squad.FromCard);
                Assert.IsTrue(match.CanMove(squad), "Placed like squads bought this round.");
            }
            Assert.AreEqual(0, match.Player.SquadsBoughtThisRound, "Not counted toward the squads per round.");
            Assert.IsTrue(match.Buy(Team.A, _infantry).Ok);
            Assert.IsTrue(match.Buy(Team.A, _infantry).Ok, "Both normal purchases are still available.");

            PlacedSquad first = added[0];
            Assert.AreEqual(first.PricePaid, match.SellValue(first), "Before its first battle it sells for what was paid for it.");
        }

        [Test]
        public void ReinforcementSquads_AfterABattle_SellForHalfTheirWorth()
        {
            Match match = NewMatch();
            match.Player.Squads.Add(new PlacedSquad { Id = 900, Type = _tank, Column = 10, Row = 20, RoundBought = 1, PricePaid = 0, FromCard = true, Level = 2, GrantedLevelWorth = 150, HasFought = true });
            PlacedSquad squad = match.Player.FindSquad(900);
            Assert.AreEqual((300 + 150) / 2, match.SellValue(squad));
        }

        // ------------------------------------------------------------------ results and AI

        [Test]
        public void SpellKills_AppearInWhatKilledWhat()
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats weak = TestUnits.Unit(name: "Weak", health: 50f, speed: 0f, filter: TargetFilter.Air);
            sim.AddUnit(weak, Team.B, new Vec2(0f, 30f));
            sim.AddUnit(weak, Team.B, new Vec2(1.5f, 30f));
            sim.AddUnit(TestUnits.Unit(name: "Far", speed: 0f, filter: TargetFilter.Air), Team.A, new Vec2(0f, -40f));
            sim.AddSpell(new SpellStats { Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Radius = 5f, DamagePerTick = 100f, DamageType = DamageType.Explosive }, Team.A, new Vec2(0f, 30f));
            sim.Run();
            KillCount kills = RoundSummary.CountKills(sim).Single();
            Assert.AreEqual("Strike", kills.KillerType);
            Assert.AreEqual(Team.A, kills.KillerTeam);
            Assert.AreEqual(2, kills.Kills);
        }

        [TestCase(AiDifficulty.Normal, AiDifficulty.Normal)]
        [TestCase(AiDifficulty.Hard, AiDifficulty.Easy)]
        public void AFullMatchWithCards_BetweenTwoAis_Ends_AndTheyUseCards(AiDifficulty playerSide, AiDifficulty enemySide)
        {
            var enemy = new RuleBasedAi(enemySide, 21);
            var player = new RuleBasedAi(playerSide, 22);
            var match = new Match(TestMatch.RulesWithCards(), TestRules.Default(), Factions(), 21, enemy);
            match.ChooseDraft(player.ChooseDraftCard(new AiView(match, Team.A), match.PlayerDraft));
            int rounds = 0, spellsCast = 0;
            while (match.Phase != MatchPhase.MatchOver && rounds < 60)
            {
                var view = new AiView(match, Team.A);
                if (match.Phase == MatchPhase.Cards)
                {
                    int choice = player.ChooseCard(view, match.Player.Offer);
                    if (choice < 0 || !match.ChooseCard(Team.A, choice).Ok) match.SkipCards(Team.A);
                }
                player.Deploy(view);
                BattleSimulation battle = match.StartBattle();
                spellsCast += battle.Spells.Count;
                battle.Run();
                match.FinishBattle();
                if (match.Phase == MatchPhase.Result) match.ContinueAfterResult();
                rounds++;
            }
            Assert.AreEqual(MatchPhase.MatchOver, match.Phase, "The match ended.");
            Assert.IsTrue(match.Winner.HasValue);
            Assert.Greater(match.Player.Spells.Count + match.Enemy.Spells.Count, 0, "Spells were taken.");
            Assert.Greater(spellsCast, 0, "Spells were cast.");
            Assert.IsTrue(match.Player.Squads.Concat(match.Enemy.Squads).Any(s => s.FromCard) || rounds < 4, "Reinforcements were taken (in a match that reached round 3).");
        }
    }
}
