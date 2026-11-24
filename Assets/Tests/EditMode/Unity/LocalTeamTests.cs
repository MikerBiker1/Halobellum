// LocalTeamTests.cs
// M11 Stage 3: the screens' words and numbers from the local side's point of view. A LAN guest plays Team B and must read its own
// results ("you" = Team B), its own income and squads ready to level up, the towers it has seen of the host's, and land its drags in
// its own grid (Team A's turned 180 degrees). With local = Team A every text is exactly the single-player one.
// Depends on: Halobellum.UI (LocalSideTexts, BattleHud), Halobellum.Presentation (MatchController, DeploymentInput, BattleCameraController),
//             Halobellum.Economy, Halobellum.Combat, Halobellum.Core, LockstepTestKit, TestMatch, TestRules, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.Tests.Net;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class LocalTeamTests
    {
        /// <summary>A round where A dealt 120 to B's base and B dealt 45 to A's; A kept 7 of 10 bodies, B 2 of 8.</summary>
        private static RoundSummary Summary()
        {
            var s = new RoundSummary
            {
                Round = 3, BaseDamageByA = 120, BaseDamageByB = 45, BaseHealthA = 1955, BaseHealthB = 1880, IncomeAwarded = 310, IncomeAwardedB = 260,
                Battle = new BattleResult { TeamA = new TeamResult { Team = Team.A, Deployed = 10, Survivors = 7 }, TeamB = new TeamResult { Team = Team.B, Deployed = 8, Survivors = 2 } },
            };
            s.PlayerSquadsReadyToLevel.AddRange(new[] { "Marines", "Marines", "Warthog" });
            s.EnemySquadsReadyToLevel.Add("Grunts");
            return s;
        }

        [Test]
        public void TheRoundWord_IsFromTheLocalSide()
        {
            Assert.AreEqual("MATCH WON", LocalSideTexts.RoundWord(Team.A, Team.A));
            Assert.AreEqual("MATCH LOST", LocalSideTexts.RoundWord(Team.B, Team.A));
            Assert.AreEqual("MATCH LOST", LocalSideTexts.RoundWord(Team.A, Team.B), "The guest lost a round Team A won.");
            Assert.AreEqual("MATCH WON", LocalSideTexts.RoundWord(Team.B, Team.B));
            Assert.AreEqual("MATCH DRAWN", LocalSideTexts.RoundWord(null, Team.B));
        }

        [Test]
        public void TheResultLines_ForTeamA_AreTheSinglePlayerOnes()
        {
            RoundSummary s = Summary();
            Assert.AreEqual("Round 3: you hit the enemy base for 120, it hit yours for 45", LocalSideTexts.RoundLine(s, Team.A));
            Assert.AreEqual("Base damage: you dealt 120, the enemy dealt 45", LocalSideTexts.BaseDamageLine(s, Team.A));
            Assert.AreEqual("Bases: yours 1955, enemy 1880", LocalSideTexts.BasesLine(s, Team.A));
            Assert.AreEqual("Survivors: yours 7 of 10, enemy 2 of 8", LocalSideTexts.SurvivorsLine(s, Team.A));
            Assert.AreEqual("Ready to level up: Marines, Warthog (select it next round)", LocalSideTexts.ReadyToLevelLine(s, Team.A));
            Assert.AreEqual("Income: +310 credits for next round.", LocalSideTexts.ResultFooter(s, Team.A));
            Assert.AreEqual("Your", LocalSideTexts.SideWord(Team.A, Team.A));
            Assert.AreEqual("Enemy", LocalSideTexts.SideWord(Team.B, Team.A));
        }

        [Test]
        public void TheResultLines_ForTeamB_TellTheGuestItsOwnNumbers()
        {
            RoundSummary s = Summary();
            Assert.AreEqual("Round 3: you hit the enemy base for 45, it hit yours for 120", LocalSideTexts.RoundLine(s, Team.B));
            Assert.AreEqual("Base damage: you dealt 45, the enemy dealt 120", LocalSideTexts.BaseDamageLine(s, Team.B));
            Assert.AreEqual("Bases: yours 1880, enemy 1955", LocalSideTexts.BasesLine(s, Team.B));
            Assert.AreEqual("Survivors: yours 2 of 8, enemy 7 of 10", LocalSideTexts.SurvivorsLine(s, Team.B));
            Assert.AreEqual("Ready to level up: Grunts (select it next round)", LocalSideTexts.ReadyToLevelLine(s, Team.B));
            Assert.AreEqual("Income: +260 credits for next round.", LocalSideTexts.ResultFooter(s, Team.B));
            Assert.AreEqual("Your", LocalSideTexts.SideWord(Team.B, Team.B));
            Assert.AreEqual("Enemy", LocalSideTexts.SideWord(Team.A, Team.B));

            s.Tie = true;
            s.EnemySquadsReadyToLevel.Clear();
            s.Battle.TimedOut = true;
            Assert.IsNull(LocalSideTexts.ReadyToLevelLine(s, Team.B), "No line when none of the guest's squads is ready.");
            Assert.AreEqual("Exact tie: both bases are restored. Income: +260 credits for next round.", LocalSideTexts.ResultFooter(s, Team.B));
            Assert.AreEqual("Survivors: yours 2 of 8, enemy 7 of 10  (time limit)", LocalSideTexts.SurvivorsLine(s, Team.B));
        }

        [Test]
        public void WhenBothArmiesFell_TheLineIsTheSameForBothSides()
        {
            RoundSummary s = Summary();
            s.Battle.TeamA.Survivors = 0;
            s.Battle.TeamB.Survivors = 0;
            Assert.AreEqual("Round 3: both armies fell, and neither base was hit", LocalSideTexts.RoundLine(s, Team.A));
            Assert.AreEqual(LocalSideTexts.RoundLine(s, Team.A), LocalSideTexts.RoundLine(s, Team.B));
        }

        [Test]
        public void WhenTheHostsResultWasUsed_TheTextsSaySoAndNeverReadTheGuestsOwnBattleAsTheResult()
        {
            // M11: the guest's own battle ended with both armies down; the host's result (with base damage) is the one used.
            RoundSummary s = Summary();
            s.Battle.TeamA.Survivors = 0;
            s.Battle.TeamB.Survivors = 0;
            Assert.AreEqual("Round 3: you hit the enemy base for 45, it hit yours for 120", LocalSideTexts.RoundLine(s, Team.B, hostResult: true));
            Assert.AreEqual("In this game's battle, survivors: yours 0 of 8, enemy 0 of 10", LocalSideTexts.SurvivorsLine(s, Team.B, hostResult: true));
            Assert.AreEqual("Top damage dealers (this game's battle)", LocalSideTexts.BattleHeading("Top damage dealers", true));
            Assert.AreEqual("Top damage dealers", LocalSideTexts.BattleHeading("Top damage dealers", false), "Unchanged otherwise.");
            StringAssert.Contains("host's result is used", LocalSideTexts.HostResultNote);
            Assert.AreEqual("Round 3: both armies fell, and neither base was hit", LocalSideTexts.RoundLine(s, Team.B), "Single-player and agreeing rounds: as before.");
        }

        private static Match TwoHumans(int seed = 5) => new Match(LockstepTestKit.Rules(), TestRules.Default(), LockstepTestKit.Factions(), seed, MatchSeats.TwoHumans);

        [Test]
        public void TheMatchOverLine_IsFromTheLocalSide()
        {
            Match m = TwoHumans();
            m.Player.BaseHealth = 700;
            m.Enemy.BaseHealth = 1200;
            // No winner yet: neither side has won, and each reads its own base.
            Assert.AreEqual("DEFEAT", LocalSideTexts.GameWord(m, Team.A));
            Assert.AreEqual($"You lost the game in round {m.Round}: your base ended at 700", LocalSideTexts.GameLine(m, Team.A));
            Assert.AreEqual($"You lost the game in round {m.Round}: your base ended at 1200", LocalSideTexts.GameLine(m, Team.B));
        }

        [Test]
        public void TheMatchOverLine_OfAWonMatch_SaysWonOnlyOnTheWinnersScreen()
        {
            // A whole small match between two Insane AIs, through the Economy only.
            Match m = TwoHumans(23);
            Assert.IsTrue(m.ChooseDraft(Team.A, 0).Ok);
            Assert.IsTrue(m.ChooseDraft(Team.B, 0).Ok);
            m.Player.BaseHealth = m.Enemy.BaseHealth = 1; // The first base hit ends it.
            for (int round = 0; round < 10 && m.Phase != MatchPhase.MatchOver; round++)
            {
                if (m.Phase == MatchPhase.Result) m.ContinueAfterResult();
                foreach (Team team in new[] { Team.A, Team.B }) if (m.Get(team).Offer != null) m.SkipCards(team);
                m.Enemy.Squads.Clear(); // Team B fields nobody: Team A's army reaches its base.
                m.StartBattle().Run();
                m.FinishBattle();
            }
            Assume.That(m.Phase == MatchPhase.MatchOver && m.Winner == Team.A, "The match did not end with Team A the winner.");
            Assert.AreEqual("VICTORY", LocalSideTexts.GameWord(m, Team.A));
            Assert.AreEqual("DEFEAT", LocalSideTexts.GameWord(m, Team.B));
            Assert.AreEqual($"You won the game in round {m.Round}: the enemy base ended at {m.Enemy.BaseHealth}", LocalSideTexts.GameLine(m, Team.A));
            Assert.AreEqual($"You lost the game in round {m.Round}: your base ended at {m.Enemy.BaseHealth}", LocalSideTexts.GameLine(m, Team.B));
        }

        [Test]
        public void ATowerWindow_ShowsTheOtherSidesLevel_AsLastSeen_ForEitherSide()
        {
            Match m = TwoHumans();
            Assert.IsTrue(m.ChooseDraft(Team.A, 0).Ok);
            Assert.IsTrue(m.ChooseDraft(Team.B, 0).Ok);
            Assert.AreEqual(MatchPhase.Deployment, m.Phase);
            m.Player.Credits += 1000;
            Assert.IsTrue(m.LevelUpTower(Team.A, TowerKind.CommandCenter).Ok, "The host levels up its Command Center.");

            Assert.AreEqual(2, MatchController.ShownTowerLevel(m, Team.A, Team.A, TowerKind.CommandCenter), "Its own tower: the real level.");
            Assert.AreEqual(1, MatchController.ShownTowerLevel(m, Team.B, Team.A, TowerKind.CommandCenter), "The guest has not seen it yet.");
            Assert.AreEqual(1, MatchController.ShownTowerLevel(m, Team.A, Team.B, TowerKind.CommandCenter));

            m.StartBattle();
            Assert.AreEqual(2, MatchController.ShownTowerLevel(m, Team.B, Team.A, TowerKind.CommandCenter), "Seen at the battle start.");
            Assert.AreEqual(m.SeenEnemyTowerLevel(TowerKind.CommandCenter), MatchController.ShownTowerLevel(m, Team.A, Team.B, TowerKind.CommandCenter), "Team A reads what it always did.");
        }

        [Test]
        public void AGuestsDrag_LandsInTheCellUnderIt_InItsOwnGrid()
        {
            CombatRules rules = TestRules.Default();
            UnitStats tank = TestMatch.Tank();
            DeploymentGrid.Footprint(tank, false, rules, out int width, out int depth);
            foreach (Team team in new[] { Team.A, Team.B })
            {
                for (int column = 0; column < 4; column++)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        Vec2 centre = DeploymentGrid.WorldCentre(tank, column, row, false, team, rules);
                        DeploymentInput.CellUnder(new Vector3(centre.X, 0f, centre.Y), team, width, depth, rules, out int c, out int r);
                        Assert.AreEqual((column, row), (c, r), $"{team}: the cell whose centre is under the pointer.");
                    }
                }
            }
            // Team B's grid is Team A's turned 180 degrees: the same cell is at the opposite point.
            Vec2 a = DeploymentGrid.WorldCentre(tank, 2, 1, false, Team.A, rules), b = DeploymentGrid.WorldCentre(tank, 2, 1, false, Team.B, rules);
            Assert.AreEqual(-a.X, b.X, 1e-4f);
            Assert.AreEqual(-a.Y, b.Y, 1e-4f);
            DeploymentInput.CellUnder(new Vector3(-a.X, 0f, -a.Y), Team.B, width, depth, rules, out int bc, out int br);
            Assert.AreEqual((2, 1), (bc, br));
        }

        [Test]
        public void TheCameraHome_IsTurnedForTheGuest()
        {
            var focus = new Vector2(0f, -5f);
            Assert.AreEqual(0f, BattleCameraController.HomeYaw(Team.A));
            Assert.AreEqual(180f, BattleCameraController.HomeYaw(Team.B));
            Assert.AreEqual(focus, BattleCameraController.HomeFocus(focus, Team.A));
            Assert.AreEqual(new Vector2(0f, 5f), BattleCameraController.HomeFocus(focus, Team.B));
        }

        [Test]
        public void TheBattleBar_PutsTheLocalSidesCountOnTheLeft()
        {
            Assert.AreEqual("Team A  12        3.5 s / 90 s        9  Team B", BattleHud.TopLine(Team.A, 12, 9, 3.5f, 90f));
            Assert.AreEqual("Team B  9        3.5 s / 90 s        12  Team A", BattleHud.TopLine(Team.B, 9, 12, 3.5f, 90f));
        }

        [Test]
        public void TheBattleStatsMap_IsTurnedForTheGuest()
        {
            var stats = new BattleStats { FieldWidth = 100f, FieldLength = 200f };
            var map = new Rect(0f, 0f, 100f, 200f);
            Vec2 nearA = new Vec2(-30f, -80f); // Team A's left, near its edge.
            Vector2 forA = BattleStatsWindow.OnMap(map, stats, nearA);
            Assert.AreEqual(20f, forA.x, 1e-3f, "Team A: its left on the left.");
            Assert.AreEqual(180f, forA.y, 1e-3f, "Team A: its edge at the bottom.");
            Vector2 forB = BattleStatsWindow.OnMap(map, stats, new Vec2(30f, 80f), turned: true);
            Assert.AreEqual(forA.x, forB.x, 1e-3f, "Team B's own corner is where Team A's is on Team A's map.");
            Assert.AreEqual(forA.y, forB.y, 1e-3f, "Team B's own corner is where Team A's is on Team A's map.");
        }

        [Test]
        public void TheOpponentLabel_NamesTheAi_OrAnotherPlayer()
        {
            Assert.AreEqual("Other player", MatchHud.OpponentLabel(TwoHumans()));
            Match single = TestMatch.Started(out _, out _, out _, out _);
            Assert.AreEqual(single.EnemyAi.Label, MatchHud.OpponentLabel(single));
        }
    }
}
