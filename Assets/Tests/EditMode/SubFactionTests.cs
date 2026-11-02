// SubFactionTests.cs
// The sub-factions as the match and the simulation see them (M10, Bible Section 8.4): exclusive units belong only to their
// own sub-faction, the draft and reinforcement cards offer them, the AI values them, and the two general mechanics
// the new units brought along (a weapon whose hit slows, lifesteal from a unit's own data) do what they say.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, TestMatch, TestUnits, TestRules, NUnit.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SubFactionTests
    {
        private static UnitStats Exclusive(string name, int tier = 1, int unlock = 0, int cost = 100)
        {
            UnitStats unit = TestMatch.Infantry();
            unit.Name = name;
            unit.Tier = tier;
            unit.UnlockCost = unlock;
            unit.Cost = cost;
            return unit;
        }

        /// <summary>Two factions with two sub-factions each, each with an exclusive unit of its own.</summary>
        private static List<FactionInfo> Factions(out UnitStats first, out UnitStats second)
        {
            var roster = new List<UnitStats> { TestMatch.Infantry(), TestMatch.Buggy() };
            FactionInfo alpha = TestMatch.Faction("Alpha", roster, "One", "Two");
            FactionInfo beta = TestMatch.Faction("Beta", roster, "Three");
            first = Exclusive("First");
            second = Exclusive("Second");
            alpha.SubFactions[0].ExclusiveUnits.Add(first);
            alpha.SubFactions[1].ExclusiveUnits.Add(second);
            beta.SubFactions[0].ExclusiveUnits.Add(Exclusive("Third"));
            return new List<FactionInfo> { alpha, beta };
        }

        private static int CardFor(Match match, string subFaction) => match.PlayerDraft.FindIndex(c => c.SubFaction.Name == subFaction);

        [Test]
        public void ExclusiveUnits_BelongOnlyToTheirSubFaction()
        {
            var match = new Match(TestMatch.Rules(), TestRules.Default(), Factions(out UnitStats first, out UnitStats second), 1, new DoNothingAi());
            Assert.IsTrue(match.ChooseDraft(CardFor(match, "One")).Ok);
            CollectionAssert.Contains(match.Player.Roster, first);
            CollectionAssert.DoesNotContain(match.Player.Roster, second);
            Assert.AreEqual("Alpha (One)", match.Player.Label);

            ActionResult other = match.Unlock(Team.A, second);
            Assert.IsFalse(other.Ok, "Another sub-faction's unit cannot be unlocked.");
            StringAssert.Contains("not in your faction's roster", other.Message);
        }

        [Test]
        public void ReinforcementCards_OfferExclusiveUnits_OnlyOfTheirOwnSubFaction()
        {
            bool offeredFirst = false;
            for (int seed = 0; seed < 40 && !offeredFirst; seed++)
            {
                var player = new PlayerState(Team.A);
                List<FactionInfo> factions = Factions(out UnitStats first, out UnitStats second);
                player.Faction = factions[0];
                player.SubFaction = factions[0].SubFactions[0];
                CardOffer offer = CardDeck.Draw(player, TestMatch.RulesWithCards(), 4, new SimRandom(seed));
                Assert.AreEqual(CardKind.Reinforcement, offer.Kind);
                foreach (ReinforcementCard card in offer.Reinforcements)
                {
                    Assert.AreNotSame(second, card.Type, "Never another sub-faction's unit.");
                    if (card.Type == first) offeredFirst = true;
                }
            }
            Assert.IsTrue(offeredFirst, "Reinforcements include the sub-faction's exclusive units.");
        }

        [Test]
        public void Reinforcement_UnlocksAnExpensiveExclusiveUnit()
        {
            UnitStats pricey = Exclusive("Pricey", tier: 3, unlock: 225, cost: 450);
            FactionInfo faction = TestMatch.Faction("Alpha", new List<UnitStats> { TestMatch.Infantry() }, "One");
            faction.SubFactions[0].ExclusiveUnits.Add(pricey);
            var factions = new List<FactionInfo> { faction };
            var match = new Match(TestMatch.RulesWithCards(), TestRules.Default(), factions, 3, new DoNothingAi());
            match.ChooseDraft(0);
            for (int round = 1; round < 4; round++)
            {
                match.StartBattle().Run();
                match.FinishBattle();
                if (match.Phase != MatchPhase.Result) Assert.Inconclusive("The match ended early.");
                match.ContinueAfterResult();
                if (round < 3) match.SkipCards(Team.A);
            }
            Assert.AreEqual(4, match.Round);
            CardOffer offer = match.Player.Offer;
            Assert.AreEqual(CardKind.Reinforcement, offer.Kind);
            int index = offer.Reinforcements.FindIndex(c => c.Type == pricey);
            Assert.GreaterOrEqual(index, 0, "With 2 types and 3 cards, every type is offered.");
            match.Player.Credits += offer.PriceOf(index);
            Assert.IsTrue(match.ChooseCard(Team.A, index).Ok);
            Assert.IsTrue(match.Player.Unlocked.Contains(pricey));
            Assert.IsTrue(match.Player.Squads.Exists(s => s.Type == pricey && s.FromCard));
        }

        [Test]
        public void NormalAi_WantsItsExclusiveUnits_AtTheDraftAndInTheShop()
        {
            // A card whose squads are worth the same, but one of them exclusive, is worth more to it.
            UnitStats plain = TestMatch.Infantry(), special = Exclusive("Special");
            var withExclusive = new SubFactionInfo { Name = "One" };
            withExclusive.ExclusiveUnits.Add(special);
            var cards = new List<DraftCard>
            {
                new DraftCard { SubFaction = new SubFactionInfo { Name = "Two" }, Squads = { plain, plain } },
                new DraftCard { SubFaction = withExclusive, Squads = { plain, special } },
            };
            Assert.AreEqual(1, new RuleBasedAi(AiDifficulty.Normal, 1).ChooseDraftCard(null, cards));

            // Both unit types are the same but for being exclusive: the credits go on the exclusive one.
            int exclusiveBought = 0, plainBought = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                FactionInfo faction = TestMatch.Faction("Alpha", new List<UnitStats> { TestMatch.Infantry() }, "One");
                UnitStats wanted = Exclusive("Wanted");
                faction.SubFactions[0].ExclusiveUnits.Add(wanted);
                var match = new Match(TestMatch.Rules(), TestRules.Default(), new List<FactionInfo> { faction }, seed, new RuleBasedAi(AiDifficulty.Normal, seed));
                match.ChooseDraft(0);
                foreach (PlacedSquad squad in match.Enemy.Squads.Where(s => !s.FromDraft))
                {
                    if (squad.Type == wanted) exclusiveBought++;
                    else plainBought++;
                }
            }
            Assert.Greater(exclusiveBought, plainBought);
        }

        [Test]
        public void AHitThatSlows_SlowsItsTarget_ThenWearsOff()
        {
            WeaponStats emp = TestUnits.Weapon(damage: 1f, range: 30f, interval: 100f);
            emp.HitSlowSeconds = 2f;
            emp.HitSlowShare = 0.4f;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(TestUnits.Unit(name: "Gun", weapon: emp, health: 1000f), Team.A, new Vec2(0f, 0f));
            SimUnit target = sim.AddUnit(TestUnits.Unit(name: "Target", health: 1000f, speed: 3f, weapon: TestUnits.Weapon(damage: 0f, range: 1f)), Team.B, new Vec2(0f, 28f));

            for (int i = 0; i < 15; i++) sim.Step();
            Assert.AreEqual(0.6f, target.SpeedMultiplier, 1e-4f, "40% slower.");
            for (int i = 0; i < 60; i++) sim.Step();
            Assert.AreEqual(1f, target.SpeedMultiplier, 1e-4f, "It wears off after 2 seconds, with no new hit.");
        }

        [Test]
        public void AHitThatSlows_DoesNotSlowATitan()
        {
            WeaponStats emp = TestUnits.Weapon(damage: 1f, range: 30f, interval: 0.5f);
            emp.HitSlowSeconds = 2f;
            emp.HitSlowShare = 0.4f;
            UnitStats titan = TestUnits.Unit(name: "Titan", health: 1000f, speed: 1f, weapon: TestUnits.Weapon(damage: 0f, range: 1f));
            titan.Role = UnitRole.Titan;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(TestUnits.Unit(name: "Gun", weapon: emp, health: 1000f), Team.A, new Vec2(0f, 0f));
            SimUnit target = sim.AddUnit(titan, Team.B, new Vec2(0f, 20f));
            for (int i = 0; i < 30; i++) sim.Step();
            Assert.Greater(target.SecondsSinceDamaged, -1f);
            Assert.Less(target.Health, 1000f, "It is being hit.");
            Assert.AreEqual(1f, target.SpeedMultiplier, 1e-4f);
        }

        [Test]
        public void Validate_RejectsANonsenseHitSlow()
        {
            WeaponStats emp = TestUnits.Weapon();
            emp.HitSlowSeconds = 1f;
            emp.HitSlowShare = 1.5f;
            Assert.IsNotEmpty(TestUnits.Unit(weapon: emp).Validate());
        }

        [Test]
        public void LifestealFromData_HealsTheUnit_AndAnItemDoesNotTakeItAway()
        {
            UnitStats captain = TestUnits.Unit(name: "Captain", health: 1000f, weapon: TestUnits.Weapon(damage: 100f, range: 30f, interval: 1f));
            captain.LifestealShare = 0.3f;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit healer = sim.AddUnit(captain, Team.A, Vec2.Zero);
            healer.Health = 500f;
            sim.AddUnit(TestUnits.Unit(name: "Target", health: 10000f, weapon: TestUnits.Weapon(damage: 0f, range: 1f), speed: 0f), Team.B, new Vec2(0f, 20f));
            for (int i = 0; i < 20; i++) sim.Step();
            Assert.AreEqual(530f, healer.Health, 1e-2f, "One hit of 100 gives back 30.");

            UnitStats withItem = UnitUpgrades.Apply(captain, 1, null, new ItemStats { Name = "Weak", LifestealShare = 0.1f }, null);
            Assert.AreEqual(0.3f, withItem.LifestealShare, 1e-5f, "The stronger of the two.");
            UnitStats strongItem = UnitUpgrades.Apply(captain, 1, null, new ItemStats { Name = "Strong", LifestealShare = 0.5f }, null);
            Assert.AreEqual(0.5f, strongItem.LifestealShare, 1e-5f);
        }

        /// <summary>A unit whose +25% health tech is switched off by an EMP, from the first step on, so that its health is over its maximum for now.</summary>
        private static SimUnit WithTechOff(BattleSimulation sim, UnitStats type, Vec2 position)
        {
            UnitStats upgraded = UnitUpgrades.Apply(type, 1, new[] { new TechUpgrade { Name = "Bond Armor", Cost = 250, HealthMultiplier = 1.25f } }, null, null);
            SimUnit unit = sim.AddUnit(upgraded, Team.A, position);
            unit.TechsDisabledUntil = 9999f;
            return unit;
        }

        [Test]
        public void Lifesteal_UnderAnEmp_DoesNotTakeHealthAway()
        {
            UnitStats captain = TestUnits.Unit(name: "Captain", health: 2000f, weapon: TestUnits.Weapon(damage: 72f, range: 30f, interval: 0.75f), speed: 0f);
            captain.LifestealShare = 0.3f;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            SimUnit unit = WithTechOff(sim, captain, Vec2.Zero);
            sim.AddUnit(TestUnits.Unit(name: "Target", health: 100000f, weapon: TestUnits.Weapon(damage: 0f, range: 1f), speed: 0f), Team.B, new Vec2(0f, 20f));
            Assert.AreEqual(2500f, unit.Health, 1e-2f);
            float targetHealth = 100000f;
            for (int i = 0; i < 60; i++) sim.Step();
            Assert.Less(sim.Units[1].Health, targetHealth, "It has hit its target by now.");
            Assert.AreEqual(2000f, unit.Stats.MaxHealth, 1e-2f, "The EMP has switched the tech off.");
            Assert.AreEqual(2500f, unit.Health, 1e-2f, "Above its maximum for now: no heal, and no loss either.");

            // And below the lower maximum it still heals, up to that maximum and no further.
            unit.Health = 1990f;
            for (int i = 0; i < 200 && unit.Health < 2000f; i++) sim.Step();
            Assert.AreEqual(2000f, unit.Health, 1e-2f);
        }

        [Test]
        public void HealingAura_UnderAnEmp_DoesNotTakeHealthAway()
        {
            UnitStats goblin = TestUnits.Unit(name: "Goblin", health: 1000f, weapon: TestUnits.Weapon(damage: 0f, range: 1f), speed: 0f);
            goblin.AuraRadius = 14f;
            goblin.AuraSpeedMultiplier = 1.2f;
            goblin.AuraHealPerSecondShare = 0.01f;
            var sim = new BattleSimulation(TestRules.Default(), 1);
            sim.AddUnit(goblin, Team.A, Vec2.Zero);
            SimUnit friend = WithTechOff(sim, TestUnits.Unit(name: "Friend", health: 2000f, weapon: TestUnits.Weapon(damage: 0f, range: 1f), speed: 0f), new Vec2(5f, 0f));
            sim.AddUnit(TestUnits.Unit(name: "Far", health: 100000f, weapon: TestUnits.Weapon(damage: 0f, range: 1f), speed: 0f), Team.B, new Vec2(0f, 60f));
            for (int i = 0; i < 10; i++) sim.Step();
            Assert.AreEqual(2000f, friend.Stats.MaxHealth, 1e-2f, "The EMP has switched the tech off.");
            Assert.AreEqual(2500f, friend.Health, 1e-2f, "Above its maximum for now: no heal, and no loss either.");

            friend.Health = 1000f;
            for (int i = 0; i < 10; i++) sim.Step();
            Assert.Greater(friend.Health, 1000f, "Below the lower maximum the aura still heals.");
            Assert.Less(friend.Health, 2000f);
        }
    }
}
