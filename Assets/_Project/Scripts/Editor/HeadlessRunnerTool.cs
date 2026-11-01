// HeadlessRunnerTool.cs
// Unity editor entry points for the headless battle runner (Bible Section 4, rule 4).
//   Menu: Halobellum > Headless Runner > Run Placeholder Round Robin
//     Runs every placeholder unit (Data/Units/Placeholder) against every other with equal budgets,
//     prints the report to the Console and writes a CSV to <project>/BalanceReports/.
//   Menu: Halobellum > Headless Runner > Run Counter Web
//     Runs every slice unit (Data/Units/UNSC and Covenant) against every other at equal cost and
//     checks that each unit has a counter winning over 70% (CounterWeb). Report to the Console,
//     win-rate matrix CSV to <project>/BalanceReports/.
//   Menu: Halobellum > Headless Runner > Open Runner Window   (pick any two armies)
//   Command line, with the Unity editor closed:
//     Unity.exe -batchmode -nographics -projectPath <project> -logFile <log>
//       -executeMethod Halobellum.EditorTools.HeadlessRunnerTool.RunPlaceholderRoundRobinBatch
//       [-battles 10] [-seed 1000] [-budget 600]
//     The report is written to the log file and the CSV to BalanceReports/.
//     Counter web: -executeMethod Halobellum.EditorTools.HeadlessRunnerTool.RunCounterWebBatch
//       [-battles 10] [-seed 1000] [-budget 1800]   (exit code 1 if a unit has no counter)
// Runner options below are tool defaults, not game rules. Game rules come from Data/CombatRules.asset.
// Depends on: Halobellum.Combat (HeadlessBattleRunner), Halobellum.Units (UnitDefinition,
//             CombatRulesDefinition), UnityEditor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Units;
using UnityEditor;
using UnityEngine;

namespace Halobellum.EditorTools
{
    public static class HeadlessRunnerTool
    {
        public const string PlaceholderFolder = "Assets/_Project/Data/Units/Placeholder";
        /// <summary>The slice rosters (Bible Section 8), UNSC first.</summary>
        public static readonly string[] SliceFolders = { "Assets/_Project/Data/Units/UNSC", "Assets/_Project/Data/Units/Covenant" };
        public const int DefaultCounterWebBudget = 1800;

        // Default runner options (tool settings, not balance values).
        public const int DefaultBattles = 10;
        public const int DefaultSeed = 1000;
        public const int DefaultBudget = 600;
        public const float DefaultPositionJitter = 0.3f;
        public const float DefaultFrontLineDistance = 20f;
        public const float DefaultSquadGap = 3f;

        public static RunnerSettings DefaultSettings(int battles = DefaultBattles, int seed = DefaultSeed)
        {
            return new RunnerSettings
            {
                Battles = battles,
                BaseSeed = seed,
                PositionJitter = DefaultPositionJitter,
                FrontLineDistance = DefaultFrontLineDistance,
                SquadGap = DefaultSquadGap,
            };
        }

        [MenuItem("Halobellum/Headless Runner/Run Placeholder Round Robin")]
        public static void RunPlaceholderRoundRobinMenu()
        {
            try
            {
                RunPlaceholderRoundRobin(DefaultBattles, DefaultSeed, DefaultBudget, out string report, out string csvPath);
                Debug.Log(report + "\nCSV written to " + csvPath);
            }
            catch (Exception e)
            {
                Debug.LogError("Headless runner failed: " + e.Message);
                throw;
            }
        }

