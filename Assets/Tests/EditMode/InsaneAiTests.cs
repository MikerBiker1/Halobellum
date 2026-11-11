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
            int seed = 3, System.Action<Match> beforeDeploying = null, InsanePlaystyle style = InsanePlaystyle.Typical, bool towers = false)
        {
            var ai = new RuleBasedAi(AiDifficulty.Insane, seed) { Playstyle = style };
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
