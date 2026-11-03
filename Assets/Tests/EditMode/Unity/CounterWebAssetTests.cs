// CounterWebAssetTests.cs
// Verifies the counter web on the real slice roster assets (Milestone 4): for every unit, its best
// counter from the latest full counter-web run (Halobellum > Headless Runner > Run Counter Web, also
// listed in each unit's notes) still wins at least 90% of equal-cost battles against it (D23). This runs
// only one pair per unit, so it stays quick; the full run checks every pair. If a balance change moves a
// unit's best counter, run the console in Tools/Data/cw again, then Tools/Data/gen_counter_list.py, which rewrites
// this list, and Tools/Data/gen_matchup_asset.py. Also checks that the
// matchup table the Hard AI uses (written by the counter web) covers every pair of slice units.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Combat (CounterWeb), Halobellum.EditorTools (HeadlessRunnerTool), Halobellum.Units, NUnit.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.EditorTools;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

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
            ("Wolverine", "Warthog"),
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
            ("Infection Forms", "Flamethrower Trooper"),
            ("Carrier Form", "Scorpion"),
            ("Combat Form", "Marine"),
            ("Stalker Form", "Flamethrower Trooper"),
            ("Pure Form Ranged", "Hornet"),
            ("Swarm Form", "Marine"),
            ("Pure Form Tank", "Gauss Warthog"),
            ("Seeder", "Marine"),
            ("Infected Pelican", "Vampire"),
            ("Infected Phantom", "Swarm Form"),
            ("Flood Abomination", "Zealot"),
            ("Spartan Sniper", "Marine"),
            ("Spartan Heavy Weapons", "Elite"),
            ("Spartan Grenadier", "Scorpion"),
            ("Spartan CQB", "Flamethrower Trooper"),
            ("Spartan Fireteam", "Marine"),
            ("Gremlin", "Flamethrower Trooper"),
            ("Cobra", "Marine"),
            ("Rhino", "Hornet"),
            ("Grizzly", "Gauss Warthog"),
            ("Sparrowhawk", "Wolverine"),
            ("Shortsword", "Wolverine"),
            ("Vulture", "Wolverine"),
            ("Condor Gunship", "Wolverine"),
            ("Chopper", "Flamethrower Trooper"),
            ("Brute Grenadier", "Scorpion"),
            ("Marauder", "Gauss Warthog"),
            ("Reaver", "Marine"),
            ("Skitterer", "Flamethrower Trooper"),
            ("Goliath", "Gauss Warthog"),
            ("Hunter Captain", "Marine"),
            ("Cannon Fodder", "Flamethrower Trooper"),
            ("Heavy Grunts", "Marine"),
            ("Grunt Goblin", "Gauss Warthog"),
            ("Infested Marine", "Flamethrower Trooper"),
            ("Infested Warthog", "Flamethrower Trooper"),
            ("Infested Scorpion", "Gauss Warthog"),
            ("Infested Hornet", "Wolverine"),
            ("Juggernaut", "Scorpion"),
            ("Infested Grunt", "Flamethrower Trooper"),
            ("Infested Ghost", "Flamethrower Trooper"),
            ("Infested Elite", "Marine"),
            ("Infested Wraith", "Hornet"),
            ("Infested Banshee", "Wolverine"),
            ("Spawner", "Jackal (Plasma Rifle)"),
        };

        // With the sub-factions there are 69 pairs: they run side by side, and may take longer than a test's usual 3 minutes on a busy machine.
        [Test, Timeout(600000)]
        public void EveryUnit_HasACounterThatWinsAtLeast90Percent()
        {
            Dictionary<string, UnitStats> units = HeadlessRunnerTool.LoadSliceUnits()
                .ToDictionary(u => u.unitName, HeadlessRunnerTool.ValidatedStats);
            Assert.AreEqual(69, units.Count);
            CollectionAssert.AreEquivalent(units.Keys, BestCounters.Select(p => p.unit), "One line per slice unit.");

            CombatRules rules = HeadlessRunnerTool.LoadRules();
            var settings = new CounterWebSettings
            {
                Budget = HeadlessRunnerTool.DefaultCounterWebBudget,
                Runner = HeadlessRunnerTool.DefaultSettings(),
            };
            foreach (var (unitName, counterName) in BestCounters)
            {
                UnitStats unit = units[unitName], counter = units[counterName];
                Assert.IsTrue(CounterWeb.IsCounter(counter, unit), $"{counterName} ({counter.Role}) is not a Section 7 counter of {unitName} ({unit.Role}).");
            }
            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            System.Threading.Tasks.Parallel.ForEach(BestCounters, pair =>
            {
                UnitStats unit = units[pair.unit], counter = units[pair.counter];
                CounterMatchup matchup = CounterWeb.RunPair(rules, counter, unit, settings);
                if (matchup.WinRateA < settings.Threshold - 1e-4f)
                    failures.Add($"{pair.counter} beats {pair.unit} only {CounterWeb.Percent(matchup.WinRateA)} ({matchup.CostA} vs {matchup.CostB} credits)");
            });
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void MatchupTable_ForTheHardAi_CoversEveryPairOfSliceUnits()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MatchupTableDefinition>(MatchupTableDefinition.DefaultAssetPath);
            Assert.IsNotNull(asset, "Missing " + MatchupTableDefinition.DefaultAssetPath + " (run the counter web).");
            MatchupTable table = asset.ToTable();
            List<UnitStats> units = HeadlessRunnerTool.LoadSliceUnits().Select(u => u.ToStats()).ToList();
            foreach (UnitStats a in units)
            {
                foreach (UnitStats b in units)
                {
                    if (a != b) Assert.IsTrue(table.WinRate(a, b).HasValue, $"No measured win rate for {a.Name} vs {b.Name}.");
                }
            }
        }
    }
}
