// MatchCommand.cs
// One action a side did in a two-humans match (Milestone 11, a LAN game), exact enough for the other machine to do it again: the team,
// the round, its number in the side's list, the action's name and every value it was given (squad and spell ids, the card or starting
// card, the unit, tech and item, the tower, research health or damage, the column, row and turn, and a spell's position and angle as
// their exact float bits). The Match records every action that works (Match.TakeCommands); MatchCommands.Apply does one on another Match.
// Its text is one JSON object per command, written and read without the PC's culture, and reads back to exactly the same command.
// Depends on: Match, PlayerState, StableHasher, MatchLog (Quote), LogJson, Halobellum.Core.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public sealed class MatchCommand
    {
        // The action names (the same words the battle log uses where it has them).
        public const string ChooseDraftAction = "ChooseDraft";
        public const string ChooseCardAction = "ChooseCard";
        public const string SkipCardsAction = "SkipCards";
        public const string UnlockAction = "Unlock";
        public const string BuyAction = "Buy";
        public const string MoveAction = "Move";
        public const string SellAction = "Sell";
        public const string LevelUpAction = "LevelUp";
        public const string LevelUpAllAction = "LevelUpAll";
        public const string BuyTechAction = "BuyTech";
        public const string LevelUpTowerAction = "LevelUpTower";
        public const string BuyExtraSlotAction = "BuyExtraSlot";
        public const string BuyOilZoneAction = "BuyOilZone";
        public const string RapidResupplyAction = "RapidResupply";
        public const string BuyEliteRecruitmentAction = "BuyEliteRecruitment";
        public const string BuyResearchAction = "BuyResearch";
        public const string BuyDeploymentModuleAction = "BuyDeploymentModule";
        public const string PlaceSpellAction = "PlaceSpell";
        public const string UnplaceSpellAction = "UnplaceSpell";
        public const string PlaceItemAction = "PlaceItem";
        public const string UseTrainingAction = "UseTraining";
        public const string UndoAction = "Undo";

        public Team Team;
        /// <summary>The match round it was done in (0 for the starting card).</summary>
        public int Round;
        /// <summary>Its number in the side's list, from 1 (set when the Match records it).</summary>
        public int Seq;
        public string Action;
        public int SquadId;
        public int SpellId;
        /// <summary>The card (ChooseCard), the starting card (ChooseDraft) or the item's place in the inventory (PlaceItem).</summary>
        public int Index;
        public string Unit;
        public string Tech;
        public string Item;
        public TowerKind Tower;
        /// <summary>BuyResearch: health (true) or damage (false).</summary>
        public bool Health;
        public int Column;
        public int Row;
        public bool Turned;
        /// <summary>PlaceSpell: the position in battlefield metres and the angle, exact.</summary>
        public float X, Y, Angle;

        /// <summary>The command as one line of JSON. Floats are written as their bits, so the text reads back to the same command.</summary>
        public string ToText()
        {
            var json = new StringBuilder("{");
            void Field(string name, string value)
            {
                if (json.Length > 1) json.Append(',');
                json.Append(MatchLog.Quote(name)).Append(':').Append(value);
            }
            string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
            Field("team", MatchLog.Quote(Team.ToString()));
            Field("round", Number(Round));
            Field("seq", Number(Seq));
            Field("action", MatchLog.Quote(Action));
            Field("squad", Number(SquadId));
            Field("spell", Number(SpellId));
            Field("index", Number(Index));
            Field("unit", MatchLog.Quote(Unit));
            Field("tech", MatchLog.Quote(Tech));
            Field("item", MatchLog.Quote(Item));
            Field("tower", MatchLog.Quote(Tower.ToString()));
            Field("health", Health ? "true" : "false");
            Field("column", Number(Column));
            Field("row", Number(Row));
            Field("turned", Turned ? "true" : "false");
            Field("x", Number(StableHasher.FloatToBits(X)));
            Field("y", Number(StableHasher.FloatToBits(Y)));
            Field("angle", Number(StableHasher.FloatToBits(Angle)));
            return json.Append('}').ToString();
        }

        /// <summary>Reads a command back from ToText. Throws FormatException if the text is not one.</summary>
        public static MatchCommand Parse(string text)
        {
            Dictionary<string, object> json = LogJson.Object(text);
            string team = LogJson.Text(json, "team");
            if (team != "A" && team != "B") throw new FormatException("A command names team A or B.");
            string action = LogJson.Text(json, "action");
            if (string.IsNullOrEmpty(action)) throw new FormatException("A command names its action.");
            if (!TryTower(LogJson.Text(json, "tower"), out TowerKind tower)) throw new FormatException("A command names a known tower.");
            return new MatchCommand
            {
                Team = team == "A" ? Team.A : Team.B,
                Round = LogJson.Int(json, "round"),
                Seq = LogJson.Int(json, "seq"),
                Action = action,
                SquadId = LogJson.Int(json, "squad"),
                SpellId = LogJson.Int(json, "spell"),
                Index = LogJson.Int(json, "index"),
                Unit = LogJson.Text(json, "unit"),
                Tech = LogJson.Text(json, "tech"),
                Item = LogJson.Text(json, "item"),
                Tower = tower,
                Health = Bool(json, "health"),
                Column = LogJson.Int(json, "column"),
                Row = LogJson.Int(json, "row"),
                Turned = Bool(json, "turned"),
                X = StableHasher.BitsToFloat(LogJson.Int(json, "x")),
                Y = StableHasher.BitsToFloat(LogJson.Int(json, "y")),
                Angle = StableHasher.BitsToFloat(LogJson.Int(json, "angle")),
            };
        }

        /// <summary>Several commands as text, one per line.</summary>
        public static string ListToText(IEnumerable<MatchCommand> commands)
        {
            var text = new StringBuilder();
            foreach (MatchCommand command in commands) text.Append(command.ToText()).Append('\n');
            return text.ToString();
        }

        /// <summary>Reads ListToText's lines back (blank lines are skipped).</summary>
        public static List<MatchCommand> ParseList(string text)
        {
            var commands = new List<MatchCommand>();
            if (string.IsNullOrEmpty(text)) return commands;
            foreach (string line in text.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line)) commands.Add(Parse(line));
            }
            return commands;
        }

        /// <summary>
        /// Reads a tower's name, exactly as TowerKind.ToString() writes it. Numbers ("7"), unknown names, a missing name and lists of
        /// names are refused (Enum.TryParse would take them and give a tower that does not exist, or the Command Center).
        /// </summary>
        internal static bool TryTower(string text, out TowerKind tower)
        {
            tower = default;
            if (string.IsNullOrEmpty(text)) return false;
            foreach (TowerKind kind in (TowerKind[])Enum.GetValues(typeof(TowerKind)))
            {
                if (!string.Equals(kind.ToString(), text, StringComparison.Ordinal)) continue;
                tower = kind;
                return true;
            }
            return false;
        }

        private static bool Bool(Dictionary<string, object> json, string name) => json.TryGetValue(name, out object value) && value is bool b && b;

        public override string ToString() => ToText();
    }

    /// <summary>Does another machine's commands on a Match (two humans, Milestone 11).</summary>
    public static class MatchCommands
    {
        /// <summary>
        /// Does one command from the other machine, as that side did it there, and says whether it worked. `from` is the team the other
        /// machine plays: a command for any other team is refused, so a list from the other machine can never act for this machine's side.
        /// A command for another round than the match's, or one out of order (each side's Seq must rise by one: 1, 2, 3, ... over the
        /// whole match), fails without doing anything. The command is not recorded again (Match.TakeCommands only holds what was done on
        /// this machine), but it goes into the side's undo history as it did there, so a later Undo in the list takes it back.
        /// </summary>
        public static ActionResult Apply(Match match, Team from, MatchCommand command)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (!match.IsTwoHumans) return ActionResult.Fail("Commands from another machine only work in a two-humans match.");
            if (command.Team != from) return ActionResult.Fail($"{command.Action} is for team {command.Team}; commands from the other machine must be for team {from}.");
            if (command.Round != match.Round) return ActionResult.Fail($"{command.Action} was done in round {command.Round}; the match is in round {match.Round}.");
            if (!match.TakeRemoteSeq(command.Team, command.Seq, out int expected)) return ActionResult.Fail($"{command.Action} is command #{command.Seq} of team {command.Team}; #{expected} was next.");
            bool before = match.ApplyingRemote;
            match.ApplyingRemote = true;
            try
            {
                return Do(match, command);
            }
            finally
            {
                match.ApplyingRemote = before;
            }
        }

        /// <summary>
        /// Does every command in order (each as Apply does it, all from team `from`). Returns true if all worked; otherwise `failures`
        /// (if given) says which did not and why.
        /// </summary>
        public static bool ApplyAll(Match match, Team from, IEnumerable<MatchCommand> commands, List<string> failures = null)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            bool all = true;
            foreach (MatchCommand command in commands)
            {
                ActionResult result = Apply(match, from, command);
                if (result.Ok) continue;
                all = false;
                failures?.Add($"team {command?.Team} #{command?.Seq} {command?.Action}: {result.Message}");
            }
            return all;
        }

        private static ActionResult Do(Match match, MatchCommand c)
        {
            Team team = c.Team;
            PlayerState player = match.Get(team);
            switch (c.Action)
            {
                case MatchCommand.ChooseDraftAction: return match.ChooseDraft(team, c.Index);
                case MatchCommand.ChooseCardAction: return match.ChooseCard(team, c.Index);
                case MatchCommand.SkipCardsAction: return match.SkipCards(team);
                case MatchCommand.UnlockAction: return WithUnit(player, c, type => match.Unlock(team, type));
                case MatchCommand.BuyAction: return WithUnit(player, c, type => match.Buy(team, type, c.Column, c.Row, c.Turned));
                case MatchCommand.MoveAction: return match.Move(team, c.SquadId, c.Column, c.Row, c.Turned);
                case MatchCommand.SellAction: return match.Sell(team, c.SquadId);
                case MatchCommand.LevelUpAction: return match.LevelUp(team, c.SquadId);
                case MatchCommand.LevelUpAllAction: return WithUnit(player, c, type => match.LevelUpAll(team, type));
                case MatchCommand.BuyTechAction:
                    return WithUnit(player, c, type =>
                    {
                        TechUpgrade tech = type.TechUpgrades == null ? null : Array.Find(type.TechUpgrades, t => t != null && t.Name == c.Tech);
                        return tech == null ? ActionResult.Fail($"{type.Name} has no tech {c.Tech}.") : match.BuyTech(team, type, tech);
                    });
                case MatchCommand.LevelUpTowerAction: return match.LevelUpTower(team, c.Tower);
                case MatchCommand.BuyExtraSlotAction: return match.BuyExtraSlot(team);
                case MatchCommand.BuyOilZoneAction: return match.BuyOilZone(team);
                case MatchCommand.RapidResupplyAction: return match.RapidResupply(team);
                case MatchCommand.BuyEliteRecruitmentAction: return match.BuyEliteRecruitment(team);
                case MatchCommand.BuyResearchAction: return match.BuyResearch(team, c.Health);
                case MatchCommand.BuyDeploymentModuleAction: return match.BuyDeploymentModule(team);
                case MatchCommand.PlaceSpellAction: return match.PlaceSpell(team, c.SpellId, new Vec2(c.X, c.Y), c.Angle);
                case MatchCommand.UnplaceSpellAction: return match.UnplaceSpell(team, c.SpellId);
                case MatchCommand.PlaceItemAction:
                {
                    // The item at the place it had in the inventory (the same object as on the other machine), or else the first of its name.
                    ItemStats item = c.Index >= 0 && c.Index < player.Inventory.Count && player.Inventory[c.Index]?.Name == c.Item
                        ? player.Inventory[c.Index]
                        : player.Inventory.Find(i => i != null && i.Name == c.Item);
                    return item == null ? ActionResult.Fail($"No {c.Item} in the inventory.") : match.PlaceItem(team, item, c.SquadId);
                }
                case MatchCommand.UseTrainingAction: return match.UseTraining(team, c.SquadId);
                case MatchCommand.UndoAction: return match.Undo(team);
                default: return ActionResult.Fail($"Unknown command {c.Action}.");
            }
        }

        private static ActionResult WithUnit(PlayerState player, MatchCommand c, Func<UnitStats, ActionResult> act)
        {
            UnitStats type = FindUnit(player, c.Unit);
            return type == null ? ActionResult.Fail($"No {c.Unit} for team {c.Team}.") : act(type);
        }

        /// <summary>The side's unit type of this name: from its roster, or else among its unlocked types (by name, in ordinal order).</summary>
        public static UnitStats FindUnit(PlayerState player, string name)
        {
            if (name == null) return null;
            UnitStats type = player.Roster.Find(u => u.Name == name);
            if (type != null) return type;
            var unlocked = new List<UnitStats>(player.Unlocked);
            unlocked.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return unlocked.Find(u => u.Name == name);
        }
    }
}
