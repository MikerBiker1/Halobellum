// MatchLogReplayTests.cs
// Tests the battle log's replay (D106): a match logged as the Match scene logs it (MatchLog's own action details) replays on a fresh match from
// the same start to the same armies at every battle start and the same battles, through the player's actions of every kind used here (buy,
// move, turn, unlock, a card, a spell, an item, a level, a tower, research, undo, sell); a refused action is skipped; a log that no
// longer fits the game says where; and the lines' JSON is read back as written.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class MatchLogReplayTests
    {
        private const int Seed = 7;

        /// <summary>Two factions sharing the test roster, each with a strike spell and an item card on offer.</summary>
        private static List<FactionInfo> Factions(out UnitStats infantry, out UnitStats tank)
        {
            List<FactionInfo> factions = TestMatch.StandardFactions(out infantry, out _, out tank, out _);
            var strike = new SpellStats { Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Cost = 50, CooldownRounds = 1, MaxOwnedCopies = 2, Radius = 5f, DelaySeconds = 1f, DamagePerTick = 200f, DamageType = DamageType.Explosive };
            var armor = new CardStats { Name = "Heavy Armor", Type = CardType.Item, Cost = 50, MaxOwnedCopies = 99, Item = new ItemStats { Name = "Heavy Armor", HealthMultiplier = 1.7f } };
            foreach (FactionInfo faction in factions) faction.Cards.AddRange(new[] { CardStats.FromSpell(strike), armor });
            return factions;
        }

        /// <summary>A fresh match as the log's start makes it: same rules, factions, seed and AI.</summary>
        private static Match NewMatch(out UnitStats infantry, out UnitStats tank)
        {
            MatchRules rules = TestMatch.RulesWithCards();
            rules.StartingCredits = 3000; // Room for every kind of action,
            rules.XpBarPerSquadCost = 0f; // and a level up whenever one is asked for: nothing happens outside the logged actions.
            rules.TowerBaseHealth = 100f; // The towers, so that one can be leveled.
            return new Match(rules, TestRules.Default(), Factions(out infantry, out tank), Seed, new RuleBasedAi(AiDifficulty.Normal, Seed));
        }

        /// <summary>Plays three rounds as a player would, logging each action as the Match scene does (and recording it for Undo, as it does).</summary>
        private static List<string> Record(out Match match)
        {
            match = NewMatch(out UnitStats infantry, out UnitStats tank);
            var lines = new List<string>();
            var log = new MatchLog(lines.Add);
            Match m = match;
            void Act(string action, string detail, ActionResult result)
            {
                if (result.Ok && action != "ChooseCard" && action != "SkipCards" && action != "Undo") m.RecordPlayerAction();
                log.PlayerAction(m, action, detail, result);
            }
            void Battle()
            {
                log.BattleStarted(m);
                m.StartBattle().Run();
                log.BattleFinished(m.FinishBattle());
                if (m.Phase == MatchPhase.Result) m.ContinueAfterResult();
                log.CardsOffered(m);
            }

            Assert.IsTrue(match.ChooseDraft(1).Ok);
            log.Started(match, "Normal", new System.DateTime(2026, 11, 7, 20, 0, 0), 1);

            // Round 1: buy, move, turn in place, a refused buy, then undo the turn.
            Act("Buy", infantry.Name, match.Buy(Team.A, infantry));
            PlacedSquad first = match.Player.Squads.Last();
            Act("PlaceAt", MatchLog.MoveDetail(first, 30, 2, false), match.Move(Team.A, first.Id, 30, 2, false));
            Act("ToggleRotation", MatchLog.TurnDetail(first, true), match.Move(Team.A, first.Id, first.Column, first.Row, true));
            Act("Buy", tank.Name, match.Buy(Team.A, tank)); // Not unlocked: refused, and skipped by the replay.
            Act("Undo", null, match.Undo());
            Battle();

            // Round 2: a card (the strike), placed; unlock, buy and move the tank.
            CardOffer offer = match.Player.Offer;
            Assert.IsNotNull(offer, "Cards from round 2.");
            int strike = offer.Cards.FindIndex(c => c.Spell != null);
            Assert.GreaterOrEqual(strike, 0, "Both of the faction's cards are offered (an offer changed by hand would not replay).");
            Act("ChooseCard", offer.NameOf(strike), match.ChooseCard(Team.A, strike));
            OwnedSpell spell = match.Player.Spells.Single();
            var at = new Vec2(-3.25f, 20.5f);
            Act("PlaceSpellAt", MatchLog.SpellDetail(spell.Stats, at, 0f), match.PlaceSpell(Team.A, spell.Id, at, 0f));
            Act("Unlock", tank.Name, match.Unlock(Team.A, tank));
            Act("Buy", tank.Name, match.Buy(Team.A, tank));
            PlacedSquad tankSquad = match.Player.Squads.Last();
            Act("PlaceAt", MatchLog.MoveDetail(tankSquad, 50, 0, false), match.Move(Team.A, tankSquad.Id, 50, 0, false));
            Battle();

            // Round 3: an item card on the tank, a level, the tower, research; sell the infantry.
            offer = match.Player.Offer;
            Assert.AreEqual(CardKind.General, offer.Kind);
            int armor = offer.Cards.FindIndex(c => c.Item != null);
            Assert.GreaterOrEqual(armor, 0);
            Act("ChooseCard", offer.NameOf(armor), match.ChooseCard(Team.A, armor));
            ItemStats item = match.Player.Inventory.Single();
            Act("PlaceItemOn", MatchLog.ItemDetail(item, tankSquad), match.PlaceItem(Team.A, item, tankSquad.Id));
            Act("LevelUp", MatchLog.SquadLabel(tankSquad), match.LevelUp(Team.A, tankSquad.Id));
            Act("LevelUpTower", TowerKind.CommandCenter.ToString(), match.LevelUpTower(Team.A, TowerKind.CommandCenter));
            Act("BuyResearch", "health", match.BuyResearch(Team.A, true));
            Act("Sell", MatchLog.SquadLabel(first), match.Sell(Team.A, first.Id));
            Battle();
            return lines;
        }

        [Test]
        public void ALoggedMatch_ReplaysToTheSameArmiesAndBattles()
        {
            List<string> lines = Record(out Match recorded);
            foreach (string action in new[] { "Buy", "PlaceAt", "ToggleRotation", "Undo", "ChooseCard", "PlaceSpellAt", "Unlock", "PlaceItemOn", "LevelUp", "LevelUpTower", "BuyResearch", "Sell" })
                Assert.IsTrue(lines.Exists(l => l.Contains($"\"action\":\"{action}\",\"ok\":true")), $"The recording did a {action}.");
            Assert.IsTrue(lines.Exists(l => l.Contains("\"ok\":false")), "...and had one refused.");
            MatchLogReplay replay = MatchLogReplay.Read(lines);
            Assert.AreEqual(Seed, replay.Seed);
            Assert.AreEqual(1, replay.DraftChosen);
            Assert.AreEqual(AiDifficulty.Normal, replay.AiDifficulty);

            Match fresh = NewMatch(out _, out _);
            List<string> drift = replay.PlayHeadless(fresh);
            CollectionAssert.IsEmpty(drift, "Replayed exactly:\n" + string.Join("\n", drift));
            Assert.AreEqual(recorded.Round, fresh.Round);
            CollectionAssert.AreEqual(recorded.Player.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row, s.Rotated90, s.Level)).ToList(),
                fresh.Player.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row, s.Rotated90, s.Level)).ToList());
            CollectionAssert.AreEqual(recorded.Enemy.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row)).ToList(),
                fresh.Enemy.Squads.Select(s => (s.Id, s.Type.Name, s.Column, s.Row)).ToList(), "The AI answered as it did.");
            Assert.AreEqual(recorded.Player.BaseHealth, fresh.Player.BaseHealth);
            Assert.AreEqual(recorded.Enemy.BaseHealth, fresh.Enemy.BaseHealth);
        }

        [Test]
        public void ALogThatNoLongerFits_SaysWhere()
        {
            List<string> lines = Record(out _);
            int buy = lines.FindIndex(l => l.Contains("\"action\":\"Buy\"") && l.Contains("\"ok\":true"));
            lines.RemoveAt(buy); // As if that squad had never been bought.

            List<string> drift = MatchLogReplay.Read(lines).PlayHeadless(NewMatch(out _, out _));
            Assert.IsNotEmpty(drift);
            StringAssert.Contains("round 1", drift[0], "The first difference is where it began.");
        }

        [Test]
        public void AFileThatIsNotALog_IsRefused()
        {
            Assert.Throws<System.FormatException>(() => MatchLogReplay.Read(new[] { "{\"type\":\"action\",\"round\":1}" }));
            Assert.Throws<System.FormatException>(() => MatchLogReplay.Read(new[] { "not json" }));
        }

        [Test]
        public void TheJson_IsReadBackAsWritten()
        {
            Dictionary<string, object> json = LogJson.Object("{\"a\":\"x \\\"y\\\" \\u00e9\\n\",\"b\":-12.5,\"c\":[1,{\"d\":true}],\"e\":null,\"f\":false}");
            Assert.AreEqual("x \"y\" é\n", json["a"]);
            Assert.AreEqual(-12.5, (double)json["b"], 1e-9);
            Assert.AreEqual(2, ((List<object>)json["c"]).Count);
            Assert.AreEqual(true, ((Dictionary<string, object>)((List<object>)json["c"])[1])["d"]);
            Assert.IsNull(json["e"]);
            Assert.AreEqual(false, json["f"]);
        }
    }
}
