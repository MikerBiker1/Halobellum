// RuleBasedAiSkirmishTests.cs
// Tests how the AI plays Unlock Skirmish: it deploys at all with the one made-up faction, Hard doesn't field only flyers, Normal and Hard
// don't unlock on a whim, reinforcement squads go where their role belongs, and a squad in a flank zone doesn't waste a spell.
// Each has Faction mode alongside it, which keeps the behaviour it was measured with. The carrier corners trial is here too.
// The roster here is a test fixture with one unit per role, not game balance.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestUnits, TestRules, TestMatch.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class RuleBasedAiSkirmishTests
    {
        private static UnitStats Unit(string name, UnitRole role, int tier, int unlock, int squad, int cost, float health, bool air = false, bool canFlank = false)
        {
            UnitStats unit = TestUnits.Unit(name: name, health: health, speed: 4f, radius: squad > 4 ? 0.5f : 1.2f, air: air, squadSize: squad, cost: cost,
                weapon: TestUnits.Weapon(damage: 10f, range: 12f, interval: 0.5f));
            unit.Role = role;
            unit.Tier = tier;
            unit.UnlockCost = unlock;
            unit.BaseDamage = cost / (float)squad / 5f;
            unit.CanFlank = canFlank;
            return unit;
        }

        private static List<UnitStats> Roster() => new List<UnitStats>
        {
            Unit("Chaff", UnitRole.Chaff, 1, 0, 12, 100, 60),
            Unit("Chaff Clear", UnitRole.ChaffClear, 1, 0, 6, 150, 90),
            Unit("Sniper", UnitRole.SingleTargetDps, 2, 50, 1, 250, 300),
            Unit("Tank", UnitRole.Tank, 2, 50, 1, 300, 1200),
            Unit("Raider", UnitRole.Flanker, 1, 0, 2, 200, 300),
            Unit("Artillery", UnitRole.Artillery, 2, 50, 2, 300, 350),
            Unit("Flak", UnitRole.AntiAir, 2, 50, 1, 300, 500),
            Unit("Gunship", UnitRole.Aerial, 2, 50, 2, 300, 400, air: true, canFlank: true),
            Unit("Titan", UnitRole.Titan, 4, 350, 1, 800, 8000),
        };

        /// <summary>A match in the given mode, not started. In Unlock Skirmish the field is the one made-up faction, as in the game.</summary>
        private static Match NewMatch(MatchMode mode, IMatchAi ai, int seed = 3, List<UnitStats> roster = null, CardFilter cards = CardFilter.AllCards, System.Action<MatchRules> prepare = null)
        {
            MatchRules rules = TestMatch.Rules();
            new MatchSetup { Mode = mode, Cards = cards }.ApplyTo(rules);
            prepare?.Invoke(rules);
            FactionInfo faction = TestMatch.Faction("Alpha", roster ?? Roster(), "Standard");
            if (mode == MatchMode.UnlockSkirmish) faction = SkirmishFaction.Build(new[] { faction });
            return new Match(rules, TestRules.Default(), new List<FactionInfo> { faction }, seed, ai);
        }

        private static void PlayBattle(Match match)
        {
            match.StartBattle().Run();
            match.FinishBattle();
        }

        // ------------------------------------------------------------------ it plays at all

        [TestCase(AiDifficulty.Easy)]
        [TestCase(AiDifficulty.Normal)]
        [TestCase(AiDifficulty.Hard)]
        public void TheAiDeploys_WithTheOneFaction(AiDifficulty difficulty)
        {
            int bought = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var ai = new RuleBasedAi(difficulty, seed);
                Match match = NewMatch(MatchMode.UnlockSkirmish, ai, seed);
                match.ChooseDraft(0);
                Assert.IsTrue(match.Enemy.HasFaction);
                StringAssert.StartsWith("Round 1", ai.LastDecision, "It deployed.");
                bought += match.Enemy.SquadsBoughtThisRound;
            }
            Assert.Greater(bought, 0, "It buys squads.");
        }

        // ------------------------------------------------------------------ flyers and unlocks

        /// <summary>How many of 20 matches have a flyer among Hard's first-round squads, when money doesn't decide.</summary>
        private static int MatchesWithAFlyerInRoundOne(MatchMode mode)
        {
            int flyers = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                Match match = NewMatch(mode, new RuleBasedAi(AiDifficulty.Hard, seed), seed, prepare: rules => rules.StartingCredits = 5000);
                match.ChooseDraft(0);
                if (match.Enemy.Squads.Any(s => s.Type.CanFlank)) flyers++;
            }
            return flyers;
        }

        [Test]
        public void HardsFirstRound_IsNotForcedToAFlyer()
        {
            Assert.AreEqual(20, MatchesWithAFlyerInRoundOne(MatchMode.Faction), "In Faction mode the full bonus decides it.");
            Assert.Less(MatchesWithAFlyerInRoundOne(MatchMode.UnlockSkirmish), 10, "In Unlock Skirmish it is one reason among others.");
        }

        /// <summary>How many more unit types than its starting card gave it the AI has after round 1, with locked units that are dear to unlock.</summary>
        private static int UnlockedInRoundOne(MatchMode mode, AiDifficulty difficulty)
        {
            List<UnitStats> roster = Roster();
            foreach (UnitStats unit in roster) unit.UnlockCost = 2000;
            Match match = NewMatch(mode, new RuleBasedAi(difficulty, 3), roster: roster, prepare: rules => rules.StartingCredits = 5000);
            match.ChooseDraft(0);
            return match.Enemy.Unlocked.Count - 2;
        }

        [TestCase(AiDifficulty.Normal)]
        [TestCase(AiDifficulty.Hard)]
        public void NormalAndHard_DoNotUnlockADearUnit_ThatIsOnlyALittleBetter(AiDifficulty difficulty)
        {
            Assert.AreEqual(1, UnlockedInRoundOne(MatchMode.Faction, difficulty), "Faction mode unlocks whatever scores higher.");
            Assert.AreEqual(0, UnlockedInRoundOne(MatchMode.UnlockSkirmish, difficulty));
        }

        [Test]
        public void Easy_UnlocksAsItAlwaysDid()
        {
            Assert.AreEqual(1, UnlockedInRoundOne(MatchMode.UnlockSkirmish, AiDifficulty.Easy));
        }

        // ------------------------------------------------------------------ reinforcement squads

        /// <summary>Always takes the first card of an offer, around the real AI, so that the test is about where the squads go.</summary>
        private sealed class TakesFirstCardAi : IMatchAi
        {
            private readonly RuleBasedAi _ai;
            public TakesFirstCardAi(RuleBasedAi ai) { _ai = ai; }
            public string Label => _ai.Label;
            public int ChooseDraftCard(AiView view, IReadOnlyList<DraftCard> cards) => _ai.ChooseDraftCard(view, cards);
            public int ChooseCard(AiView view, CardOffer offer) => 0;
            public void Deploy(AiView view) => _ai.Deploy(view);
        }

        /// <summary>The rows of the squads a reinforcement card brought in round 2, leaving out artillery, whose own spot is the middle of the zone.</summary>
        private static List<int> RowsOfNewReinforcements(MatchMode mode)
        {
            var rows = new List<int>();
            for (int seed = 1; seed <= 10; seed++)
            {
                var ai = new TakesFirstCardAi(new RuleBasedAi(AiDifficulty.Normal, seed));
                Match match = NewMatch(mode, ai, seed, cards: CardFilter.OnlyReinforcements, prepare: rules =>
                {
                    rules.FirstCardRound = 2;
                    rules.StartingCredits = 5000;
                    rules.ReinforcementMaxTier = 2;
                });
                match.ChooseDraft(0);
                PlayBattle(match);
                match.ContinueAfterResult();
                rows.AddRange(match.Enemy.Squads.Where(s => s.FromCard && s.Type.Role != UnitRole.Artillery).Select(s => s.Row));
            }
            return rows;
        }

        [Test]
        public void ReinforcementSquads_GoWhereTheirRoleBelongs()
        {
            int middle = TestRules.Default().DeploymentDepthSquares / 2;
            List<int> faction = RowsOfNewReinforcements(MatchMode.Faction), skirmish = RowsOfNewReinforcements(MatchMode.UnlockSkirmish);
            Assert.IsNotEmpty(faction);
            Assert.IsNotEmpty(skirmish);
            Assert.Greater(faction.Average(), middle - 5, "Faction mode leaves them in the middle of the zone, where they land.");
            Assert.Less(skirmish.Average(), middle - 5);
        }

        // ------------------------------------------------------------------ spells

        private static SpellStats Strike() => new SpellStats { Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Radius = 4f, Ticks = 1, DamagePerTick = 10f };
        private static SpellStats Pods() => new SpellStats { Name = "Pods", Offensive = true, Effect = SpellEffect.DropSquad, Radius = 4f, DropSquads = 1, DropUnit = TestMatch.Infantry() };

        /// <summary>Round 2's spell, when the army the AI saw has its most valuable squad in a flank zone and a cheap one on the field.</summary>
        private static OwnedSpell SpellAfterSeeingAFlanker(MatchMode mode, AiDifficulty difficulty, SpellStats stats, int seed = 3)
        {
            Match match = NewMatch(mode, new RuleBasedAi(difficulty, seed), seed);
            match.ChooseDraft(0);
            List<UnitStats> roster = match.Player.Roster;
            match.Player.Squads.Clear();
            match.Player.Squads.Add(new PlacedSquad { Id = 1000, Type = roster.Single(u => u.Name == "Gunship"), Column = -6, Row = -30, RoundBought = 1 });
            match.Player.Squads.Add(new PlacedSquad { Id = 1001, Type = roster.Single(u => u.Name == "Chaff"), Column = 40, Row = 0, RoundBought = 1 });
            var spell = new OwnedSpell { Id = 900, Stats = stats, ReadyRound = 2 };
            match.Enemy.Spells.Add(spell);
            PlayBattle(match);
            match.ContinueAfterResult();
            return spell;
        }

        [Test]
        public void AFlankTarget_DoesNotWasteAStrike()
        {
            Assert.IsFalse(SpellAfterSeeingAFlanker(MatchMode.Faction, AiDifficulty.Normal, Strike()).IsPlaced, "Faction mode keeps its old behaviour.");
            OwnedSpell spell = SpellAfterSeeingAFlanker(MatchMode.UnlockSkirmish, AiDifficulty.Normal, Strike());
            Assert.IsTrue(spell.IsPlaced);
            Assert.IsTrue(DeploymentGrid.InArena(spell.Position, TestRules.Default()));
        }

        [Test]
        public void AFlankTarget_DoesNotWasteDropPods()
        {
            Assert.IsFalse(SpellAfterSeeingAFlanker(MatchMode.Faction, AiDifficulty.Hard, Pods()).IsPlaced, "Faction mode keeps its old behaviour.");
            OwnedSpell spell = SpellAfterSeeingAFlanker(MatchMode.UnlockSkirmish, AiDifficulty.Hard, Pods());
            Assert.IsTrue(spell.IsPlaced);
            Assert.IsTrue(DeploymentGrid.InArena(spell.Position, TestRules.Default()));
        }

        [Test]
        public void Easy_NeverAimsAtAFlankTarget()
        {
            int placed = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                OwnedSpell spell = SpellAfterSeeingAFlanker(MatchMode.UnlockSkirmish, AiDifficulty.Easy, Strike(), seed);
                if (!spell.IsPlaced) continue;
                placed++;
                Assert.IsTrue(DeploymentGrid.InArena(spell.Position, TestRules.Default()));
            }
            // Easy forgets half of its spells, and it picks at random among the two squads it saw: without the filter a quarter would be lost as well.
            Assert.Greater(placed, 5);
        }

        // ------------------------------------------------------------------ the carrier corners trial

        private static UnitStats Carrier()
        {
            UnitStats carrier = Unit("Carrier", UnitRole.Aerial, 2, 0, 1, 300, 1000, air: true, canFlank: true);
            carrier.LockedReinforcement = TestMatch.Infantry();
            return carrier;
        }

        /// <summary>Where the AI puts two new carriers in turn under this trial.</summary>
        private static List<(int, int)> CarrierSpots(AiDifficulty difficulty, CarrierPlacement placement, UnitStats carrier)
        {
            var ai = new RuleBasedAi(difficulty, 3);
            Match match = NewMatch(MatchMode.Faction, new DoNothingAi(), prepare: rules => rules.AiCarrierPlacement = placement);
            match.ChooseDraft(0);
            match.Enemy.Squads.Clear();
            var spots = new List<(int, int)>();
            for (int i = 0; i < 2; i++)
            {
                Assert.IsTrue(ai.ChooseSpot(new AiView(match, Team.B), carrier, out int column, out int row));
                spots.Add((column, row));
                match.Enemy.Squads.Add(new PlacedSquad { Id = 500 + i, Type = carrier, Column = column, Row = row, RoundBought = 1 });
            }
            return spots;
        }

        [Test]
        public void HardsCarriers_GoToTheCorners_LeftAndRightInTurn()
        {
            UnitStats carrier = Carrier();
            CombatRules rules = TestRules.Default();
            DeploymentGrid.Footprint(carrier, false, rules, out int width, out int depth);

            CollectionAssert.AreEqual(new[] { (0, 40 - depth), (80 - width, 40 - depth) }, CarrierSpots(AiDifficulty.Hard, CarrierPlacement.OwnBackCorners, carrier));
            CollectionAssert.AreEqual(new[] { (-6, -42), (86 - width, -42) }, CarrierSpots(AiDifficulty.Hard, CarrierPlacement.FlankFarCorners, carrier), "The same far corner on both sides.");
        }

        [Test]
        public void TheCornersAreForHardsCarriers_Only()
        {
            UnitStats carrier = Carrier();
            Assert.IsFalse(CarrierSpots(AiDifficulty.Normal, CarrierPlacement.FlankFarCorners, carrier).Any(spot => spot.Item2 < 0), "Normal never flanks.");
            UnitStats flyer = Unit("Flyer", UnitRole.Aerial, 2, 0, 1, 300, 1000, air: true, canFlank: true);
            CollectionAssert.DoesNotContain(CarrierSpots(AiDifficulty.Hard, CarrierPlacement.FlankFarCorners, flyer), (-6, -42), "With no reinforcement it flanks as usual.");
        }
    }
}
