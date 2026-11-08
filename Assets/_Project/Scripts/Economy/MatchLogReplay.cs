// MatchLogReplay.cs
// Plays a battle log (D68) again (D106): the log's seed, mode, difficulty and starting card give the same match, and the player's actions are
// done again in their order, so that the AI, which only ever sees what the player could see, answers as it did. Nothing in a log is trusted
// blindly: at every battle start both armies are compared with the log's (each squad's id, unit, place, turn and level, and the credits), and an
// action that now works differently, or a battle that ends otherwise, is noted. A log from before a balance change can drift; the notes say where.
// Read reads the lines. Apply does one action through an IReplayHands: MatchHands works on a Match directly (PlayHeadless, the tests), and the
// Match scene's hands go through its MatchController, as the player's clicks did (Presentation's MatchReplay, with pictures).
// Only what the player did is replayed; refused actions changed nothing and are skipped. Docs/battle_log.md says how to start one.
// Depends on: Match, MatchLog (the format), PlayerState, Halobellum.Core, Halobellum.Combat.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RegexMatch = System.Text.RegularExpressions.Match;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    /// <summary>Does the player's actions on a match: on the Match itself, or through the Match scene as the player's clicks did.</summary>
    public interface IReplayHands
    {
        Match Match { get; }
        ActionResult ChooseDraft(int index);
        ActionResult ChooseCard(int index);
        ActionResult SkipCards();
        ActionResult Unlock(UnitStats type);
        ActionResult Buy(UnitStats type);
        ActionResult Move(PlacedSquad squad, int column, int row, bool turned);
        ActionResult Turn(PlacedSquad squad);
        ActionResult Sell(PlacedSquad squad);
        ActionResult LevelUp(PlacedSquad squad);
        ActionResult LevelUpAll(UnitStats type);
        ActionResult BuyTech(UnitStats type, TechUpgrade tech);
        ActionResult LevelUpTower(TowerKind kind);
        ActionResult BuyExtraSlot();
        ActionResult BuyOilZone();
        ActionResult RapidResupply();
        ActionResult BuyEliteRecruitment();
        ActionResult BuyResearch(bool health);
        ActionResult PlaceSpell(OwnedSpell spell, Vec2 position, float angleDegrees);
        ActionResult UnplaceSpell(OwnedSpell spell);
        ActionResult PlaceItem(ItemStats item, PlacedSquad squad);
        ActionResult UseTraining(PlacedSquad squad);
        ActionResult Undo();
    }

    /// <summary>One line of a log, with its JSON.</summary>
    public sealed class ReplayLine
    {
        public int Number;
        public string Type;
        public int Round;
        public string Action;
        public bool Ok;
        public string Detail;
        public Dictionary<string, object> Json;
    }

    public sealed class MatchLogReplay
    {
        public int Format;
        public int Seed;
        public string Mode;
        public string Difficulty;
        public int BaseHealthRule;
        public int DraftChosen;
        public readonly List<ReplayLine> Lines = new List<ReplayLine>();

        /// <summary>What drifted from the log, line by line. Empty if the replay matched it all.</summary>
        public readonly List<string> Drift = new List<string>();

        public MatchMode MatchMode => Enum.TryParse(Mode, out MatchMode mode) ? mode : MatchMode.Faction;

        public AiDifficulty AiDifficulty => Enum.TryParse(Difficulty, out AiDifficulty difficulty) ? difficulty : AiDifficulty.Normal;

        /// <summary>The setup the logged match was started with (the card filter isn't in format 1 logs: all cards).</summary>
        public MatchSetup Setup => new MatchSetup { Mode = MatchMode, BaseHealth = BaseHealthRule };

        /// <summary>Reads a log's lines. Throws FormatException if it has no match_start, which is its first line.</summary>
        public static MatchLogReplay Read(IEnumerable<string> lines)
        {
            var replay = new MatchLogReplay();
            int number = 0;
            foreach (string text in lines)
            {
                number++;
                if (string.IsNullOrWhiteSpace(text)) continue;
                Dictionary<string, object> json = LogJson.Object(text);
                var line = new ReplayLine
                {
                    Number = number, Json = json, Type = LogJson.Text(json, "type"), Round = LogJson.Int(json, "round"),
                    Action = LogJson.Text(json, "action"), Ok = json.TryGetValue("ok", out object ok) && ok is bool b && b, Detail = LogJson.Text(json, "detail"),
                };
                if (line.Type == "match_start")
                {
                    replay.Format = LogJson.Int(json, "format");
                    replay.Seed = LogJson.Int(json, "seed");
                    replay.Mode = LogJson.Text(json, "mode");
                    replay.Difficulty = LogJson.Text(json, "difficulty");
                    replay.BaseHealthRule = LogJson.Int(json, "rule_base_health");
                    replay.DraftChosen = LogJson.Int(json, "draft_chosen");
                }
                replay.Lines.Add(line);
            }
            if (replay.Lines.Count == 0 || replay.Lines[0].Type != "match_start") throw new FormatException("A battle log starts with its match_start line.");
            return replay;
        }

        // ------------------------------------------------------------------ the player's actions

        private static readonly Regex SquadId = new Regex(@"#(\d+)", RegexOptions.CultureInvariant);
        private static readonly Regex MoveDetail = new Regex(@"#(\d+) to column (-?\d+), row (-?\d+), turned (True|False)$", RegexOptions.CultureInvariant);
        private static readonly Regex SpellDetail = new Regex(@"^(.*) at (-?[\d.]+), (-?[\d.]+), angle (-?[\d.]+)$", RegexOptions.CultureInvariant);
        private static readonly Regex ItemDetail = new Regex(@"^(.*) on .* #(\d+)$", RegexOptions.CultureInvariant);

        /// <summary>True for the lines Apply does: actions that worked when they were logged.</summary>
        public static bool IsReplayed(ReplayLine line) => line.Type == "action" && line.Ok;

        /// <summary>Does one logged action again. Fails, saying why, if what it names isn't there now.</summary>
        public static ActionResult Apply(IReplayHands hands, ReplayLine line)
        {
            Match match = hands.Match;
            PlayerState player = match.Player;
            string detail = line.Detail ?? "";
            switch (line.Action)
            {
                case "Unlock": return WithUnit(player, detail, hands.Unlock);
                case "Buy": return WithUnit(player, detail, hands.Buy);
                case "LevelUpAll": return WithUnit(player, detail, hands.LevelUpAll);
                case "Sell": return WithSquad(player, detail, hands.Sell);
                case "LevelUp": return WithSquad(player, detail, hands.LevelUp);
                case "UseTraining": return WithSquad(player, detail, hands.UseTraining);
                case "ToggleRotation": return WithSquad(player, detail, hands.Turn);
                case "PlaceAt":
                {
                    RegexMatch m = MoveDetail.Match(detail);
                    if (!m.Success) return Unreadable(line);
                    PlacedSquad squad = player.FindSquad(Int(m.Groups[1].Value));
                    if (squad == null) return ActionResult.Fail($"No squad #{m.Groups[1].Value} now.");
                    return hands.Move(squad, Int(m.Groups[2].Value), Int(m.Groups[3].Value), m.Groups[4].Value == "True");
                }
                case "BuyTech":
                {
                    int colon = detail.IndexOf(": ", StringComparison.Ordinal);
                    if (colon < 0) return Unreadable(line);
                    UnitStats type = FindUnit(player, detail.Substring(0, colon));
                    string techName = detail.Substring(colon + 2);
                    TechUpgrade tech = type?.TechUpgrades == null ? null : Array.Find(type.TechUpgrades, t => t != null && t.Name == techName);
                    if (tech == null) return ActionResult.Fail($"No tech {detail} now.");
                    return hands.BuyTech(type, tech);
                }
                case "LevelUpTower": return Enum.TryParse(detail, out TowerKind kind) ? hands.LevelUpTower(kind) : Unreadable(line);
                case "BuyExtraSlot": return hands.BuyExtraSlot();
                case "BuyOilZone": return hands.BuyOilZone();
                case "RapidResupply": return hands.RapidResupply();
                case "BuyEliteRecruitment": return hands.BuyEliteRecruitment();
                case "BuyResearch": return hands.BuyResearch(detail == "health");
                case "ChooseCard":
                {
                    CardOffer offer = player.Offer;
                    for (int i = 0; offer != null && i < offer.Count; i++)
                    {
                        if (offer.NameOf(i) == detail) return hands.ChooseCard(i);
                    }
                    return ActionResult.Fail($"{detail} is not offered now.");
                }
                case "SkipCards": return hands.SkipCards();
                case "PlaceSpellAt":
                {
                    RegexMatch m = SpellDetail.Match(detail);
                    if (!m.Success) return Unreadable(line);
                    string name = m.Groups[1].Value;
                    // The log names the spell, not which copy: a ready one not placed yet, or else the placed one being moved.
                    OwnedSpell spell = player.Spells.Find(s => s.Stats.Name == name && s.IsReady(match.Round) && !s.IsPlaced) ?? player.Spells.Find(s => s.Stats.Name == name && s.IsPlaced);
                    if (spell == null) return ActionResult.Fail($"No {name} to place now.");
                    return hands.PlaceSpell(spell, new Vec2(Float(m.Groups[2].Value), Float(m.Groups[3].Value)), Float(m.Groups[4].Value));
                }
                case "UnplaceSpell":
                {
                    OwnedSpell spell = player.Spells.Find(s => s.Stats.Name == detail && s.IsPlaced);
                    return spell == null ? ActionResult.Fail($"No {detail} placed now.") : hands.UnplaceSpell(spell);
                }
                case "PlaceItemOn":
                {
                    RegexMatch m = ItemDetail.Match(detail);
                    if (!m.Success) return Unreadable(line);
                    ItemStats item = player.Inventory.Find(i => i.Name == m.Groups[1].Value);
                    PlacedSquad squad = player.FindSquad(Int(m.Groups[2].Value));
                    if (item == null || squad == null) return ActionResult.Fail($"No {m.Groups[1].Value} or no squad #{m.Groups[2].Value} now.");
                    return hands.PlaceItem(item, squad);
                }
                case "Undo": return hands.Undo();
                default: return ActionResult.Fail($"Line {line.Number}: the action {line.Action} can't be replayed.");
            }
        }

        private static ActionResult Unreadable(ReplayLine line) => ActionResult.Fail($"Line {line.Number}: can't read \"{line.Detail}\".");

        private static UnitStats FindUnit(PlayerState player, string name) => player.Roster.Find(u => u.Name == name);

        private static ActionResult WithUnit(PlayerState player, string name, Func<UnitStats, ActionResult> act)
        {
            UnitStats type = FindUnit(player, name);
            return type == null ? ActionResult.Fail($"No {name} in the roster now.") : act(type);
        }

        private static ActionResult WithSquad(PlayerState player, string detail, Func<PlacedSquad, ActionResult> act)
        {
            RegexMatch m = SquadId.Match(detail);
            if (!m.Success) return ActionResult.Fail($"No squad named in \"{detail}\".");
            PlacedSquad squad = player.FindSquad(Int(m.Groups[1].Value));
            return squad == null ? ActionResult.Fail($"No squad #{m.Groups[1].Value} now.") : act(squad);
        }

        private static int Int(string text) => int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static float Float(string text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ checks

        /// <summary>Where both armies differ from a battle_start line: squads by id, unit, place, turn and level, and the credits. Empty if they match.</summary>
        public static List<string> Differences(Match match, ReplayLine battleStart)
        {
            var differences = new List<string>();
            Compare("player", match.Player, battleStart.Json, differences);
            Compare("enemy", match.Enemy, battleStart.Json, differences);
            return differences;
        }

        private static void Compare(string side, PlayerState state, Dictionary<string, object> line, List<string> differences)
        {
            if (!(line.TryGetValue(side, out object value) && value is Dictionary<string, object> logged)) return;
            int credits = LogJson.Int(logged, "credits");
            if (credits != state.Credits) differences.Add($"{side} credits {state.Credits}, logged {credits}");
            var seen = new HashSet<int>();
            if (logged.TryGetValue("squads", out object list) && list is List<object> squads)
            {
                foreach (object item in squads)
                {
                    if (!(item is Dictionary<string, object> squad)) continue;
                    int id = LogJson.Int(squad, "id");
                    seen.Add(id);
                    string unit = LogJson.Text(squad, "unit");
                    PlacedSquad now = state.FindSquad(id);
                    if (now == null)
                    {
                        differences.Add($"{side} {unit} #{id} missing");
                        continue;
                    }
                    bool turned = squad.TryGetValue("turned", out object t) && t is bool tb && tb;
                    if (now.Type.Name != unit || now.Column != LogJson.Int(squad, "column") || now.Row != LogJson.Int(squad, "row") || now.Rotated90 != turned || now.Level != LogJson.Int(squad, "level"))
                        differences.Add($"{side} #{id}: {Describe(now)}, logged {unit} at {LogJson.Int(squad, "column")},{LogJson.Int(squad, "row")} turned {turned} level {LogJson.Int(squad, "level")}");
                }
            }
            foreach (PlacedSquad squad in state.Squads)
            {
                if (!seen.Contains(squad.Id)) differences.Add($"{side} #{squad.Id}: {Describe(squad)}, not in the log");
            }
        }

        private static string Describe(PlacedSquad squad) => $"{squad.Type.Name} at {squad.Column},{squad.Row} turned {squad.Rotated90} level {squad.Level}";

        /// <summary>Notes how a finished battle differs from its battle_end line (the outcome and both bases).</summary>
        public static List<string> BattleDifferences(Match match, ReplayLine battleEnd)
        {
            var differences = new List<string>();
            RoundSummary summary = match.LastSummary;
            if (summary == null) return differences;
            string outcome = LogJson.Text(battleEnd.Json, "outcome");
            if (outcome != summary.Battle.Outcome.ToString()) differences.Add($"outcome {summary.Battle.Outcome}, logged {outcome}");
            int playerBase = LogJson.Int(battleEnd.Json, "player_base"), enemyBase = LogJson.Int(battleEnd.Json, "enemy_base");
            if (playerBase != summary.BaseHealthA || enemyBase != summary.BaseHealthB)
                differences.Add($"bases {summary.BaseHealthA} and {summary.BaseHealthB}, logged {playerBase} and {enemyBase}");
            return differences;
        }

        /// <summary>Notes a difference, with the line and round it was found at.</summary>
        public void Note(ReplayLine line, string what) => Drift.Add($"line {line.Number}, round {line.Round}: {what}");

        // ------------------------------------------------------------------ playing it without the scene

        /// <summary>
        /// Replays the whole log on a match made from its start (same seed, rules, factions and AI), battles run at once, and returns the drift.
        /// The match must still be in its draft.
        /// </summary>
        public List<string> PlayHeadless(Match match)
        {
            var hands = new MatchHands(match);
            Drift.Clear();
            ActionResult drafted = hands.ChooseDraft(DraftChosen);
            if (!drafted.Ok) Drift.Add($"the starting card {DraftChosen}: {drafted.Message}");
            foreach (ReplayLine line in Lines)
            {
                if (match.Phase == MatchPhase.MatchOver && line.Type != "match_end")
                {
                    Note(line, "the match was already over");
                    break;
                }
                switch (line.Type)
                {
                    case "action":
                        if (!IsReplayed(line)) break;
                        ActionResult result = Apply(hands, line);
                        if (!result.Ok) Note(line, $"{line.Action} {line.Detail}: {result.Message}");
                        break;
                    case "battle_start":
                        foreach (string difference in Differences(match, line)) Note(line, difference);
                        if (match.Phase != MatchPhase.Deployment) { Note(line, $"no battle can start in {match.Phase}"); break; }
                        match.StartBattle().Run();
                        break;
                    case "battle_end":
                        if (match.Phase != MatchPhase.Battle) break;
                        match.FinishBattle();
                        foreach (string difference in BattleDifferences(match, line)) Note(line, difference);
                        if (match.Phase == MatchPhase.Result) match.ContinueAfterResult();
                        break;
                }
            }
            return Drift;
        }
    }

    /// <summary>The player's actions done on the Match itself, each recorded for Undo as the Match scene records them.</summary>
    public sealed class MatchHands : IReplayHands
    {
        public MatchHands(Match match) => Match = match;

        public Match Match { get; }

        private ActionResult Done(ActionResult result)
        {
            if (result.Ok) Match.RecordPlayerAction();
            return result;
        }

        public ActionResult ChooseDraft(int index) => Match.ChooseDraft(index);
        public ActionResult ChooseCard(int index) => Match.ChooseCard(Team.A, index);
        public ActionResult SkipCards() => Match.SkipCards(Team.A);
        public ActionResult Unlock(UnitStats type) => Done(Match.Unlock(Team.A, type));
        public ActionResult Buy(UnitStats type) => Done(Match.Buy(Team.A, type));
        public ActionResult Move(PlacedSquad squad, int column, int row, bool turned) => Done(Match.Move(Team.A, squad.Id, column, row, turned));
        public ActionResult Turn(PlacedSquad squad) => Done(Match.Move(Team.A, squad.Id, squad.Column, squad.Row, !squad.Rotated90));
        public ActionResult Sell(PlacedSquad squad) => Done(Match.Sell(Team.A, squad.Id));
        public ActionResult LevelUp(PlacedSquad squad) => Done(Match.LevelUp(Team.A, squad.Id));
        public ActionResult LevelUpAll(UnitStats type) => Done(Match.LevelUpAll(Team.A, type));
        public ActionResult BuyTech(UnitStats type, TechUpgrade tech) => Done(Match.BuyTech(Team.A, type, tech));
        public ActionResult LevelUpTower(TowerKind kind) => Done(Match.LevelUpTower(Team.A, kind));
        public ActionResult BuyExtraSlot() => Done(Match.BuyExtraSlot(Team.A));
        public ActionResult BuyOilZone() => Done(Match.BuyOilZone(Team.A));
        public ActionResult RapidResupply() => Done(Match.RapidResupply(Team.A));
        public ActionResult BuyEliteRecruitment() => Done(Match.BuyEliteRecruitment(Team.A));
        public ActionResult BuyResearch(bool health) => Done(Match.BuyResearch(Team.A, health));
        public ActionResult PlaceSpell(OwnedSpell spell, Vec2 position, float angleDegrees) => Done(Match.PlaceSpell(Team.A, spell.Id, position, angleDegrees));
        public ActionResult UnplaceSpell(OwnedSpell spell) => Done(Match.UnplaceSpell(Team.A, spell.Id));
        public ActionResult PlaceItem(ItemStats item, PlacedSquad squad) => Done(Match.PlaceItem(Team.A, item, squad.Id));
        public ActionResult UseTraining(PlacedSquad squad) => Done(Match.UseTraining(Team.A, squad.Id));
        public ActionResult Undo() => Match.Undo();
    }

    /// <summary>Just enough JSON to read the battle log's lines back: objects, arrays, strings, numbers, true, false and null.</summary>
    public static class LogJson
    {
        public static Dictionary<string, object> Object(string text)
        {
            int at = 0;
            object value = Value(text, ref at);
            return value as Dictionary<string, object> ?? throw new FormatException("A log line is one JSON object.");
        }

        public static string Text(Dictionary<string, object> json, string name) => json.TryGetValue(name, out object value) ? value as string : null;

        public static int Int(Dictionary<string, object> json, string name) =>
            json.TryGetValue(name, out object value) && value is double number ? (int)Math.Round(number) : 0;

        private static object Value(string s, ref int at)
        {
            Skip(s, ref at);
            if (at >= s.Length) throw new FormatException("The line ends too soon.");
            char c = s[at];
            if (c == '{')
            {
                var obj = new Dictionary<string, object>();
                at++;
                Skip(s, ref at);
                if (s[at] == '}') { at++; return obj; }
                while (true)
                {
                    Skip(s, ref at);
                    string key = String(s, ref at);
                    Skip(s, ref at);
                    Expect(s, ref at, ':');
                    obj[key] = Value(s, ref at);
                    Skip(s, ref at);
                    if (s[at] == ',') { at++; continue; }
                    Expect(s, ref at, '}');
                    return obj;
                }
            }
            if (c == '[')
            {
                var list = new List<object>();
                at++;
                Skip(s, ref at);
                if (s[at] == ']') { at++; return list; }
                while (true)
                {
                    list.Add(Value(s, ref at));
                    Skip(s, ref at);
                    if (s[at] == ',') { at++; continue; }
                    Expect(s, ref at, ']');
                    return list;
                }
            }
            if (c == '"') return String(s, ref at);
            if (Word(s, ref at, "true")) return true;
            if (Word(s, ref at, "false")) return false;
            if (Word(s, ref at, "null")) return null;
            int start = at;
            while (at < s.Length && "+-0123456789.eE".IndexOf(s[at]) >= 0) at++;
            if (start == at) throw new FormatException($"Unexpected '{c}' at {at}.");
            return double.Parse(s.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static string String(string s, ref int at)
        {
            Expect(s, ref at, '"');
            var text = new StringBuilder();
            while (at < s.Length && s[at] != '"')
            {
                char c = s[at++];
                if (c != '\\') { text.Append(c); continue; }
                char e = s[at++];
                switch (e)
                {
                    case 'n': text.Append('\n'); break;
                    case 'r': text.Append('\r'); break;
                    case 't': text.Append('\t'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'u': text.Append((char)int.Parse(s.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)); at += 4; break;
                    default: text.Append(e); break;
                }
            }
            Expect(s, ref at, '"');
            return text.ToString();
        }

        private static bool Word(string s, ref int at, string word)
        {
            if (string.CompareOrdinal(s, at, word, 0, word.Length) != 0) return false;
            at += word.Length;
            return true;
        }

        private static void Skip(string s, ref int at)
        {
            while (at < s.Length && char.IsWhiteSpace(s[at])) at++;
        }

        private static void Expect(string s, ref int at, char c)
        {
            if (at >= s.Length || s[at] != c) throw new FormatException($"Expected '{c}' at {at}.");
            at++;
        }
    }
}
