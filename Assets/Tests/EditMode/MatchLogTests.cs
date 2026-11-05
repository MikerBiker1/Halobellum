// MatchLogTests.cs
// Tests the battle log of a person's game (D68): it says what was offered, what was done, both armies going into each battle and how each went;
// its base health can be below zero; what it writes is well formed; and writing it changes nothing in the match.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestMatch.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class MatchLogTests
    {
        private static List<string> PlayARound(out Match match, int enemyBase = 2000)
        {
            match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            match.Enemy.BaseHealth = enemyBase;
            var lines = new List<string>();
            var log = new MatchLog(lines.Add);
            log.Started(match, "Hard", new System.DateTime(2026, 11, 5, 18, 30, 0), 0);
            log.PlayerAction(match, "Buy", infantry.Name, match.Buy(Team.A, infantry, 0, 5, false));
            log.PlayerAction(match, "Sell", "nothing", ActionResult.Fail("Select a squad first."));
            BattleSimulation battle = match.StartBattle();
            log.BattleStarted(match);
            battle.Run();
            log.BattleFinished(match.FinishBattle());
            if (match.Phase == MatchPhase.MatchOver) log.Finished(match);
            return lines;
        }

        [Test]
        public void TheLog_SaysWhatWasDone_BothArmies_AndHowTheBattleWent()
        {
            List<string> lines = PlayARound(out Match match);
            StringAssert.StartsWith("{\"type\":\"match_start\"", lines[0]);
            StringAssert.Contains($"\"seed\":{match.Seed}", lines[0]);
            StringAssert.Contains("\"difficulty\":\"Hard\"", lines[0]);
            StringAssert.Contains("\"time\":\"2026-11-05T18:30:00\"", lines[0]);

            StringAssert.StartsWith("{\"type\":\"action\"", lines[1]);
            StringAssert.Contains("\"action\":\"Buy\"", lines[1]);
            StringAssert.Contains("\"ok\":true", lines[1]);
            StringAssert.Contains("\"refused\":\"Select a squad first.\"", lines[2]);

            string start = lines.Single(l => l.Contains("\"type\":\"battle_start\""));
            foreach (PlacedSquad squad in match.Player.Squads.Concat(match.Enemy.Squads))
                StringAssert.Contains($"\"id\":{squad.Id},\"unit\":\"{squad.Type.Name}\",\"column\":{squad.Column},\"row\":{squad.Row}", start, "Both armies go in, where they stood.");

            string end = lines.Single(l => l.Contains("\"type\":\"battle_end\""));
            StringAssert.Contains($"\"base_damage_by_player\":{match.LastSummary.BaseDamageByA}", end);
            StringAssert.Contains($"\"player_base\":{match.Player.BaseHealth}", end);
            StringAssert.Contains("\"player_army\":{", end);
        }

        [Test]
        public void ABaseOverkilled_IsLoggedBelowZero_AndTheMatchsEndIsWritten()
        {
            List<string> lines = PlayARound(out Match match, enemyBase: 1);
            if (match.LastSummary.BaseDamageByA < 2) Assert.Inconclusive("The army did too little damage to overkill a base of 1 here.");
            Assert.Less(match.Enemy.BaseHealth, 0, "A base's health goes on below zero, so that the overkill can be seen.");
            StringAssert.Contains($"\"enemy_base\":{match.Enemy.BaseHealth}", lines.Single(l => l.Contains("\"type\":\"battle_end\"")));
            StringAssert.Contains("\"winner\":\"player\"", lines.Last());
        }

        [Test]
        public void EveryLine_IsOneWholeObject()
        {
            foreach (string line in PlayARound(out _))
            {
                StringAssert.DoesNotContain("\n", line);
                int depth = 0;
                bool quoted = false;
                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (quoted)
                    {
                        if (c == '\\') i++;
                        else if (c == '"') quoted = false;
                        continue;
                    }
                    if (c == '"') quoted = true;
                    else if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') depth--;
                    if (i < line.Length - 1) Assert.Greater(depth, 0, line);
                }
                Assert.AreEqual(0, depth, line);
                Assert.IsFalse(quoted, line);
            }
        }

        [Test]
        public void Names_AreWrittenSafely()
        {
            Assert.AreEqual("\"a \\\"b\\\" \\\\ c\\n\"", MatchLog.Quote("a \"b\" \\ c\n"));
            Assert.AreEqual("null", MatchLog.Quote(null));
            Assert.AreEqual("\"\\u0001\"", MatchLog.Quote("\u0001"));
        }

        [Test]
        public void Writing_ChangesNothingInTheMatch()
        {
            string Play(bool logged)
            {
                Match match = TestMatch.Started(out UnitStats infantry, out _, out _, out _, seed: 11);
                var log = logged ? new MatchLog(_ => { }) : null;
                log?.Started(match, "Hard", System.DateTime.MinValue, 0);
                match.Buy(Team.A, infantry, 0, 5, false);
                BattleSimulation battle = match.StartBattle();
                log?.BattleStarted(match);
                battle.Run();
                RoundSummary summary = match.FinishBattle();
                log?.BattleFinished(summary);
                return summary.Battle.ToSummaryLine();
            }
            Assert.AreEqual(Play(false), Play(true));
        }
    }
}
