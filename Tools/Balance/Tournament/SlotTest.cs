// SlotTest.cs
// The army slot test (D137): what one unit is worth inside the armies the AI really builds, at equal credits.
// It takes real armies from a tournament's results file (each side's squads at the end of a match, at the levels they reached) and brings
// each to the same budget, keeping its mix. For every unit a sub-faction can field, it adds a slot of that unit (about a fifth of the budget)
// to the sub-faction's army and fights the real enemy army from the same match, grown to exactly the same credits with more of its own mix.
// The baseline gives the army a fifth more of its own mix instead. Every variant of a pair fights on the same seed, so they differ only by
// the slot. A unit's value is how many points of win rate its slot adds over the baseline: 0 means its credits are worth as much as the
// army's average credit, below 0 less, above 0 more. Shared units are measured in each army that fields them.
// Win or loss is coarse, so it also reports the margin: the base damage of the survivors (what they would deal to the enemy's base in a
// match) minus the enemy's, as a share of the army's base damage. It moves with every body that lives or dies, so its value needs far
// fewer battles to settle than the win rate's.
// Levels: with --levels real (the default) every squad fights at the level its unit had in that army, a slot at the average level its unit
// reached in that sub-faction's armies, and a level costs what it costs in a match (half the price each, LevelUpCostShare), so a unit that
// lives and levels up is worth what that brings. With --levels one every squad is level 1: the worth of the unit's price alone.
// Battles only: no towers, techs, cards, items or spells; squads stand on the real deployment grid where Hard puts them by role
// (chaff and tanks in front, artillery at the back), and units that can flank go to the flank zones, left and right in turn, arriving on
// time as they do after their first battle. Draws count half.
// Usage: dotnet Tournament.dll <game_data.json> - <out.txt> --slot <results.jsonl> [--pairs 210] [--budget 4000] [--slot-share 0.2]
//        [--levels real|one] [--only "Sub A,Sub B"] [--trial trial.txt] [--threads 8]
// It writes the report to out.txt and one line per unit to out.csv next to it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;

static class SlotTest
{
    /// <summary>Some squads of one unit at one average level.</summary>
    sealed class Entry
    {
        public UnitStats Type;
        public int Squads;
        public double Level = 1;
        public double SquadCost => Type.Cost * (1 + _levelShare * (Level - 1));
    }

    sealed class Pair
    {
        public string Sub, Enemy;
        public List<Entry> Own, Foe;
    }

    static double _levelShare;

