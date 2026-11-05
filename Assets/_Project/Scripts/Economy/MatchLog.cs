// MatchLog.cs
// The battle log of a game played by a person (D68): one line of JSON for each thing that happened, handed to whoever writes it down
// (Presentation's BattleLogFile). It says what was offered and chosen, what the player did and in what order, both armies as they went into each
// battle, and how each battle went. It only reads the match, so a game plays the same with or without it. Docs/battle_log.md explains each line.
// Nothing reads these files yet. They are kept so that an AI could perhaps learn from them one day.
// Depends on: Match, PlayerState, RoundSummary, Halobellum.Core, Halobellum.Combat.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class MatchLog
    {
        /// <summary>Goes up when a line changes meaning, so that old logs can still be told apart.</summary>
        public const int FormatVersion = 1;

        private readonly Action<string> _write;

        public MatchLog(Action<string> write) => _write = write;

        /// <summary>The first line, once the player has chosen a starting card: enough to know which game this was, and the cards that were on offer.</summary>
        public void Started(Match match, string difficulty, DateTime localTime, int cardChosen)
        {
            var line = new Line("match_start")
                .Number("format", FormatVersion)
                .Text("time", localTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))
                .Number("seed", match.Seed)
                .Text("mode", match.Rules.Mode.ToString())
                .Text("difficulty", difficulty)
                .Number("rule_base_health", match.Rules.BaseHealth)
                .Number("draft_chosen", cardChosen)
                .Raw("draft_offered", List(match.PlayerDraft, DraftCardJson))
                .Raw("player", Side(match.Player, match.Round))
                .Raw("enemy", Side(match.Enemy, match.Round));
            _write(line.ToString());
        }

        /// <summary>The cards this round offers the player.</summary>
        public void CardsOffered(Match match)
        {
            CardOffer offer = match.Player.Offer;
            if (offer == null) return;
            var names = new List<string>();
            for (int i = 0; i < offer.Count; i++) names.Add(new Line().Text("name", offer.NameOf(i)).Number("price", offer.PriceOf(i)).ToString());
            _write(new Line("cards_offered").Number("round", match.Round).Text("kind", offer.Kind.ToString()).Number("credits", match.Player.Credits).Number("skip_bonus", offer.SkipBonus).Raw("cards", "[" + string.Join(",", names) + "]").ToString());
        }

        /// <summary>One thing the player did or tried, with the credits it left. Refused ones are kept too: they say what the player wanted.</summary>
        public void PlayerAction(Match match, string action, string detail, ActionResult result)
        {
            var line = new Line("action").Number("round", match.Round).Text("action", action).Bool("ok", result.Ok).Number("credits", match.Player.Credits);
            if (!string.IsNullOrEmpty(detail)) line.Text("detail", detail);
            if (!result.Ok && !string.IsNullOrEmpty(result.Message)) line.Text("refused", result.Message);
            _write(line.ToString());
        }

        /// <summary>Both sides as they went into this battle: this is the round's whole deployment, with the AI's.</summary>
        public void BattleStarted(Match match)
        {
            _write(new Line("battle_start").Number("round", match.Round).Number("battle_seed", match.BattleSeed).Raw("player", Side(match.Player, match.Round)).Raw("enemy", Side(match.Enemy, match.Round)).ToString());
        }

        /// <summary>How the battle went. Team A is the player.</summary>
        public void BattleFinished(RoundSummary summary)
        {
            BattleResult battle = summary.Battle;
            var line = new Line("battle_end")
                .Number("round", summary.Round)
                .Text("outcome", battle.Outcome.ToString())
                .Text("round_winner", TeamName(summary.RoundWinner))
                .Bool("timed_out", battle.TimedOut)
                .Number("seconds", battle.DurationSeconds)
                .Number("base_damage_by_player", summary.BaseDamageByA)
                .Number("base_damage_by_enemy", summary.BaseDamageByB)
                .Number("player_base", summary.BaseHealthA)
                .Number("enemy_base", summary.BaseHealthB)
                .Bool("tie", summary.Tie)
                .Number("income", summary.IncomeAwarded)
                .Raw("player_army", Army(battle.TeamA))
                .Raw("enemy_army", Army(battle.TeamB))
                .Raw("kills", List(summary.Kills, k => new Line().Text("by", TeamName(k.KillerTeam)).Text("killer", k.KillerType).Text("victim", k.VictimType).Number("count", k.Kills).ToString()));
            _write(line.ToString());
        }

        /// <summary>The last line of a game played to its end. A log without one was left early.</summary>
        public void Finished(Match match)
        {
            _write(new Line("match_end").Number("rounds", match.Round).Text("winner", TeamName(match.Winner)).Number("player_base", match.Player.BaseHealth).Number("enemy_base", match.Enemy.BaseHealth).ToString());
        }

        private static string TeamName(Team? team) => team == null ? "none" : team == Team.A ? "player" : "enemy";

        private static string DraftCardJson(DraftCard card) =>
            new Line().Text("faction", card.Faction?.Name).Text("sub_faction", card.SubFaction?.Name).Number("value", card.Value).Raw("squads", Names(card.Squads.Select(s => s.Name))).ToString();

        /// <summary>Everything one side owns at this moment. The unit names are the game's own, and the columns and rows are each in their owner's frame (0, 0 is its front left).</summary>
        private static string Side(PlayerState side, int round)
        {
            var techs = new Line();
            foreach (var pair in side.Techs.OrderBy(p => p.Key.Name, StringComparer.Ordinal))
            {
                if (pair.Value.Count > 0) techs.Raw(pair.Key.Name, Names(pair.Value.Select(t => t.Name)));
            }
            return new Line()
                .Text("faction", side.Faction?.Name)
                .Text("sub_faction", side.SubFaction?.Name)
                .Number("credits", side.Credits)
                .Number("base", side.BaseHealth)
                .Number("starting_base", side.StartingBaseHealth)
                .Number("bonus_income", side.BonusIncome)
                .Number("research_health_steps", side.ResearchHealthSteps)
                .Number("research_damage_steps", side.ResearchDamageSteps)
                .Raw("unlocked", Names(side.Unlocked.Select(u => u.Name).OrderBy(n => n, StringComparer.Ordinal)))
                .Raw("squads", List(side.Squads, SquadJson))
                .Raw("techs", techs.ToString())
                .Raw("spells", List(side.Spells, s => SpellJson(s, round)))
                .Raw("cards_taken", Names(side.CardsTaken.Select(c => c.Name)))
                .Raw("inventory", Names(side.Inventory.Select(i => i.Name)))
                .Raw("towers", List(side.Towers, t => new Line().Text("kind", t.Kind.ToString()).Number("level", t.Level).Number("falls", t.Falls).ToString()))
                .ToString();
        }

        private static string SquadJson(PlacedSquad squad)
        {
            var line = new Line().Number("id", squad.Id).Text("unit", squad.Type.Name).Number("column", squad.Column).Number("row", squad.Row).Bool("turned", squad.Rotated90)
                .Number("level", squad.Level).Number("xp", squad.Xp).Number("round_bought", squad.RoundBought);
            if (squad.FromDraft) line.Bool("from_draft", true);
            if (squad.FromCard) line.Bool("from_card", true);
            if (squad.Item != null) line.Text("item", squad.Item.Name);
            return line.ToString();
        }

        private static string SpellJson(OwnedSpell spell, int round)
        {
            var line = new Line().Text("name", spell.Stats.Name).Number("rounds_until_ready", spell.RoundsLeft(round));
            if (spell.IsPlaced) line.Number("x", spell.Position.X).Number("y", spell.Position.Y).Number("angle", spell.AngleDegrees);
            return line.ToString();
        }

        private static string Army(TeamResult team)
        {
            return new Line().Number("deployed", team.Deployed).Number("survivors", team.Survivors).Number("damage", team.DamageDealt).Number("kills", team.Kills)
                .Raw("units", List(team.UnitTypes, TypeJson)).Raw("spells", List(team.Spells, TypeJson)).ToString();
        }

        private static string TypeJson(UnitTypeResult type) =>
            new Line().Text("name", type.UnitName).Number("deployed", type.Deployed).Number("survivors", type.Survivors).Number("damage", type.DamageDealt).Number("kills", type.Kills).ToString();

        private static string List<T>(IEnumerable<T> items, Func<T, string> json) => "[" + string.Join(",", items.Select(json)) + "]";

        private static string Names(IEnumerable<string> names) => "[" + string.Join(",", names.Select(Quote)) + "]";

        /// <summary>A string as JSON has it.</summary>
        public static string Quote(string text)
        {
            if (text == null) return "null";
            var quoted = new StringBuilder(text.Length + 2).Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < ' ') quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else quoted.Append(c);
                        break;
                }
            }
            return quoted.Append('"').ToString();
        }

        /// <summary>One JSON object, written field by field. With a type it says what kind of line it is.</summary>
        private sealed class Line
        {
            private readonly StringBuilder _json = new StringBuilder("{");

            public Line(string type = null)
            {
                if (type != null) Text("type", type);
            }

            public Line Raw(string name, string json)
            {
                if (_json.Length > 1) _json.Append(',');
                _json.Append(Quote(name)).Append(':').Append(json);
                return this;
            }

            public Line Text(string name, string value) => Raw(name, Quote(value));
            public Line Bool(string name, bool value) => Raw(name, value ? "true" : "false");
            public Line Number(string name, int value) => Raw(name, value.ToString(CultureInfo.InvariantCulture));
            public Line Number(string name, float value) => Raw(name, value.ToString("0.###", CultureInfo.InvariantCulture));

            public override string ToString() => _json.ToString() + "}";
        }
    }
}
