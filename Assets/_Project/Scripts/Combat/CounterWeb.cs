// CounterWeb.cs
// Checks the counter web (Bible Sections 7 and 15, Milestone 4): every unit fights every other unit
// in single-type armies of (nearly) equal cost, with the headless runner. For each unit, its counters
// are the units whose role is in its role's "weak against" list (Section 7 table); the unit passes
// if its best counter wins more than the threshold (70%) of the battles against it.
// Armies: as many squads of each type as fit a budget (default 1800 credits), picking the two squad
// counts whose total costs are closest to each other (both within 80% to 125% of the budget).
// Half of each matchup's battles swap the sides, so neither unit always plays Team A.
// Win = D9: the side whose survivors would deal more base damage.
// Plain C#: the Unity editor tool (HeadlessRunnerTool) and the tests both call it.
// Depends on: HeadlessBattleRunner, BattleResult, Halobellum.Core.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halobellum.Core;

namespace Halobellum.Combat
{
    /// <summary>Counter web options. These are runner options, not game rules.</summary>
    public sealed class CounterWebSettings
    {
        public int Budget = 1800;
        public float MinBudgetShare = 0.8f;
        public float MaxBudgetShare = 1.25f;
        /// <summary>A counter must win MORE than this share of its battles (Milestone 4: 70%).</summary>
        public float Threshold = 0.7f;
        /// <summary>Battles per matchup, seeds, layout and position jitter.</summary>
        public RunnerSettings Runner;
    }

    /// <summary>Result of one pair of unit types: how often A beat B, B beat A, or drew.</summary>
    public sealed class CounterMatchup
    {
        public UnitStats A;
        public UnitStats B;
        public int SquadsA;
        public int SquadsB;
        public int Battles;
        public int WinsA;
        public int WinsB;
        public int Draws;
        public float WinRateA => Battles > 0 ? WinsA / (float)Battles : 0f;
        public float WinRateB => Battles > 0 ? WinsB / (float)Battles : 0f;
        public int CostA => SquadsA * A.Cost;
        public int CostB => SquadsB * B.Cost;
    }

    /// <summary>One unit's line in the report: its best counters and whether it passes.</summary>
    public sealed class CounterWebLine
    {
        public UnitStats Unit;
        /// <summary>Units whose role counters this unit's role, best first, with their win rate against it.</summary>
        public List<(UnitStats counter, float winRate)> Counters = new List<(UnitStats, float)>();
        public bool Passes;
        public float BestRate => Counters.Count > 0 ? Counters[0].winRate : 0f;
        public UnitStats BestCounter => Counters.Count > 0 ? Counters[0].counter : null;
    }

    public sealed class CounterWebReport
    {
        public List<UnitStats> Units = new List<UnitStats>();
        public List<CounterMatchup> Matchups = new List<CounterMatchup>();
        public List<CounterWebLine> Lines = new List<CounterWebLine>();
        public float Threshold;
        public bool AllPass => Lines.TrueForAll(l => l.Passes);

        /// <summary>How often attacker beat defender (0 to 1), or -1 if they never met.</summary>
        public float WinRate(UnitStats attacker, UnitStats defender)
        {
            foreach (CounterMatchup m in Matchups)
            {
                if (m.A == attacker && m.B == defender) return m.WinRateA;
                if (m.B == attacker && m.A == defender) return m.WinRateB;
            }
            return -1f;
        }
    }

