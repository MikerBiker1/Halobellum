// BattleStatsTests.cs
// Tests the Battle stats (D69): what a body takes is what the other side dealt; what armor stops is counted, and never
// past what the body had; a shot a defense arc blocks counts whole as stopped; the match keeps a line for each squad of
// both sides, where it stood, and adds the numbers up when the battle ends.
// Depends on: Halobellum.Combat, Halobellum.Economy, Halobellum.Core, NUnit, TestRules, TestUnits, TestMatch.

using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class BattleStatsTests
    {
        private static BattleSimulation Duel(ArmorType armor, float targetHealth, DefenseArcStats arc = null, bool blockable = false)
        {
            var sim = new BattleSimulation(TestRules.Default(), 1);
            UnitStats shooter = TestUnits.Unit("Shooter", health: 10000f, speed: 0f, weapon: TestUnits.Weapon(damage: 10f, range: 30f, interval: 0.5f, projectileSpeed: blockable ? 60f : 0f, blockable: blockable));
            UnitStats target = TestUnits.Unit("Target", health: targetHealth, armor: armor, speed: 0f, weapon: TestUnits.Weapon(damage: 0.001f, range: 1f));
            target.DefenseArc = arc;
            sim.AddSquad(shooter, Team.A, new Vec2(0f, -10f));
            sim.AddSquad(target, Team.B, new Vec2(0f, 10f));
            return sim;
        }

        [Test]
        public void ArmorThatHalvesAHit_IsCountedAsStopped_AndWhatGotThroughAsTaken()
        {
            BattleSimulation sim = Duel(ArmorType.Heavy, 5000f); // Ballistic against heavy armor: a quarter gets through.
            for (int i = 0; i < 200; i++) sim.Step();
            SimUnit shooter = sim.Units[0], target = sim.Units[1];
            Assert.Greater(shooter.DamageDealt, 0f);
            Assert.AreEqual(shooter.DamageDealt, target.DamageTaken, 0.01f, "What a body takes is what the other side dealt.");
            Assert.AreEqual(target.DamageTaken * 3f, target.DamagePrevented, 0.05f, "Three quarters of each hit were stopped.");
        }

        [Test]
        public void WhatIsStopped_NeverGoesPastWhatTheBodyHadLeft()
        {
            BattleSimulation sim = Duel(ArmorType.Unarmored, 25f); // Three hits of 10: the last one only finds 5.
            sim.Run();
            SimUnit target = sim.Units[1];
            Assert.IsFalse(target.IsAlive);
            Assert.AreEqual(25f, target.DamageTaken, 0.01f);
            Assert.AreEqual(0f, target.DamagePrevented, 0.01f, "Nothing stopped a hit to an unarmored body, and the overkill isn't stopped damage.");
        }

        [Test]
        public void WhatARegeneratedShieldSoaks_CountsAsTaken()
        {
            CombatRules rules = TestRules.Default();
            rules.ShieldRegenDelaySeconds = 1f;
            var sim = new BattleSimulation(rules, 1);
            // Every 5 s a hit of 40: 30 to the shield and 10 to health. The shield is back to 30 well before the next one.
            UnitStats shooter = TestUnits.Unit("Shooter", health: 10000f, speed: 0f, weapon: TestUnits.Weapon(damage: 40f, range: 30f, interval: 5f, type: DamageType.Explosive));
            UnitStats target = TestUnits.Unit("Target", health: 50f, shield: 30f, shieldRegen: 1000f, speed: 0f, weapon: TestUnits.Weapon(damage: 0.001f, range: 1f));
            sim.AddSquad(shooter, Team.A, new Vec2(0f, -10f));
            sim.AddSquad(target, Team.B, new Vec2(0f, 10f));
            sim.Run();
            SimUnit dead = sim.Units[1];
            Assert.IsFalse(dead.IsAlive);
            Assert.AreEqual(200f, dead.DamageTaken, 0.01f, "Five hits of 40: its 80 at the start, and 120 more its shield came back with.");
        }

        [Test]
        public void AShotAnArcBlocks_CountsWholeAsStopped()
        {
            var arc = new DefenseArcStats { ArcDegrees = 180f, FrontDamageMultiplier = 1f, RearDamageMultiplier = 1f, BlockChance = 1f, BlockedDamageTypes = new[] { DamageType.Ballistic } };
            BattleSimulation sim = Duel(ArmorType.Unarmored, 5000f, arc, blockable: true);
            for (int i = 0; i < 200; i++) sim.Step();
            SimUnit target = sim.Units[1];
            Assert.AreEqual(0f, target.DamageTaken, 0.01f, "Every shot came into the arc and was blocked.");
            Assert.Greater(target.DamagePrevented, 30f);
            Assert.AreEqual(0f, target.DamagePrevented % 10f, 0.01f, "Each blocked shot counts its whole 10.");
        }

        [Test]
        public void TheMap_TakesInTheFlankZones_SoASquadThereIsOnIt()
        {
            CombatRules rules = TestRules.Default();
            rules.ArenaWidthSquares = 80;
            rules.DeploymentDepthSquares = 40;
            rules.NoMansLandSquares = 2;
            rules.SquareSize = 2f;
            var flier = new UnitStats
            {
                Name = "Flier", Tier = 2, Cost = 300, SquadSize = 1, MaxHealth = 1000f, MoveSpeed = 5f, CollisionRadius = 1f, IsAir = true, CanFlank = true, TargetFilter = TargetFilter.Both,
                Weapons = new[] { TestUnits.Weapon(damage: 1f, range: 12f) },
            };
            var player = new PlayerState(Team.A);
            player.Squads.Add(new PlacedSquad { Id = 1, Type = flier, Column = -6, Row = -20 }); // The far edge of the left flank zone.
            var enemy = new PlayerState(Team.B);
            BattleSimulation battle = DeploymentGrid.CreateBattle(rules, player, enemy, 1);
            BattleStats stats = BattleStats.AtStart(battle, 1, player, enemy, rules);

            Assert.AreEqual(rules.ArenaWidth + 2f * (rules.FlankGapColumns + rules.FlankZoneColumns) * rules.SquareSize, stats.FieldWidth, 1e-3f);
            BattleStatsLine line = stats.Lines.Single();
            Assert.Less(line.Start.X, -rules.ArenaWidth * 0.5f, "It stands beyond the field's edge.");
            Assert.GreaterOrEqual(line.Start.X, -stats.FieldWidth * 0.5f, "But on the map.");
            Assert.Greater(line.Start.Y, 0f, "Beside the enemy's area.");
        }

        [Test]
        public void TheMatch_KeepsEachBattlesLines_ForBothSides_AndAddsThemUp()
        {
            Match match = TestMatch.Started(out UnitStats infantry, out _, out _, out _);
            Assert.IsTrue(match.Buy(Team.A, infantry, 0, 5, false).Ok);
            BattleSimulation battle = match.StartBattle();
            BattleStats stats = match.BattleHistory.Single();
            Assert.IsFalse(stats.Finished);
            Assert.AreEqual(1, stats.Round);
            Assert.AreEqual(match.Player.Squads.Count, stats.Lines.Count(l => l.Team == Team.A && l.Kind == BattleStatsKind.Squad));
            Assert.AreEqual(match.Enemy.Squads.Count, stats.Lines.Count(l => l.Team == Team.B && l.Kind == BattleStatsKind.Squad));
            foreach (BattleStatsLine line in stats.Lines)
            {
                Assert.LessOrEqual(System.Math.Abs(line.Start.X), stats.FieldWidth * 0.5f, line.Name);
                Assert.LessOrEqual(System.Math.Abs(line.Start.Y), stats.FieldLength * 0.5f, line.Name);
                if (line.Kind == BattleStatsKind.Squad) Assert.AreEqual(line.Team == Team.A, line.Start.Y < 0f, "Team A stands on its own half.");
            }

            BattleResult result = battle.Run();
            match.FinishBattle();
            Assert.IsTrue(stats.Finished);
            foreach (Team team in new[] { Team.A, Team.B })
            {
                Team other = team == Team.A ? Team.B : Team.A;
                float dealt = stats.Lines.Where(l => l.Team == team).Sum(l => l.DamageDealt);
                Assert.AreEqual(result.For(team).DamageDealt, stats.Lines.Where(l => l.Team == team && l.Kind != BattleStatsKind.Tower).Sum(l => l.DamageDealt), 0.5f, $"Team {team}: the lines add up to what the side dealt (the result leaves towers out).");
                Assert.AreEqual(dealt, stats.Lines.Where(l => l.Team == other).Sum(l => l.DamageTaken), 0.5f, "With no spells, one side takes what the other dealt.");
                Assert.AreEqual(result.For(team).Survivors, stats.Lines.Where(l => l.Team == team && l.Kind == BattleStatsKind.Squad).Sum(l => l.Survivors));
            }

            var sorted = stats.Side(Team.A, byReceived: false);
            for (int i = 1; i < sorted.Count; i++) Assert.GreaterOrEqual(sorted[i - 1].DamageDealt, sorted[i].DamageDealt);
            sorted = stats.Side(Team.A, byReceived: true);
            for (int i = 1; i < sorted.Count; i++) Assert.GreaterOrEqual(sorted[i - 1].DamageReceived, sorted[i].DamageReceived);
        }
    }
}
