// The Hard AI tournament: two Hard AIs play whole matches under the game's real rules, for every pairing of sub-factions, half of them with the sides
// swapped. It loads what TournamentDataExport wrote from the game's assets, and writes one line per match to a results file as soon as the match ends,
// so that a run stopped by a power cut carries on where it was when started again. report.py turns that file into the text report.
// Usage: dotnet run -c Release -- <game_data.json> <matrix.csv> <results.jsonl> [--matches 100] [--trial trial.txt] [--only "Sub A,Sub B"] [--threads N]
//                                  [--setup mode=skirmish,hp=10000,cards=only] [--most-rounds 60]
// Use one results file per trial and setup: what it carries on from is only the pairing and the match number, so a file would mix them without a word.
// --setup goes through the game's own MatchSetup; hp is the base health, and cards is all, no or only (reinforcement cards). With mode=skirmish the field is the
// one made-up faction of all units, and its "sub-factions" are the AI difficulties Hard, Normal and Easy; that needs an export made since Unlock Skirmish came in.
// In Faction mode every side is a Hard AI.
// A trial file tries balance changes without touching the game, one per line:
//   Flood Abomination|MaxHealth=6000            a field of the unit
//   Flood Abomination|Tendril Slam|DamagePerShot=300     a field of one of its weapons
//   rules|TowerBaseHealth=150                   a field of the match rules
//   rules|AiCarrierPlacement=FlankFarCorners    an enum goes by its name
//   without|Armored Division|Pelican            a sub-faction can't have this base unit
// (# starts a comment.)
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Match = Halobellum.Economy.Match;

static class Program
{
    /// <summary>A match with no winner after this many rounds is called off.</summary>
    static int MostRounds = 60;
    // --seed N plays a different set of matches with the same data, to check a result is not luck; 0 keeps the old matches.
    static int SeedOffset = 0;

    static int Main(string[] args)
    {
        string dataPath = args[0], matrixPath = args[1], resultsPath = args[2];
        int matches = int.Parse(Option(args, "--matches") ?? "100");
        int threads = int.Parse(Option(args, "--threads") ?? Math.Max(1, Environment.ProcessorCount - 2).ToString());
        MostRounds = int.Parse(Option(args, "--most-rounds") ?? MostRounds.ToString());
        SeedOffset = int.Parse(Option(args, "--seed") ?? "0");

        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(dataPath));
        var reader = new GraphReader();
        var factions = (List<FactionInfo>)reader.Read(doc.RootElement.GetProperty("factions"), typeof(List<FactionInfo>));
        var rules = (MatchRules)reader.Read(doc.RootElement.GetProperty("rules"), typeof(MatchRules));
        var combat = (CombatRules)reader.Read(doc.RootElement.GetProperty("combat"), typeof(CombatRules));
        string trial = Option(args, "--trial");
        if (trial != null) ApplyTrial(trial, factions, rules);
        string setup = Option(args, "--setup");
        if (setup != null) ParseSetup(setup).ApplyTo(rules);
        if (rules.Mode == MatchMode.UnlockSkirmish)
        {
            // The rules are read without their initial values, so an older export would quietly play with these at 0.
            if (rules.AiSkirmishFlankBonus == 0f && rules.AiSkirmishUnlockMargin == 0f) throw new InvalidOperationException($"{dataPath} is older than Unlock Skirmish: export it again to a new file.");
            factions = new List<FactionInfo> { SkirmishField(factions) };
        }
        var wins = LoadMatrix(matrixPath);

        var subs = new List<(FactionInfo faction, SubFactionInfo sub)>();
        foreach (FactionInfo faction in factions) foreach (SubFactionInfo sub in faction.SubFactions) subs.Add((faction, sub));
        subs = subs.OrderBy(s => s.faction.Name, StringComparer.Ordinal).ThenBy(s => s.sub.Name, StringComparer.Ordinal).ToList();

