// DeploymentUndoTests.cs
// Undo in deployment (the owner, 2026-11-06): each deployment action this round can be taken back, one at a time, back to
// the start of deployment; the card choice is the line it stops at; the history ends with the round; squads keep their objects.
// The match records a step when told an action worked (MatchController does it after each player action), as here.
// Depends on: Halobellum.Economy, Halobellum.Core, NUnit, TestMatch.

using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class DeploymentUndoTests
    {
        private static void Do(Match match, ActionResult result)
        {
            Assert.IsTrue(result.Ok, result.Message);
            match.RecordPlayerAction();
        }

        [Test]
        public void UndoTakesBackEachAction_InTurn_BackToTheStartOfDeployment()
        {
            MatchRules rules = TestMatch.Rules();
            rules.StartingCredits = 1000; // Enough to unlock and buy a tank.
            var match = new Match(rules, TestRules.Default(), TestMatch.StandardFactions(out _, out _, out UnitStats tank, out _), 1, new DoNothingAi());
            match.ChooseDraft(0);
            PlayerState player = match.Player;
            Assert.IsFalse(match.CanUndo, "Nothing done yet.");
            Assert.IsFalse(match.Undo().Ok);

            int credits0 = player.Credits, squads0 = player.Squads.Count;
            Do(match, match.Unlock(Team.A, tank));
            int credits1 = player.Credits;
            Do(match, match.Buy(Team.A, tank));
            PlacedSquad bought = player.Squads.Last();
            int column = bought.Column, row = bought.Row;
            Do(match, match.Move(Team.A, bought.Id, column + 4, row, false));
            Assert.AreEqual(3, match.UndoSteps);

            Assert.IsTrue(match.Undo().Ok);
            Assert.AreEqual(column, bought.Column, "The move is taken back.");
            Assert.AreSame(bought, player.Squads.Last(), "The squad keeps its object.");

            Assert.IsTrue(match.Undo().Ok);
            Assert.AreEqual(squads0, player.Squads.Count, "The buy is taken back.");
            Assert.AreEqual(credits1, player.Credits, "And paid back.");
            Assert.IsTrue(player.Unlocked.Contains(tank));

            Assert.IsTrue(match.Undo().Ok);
            Assert.IsFalse(player.Unlocked.Contains(tank), "The unlock is taken back.");
            Assert.AreEqual(credits0, player.Credits);
            Assert.AreEqual(0, player.UnlocksThisRound);
            Assert.IsFalse(match.CanUndo, "Back at the start of deployment.");
        }

        [Test]
        public void ASell_AndALevelUp_AreTakenBack()
        {
            Match match = TestMatch.Started(out _, out _, out _, out _);
            PlayerState player = match.Player;
            PlacedSquad squad = player.Squads[0];
            int credits = player.Credits;
            Do(match, match.Sell(Team.A, squad.Id));
            Assert.IsNull(player.FindSquad(squad.Id));
            Assert.IsTrue(match.Undo().Ok);
            Assert.AreSame(squad, player.FindSquad(squad.Id), "The sold squad is back, the same object.");
            Assert.AreEqual(credits, player.Credits);

            squad.Xp = match.XpNeeded(squad);
            int level = squad.Level;
            Do(match, match.LevelUp(Team.A, squad.Id));
            Assert.AreEqual(level + 1, squad.Level);
            Assert.IsTrue(match.Undo().Ok);
            Assert.AreEqual(level, squad.Level);
            Assert.AreEqual(credits, player.Credits);
        }

        [Test]
        public void TheCardChoice_CannotBeUndone()
        {
            var match = new Match(TestMatch.RulesWithCards(), TestRules.Default(), TestMatch.StandardFactions(out UnitStats infantry, out _, out _, out _), 1, new DoNothingAi());
            match.ChooseDraft(0);
            match.StartBattle().Run();
            match.FinishBattle();
            match.ContinueAfterResult();
            Assert.AreEqual(MatchPhase.Cards, match.Phase);
            Assert.IsTrue(match.SkipCards(Team.A).Ok);
            int afterCard = match.Player.Credits;
            Assert.IsFalse(match.CanUndo, "The card choice can't be undone.");

            Do(match, match.Buy(Team.A, infantry));
            Assert.IsTrue(match.Undo().Ok);
            Assert.AreEqual(afterCard, match.Player.Credits, "Undo stops after the card: its bonus stays.");
            Assert.IsFalse(match.CanUndo);
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
        }

        [Test]
        public void TheHistory_EndsWithTheRound()
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            Do(match, match.Buy(Team.A, infantry));
            Assert.IsTrue(match.CanUndo);
            match.StartBattle().Run();
            Assert.IsFalse(match.CanUndo, "Not during the battle.");
            Assert.IsFalse(match.Undo().Ok);
            match.FinishBattle();
            match.ContinueAfterResult();
            Assert.AreEqual(MatchPhase.Deployment, match.Phase);
            Assert.IsFalse(match.CanUndo, "Last round's actions are gone.");
        }
    }
}
