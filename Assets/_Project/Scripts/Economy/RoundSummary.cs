// RoundSummary.cs
// What happened in one round, for the result screen (Bible Sections 5 and 13): base damage dealt by
// each side, base health afterwards, income awarded, survivors, damage per unit type, and "what killed
// what" (kills per attacker type and victim type).
// Depends on: Halobellum.Combat (BattleSimulation, BattleResult, SimUnit), Halobellum.Core (Team).

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    /// <summary>How many bodies of one unit type killed bodies of another type.</summary>
    public sealed class KillCount
    {
        public Team KillerTeam;
        public string KillerType;
        public string VictimType;
        public int Kills;
    }

    public sealed class RoundSummary
    {
        public int Round;
        public BattleResult Battle;
        /// <summary>Damage dealt to Team B's base by Team A's survivors, and the other way around.</summary>
        public int BaseDamageByA;
        public int BaseDamageByB;
        public int BaseHealthA;
        public int BaseHealthB;
        public int IncomeAwarded;
        /// <summary>XP each placed squad earned in this battle (placed squad id -> XP), both sides.</summary>
        public Dictionary<int, float> XpGained = new Dictionary<int, float>();
        /// <summary>The player's squads whose XP bar is full (they can be leveled up), by unit name.</summary>
        public List<string> PlayerSquadsReadyToLevel = new List<string>();
        /// <summary>True if both bases fell to exactly the same health: a tied round (damage undone, the match goes on).</summary>
        public bool Tie;
        /// <summary>The winner if this round ended the match, otherwise null.</summary>
        public Team? MatchWinner;
        public List<KillCount> Kills = new List<KillCount>();

        public int BaseDamageBy(Team team) => team == Team.A ? BaseDamageByA : BaseDamageByB;

        /// <summary>Counts kills by (killer team, killer type or spell, victim type) from a finished battle.</summary>
        public static List<KillCount> CountKills(BattleSimulation simulation)
        {
            var byKey = new Dictionary<string, KillCount>();
            var ordered = new List<KillCount>();
            foreach (SimUnit unit in simulation.Units)
            {
                if (unit.IsAlive) continue;
                Team killerTeam;
                string killerName;
                if (unit.KilledBySpell != null)
                {
                    killerTeam = unit.KilledBySpell.Team;
                    killerName = unit.KilledBySpell.Stats.Name;
                }
                else if (unit.KilledById >= 0)
                {
                    SimUnit killer = simulation.Units[unit.KilledById];
                    killerTeam = killer.Team;
                    killerName = killer.Stats.Name;
                }
                else continue;
                string key = $"{killerTeam}|{killerName}|{unit.Stats.Name}";
                if (!byKey.TryGetValue(key, out KillCount count))
                {
                    count = new KillCount { KillerTeam = killerTeam, KillerType = killerName, VictimType = unit.Stats.Name };
                    byKey[key] = count;
                    ordered.Add(count);
                }
                count.Kills++;
            }
            ordered.Sort((a, b) => b.Kills.CompareTo(a.Kills));
            return ordered;
        }
    }
}
