// HeadlessBattleRunner.cs
// Runs many battles between two armies with no graphics and summarises win rates
// (Bible Section 4, rule 4, and Section 15). It uses exactly the same BattleSimulation as the game.
// Each battle i uses seed (BaseSeed + i). The seed drives the simulation's random rolls and a small
// random nudge to each body's start position (PositionJitter), so battles differ slightly.
// Armies are laid out by ArmyLayout: squads side by side across each team's half, front rows first.
// Results come back as MatchupSummary objects, a readable text report and CSV text.
// The Unity editor tool (Scripts/Editor/HeadlessRunnerTool) loads unit data and writes the files.
// Depends on: BattleSimulation, BattleResult, SquadFormation, Halobellum.Core.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halobellum.Core;

namespace Halobellum.Combat
{
    /// <summary>Some number of squads of one unit type.</summary>
    public sealed class ArmySquad
    {
        public UnitStats Stats;
        public int SquadCount;
        /// <summary>
        /// Optional fixed squad centres, one per squad, in deployment squares in the team's own frame: X across the
        /// field (0 = centre line), Y = depth behind the front edge of the team's deployment zone.
        /// Null means the squads are laid out automatically in rows.
        /// </summary>
        public List<Vec2> Positions;

        public ArmySquad(UnitStats stats, int squadCount, List<Vec2> positions = null)
        {
            Stats = stats;
            SquadCount = squadCount;
            Positions = positions;
        }
    }

    /// <summary>A named army: a list of squads. Squads are placed in list order, front to back.</summary>
    public sealed class ArmySetup
    {
        public string Name;
        public List<ArmySquad> Squads = new List<ArmySquad>();

        public ArmySetup(string name)
        {
            Name = name;
        }

        public ArmySetup Add(UnitStats stats, int squadCount)
        {
            Squads.Add(new ArmySquad(stats, squadCount));
            return this;
        }

        /// <summary>Adds one squad at each given position (see ArmySquad.Positions).</summary>
        public ArmySetup AddAt(UnitStats stats, List<Vec2> positions)
        {
            Squads.Add(new ArmySquad(stats, positions.Count, new List<Vec2>(positions)));
            return this;
        }

        public int TotalCost
        {
            get
            {
                int total = 0;
                foreach (ArmySquad squad in Squads) total += squad.Stats.Cost * squad.SquadCount;
                return total;
            }
        }

        public int SquadTotal
        {
            get
            {
                int total = 0;
                foreach (ArmySquad squad in Squads) total += squad.SquadCount;
                return total;
            }
        }

        /// <summary>An army of only one unit type with as many squads as the budget buys (at least one).</summary>
        public static ArmySetup ForBudget(UnitStats stats, int budget)
        {
            int squads = stats.Cost > 0 ? Math.Max(1, budget / stats.Cost) : 1;
            return new ArmySetup(stats.Name).Add(stats, squads);
        }
    }

    /// <summary>Tool settings for a batch of battles. These are runner options, not game rules.</summary>
    public sealed class RunnerSettings
    {
        public int Battles;
        public int BaseSeed;
        /// <summary>Each body's start position is nudged by up to this many metres (seeded).</summary>
        public float PositionJitter;
        /// <summary>Distance in metres from the centre line to each army's front row.</summary>
        public float FrontLineDistance;
        /// <summary>Gap in metres between neighbouring squads, sideways and between rows.</summary>
        public float SquadGap;
    }

    /// <summary>Aggregated results of every battle in one matchup. Averages are per battle.</summary>
    public sealed class MatchupSummary
    {
        public string ArmyA;
        public string ArmyB;
        public int CostA;
        public int CostB;
        public int Battles;
        public int FirstSeed;
        public int TeamAWins;
        public int TeamBWins;
        public int Draws;
        public int TimedOut;
        public float AverageDurationSeconds;
        public float AverageSurvivorsA;
        public float AverageSurvivorsB;
        public int DeployedA;
        public int DeployedB;
        public float AverageBaseDamageA;
        public float AverageBaseDamageB;
        public float AverageDamageDealtA;
        public float AverageDamageDealtB;
        public List<BattleResult> Results = new List<BattleResult>();

