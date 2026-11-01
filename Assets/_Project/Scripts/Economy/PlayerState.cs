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

        public PlayerState(Team team)
        {
            Team = team;
        }

        public bool HasFaction => Faction != null;

        /// <summary>Unit types this player can ever unlock (empty until the draft is chosen).</summary>
        public List<UnitStats> Roster => Faction != null ? Faction.RosterFor(SubFaction) : new List<UnitStats>();

        public PlacedSquad FindSquad(int squadId) => Squads.Find(s => s.Id == squadId);

        public string Label => HasFaction ? $"{Faction.Name} ({SubFaction?.Name})" : $"Team {Team}";
    }
}
