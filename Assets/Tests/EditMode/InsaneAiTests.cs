// InsaneAiTests.cs
// The Insane AI (D123): it reads the last battle, stops buying a unit type that was farmed, answers air at once, and levels its
// carries before buying new squads; and the playstyles (D128) do what they say. (It also plays by the player's limits and finishes a match
// against Hard: RuleBasedAiTests; which playstyle each mode gets: RuleBasedAiSkirmishTests.)

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class InsaneAiTests
    {
        private static UnitStats Unit(string name, UnitRole role, int squad, int cost, float health, float damage, float range,
            float splash = 0f, bool air = false, TargetFilter filter = TargetFilter.Both)
        {
            UnitStats unit = TestUnits.Unit(name: name, health: health, speed: 4f, radius: squad > 4 ? 0.5f : 1.2f, air: air, filter: filter,
                squadSize: squad, cost: cost, weapon: TestUnits.Weapon(damage: damage, range: range, interval: 0.5f, splash: splash));
            unit.Role = role;
            unit.Tier = 1;
            unit.BaseDamage = cost / (float)squad / 5f;
            return unit;
        }

        private static List<UnitStats> Roster() => new List<UnitStats>
        {
            Unit("Grunt", UnitRole.Chaff, 12, 100, 40f, 4f, 10f, filter: TargetFilter.Ground),
            Unit("Flamer", UnitRole.ChaffClear, 6, 150, 150f, 30f, 8f, splash: 3f, filter: TargetFilter.Ground),
            Unit("Rifle", UnitRole.SingleTargetDps, 1, 250, 400f, 60f, 20f),
            Unit("Flak", UnitRole.AntiAir, 1, 250, 400f, 40f, 20f),
            Unit("Gunship", UnitRole.Aerial, 2, 300, 400f, 20f, 12f, air: true),
        };

        private static Match NewMatch(IMatchAi enemy, int seed, bool towers = false)
        {
            List<UnitStats> roster = Roster();
            var factions = new List<FactionInfo> { TestMatch.Faction("Alpha", roster, "Standard"), TestMatch.Faction("Beta", roster, "Standard") };
            MatchRules rules = TestMatch.Rules();
            if (towers) rules.TowerBaseHealth = 150f;
            return new Match(rules, TestRules.Default(), factions, seed, enemy);
        }

        private static void Place(Match match, PlayerState side, string unitName, int squads, int firstId)
        {
            UnitStats type = side.Roster.Single(u => u.Name == unitName);
            side.Unlocked.Add(type);
            for (int i = 0; i < squads; i++)
            {
                Assert.IsTrue(DeploymentGrid.FindFreeSpot(side, type, false, match.CombatRules, out int c, out int r));
                side.Squads.Add(new PlacedSquad { Id = firstId + i, Type = type, Column = c, Row = r, RoundBought = 1 });
            }
        }

        /// <summary>Round 1 is fought between the armies given; then, with every unit unlocked and plenty of credits, Insane deploys for round 2.</summary>
        private static (Match match, RuleBasedAi ai) AfterBattle(string aiUnit, int aiSquads, string playerUnit, int playerSquads,
            int seed = 3, System.Action<Match> beforeDeploying = null, InsanePlaystyle style = InsanePlaystyle.Typical, bool towers = false, bool readsUpgrades = true, MatchupTable table = null, InsaneReads? reads = null)
        {
            var ai = new RuleBasedAi(AiDifficulty.Insane, seed, table) { Playstyle = style, ReadsUpgrades = readsUpgrades };
            if (reads.HasValue) ai.Reads = reads.Value;
            Match match = NewMatch(ai, seed, towers);
            match.ChooseDraft(0);
            match.Player.Squads.Clear();
            match.Enemy.Squads.Clear();
            Place(match, match.Player, playerUnit, playerSquads, 1000);
            Place(match, match.Enemy, aiUnit, aiSquads, 2000);
            match.StartBattle().Run();
            match.FinishBattle();
            Assert.AreEqual(MatchPhase.Result, match.Phase, "The match goes on to round 2.");
            foreach (UnitStats unit in match.Enemy.Roster) match.Enemy.Unlocked.Add(unit);
            match.Enemy.Credits = 5000;
            beforeDeploying?.Invoke(match);
            match.ContinueAfterResult();
            return (match, ai);
        }

        private static List<PlacedSquad> BoughtThisRound(Match match) => match.Enemy.Squads.Where(s => s.RoundBought == match.Round).ToList();

        [Test]
        public void StopsBuyingAUnitTypeThatWasFarmed()
        {
            var (match, ai) = AfterBattle("Grunt", 3, "Flamer", 3);
            UnitStats grunt = match.Enemy.Roster.Single(u => u.Name == "Grunt");
            Assert.Greater(ai.FarmedShare(grunt), 0.3f, ai.LastDecision);
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsNotEmpty(bought, ai.LastDecision);
            Assert.IsFalse(bought.Any(s => s.Type == grunt), $"No more Grunts into the Flamers. {ai.LastDecision}");
        }

        [Test]
        public void AnswersAirItCannotShoot_AtOnce()
        {
            var (match, ai) = AfterBattle("Grunt", 2, "Gunship", 3);
            Assert.Greater(ai.AirUncovered, 0.5f, ai.LastDecision);
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsNotEmpty(bought, ai.LastDecision);
            Assert.IsTrue(bought.All(s => s.Type.TargetFilter != TargetFilter.Ground), $"Every new squad can shoot the Gunships. {ai.LastDecision}");
        }

        [Test]
        public void LevelsItsCarry_BeforeBuyingNewSquads()
        {
            PlacedSquad veteran = null;
            var (match, ai) = AfterBattle("Rifle", 2, "Grunt", 1, beforeDeploying: m =>
            {
                veteran = m.Enemy.Squads.First(s => s.Type.Name == "Rifle");
                veteran.Xp = m.XpNeeded(veteran);
                // Enough for the level-up and one more squad, not for the level-up after a round's worth of squads.
                m.Enemy.Credits = m.LevelUpCost(veteran) + 300;
            });
            Assert.IsTrue(ai.Carries.Any(t => t.Name == "Rifle"), ai.LastDecision);
            Assert.AreEqual(2, veteran.Level, ai.LastDecision);
        }

        // ------------------------------------------------------------------ levels, techs and concentration (D145)

        [Test]
        public void StrengthEdge_IsNothingForAFreshSquad_AndGrowsWithLevelsTechsAndRange()
        {
            UnitStats rifle = Roster().Single(u => u.Name == "Rifle"); // Range 20.
            Assert.AreEqual(0f, RuleBasedAi.StrengthEdge(rifle, 1, null, null), 1e-5f);
            float five = RuleBasedAi.StrengthEdge(rifle, 5, null, null);
            Assert.AreEqual(2f * System.MathF.Log(5f / 3f), five, 1e-4f, "Level 5: five times the health and damage for three times the cost.");
            var longRange = new TechUpgrade { Name = "Long Range", RangeBonus = 5f };
            var rapid = new TechUpgrade { Name = "Rapid Loader", FireRateMultiplier = 1.25f };
            float teched = RuleBasedAi.StrengthEdge(rifle, 5, new[] { longRange, rapid }, null);
            Assert.AreEqual(five + 2f * System.MathF.Log(1.25f) + System.MathF.Log(1.25f), teched, 1e-4f, "Range counts double: it fires first.");
        }

        [Test]
        public void ATypeThatFillsTheArmy_IsNotPiledOn_AfterALostBattle()
        {
            // Three Gunships into eight Flaks: they lose. Its table rates the Gunship the Flak's counter (as the real one rates the Pelican against
            // the Wolverine); the Insane before D145 bought more of them; this one must not, however the table rates them.
            Assert.IsTrue(GunshipsBoughtAfterLosing(readsUpgrades: false, out _) > 0, "The test's premise: the Insane before D145 piles them on.");
            int gunships = GunshipsBoughtAfterLosing(readsUpgrades: true, out string decision);
            Assert.AreEqual(0, gunships, $"No more Gunships. {decision}");
        }

        private static int GunshipsBoughtAfterLosing(bool readsUpgrades, out string decision)
        {
            var table = new MatchupTable();
            string[] names = { "Grunt", "Flamer", "Rifle", "Flak", "Gunship" };
            foreach (string row in names) foreach (string column in names) if (row != column) table.Set(row, column, 0.5f);
            table.Set("Gunship", "Flak", 1f);
            table.Set("Flak", "Gunship", 0f);
            foreach (string other in new[] { "Grunt", "Flamer", "Rifle" }) { table.Set(other, "Flak", 0.4f); table.Set("Gunship", other, 0.7f); }
            var (match, ai) = AfterBattle("Gunship", 3, "Flak", 8, beforeDeploying: m => m.Enemy.Credits = 1500, readsUpgrades: readsUpgrades, table: table);
            decision = $"{ai.LastDecision} (farmed {ai.FarmedShare(match.Enemy.Roster.Single(u => u.Name == "Gunship")):0.00}, base damage taken {match.LastSummary?.BaseDamageByA}, dealt {match.LastSummary?.BaseDamageByB})";
            return BoughtThisRound(match).Count(s => s.Type.Name == "Gunship");
        }

        // ------------------------------------------------------------------ margins (D154)

        [Test]
        public void TheTableScore_TellsTwoSureWinsApart_ByTheirMargins()
        {
            UnitStats crusher = Unit("Crusher", UnitRole.Tank, 1, 300, 1000f, 50f, 10f), scraper = Unit("Scraper", UnitRole.Tank, 1, 300, 1000f, 50f, 10f);
            UnitStats target = Unit("Target", UnitRole.Chaff, 1, 300, 1000f, 50f, 10f);
            var table = new MatchupTable();
            table.Set("Crusher", "Target", 1f);
            table.Set("Scraper", "Target", 1f);
            Assert.AreEqual(1f, table.Score(scraper, target).Value, 1e-5f, "No margin measured: the rate alone.");
            table.SetMargin("Crusher", "Target", 0.9f);
            table.SetMargin("Scraper", "Target", 0.05f);
            Assert.AreEqual(table.WinRate(crusher, target), table.WinRate(scraper, target), "Both win every time...");
            Assert.AreEqual(0.975f, table.Score(crusher, target).Value, 1e-5f);
            Assert.AreEqual(0.7625f, table.Score(scraper, target).Value, 1e-5f, "...but one by its last body.");
            Assert.Greater(table.OverallScore(crusher), table.OverallScore(scraper));
        }

        [Test]
        public void InsaneWithMargins_BuysTheCounterThatWinsByMore()
        {
            // Rifles and Flamers both beat Grunts every time in the table; only the margin tells them apart, and Insane follows it.
            (int flamers, int rifles) = CountersBoughtAgainstGrunts(flamerMargin: 0.9f, rifleMargin: 0.05f, out string first);
            (int flamers2, int rifles2) = CountersBoughtAgainstGrunts(flamerMargin: 0.05f, rifleMargin: 0.9f, out string second);
            Assert.Greater(flamers * 150 - rifles * 250, flamers2 * 150 - rifles2 * 250, $"More of its credits go to the counter that wins by more. {first} / {second}");
            Assert.Greater(flamers * 150, rifles * 250, first);
            Assert.Greater(rifles2 * 250, flamers2 * 150, second);
        }

        private static (int flamers, int rifles) CountersBoughtAgainstGrunts(float flamerMargin, float rifleMargin, out string decision)
        {
            var table = new MatchupTable();
            string[] names = { "Grunt", "Flamer", "Rifle", "Flak", "Gunship" };
            foreach (string row in names) foreach (string column in names) if (row != column) { table.Set(row, column, 0.5f); table.SetMargin(row, column, 0f); }
            table.Set("Flamer", "Grunt", 1f); table.Set("Grunt", "Flamer", 0f);
            table.Set("Rifle", "Grunt", 1f); table.Set("Grunt", "Rifle", 0f);
            table.SetMargin("Flamer", "Grunt", flamerMargin); table.SetMargin("Grunt", "Flamer", -flamerMargin);
            table.SetMargin("Rifle", "Grunt", rifleMargin); table.SetMargin("Grunt", "Rifle", -rifleMargin);
            var (match, ai) = AfterBattle("Flak", 2, "Grunt", 6, beforeDeploying: m => m.Enemy.Credits = 1500, table: table, reads: InsaneReads.All | InsaneReads.Margins);
            decision = ai.LastDecision;
            List<PlacedSquad> bought = BoughtThisRound(match);
            return (bought.Count(s => s.Type.Name == "Flamer"), bought.Count(s => s.Type.Name == "Rifle"));
        }

        // ------------------------------------------------------------------ playstyles (D128)

        [Test]
        public void TowerRush_SendsEveryNewSquadUpOneLane_AndLevelsItsOtherTower()
        {
            var (match, ai) = AfterBattle("Rifle", 2, "Grunt", 2, style: InsanePlaystyle.TowerRush, towers: true);
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsNotEmpty(bought, ai.LastDecision);
            int half = match.CombatRules.ArenaWidthSquares / 2;
            foreach (PlacedSquad squad in bought)
            {
                DeploymentGrid.Footprint(squad.Type, squad.Rotated90, match.CombatRules, out int width, out _);
                Assert.AreEqual(ai.RushesLeft, squad.Column + width / 2 < half, $"{squad.Type.Name} at column {squad.Column}. {ai.LastDecision}");
            }
            foreach (PlacedTower tower in match.Enemy.Towers)
            {
                bool onLeft = tower.Column + tower.Squares / 2 < half;
                if (onLeft == ai.RushesLeft) Assert.AreEqual(1, tower.Level, "The tower on its own lane isn't levelled.");
                else Assert.Greater(tower.Level, 1, $"The tower it leaves alone is. {ai.LastDecision}");
            }
        }

        [Test]
        public void Defender_FormsItsLineInFrontOfItsTowers_AndLevelsThem()
        {
            var (match, ai) = AfterBattle("Rifle", 2, "Grunt", 2, style: InsanePlaystyle.Defender, towers: true);
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsNotEmpty(bought, ai.LastDecision);
            int towerRow = match.Enemy.Towers[0].Row;
            foreach (PlacedSquad squad in bought) Assert.GreaterOrEqual(squad.Row, towerRow - 6, $"{squad.Type.Name} at row {squad.Row}, not at the front edge. {ai.LastDecision}");
            Assert.IsTrue(match.Enemy.Towers.Any(t => t.Level > 1), ai.LastDecision);
        }

        [Test]
        public void Synergy_CommitsToAFrontAndABackUnit_AndBuysThem()
        {
            var (match, ai) = AfterBattle("Rifle", 2, "Grunt", 2, style: InsanePlaystyle.Synergy);
            Assert.AreEqual(2, ai.SynergyPair.Count, ai.LastDecision);
            Assert.IsTrue(ai.SynergyPair.Any(u => u.Role == UnitRole.Chaff || u.Role == UnitRole.Tank || u.Role == UnitRole.Flanker), "A front-line unit.");
            Assert.IsTrue(ai.SynergyPair.Any(u => u.Role == UnitRole.SingleTargetDps || u.Role == UnitRole.ChaffClear || u.Role == UnitRole.Artillery), "A back-line unit.");
            List<PlacedSquad> bought = BoughtThisRound(match);
            Assert.IsNotEmpty(bought, ai.LastDecision);
            Assert.IsTrue(bought.All(s => ai.SynergyPair.Contains(s.Type)), $"Only its pair. {ai.LastDecision}");
        }
    }
}