        public float WinRateA => Battles > 0 ? TeamAWins / (float)Battles : 0f;
        public float WinRateB => Battles > 0 ? TeamBWins / (float)Battles : 0f;
        public float DrawRate => Battles > 0 ? Draws / (float)Battles : 0f;
    }

    public static class HeadlessBattleRunner
    {
        /// <summary>Runs settings.Battles battles of army A (Team A) against army B (Team B).</summary>
        public static MatchupSummary RunMatchup(CombatRules rules, ArmySetup armyA, ArmySetup armyB, RunnerSettings settings)
        {
            var summary = new MatchupSummary
            {
                ArmyA = armyA.Name,
                ArmyB = armyB.Name,
                CostA = armyA.TotalCost,
                CostB = armyB.TotalCost,
                Battles = settings.Battles,
                FirstSeed = settings.BaseSeed,
            };

            for (int i = 0; i < settings.Battles; i++)
            {
                BattleResult result = RunBattle(rules, armyA, armyB, settings, settings.BaseSeed + i);
                summary.Results.Add(result);

                switch (result.Outcome)
                {
                    case BattleOutcome.TeamAWins: summary.TeamAWins++; break;
                    case BattleOutcome.TeamBWins: summary.TeamBWins++; break;
                    default: summary.Draws++; break;
                }
                if (result.TimedOut) summary.TimedOut++;
                summary.DeployedA = result.TeamA.Deployed;
                summary.DeployedB = result.TeamB.Deployed;
                summary.AverageDurationSeconds += result.DurationSeconds;
                summary.AverageSurvivorsA += result.TeamA.Survivors;
                summary.AverageSurvivorsB += result.TeamB.Survivors;
                summary.AverageBaseDamageA += result.TeamA.SurvivorBaseDamage;
                summary.AverageBaseDamageB += result.TeamB.SurvivorBaseDamage;
                summary.AverageDamageDealtA += result.TeamA.DamageDealt;
                summary.AverageDamageDealtB += result.TeamB.DamageDealt;
            }

            if (settings.Battles > 0)
            {
                float n = settings.Battles;
                summary.AverageDurationSeconds /= n;
                summary.AverageSurvivorsA /= n;
                summary.AverageSurvivorsB /= n;
                summary.AverageBaseDamageA /= n;
                summary.AverageBaseDamageB /= n;
                summary.AverageDamageDealtA /= n;
                summary.AverageDamageDealtB /= n;
            }
            return summary;
        }

        /// <summary>Runs every pair of armies against each other once (A vs B, A vs C, B vs C, ...).</summary>
        public static List<MatchupSummary> RunRoundRobin(CombatRules rules, IReadOnlyList<ArmySetup> armies, RunnerSettings settings)
        {
            var summaries = new List<MatchupSummary>();
            for (int i = 0; i < armies.Count; i++)
            {
                for (int j = i + 1; j < armies.Count; j++)
                {
                    summaries.Add(RunMatchup(rules, armies[i], armies[j], settings));
                }
            }
            return summaries;
        }

        /// <summary>Sets up and runs one battle with the given seed.</summary>
        public static BattleResult RunBattle(CombatRules rules, ArmySetup armyA, ArmySetup armyB, RunnerSettings settings, int seed)
        {
            return CreateBattle(rules, armyA, armyB, settings, seed).Run();
        }

        /// <summary>
        /// Builds a battle with both armies placed, ready to step. The game's battle scene uses this
        /// too, so the same armies, settings and seed give the same battle in the game and the runner.
        /// </summary>
        public static BattleSimulation CreateBattle(CombatRules rules, ArmySetup armyA, ArmySetup armyB, RunnerSettings settings, int seed)
        {
            var simulation = new BattleSimulation(rules, seed);
            var layoutRandom = new SimRandom(seed ^ 0x5EED);
            ArmyLayout.Place(simulation, rules, armyA, Team.A, settings, layoutRandom);
            ArmyLayout.Place(simulation, rules, armyB, Team.B, settings, layoutRandom);
            return simulation;
        }

