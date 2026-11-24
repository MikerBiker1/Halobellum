// RoundOutcome.cs
// Everything a finished battle does to the match (Milestone 11): the base damage each side's survivors deal, the XP each placed squad
// earned (by squad id, in battle order: Team A's squads first) and the towers that fell. Match.OutcomeOfCurrentBattle takes it from the
// battle and FinishBattle applies it. In a LAN game the host's outcome is final: the host sends it as text and the guest applies it with
// FinishBattle(outcome), so both matches stay the same even if the guest's battle played out a little differently. The text is one JSON
// object; XP goes as exact float bits, so it reads back to the same outcome.
// Depends on: StableHasher, MatchLog (Quote), LogJson, MatchCommand (TryTower), Halobellum.Core (Team, TowerKind).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halobellum.Core;

namespace Halobellum.Economy
{
    /// <summary>The XP one placed squad earned in a battle.</summary>
    public sealed class SquadXp
    {
        public int SquadId;
        public float Xp;
    }

    /// <summary>One tower that fell in a battle.</summary>
    public sealed class TowerFall
    {
        public Team Team;
        public TowerKind Kind;
    }

    public sealed class RoundOutcome
    {
        public int Round;
        /// <summary>Damage dealt to Team B's base by Team A's survivors, and the other way around.</summary>
        public int BaseDamageByA;
        public int BaseDamageByB;
        /// <summary>The battle's length in ticks. Not applied: only there to compare two machines' battles.</summary>
        public int Ticks;
        /// <summary>Every placed squad, in battle order, with the XP it earned (0 for none).</summary>
        public List<SquadXp> Xp = new List<SquadXp>();
        /// <summary>The towers that fell, in the battle's unit order.</summary>
        public List<TowerFall> TowerFalls = new List<TowerFall>();

        /// <summary>A stable hash of the whole outcome (the same on every machine).</summary>
        public ulong Hash()
        {
            var hash = new StableHasher().Text("Halobellum.RoundOutcome.1").Int(Round).Int(BaseDamageByA).Int(BaseDamageByB).Int(Ticks).Int(Xp.Count);
            foreach (SquadXp entry in Xp) hash.Int(entry.SquadId).Float(entry.Xp);
            hash.Int(TowerFalls.Count);
            foreach (TowerFall fall in TowerFalls) hash.Int((int)fall.Team).Int((int)fall.Kind);
            return hash.Value;
        }

        /// <summary>The outcome as one line of JSON.</summary>
        public string ToText()
        {
            string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
            var json = new StringBuilder("{");
            json.Append("\"round\":").Append(Number(Round));
            json.Append(",\"a\":").Append(Number(BaseDamageByA));
            json.Append(",\"b\":").Append(Number(BaseDamageByB));
            json.Append(",\"ticks\":").Append(Number(Ticks));
            json.Append(",\"xp\":[");
            for (int i = 0; i < Xp.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append('[').Append(Number(Xp[i].SquadId)).Append(',').Append(Number(StableHasher.FloatToBits(Xp[i].Xp))).Append(']');
            }
            json.Append("],\"falls\":[");
            for (int i = 0; i < TowerFalls.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append('[').Append(MatchLog.Quote(TowerFalls[i].Team.ToString())).Append(',').Append(MatchLog.Quote(TowerFalls[i].Kind.ToString())).Append(']');
            }
            return json.Append("]}").ToString();
        }

        /// <summary>Reads an outcome back from ToText. Throws FormatException if the text is not one.</summary>
        public static RoundOutcome Parse(string text)
        {
            Dictionary<string, object> json = LogJson.Object(text);
            var outcome = new RoundOutcome
            {
                Round = LogJson.Int(json, "round"),
                BaseDamageByA = LogJson.Int(json, "a"),
                BaseDamageByB = LogJson.Int(json, "b"),
                Ticks = LogJson.Int(json, "ticks"),
            };
            if (json.TryGetValue("xp", out object xp) && xp is List<object> xpList)
            {
                foreach (object item in xpList)
                {
                    if (!(item is List<object> pair) || pair.Count != 2 || !(pair[0] is double id) || !(pair[1] is double bits)) throw new FormatException("An XP entry is [squad id, bits].");
                    outcome.Xp.Add(new SquadXp { SquadId = (int)Math.Round(id), Xp = StableHasher.BitsToFloat((int)Math.Round(bits)) });
                }
            }
            if (json.TryGetValue("falls", out object falls) && falls is List<object> fallList)
            {
                foreach (object item in fallList)
                {
                    if (!(item is List<object> pair) || pair.Count != 2 || !(pair[0] is string team) || !(pair[1] is string kind)
                        || (team != "A" && team != "B") || !MatchCommand.TryTower(kind, out TowerKind towerKind))
                        throw new FormatException("A tower fall is [team, tower].");
                    outcome.TowerFalls.Add(new TowerFall { Team = team == "A" ? Team.A : Team.B, Kind = towerKind });
                }
            }
            return outcome;
        }

        /// <summary>A short line for logs: where two outcomes differ is easy to see.</summary>
        public override string ToString() =>
            FormattableString.Invariant($"round {Round}: base damage {BaseDamageByA} by A, {BaseDamageByB} by B, {Ticks} ticks, {TowerFalls.Count} tower(s) fell, hash {StableHasher.Hex(Hash())}");
    }
}