    public static int Run(string sourcePath, string outPath, List<FactionInfo> factions, MatchRules matchRules, CombatRules combat, int threads,
        int pairsPerSub, int budget, float slotShare, bool realLevels, HashSet<string> only)
    {
        _levelShare = matchRules.LevelUpCostShare;
        var rosters = new Dictionary<string, List<UnitStats>>();
        foreach (FactionInfo faction in factions)
            foreach (SubFactionInfo sub in faction.SubFactions)
                rosters[sub.Name] = faction.RosterFor(sub).Where(u => u != null && u.Cost > 0).OrderBy(u => u.Name, StringComparer.Ordinal).ToList();

        // Real armies: each side's squads still owned at the end of a match, of units its roster can buy.
        var all = new List<Pair>();
        foreach (string line in File.ReadLines(sourcePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement m = doc.RootElement;
            string a = m.GetProperty("a").GetString(), b = m.GetProperty("b").GetString();
            if (a == b || !rosters.ContainsKey(a) || !rosters.ContainsKey(b)) continue;
            bool aFirst = m.GetProperty("aOnTeam").GetInt32() == 1;
            JsonElement sideOfA = m.GetProperty(aFirst ? "sideA" : "sideB"), sideOfB = m.GetProperty(aFirst ? "sideB" : "sideA");
            var armyA = ArmyOf(sideOfA, rosters[a], realLevels);
            var armyB = ArmyOf(sideOfB, rosters[b], realLevels);
            if (armyA.Count == 0 || armyB.Count == 0) continue;
            all.Add(new Pair { Sub = a, Enemy = b, Own = armyA, Foe = armyB });
            all.Add(new Pair { Sub = b, Enemy = a, Own = armyB, Foe = armyA });
        }

        var subs = rosters.Keys.Where(s => only == null || only.Contains(s)).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var chosen = new Dictionary<string, List<Pair>>();
        var slotLevel = new Dictionary<string, double[]>();
        foreach (string sub in subs)
        {
            // The same number of armies against every opponent, in file order.
            var mine = all.Where(p => p.Sub == sub).ToList();
            var enemies = mine.Select(p => p.Enemy).Distinct().OrderBy(e => e, StringComparer.Ordinal).ToList();
            int each = Math.Max(1, (int)Math.Ceiling(pairsPerSub / (double)Math.Max(1, enemies.Count)));
            chosen[sub] = enemies.SelectMany(e => mine.Where(p => p.Enemy == e).Take(each)).ToList();
            // A slot comes at the average level its unit reached in this sub-faction's armies (in any army, if this one never kept it).
            slotLevel[sub] = rosters[sub].Select(u => AverageLevel(mine, u) ?? AverageLevel(all, u) ?? 1.0).ToArray();
        }

        int slot = (int)Math.Round(budget * slotShare);
        var jobs = new List<(string sub, int pair)>();
        foreach (string sub in subs) for (int i = 0; i < chosen[sub].Count; i++) jobs.Add((sub, i));
        // scores[sub][pair][0] is the baseline, [1 + u] the slot of roster unit u; margins likewise.
        var scores = subs.ToDictionary(s => s, s => new double[chosen[s].Count][]);
        var margins = subs.ToDictionary(s => s, s => new double[chosen[s].Count][]);
        var slotSquads = subs.ToDictionary(s => s, s => new int[chosen[s].Count][]);
        long overflow = 0, battles = 0;
        double gapTotal = 0;
        string levels = realLevels ? "at the levels they reached" : "all at level 1";
        Console.WriteLine($"Army slot test: {subs.Count} sub-factions, {jobs.Count} army pairs, budget {budget}, slot {slot}, squads {levels}, on {threads} threads.");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int finished = 0;
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = threads }, job =>
        {
            Pair pair = chosen[job.sub][job.pair];
            List<UnitStats> roster = rosters[job.sub];
            int seed = 1000 + job.pair;
            var own = Scale(pair.Own, budget);
            double ownCost = CostOf(own);
            var result = new double[roster.Count + 1];
            var margin = new double[roster.Count + 1];
            var squads = new int[roster.Count];
            int over = 0;
            double gaps = 0;

            (result[0], margin[0]) = Fight(Scale(pair.Own, ownCost + slot), pair.Foe, combat, seed, ref over, ref gaps);
            for (int u = 0; u < roster.Count; u++)
            {
                var added = new Entry { Type = roster[u], Level = slotLevel[job.sub][u] };
                added.Squads = Math.Max(1, (int)Math.Round(slot / added.SquadCost));
                squads[u] = added.Squads;
                var withSlot = new List<Entry>(own) { added };
                (result[u + 1], margin[u + 1]) = Fight(withSlot, pair.Foe, combat, seed, ref over, ref gaps);
            }
            scores[job.sub][job.pair] = result;
            margins[job.sub][job.pair] = margin;
            slotSquads[job.sub][job.pair] = squads;
            lock (scores)
            {
                overflow += over;
                gapTotal += gaps;
                battles += roster.Count + 1;
            }
            int done = Interlocked.Increment(ref finished);
            if (done % 50 == 0) Console.WriteLine($"{done} of {jobs.Count} pairs, {clock.Elapsed.TotalMinutes:0.0} minutes");
        });