        // ------------------------------------------------------------------ reports

        public static string FormatReport(IReadOnlyList<MatchupSummary> summaries)
        {
            var text = new StringBuilder();
            if (summaries.Count == 0) return "No matchups were run.";

            // Invariant formatting: decimals always use a dot, matching the CSV, whatever the PC's language.
            MatchupSummary first = summaries[0];
            text.AppendLine(FormattableString.Invariant($"Headless battle results: {first.Battles} battles per matchup, seeds {first.FirstSeed} to {first.FirstSeed + first.Battles - 1}."));
            text.AppendLine("Winner = side whose survivors deal more base damage (a wipe always wins).");
            text.AppendLine();
            foreach (MatchupSummary s in summaries)
            {
                text.AppendLine(FormattableString.Invariant($"{s.ArmyA} ({s.CostA} cr, {s.DeployedA} bodies)  vs  {s.ArmyB} ({s.CostB} cr, {s.DeployedB} bodies)"));
                text.AppendLine(FormattableString.Invariant($"  Wins: {s.ArmyA} {Percent(s.WinRateA)}, {s.ArmyB} {Percent(s.WinRateB)}, draws {Percent(s.DrawRate)}, timed out {s.TimedOut}/{s.Battles}"));
                text.AppendLine(FormattableString.Invariant($"  Average length {s.AverageDurationSeconds:0.0} s; survivors {s.AverageSurvivorsA:0.0} vs {s.AverageSurvivorsB:0.0}; ") +
                                FormattableString.Invariant($"base damage {s.AverageBaseDamageA:0.0} vs {s.AverageBaseDamageB:0.0}; damage dealt {s.AverageDamageDealtA:0} vs {s.AverageDamageDealtB:0}"));
                text.AppendLine();
            }
            return text.ToString();
        }

        public const string CsvHeader =
            "ArmyA,ArmyB,CostA,CostB,BodiesA,BodiesB,Battles,FirstSeed,WinsA,WinsB,Draws,WinRateA,WinRateB,TimedOut," +
            "AvgDurationSeconds,AvgSurvivorsA,AvgSurvivorsB,AvgBaseDamageA,AvgBaseDamageB,AvgDamageDealtA,AvgDamageDealtB";

        /// <summary>One header row plus one row per matchup. Numbers use a dot as the decimal separator.</summary>
        public static string ToCsv(IReadOnlyList<MatchupSummary> summaries)
        {
            var csv = new StringBuilder();
            csv.AppendLine(CsvHeader);
            foreach (MatchupSummary s in summaries)
            {
                csv.AppendLine(string.Join(",",
                    Csv(s.ArmyA), Csv(s.ArmyB), Num(s.CostA), Num(s.CostB), Num(s.DeployedA), Num(s.DeployedB),
                    Num(s.Battles), Num(s.FirstSeed), Num(s.TeamAWins), Num(s.TeamBWins), Num(s.Draws),
                    Num(s.WinRateA), Num(s.WinRateB), Num(s.TimedOut), Num(s.AverageDurationSeconds),
                    Num(s.AverageSurvivorsA), Num(s.AverageSurvivorsB), Num(s.AverageBaseDamageA), Num(s.AverageBaseDamageB),
                    Num(s.AverageDamageDealtA), Num(s.AverageDamageDealtB)));
            }
            return csv.ToString();
        }

        private static string Percent(float rate) => (rate * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Csv(string value)
        {
            if (value == null) return "";
            bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n");
            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }
    }

