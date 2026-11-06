// Tuning console: loads units.json (from gen_roster.py), runs the counter web in parallel and prints
// the report. Usage: dotnet run -c Release -- [battles] [only-unit-id,...]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Tests;

static class Program
{
    static readonly Dictionary<string, int> Role = new() { ["Chaff"] = 0, ["ChaffClear"] = 1, ["SingleTargetDps"] = 2, ["Tank"] = 3, ["Flanker"] = 4, ["Artillery"] = 5, ["AntiAir"] = 6, ["Aerial"] = 7, ["Titan"] = 8 };
    static readonly Dictionary<string, int> Dtype = new() { ["Ballistic"] = 0, ["Explosive"] = 1, ["Energy"] = 2, ["Fire"] = 3, ["Melee"] = 4 };

    static void Bump(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int value);
        counts[key] = value + 1;
    }

    static int Main(string[] args)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "units.json"))) dir = Path.GetDirectoryName(dir.TrimEnd('\\', '/'));
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "units.json")));
        _doc = doc;
        _dir = dir;
        var byId = new Dictionary<string, UnitStats>();
        var units = new List<UnitStats>();
        var ids = new List<string>();
        foreach (JsonElement u in doc.RootElement.EnumerateArray())
        {
            UnitStats s = Build(u);
            byId[u.GetProperty("id").GetString()] = s;
            units.Add(s);
            ids.Add(u.GetProperty("id").GetString());
        }
        foreach (JsonElement u in doc.RootElement.EnumerateArray())
        {
            UnitStats flood = byId[u.GetProperty("id").GetString()];
            flood.FormsPerBody = u.GetProperty("forms").GetInt32();
            flood.IsFlood = u.GetProperty("flood").GetBoolean();
            flood.Infects = u.GetProperty("infects").GetBoolean();
            flood.ImmuneToInfection = u.GetProperty("immune").GetBoolean();
            flood.SelfDestructs = u.GetProperty("selfd").GetBoolean();
            flood.SeeksDensestCluster = u.GetProperty("seek").GetBoolean();
            var leap = u.GetProperty("leap");
            if (leap.ValueKind == JsonValueKind.Array)
            {
                flood.LeapRange = leap[0].GetSingle();
                flood.LeapSpeed = leap[1].GetSingle();
                flood.LeapCooldownSeconds = leap[2].GetSingle();
            }
            var burst = u.GetProperty("burst");
            if (burst.ValueKind == JsonValueKind.Object)
            {
                var spawns = new List<SpawnEntry>();
                foreach (JsonElement entry in burst.GetProperty("spawns").EnumerateArray())
                    spawns.Add(new SpawnEntry { Unit = byId[entry[0].GetString()], Bodies = entry[1].GetInt32() });
                flood.DeathBurst = new DeathBurstStats
                {
                    Damage = burst.GetProperty("dmg").GetSingle(), Radius = burst.GetProperty("radius").GetSingle(),
                    DamageType = (DamageType)Dtype[burst.GetProperty("type").GetString()], IsAntiArmor = burst.GetProperty("aa").GetBoolean(),
                    Spawns = spawns.ToArray(),
                };
            }
            var spawn = u.GetProperty("spawn");
            if (spawn.ValueKind == JsonValueKind.Array)
            {
                flood.SpawnedUnit = byId[spawn[0].GetString()].Clone();
                flood.SpawnedUnit.SquadSize = spawn[1].GetInt32();
                flood.SpawnIntervalSeconds = spawn[2].GetSingle();
            }
            var aura = u.GetProperty("aura");
            if (aura.ValueKind == JsonValueKind.Array)
            {
                flood.AuraRadius = aura[0].GetSingle();
                flood.AuraSpeedMultiplier = aura[1].GetSingle();
                flood.AuraHealPerSecondShare = aura[2].GetSingle();
            }
            flood.LifestealShare = u.GetProperty("lifesteal").GetSingle();
            if (u.TryGetProperty("seedforms", out JsonElement seeds) && seeds.ValueKind == JsonValueKind.Array)
            {
                var forms = new List<SeedForm>();
                foreach (JsonElement entry in seeds.EnumerateArray()) forms.Add(new SeedForm { From = entry[0].GetString(), Becomes = byId[entry[1].GetString()] });
                flood.SeedForms = forms.ToArray(); // D76: what the Seeder's seeds raise, and as what.
            }
        }
        foreach (JsonElement u in doc.RootElement.EnumerateArray())
        {
            if (u.GetProperty("drop").ValueKind != JsonValueKind.Array) continue;
            var d = u.GetProperty("drop");
            UnitStats carrier = byId[u.GetProperty("id").GetString()];
            UnitStats dropped = byId[d[0].GetString()].Clone();
            dropped.SquadSize = d[1].GetInt32();
            carrier.ReinforcementDelaySeconds = d[2].GetSingle();
            // A drop that needs a tech (the Pelican's Troop Bay) is not there at the start, in the game or here.
            if (d.GetArrayLength() > 3 && d[3].GetBoolean()) carrier.LockedReinforcement = dropped;
            else carrier.Reinforcement = dropped;
        }
        foreach (UnitStats s in units)
        {
            var problems = s.Validate();
            if (problems.Count > 0) { Console.WriteLine(string.Join("\n", problems)); return 1; }
        }

        if (args.Length > 0 && args[0] == "ai") return AiMatches(units, ids, args.Length > 1 ? int.Parse(args[1]) : 20);
        if (args.Length > 0 && args[0] == "stuck")
        {
            // Why Infection Forms stop: a squad behind Carrier Forms against Marines, as far apart as in a match.
            var stuckRules = TestRules.Default();
            var stuckSettings = new RunnerSettings { Battles = 1, BaseSeed = 1000, PositionJitter = 0.3f, FrontLineDistance = 40f, SquadGap = 3f };
            UnitStats formsType = units.First(u => u.Name == "Infection Forms");
            UnitStats carrierType = units.First(u => u.Name == "Carrier Form");
            UnitStats marineType = units.First(u => u.Name == "Marine");
            foreach (string scenario in new[] { "Forms only", "Forms with Carriers" })
            {
                ArmySetup flood = new ArmySetup("Flood");
                if (scenario != "Forms only") flood.Add(carrierType, 2);
                flood.Add(formsType, 3);
                var sim = HeadlessBattleRunner.CreateBattle(stuckRules, flood, new ArmySetup("UNSC").Add(marineType, 3), stuckSettings, 1000);
                var stopped = new Dictionary<string, int>();
                int packTicks = 0;
                Console.WriteLine("--- " + scenario);
                int lastReport = -30;
                while (!sim.IsFinished && sim.ElapsedSeconds < 30f)
                {
                    sim.Step();
                    int idle = 0, slowMove = 0, slowFormation = 0, moving = 0, attack = 0;
                    foreach (SimUnit u in sim.Units)
                    {
                        if (!u.IsAlive || u.Stats.Name != formsType.Name) continue;
                        packTicks++;
                        bool slow = u.Velocity.Length < 1f;
                        bool intact = sim.Squads.Any(q => q.Id == u.SquadId && q.IsIntact);
                        if (u.State == UnitState.Attack) attack++;
                        else if (u.State == UnitState.Idle) { idle++; Bump(stopped, "idle, target " + (u.MoveTarget == null ? "none" : "set") + ", unlimited " + u.UnlimitedAcquisition); }
                        else if (slow) { slowMove++; if (intact) slowFormation++; Bump(stopped, "Move but slow, " + (intact ? "formation intact" : "formation broken") + (u.MoveTarget == null ? ", no target" : ", has target")); }
                        else moving++;
                    }
                    if (sim.Tick - lastReport >= 30 && (idle + slowMove + moving + attack) > 0)
                    {
                        lastReport = sim.Tick;
                        Console.WriteLine($"{sim.ElapsedSeconds,5:0.0}s moving {moving,2}  attacking {attack,2}  slow while moving {slowMove,2} ({slowFormation} in formation)  idle {idle,2}");
                    }
                }
                foreach (var pair in stopped.OrderByDescending(p => p.Value)) Console.WriteLine($"   {pair.Value,6} pack-ticks: {pair.Key}");
                Console.WriteLine($"   {packTicks,6} pack-ticks in all; result {sim.Result?.Outcome}");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "inf")
        {
            // How much infection really does: Infection Forms against other units at equal cost.
            var infRules = TestRules.Default();
            var infSettings = new RunnerSettings { Battles = 1, BaseSeed = 1000, PositionJitter = 0.3f, FrontLineDistance = 12f, SquadGap = 3f };
            UnitStats forms = units.First(u => u.Name == "Infection Forms");
            foreach (string name in new[] { "Marine", "Grunt", "Jackal (Beam Rifle)", "Elite", "Flamethrower Trooper", "Fuel Rod Grunt", "Spartan", "Scorpion" })
            {
                UnitStats enemy = units.First(u => u.Name == name);
                CounterWeb.EqualCostCounts(forms.Cost, enemy.Cost, new CounterWebSettings { Runner = infSettings }, out int nf, out int ne);
                var sim = HeadlessBattleRunner.CreateBattle(infRules, new ArmySetup("Flood").Add(forms, nf), new ArmySetup(name).Add(enemy, ne), infSettings, 1000);
                sim.Run();
                int rose = sim.Units.Count(u => u.Stats.InfectedFrom != null);
                int total = sim.Units.Count(u => u.Stats.Name == name);
                int packs = sim.Units.Count(u => u.Stats.Name == forms.Name);
                int packsLeft = sim.Units.Count(u => u.Stats.Name == forms.Name && u.IsAlive);
                int risenLeft = sim.Units.Count(u => u.Stats.InfectedFrom != null && u.IsAlive);
                int enemyLeft = sim.Units.Count(u => u.Stats.Name == name && u.IsAlive);
                Console.WriteLine($"{name,-24} {ne} squads ({total} bodies) vs {nf} of Forms: {rose} rose ({100f * rose / total:0}%), {sim.ElapsedSeconds:0} s; left: {enemyLeft} {name}, {packsLeft}/{packs} packs, {risenLeft} risen");
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "dur")
        {
            // Battle length with the armies as far apart as squads placed mid-zone in a match (front line 40 m from the centre).
            var durRules = TestRules.Default();
            var durPairs = CounterWeb.Pairs(units);
            var durSettings = new RunnerSettings { Battles = 1, BaseSeed = 1000, PositionJitter = 0.3f, FrontLineDistance = 40f, SquadGap = 3f };
            var lengths = new float[durPairs.Count];
            var timedOut = new bool[durPairs.Count];
            var slowest = new string[durPairs.Count];
            Parallel.For(0, durPairs.Count, i =>
            {
                var (x, y) = durPairs[i];
                CounterWeb.EqualCostCounts(x.Cost, y.Cost, new CounterWebSettings { Runner = durSettings }, out int nx, out int ny);
                BattleResult r = HeadlessBattleRunner.RunBattle(durRules, new ArmySetup(x.Name).Add(x, nx), new ArmySetup(y.Name).Add(y, ny), durSettings, 1000);
                lengths[i] = r.DurationSeconds;
                timedOut[i] = r.TimedOut;
                slowest[i] = x.Name + " vs " + y.Name;
            });
            var sorted = lengths.OrderBy(v => v).ToArray();
            Console.WriteLine($"battles {sorted.Length}: mean {sorted.Average():0.0} s, median {sorted[sorted.Length / 2]:0.0} s, 90th percentile {sorted[sorted.Length * 9 / 10]:0.0} s, timed out {timedOut.Count(t => t)} ({100f * timedOut.Count(t => t) / sorted.Length:0}%)");
            return 0;
        }
        int battles = args.Length > 0 ? int.Parse(args[0]) : 10;
        var settings = new CounterWebSettings
        {
            Runner = new RunnerSettings { Battles = battles, BaseSeed = 1000, PositionJitter = 0.3f, FrontLineDistance = 20f, SquadGap = 3f },
        };
        CombatRules rules = TestRules.Default();
        var pairs = CounterWeb.Pairs(units);
        if (args.Length > 1)
        {
            var only = new HashSet<string>(args[1].Split(','));
            pairs = pairs.Where(p => only.Contains(ids[units.IndexOf(p.a)]) || only.Contains(ids[units.IndexOf(p.b)])).ToList();
        }
        var results = new CounterMatchup[pairs.Count];
        var watch = Stopwatch.StartNew();
        Parallel.For(0, pairs.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            i => results[i] = CounterWeb.RunPair(rules, pairs[i].a, pairs[i].b, settings));
        CounterWebReport report = CounterWeb.BuildReport(units, results.ToList(), settings.Threshold);
        Console.WriteLine($"{pairs.Count} pairs x {battles} battles in {watch.Elapsed.TotalSeconds:0.0} s");
        if (args.Length <= 1)
        {
            Console.Write(CounterWeb.FormatReport(report));
            File.WriteAllText(Path.Combine(dir, "matrix.csv"), CounterWeb.MatrixCsv(report));
        }
        else
        {
            // Only some units were measured again: the rest of the table stays as it was.
            string path = Path.Combine(dir, "matrix.csv");
            List<string[]> old = File.Exists(path) ? File.ReadAllLines(path).Select(l => l.Split(',')).ToList() : new List<string[]>();
            var known = new Dictionary<(string, string), string>();
            if (old.Count > 0)
            {
                for (int r = 1; r < old.Count; r++) for (int c = 1; c < old[r].Length && c < old[0].Length; c++) known[(old[r][0], old[0][c])] = old[r][c];
            }
            foreach (CounterMatchup m in results)
            {
                known[(Csv(m.A.Name), Csv(m.B.Name))] = m.WinRateA.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                known[(Csv(m.B.Name), Csv(m.A.Name))] = m.WinRateB.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }
            var names = units.Select(u => Csv(u.Name)).ToList();
            var rows = new List<string> { "unit \\ against," + string.Join(",", names) };
            foreach (string row in names) rows.Add(row + "," + string.Join(",", names.Select(column => row != column && known.TryGetValue((row, column), out string v) ? v : "")));
            File.WriteAllLines(path, rows);
        }

        // Two best counters per unit, for the asset notes.
        var counters = new Dictionary<string, string>();
        for (int i = 0; i < units.Count; i++)
        {
            CounterWebLine line = report.Lines[i];
            counters[ids[i]] = string.Join(", ", line.Counters.Take(2).Select(c => $"{c.counter.Name} ({CounterWeb.Percent(c.winRate)})"));
        }
        if (args.Length <= 1) File.WriteAllText(Path.Combine(dir, "counters.json"), JsonSerializer.Serialize(counters, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    static readonly Dictionary<string, Dictionary<string, int>> Armies = new();
    static void Add(string who, string unit) { if (!Armies.TryGetValue(who, out var d)) Armies[who] = d = new(); d[unit] = d.GetValueOrDefault(unit) + 1; }

    static string Csv(string name) => name.Replace(",", ";");

    static JsonDocument _doc;
    static string _dir;

    static int AiMatches(List<UnitStats> units, List<string> ids, int matches)
    {
        var factions = new List<Halobellum.Economy.FactionInfo>();
        foreach (JsonElement u in _doc.RootElement.EnumerateArray())
        {
            string factionName = u.GetProperty("faction").GetString();
            if (factionName == null) continue;
            var faction = factions.Find(f => f.Name == factionName);
            if (faction == null) factions.Add(faction = new Halobellum.Economy.FactionInfo { Name = factionName });
            UnitStats stats = units[ids.IndexOf(u.GetProperty("id").GetString())];
            var subValue = u.GetProperty("sub");
            if (subValue.ValueKind != JsonValueKind.String) { faction.BaseRoster.Add(stats); continue; }
            var sub = faction.SubFactions.Find(s => s.Name == subValue.GetString());
            if (sub == null) faction.SubFactions.Add(sub = new Halobellum.Economy.SubFactionInfo { Name = subValue.GetString() });
            sub.ExclusiveUnits.Add(stats);
        }
        foreach (var faction in factions) if (faction.SubFactions.Count == 0) faction.SubFactions.Add(new Halobellum.Economy.SubFactionInfo { Name = "Standard" });
        var rules = new Halobellum.Economy.MatchRules { StartingCredits = 200, BaseIncome = 400, IncomeIncreasePerRound = 200, IncomeCap = 1600, BaseHealth = 2000,
            NewSquadsPerRound = 2, UnlocksPerRound = 1, RefundBeforeBattle = 1f, RefundAfterBattle = 0.5f, DraftCardCount = 5, DraftSquadsPerCard = 2 };
        var combat = TestRules.Default();
        var table = new Halobellum.Economy.MatchupTable();
        var lines = File.ReadAllLines(Path.Combine(_dir, "matrix.csv"));
        var cols = lines[0].Split(',');
        foreach (var line in lines.Skip(1))
        {
            var cells = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            string row = cells[0].Trim('"');
            for (int j = 1; j < cells.Length; j++) if (cells[j] != "") table.Set(row, cols[j].Trim('"'), float.Parse(cells[j], System.Globalization.CultureInfo.InvariantCulture));
        }
        var levels = new[] { Halobellum.Economy.AiDifficulty.Easy, Halobellum.Economy.AiDifficulty.Normal, Halobellum.Economy.AiDifficulty.Hard };
        foreach (var a in levels) foreach (var b in levels)
        {
            int winsA = 0, roundsTotal = 0, unfinished = 0;
            var results = new (int winner, int rounds)[matches];
            Parallel.For(0, matches, m =>
            {
                var enemy = new Halobellum.Economy.RuleBasedAi(b, 1000 + m, table);
                var player = new Halobellum.Economy.RuleBasedAi(a, 5000 + m, table);
                var match = new Halobellum.Economy.Match(rules, combat, factions, 100 + m, enemy);
                match.ChooseDraft(player.ChooseDraftCard(new Halobellum.Economy.AiView(match, Team.A), match.PlayerDraft));
                int rounds = 0;
                while (match.Phase != Halobellum.Economy.MatchPhase.MatchOver && rounds < 60)
                {
                    if (match.Phase == Halobellum.Economy.MatchPhase.Cards)
                    {
                        int choice = player.ChooseCard(new Halobellum.Economy.AiView(match, Team.A), match.Player.Offer);
                        if (choice < 0 || !match.ChooseCard(Team.A, choice).Ok) match.SkipCards(Team.A);
                    }
                    player.Deploy(new Halobellum.Economy.AiView(match, Team.A));
                    match.StartBattle().Run();
                    match.FinishBattle();
                    if (match.Phase == Halobellum.Economy.MatchPhase.Result) match.ContinueAfterResult();
                    rounds++;
                }
                results[m] = (match.Winner == Team.A ? 1 : match.Winner == Team.B ? 2 : 0, rounds);
                lock (Armies)
                {
                    Add("subs", match.Player.SubFaction.Name);
                    Add("subs", match.Enemy.SubFaction.Name);
                    if (match.Winner == Team.A) Add("wins", match.Player.SubFaction.Name);
                    if (match.Winner == Team.B) Add("wins", match.Enemy.SubFaction.Name);
                    foreach (var side in new[] { match.Player, match.Enemy })
                    {
                        foreach (var sq in side.Squads) if (side.SubFaction.ExclusiveUnits.Contains(sq.Type)) Add("exclusive squads", sq.Type.Name);
                    }
                    foreach (var sq in match.Player.Squads) Add(a + "", sq.Type.Name);
                    foreach (var sq in match.Enemy.Squads) Add(b + "", sq.Type.Name);
                }
            });
            foreach (var r in results) { if (r.winner == 1) winsA++; if (r.winner == 0) unfinished++; roundsTotal += r.rounds; }
            Console.WriteLine($"{a,-6} vs {b,-6}: first wins {winsA}/{matches}, unfinished {unfinished}, avg rounds {roundsTotal / (float)matches:0.0}");
        }
        foreach (var kv in Armies) Console.WriteLine(kv.Key + ": " + string.Join(", ", kv.Value.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}")));
        return 0;
    }

    static UnitStats Build(JsonElement u)
    {
        var s = new UnitStats
        {
            Name = u.GetProperty("name").GetString(),
            Role = (UnitRole)Role[u.GetProperty("role").GetString()],
            Tier = u.GetProperty("tier").GetInt32(),
            UnlockCost = u.GetProperty("unlock").GetInt32(),
            SquadSize = u.GetProperty("squad").GetInt32(),
            Cost = u.GetProperty("cost").GetInt32(),
            MaxHealth = u.GetProperty("hp").GetSingle(),
            ArmorType = (ArmorType)"ULH".IndexOf(u.GetProperty("armor").GetString()[0]),
            MaxShield = u.GetProperty("shield").GetSingle(),
            ShieldRegenPerSecond = u.GetProperty("regen").GetSingle(),
            MoveSpeed = u.GetProperty("speed").GetSingle(),
            CollisionRadius = u.GetProperty("radius").GetSingle(),
            TurnTime180 = u.GetProperty("turn").GetSingle(),
            IsAir = u.GetProperty("air").GetBoolean(),
            TargetFilter = (TargetFilter)"GAB".IndexOf(u.GetProperty("filter").GetString()[0]),
        };
        s.BaseDamage = u.GetProperty("basedmg").ValueKind == JsonValueKind.Number ? u.GetProperty("basedmg").GetSingle() : s.Cost / (float)s.SquadSize / 5f;
        var weapons = new List<WeaponStats>();
        foreach (JsonElement w in u.GetProperty("weapons").EnumerateArray())
        {
            var own = w.GetProperty("own");
            weapons.Add(new WeaponStats
            {
                Name = w.GetProperty("name").GetString(),
                DamagePerShot = w.GetProperty("dmg").GetSingle(),
                FireInterval = w.GetProperty("interval").GetSingle(),
                Range = w.GetProperty("range").GetSingle(),
                MinRange = w.GetProperty("minr").GetSingle(),
                ProjectileSpeed = w.GetProperty("proj").GetSingle(),
                SplashRadius = w.GetProperty("splash").GetSingle(),
                DamageType = (DamageType)Dtype[w.GetProperty("type").GetString()],
                IsAntiArmor = w.GetProperty("aa").GetBoolean(),
                CanBeBlocked = w.GetProperty("block").GetBoolean(),
                HasOwnTargetFilter = own.ValueKind == JsonValueKind.String,
                TargetFilter = own.ValueKind == JsonValueKind.String ? (TargetFilter)"GAB".IndexOf(own.GetString()[0]) : TargetFilter.Ground,
                AirDamageMultiplier = w.GetProperty("air").GetSingle(),
                ConeDegrees = w.GetProperty("cone").GetSingle(),
                Lobbed = w.GetProperty("lob").GetBoolean(),
                BeamSeconds = w.GetProperty("beam").GetSingle(),
                BeamChargeSeconds = w.GetProperty("charge").GetSingle(),
                BeamRestSeconds = w.GetProperty("rest").GetSingle(),
                BeamSweepSpeed = w.GetProperty("sweep").GetSingle(),
                SeedSeconds = w.GetProperty("seed").GetSingle(),
                SeedDamageSharePerSecond = w.GetProperty("seedshare").GetSingle(),
                SeedSlowShare = w.GetProperty("seedslow").GetSingle(),
                HitSlowSeconds = w.GetProperty("slow").GetSingle(),
                HitSlowShare = w.GetProperty("slowshare").GetSingle(),
                WindUpSeconds = w.TryGetProperty("windup", out JsonElement windUp) ? windUp.GetSingle() : 0f,
            });
        }
        s.Weapons = weapons.ToArray();
        var arc = u.GetProperty("arc");
        if (arc.ValueKind == JsonValueKind.Object)
        {
            s.DefenseArc = new DefenseArcStats
            {
                ArcDegrees = arc.GetProperty("deg").GetSingle(),
                FrontDamageMultiplier = arc.GetProperty("front").GetSingle(),
                RearDamageMultiplier = arc.GetProperty("rear").GetSingle(),
                BlockChance = arc.GetProperty("block").GetSingle(),
                BlockedDamageTypes = arc.GetProperty("types").EnumerateArray().Select(t => (DamageType)Dtype[t.GetString()]).ToArray(),
            };
        }
        return s;
    }
}
