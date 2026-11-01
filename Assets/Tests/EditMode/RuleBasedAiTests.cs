// RuleBasedAiTests.cs
// Tests the rule-based AI (Milestone 5, Bible Section 11): it plays by the same limits as the
// player, counters the opponent's most expensive role, places squads by role (chaff and tanks in
// front, artillery behind, flankers on the sides), never sees the opponent's current deployment,
// differs by difficulty (Easy wastes budget, Hard adapts), and a full match between two AIs ends.
// The roster here is a test fixture with one unit per role, not game balance.
// Depends on: Halobellum.Economy, Halobellum.Combat, Halobellum.Core, NUnit, TestUnits, TestRules, TestMatch.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class RuleBasedAiTests
    {
        private static UnitStats Unit(string name, UnitRole role, int tier, int unlock, int squad, int cost, float health,
            float range = 12f, bool air = false, TargetFilter filter = TargetFilter.Both, float damage = 10f)
        {
            UnitStats unit = TestUnits.Unit(name: name, health: health, speed: 4f, radius: squad > 4 ? 0.5f : 1.2f, air: air,
                filter: filter, squadSize: squad, cost: cost, weapon: TestUnits.Weapon(damage: damage, range: range, interval: 0.5f));
            unit.Role = role;
            unit.Tier = tier;
            unit.UnlockCost = unlock;
            unit.BaseDamage = cost / (float)squad / 5f;
            return unit;
        }

        /// <summary>One unit per role (index = UnitRole).</summary>
        private static List<UnitStats> Roster() => new List<UnitStats>
        {
            Unit("Chaff", UnitRole.Chaff, 1, 0, 12, 100, 60),
            Unit("Chaff Clear", UnitRole.ChaffClear, 1, 0, 6, 150, 90),
            Unit("Sniper", UnitRole.SingleTargetDps, 2, 50, 1, 250, 300, range: 20f, damage: 60f),
            Unit("Tank", UnitRole.Tank, 2, 50, 1, 300, 1200),
            Unit("Raider", UnitRole.Flanker, 1, 0, 2, 200, 300),
            Unit("Artillery", UnitRole.Artillery, 2, 50, 2, 300, 350, range: 40f, filter: TargetFilter.Ground),
            Unit("Flak", UnitRole.AntiAir, 2, 50, 1, 300, 500),
            Unit("Gunship", UnitRole.Aerial, 2, 50, 2, 300, 400, air: true),
            Unit("Titan", UnitRole.Titan, 4, 350, 1, 800, 8000, damage: 100f),
        };

        private static Match NewMatch(IMatchAi enemy, int seed = 1)
        {
            List<UnitStats> roster = Roster();
            var factions = new List<FactionInfo> { TestMatch.Faction("Alpha", roster, "Standard"), TestMatch.Faction("Beta", roster, "Standard") };
            return new Match(TestMatch.Rules(), TestRules.Default(), factions, seed, enemy);
        }

        private static void PlayBattle(Match match)
        {
            match.StartBattle().Run();
            match.FinishBattle();
        }

        // ------------------------------------------------------------------ rules

        [TestCase(AiDifficulty.Easy)]
        [TestCase(AiDifficulty.Normal)]
        [TestCase(AiDifficulty.Hard)]
        public void PlaysByThePlayersLimits_AndSpendsItsCredits(AiDifficulty difficulty)
        {
            var ai = new RuleBasedAi(difficulty, 7);
            Match match = NewMatch(ai);
            match.ChooseDraft(0);
            int bought = 0;
            for (int round = 1; round <= 8 && match.Phase == MatchPhase.Deployment; round++)
            {
                PlayerState enemy = match.Enemy;
                Assert.LessOrEqual(enemy.SquadsBoughtThisRound, match.Rules.NewSquadsPerRound);
                Assert.LessOrEqual(enemy.UnlocksThisRound, match.Rules.UnlocksPerRound);
                Assert.GreaterOrEqual(enemy.Credits, 0);
                foreach (PlacedSquad squad in enemy.Squads)
                {
                    Assert.IsTrue(enemy.Unlocked.Contains(squad.Type), "Only unlocked units.");
                    Assert.IsTrue(enemy.Roster.Contains(squad.Type), "Only its own roster.");
                }
                bought += enemy.SquadsBoughtThisRound;
                PlayBattle(match);
                if (match.Phase == MatchPhase.Result) match.ContinueAfterResult();
            }
            Assert.Greater(bought, 0, "The AI buys squads.");
        }

        [Test]
        public void PicksTheMostValuableDraftCard_ExceptOnEasy()
        {
            var cards = new List<DraftCard>
            {
                new DraftCard { Squads = { Roster()[0], Roster()[0] } },  // 200
                new DraftCard { Squads = { Roster()[4], Roster()[1] } },  // 350
            };
            Assert.AreEqual(1, new RuleBasedAi(AiDifficulty.Normal, 1).ChooseDraftCard(null, cards));
            Assert.AreEqual(1, new RuleBasedAi(AiDifficulty.Hard, 1).ChooseDraftCard(null, cards));
        }

        // ------------------------------------------------------------------ countering

        /// <summary>Round 2 after the player fielded a big army of one unit type (seen by the AI in round 1's battle).</summary>
        private static (Match match, RuleBasedAi ai) AfterPlayerFielded(string unitName, int squads, AiDifficulty difficulty, int seed = 3, System.Action<IReadOnlyList<UnitStats>> prepare = null)
        {
            var ai = new RuleBasedAi(difficulty, seed);
            Match match = NewMatch(ai, seed);
            match.ChooseDraft(0);
            prepare?.Invoke(match.Player.Roster);
            match.Player.Squads.Clear();
            UnitStats type = match.Player.Roster.Single(u => u.Name == unitName);
            for (int i = 0; i < squads; i++)
            {
                Assert.IsTrue(DeploymentGrid.FindFreeSpot(match.Player, type, false, match.CombatRules, out int c, out int r));
                match.Player.Squads.Add(new PlacedSquad { Id = 1000 + i, Type = type, Column = c, Row = r, RoundBought = 1 });
            }
            match.Enemy.Credits = 5000; // Plenty, so the choice is about counters, not money.
            PlayBattle(match);
            match.Enemy.Credits = 5000;
            if (match.Phase == MatchPhase.Result) match.ContinueAfterResult(); // Round 2: the AI deploys now.
            return (match, ai);
        }

        private static List<PlacedSquad> BoughtThisRound(Match match) => match.Enemy.Squads.Where(s => s.RoundBought == match.Round).ToList();

        [TestCase("Tank", UnitRole.SingleTargetDps)]
        [TestCase("Chaff", UnitRole.ChaffClear)]
        [TestCase("Gunship", UnitRole.AntiAir)]
        public void Normal_CountersTheOpponentsMostExpensiveRole(string fielded, UnitRole expectedCounter)
        {
            var (match, ai) = AfterPlayerFielded(fielded, 4, AiDifficulty.Normal);
            Assert.AreEqual(match.Player.Roster.Single(u => u.Name == fielded).Role, ai.CounteredRole);
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsNotEmpty(bought, ai.LastDecision);
            Assert.IsTrue(bought.Any(s => s.Type.Role == expectedCounter), $"Expected a {expectedCounter} squad. {ai.LastDecision}");
        }

        /// <summary>The player's chaff is Flood; some of the roster has armor, the rest are Unarmored bodies.</summary>
        private static void FloodChaff(IReadOnlyList<UnitStats> roster)
        {
            foreach (UnitStats unit in roster)
            {
                if (unit.Name == "Chaff") { unit.IsFlood = true; unit.Infects = true; }
                if (unit.Name == "Tank" || unit.Name == "Titan") unit.ArmorType = ArmorType.Heavy;
                if (unit.Name == "Raider" || unit.Name == "Flak" || unit.Name == "Gunship") unit.ArmorType = ArmorType.Light;
            }
        }

        [TestCase(AiDifficulty.Normal)]
        [TestCase(AiDifficulty.Hard)]
        public void AgainstTheFlood_ItDoesNotBuyUnitsThatFeedIt(AiDifficulty difficulty)
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                var (match, ai) = AfterPlayerFielded("Chaff", 5, difficulty, seed, FloodChaff);
                foreach (PlacedSquad squad in BoughtThisRound(match))
                {
                    bool feedsIt = !squad.Type.IsFlood && squad.Type.ArmorType == ArmorType.Unarmored && squad.Type.Role != UnitRole.ChaffClear;
                    Assert.IsFalse(feedsIt, $"{squad.Type.Name}: {ai.LastDecision}");
                }
            }
        }

        [Test]
        public void Easy_DoesNotCareAboutTheFlood()
        {
            int fed = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                var (match, _) = AfterPlayerFielded("Chaff", 5, AiDifficulty.Easy, seed, FloodChaff);
                fed += BoughtThisRound(match).Count(s => !s.Type.IsFlood && s.Type.ArmorType == ArmorType.Unarmored && s.Type.Role != UnitRole.ChaffClear);
            }
            Assert.Greater(fed, 0, "Easy buys them anyway.");
        }

        [Test]
        public void Hard_AvoidsUnitsTheOpponentCounters()
        {
            // The player fielded chaff clear: Hard should not answer with chaff (chaff clear beats chaff).
            var (match, ai) = AfterPlayerFielded("Chaff Clear", 5, AiDifficulty.Hard);
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsNotEmpty(bought, ai.LastDecision);
            Assert.IsFalse(bought.Any(s => s.Type.Role == UnitRole.Chaff), ai.LastDecision);
            Assert.IsTrue(bought.Any(s => s.Type.Role == UnitRole.Tank || s.Type.Role == UnitRole.Flanker), ai.LastDecision);
        }

        [Test]
        public void Hard_WithMeasuredMatchups_PicksTheUnitThatActuallyWins()
        {
            // The role table says single-target DPS counters tanks; the measured table says only the
            // Titan beats this Tank. Hard trusts the measurements.
            var table = new MatchupTable();
            foreach (UnitStats unit in Roster()) table.Set(unit.Name, "Tank", unit.Name == "Titan" ? 1f : 0f);
            var ai = new RuleBasedAi(AiDifficulty.Hard, 3, table);
            Match match = NewMatch(ai, 3);
            match.ChooseDraft(0);
            match.Player.Squads.Clear();
            UnitStats tank = match.Player.Roster.Single(u => u.Name == "Tank");
            for (int i = 0; i < 4; i++)
            {
                DeploymentGrid.FindFreeSpot(match.Player, tank, false, match.CombatRules, out int c, out int r);
                match.Player.Squads.Add(new PlacedSquad { Id = 1000 + i, Type = tank, Column = c, Row = r });
            }
            match.Enemy.Credits = 5000;
            PlayBattle(match);
            match.Enemy.Credits = 5000;
            if (match.Phase == MatchPhase.Result) match.ContinueAfterResult();
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsTrue(bought.Any(s => s.Type.Name == "Titan"), ai.LastDecision);
            Assert.AreEqual(1f, table.WinRate(Roster()[8], tank));
            Assert.Greater(table.OverallStrength(Roster()[8]), table.OverallStrength(Roster()[0]));
        }

        [Test]
        public void Easy_LeavesMoreCreditsUnspent_ThanNormal()
        {
            int unspentEasy = 0, unspentNormal = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var easy = AfterPlayerFielded("Tank", 3, AiDifficulty.Easy, seed);
                var normal = AfterPlayerFielded("Tank", 3, AiDifficulty.Normal, seed);
                unspentEasy += easy.match.Enemy.Credits;
                unspentNormal += normal.match.Enemy.Credits;
            }
            Assert.Greater(unspentEasy, unspentNormal);
        }

        // ------------------------------------------------------------------ placement

        [Test]
        public void Places_ChaffAndTanksInFront_ArtilleryBehind_FlankersOnTheSides()
        {
            var ai = new RuleBasedAi(AiDifficulty.Normal, 1);
            Match match = NewMatch(ai);
            match.ChooseDraft(0);
            var view = new AiView(match, Team.B);
            PlayerState enemy = match.Enemy;
            enemy.Squads.Clear();
            enemy.Credits = 10000;
            foreach (UnitStats unit in enemy.Roster) enemy.Unlocked.Add(unit);

            // Place one squad of each role where the AI would put it.
            var rows = new Dictionary<UnitRole, int>();
            var columns = new Dictionary<UnitRole, int>();
            foreach (UnitStats unit in enemy.Roster)
            {
                enemy.SquadsBoughtThisRound = 0;
                Assert.IsTrue(ai.ChooseSpot(view, unit, out int column, out int row), unit.Name);
                Assert.IsTrue(view.Buy(unit, column, row, false).Ok, unit.Name);
                rows[unit.Role] = row;
                columns[unit.Role] = column;
            }
            Assert.Less(rows[UnitRole.Chaff], rows[UnitRole.SingleTargetDps], "Chaff in front of single-target DPS.");
            Assert.Less(rows[UnitRole.Tank], rows[UnitRole.SingleTargetDps], "Tanks in front of single-target DPS.");
            Assert.Less(rows[UnitRole.SingleTargetDps], rows[UnitRole.Artillery], "Artillery at the back.");
            int width = match.CombatRules.ArenaWidthSquares;
            Assert.IsTrue(columns[UnitRole.Flanker] < width / 5 || columns[UnitRole.Flanker] > width * 4 / 5, "Flankers on a side.");
            int centre = width / 2;
            Assert.Less(System.Math.Abs(columns[UnitRole.Tank] - centre), width / 4, "Tanks near the centre.");
        }

        // ------------------------------------------------------------------ no cheating

        [Test]
        public void NeverSeesTheOpponentsCurrentDeployment()
        {
            // Two identical matches; in the second the player secretly adds squads after the battle
            // started (as if bought next round). The AI's next deployment must be identical.
            string Deployment(bool playerAddsHiddenSquads)
            {
                var ai = new RuleBasedAi(AiDifficulty.Hard, 5);
                Match match = NewMatch(ai, 5);
                match.ChooseDraft(0);
                PlayBattle(match);
                if (playerAddsHiddenSquads)
                {
                    UnitStats titan = match.Player.Roster.Single(u => u.Name == "Titan");
                    DeploymentGrid.FindFreeSpot(match.Player, titan, false, match.CombatRules, out int c, out int r);
                    match.Player.Squads.Add(new PlacedSquad { Id = 999, Type = titan, Column = c, Row = r });
                }
                match.ContinueAfterResult();
                return string.Join(";", match.Enemy.Squads.Select(s => $"{s.Type.Name}@{s.Column},{s.Row}")) + "|" + ai.LastDecision;
            }
            Assert.AreEqual(Deployment(false), Deployment(true));
        }

        [Test]
        public void SeenArmy_IsTheArmyAtTheStartOfTheLastBattle()
        {
            Match match = NewMatch(new DoNothingAi());
            match.ChooseDraft(0);
            Assert.IsEmpty(match.SeenArmy(Team.A), "Nothing seen before the first battle.");
            Assert.IsNull(match.SeenFaction(Team.A));
            int squads = match.Player.Squads.Count;
            match.StartBattle();
            Assert.AreEqual(squads, match.SeenArmy(Team.A).Count);
            Assert.AreEqual("Alpha", match.SeenFaction(Team.A));
        }

        [Test]
        public void PlayersPreview_ShowsTheEnemyAsSeenAtTheLastBattle_NeverItsCurrentDeployment()
        {
            // Decision D25: during deployment the player sees the enemy's last-battle army (as ghosts).
            Match match = NewMatch(new DoNothingAi());
            match.ChooseDraft(0);
            int playerSquads = match.Player.Squads.Count;
            Assert.AreEqual(playerSquads, match.BuildPreview().Squads.Count, "Round 1: nothing of the enemy seen yet.");

            PlacedSquad veteran = match.Enemy.Squads[0];
            var plating = new TechUpgrade { Name = "Plating", Cost = 100, HealthMultiplier = 2f };
            veteran.Type.TechUpgrades = new[] { plating };
            match.Enemy.AddTech(veteran.Type, plating);
            veteran.Level = 2;
            int enemySquadsAtBattle = match.Enemy.Squads.Count;
            PlayBattle(match);
            match.ContinueAfterResult();

            // Round 2: the enemy buys a squad; the player must not see it.
            Assert.IsTrue(match.Buy(Team.B, veteran.Type).Ok);
            BattleSimulation preview = match.BuildPreview();
            Assert.AreEqual(playerSquads + enemySquadsAtBattle, preview.Squads.Count, "The player's squads, then the enemy's as seen at the last battle.");
            Assert.AreEqual(Team.B, preview.Squads[playerSquads].Team);
            SeenSquad seen = match.SeenArmy(Team.B)[0];
            Assert.AreEqual(2, seen.Level);
            CollectionAssert.AreEqual(new[] { plating }, seen.Techs, "Techs show in battle, so they are seen.");
            Assert.AreEqual(veteran.Type.MaxHealth * 2f * 2f, preview.Squads[playerSquads].Members[0].Stats.MaxHealth, 1e-3f,
                "The ghost has the level and techs it fought with.");
        }

        // ------------------------------------------------------------------ full match

        [TestCase(AiDifficulty.Normal, AiDifficulty.Normal)]
        [TestCase(AiDifficulty.Hard, AiDifficulty.Easy)]
        public void AFullMatchBetweenTwoAis_Ends(AiDifficulty playerSide, AiDifficulty enemySide)
        {
            var enemy = new RuleBasedAi(enemySide, 11);
            var player = new RuleBasedAi(playerSide, 12);
            Match match = NewMatch(enemy, 11);
            match.ChooseDraft(player.ChooseDraftCard(new AiView(match, Team.A), match.PlayerDraft));
            int rounds = 0;
            while (match.Phase != MatchPhase.MatchOver && rounds < 60)
            {
                player.Deploy(new AiView(match, Team.A));
                PlayBattle(match);
                if (match.Phase == MatchPhase.Result) match.ContinueAfterResult();
                rounds++;
            }
            Assert.AreEqual(MatchPhase.MatchOver, match.Phase, $"No winner after {rounds} rounds.");
            Assert.IsTrue(match.Winner.HasValue);
        }
    }
}
