// BattleStats.cs
// One battle's statistics for the Battle stats window (D69): a line for each placed squad, tower and spell of both sides, with
// where it stood when the battle started, and what it did: damage dealt, damage taken, damage its defenses stopped, kills,
// and how many of its bodies lived. The Match makes one when a battle starts (AtStart) and fills it in when it ends (Finish);
// they live as long as the Match, so a new game or quitting forgets them.
// Bodies dropped, released or risen in the battle count for the squad or spell they came from (SimUnit.CreditSquadId and
// CreditSpell). A seed's damage counts for the unit that planted it.
// "Stopped" is what the hits would have done to a body with no armor, shield or defense arc, minus what they did, never more
// than the body had left; a shot an arc blocked counts whole.
// Depends on: Halobellum.Combat (BattleSimulation, SimUnit, SimSpell), Halobellum.Core (UnitStats, SpellStats, Team, Vec2), PlayerState.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public enum BattleStatsKind
    {
        Squad = 0,
        Tower = 1,
        Spell = 2,
    }

    public sealed class BattleStatsLine
    {
        public BattleStatsKind Kind;
        public Team Team;
        /// <summary>The unit type (a squad or a tower), or null for a spell.</summary>
        public UnitStats Type;
        /// <summary>The spell, or null.</summary>
        public SpellStats Spell;
        public string Name;
        /// <summary>Squad or tower level, 0 for a spell.</summary>
        public int Level;
        /// <summary>Where it stood when the battle started (a squad's centre, a tower, a spell's spot), in the battle's metres.</summary>
        public Vec2 Start;
        /// <summary>A squad that arrives late (D61).</summary>
        public bool ArrivesLate;
        public int Bodies, Survivors;
        public float DamageDealt, DamageTaken, DamageStopped;
        public int Kills;

        /// <summary>Everything thrown at it: what it took and what it stopped.</summary>
        public float DamageReceived => DamageTaken + DamageStopped;

        internal int SimSquadId = -1, UnitId = -1, SpellId = -1;
    }

    public sealed class BattleStats
    {
        public int Round;
        /// <summary>Team A's lines first (squads, towers, spells, in that order), then Team B's.</summary>
        public readonly List<BattleStatsLine> Lines = new List<BattleStatsLine>();
        public bool Finished { get; private set; }
        /// <summary>The field's size in metres: X from -Width/2 to Width/2, Y from -Length/2 (Team A's edge) to Length/2.</summary>
        public float FieldWidth, FieldLength;

        /// <summary>One side's lines, sorted by damage dealt (or by everything thrown at them), the biggest first.</summary>
        public List<BattleStatsLine> Side(Team team, bool byReceived)
        {
            var side = Lines.FindAll(line => line.Team == team);
            // A stable sort, so that equal lines keep their order.
            var order = new List<int>();
            for (int i = 0; i < side.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                float x = byReceived ? side[a].DamageReceived : side[a].DamageDealt, y = byReceived ? side[b].DamageReceived : side[b].DamageDealt;
                return x != y ? y.CompareTo(x) : a.CompareTo(b);
            });
            return order.ConvertAll(i => side[i]);
        }

        /// <summary>
        /// The lines of a battle about to start: simulation squad i is placed squad i, Team A's first (Match.BuildBattle); towers are
        /// the units without a squad that are towers; then the spells.
        /// </summary>
        public static BattleStats AtStart(BattleSimulation battle, int round, PlayerState a, PlayerState b, CombatRules rules)
        {
            var stats = new BattleStats { Round = round, FieldWidth = rules.ArenaWidth, FieldLength = rules.ArenaLength };
            foreach (PlayerState side in new[] { a, b }) AddSide(stats, battle, side, side == a ? 0 : a.Squads.Count);
            return stats;
        }

        private static void AddSide(BattleStats stats, BattleSimulation battle, PlayerState side, int firstSquad)
        {
            for (int i = 0; i < side.Squads.Count && firstSquad + i < battle.Squads.Count; i++)
            {
                PlacedSquad placed = side.Squads[i];
                SimSquad squad = battle.Squads[firstSquad + i];
                Vec2 centre = Vec2.Zero;
                bool late = false;
                foreach (SimUnit member in squad.Members)
                {
                    centre += member.Position;
                    late |= member.NotArrived;
                }
                if (squad.Members.Count > 0) centre /= squad.Members.Count;
                stats.Lines.Add(new BattleStatsLine
                {
                    Kind = BattleStatsKind.Squad, Team = side.Team, Type = placed.Type, Name = placed.Type.Name, Level = placed.Level,
                    Start = centre, ArrivesLate = late, Bodies = squad.Members.Count, SimSquadId = squad.Id,
                });
            }
            foreach (SimUnit unit in battle.Units)
            {
                if (unit.Team != side.Team || !unit.Stats.IsTower) continue;
                PlacedTower tower = unit.Stats.Shockwave != null ? side.FindTower(unit.Stats.Shockwave.Kind) : null;
                stats.Lines.Add(new BattleStatsLine
                {
                    Kind = BattleStatsKind.Tower, Team = side.Team, Type = unit.Stats, Name = unit.Stats.Name, Level = tower?.Level ?? 1,
                    Start = unit.Position, Bodies = 1, UnitId = unit.Id,
                });
            }
            foreach (SimSpell spell in battle.Spells)
            {
                if (spell.Team != side.Team) continue;
                stats.Lines.Add(new BattleStatsLine
                {
                    Kind = BattleStatsKind.Spell, Team = side.Team, Spell = spell.Stats, Name = spell.Stats.Name, Start = spell.Position, SpellId = spell.Id,
                });
            }
        }

        /// <summary>Fills the lines in from the finished battle. Safe to call again: it counts from scratch.</summary>
        public void Finish(BattleSimulation battle)
        {
            var bySquad = new Dictionary<int, BattleStatsLine>();
            var byUnit = new Dictionary<int, BattleStatsLine>();
            var bySpell = new Dictionary<int, BattleStatsLine>();
            foreach (BattleStatsLine line in Lines)
            {
                line.Survivors = 0;
                line.DamageDealt = line.DamageTaken = line.DamageStopped = 0f;
                line.Kills = 0;
                if (line.SimSquadId >= 0) bySquad[line.SimSquadId] = line;
                if (line.UnitId >= 0) byUnit[line.UnitId] = line;
                if (line.SpellId >= 0) bySpell[line.SpellId] = line;
            }

            foreach (SimUnit unit in battle.Units)
            {
                BattleStatsLine line;
                if (!byUnit.TryGetValue(unit.Id, out line)
                    && !(unit.CreditSquadId >= 0 && bySquad.TryGetValue(unit.CreditSquadId, out line))
                    && !(unit.CreditSpell != null && bySpell.TryGetValue(unit.CreditSpell.Id, out line))) continue;
                if (line.Team != unit.Team) continue; // A body risen for the other side counts for nobody.
                line.DamageDealt += unit.DamageDealt + unit.SeedDamageDealt;
                line.DamageTaken += unit.DamageTaken;
                line.DamageStopped += unit.DamagePrevented;
                line.Kills += unit.Kills;
                bool ownBody = line.Kind == BattleStatsKind.Tower || unit.SquadId == line.SimSquadId;
                if (ownBody && unit.IsAlive) line.Survivors++;
            }
            foreach (SimSpell spell in battle.Spells)
            {
                if (!bySpell.TryGetValue(spell.Id, out BattleStatsLine line)) continue;
                line.DamageDealt += spell.DamageDealt;
                line.Kills += spell.Kills;
            }
            Finished = true;
        }
    }
}
