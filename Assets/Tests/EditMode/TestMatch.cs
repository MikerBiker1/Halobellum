// TestMatch.cs
// Builds match rules (Bible Section 5 starting values), test factions and matches for the economy,
// draft, deployment and round-flow tests. Unit numbers here are test fixtures, not game balance.
// Depends on: Halobellum.Economy, Halobellum.Core, TestRules, TestUnits.

using System.Collections.Generic;
using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Tests
{
    public static class TestMatch
    {
        public static MatchRules Rules() => new MatchRules
        {
            StartingCredits = 200,
            BaseIncome = 400,
            IncomeIncreasePerRound = 200,
            IncomeCap = 1600,
            BaseHealth = 2000,
            NewSquadsPerRound = 2,
            UnlocksPerRound = 1,
            RefundBeforeBattle = 1f,
            RefundAfterBattle = 0.5f,
            DraftCardCount = 5,
            DraftSquadsPerCard = 2,
            XpBarPerSquadCost = 1f,
            LevelUpCostShare = 0.5f,
        };

        public static UnitStats Infantry() => Unit("Infantry", tier: 1, unlock: 0, squad: 12, cost: 100, health: 60f, radius: 0.5f, baseDamage: 100f / 12f / 5f);
        public static UnitStats Buggy() => Unit("Buggy", tier: 1, unlock: 0, squad: 2, cost: 200, health: 300f, radius: 1.2f, baseDamage: 20f);
        public static UnitStats Tank() => Unit("Tank", tier: 2, unlock: 50, squad: 1, cost: 300, health: 1200f, radius: 2f, baseDamage: 60f);
        public static UnitStats Walker() => Unit("Walker", tier: 3, unlock: 225, squad: 1, cost: 450, health: 1500f, radius: 1.5f, baseDamage: 90f);

        private static UnitStats Unit(string name, int tier, int unlock, int squad, int cost, float health, float radius, float baseDamage)
        {
            UnitStats stats = TestUnits.Unit(name: name, health: health, speed: 4f, radius: radius, squadSize: squad, cost: cost,
                weapon: TestUnits.Weapon(damage: 10f, range: 12f, interval: 0.5f));
            stats.Tier = tier;
            stats.UnlockCost = unlock;
            stats.BaseDamage = baseDamage;
            return stats;
        }

        /// <summary>One faction with the given sub-faction names, all sharing a base roster.</summary>
        public static FactionInfo Faction(string name, List<UnitStats> roster, params string[] subFactions)
        {
            var faction = new FactionInfo { Name = name, BaseRoster = roster };
            foreach (string sub in subFactions) faction.SubFactions.Add(new SubFactionInfo { Name = sub });
            return faction;
        }

        /// <summary>Standard UNSC and Covenant, both with Infantry, Buggy, Tank and Walker.</summary>
        public static List<FactionInfo> StandardFactions(out UnitStats infantry, out UnitStats buggy, out UnitStats tank, out UnitStats walker)
        {
            infantry = Infantry();
            buggy = Buggy();
            tank = Tank();
            walker = Walker();
            var roster = new List<UnitStats> { infantry, buggy, tank, walker };
            return new List<FactionInfo> { Faction("UNSC", roster, "Standard"), Faction("Covenant", roster, "Standard") };
        }

        /// <summary>A match past the draft (player picked card 0), in round 1's deployment.</summary>
        public static Match Started(out UnitStats infantry, out UnitStats buggy, out UnitStats tank, out UnitStats walker, int seed = 1, IMatchAi ai = null)
        {
            var match = new Match(Rules(), TestRules.Default(), StandardFactions(out infantry, out buggy, out tank, out walker), seed, ai ?? new DoNothingAi());
            match.ChooseDraft(0);
            return match;
        }
    }
}