        [MenuItem("Halobellum/Headless Runner/Run Counter Web")]
        public static void RunCounterWebMenu()
        {
            try
            {
                CounterWebReport result = RunCounterWeb(DefaultBattles, DefaultSeed, DefaultCounterWebBudget, out string report, out string csvPath,
                    (done, total) => EditorUtility.DisplayProgressBar("Counter web", $"Matchup {done} of {total}", done / (float)total));
                if (result.AllPass) Debug.Log(report + "\nMatrix CSV written to " + csvPath);
                else Debug.LogWarning(report + "\nMatrix CSV written to " + csvPath);
            }
            catch (Exception e)
            {
                Debug.LogError("Counter web failed: " + e.Message);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Command-line entry point (-executeMethod). Exit code 0 if every unit has a counter, 1 otherwise.</summary>
        public static void RunCounterWebBatch()
        {
            int exitCode = 1;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                CounterWebReport result = RunCounterWeb(IntArg(args, "-battles", DefaultBattles), IntArg(args, "-seed", DefaultSeed),
                    IntArg(args, "-budget", DefaultCounterWebBudget), out string report, out string csvPath);
                Debug.Log("===== COUNTER WEB REPORT =====\n" + report + "Matrix CSV written to " + csvPath + "\n===== END REPORT =====");
                if (result.AllPass) exitCode = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("Counter web failed: " + e);
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Runs the counter web on the slice rosters and writes the win-rate matrix CSV.</summary>
        public static CounterWebReport RunCounterWeb(int battles, int seed, int budget, out string report, out string csvPath,
            Action<int, int> progress = null)
        {
            List<UnitStats> units = LoadSliceUnits().Select(ValidatedStats).ToList();
            if (units.Count < 2) throw new InvalidOperationException("Need the slice roster unit assets in " + string.Join(", ", SliceFolders) + ".");
            var settings = new CounterWebSettings { Budget = budget, Runner = DefaultSettings(battles, seed) };
            CounterWebReport result = CounterWeb.Run(LoadRules(), units, settings, progress);
            report = $"Slice rosters, about {budget} credits per army, {battles} battles per matchup.\n" + CounterWeb.FormatReport(result);
            csvPath = WriteCsv("counter_web", CounterWeb.MatrixCsv(result));
            return result;
        }

        /// <summary>Every unit of the slice rosters, UNSC first, in file name order.</summary>
        public static List<UnitDefinition> LoadSliceUnits()
        {
            var units = new List<UnitDefinition>();
            foreach (string folder in SliceFolders) units.AddRange(LoadUnitsInFolder(folder));
            return units;
        }

        [MenuItem("Halobellum/Headless Runner/Open Runner Window")]
        public static void OpenWindow() => HeadlessRunnerWindow.Open();

        /// <summary>
        /// Runs the Battle Setup selected in the Project window (or the scene's test battle if none
        /// is selected) once, headless, and prints the same summary line the battle scene prints when
        /// its battle ends. Matching lines show the game and the runner produce the same result.
        /// </summary>
        [MenuItem("Halobellum/Headless Runner/Run Selected Battle Setup")]
        public static void RunSelectedBattleSetup()
        {
            var setup = Selection.activeObject as BattleSetupDefinition;
            if (setup == null) setup = AssetDatabase.LoadAssetAtPath<BattleSetupDefinition>(BattleSceneBuilder.TestBattlePath);
            if (setup == null)
            {
                Debug.LogError("Select a Battle Setup asset in the Project window first.");
                return;
            }
            BattleResult result = HeadlessBattleRunner.RunBattle(LoadRules(), setup.BuildArmyA(), setup.BuildArmyB(), setup.BuildLayoutSettings(), setup.seed);
            Debug.Log($"Headless result for '{setup.name}': {result.ToSummaryLine()}");
        }

        /// <summary>Command-line entry point (-executeMethod). Exits Unity with code 0 on success, 1 on failure.</summary>
        public static void RunPlaceholderRoundRobinBatch()
        {
            int exitCode = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int battles = IntArg(args, "-battles", DefaultBattles);
                int seed = IntArg(args, "-seed", DefaultSeed);
                int budget = IntArg(args, "-budget", DefaultBudget);
                RunPlaceholderRoundRobin(battles, seed, budget, out string report, out string csvPath);
                Debug.Log("===== HEADLESS RUNNER REPORT =====\n" + report + "CSV written to " + csvPath + "\n===== END REPORT =====");
            }
            catch (Exception e)
            {
                Debug.LogError("Headless runner failed: " + e);
                exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Runs the placeholder round robin and writes the CSV. Returns the text report and CSV path.</summary>
        public static List<MatchupSummary> RunPlaceholderRoundRobin(int battles, int seed, int budget, out string report, out string csvPath)
        {
            CombatRules rules = LoadRules();
            List<UnitDefinition> units = LoadUnitsInFolder(PlaceholderFolder);
            if (units.Count < 2) throw new InvalidOperationException($"Need at least 2 unit definitions in {PlaceholderFolder}, found {units.Count}.");

            var armies = new List<ArmySetup>();
            foreach (UnitDefinition unit in units)
            {
                UnitStats stats = ValidatedStats(unit);
                armies.Add(ArmySetup.ForBudget(stats, budget));
            }

            List<MatchupSummary> summaries = HeadlessBattleRunner.RunRoundRobin(rules, armies, DefaultSettings(battles, seed));
            report = $"Placeholder round robin, {budget} credit armies.\n" + HeadlessBattleRunner.FormatReport(summaries);
            csvPath = WriteCsv("placeholder_round_robin", HeadlessBattleRunner.ToCsv(summaries));
            return summaries;
        }

        public static CombatRules LoadRules()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CombatRulesDefinition>(CombatRulesDefinition.DefaultAssetPath);
            if (asset == null) throw new InvalidOperationException("Missing combat rules asset at " + CombatRulesDefinition.DefaultAssetPath);
            return asset.ToRules();
        }

        public static List<UnitDefinition> LoadUnitsInFolder(string folder)
        {
            return AssetDatabase.FindAssets("t:" + nameof(UnitDefinition), new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<UnitDefinition>)
                .Where(unit => unit != null)
                .ToList();
        }

        /// <summary>Converts a unit asset to stats and throws with a clear message if its data is broken.</summary>
        public static UnitStats ValidatedStats(UnitDefinition unit)
        {
            UnitStats stats = unit.ToStats();
            List<string> problems = stats.Validate();
            if (problems.Count > 0) throw new InvalidOperationException($"{unit.name} has invalid data:\n" + string.Join("\n", problems));
            return stats;
        }

        /// <summary>Writes CSV text to BalanceReports/&lt;name&gt;_&lt;timestamp&gt;.csv next to Assets. Returns the full path.</summary>
        public static string WriteCsv(string name, string csv)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "BalanceReports");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            File.WriteAllText(path, csv);
            return path;
        }

        private static int IntArg(string[] args, string flag, int fallback)
        {
            int index = Array.IndexOf(args, flag);
            if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int value)) return value;
            return fallback;
        }
    }
}