    public static class CounterWeb
    {
        /// <summary>
        /// True if a unit of counterRole is listed as a counter of targetRole in the Bible's Section 7
        /// table ("weak against"). Anti-air is weak against ground units, so for anti-air every
        /// ground unit that is not anti-air itself counts (see IsCounter).
        /// </summary>
        public static bool RoleCounters(UnitRole counterRole, UnitRole targetRole)
        {
            switch (targetRole)
            {
                case UnitRole.Chaff: return counterRole == UnitRole.ChaffClear;
                case UnitRole.ChaffClear: return counterRole == UnitRole.Tank || counterRole == UnitRole.Flanker;
                case UnitRole.SingleTargetDps: return counterRole == UnitRole.Chaff || counterRole == UnitRole.Flanker;
                case UnitRole.Tank: return counterRole == UnitRole.SingleTargetDps;
                case UnitRole.Flanker: return counterRole == UnitRole.ChaffClear || counterRole == UnitRole.Tank;
                case UnitRole.Artillery: return counterRole == UnitRole.Flanker || counterRole == UnitRole.Aerial;
                case UnitRole.Aerial: return counterRole == UnitRole.AntiAir;
                case UnitRole.Titan: return counterRole == UnitRole.SingleTargetDps || counterRole == UnitRole.Chaff;
                default: return false; // Anti-air: see IsCounter.
            }
        }

        /// <summary>True if counter's role counters target's role (anti-air: any ground unit that is not anti-air).</summary>
        public static bool IsCounter(UnitStats counter, UnitStats target)
        {
            if (target.Role == UnitRole.AntiAir) return !counter.IsAir && counter.Role != UnitRole.AntiAir;
            return RoleCounters(counter.Role, target.Role);
        }

        /// <summary>Squad counts for two unit types whose army costs are as close as possible, near the budget.</summary>
        public static void EqualCostCounts(int costA, int costB, CounterWebSettings settings, out int squadsA, out int squadsB)
        {
            List<int> totalsA = Options(costA, settings), totalsB = Options(costB, settings);
            squadsA = Math.Max(1, (int)MathF.Round(settings.Budget / (float)Math.Max(1, costA)));
            squadsB = Math.Max(1, (int)MathF.Round(settings.Budget / (float)Math.Max(1, costB)));
            double best = double.MaxValue;
            foreach (int a in totalsA)
            {
                foreach (int b in totalsB)
                {
                    double difference = Math.Abs(Math.Log(a * (double)costA / (b * (double)costB)));
                    double nearBudget = Math.Abs(Math.Log((a * (double)costA + b * (double)costB) * 0.5 / settings.Budget)) * 1e-3;
                    if (difference + nearBudget >= best) continue;
                    best = difference + nearBudget;
                    squadsA = a;
                    squadsB = b;
                }
            }
        }

        private static List<int> Options(int cost, CounterWebSettings settings)
        {
            var counts = new List<int>();
            cost = Math.Max(1, cost);
            for (int n = 1; n * cost <= settings.Budget * settings.MaxBudgetShare; n++)
            {
                if (n * cost >= settings.Budget * settings.MinBudgetShare) counts.Add(n);
            }
            if (counts.Count == 0) counts.Add(Math.Max(1, (int)MathF.Round(settings.Budget / (float)cost)));
            return counts;
        }

        /// <summary>Runs one pair of unit types (half the battles with the sides swapped).</summary>
        public static CounterMatchup RunPair(CombatRules rules, UnitStats a, UnitStats b, CounterWebSettings settings)
        {
            EqualCostCounts(a.Cost, b.Cost, settings, out int squadsA, out int squadsB);
            var matchup = new CounterMatchup { A = a, B = b, SquadsA = squadsA, SquadsB = squadsB };
            ArmySetup armyA = new ArmySetup(a.Name).Add(a, squadsA);
            ArmySetup armyB = new ArmySetup(b.Name).Add(b, squadsB);

            for (int i = 0; i < settings.Runner.Battles; i++)
            {
                bool swapped = i % 2 == 1;
                BattleResult result = HeadlessBattleRunner.RunBattle(rules, swapped ? armyB : armyA, swapped ? armyA : armyB,
                    settings.Runner, settings.Runner.BaseSeed + i);
                matchup.Battles++;
                bool firstWon = result.Outcome == BattleOutcome.TeamAWins, secondWon = result.Outcome == BattleOutcome.TeamBWins;
                if (!firstWon && !secondWon) matchup.Draws++;
                else if (firstWon != swapped) matchup.WinsA++;
                else matchup.WinsB++;
            }
            return matchup;
        }

