// ISquadProgression.cs
// What the stats window shows and offers for a selected squad's level, XP and tech upgrades
// (Bible Section 13, Milestone 6). The Match (MatchController) and the Sandbox (SandboxController)
// implement it on the Battle object; SquadStatsWindow draws whatever DescribeProgress returns.
// Depends on: Halobellum.Core (TechUpgrade).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Presentation
{
    /// <summary>One tech upgrade line in the stats window.</summary>
    public sealed class TechLine
    {
        public TechUpgrade Tech;
        public bool Owned;
        /// <summary>Button text (e.g. "Buy 150", "Remove"), or null for no button.</summary>
        public string ButtonLabel;
        public bool ButtonEnabled;
        public Action OnButton;
    }

    /// <summary>The level / XP / tech section of the stats window for one squad.</summary>
    public sealed class SquadProgressView
    {
        /// <summary>Replaces "Team A" / "Team B" in the window title when set (e.g. for the enemy's ghosts).</summary>
        public string SideLabel;
        public int Level = 1;
        public int MaxLevel = UnitUpgrades.MaxLevel;
        public bool ShowXp;
        public float Xp;
        public float XpNeeded;
        /// <summary>Button text such as "Level up (150)", or null for no button.</summary>
        public string LevelUpLabel;
        public bool LevelUpEnabled;
        public Action OnLevelUp;
        /// <summary>Sandbox only: lowers the level. Null = no button.</summary>
        public Action OnLevelDown;
        /// <summary>A short note under the level (e.g. "XP bar full: ready to level up").</summary>
        public string Note;
        /// <summary>The item the squad carries and what it does, or null.</summary>
        public string ItemText;
        public List<TechLine> Techs = new List<TechLine>();
    }

    public interface ISquadProgression
    {
        /// <summary>The progression section for this simulation squad, or null if there is nothing to show.</summary>
        SquadProgressView DescribeProgress(int simSquadId);
    }
}