        string only = Option(args, "--only");
        HashSet<string> onlyNames = only != null ? new HashSet<string>(only.Split(',')) : null;

        var work = new List<(int a, int b, int i)>();
        var done = LoadDone(resultsPath);
        for (int a = 0; a < subs.Count; a++)
        {
            for (int b = a; b < subs.Count; b++)
            {
                if (onlyNames != null && !onlyNames.Contains(subs[a].sub.Name) && !onlyNames.Contains(subs[b].sub.Name)) continue;
                for (int i = 0; i < matches; i++)
                {
                    if (!done.Contains((subs[a].sub.Name, subs[b].sub.Name, i))) work.Add((a, b, i));
                }
            }
        }
        Console.WriteLine($"{subs.Count} sub-factions, {matches} matches a pairing: {work.Count} to play, {done.Count} already done, on {threads} threads.");

        var clock = System.Diagnostics.Stopwatch.StartNew();
        int finished = 0;
        using var results = new StreamWriter(resultsPath, append: true, new UTF8Encoding(false));
        Parallel.ForEach(work, new ParallelOptions { MaxDegreeOfParallelism = threads }, job =>
        {
            string line = PlayMatch(subs[job.a], subs[job.b], job.i, rules, combat, wins);
            lock (results)
            {
                results.WriteLine(line);
                results.Flush();
                finished++;
                if (finished % 100 == 0) Console.WriteLine($"{finished} of {work.Count}, {clock.Elapsed.TotalMinutes:0.0} minutes");
            }
        });
        Console.WriteLine($"Finished {work.Count} matches in {clock.Elapsed.TotalMinutes:0.0} minutes.");
        return 0;
    }

    static string Option(string[] args, string name)
    {
        int at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    static MatchSetup ParseSetup(string text)
    {
        var setup = new MatchSetup();
        foreach (string part in text.Split(','))
        {
            string[] pair = part.Split('=');
            switch (pair[0] + "=" + (pair[0] == "hp" ? "" : pair[1]))
            {
                case "mode=skirmish": setup.Mode = MatchMode.UnlockSkirmish; break;
                case "mode=faction": setup.Mode = MatchMode.Faction; break;
                case "hp=": setup.BaseHealth = int.Parse(pair[1]); break;
                case "cards=all": setup.Cards = CardFilter.AllCards; break;
                case "cards=no": setup.Cards = CardFilter.NoReinforcements; break;
                case "cards=only": setup.Cards = CardFilter.OnlyReinforcements; break;
                default: throw new InvalidOperationException($"No such setup: {part}");
            }
        }
        return setup;
    }

    /// <summary>Unlock Skirmish's one faction, the same as the game builds, with the AI difficulties in place of its one sub-faction. They have no units of their own.</summary>
    static FactionInfo SkirmishField(List<FactionInfo> factions)
    {
        FactionInfo field = SkirmishFaction.Build(factions);
        field.SubFactions = new List<SubFactionInfo>();
        foreach (AiDifficulty difficulty in Enum.GetValues(typeof(AiDifficulty))) field.SubFactions.Add(new SubFactionInfo { Name = difficulty.ToString() });
        return field;
    }

    /// <summary>Who plays a side: in Unlock Skirmish its name says, and in Faction mode it is always Hard.</summary>
    static AiDifficulty DifficultyOf(SubFactionInfo sub, MatchRules rules) =>
        rules.Mode == MatchMode.UnlockSkirmish ? (AiDifficulty)Enum.Parse(typeof(AiDifficulty), sub.Name) : AiDifficulty.Hard;

    static HashSet<(string, string, int)> LoadDone(string path)
    {
        var done = new HashSet<(string, string, int)>();
        if (!File.Exists(path)) return done;
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            using JsonDocument row = JsonDocument.Parse(line);
            done.Add((row.RootElement.GetProperty("a").GetString(), row.RootElement.GetProperty("b").GetString(), row.RootElement.GetProperty("i").GetInt32()));
        }
        return done;
    }

    /// <summary>The table the Hard AI counters with, as the game has it. Each match builds its own, as the table keeps a cache as it is read.</summary>
    static List<(string, string, float)> LoadMatrix(string path)
    {
        var entries = new List<(string, string, float)>();
        string[] lines = File.ReadAllLines(path);
        string[] columns = Regex.Split(lines[0], ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
        foreach (string line in lines.Skip(1))
        {
            string[] cells = Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
            string row = cells[0].Trim('"');
            for (int j = 1; j < cells.Length; j++)
            {
                if (cells[j] != "") entries.Add((row, columns[j].Trim('"'), float.Parse(cells[j], CultureInfo.InvariantCulture)));
            }
        }
        return entries;
    }

    static void ApplyTrial(string path, List<FactionInfo> factions, MatchRules rules)
    {
        var units = new Dictionary<string, UnitStats>();
        foreach (FactionInfo faction in factions)
        {
            foreach (UnitStats unit in faction.BaseRoster) units[unit.Name] = unit;
            foreach (SubFactionInfo sub in faction.SubFactions) foreach (UnitStats unit in sub.ExclusiveUnits) units[unit.Name] = unit;
        }
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Split('#')[0].Trim();
            if (line.Length == 0) continue;
            string[] parts = line.Split('|');
            if (parts[0] == "without")
            {
                SubFactionInfo sub = factions.SelectMany(f => f.SubFactions).First(s => s.Name == parts[1]);
                (sub.ExcludedUnits ??= new List<UnitStats>()).Add(units[parts[2]]);
                Console.WriteLine("Trial: " + line);
                continue;
            }
            string[] assignment = parts[parts.Length - 1].Split('=');
            object target;
            if (parts[0] == "rules") target = rules;
            else
            {
                if (!units.TryGetValue(parts[0], out UnitStats unit)) throw new InvalidOperationException($"No such unit: {parts[0]}");
                target = parts.Length == 2 ? unit : (object)unit.Weapons.First(w => w.Name == parts[1]);
            }
            FieldInfo field = target.GetType().GetField(assignment[0], BindingFlags.Instance | BindingFlags.Public);
            if (field == null) throw new InvalidOperationException($"No such field: {line}");
            field.SetValue(target, field.FieldType.IsEnum ? Enum.Parse(field.FieldType, assignment[1]) : Convert.ChangeType(assignment[1], field.FieldType, CultureInfo.InvariantCulture));
            Console.WriteLine("Trial: " + line);
        }
    }

    /// <summary>Records the cards an AI takes, around the real AI, which decides everything.</summary>
    sealed class RecordingAi : IMatchAi
    {
        readonly RuleBasedAi _ai;
        public readonly List<string> Taken = new List<string>();
        public int Skipped;
        public int RapidResupplies => _ai.RapidResupplies;

        public RecordingAi(RuleBasedAi ai) { _ai = ai; }
        public int ChooseDraftCard(AiView view, IReadOnlyList<DraftCard> cards) => _ai.ChooseDraftCard(view, cards);
        public void Deploy(AiView view) => _ai.Deploy(view);
        public string Label => _ai.Label;

        public int ChooseCard(AiView view, CardOffer offer)
        {
            int choice = _ai.ChooseCard(view, offer);
            if (choice >= 0 && choice < offer.Count && offer.PriceOf(choice) <= view.Self.Credits) Taken.Add(offer.NameOf(choice));
            else Skipped++;
            return choice;
        }
    }

    sealed class UnitTally
    {
        public int SquadRounds, Kills, EndSquads, EndLevels, CreditRounds;
        public float Damage;
    }

    static string PlayMatch((FactionInfo faction, SubFactionInfo sub) a, (FactionInfo faction, SubFactionInfo sub) b, int i, MatchRules rules, CombatRules combat, List<(string, string, float)> wins)
    {
        int seed = unchecked(StableHash(a.sub.Name) * 31 + StableHash(b.sub.Name) * 17 + i * 7919 + SeedOffset * 1000003);
        // Half of the matches with the sides swapped. The sub-faction on Team A is "a" on even matches.
        bool aFirst = i % 2 == 0;
        var teamA = aFirst ? a : b;
        var teamB = aFirst ? b : a;

        var table = new MatchupTable();
        foreach ((string row, string column, float rate) in wins) table.Set(row, column, rate);
        var aiA = new RecordingAi(new RuleBasedAi(DifficultyOf(teamA.sub, rules), unchecked(seed + 1), table));
        var aiB = new RecordingAi(new RuleBasedAi(DifficultyOf(teamB.sub, rules), unchecked(seed + 2), table));
        var match = new Match(rules, combat, new List<FactionInfo> { teamA.faction, teamB.faction }, seed, aiB);
        var random = new SimRandom(unchecked(seed + 3));
        SetCard(match.PlayerDraft, teamA, rules, random);
        SetCard(match.EnemyDraft, teamB, rules, random);
        int valueA = match.PlayerDraft[0].Value, valueB = match.EnemyDraft[0].Value;
        string openingA = string.Join(" + ", match.PlayerDraft[0].Squads.Select(s => s.Name)), openingB = string.Join(" + ", match.EnemyDraft[0].Squads.Select(s => s.Name));
        match.ChooseDraft(0);

        var tallyA = new Dictionary<string, UnitTally>();
        var tallyB = new Dictionary<string, UnitTally>();
        int startA = match.Player.BaseHealth, startB = match.Enemy.BaseHealth;
        int rounds = 0, firstBattle = 0;
        int[] carrierBattles = new int[2], carrierDrops = new int[2];
        while (match.Phase != MatchPhase.MatchOver && rounds < MostRounds)
        {
            if (match.Phase == MatchPhase.Cards)
            {
                int choice = aiA.ChooseCard(new AiView(match, Team.A), match.Player.Offer);
                if (choice < 0 || !match.ChooseCard(Team.A, choice).Ok) match.SkipCards(Team.A);
            }
            aiA.Deploy(new AiView(match, Team.A));
            CountArmy(match.Player, tallyA);
            CountArmy(match.Enemy, tallyB);
            BattleSimulation battle = match.StartBattle();
            battle.Run();
            foreach (SimUnit unit in battle.Units)
            {
                if (unit.Stats.IsTower) continue;
                UnitTally tally = Tally(unit.Team == Team.A ? tallyA : tallyB, unit.Stats.Name);
                tally.Damage += unit.DamageDealt;
                tally.Kills += unit.Kills;
                if (unit.Stats.HasReinforcement && unit.Stats.ReinforcementLandSeconds > 0f)
                {
                    int side = unit.Team == Team.A ? 0 : 1;
                    carrierBattles[side]++;
                    carrierDrops[side] += unit.DropsMade;
                }
            }
            RoundSummary summary = match.FinishBattle();
            if (rounds == 0) firstBattle = summary.BaseDamageByA > summary.BaseDamageByB ? 1 : summary.BaseDamageByB > summary.BaseDamageByA ? 2 : 0;
            if (match.Phase == MatchPhase.Result) match.ContinueAfterResult();
            rounds++;
        }
        foreach (PlacedSquad squad in match.Player.Squads) { UnitTally t = Tally(tallyA, squad.Type.Name); t.EndSquads++; t.EndLevels += squad.Level; }
        foreach (PlacedSquad squad in match.Enemy.Squads) { UnitTally t = Tally(tallyB, squad.Type.Name); t.EndSquads++; t.EndLevels += squad.Level; }

        var text = new StringBuilder();
        var w = new Utf8JsonWriterHelper(text);
        text.Append('{');
        w.Str("a", a.sub.Name); w.Str("b", b.sub.Name); w.Str("fa", a.faction.Name); w.Str("fb", b.faction.Name);
        w.Num("i", i);
        string winner = match.Winner == null ? "none" : (match.Winner == Team.A) == aFirst ? "a" : "b";
        w.Str("winner", winner);
        w.Str("winnerTeam", match.Winner == null ? "none" : match.Winner == Team.A ? "A" : "B");
        w.Num("aOnTeam", aFirst ? 1 : 2);
        w.Num("rounds", rounds);
        w.Num("mostRounds", MostRounds);
        w.Num("firstBattle", firstBattle);
        text.Append("\"sideA\":");
        Side(text, match, match.Player, aiA, tallyA, valueA, startA, openingA, carrierBattles[0], carrierDrops[0]);
        text.Append(",\"sideB\":");
        Side(text, match, match.Enemy, aiB, tallyB, valueB, startB, openingB, carrierBattles[1], carrierDrops[1]);
        text.Append('}');
        return text.ToString();
    }

    static void SetCard(List<DraftCard> draft, (FactionInfo faction, SubFactionInfo sub) side, MatchRules rules, SimRandom random)
    {
        var alone = new FactionInfo { Name = side.faction.Name, BaseRoster = side.faction.BaseRoster, SubFactions = new List<SubFactionInfo> { side.sub }, Cards = side.faction.Cards };
        var one = new MatchRules { Mode = rules.Mode, DraftCardCount = 1, DraftSquadsPerCard = rules.DraftSquadsPerCard, DraftCardValue = rules.DraftCardValue, DraftCardValueTolerance = rules.DraftCardValueTolerance };
        DraftCard card = StartingDraft.Draw(new List<FactionInfo> { alone }, one, random)[0];
        card.Faction = side.faction;
        draft.Clear();
        draft.Add(card);
    }

    static UnitTally Tally(Dictionary<string, UnitTally> tallies, string name)
    {
        if (!tallies.TryGetValue(name, out UnitTally tally)) tallies[name] = tally = new UnitTally();
        return tally;
    }

    static void CountArmy(PlayerState side, Dictionary<string, UnitTally> tallies)
    {
        foreach (PlacedSquad squad in side.Squads)
        {
            UnitTally tally = Tally(tallies, squad.Type.Name);
            tally.SquadRounds++;
            tally.CreditRounds += squad.Type.Cost;
        }
    }

    static void Side(StringBuilder text, Match match, PlayerState side, RecordingAi ai, Dictionary<string, UnitTally> tallies, int value, int start, string opening, int carrierBattles, int carrierDrops)
    {
        var w = new Utf8JsonWriterHelper(text);
        text.Append('{');
        w.Num("value", value); w.Num("start", start); w.Num("end", side.BaseHealth); w.Num("credits", side.Credits);
        w.Str("opening", opening);
        w.Num("spells", side.Spells.Count);
        w.Num("research", side.ResearchHealthSteps + side.ResearchDamageSteps);
        w.Num("skipped", ai.Skipped);
        w.Num("carrierBattles", carrierBattles);
        w.Num("carrierDrops", carrierDrops);
        w.Num("resupplies", ai.RapidResupplies);
        w.Num("unlocked", side.Unlocked.Count);
        w.Num("flankSquads", side.Squads.Count(squad => DeploymentGrid.IsInFlankZone(squad, match.CombatRules)));
        text.Append("\"towers\":[").Append(string.Join(",", side.Towers.Select(t => $"[{t.Level},{t.Falls}]"))).Append("],");
        text.Append("\"cards\":[").Append(string.Join(",", ai.Taken.Select(c => JsonSerializer.Serialize(c)))).Append("],");
        text.Append("\"units\":{");
        text.Append(string.Join(",", tallies.Select(kv => $"{JsonSerializer.Serialize(kv.Key)}:[{kv.Value.SquadRounds},{kv.Value.CreditRounds},{kv.Value.Damage.ToString("0", CultureInfo.InvariantCulture)},{kv.Value.Kills},{kv.Value.EndSquads},{kv.Value.EndLevels}]")));
        text.Append("}}");
    }

    static int StableHash(string text)
    {
        int hash = 17;
        foreach (char c in text) hash = unchecked(hash * 31 + c);
        return hash;
    }

    sealed class Utf8JsonWriterHelper
    {
        readonly StringBuilder _text;
        public Utf8JsonWriterHelper(StringBuilder text) { _text = text; }
        public void Str(string name, string value) => _text.Append(JsonSerializer.Serialize(name)).Append(':').Append(JsonSerializer.Serialize(value)).Append(',');
        public void Num(string name, int value) => _text.Append(JsonSerializer.Serialize(name)).Append(':').Append(value).Append(',');
    }

    /// <summary>Puts the objects of the export back together, every field of them, shared objects shared.</summary>
    sealed class GraphReader
    {
        readonly Dictionary<int, object> _objects = new Dictionary<int, object>();

        public object Read(JsonElement json, Type expected)
        {
            Type plain = Nullable.GetUnderlyingType(expected) ?? expected;
            switch (json.ValueKind)
            {
                case JsonValueKind.Null: return null;
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.String:
                    if (plain == typeof(float)) return float.Parse(json.GetString(), CultureInfo.InvariantCulture);
                    return json.GetString();
                case JsonValueKind.Number:
                    if (plain.IsEnum) return Enum.ToObject(plain, json.GetInt64());
                    return Convert.ChangeType(json.GetDouble(), plain == typeof(object) ? typeof(double) : plain, CultureInfo.InvariantCulture);
            }

            if (json.TryGetProperty("$r", out JsonElement reference)) return _objects[reference.GetInt32()];
            string typeName = json.GetProperty("$t").GetString();
            int id = json.TryGetProperty("$i", out JsonElement number) ? number.GetInt32() : -1;

            if (json.TryGetProperty("$g", out JsonElement dims))
            {
                Type element = FindType(typeName.Substring(0, typeName.Length - 2));
                var grid = Array.CreateInstance(element, dims[0].GetInt32(), dims[1].GetInt32());
                if (id >= 0) _objects[id] = grid;
                int at = 0;
                foreach (JsonElement cell in json.GetProperty("$l").EnumerateArray())
                {
                    grid.SetValue(Read(cell, element), at / grid.GetLength(1), at % grid.GetLength(1));
                    at++;
                }
                return grid;
            }
            if (typeName.EndsWith("[]", StringComparison.Ordinal))
            {
                Type element = FindType(typeName.Substring(0, typeName.Length - 2));
                JsonElement items = json.GetProperty("$l");
                var array = Array.CreateInstance(element, items.GetArrayLength());
                if (id >= 0) _objects[id] = array;
                int at = 0;
                foreach (JsonElement item in items.EnumerateArray()) array.SetValue(Read(item, element), at++);
                return array;
            }
            if (typeName.StartsWith("List:", StringComparison.Ordinal))
            {
                Type element = FindType(typeName.Substring(5));
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element));
                if (id >= 0) _objects[id] = list;
                foreach (JsonElement item in json.GetProperty("$l").EnumerateArray()) list.Add(Read(item, element));
                return list;
            }

            Type type = FindType(typeName);
            object made = RuntimeHelpers.GetUninitializedObject(type);
            if (id >= 0) _objects[id] = made;
            foreach (JsonProperty property in json.GetProperty("f").EnumerateObject())
            {
                FieldInfo field = FindField(type, property.Name);
                field.SetValue(made, Read(property.Value, field.FieldType));
            }
            return made;
        }

        static Type FindType(string name)
        {
            Type type = Type.GetType(name) ?? typeof(Program).Assembly.GetType(name);
            if (type == null) throw new InvalidOperationException($"No such type: {name}");
            return type;
        }

        static FieldInfo FindField(Type type, string name)
        {
            for (Type at = type; at != null; at = at.BaseType)
            {
                FieldInfo field = at.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new InvalidOperationException($"{type} has no field {name}");
        }
    }
}
