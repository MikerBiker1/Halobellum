// CounterWebTests.cs
// Tests the counter web checker (Milestone 4): the role counters from the Bible's Section 7 table,
// equal-cost army sizes, side swapping, and the pass rule (best counter wins MORE than 70%).
// The real rosters are checked against it in Unity/CounterWebAssetTests.
// Depends on: Halobellum.Combat (CounterWeb), Halobellum.Core, NUnit, TestUnits, TestRules.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class CounterWebTests
    {
        private static UnitStats Role(UnitRole role, bool air = false, int cost = 100)
        {
            UnitStats unit = TestUnits.Unit(name: role + (air ? " (air)" : ""), air: air, cost: cost);
            unit.Role = role;
            return unit;
        }

        [TestCase(UnitRole.ChaffClear, UnitRole.Chaff)]
        [TestCase(UnitRole.Tank, UnitRole.ChaffClear)]
        [TestCase(UnitRole.Flanker, UnitRole.ChaffClear)]
        [TestCase(UnitRole.Chaff, UnitRole.SingleTargetDps)]
        [TestCase(UnitRole.Flanker, UnitRole.SingleTargetDps)]
        [TestCase(UnitRole.SingleTargetDps, UnitRole.Tank)]
        [TestCase(UnitRole.ChaffClear, UnitRole.Flanker)]
        [TestCase(UnitRole.Tank, UnitRole.Flanker)]
        [TestCase(UnitRole.Flanker, UnitRole.Artillery)]
        [TestCase(UnitRole.Aerial, UnitRole.Artillery)]
        [TestCase(UnitRole.AntiAir, UnitRole.Aerial)]
        [TestCase(UnitRole.SingleTargetDps, UnitRole.Titan)]
        [TestCase(UnitRole.Chaff, UnitRole.Titan)]
        public void RoleCounters_FollowTheBiblesTable(UnitRole counter, UnitRole target)
        {
            Assert.IsTrue(CounterWeb.RoleCounters(counter, target));
        }

        [TestCase(UnitRole.Chaff, UnitRole.Chaff)]
        [TestCase(UnitRole.Tank, UnitRole.Chaff)]
        [TestCase(UnitRole.Chaff, UnitRole.Tank)]
        [TestCase(UnitRole.Aerial, UnitRole.Aerial)]
        [TestCase(UnitRole.Titan, UnitRole.Titan)]
        public void RoleCounters_OnlyTheListedRoles(UnitRole counter, UnitRole target)
        {
            Assert.IsFalse(CounterWeb.RoleCounters(counter, target));
        }

        [Test]
        public void AntiAir_IsCounteredByGroundUnitsOnly()
        {
            UnitStats antiAir = Role(UnitRole.AntiAir);
            Assert.IsTrue(CounterWeb.IsCounter(Role(UnitRole.Chaff), antiAir));
            Assert.IsTrue(CounterWeb.IsCounter(Role(UnitRole.Titan), antiAir));
            Assert.IsFalse(CounterWeb.IsCounter(Role(UnitRole.Aerial, air: true), antiAir), "Air units are not ground units.");
            Assert.IsFalse(CounterWeb.IsCounter(Role(UnitRole.AntiAir), antiAir), "Anti-air does not counter itself.");
        }

        [TestCase(100, 100)]
        [TestCase(100, 800)]
        [TestCase(450, 500)]
        [TestCase(150, 250)]
        [TestCase(300, 450)]
        public void EqualCostCounts_KeepsBothArmiesWithin10PercentOfEachOther_NearTheBudget(int costA, int costB)
        {
            var settings = new CounterWebSettings();
            CounterWeb.EqualCostCounts(costA, costB, settings, out int squadsA, out int squadsB);
            int totalA = squadsA * costA, totalB = squadsB * costB;
            Assert.LessOrEqual(Math.Abs(totalA - totalB) / (float)Math.Max(totalA, totalB), 0.1f, $"{totalA} vs {totalB}");
            foreach (int total in new[] { totalA, totalB })
            {
                Assert.GreaterOrEqual(total, settings.Budget * settings.MinBudgetShare);
                Assert.LessOrEqual(total, settings.Budget * settings.MaxBudgetShare);
            }
        }

        private static CounterWebSettings Quick(int battles) => new CounterWebSettings
        {
            Budget = 300,
            Runner = new RunnerSettings { Battles = battles, BaseSeed = 5, PositionJitter = 0.3f, FrontLineDistance = 10f, SquadGap = 2f },
        };

        [Test]
        public void RunPair_CountsEveryBattle_AndTheStrongerUnitWins_OnBothSides()
        {
            UnitStats strong = TestUnits.Unit(name: "Strong", health: 400f, weapon: TestUnits.Weapon(damage: 20f, range: 10f, interval: 0.5f), cost: 100);
            UnitStats weak = TestUnits.Unit(name: "Weak", health: 100f, weapon: TestUnits.Weapon(damage: 5f, range: 10f), cost: 100);
            CounterMatchup matchup = CounterWeb.RunPair(TestRules.Default(), strong, weak, Quick(4));

            Assert.AreEqual(4, matchup.Battles);
            Assert.AreEqual(matchup.Battles, matchup.WinsA + matchup.WinsB + matchup.Draws);
            Assert.AreEqual(4, matchup.WinsA, "Wins as Team A and, with the sides swapped, as Team B.");
            Assert.AreEqual(matchup.CostA, matchup.CostB);
        }

        [Test]
        public void Report_PassesOnlyWhenTheBestCounterWinsMoreThanTheThreshold()
        {
            UnitStats chaff = Role(UnitRole.Chaff), clear = Role(UnitRole.ChaffClear), tank = Role(UnitRole.Tank);
            var matchups = new List<CounterMatchup>
            {
                new CounterMatchup { A = clear, B = chaff, Battles = 10, WinsA = 8, WinsB = 2 },  // Chaff clear beats chaff 80%.
                new CounterMatchup { A = tank, B = clear, Battles = 10, WinsA = 7, WinsB = 3 },   // Tank beats chaff clear exactly 70%.
                new CounterMatchup { A = tank, B = chaff, Battles = 10, WinsA = 5, WinsB = 5 },
            };
            CounterWebReport report = CounterWeb.BuildReport(new[] { chaff, clear, tank }, matchups, 0.7f);

            Assert.IsTrue(report.Lines[0].Passes, "Chaff: countered 80% by chaff clear.");
            Assert.AreSame(clear, report.Lines[0].BestCounter);
            Assert.IsFalse(report.Lines[1].Passes, "Chaff clear: 70% is not MORE than 70%.");
            Assert.IsFalse(report.Lines[2].Passes, "Tank: no single-target DPS unit in this roster.");
            Assert.IsFalse(report.AllPass);
            Assert.AreEqual(0.2f, report.WinRate(chaff, clear), 1e-5f);
            StringAssert.Contains("1 of 3 units", CounterWeb.FormatReport(report));
            StringAssert.Contains("0.80", CounterWeb.MatrixCsv(report));
        }
    }
}
