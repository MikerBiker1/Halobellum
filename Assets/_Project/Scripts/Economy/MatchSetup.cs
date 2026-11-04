// MatchSetup.cs
// What is chosen before a match starts: the mode (today's Faction mode or Unlock Skirmish), the base health and which cards are
// offered. It is applied to the match's own fresh MatchRules, so the Match Rules asset never changes, and with no setup every
// value stays what it is in Faction mode.
// Below the asset's base health, what a starting card is worth moves the base by less, in proportion, so that a low setting
// can't start a side at nothing. From the asset's value up, the owner's flat 100 health per 50 credits stays (D59).
// Depends on: MatchRules.

using System;

namespace Halobellum.Economy
{
    public enum MatchMode
    {
        Faction = 0,
        UnlockSkirmish = 1,
    }

    public enum CardFilter
    {
        AllCards = 0,
        NoReinforcements = 1,
        OnlyReinforcements = 2,
    }

    /// <summary>Where the Hard AI puts its carriers, for trying the idea out in the tournament. The game itself never sets it.</summary>
    public enum CarrierPlacement
    {
        Default = 0,
        OwnBackCorners = 1,
        FlankFarCorners = 2,
    }

    public sealed class MatchSetup
    {
        public const int LeastBaseHealth = 100, MostBaseHealth = 10000, BaseHealthStep = 100;

        public MatchMode Mode;
        /// <summary>Each base's health before the starting card moves it. 0 = the asset's.</summary>
        public int BaseHealth;
        public CardFilter Cards;

        /// <summary>A base health setting inside the limits, on a whole step.</summary>
        public static int SnapBaseHealth(int health)
        {
            int clamped = Math.Clamp(health, LeastBaseHealth, MostBaseHealth);
            return (clamped + BaseHealthStep / 2) / BaseHealthStep * BaseHealthStep;
        }

        /// <summary>Writes this setup into a match's own rules.</summary>
        public void ApplyTo(MatchRules rules)
        {
            rules.Mode = Mode;
            rules.Cards = Cards;
            if (BaseHealth == 0) return;

            int health = SnapBaseHealth(BaseHealth);
            if (health < rules.BaseHealth) rules.DraftBaseHealthPerCredit *= health / (float)rules.BaseHealth;
            rules.BaseHealth = health;
        }
    }
}
