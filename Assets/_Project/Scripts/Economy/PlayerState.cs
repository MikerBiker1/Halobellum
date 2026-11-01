// PlayerState.cs
// Everything one player owns during a match: faction and sub-faction from the draft, credits, base
// health, unlocked unit types, and the squads placed on the deployment grid. The deployment is the
// part that persists between rounds: every battle is built fresh from it, so units destroyed in a
// battle are back at full health in their places next round (Bible Section 5, Round reset).
// Depends on: Halobellum.Core (UnitStats, Team), FactionInfo.

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    /// <summary>One squad on the deployment grid.</summary>
    public sealed class PlacedSquad
    {
        public int Id;
        public UnitStats Type;
        /// <summary>Left-most column of its footprint, in the owner's frame (0 = the owner's left edge of the field).</summary>
        public int Column;
        /// <summary>Front-most row of its footprint, counted back from the front edge of the owner's zone (0 = front).</summary>
        public int Row;
        /// <summary>Turned 90 degrees (width and depth swapped).</summary>
        public bool Rotated90;
        public int RoundBought;
        /// <summary>Credits paid (0 for the free starting squads from the draft).</summary>
        public int PricePaid;
        /// <summary>True for the free starting squads from the draft (they do not count toward the squads per round).</summary>
        public bool FromDraft;
        /// <summary>True once the squad has been in a battle: it can no longer be moved or turned.</summary>
        public bool HasFought;
        /// <summary>Squad level, 1 to 5 (Bible Section 5, Squad leveling). It survives the round reset.</summary>
        public int Level = 1;
        /// <summary>Experience earned from kills toward the next level. It survives the round reset.</summary>
        public float Xp;
        /// <summary>Credits spent leveling this squad up (refunded with the squad's price when sold).</summary>
        public int LevelUpSpent;
    }

    public sealed class PlayerState
    {
        public readonly Team Team;
        public FactionInfo Faction;
        public SubFactionInfo SubFaction;
        public int Credits;
        public int BaseHealth;
        public readonly HashSet<UnitStats> Unlocked = new HashSet<UnitStats>();
        public readonly List<PlacedSquad> Squads = new List<PlacedSquad>();
        public int SquadsBoughtThisRound;
        public int UnlocksThisRound;

        /// <summary>Tech upgrades bought, per unit type (they apply to every squad of that type).</summary>
        public readonly Dictionary<UnitStats, List<TechUpgrade>> Techs = new Dictionary<UnitStats, List<TechUpgrade>>();
        private readonly Dictionary<(UnitStats, int, int), UnitStats> _effective = new Dictionary<(UnitStats, int, int), UnitStats>();

        public PlayerState(Team team)
        {
            Team = team;
        }

        /// <summary>The techs this player owns for a unit type (empty if none).</summary>
        public IReadOnlyList<TechUpgrade> TechsFor(UnitStats type) =>
            type != null && Techs.TryGetValue(type, out List<TechUpgrade> list) ? list : (IReadOnlyList<TechUpgrade>)System.Array.Empty<TechUpgrade>();

        public bool HasTech(UnitStats type, TechUpgrade tech) => Techs.TryGetValue(type, out List<TechUpgrade> list) && list.Contains(tech);

        public void AddTech(UnitStats type, TechUpgrade tech)
        {
            if (!Techs.TryGetValue(type, out List<TechUpgrade> list)) Techs[type] = list = new List<TechUpgrade>();
            if (!list.Contains(tech)) list.Add(tech);
        }

        /// <summary>Removes a tech (sandbox only; in a match techs are permanent).</summary>
        public void RemoveTech(UnitStats type, TechUpgrade tech)
        {
            if (Techs.TryGetValue(type, out List<TechUpgrade> list)) list.Remove(tech);
        }

        /// <summary>The squad's battle stats: its unit type at its level with this player's techs (cached).</summary>
        public UnitStats EffectiveStats(PlacedSquad squad)
        {
            // Key on which techs are owned (one bit per slot), and apply them in slot order.
            int mask = 0;
            var owned = new List<TechUpgrade>();
            TechUpgrade[] slots = squad.Type.TechUpgrades ?? System.Array.Empty<TechUpgrade>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || !HasTech(squad.Type, slots[i])) continue;
                mask |= 1 << i;
                owned.Add(slots[i]);
            }
            var key = (squad.Type, squad.Level, mask);
            if (!_effective.TryGetValue(key, out UnitStats stats))
            {
                stats = UnitUpgrades.Apply(squad.Type, squad.Level, owned);
                _effective[key] = stats;
            }
            return stats;
        }

        public bool HasFaction => Faction != null;

        /// <summary>Unit types this player can ever unlock (empty until the draft is chosen).</summary>
        public List<UnitStats> Roster => Faction != null ? Faction.RosterFor(SubFaction) : new List<UnitStats>();

        public PlacedSquad FindSquad(int squadId) => Squads.Find(s => s.Id == squadId);

        public string Label => HasFaction ? $"{Faction.Name} ({SubFaction?.Name})" : $"Team {Team}";
    }
}
