// CounterWebAssetTests.cs
// Verifies the counter web on the real slice roster assets (Milestone 4): for every unit, its best
// counter from the latest full counter-web run (Halobellum > Headless Runner > Run Counter Web, also
// listed in each unit's notes) still wins more than 70% of equal-cost battles against it. This runs
// only those 24 pairs, so it stays quick; the full run checks every pair. If a balance change moves a
// unit's best counter, run the full counter web again and update this list.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Combat (CounterWeb), Halobellum.EditorTools (HeadlessRunnerTool), Halobellum.Units, NUnit.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.EditorTools;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class CounterWebAssetTests
    {
        // Unit -> its best counter in the latest full run (10 battles per pair, about 1800 credits per army).
        private static readonly (string unit, string counter)[] BestCounters =
        {
            ("Marine", "Fuel Rod Grunt"),
            ("Flamethrower Trooper", "Scorpion"),
            ("Warthog", "Flamethrower Trooper"),
            ("Gauss Warthog", "Marine"),
            ("Scorpion", "Gauss Warthog"),
            ("Wolverine", "Marine"),
            ("Hornet", "Wolverine"),
            ("Archer Missile Pod", "Hornet"),
            ("Pelican", "Wolverine"),
            ("Spartan", "Marine"),
            ("Mantis", "Gauss Warthog"),
            ("Elephant", "Spartan"),
            ("Grunt", "Flamethrower Trooper"),
            ("Fuel Rod Grunt", "Scorpion"),
            ("Jackal (Plasma Rifle)", "Marine"),
            ("Jackal (Shield)", "Jackal (Plasma Rifle)"),
            ("Ghost", "Flamethrower Trooper"),
            ("Elite", "Marine"),
            ("Wraith", "Hornet"),
            ("Vampire", "Marine"),
            ("Banshee", "Wolverine"),
            ("Hunter Pair", "Spartan"),
            ("Zealot", "Marine"),
            ("Scarab", "Spartan"),
        };

        [Test]
        public void EveryUnit_HasACounterThatWinsOver70Percent()
        {
            Dictionary<string, UnitStats> units = HeadlessRunnerTool.LoadSliceUnits()
                .ToDictionary(u => u.unitName, HeadlessRunnerTool.ValidatedStats);
            Assert.AreEqual(24, units.Count);
            CollectionAssert.AreEquivalent(units.Keys, BestCounters.Select(p => p.unit), "One line per slice unit.");

            CombatRules rules = HeadlessRunnerTool.LoadRules();
            var settings = new CounterWebSettings
            {
                Budget = HeadlessRunnerTool.DefaultCounterWebBudget,
                Runner = HeadlessRunnerTool.DefaultSettings(),
            };
            var failures = new List<string>();
            foreach (var (unitName, counterName) in BestCounters)
            {
                UnitStats unit = units[unitName], counter = units[counterName];
                Assert.IsTrue(CounterWeb.IsCounter(counter, unit), $"{counterName} ({counter.Role}) is not a Section 7 counter of {unitName} ({unit.Role}).");
                CounterMatchup matchup = CounterWeb.RunPair(rules, counter, unit, settings);
                if (matchup.WinRateA <= settings.Threshold)
                    failures.Add($"{counterName} beats {unitName} only {CounterWeb.Percent(matchup.WinRateA)} ({matchup.CostA} vs {matchup.CostB} credits)");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
