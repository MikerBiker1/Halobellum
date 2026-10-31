// HeadlessBattleRunnerTests.cs
// Tests the headless battle runner: army layout stays in each team's half without overlap, win
// counts add up, the same seed gives the same summary, round robins cover every pair, budget armies
// buy the right number of squads, and the CSV has one row per matchup.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class HeadlessBattleRunnerTests
    {
        private CombatRules _rules;
        private RunnerSettings _settings;
        private UnitStats _rifle;
        private UnitStats _tank;
        private UnitStats _buggy;

        [SetUp]
        public void SetUp()
        {
            _rules = TestRules.Default();
            _settings = new RunnerSettings { Battles = 4, BaseSeed = 100, PositionJitter = 0.3f, FrontLineDistance = 20f, SquadGap = 3f };
            _rifle = TestUnits.Unit(name: "Rifle", health: 60f, speed: 4f, radius: 0.5f, squadSize: 12, cost: 100,
                weapon: TestUnits.Weapon(damage: 6f, range: 12f, interval: 0.5f));
            _tank = TestUnits.Unit(name: "Tank", health: 1200f, armor: ArmorType.Heavy, speed: 3f, radius: 2f, squadSize: 1, cost: 300,
                weapon: TestUnits.Weapon(damage: 120f, range: 16f, interval: 2.5f, projectileSpeed: 30f, splash: 2f, type: DamageType.Explosive));
            _buggy = TestUnits.Unit(name: "Buggy", health: 300f, armor: ArmorType.Light, speed: 9f, radius: 1.2f, squadSize: 2, cost: 200,
                weapon: TestUnits.Weapon(damage: 8f, range: 10f, interval: 0.2f));
        }

        [Test]
        public void ForBudget_BuysAsManySquadsAsTheBudgetAllows()
        {
            Assert.AreEqual(6, ArmySetup.ForBudget(_rifle, 600).Squads[0].SquadCount);
            Assert.AreEqual(2, ArmySetup.ForBudget(_tank, 600).Squads[0].SquadCount);
            Assert.AreEqual(1, ArmySetup.ForBudget(_tank, 100).Squads[0].SquadCount, "Always at least one squad.");
            Assert.AreEqual(600, ArmySetup.ForBudget(_buggy, 700).TotalCost);
        }

        [Test]
        public void Layout_KeepsEachArmyInItsOwnHalf_WithoutOverlap()
        {
            var sim = new BattleSimulation(_rules, 1);
            var noJitter = new RunnerSettings { PositionJitter = 0f, FrontLineDistance = 20f, SquadGap = 3f };
            var army = new ArmySetup("Mixed").Add(_rifle, 8).Add(_tank, 3).Add(_buggy, 2);
            ArmyLayout.Place(sim, _rules, army, Team.A, noJitter, new SimRandom(1));
            int teamACount = sim.Units.Count;
            ArmyLayout.Place(sim, _rules, army, Team.B, noJitter, new SimRandom(1));

            Assert.AreEqual(8 * 12 + 3 + 2 * 2, teamACount);
            foreach (SimUnit unit in sim.Units)
            {
                if (unit.Team == Team.A) Assert.Less(unit.Position.Y, -19f, unit.ToString());
                else Assert.Greater(unit.Position.Y, 19f, unit.ToString());
                Assert.LessOrEqual(System.Math.Abs(unit.Position.X), _rules.ArenaWidth / 2f, unit.ToString());
                Assert.LessOrEqual(System.Math.Abs(unit.Position.Y), _rules.ArenaLength / 2f, unit.ToString());
            }
            for (int i = 0; i < sim.Units.Count; i++)
            for (int j = i + 1; j < sim.Units.Count; j++)
            {
                float gap = Vec2.Distance(sim.Units[i].Position, sim.Units[j].Position) - sim.Units[i].Radius - sim.Units[j].Radius;
                Assert.GreaterOrEqual(gap, -1e-3f, $"{sim.Units[i]} overlaps {sim.Units[j]}");
            }
        }

        [Test]
        public void Matchup_CountsAddUp()
        {
            var summary = HeadlessBattleRunner.RunMatchup(_rules, ArmySetup.ForBudget(_rifle, 600), ArmySetup.ForBudget(_buggy, 600), _settings);
            Assert.AreEqual(4, summary.Battles);
            Assert.AreEqual(4, summary.Results.Count);
            Assert.AreEqual(4, summary.TeamAWins + summary.TeamBWins + summary.Draws);
            Assert.AreEqual(72, summary.DeployedA);
            Assert.AreEqual(6, summary.DeployedB);
            Assert.AreEqual(100, summary.Results[0].Seed);
            Assert.AreEqual(103, summary.Results[3].Seed);
        }

        [Test]
        public void Matchup_SameSeedsGiveTheSameSummary()
        {
            var first = HeadlessBattleRunner.RunMatchup(_rules, ArmySetup.ForBudget(_rifle, 600), ArmySetup.ForBudget(_tank, 600), _settings);
            var second = HeadlessBattleRunner.RunMatchup(_rules, ArmySetup.ForBudget(_rifle, 600), ArmySetup.ForBudget(_tank, 600), _settings);
            Assert.AreEqual(first.TeamAWins, second.TeamAWins);
            Assert.AreEqual(first.TeamBWins, second.TeamBWins);
            Assert.AreEqual(first.AverageDurationSeconds, second.AverageDurationSeconds);
            Assert.AreEqual(first.AverageDamageDealtA, second.AverageDamageDealtA);
            Assert.AreEqual(HeadlessBattleRunner.ToCsv(new[] { first }), HeadlessBattleRunner.ToCsv(new[] { second }));
        }

        [Test]
        public void RoundRobin_RunsEveryPairOnce()
        {
            var armies = new List<ArmySetup>
            {
                ArmySetup.ForBudget(_rifle, 600), ArmySetup.ForBudget(_buggy, 600), ArmySetup.ForBudget(_tank, 600),
            };
            _settings.Battles = 2;
            var summaries = HeadlessBattleRunner.RunRoundRobin(_rules, armies, _settings);
            Assert.AreEqual(3, summaries.Count);
            Assert.AreEqual("Rifle", summaries[0].ArmyA); Assert.AreEqual("Buggy", summaries[0].ArmyB);
            Assert.AreEqual("Rifle", summaries[1].ArmyA); Assert.AreEqual("Tank", summaries[1].ArmyB);
            Assert.AreEqual("Buggy", summaries[2].ArmyA); Assert.AreEqual("Tank", summaries[2].ArmyB);
            foreach (var s in summaries) Assert.Greater(s.AverageDurationSeconds, 0f);
        }

        [Test]
        public void Csv_HasHeaderAndOneRowPerMatchup()
        {
            _settings.Battles = 1;
            var summaries = HeadlessBattleRunner.RunRoundRobin(_rules,
                new List<ArmySetup> { ArmySetup.ForBudget(_rifle, 600), ArmySetup.ForBudget(_buggy, 600), ArmySetup.ForBudget(_tank, 600) }, _settings);
            string[] lines = HeadlessBattleRunner.ToCsv(summaries).Trim().Split('\n');
            Assert.AreEqual(4, lines.Length);
            Assert.AreEqual(HeadlessBattleRunner.CsvHeader, lines[0].Trim());
            int columns = HeadlessBattleRunner.CsvHeader.Split(',').Length;
            for (int i = 1; i < lines.Length; i++) Assert.AreEqual(columns, lines[i].Trim().Split(',').Length, lines[i]);
        }

        [Test]
        public void Report_NamesEachMatchup()
        {
            _settings.Battles = 1;
            var summary = HeadlessBattleRunner.RunMatchup(_rules, ArmySetup.ForBudget(_rifle, 600), ArmySetup.ForBudget(_tank, 600), _settings);
            string report = HeadlessBattleRunner.FormatReport(new[] { summary });
            StringAssert.Contains("Rifle (600 cr, 72 bodies)  vs  Tank (600 cr, 2 bodies)", report);
            StringAssert.Contains("Wins:", report);
        }
    }
}
