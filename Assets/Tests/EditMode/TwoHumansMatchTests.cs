// TwoHumansMatchTests.cs
// Tests the two-humans match (Milestone 11, a LAN game): two machines build the same match from one seed, each plays its own side live
// (the Insane AI through an AiView on its own machine), they exchange their command lists and apply the other's, and their state hashes
// agree before every battle and after every result, for whole matches, whatever order the lists are applied in. Also: undo inside a list
// gives the same ids; a spell's position goes across exactly; each side picks its own starting card and the match begins when both are
// in; one side deploys while the other's card is still open; the host's round outcome is final on the guest; commands and outcomes read
// back from their text exactly; a list from the other machine cannot act for this machine's side, and a command out of order is refused;
// a bad tower name is a format error and a missing tower is refused; ChooseDraft(index) and Undo() with no team do nothing in this mode;
// the content fingerprint sees a changed unit; the preview works for Team B; and a match against the AI records no commands.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch, TestRules.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class TwoHumansMatchTests
    {
        // ------------------------------------------------------------------ fixtures

        /// <summary>
        /// Standard UNSC and Covenant (the test roster), the Tank with a tech, and cards of every general kind; with onlyStrike, the strike
        /// spell is the one card (so that round 2 offers it). Each call builds new objects, as a second machine would.
        /// </summary>
        private static List<FactionInfo> Factions(bool onlyStrike = false, float tankDamage = 10f)
        {
            List<FactionInfo> factions = TestMatch.StandardFactions(out UnitStats infantry, out _, out UnitStats tank, out _);
            tank.Weapons[0].DamagePerShot = tankDamage;
            tank.TechUpgrades = new[] { new TechUpgrade { Name = "Plating", Cost = 100, HealthMultiplier = 1.2f } };
            var strike = new SpellStats { Name = "Strike", Offensive = true, Effect = SpellEffect.Damage, Cost = 50, CooldownRounds = 1, MaxOwnedCopies = 2, Radius = 5f, DelaySeconds = 1f, DamagePerTick = 200f, DamageType = DamageType.Explosive };
            var cards = new List<CardStats> { CardStats.FromSpell(strike) };
            if (!onlyStrike)
            {
                cards.Add(new CardStats { Name = "Heavy Armor", Type = CardType.Item, Cost = 50, MaxOwnedCopies = 99, Item = new ItemStats { Name = "Heavy Armor", HealthMultiplier = 1.7f } });
                cards.Add(new CardStats { Name = "Deployment Module", Type = CardType.Item, Cost = 0, MaxOwnedCopies = 99, Item = new ItemStats { Name = "Deployment Module", FreeDeployment = true } });
                cards.Add(new CardStats { Name = "Supply Enhancement", Type = CardType.Economy, Cost = 100, BonusIncome = 50 });
                cards.Add(new CardStats { Name = "Intensive Training", Type = CardType.Training, Cost = 50, CooldownRounds = 1 });
            }
            foreach (FactionInfo faction in factions) faction.Cards.AddRange(cards);
            return factions;
        }

        /// <summary>The game's rules with cards, towers and the Command Center's oil.</summary>
        private static MatchRules Rules()
        {
            MatchRules rules = TestMatch.RulesWithCards();
            rules.TowerBaseHealth = 100f;
            rules.OilZone = new SpellStats { Name = "Oil Zone", Offensive = true, Effect = SpellEffect.Oil, Cost = 150, AreaWidth = 15f, AreaDepth = 4f, DurationSeconds = 0f, SlowShare = 0.4f, FireDamageMultiplier = 1.5f, CooldownRounds = 2 };
            return rules;
        }

        private static Match NewMatch(int seed, bool onlyStrike = false, int baseHealth = 0)
        {
            MatchRules rules = Rules();
            if (baseHealth > 0) rules.BaseHealth = baseHealth;
            return new Match(rules, TestRules.Default(), Factions(onlyStrike), seed, MatchSeats.TwoHumans);
        }

        private static string HashOf(Match match) => MatchStateHash.Hex(MatchStateHash.Compute(match));

        /// <summary>Sends one side's commands from one machine to another, through their text, and applies them there; every one must work.</summary>
        private static void Send(Match from, Match to, Team team)
        {
            List<MatchCommand> commands = MatchCommand.ParseList(MatchCommand.ListToText(from.TakeCommands(team)));
            ApplyAll(to, team, commands);
        }

        private static void ApplyAll(Match to, Team from, List<MatchCommand> commands)
        {
            var failures = new List<string>();
            Assert.IsTrue(MatchCommands.ApplyAll(to, from, commands, failures), "Every command works on the other machine:\n" + string.Join("\n", failures));
        }

        /// <summary>Both machines pick their own starting card (first on offer) and exchange them: round 1 begins on both.</summary>
        private static void Draft(Match host, Match guest, int cardA = 0, int cardB = 0)
        {
            Assert.IsTrue(host.ChooseDraft(Team.A, cardA).Ok);
            Assert.IsTrue(guest.ChooseDraft(Team.B, cardB).Ok);
            Send(host, guest, Team.A);
            Send(guest, host, Team.B);
            Assert.AreEqual(MatchPhase.Deployment, host.Phase);
            Assert.AreEqual(HashOf(host), HashOf(guest));
        }

        private static void FightAndFinish(Match host, Match guest)
        {
            host.StartBattle().Run();
            guest.StartBattle().Run();
            RoundOutcome outcome = host.OutcomeOfCurrentBattle();
            host.FinishBattle();
            guest.FinishBattle(RoundOutcome.Parse(outcome.ToText()));
            Assert.AreEqual(HashOf(host), HashOf(guest));
            if (host.Phase == MatchPhase.Result) host.ContinueAfterResult();
            if (guest.Phase == MatchPhase.Result) guest.ContinueAfterResult();
        }

        private static UnitStats Unit(Match match, Team team, string name) => match.Get(team).Roster.Single(u => u.Name == name);

        // ------------------------------------------------------------------ whole matches

        /// <summary>One side's turn on its own machine, as a person would take it: the card first, then the deployment (the Insane AI decides).</summary>
        private static void PlaySide(Match match, Team team, RuleBasedAi ai)
        {
            var view = new AiView(match, team);
            if (match.InCards(team))
            {
                int choice = ai.ChooseCard(view, match.Get(team).Offer);
                if (choice < 0 || !match.ChooseCard(team, choice).Ok) match.SkipCards(team);
            }
            ai.Deploy(view);
        }

        /// <summary>Smaller bases than the game's, so that a whole match (three machines, the Insane AI on both sides) takes seconds, not minutes.</summary>
        private const int WholeMatchBaseHealth = 400;

        [TestCase(23)]
        [TestCase(31)]
        [TestCase(17)]
        public void AWholeMatch_PlayedOnTwoMachines_StaysInStep_WhateverTheOrder(int seed)
        {
            // Machine 1 (the host) plays Team A live; machine 2 (the guest) plays Team B live; machine 3 only applies both lists, in turns A
            // first or B first. Each AI sees only its own machine's match.
            Match host = NewMatch(seed, baseHealth: WholeMatchBaseHealth), guest = NewMatch(seed, baseHealth: WholeMatchBaseHealth), watcher = NewMatch(seed, baseHealth: WholeMatchBaseHealth);
            var aiA = new RuleBasedAi(AiDifficulty.Insane, seed + 1);
            var aiB = new RuleBasedAi(AiDifficulty.Insane, seed + 2);
            Assert.AreEqual(HashOf(host), HashOf(guest), "The same seed builds the same match.");

            Assert.IsTrue(host.ChooseDraft(Team.A, aiA.ChooseDraftCard(new AiView(host, Team.A), host.PlayerDraft)).Ok);
            Assert.IsTrue(guest.ChooseDraft(Team.B, aiB.ChooseDraftCard(new AiView(guest, Team.B), guest.EnemyDraft)).Ok);
            Assert.AreEqual(MatchPhase.Draft, host.Phase, "The host waits for the guest's starting card.");

            var actions = new HashSet<string>();
            int rounds = 0;
            bool aFirst = true;
            bool drafted = false;
            while (rounds < 60)
            {
                // Exchange this step's commands: the starting cards first, then each round's card and deployment.
                if (drafted)
                {
                    PlaySide(host, Team.A, aiA);
                    PlaySide(guest, Team.B, aiB);
                }
                List<MatchCommand> listA = MatchCommand.ParseList(MatchCommand.ListToText(host.TakeCommands(Team.A)));
                List<MatchCommand> listB = MatchCommand.ParseList(MatchCommand.ListToText(guest.TakeCommands(Team.B)));
                CollectionAssert.IsEmpty(host.TakeCommands(Team.B), "Applied commands are not recorded again.");
                foreach (MatchCommand command in listA.Concat(listB)) actions.Add(command.Action);
                ApplyAll(guest, Team.A, listA);
                ApplyAll(host, Team.B, listB);
                ApplyAll(watcher, aFirst ? Team.A : Team.B, aFirst ? listA : listB);
                ApplyAll(watcher, aFirst ? Team.B : Team.A, aFirst ? listB : listA);
                aFirst = !aFirst;
                if (!drafted)
                {
                    drafted = true;
                    Assert.AreEqual(HashOf(host), HashOf(guest), "In step after the starting cards.");
                    Assert.AreEqual(HashOf(host), HashOf(watcher));
                    continue;
                }

                string hash = HashOf(host);
                Assert.AreEqual(hash, HashOf(guest), $"Round {host.Round}: in step before the battle.");
                Assert.AreEqual(hash, HashOf(watcher), $"Round {host.Round}: the order the lists were applied in made no difference.");
                Assert.AreEqual(MatchPhase.Deployment, host.Phase);
                Assert.IsTrue(host.CanStartBattle, "Both sides took or skipped their card.");

                BattleSimulation battle = host.StartBattle();
                guest.StartBattle();
                watcher.StartBattle();
                battle.Run();
                guest.CurrentBattle.Run();
                watcher.CurrentBattle.Run();
                RoundOutcome outcome = host.OutcomeOfCurrentBattle();
                Assert.AreEqual(outcome.Hash(), guest.OutcomeOfCurrentBattle().Hash(), "The same battle on both machines.");
                RoundSummary hostSummary = host.FinishBattle();
                RoundSummary guestSummary = guest.FinishBattle(RoundOutcome.Parse(outcome.ToText()));
                watcher.FinishBattle();
                Assert.AreEqual(MatchStateHash.Compute(hostSummary), MatchStateHash.Compute(guestSummary));
                hash = HashOf(host);
                Assert.AreEqual(hash, HashOf(guest), $"Round {host.Round}: in step after the result.");
                Assert.AreEqual(hash, HashOf(watcher));
                rounds++;
                if (host.Phase == MatchPhase.MatchOver) break;
                host.ContinueAfterResult();
                guest.ContinueAfterResult();
                watcher.ContinueAfterResult();
            }
            Assert.AreEqual(MatchPhase.MatchOver, host.Phase, $"No winner after {rounds} rounds.");
            Assert.AreEqual(host.Winner, guest.Winner);
            foreach (string action in new[] { MatchCommand.ChooseDraftAction, MatchCommand.BuyAction, MatchCommand.UnlockAction })
                Assert.IsTrue(actions.Contains(action), $"The match had a {action} (it had {string.Join(", ", actions)}).");
            TestContext.WriteLine($"Seed {seed}: {rounds} rounds, actions {string.Join(", ", actions)}.");
            Assert.IsTrue(actions.Contains(MatchCommand.ChooseCardAction) || actions.Contains(MatchCommand.SkipCardsAction) || rounds < 2, "Cards were decided.");
        }

        // ------------------------------------------------------------------ undo, spells, draft, cards

        [Test]
        public void UndoInsideAList_GivesTheSameIds_OnBothMachines()
        {
            Match host = NewMatch(3), guest = NewMatch(3);
            Draft(host, guest);
            List<int> draftIdsB = host.Enemy.Squads.Select(s => s.Id).ToList();
            CollectionAssert.AreEqual(new[] { Match.TeamBFirstId, Match.TeamBFirstId + 1 }, draftIdsB, "Team B counts from its own first id.");
            CollectionAssert.AreEqual(new[] { 1, 2 }, host.Player.Squads.Select(s => s.Id).ToList());

            UnitStats infantryA = Unit(host, Team.A, "Infantry");
            Assert.IsTrue(host.Buy(Team.A, infantryA).Ok);
            int first = host.Player.Squads.Last().Id;
            Assert.IsTrue(host.CanUndoFor(Team.A));
            Assert.IsTrue(host.Undo(Team.A).Ok);
            Assert.IsTrue(host.Buy(Team.A, infantryA).Ok);
            Assert.AreEqual(first, host.Player.Squads.Last().Id, "Undo gave the id back.");

            UnitStats buggyB = Unit(guest, Team.B, "Buggy"), infantryB = Unit(guest, Team.B, "Infantry");
            Assert.IsTrue(guest.Buy(Team.B, buggyB).Ok);
            Assert.IsTrue(guest.Undo(Team.B).Ok);
            Assert.IsTrue(guest.Buy(Team.B, infantryB).Ok);
            Assert.AreEqual(Match.TeamBFirstId + 2, guest.Enemy.Squads.Last().Id);
            Assert.IsTrue(host.LevelUpTower(Team.A, TowerKind.CommandCenter).Ok);
            Assert.IsTrue(host.Sell(Team.A, 1).Ok, "A starting squad sold.");

            Send(host, guest, Team.A);
            Send(guest, host, Team.B);
            Assert.AreEqual(HashOf(host), HashOf(guest));
            CollectionAssert.AreEqual(host.Player.Squads.Select(s => s.Id), guest.Player.Squads.Select(s => s.Id));
            CollectionAssert.AreEqual(host.Enemy.Squads.Select(s => s.Id), guest.Enemy.Squads.Select(s => s.Id));
            Assert.AreEqual(host.UndoStepsFor(Team.A), guest.UndoStepsFor(Team.A), "The applied list built the same undo history.");
            Assert.AreEqual(host.UndoStepsFor(Team.B), guest.UndoStepsFor(Team.B));

            // An undo sent in a later list takes back the same action on both machines.
            Assert.IsTrue(guest.Undo(Team.B).Ok);
            Send(guest, host, Team.B);
            Assert.AreEqual(HashOf(host), HashOf(guest));
            FightAndFinish(host, guest);
        }

        [Test]
        public void ASpellsPosition_GoesAcrossExactly()
        {
            Match host = NewMatch(8, onlyStrike: true), guest = NewMatch(8, onlyStrike: true);
            Draft(host, guest);
            FightAndFinish(host, guest);
            Assert.AreEqual(2, host.Round);
            Assert.IsTrue(host.InCards(Team.A) && host.InCards(Team.B), "Round 2 offers each side the strike.");

            Assert.IsTrue(host.ChooseCard(Team.A, 0).Ok);
            OwnedSpell spell = host.Player.Spells.Single();
            Assert.AreEqual(1, spell.Id, "Team A's spells count from 1.");
            var at = new Vec2(1f / 3f, -20f / 3f);
            float angle = 100f / 7f;
            Assert.IsTrue(host.PlaceSpell(Team.A, spell.Id, at, angle).Ok);
            List<MatchCommand> sent = host.TakeCommands(Team.A);
            MatchCommand place = MatchCommand.Parse(sent.Last().ToText());
            Assert.AreEqual(StableHasher.FloatToBits(at.X), StableHasher.FloatToBits(place.X));
            Assert.AreEqual(StableHasher.FloatToBits(at.Y), StableHasher.FloatToBits(place.Y));
            Assert.AreEqual(StableHasher.FloatToBits(angle), StableHasher.FloatToBits(place.Angle));
            ApplyAll(guest, Team.A, sent.Select(c => MatchCommand.Parse(c.ToText())).ToList());

            OwnedSpell copy = guest.Player.Spells.Single();
            Assert.IsTrue(copy.IsPlaced);
            Assert.AreEqual(StableHasher.FloatToBits(at.X), StableHasher.FloatToBits(copy.Position.X), "Exactly 1/3, not 0.33.");
            Assert.AreEqual(StableHasher.FloatToBits(at.Y), StableHasher.FloatToBits(copy.Position.Y));
            Assert.AreEqual(StableHasher.FloatToBits(angle), StableHasher.FloatToBits(copy.AngleDegrees));

            Assert.IsTrue(guest.ChooseCard(Team.B, 0).Ok);
            Assert.AreEqual(Match.TeamBFirstId, guest.Enemy.Spells.Single().Id, "Team B's spells count from its own first id.");
            Send(guest, host, Team.B);
            Assert.AreEqual(HashOf(host), HashOf(guest));
            FightAndFinish(host, guest);
        }

        [Test]
        public void EachMachinePicksItsOwnStartingCard_AndTheMatchBeginsWhenBothAreIn()
        {
            Match host = NewMatch(4), guest = NewMatch(4);
            Assert.IsTrue(host.IsTwoHumans);
            Assert.IsFalse(host.SetEnemyAi(new DoNothingAi()), "No AI in a two-humans match.");
            Assert.IsTrue(host.ChooseDraft(Team.A, 1).Ok);
            Assert.AreEqual(MatchPhase.Draft, host.Phase);
            Assert.IsTrue(host.HasChosenDraft(Team.A));
            Assert.IsFalse(host.HasChosenDraft(Team.B));
            Assert.IsFalse(host.ChooseDraft(Team.A, 0).Ok, "One pick each.");
            Assert.IsFalse(guest.ChooseDraft(Team.B, guest.EnemyDraft.Count).Ok, "No such card.");
            Assert.IsTrue(guest.ChooseDraft(Team.B, 1).Ok);
            Assert.AreEqual(MatchPhase.Draft, guest.Phase);

            Send(guest, host, Team.B);
            Assert.AreEqual(MatchPhase.Deployment, host.Phase, "Both cards in: round 1 begins on the host.");
            Assert.AreEqual(1, host.Round);
            Send(host, guest, Team.A);
            Assert.AreEqual(MatchPhase.Deployment, guest.Phase);
            Assert.AreEqual(HashOf(host), HashOf(guest));
            Assert.AreEqual(host.PlayerDraft[1].SubFaction, host.Player.SubFaction);
            Assert.AreEqual(host.EnemyDraft[1].SubFaction, host.Enemy.SubFaction);
            Assert.AreEqual(Rules().StartingCredits, guest.Enemy.Credits);
            Assert.AreEqual(2, host.SeenArmy(Team.B).Count, "Both see each other's starting squads.");
        }

        [Test]
        public void OneSideDeploys_WhileTheOthersCardIsStillOpen()
        {
            Match match = NewMatch(6);
            Assert.IsTrue(match.ChooseDraft(Team.A, 0).Ok);
            Assert.IsTrue(match.ChooseDraft(Team.B, 0).Ok);
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();

            Assert.AreEqual(MatchPhase.Deployment, match.Phase, "No Cards phase in a two-humans match.");
            Assert.IsTrue(match.InCards(Team.A));
            Assert.IsTrue(match.InCards(Team.B));
            Assert.IsFalse(match.CanStartBattle);
            Assert.IsTrue(match.SkipCards(Team.B).Ok);
            Assert.IsFalse(match.InCards(Team.B));

            UnitStats infantryB = Unit(match, Team.B, "Infantry"), infantryA = Unit(match, Team.A, "Infantry");
            Assert.IsTrue(match.Buy(Team.B, infantryB).Ok, "Team B deploys while Team A's card is open.");
            Assert.IsTrue(match.CanUndoFor(Team.B));
            ActionResult refused = match.Buy(Team.A, infantryA);
            Assert.IsFalse(refused.Ok);
            StringAssert.Contains("card", refused.Message);
            Assert.IsFalse(match.CanUndoFor(Team.A), "Team A's undo history starts with its card.");
            Assert.Throws<System.InvalidOperationException>(() => match.StartBattle(), "No battle while a card is open.");

            Assert.IsTrue(match.SkipCards(Team.A).Ok);
            Assert.IsTrue(match.Buy(Team.A, infantryA).Ok);
            Assert.IsTrue(match.CanStartBattle);
        }

        // ------------------------------------------------------------------ the host's outcome

        [Test]
        public void TheHostsOutcome_IsFinalOnTheGuest_EvenWhenTheGuestsBattleWentOtherwise()
        {
            Match host = NewMatch(9), guest = NewMatch(9);
            Draft(host, guest);
            Assert.IsTrue(host.Buy(Team.A, Unit(host, Team.A, "Infantry")).Ok);
            Assert.IsTrue(guest.Buy(Team.B, Unit(guest, Team.B, "Buggy")).Ok);
            Send(host, guest, Team.A);
            Send(guest, host, Team.B);
            Assert.AreEqual(HashOf(host), HashOf(guest));

            host.StartBattle().Run();
            BattleSimulation guestBattle = guest.StartBattle();
            foreach (SimUnit unit in guestBattle.Units)
            {
                if (unit.Team == Team.B && !unit.Stats.IsTower) unit.Health = 1f; // The guest's battle goes another way.
            }
            guestBattle.Run();
            RoundOutcome outcome = host.OutcomeOfCurrentBattle();
            Assert.AreNotEqual(outcome.Hash(), guest.OutcomeOfCurrentBattle().Hash(), "The guest's own battle ended otherwise.");

            RoundOutcome received = RoundOutcome.Parse(outcome.ToText());
            Assert.AreEqual(outcome.Hash(), received.Hash(), "The outcome reads back exactly.");
            Assert.AreEqual(outcome.ToText(), received.ToText());
            host.FinishBattle();
            guest.FinishBattle(received);
            Assert.AreEqual(HashOf(host), HashOf(guest), "The host's numbers are the match's on both machines.");
            Assert.AreEqual(host.Player.BaseHealth, guest.Player.BaseHealth);
            Assert.AreEqual(host.Enemy.BaseHealth, guest.Enemy.BaseHealth);
        }

        [Test]
        public void AChangedOutcome_ChangesTheMatch()
        {
            Match first = NewMatch(10), second = NewMatch(10);
            Draft(first, second);
            Assert.IsTrue(first.Buy(Team.A, Unit(first, Team.A, "Infantry")).Ok);
            Send(first, second, Team.A);
            first.StartBattle().Run();
            second.StartBattle().Run();
            RoundOutcome outcome = first.OutcomeOfCurrentBattle();
            RoundOutcome changed = RoundOutcome.Parse(outcome.ToText());
            changed.BaseDamageByA += 37;
            changed.Xp[0].Xp += 5f;

            first.FinishBattle(outcome);
            second.FinishBattle(changed);
            Assert.AreNotEqual(HashOf(first), HashOf(second));
            Assert.AreEqual(first.Enemy.BaseHealth - 37, second.Enemy.BaseHealth, "Team B's base took the outcome's damage.");
            PlacedSquad squad = second.Player.Squads.First(s => s.Id == changed.Xp[0].SquadId);
            Assert.AreEqual(System.Math.Min(second.XpNeeded(squad), first.Player.FindSquad(squad.Id).Xp + 5f), squad.Xp, 1e-3f);
            Assert.Throws<System.InvalidOperationException>(() => second.FinishBattle(outcome), "Only once, in the battle.");
        }

        [Test]
        public void FinishBattleWithItsOwnOutcome_IsFinishBattle()
        {
            Match first = NewMatch(12), second = NewMatch(12);
            Draft(first, second);
            first.StartBattle().Run();
            second.StartBattle().Run();
            RoundSummary own = first.FinishBattle();
            RoundSummary given = second.FinishBattle(second.OutcomeOfCurrentBattle());
            Assert.AreEqual(HashOf(first), HashOf(second));
            Assert.AreEqual(MatchStateHash.Compute(own), MatchStateHash.Compute(given));
            Assert.AreEqual(own.IncomeFor(Team.B), given.IncomeAwardedB);
            Assert.AreEqual(own.IncomeAwarded, own.IncomeFor(Team.A));
        }

        // ------------------------------------------------------------------ text, fingerprint, preview, single player

        [Test]
        public void ACommand_ReadsBackFromItsTextExactly()
        {
            var command = new MatchCommand
            {
                Team = Team.B, Round = 7, Seq = 42, Action = MatchCommand.PlaceItemAction, SquadId = Match.TeamBFirstId + 12, SpellId = -3, Index = 2,
                Unit = "Elite \"Zealot\" " + (char)0xE9, Tech = null, Item = "Heavy Armor", Tower = TowerKind.ResearchCenter, Health = true,
                Column = -5, Row = -12, Turned = true, X = 1f / 3f, Y = -0f, Angle = float.Epsilon,
            };
            string text = command.ToText();
            MatchCommand back = MatchCommand.Parse(text);
            Assert.AreEqual(text, back.ToText());
            Assert.AreEqual(command.Team, back.Team);
            Assert.AreEqual(command.Round, back.Round);
            Assert.AreEqual(command.Seq, back.Seq);
            Assert.AreEqual(command.Action, back.Action);
            Assert.AreEqual(command.SquadId, back.SquadId);
            Assert.AreEqual(command.SpellId, back.SpellId);
            Assert.AreEqual(command.Index, back.Index);
            Assert.AreEqual(command.Unit, back.Unit);
            Assert.IsNull(back.Tech);
            Assert.AreEqual(command.Item, back.Item);
            Assert.AreEqual(command.Tower, back.Tower);
            Assert.AreEqual(command.Health, back.Health);
            Assert.AreEqual(command.Column, back.Column);
            Assert.AreEqual(command.Row, back.Row);
            Assert.AreEqual(command.Turned, back.Turned);
            Assert.AreEqual(StableHasher.FloatToBits(command.X), StableHasher.FloatToBits(back.X));
            Assert.AreEqual(StableHasher.FloatToBits(-0f), StableHasher.FloatToBits(back.Y), "Even -0 stays -0.");
            Assert.AreEqual(StableHasher.FloatToBits(command.Angle), StableHasher.FloatToBits(back.Angle));

            List<MatchCommand> list = MatchCommand.ParseList(MatchCommand.ListToText(new[] { command, new MatchCommand { Team = Team.A, Action = MatchCommand.UndoAction } }));
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(text, list[0].ToText());
            Assert.AreEqual(MatchCommand.UndoAction, list[1].Action);
            Assert.Throws<System.FormatException>(() => MatchCommand.Parse("{\"team\":\"C\",\"action\":\"Buy\"}"));
        }

        [Test]
        public void AStaleCommand_IsRefused()
        {
            Match host = NewMatch(2), guest = NewMatch(2);
            Draft(host, guest);
            Assert.IsTrue(host.Buy(Team.A, Unit(host, Team.A, "Infantry")).Ok);
            MatchCommand buy = host.TakeCommands(Team.A).Single();
            buy.Round = 5;
            ActionResult result = MatchCommands.Apply(guest, Team.A, buy);
            Assert.IsFalse(result.Ok, "A command from another round is not done.");
            Assert.AreEqual(2, guest.Player.Squads.Count);
        }

        [Test]
        public void AListFromTheOtherMachine_CannotActForThisMachinesSide()
        {
            // The guest plays Team B, so every command it sends must be Team B's. One marked Team A would act on the host's own side.
            Match host = NewMatch(3);
            var pick = new MatchCommand { Team = Team.A, Round = 0, Seq = 1, Action = MatchCommand.ChooseDraftAction, Index = 1 };
            Assert.IsFalse(MatchCommands.Apply(host, Team.B, pick).Ok, "A Team A pick in the guest's list is refused.");
            Assert.IsFalse(host.HasChosenDraft(Team.A), "The host's starting card is still the host's to pick.");

            Match guest = NewMatch(3);
            Draft(host, guest);
            int squadId = host.Player.Squads[0].Id;
            var sell = new MatchCommand { Team = Team.A, Round = host.Round, Seq = 2, Action = MatchCommand.SellAction, SquadId = squadId };
            var failures = new List<string>();
            Assert.IsFalse(MatchCommands.ApplyAll(host, Team.B, new[] { sell }, failures));
            Assert.AreEqual(1, failures.Count);
            Assert.IsNotNull(host.Player.FindSquad(squadId), "The host's squad is not sold.");
            Assert.AreEqual(HashOf(guest), HashOf(host), "Nothing changed.");
        }

        [Test]
        public void ACommandOutOfOrder_IsRefused()
        {
            Match host = NewMatch(5), guest = NewMatch(5);
            Draft(host, guest);
            UnitStats infantry = Unit(host, Team.A, "Infantry");
            Assert.IsTrue(host.Buy(Team.A, infantry).Ok);
            Assert.IsTrue(host.Buy(Team.A, infantry).Ok);
            List<MatchCommand> buys = MatchCommand.ParseList(MatchCommand.ListToText(host.TakeCommands(Team.A)));
            Assert.AreEqual(new[] { 2, 3 }, buys.Select(c => c.Seq).ToArray(), "The starting card was command 1.");

            int squads = guest.Player.Squads.Count;
            Assert.IsFalse(MatchCommands.Apply(guest, Team.A, buys[1]).Ok, "Command 3 before command 2 is refused.");
            Assert.AreEqual(squads, guest.Player.Squads.Count);
            Assert.IsTrue(MatchCommands.Apply(guest, Team.A, buys[0]).Ok);
            Assert.IsFalse(MatchCommands.Apply(guest, Team.A, buys[0]).Ok, "The same command twice is refused.");
            Assert.AreEqual(squads + 1, guest.Player.Squads.Count);
            Assert.IsTrue(MatchCommands.Apply(guest, Team.A, buys[1]).Ok);
            Assert.AreEqual(HashOf(host), HashOf(guest));
        }

        [Test]
        public void ABadTowerName_IsAFormatError_AndAMissingTowerIsRefused()
        {
            string good = new MatchCommand { Team = Team.B, Round = 1, Seq = 1, Action = MatchCommand.LevelUpTowerAction, Tower = TowerKind.ResearchCenter }.ToText();
            Assert.AreEqual(TowerKind.ResearchCenter, MatchCommand.Parse(good).Tower);
            foreach (string bad in new[] { "\"7\"", "\"1\"", "\"Foo\"", "\"commandcenter\"", "\"CommandCenter, ResearchCenter\"", "\"\"", "null" })
            {
                string text = good.Replace("\"ResearchCenter\"", bad);
                Assert.Throws<System.FormatException>(() => MatchCommand.Parse(text), text);
            }
            Assert.Throws<System.FormatException>(() => MatchCommand.Parse(good.Replace(",\"tower\":\"ResearchCenter\"", "")), "No tower at all.");

            var outcome = new RoundOutcome { Round = 1 };
            outcome.TowerFalls.Add(new TowerFall { Team = Team.B, Kind = TowerKind.ResearchCenter });
            Assert.AreEqual(TowerKind.ResearchCenter, RoundOutcome.Parse(outcome.ToText()).TowerFalls.Single().Kind);
            Assert.Throws<System.FormatException>(() => RoundOutcome.Parse(outcome.ToText().Replace("\"ResearchCenter\"", "\"7\"")));

            // A tower the side does not have is refused, not a crash.
            Match host = NewMatch(9), guest = NewMatch(9);
            Draft(host, guest);
            Assert.IsFalse(host.LevelUpTower(Team.B, (TowerKind)7).Ok);
            Assert.AreEqual(HashOf(guest), HashOf(host));
        }

        [Test]
        public void TheMembersThatNameNoTeam_DoNotActForTeamA_InATwoHumansMatch()
        {
            // The guest's screen may still call ChooseDraft(index) and Undo() with no team: they must not act for the host's side.
            Match guest = NewMatch(10), host = NewMatch(10);
            Assert.IsFalse(guest.ChooseDraft(0).Ok);
            Assert.IsFalse(guest.HasChosenDraft(Team.A));
            Assert.AreEqual(0, guest.PendingCommands(Team.A));

            Draft(host, guest);
            Assert.IsTrue(host.Buy(Team.A, Unit(host, Team.A, "Infantry")).Ok);
            Assert.IsTrue(host.CanUndoFor(Team.A));
            Assert.IsFalse(host.CanUndo);
            Assert.AreEqual(0, host.UndoSteps);
            int squads = host.Player.Squads.Count;
            Assert.IsFalse(host.Undo().Ok);
            Assert.AreEqual(squads, host.Player.Squads.Count, "Nothing was undone.");
            Assert.IsTrue(host.Undo(Team.A).Ok);
            Assert.AreEqual(squads - 1, host.Player.Squads.Count);
        }

        [Test]
        public void TheContentFingerprint_IsTheSameForTheSameContent_AndSeesAChangedUnit()
        {
            ulong one = ContentFingerprint.Compute(Factions(), Rules(), TestRules.Default());
            ulong two = ContentFingerprint.Compute(Factions(), Rules(), TestRules.Default());
            Assert.AreEqual(one, two, "Two builds of the same content.");
            Assert.AreNotEqual(one, ContentFingerprint.Compute(Factions(tankDamage: 11f), Rules(), TestRules.Default()), "The Tank deals more damage.");
            MatchRules richer = Rules();
            richer.StartingCredits += 1;
            Assert.AreNotEqual(one, ContentFingerprint.Compute(Factions(), richer, TestRules.Default()), "Other match rules.");
            CombatRules combat = TestRules.Default();
            combat.DamageTable.Set(DamageType.Ballistic, ArmorType.Heavy, combat.DamageTable.Get(DamageType.Ballistic, ArmorType.Heavy) + 0.01f);
            Assert.AreNotEqual(one, ContentFingerprint.Compute(Factions(), Rules(), combat), "Another damage table.");
            List<FactionInfo> swapped = Factions();
            swapped.Reverse();
            Assert.AreNotEqual(one, ContentFingerprint.Compute(swapped, Rules(), TestRules.Default()), "The factions' order counts (the draft walks it).");
        }

        [Test]
        public void TeamBsPreview_PutsItsOwnArmyFirst_AndTheSeenArmyAsGhosts()
        {
            Match match = NewMatch(13);
            Assert.IsTrue(match.ChooseDraft(Team.A, 0).Ok);
            Assert.IsTrue(match.ChooseDraft(Team.B, 0).Ok);
            Assert.IsTrue(match.Buy(Team.B, Unit(match, Team.B, "Infantry")).Ok);
            Assert.IsTrue(match.Buy(Team.A, Unit(match, Team.A, "Infantry")).Ok); // Not seen by B yet.
            Assert.IsTrue(match.LevelUpTower(Team.A, TowerKind.CommandCenter).Ok);

            BattleSimulation preview = match.BuildPreview(Team.B);
            int own = match.Enemy.Squads.Count, seen = match.SeenArmy(Team.A).Count;
            Assert.AreEqual(own + seen, preview.Squads.Count);
            for (int i = 0; i < own; i++)
            {
                Assert.AreEqual(Team.B, preview.Squads[i].Team, "The viewer's squads come first.");
                Assert.Greater(preview.Squads[i].Members[0].Position.Y, 0f, "...in Team B's half.");
            }
            for (int i = own; i < own + seen; i++) Assert.AreEqual(Team.A, preview.Squads[i].Team);
            Assert.AreEqual(1, match.SeenTowerLevel(Team.B, TowerKind.CommandCenter), "Team B has not seen the new level yet.");
            Assert.AreEqual(match.BuildPreview().Squads.Count, match.BuildPreview(Team.A).Squads.Count);

            match.StartBattle().Run();
            Assert.AreEqual(2, match.SeenTowerLevel(Team.B, TowerKind.CommandCenter), "Seen when the battle started.");
            Assert.AreEqual(1, match.SeenTowerLevel(Team.A, TowerKind.CommandCenter));
        }

        [Test]
        public void AMatchAgainstTheAi_RecordsNoCommands_AndKeepsItsCards()
        {
            var match = new Match(Rules(), TestRules.Default(), Factions(), 1, new RuleBasedAi(AiDifficulty.Normal, 1));
            Assert.IsFalse(match.IsTwoHumans);
            Assert.AreEqual(MatchSeats.HumanVsAi, match.Seats);
            Assert.IsFalse(match.ChooseDraft(Team.B, 0).Ok, "The AI picks Team B's card.");
            Assert.IsTrue(match.ChooseDraft(Team.A, 0).Ok);
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
            Assert.AreEqual(0, match.Player.NextSquadId, "The shared counter is used against the AI.");
            Assert.IsTrue(match.Buy(Team.A, Unit(match, Team.A, "Infantry")).Ok);
            CollectionAssert.IsEmpty(match.TakeCommands(Team.A));
            CollectionAssert.IsEmpty(match.TakeCommands(Team.B));
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            Assert.AreEqual(MatchPhase.Cards, match.Phase, "Round 2 shows the player the card screen, as before.");
            Assert.IsTrue(match.InCards(Team.A));
            Assert.IsFalse(match.InCards(Team.B), "The AI decided at once.");
        }
    }
}