    /// <summary>
    /// Places an army's squads in its team's half for test battles. Squads with fixed positions go
    /// exactly there; the others stand side by side across the field, wrapping into new rows further
    /// back when a row is full. Team B is the mirror image of Team A. (The real deployment grid
    /// comes in Milestone 3.)
    /// </summary>
    public static class ArmyLayout
    {
        public static void Place(BattleSimulation simulation, CombatRules rules, ArmySetup army, Team team,
            RunnerSettings settings, SimRandom random)
        {
            float usableWidth = rules.ArenaWidth - 2f * settings.SquadGap;
            float rowStartX = -usableWidth * 0.5f;
            float x = rowStartX;
            float rowDepth = 0f;
            float rowFront = settings.FrontLineDistance; // Distance from the centre line to the front of this row.

            // Collect squad centres row by row (in Team A's frame), then centre each row across the field.
            var row = new List<(UnitStats stats, float width, float depth)>();
            void FlushRow()
            {
                if (row.Count == 0) return;
                float rowWidth = -settings.SquadGap;
                foreach (var s in row) rowWidth += s.width + settings.SquadGap;
                float left = -rowWidth * 0.5f;
                foreach (var s in row)
                {
                    float centreX = left + s.width * 0.5f;
                    float centreY = -(rowFront + s.depth * 0.5f); // Team A's half is negative Y.
                    AddSquad(simulation, rules, s.stats, team, new Vec2(centreX, centreY), settings, random);
                    left += s.width + settings.SquadGap;
                }
                rowFront += rowDepth + settings.SquadGap;
                row.Clear();
                rowDepth = 0f;
                x = rowStartX;
            }

            foreach (ArmySquad squad in army.Squads)
            {
                if (squad.Positions != null)
                {
                    // Fixed positions in squares; depth is measured back from the front edge of the deployment zone.
                    foreach (Vec2 position in squad.Positions)
                    {
                        var centre = new Vec2(position.X * rules.SquareSize, -(rules.DeploymentZoneFront + position.Y * rules.SquareSize));
                        AddSquad(simulation, rules, squad.Stats, team, centre, settings, random);
                    }
                    continue;
                }

                for (int n = 0; n < squad.SquadCount; n++)
                {
                    FootprintOf(squad.Stats, rules.FormationGap, out float width, out float depth);
                    if (row.Count > 0 && x + width > rowStartX + usableWidth) FlushRow();
                    row.Add((squad.Stats, width, depth));
                    x += width + settings.SquadGap;
                    rowDepth = Math.Max(rowDepth, depth);
                }
            }
            FlushRow();
        }

        /// <summary>Width and depth in metres of a squad standing in formation (not rotated).</summary>
        public static void FootprintOf(UnitStats stats, float formationGap, out float width, out float depth)
        {
            int columns = SquadFormation.Columns(stats.SquadSize);
            int rows = (int)MathF.Ceiling(stats.SquadSize / (float)columns);
            float spacing = stats.CollisionRadius * 2f + formationGap;
            width = (columns - 1) * spacing + stats.CollisionRadius * 2f;
            depth = (rows - 1) * spacing + stats.CollisionRadius * 2f;
        }

        private static void AddSquad(BattleSimulation simulation, CombatRules rules, UnitStats stats, Team team,
            Vec2 centreInTeamAFrame, RunnerSettings settings, SimRandom random)
        {
            Vec2 centre = team == Team.A ? centreInTeamAFrame : -centreInTeamAFrame;
            List<Vec2> offsets = SquadFormation.Offsets(stats.SquadSize, stats.CollisionRadius, rules.FormationGap, false);
            int squadId = simulation.Units.Count; // Unique per squad within this battle.
            foreach (Vec2 offset in offsets)
            {
                Vec2 placed = team == Team.A ? offset : -offset;
                Vec2 jitter = settings.PositionJitter > 0f
                    ? new Vec2(random.Range(-1f, 1f), random.Range(-1f, 1f)) * settings.PositionJitter
                    : Vec2.Zero;
                simulation.AddUnit(stats, team, centre + placed + jitter, squadId);
            }
        }
    }
}