        var text = new StringBuilder();
        var csv = new StringBuilder("sub,unit,cost,level,slot_squads,win,value,ci95,margin_value,margin_ci95,in_armies,pairs\n");
        text.AppendLine($"Army slot test: real end-of-match armies from {Path.GetFileName(sourcePath)}, squads {levels}, brought to {budget} credits; a slot is about {slot} credits.");
        if (realLevels) text.AppendLine("A slot comes at the average level its unit reached in that sub-faction's armies (lvl); each level costs half the unit's price, as in a match.");
        text.AppendLine("value = points of win rate the unit's slot adds over a slot of the army's own mix (0 = as good as the army's average credit).");
        text.AppendLine("margin = the same for the survivors' base damage minus the enemy's, in points of the army's base damage: steadier than the win rate.");
        text.AppendLine("±95% = how far a value could be from the truth by chance. in armies = share of the source armies that had the unit. Sorted by margin.");
        text.AppendLine($"{battles} battles in {clock.Elapsed.TotalMinutes:0.0} minutes; squads that found no room: {overflow}; enemy credits off by {gapTotal / Math.Max(1, battles):0} on average.");
        foreach (string sub in subs)
        {
            var pairs = chosen[sub];
            int n = pairs.Count;
            List<UnitStats> roster = rosters[sub];
            double baseWin = scores[sub].Average(r => r[0]);
            text.AppendLine();
            text.AppendLine($"== {sub}: its armies win {100 * baseWin:0.0}% of {n} battles against the real enemy armies at equal credits (margin {100 * margins[sub].Average(r => r[0]):+0;-0})");
            text.AppendLine($"{"unit",-24}{"cost",6}{"lvl",5}{"slot",6}{"win%",7}{"value",8}{"±95%",7}{"margin",8}{"±95%",7}{"in armies",11}");
            var rows = new List<(string name, int cost, double level, double slotN, double win, double value, double ci, double mValue, double mCi, double share)>();
            for (int u = 0; u < roster.Count; u++)
            {
                var (mean, ci) = Paired(scores[sub], u + 1);
                var (mMean, mCi) = Paired(margins[sub], u + 1);
                double share = pairs.Count(p => p.Own.Any(e => e.Type == roster[u])) / (double)n;
                rows.Add((roster[u].Name, roster[u].Cost, slotLevel[sub][u], slotSquads[sub].Average(s => s[u]), scores[sub].Average(r => r[u + 1]), mean, ci, mMean, mCi, share));
            }
            foreach (var r in rows.OrderByDescending(r => r.mValue))
            {
                text.AppendLine($"{r.name,-24}{r.cost,6}{r.level,5:0.0}{r.slotN,6:0.#}{100 * r.win,7:0.0}{100 * r.value,8:+0.0;-0.0}{100 * r.ci,7:0.0}{100 * r.mValue,8:+0.0;-0.0}{100 * r.mCi,7:0.0}{100 * r.share,10:0}%");
                csv.AppendLine(string.Join(",", Quote(sub), Quote(r.name), r.cost, F(r.level), r.slotN.ToString("0.##", CultureInfo.InvariantCulture),
                    F(100 * r.win), F(100 * r.value), F(100 * r.ci), F(100 * r.mValue), F(100 * r.mCi), (100 * r.share).ToString("0", CultureInfo.InvariantCulture), n));
            }
        }
        File.WriteAllText(outPath, text.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.ChangeExtension(outPath, ".csv"), csv.ToString(), new UTF8Encoding(false));
        Console.Write(text.ToString());
        return 0;
    }

    static string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";

    static string F(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>The mean of (variant - baseline) over the pairs, and the half-width of its 95% interval.</summary>
    static (double mean, double ci) Paired(double[][] rows, int variant)
    {
        var diffs = rows.Select(r => r[variant] - r[0]).ToArray();
        double mean = diffs.Average();
        double sd = Math.Sqrt(diffs.Sum(d => (d - mean) * (d - mean)) / Math.Max(1, diffs.Length - 1));
        return (mean, 1.96 * sd / Math.Sqrt(diffs.Length));
    }

    /// <summary>A unit's average level over the squads these armies kept of it, or null if none did.</summary>
    static double? AverageLevel(List<Pair> pairs, UnitStats type)
    {
        double levels = 0;
        int squads = 0;
        foreach (Pair pair in pairs)
            foreach (Entry entry in pair.Own)
                if (entry.Type == type)
                {
                    levels += entry.Level * entry.Squads;
                    squads += entry.Squads;
                }
        return squads > 0 ? levels / squads : (double?)null;
    }

    static List<Entry> ArmyOf(JsonElement side, List<UnitStats> roster, bool realLevels)
    {
        var army = new List<Entry>();
        foreach (JsonProperty unit in side.GetProperty("units").EnumerateObject())
        {
            int squads = unit.Value[4].GetInt32();
            if (squads <= 0) continue;
            UnitStats type = roster.Find(u => u.Name == unit.Name);
            if (type == null) continue;
            double level = realLevels ? Math.Max(1.0, Math.Min(UnitUpgrades.MaxLevel, unit.Value[5].GetInt32() / (double)squads)) : 1.0;
            army.Add(new Entry { Type = type, Squads = squads, Level = level });
        }
        return army;
    }

    static double CostOf(List<Entry> army) => army.Sum(e => e.SquadCost * e.Squads);

    /// <summary>The same mix brought as close as it can get to a number of credits: whole squads, the rest handed out by largest remainder.</summary>
    static List<Entry> Scale(List<Entry> army, double target)
    {
        double factor = target / Math.Max(1.0, CostOf(army));
        var counts = army.Select(e => (int)Math.Floor(e.Squads * factor)).ToArray();
        var left = army.Select((e, i) => e.Squads * factor - counts[i]).ToArray();
        double gap = target - army.Select((e, i) => e.SquadCost * counts[i]).Sum();
        while (true)
        {
            int best = -1;
            for (int i = 0; i < army.Count; i++)
            {
                if (gap <= 0 || army[i].SquadCost >= 2 * gap) continue; // one more squad would not bring the total closer
                if (best < 0 || left[i] > left[best] || left[i] == left[best] && army[i].SquadCost < army[best].SquadCost) best = i;
            }
            if (best < 0) break;
            counts[best]++;
            left[best] -= 1;
            gap -= army[best].SquadCost;
        }
        return army.Select((e, i) => new Entry { Type = e.Type, Squads = counts[i], Level = e.Level }).Where(e => e.Squads > 0).ToList();
    }

    /// <summary>
    /// One battle: this army against the enemy's mix grown to exactly its credits. The score is 1 a win, 0.5 a draw, 0 a loss; the margin is
    /// the survivors' base damage minus the enemy's, over the base damage this army started with.
    /// </summary>
    static (double score, double margin) Fight(List<Entry> army, List<Entry> enemyMix, CombatRules rules, int seed, ref int overflow, ref double gaps)
    {
        double cost = CostOf(army);
        var enemy = Scale(enemyMix, cost);
        gaps += Math.Abs(CostOf(enemy) - cost);
        PlayerState a = Deploy(Team.A, army, rules, ref overflow);
        PlayerState b = Deploy(Team.B, enemy, rules, ref overflow);
        BattleResult result = DeploymentGrid.CreateBattle(rules, a, b, seed).Run();
        double score = result.Outcome == BattleOutcome.TeamAWins ? 1.0 : result.Outcome == BattleOutcome.TeamBWins ? 0.0 : 0.5;
        double start = a.Squads.Sum(s => a.EffectiveStats(s).BaseDamage * s.Type.SquadSize);
        return (score, (result.TeamA.SurvivorBaseDamage - result.TeamB.SurvivorBaseDamage) / Math.Max(1.0, start));
    }

    static int RoleOrder(UnitRole role)
    {
        switch (role)
        {
            case UnitRole.Chaff: return 0;
            case UnitRole.Flanker: return 1;
            case UnitRole.Tank:
            case UnitRole.Titan: return 2;
            case UnitRole.ChaffClear: return 3;
            case UnitRole.SingleTargetDps: return 4;
            case UnitRole.AntiAir: return 5;
            case UnitRole.Aerial: return 6;
            default: return 7;
        }
    }

    /// <summary>
    /// Squads where Hard puts them (RuleBasedAi.FindSpotFor: by role, and in the flank zones for units that can flank, without lanes),
    /// at whole levels that average out to the entry's level.
    /// </summary>
    static PlayerState Deploy(Team team, List<Entry> army, CombatRules rules, ref int overflow)
    {
        var player = new PlayerState(team);
        var sameKind = new Dictionary<UnitRole, int>();
        int id = 0, flanks = 0, depth = rules.DeploymentDepthSquares, arenaWidth = rules.ArenaWidthSquares;
        foreach (Entry entry in army.OrderBy(e => RoleOrder(e.Type.Role)).ThenBy(e => e.Type.Name, StringComparer.Ordinal).ThenBy(e => e.Level))
        {
            UnitStats type = entry.Type;
            int levelTotal = Math.Max(entry.Squads, Math.Min(UnitUpgrades.MaxLevel * entry.Squads, (int)Math.Round(entry.Squads * entry.Level)));
            for (int n = 0; n < entry.Squads; n++)
            {
                DeploymentGrid.Footprint(type, false, rules, out int width, out int squadDepth);
                int level = levelTotal / entry.Squads + (n < levelTotal % entry.Squads ? 1 : 0);
                // RuleBasedAi.ChooseFlankSpot with no enemy anti-air to steer by: left and right in turn (Team B's grid is the mirror image).
                if (type.CanFlank && DeploymentGrid.FindFreeFlankSpot(player, type, rules, (flanks % 2 == 0) == (team == Team.A),
                        -rules.NoMansLandSquares - depth / 2 - squadDepth / 2, out int flankColumn, out int flankRow))
                {
                    flanks++;
                    player.Squads.Add(new PlacedSquad { Id = id++, Type = type, Column = flankColumn, Row = flankRow, Level = level, HasFlanked = true });
                    continue;
                }
                int targetRow;
                switch (type.Role)
                {
                    case UnitRole.Chaff: targetRow = 0; break;
                    case UnitRole.Tank:
                    case UnitRole.Titan: targetRow = 3; break;
                    case UnitRole.Flanker: targetRow = 2; break;
                    case UnitRole.ChaffClear: targetRow = 6; break;
                    case UnitRole.SingleTargetDps: targetRow = 8; break;
                    case UnitRole.AntiAir: targetRow = depth / 4; break;
                    case UnitRole.Aerial: targetRow = depth / 3; break;
                    default: targetRow = depth / 2; break;
                }
                sameKind.TryGetValue(type.Role, out int kind);
                sameKind[type.Role] = kind + 1;
                int targetColumn;
                if (type.Role == UnitRole.Flanker) targetColumn = kind % 2 == 0 ? 2 : arenaWidth - width - 2;
                else targetColumn = (arenaWidth - width) / 2 + (kind + 1) / 2 * (kind % 2 == 1 ? 1 : -1) * (width + 2);
                targetColumn = Math.Max(0, Math.Min(arenaWidth - width, targetColumn));
                targetRow = Math.Max(0, Math.Min(depth - 1, targetRow));
                if (!DeploymentGrid.FindFreeSpotNear(player, type, false, rules, targetColumn, targetRow, out int column, out int row))
                {
                    overflow++;
                    continue;
                }
                player.Squads.Add(new PlacedSquad { Id = id++, Type = type, Column = column, Row = row, Level = level });
            }
        }
        return player;
    }
}
