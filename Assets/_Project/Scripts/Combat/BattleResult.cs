// BattleResult.cs
// The outcome of one simulated battle: how long it lasted, whether it hit the time limit, and per
// team and per unit type how many bodies survived, damage dealt, kills, and the base damage the
// survivors would deal (Bible Section 5). Used by the headless runner and, later, the result screen.
// Outcome rule: the side whose survivors deal more base damage wins the battle; equal is a draw.
// A side that wiped the other out always wins (the wiped side deals 0).
// Depends on: Halobellum.Core (Team).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public enum BattleOutcome
    {
        TeamAWins = 0,
        TeamBWins = 1,
        Draw = 2,
    }

    /// <summary>Totals for one unit type on one team.</summary>
    public sealed class UnitTypeResult
    {
        public string UnitName;
        public Team Team;
        public int Deployed;
        public int Survivors;
        public float DamageDealt;
        public int Kills;
        public float SurvivorBaseDamage;
    }

    public sealed class TeamResult
    {
        public Team Team;
        public int Deployed;
        public int Survivors;
        public float DamageDealt;
        public int Kills;
        /// <summary>Total base damage the surviving bodies deal to the enemy base.</summary>
        public float SurvivorBaseDamage;
        /// <summary>Per unit type, in the order the types were first added.</summary>
        public List<UnitTypeResult> UnitTypes = new List<UnitTypeResult>();
    }

    public sealed class BattleResult
    {
        public int Seed;
        public int Ticks;
        public float DurationSeconds;
        /// <summary>True if the battle reached the time limit with both sides still alive.</summary>
        public bool TimedOut;
        public BattleOutcome Outcome;
        public TeamResult TeamA;
        public TeamResult TeamB;

        public TeamResult For(Team team) => team == Team.A ? TeamA : TeamB;

        /// <summary>
        /// One line that identifies this result: seed, length, outcome, survivors and damage per side.
        /// The battle scene and the headless runner print it, so their results can be compared by eye.
        /// </summary>
        public string ToSummaryLine()
        {
            string limit = TimedOut ? ", time limit" : "";
            return FormattableString.Invariant($"seed {Seed}, {Ticks} ticks ({DurationSeconds:0.0} s{limit}), {Outcome}; ")
                + FormattableString.Invariant($"A {TeamA.Survivors}/{TeamA.Deployed} alive, {TeamA.DamageDealt:0.0} dmg; ")
                + FormattableString.Invariant($"B {TeamB.Survivors}/{TeamB.Deployed} alive, {TeamB.DamageDealt:0.0} dmg");
        }
    }
}