        /// <summary>Every unordered pair of distinct units, in a fixed order (for running them, possibly in parallel).</summary>
        public static List<(UnitStats a, UnitStats b)> Pairs(IReadOnlyList<UnitStats> units)
        {
            var pairs = new List<(UnitStats, UnitStats)>();
            for (int i = 0; i < units.Count; i++)
            {
                for (int j = i + 1; j < units.Count; j++) pairs.Add((units[i], units[j]));
            }
            return pairs;
        }

        /// <summary>Runs every pair and builds the report.</summary>
        public static CounterWebReport Run(CombatRules rules, IReadOnlyList<UnitStats> units, CounterWebSettings settings,
            Action<int, int> progress = null)
        {
            List<(UnitStats a, UnitStats b)> pairs = Pairs(units);
            var matchups = new List<CounterMatchup>();
            for (int i = 0; i < pairs.Count; i++)
            {
                matchups.Add(RunPair(rules, pairs[i].a, pairs[i].b, settings));
                progress?.Invoke(i + 1, pairs.Count);
            }
            return BuildReport(units, matchups, settings.Threshold);
        }

        /// <summary>Builds the per-unit lines from finished matchups (also used when the pairs ran in parallel).</summary>
        public static CounterWebReport BuildReport(IReadOnlyList<UnitStats> units, List<CounterMatchup> matchups, float threshold)
        {
            var report = new CounterWebReport { Threshold = threshold, Matchups = matchups };
            report.Units.AddRange(units);
            foreach (UnitStats unit in units)
            {
                var line = new CounterWebLine { Unit = unit };
                foreach (UnitStats other in units)
                {
                    if (other == unit || !IsCounter(other, unit)) continue;
                    float rate = report.WinRate(other, unit);
                    if (rate >= 0f) line.Counters.Add((other, rate));
                }
                line.Counters.Sort((x, y) => y.winRate.CompareTo(x.winRate));
                line.Passes = line.BestRate > threshold;
                report.Lines.Add(line);
            }
            return report;
        }

        /// <summary>A readable summary: one line per unit with its best counters.</summary>
        public static string FormatReport(CounterWebReport report)
        {
            var text = new StringBuilder();
            int passing = report.Lines.FindAll(l => l.Passes).Count;
            text.AppendLine($"Counter web: {passing} of {report.Lines.Count} units have a counter winning over {Percent(report.Threshold)}.");
            foreach (CounterWebLine line in report.Lines)
            {
                string counters = line.Counters.Count == 0 ? "(no counter role in the roster)" : "";
                for (int i = 0; i < line.Counters.Count && i < 3; i++)
                {
                    if (i > 0) counters += ", ";
                    counters += $"{line.Counters[i].counter.Name} {Percent(line.Counters[i].winRate)}";
                }
                text.AppendLine(Invariant($"  {(line.Passes ? "PASS" : "FAIL")}  {line.Unit.Name} ({line.Unit.Role}): {counters}"));
            }
            return text.ToString();
        }

        /// <summary>The full win-rate matrix as CSV: row unit's win rate against column unit.</summary>
        public static string MatrixCsv(CounterWebReport report)
        {
            var csv = new StringBuilder();
            csv.Append("unit \\ against");
            foreach (UnitStats unit in report.Units) csv.Append(',').Append(Quote(unit.Name));
            csv.AppendLine();
            foreach (UnitStats row in report.Units)
            {
                csv.Append(Quote(row.Name));
                foreach (UnitStats column in report.Units)
                {
                    float rate = row == column ? -1f : report.WinRate(row, column);
                    csv.Append(',').Append(rate < 0f ? "" : rate.ToString("0.00", CultureInfo.InvariantCulture));
                }
                csv.AppendLine();
            }
            return csv.ToString();
        }

        /// <summary>A share (0 to 1) as a whole percentage, e.g. "70%".</summary>
        public static string Percent(float share) => MathF.Round(share * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

        private static string Quote(string value) => value.Contains(",") ? "\"" + value + "\"" : value;

        private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
