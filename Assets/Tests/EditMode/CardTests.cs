// CardTests.cs
// Cards (Bible Section 10, Milestone 7, decisions D32 and D33): general offers in rounds 2 and 3,
// reinforcements from round 4 every other round, cards that only appear late, skip bonuses, spell
// prices, maximum copies, cooldowns ("a 1-round cooldown can be used every other round"), placement
// rules (offensive anywhere, defensive only on your own side), spells cast when the battle starts and
// hidden from the opponent, reinforcement budget and pricing (the Bible's examples), reinforcement
// squads (unlock, outside the squad limit, 0 XP, movable, refunds), spell kills in the round summary,
// items (inventory, one per squad, whole squad, Deployment Module), economy and buff cards, Intensive
// Training, and the AI taking cards and using them in full matches.
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
        private CardStats _strikeCard, _nukeCard, _domeCard, _podsCard, _heavyArmorCard, _moduleCard, _supplyCard, _tacticsCard, _slotCard, _trainingCard;
        private UnitStats _infantry, _buggy, _tank, _walker;

        [Test]
        public void SpellText_ReadsRightForOneSquadOrMore_AndKeepsNumbersWithTheirUnits()
        {
            var unit = new UnitStats { Name = "ODST" };
            Assert.AreEqual("1\u00A0x ODST squad lands for this battle", new SpellStats { Effect = SpellEffect.DropSquad, DropUnit = unit, DropSquads = 1 }.EffectsText());
            Assert.AreEqual("2\u00A0x ODST squads land for this battle", new SpellStats { Effect = SpellEffect.DropSquad, DropUnit = unit, DropSquads = 2 }.EffectsText());

            var spells = new[]
            {
                new SpellStats { Effect = SpellEffect.Damage, DamagePerTick = 50f, Ticks = 4, TickInterval = 2f, Radius = 8f },
                new SpellStats { Effect = SpellEffect.Damage, DamagePerTick = 50f, Radius = 8f },
                new SpellStats { Effect = SpellEffect.Buff, ShieldBonusShare = 0.5f, Radius = 8f },
                new SpellStats { Effect = SpellEffect.Disrupt, ShieldDamage = 100f, TechDisableSeconds = 10f, SlowShare = 0.4f, SlowSeconds = 10f, Radius = 10f },
                new SpellStats { Effect = SpellEffect.Smoke, AreaDepth = 8f, AreaWidth = 40f, DurationSeconds = 15f, RangeMultiplier = 0.5f },
            };
            foreach (SpellStats spell in spells)
            {
                string text = spell.EffectsText();
                StringAssert.DoesNotMatch(@"\d [ms]\b", text, "A line must not end between a number and its unit.");
                StringAssert.IsMatch("\\d\u00A0[ms]\\b", text);
            }
            StringAssert.Contains("8\u00A0x\u00A040\u00A0m line", spells[4].EffectsText());
            Assert.AreEqual("+2\u00A0m range for all ranged units", new CardStats { Type = CardType.Buff, AllDamageMultiplier = 1f, AllHealthMultiplier = 1f, AllRangeBonus = 2f }.EffectsText());
        }

        private List<FactionInfo> Factions()
        {
            List<FactionInfo> factions = TestMatch.StandardFactions(out _infantry, out _buggy, out _tank, out _walker);
            _strike = new SpellStats { Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Cost = 100, CooldownRounds = 1, MaxOwnedCopies = 2, Radius = 5f, DelaySeconds = 1f, DamagePerTick = 200f, DamageType = DamageType.Explosive };
            _nuke = new SpellStats { Name = "Nuke", Offensive = true, Effect = SpellEffect.Damage, Cost = 300, CooldownRounds = 4, MaxOwnedCopies = 1, Radius = 12f, DelaySeconds = 3f, DamagePerTick = 2000f, DamageType = DamageType.Explosive };
            _dome = new SpellStats { Name = "Dome", Effect = SpellEffect.Buff, Cost = 100, CooldownRounds = 1, MaxOwnedCopies = 1, Radius = 8f, ShieldBonusShare = 0.5f };
            _pods = new SpellStats { Name = "Pods", Offensive = true, Effect = SpellEffect.DropSquad, Cost = 150, CooldownRounds = 2, MaxOwnedCopies = 1, DropUnit = _infantry, DelaySeconds = 0.5f };
            _strikeCard = CardStats.FromSpell(_strike);
            _nukeCard = CardStats.FromSpell(_nuke);
            _nukeCard.MinRound = 7;
            _domeCard = CardStats.FromSpell(_dome);
            _podsCard = CardStats.FromSpell(_pods);
            foreach (FactionInfo faction in factions) faction.Cards.AddRange(new[] { _strikeCard, _nukeCard, _domeCard, _podsCard });
            _heavyArmorCard = new CardStats { Name = "Heavy Armor", Type = CardType.Item, Cost = 50, MaxOwnedCopies = 99, Item = new ItemStats { Name = "Heavy Armor", HealthMultiplier = 1.7f } };
            _moduleCard = new CardStats { Name = "Deployment Module", Type = CardType.Item, Cost = 0, MaxOwnedCopies = 99, Item = new ItemStats { Name = "Deployment Module", FreeDeployment = true } };
            _supplyCard = new CardStats { Name = "Supply Enhancement", Type = CardType.Economy, Cost = 100, BonusIncome = 50 };
            _tacticsCard = new CardStats { Name = "Advanced Offensive Tactics", Type = CardType.Buff, Cost = 350, MinRound = 7, AllDamageMultiplier = 1.3f };
            _slotCard = new CardStats { Name = "Additional Deployment Slot", Type = CardType.Buff, Cost = 100, MinRound = 5, ExtraSquadsPerRound = 1 };
            _trainingCard = new CardStats { Name = "Intensive Training", Type = CardType.Training, Cost = 50, CooldownRounds = 1 };
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
        public void Offers_GeneralInRounds2And3_ReinforcementsFromRound4_EveryOtherRound()
        {
            Match match = NewMatch();
            Assert.AreEqual(MatchPhase.Deployment, match.Phase, "No cards in round 1.");
            Assert.IsNull(match.Player.Offer);

            PlayRound(match);
            Assert.AreEqual(MatchPhase.Cards, match.Phase, "Round 2 starts with the card screen.");
            Assert.AreEqual(CardKind.General, match.Player.Offer.Kind);
            Assert.AreEqual(3, match.Player.Offer.Count);
            Assert.IsFalse(match.Buy(Team.A, _infantry).Ok, "No deployment before the card is decided.");
            match.SkipCards(Team.A);

            PlayRound(match);
            Assert.AreEqual(CardKind.General, match.Player.Offer.Kind, "Round 3: still no reinforcements.");
            match.SkipCards(Team.A);
            foreach (CardKind expected in new[] { CardKind.Reinforcement, CardKind.General, CardKind.Reinforcement, CardKind.General, CardKind.Reinforcement })
            {
                PlayRound(match);
                Assert.AreEqual(expected, match.Player.Offer.Kind, $"Round {match.Round}");
                match.SkipCards(Team.A);
            }
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
            match.SkipCards(Team.A);
            PlayRound(match); // Round 4: reinforcements.
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
            int index = offer.Cards.FindIndex(c => c.Type == CardType.Spell);
            CardStats taken = offer.Cards[index];
            int before = match.Player.Credits;
            Assert.IsTrue(match.ChooseCard(Team.A, index).Ok);
            Assert.AreEqual(before - taken.Cost, match.Player.Credits);
            OwnedSpell owned = match.Player.Spells.Single();
            Assert.AreSame(taken.Spell, owned.Stats);
            Assert.IsTrue(owned.IsReady(match.Round));
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
        }

        [Test]
        public void ACardYouCannotAfford_CannotBeTaken()
        {
            Match match = NewMatch();
            PlayRound(match);
            match.Player.Credits = 0;
            Assert.IsFalse(match.ChooseCard(Team.A, match.Player.Offer.Cards.FindIndex(c => c.Cost > 0)).Ok);
            Assert.AreEqual(MatchPhase.Cards, match.Phase, "Still deciding.");
        }

        [Test]
        public void SpellsAtTheirMaximum_AreNoLongerOffered_AndOffersCanHaveFewerCards()
        {
            Match match = NewMatch();
            match.Player.Spells.Add(new OwnedSpell { Id = 101, Stats = _dome });
            match.Player.Spells.Add(new OwnedSpell { Id = 102, Stats = _strike }); // Strike allows 2 copies: still offered.
            PlayRound(match);
            CardOffer offer = match.Player.Offer;
            CollectionAssert.AreEquivalent(new[] { _strikeCard, _podsCard }, offer.Cards, "The Nuke waits for round 7, Dome is at its maximum: 2 cards.");
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
            Assert.IsFalse(match.CanPlaceSpell(Team.B, new OwnedSpell { Stats = _dome }, enemyZone), "Not Team B's spell.");

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
            PlayRound(match);
            match.SkipCards(Team.A);
            PlayRound(match); // Round 4: reinforcements.
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

        // ------------------------------------------------------------------ late cards

        [Test]
        public void StrongCards_OnlyAppearFromTheirRound()
        {
            List<FactionInfo> factions = Factions();
            foreach (FactionInfo faction in factions)
            {
                faction.Cards.Clear();
                faction.Cards.AddRange(new[] { _strikeCard, _nukeCard, _tacticsCard, _slotCard });
            }
            var match = new Match(TestMatch.RulesWithCards(), TestRules.Default(), factions, 1, new DoNothingAi());
            match.ChooseDraft(0);
            PlayRound(match);
            CollectionAssert.AreEquivalent(new[] { _strikeCard }, match.Player.Offer.Cards, "Round 2.");
            for (int round = 3; round <= 7; round++)
            {
                match.SkipCards(Team.A);
                PlayRound(match);
                Assert.AreEqual(round, match.Round);
                if (round == 5) CollectionAssert.AreEquivalent(new[] { _strikeCard, _slotCard }, match.Player.Offer.Cards, "The slot from round 5.");
            }
            Assert.AreEqual(3, match.Player.Offer.Count);
            Assert.IsTrue(match.Player.Offer.Cards.All(c => c == _strikeCard || c == _nukeCard || c == _tacticsCard || c == _slotCard), "Round 7: all of them are possible.");
        }

        // ------------------------------------------------------------------ items

        /// <summary>Gives the player a card directly, as if taken from an offer in round 2.</summary>
        private static void Give(Match match, CardStats card)
        {
            match.Player.Offer = new CardOffer { Kind = CardKind.General, Round = match.Round, Cards = new List<CardStats> { card } };
            Assert.IsTrue(match.ChooseCard(Team.A, 0).Ok);
        }

        [Test]
        public void Items_GoToTheInventory_ThenOntoOneSquad_ForTheWholeSquad()
        {
            Match match = NewMatch();
            PlacedSquad squad = match.Player.Squads[0];
            int before = match.Player.Credits;
            Give(match, _heavyArmorCard);
            Assert.AreEqual(before - 50, match.Player.Credits);
            ItemStats armor = _heavyArmorCard.Item;
            CollectionAssert.Contains(match.Player.Inventory, armor);
            Assert.IsNull(squad.Item, "Nothing changes until it is placed.");

            Assert.IsTrue(match.PlaceItem(Team.A, armor, squad.Id).Ok);
            Assert.AreSame(armor, squad.Item);
            CollectionAssert.DoesNotContain(match.Player.Inventory, armor);
            Assert.AreEqual(squad.Type.MaxHealth * 1.7f, match.Player.EffectiveStats(squad).MaxHealth, 1e-3f);

            BattleSimulation battle = match.BuildBattle(includeEnemy: false);
            SimSquad simSquad = battle.Squads[match.Player.Squads.IndexOf(squad)];
            Assert.IsTrue(simSquad.Members.All(u => System.Math.Abs(u.Stats.MaxHealth - squad.Type.MaxHealth * 1.7f) < 1e-3f), "Every body of the squad.");

            Give(match, _heavyArmorCard);
            Assert.IsFalse(match.PlaceItem(Team.A, armor, squad.Id).Ok, "One item per squad.");
            Assert.IsTrue(match.PlaceItem(Team.A, armor, match.Player.Squads[1].Id).Ok, "Another squad can take it.");
        }

        [Test]
        public void SellingASquad_PutsItsItemBackInTheInventory()
        {
            Match match = NewMatch();
            PlacedSquad squad = match.Player.Squads[0];
            Give(match, _heavyArmorCard);
            match.PlaceItem(Team.A, _heavyArmorCard.Item, squad.Id);
            Assert.IsTrue(match.Sell(Team.A, squad.Id).Ok);
            CollectionAssert.Contains(match.Player.Inventory, _heavyArmorCard.Item);
        }

        [Test]
        public void DeploymentModule_LetsItsSquadMove_InEveryRound()
        {
            Match match = NewMatch();
            PlacedSquad carrier = match.Player.Squads[0], other = match.Player.Squads[1];
            Give(match, _moduleCard);
            match.PlaceItem(Team.A, _moduleCard.Item, carrier.Id);
            PlayRound(match);
            match.SkipCards(Team.A);
            Assert.IsTrue(match.CanMove(carrier), "It can still move after fighting.");
            Assert.IsFalse(match.CanMove(other));
            Assert.IsTrue(match.Move(Team.A, carrier.Id, 2, 30, true).Ok, "And turn.");
            Assert.AreEqual(match.Rules.RefundAfterBattle * (carrier.Type.Cost), match.SellValue(carrier), 1f, "It still sells like a squad that has fought.");
        }

        [Test]
        public void ItemsOfTheEnemy_AreSeenOnlyAfterTheBattle()
        {
            Match match = NewMatch();
            PlacedSquad enemy = match.Enemy.Squads[0];
            match.Enemy.Inventory.Add(_heavyArmorCard.Item);
            match.PlaceItem(Team.B, _heavyArmorCard.Item, enemy.Id);
            Assert.IsEmpty(match.SeenArmy(Team.B), "Nothing seen yet.");
            PlayRound(match);
            Assert.AreSame(_heavyArmorCard.Item, match.SeenArmy(Team.B)[0].Item);
            PlayerState seen = match.SeenAsDeployment(Team.B);
            Assert.AreEqual(enemy.Type.MaxHealth * 1.7f, seen.EffectiveStats(seen.Squads[0]).MaxHealth, 1e-3f, "The grey ghost shows it.");
        }

        // ------------------------------------------------------------------ economy and buffs

        [Test]
        public void SupplyEnhancement_AddsIncomeEveryRound_ForThatPlayerOnly_AboveTheCap()
        {
            Match match = NewMatch();
            Give(match, _supplyCard);
            Assert.AreEqual(match.NextIncome + 50, match.NextIncomeFor(Team.A));
            Assert.AreEqual(match.NextIncome, match.NextIncomeFor(Team.B));
            int mine = match.Player.Credits, theirs = match.Enemy.Credits;
            match.StartBattle().Run();
            RoundSummary summary = match.FinishBattle();
            Assert.AreEqual(mine + 400 + 50, match.Player.Credits);
            Assert.AreEqual(theirs + 400, match.Enemy.Credits);
            Assert.AreEqual(450, summary.IncomeAwarded, "The result screen shows the player's income.");

            match.Player.BonusIncome = 150;
            match.ContinueAfterResult();
            match.SkipCards(Team.A);
            Assert.AreEqual(match.Rules.IncomeCap + 150, match.Rules.IncomeForRound(30) + match.Player.BonusIncome, "On top of the income cap.");
        }

        [Test]
        public void BuffCards_ImproveTheWholeArmy_AndOldSquadsToo()
        {
            Match match = NewMatch();
            PlacedSquad squad = match.Player.Squads[0];
            float damage = match.Player.EffectiveStats(squad).Weapons[0].DamagePerShot;
            match.Player.Credits = 1000;
            Give(match, _tacticsCard);
            Assert.AreEqual(damage * 1.3f, match.Player.EffectiveStats(squad).Weapons[0].DamagePerShot, 1e-3f);
        }

        [Test]
        public void AdditionalDeploymentSlot_AllowsOneMoreSquadPerRound()
        {
            Match match = NewMatch();
            match.Player.Credits = 5000;
            Give(match, _slotCard);
            Assert.AreEqual(3, match.SquadsPerRound(Team.A));
            Assert.AreEqual(2, match.SquadsPerRound(Team.B));
            UnitStats type = match.Player.Squads[0].Type;
            Assert.IsTrue(match.Buy(Team.A, type).Ok);
            Assert.IsTrue(match.Buy(Team.A, type).Ok);
            Assert.IsTrue(match.Buy(Team.A, type).Ok, "The third squad this round.");
            Assert.IsFalse(match.Buy(Team.A, type).Ok);
        }

        [Test]
        public void CardsAtTheirMaximum_AreNoLongerOffered()
        {
            List<FactionInfo> factions = Factions();
            foreach (FactionInfo faction in factions)
            {
                faction.Cards.Clear();
                faction.Cards.Add(_supplyCard);
            }
            var match = new Match(TestMatch.RulesWithCards(), TestRules.Default(), factions, 1, new DoNothingAi());
            match.ChooseDraft(0);
            PlayRound(match);
            Assert.IsTrue(match.ChooseCard(Team.A, match.Player.Offer.Cards.IndexOf(_supplyCard)).Ok);
            PlayRound(match);
            CollectionAssert.IsEmpty(match.Player.Offer.Cards, "1 copy allowed: only Skip is left.");
        }

        // ------------------------------------------------------------------ Intensive Training

        [Test]
        public void IntensiveTraining_FillsTheXpBar_ThenRechargesForARound()
        {
            Match match = NewMatch();
            PlacedSquad squad = match.Player.Squads[0], other = match.Player.Squads[1];
            Assert.IsFalse(match.UseTraining(Team.A, squad.Id).Ok, "Not owned yet.");
            Give(match, _trainingCard);
            Assert.IsTrue(match.CanUseTraining(Team.A));
            Assert.IsTrue(match.UseTraining(Team.A, squad.Id).Ok);
            Assert.AreEqual(match.XpNeeded(squad), squad.Xp, 1e-3f);
            Assert.IsTrue(match.CanLevelUp(squad), "It can now be leveled up, if the player pays.");
            Assert.AreEqual(1, squad.Level, "It does not level up by itself.");
            Assert.IsFalse(match.CanUseTraining(Team.A), "Used this round.");

            PlayRound(match);
            match.SkipCards(Team.A);
            Assert.IsFalse(match.UseTraining(Team.A, other.Id).Ok, "Round 2: recharging.");
            PlayRound(match);
            match.SkipCards(Team.A);
            Assert.IsTrue(match.CanUseTraining(Team.A), "Round 3: ready again.");
            Assert.IsFalse(match.UseTraining(Team.A, squad.Id).Ok, "Its bar is already full: not wasted.");
            other.Xp = 0f; // Whatever it earned in the battles.
            Assert.IsTrue(match.UseTraining(Team.A, other.Id).Ok);
        }

        [Test]
        public void TwoCopiesOfIntensiveTraining_RechargeSeparately()
        {
            Match match = NewMatch();
            match.Player.Credits = 1000;
            Give(match, _trainingCard);
            Give(match, _trainingCard);
            Assert.AreEqual(2, match.Player.Trainings.Count);
            Assert.AreEqual(2, match.Player.CopiesOwned(_trainingCard));
            PlacedSquad first = match.Player.Squads[0], second = match.Player.Squads[1];
            Assert.IsTrue(match.UseTraining(Team.A, first.Id).Ok);
            Assert.IsTrue(match.CanUseTraining(Team.A), "The second copy is still ready.");
            Assert.IsTrue(match.UseTraining(Team.A, second.Id).Ok, "Both in the same round.");
            Assert.IsFalse(match.CanUseTraining(Team.A));
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
            Assert.Greater(match.Player.Spells.Count + match.Enemy.Spells.Count + match.Player.CardsTaken.Count + match.Enemy.CardsTaken.Count, 0, "Cards were taken.");
            Assert.Greater(spellsCast, 0, "Spells were cast.");
            Assert.IsTrue(match.Player.Squads.Concat(match.Enemy.Squads).Any(s => s.FromCard) || rounds < 4, "Reinforcements were taken (in a match that reached round 3).");
        }
    }
}
