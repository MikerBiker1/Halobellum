// EconomyTests.cs
// Tests the economy (Bible Section 4, rule 10: economy and income cap, unlock rules): income per
// round and its cap, banked credits, buying, 2 squads per round, 1 unlock per round with unlock
// prices, refunds, and which squads can move or turn.
// Depends on: Halobellum.Economy, Halobellum.Core, NUnit, TestMatch.

using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class EconomyTests
    {
        private Match _match;
        private UnitStats _infantry, _buggy, _tank, _walker;
        private PlayerState Player => _match.Player;

        [SetUp]
        public void SetUp()
        {
            _match = TestMatch.Started(out _infantry, out _buggy, out _tank, out _walker);
        }

        /// <summary>Plays the current round's battle and moves on to the next deployment.</summary>
        private void PlayRound()
        {
            var battle = _match.StartBattle();
            battle.Run();
            _match.FinishBattle();
            _match.ContinueAfterResult();
        }

        [TestCase(1, 400)]
        [TestCase(2, 600)]
        [TestCase(3, 800)]
        [TestCase(6, 1400)]
        [TestCase(7, 1600)]
        [TestCase(8, 1600)]
        [TestCase(20, 1600)]
        public void IncomePerRound_GrowsBy200_AndIsCappedAt1600(int round, int expected)
        {
            Assert.AreEqual(expected, TestMatch.Rules().IncomeForRound(round));
        }

        [Test]
        public void Round1_StartsWithStartingCredits()
        {
            Assert.AreEqual(1, _match.Round);
            Assert.AreEqual(200, Player.Credits);
            Assert.AreEqual(200, _match.Enemy.Credits);
            Assert.AreEqual(600, _match.NextIncome);
        }

        [Test]
        public void IncomeIsAwardedAfterEachRound_AndBankedCreditsHaveNoLimit()
        {
            PlayRound(); // +600
            Assert.AreEqual(2, _match.Round);
            Assert.AreEqual(800, Player.Credits);
            for (int i = 0; i < 8; i++) PlayRound(); // +800 +1000 +1200 +1400 +1600 +1600 +1600 +1600
            Assert.AreEqual(800 + 800 + 1000 + 1200 + 1400 + 1600 * 4, Player.Credits);
        }

        [Test]
        public void Buy_SpendsCredits_AndPlacesTheSquad()
        {
            var result = _match.Buy(Team.A, _infantry, 0, 10, false);
            Assert.IsTrue(result.Ok, result.Message);
            Assert.AreEqual(100, Player.Credits);
            Assert.AreEqual(3, Player.Squads.Count); // 2 free draft squads + 1 bought.
        }

        [Test]
        public void Buy_WithoutASpot_PlacesTheSquadAtTheFreeSpotNearestTheMiddle()
        {
            Assert.IsTrue(DeploymentGrid.FindFreeSpot(Player, _infantry, false, _match.CombatRules, out int column, out int row));
            Assert.IsTrue(_match.Buy(Team.A, _infantry).Ok);
            PlacedSquad bought = Player.Squads[Player.Squads.Count - 1];
            Assert.AreEqual(column, bought.Column);
            Assert.AreEqual(row, bought.Row);
            Assert.IsFalse(bought.Rotated90);
            Assert.AreEqual(100, Player.Credits);
        }

        [Test]
        public void Buy_NeedsEnoughCredits()
        {
            Player.Credits = 50;
            Assert.IsFalse(_match.Buy(Team.A, _infantry, 0, 10, false).Ok);
            Assert.AreEqual(50, Player.Credits);
        }

        [Test]
        public void Buy_AtMostTwoNewSquadsPerRound_DraftSquadsDontCount()
        {
            Player.Credits = 5000;
            Assert.IsTrue(_match.Buy(Team.A, _infantry, 0, 10, false).Ok);
            Assert.IsTrue(_match.Buy(Team.A, _infantry, 10, 10, false).Ok);
            Assert.IsFalse(_match.Buy(Team.A, _infantry, 20, 10, false).Ok, "A third squad in one round should be refused.");
            PlayRound();
            Assert.IsTrue(_match.Buy(Team.A, _infantry, 20, 10, false).Ok, "The limit resets each round.");
        }

        [Test]
        public void Buy_OnlyUnlockedTypes()
        {
            Player.Credits = 5000;
            Assert.IsFalse(Player.Unlocked.Contains(_tank));
            Assert.IsFalse(_match.Buy(Team.A, _tank, 0, 10, false).Ok);
        }

        [Test]
        public void Unlock_CostsTheUnitsUnlockPrice_AndAllowsBuyingTheSameRound()
        {
            Player.Credits = 1000;
            Assert.IsTrue(_match.Unlock(Team.A, _tank).Ok);
            Assert.AreEqual(950, Player.Credits);
            Assert.IsTrue(_match.Buy(Team.A, _tank, 0, 10, false).Ok);
            Assert.AreEqual(650, Player.Credits);
        }

        [Test]
        public void Unlock_OnlyOnePerRound()
        {
            Player.Credits = 5000;
            Assert.IsTrue(_match.Unlock(Team.A, _tank).Ok);
            Assert.IsFalse(_match.Unlock(Team.A, _walker).Ok);
            PlayRound();
            Assert.IsTrue(_match.Unlock(Team.A, _walker).Ok);
            Assert.AreEqual(5000 - 50 + 600 - 225, Player.Credits);
        }

        [Test]
        public void Unlock_TierOneIsFree_ButCountsAsTheRoundsUnlock()
        {
            // Card 0 gave two Tier 1 types; lock one again to test unlocking it.
            Player.Unlocked.Remove(_buggy);
            Player.Unlocked.Remove(_infantry);
            Assert.IsTrue(_match.Unlock(Team.A, _buggy).Ok);
            Assert.AreEqual(200, Player.Credits);
            Assert.IsFalse(_match.Unlock(Team.A, _infantry).Ok);
        }

        [Test]
        public void Unlock_OnlyFromOwnRoster_AndNotTwice()
        {
            Player.Credits = 5000;
            UnitStats foreign = TestMatch.Tank();
            Assert.IsFalse(_match.Unlock(Team.A, foreign).Ok);
            Assert.IsTrue(_match.Unlock(Team.A, _tank).Ok);
            PlayRound();
            Assert.IsFalse(_match.Unlock(Team.A, _tank).Ok);
        }

        [Test]
        public void Sell_BeforeTheBattle_RefundsEverything_AndFreesTheSlot()
        {
            Player.Credits = 5000;
            _match.Buy(Team.A, _infantry, 0, 10, false);
            _match.Buy(Team.A, _infantry, 10, 10, false);
            PlacedSquad bought = Player.Squads[Player.Squads.Count - 1];
            Assert.IsTrue(_match.Sell(Team.A, bought.Id).Ok);
            Assert.AreEqual(4900, Player.Credits);
            Assert.IsTrue(_match.Buy(Team.A, _infantry, 20, 10, false).Ok, "Selling a squad bought this round frees its slot.");
        }

        [Test]
        public void Sell_AfterItsFirstBattle_RefundsHalf()
        {
            _match.Buy(Team.A, _infantry, 0, 10, false);
            PlacedSquad bought = Player.Squads[Player.Squads.Count - 1];
            PlayRound();
            int before = Player.Credits;
            Assert.AreEqual(50, _match.SellValue(bought));
            Assert.IsTrue(_match.Sell(Team.A, bought.Id).Ok);
            Assert.AreEqual(before + 50, Player.Credits);
        }

        [Test]
        public void FreeDraftSquads_SellForHalfTheirCost_AndDontFreeASlot()
        {
            PlacedSquad free = Player.Squads[0];
            Assert.IsTrue(free.FromDraft);
            Assert.AreEqual(free.Type.Cost / 2, _match.SellValue(free), "Half the squad's cost, even in round 1.");

            PlayRound();
            Assert.AreEqual(free.Type.Cost / 2, _match.SellValue(free), "Still half after fighting.");
            Player.Credits = 5000;
            Assert.IsTrue(_match.Buy(Team.A, _infantry, 0, 10, false).Ok);
            Assert.IsTrue(_match.Buy(Team.A, _infantry, 10, 10, false).Ok);
            Assert.IsTrue(_match.Sell(Team.A, free.Id).Ok);
            Assert.AreEqual(5000 - 200 + free.Type.Cost / 2, Player.Credits);
            Assert.IsFalse(_match.Buy(Team.A, _infantry, 20, 10, false).Ok, "Selling a draft squad does not free a slot.");
        }

        [Test]
        public void OnlySquadsBoughtThisRound_CanMoveOrTurn()
        {
            _match.Buy(Team.A, _infantry, 0, 10, false);
            PlacedSquad bought = Player.Squads[Player.Squads.Count - 1];
            Assert.IsTrue(_match.Move(Team.A, bought.Id, 5, 12, true).Ok);
            Assert.AreEqual(5, bought.Column);
            Assert.IsTrue(bought.Rotated90);

            PlayRound();
            Assert.IsFalse(_match.Move(Team.A, bought.Id, 30, 20, false).Ok, "Squads from earlier rounds stay where they are.");
            Assert.AreEqual(5, bought.Column);
            Assert.IsTrue(bought.Rotated90, "Orientation is kept.");
        }

        [Test]
        public void Actions_AreRefusedOutsideDeployment()
        {
            _match.StartBattle();
            Assert.IsFalse(_match.Buy(Team.A, _infantry, 0, 10, false).Ok);
            Assert.IsFalse(_match.Unlock(Team.A, _tank).Ok);
            Assert.IsFalse(_match.Sell(Team.A, Player.Squads[0].Id).Ok);
        }
    }
}
