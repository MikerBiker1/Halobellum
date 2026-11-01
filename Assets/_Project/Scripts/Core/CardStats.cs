// CardStats.cs
// Plain C# copies of the cards offered between rounds (Bible Section 10, decision D33), filled from
// the data assets in Data/Cards and Data/Spells. A card is one of:
//   Spell    - a placed, rechargeable spell (see SpellStats);
//   Item     - an object kept in the Inventory and placed on one squad, for that whole squad (ItemStats);
//   Economy  - more credits every round (Supply Enhancement);
//   Buff     - a lasting bonus for the whole army, or an extra deployment slot;
//   Training - Intensive Training: fills one squad's XP bar, then recharges.
// MinRound keeps strong cards out of the early offers (nukes and the powerful buffs from round 7).
// No numbers live here: every value is data.
// Depends on: SpellStats.

using System;
using System.Collections.Generic;

namespace Halobellum.Core
{
    public enum CardType
    {
        Spell = 0,
        Item = 1,
        Economy = 2,
        Buff = 3,
        Training = 4,
    }

    /// <summary>An item: its effects apply to every body of the squad carrying it, for the whole match.</summary>
    public sealed class ItemStats
    {
        public string Name = "";
        public string Description = "";
        public float HealthMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float MoveSpeedMultiplier = 1f;
        /// <summary>Metres added to every weapon's range.</summary>
        public float RangeBonus;
        /// <summary>Share of the damage dealt that comes back as health (Absorption Module).</summary>
        public float LifestealShare;
        /// <summary>Share of its maximum health a body recovers per second (Healing Stims / Repair Kit).</summary>
        public float HealPerSecondShare;
        /// <summary>The squad can be moved during every deployment phase (Deployment Module).</summary>
        public bool FreeDeployment;

        public string EffectsText()
        {
            var parts = new List<string>();
            if (RangeBonus != 0f) parts.Add($"+{RangeBonus:0.#} m range");
            if (HealthMultiplier != 1f) parts.Add(Percent(HealthMultiplier) + " health");
            if (DamageMultiplier != 1f) parts.Add(Percent(DamageMultiplier) + " damage");
            if (MoveSpeedMultiplier != 1f) parts.Add(Percent(MoveSpeedMultiplier) + " speed");
            if (LifestealShare > 0f) parts.Add($"{LifestealShare * 100f:0}% of damage dealt heals it");
            if (HealPerSecondShare > 0f) parts.Add($"recovers {HealPerSecondShare * 100f:0.#}% health per second");
            if (FreeDeployment) parts.Add("can move in every deployment phase");
            return string.Join(", ", parts);
        }

        internal static string Percent(float multiplier)
        {
            int change = (int)MathF.Round((multiplier - 1f) * 100f);
            return (change >= 0 ? "+" : "") + change + "%";
        }
    }

    /// <summary>A player's army-wide bonuses from buff cards (decision D33).</summary>
    public sealed class ArmyBuffs
    {
        public float DamageMultiplier = 1f;
        public float HealthMultiplier = 1f;
        /// <summary>Metres added to the weapons of ranged units.</summary>
        public float RangeBonus;

        public bool IsNeutral => DamageMultiplier == 1f && HealthMultiplier == 1f && RangeBonus == 0f;
        public ArmyBuffs Clone() => (ArmyBuffs)MemberwiseClone();
    }

    public sealed class CardStats
    {
        public string Name = "";
        public string Description = "";
        public CardType Type;
        /// <summary>Credits paid when the card is taken.</summary>
        public int Cost;
        /// <summary>How many times a player can take it; at the maximum it is no longer offered.</summary>
        public int MaxOwnedCopies = 1;
        /// <summary>The first round it can be offered.</summary>
        public int MinRound;

        public SpellStats Spell;
        public ItemStats Item;

        /// <summary>Extra credits every round (Economy cards).</summary>
        public int BonusIncome;
        public float AllDamageMultiplier = 1f;
        public float AllHealthMultiplier = 1f;
        public float AllRangeBonus;
        /// <summary>More squads can be deployed per round.</summary>
        public int ExtraSquadsPerRound;
        /// <summary>Training: rounds it recharges after use.</summary>
        public int CooldownRounds;

        /// <summary>The card that gives this spell.</summary>
        public static CardStats FromSpell(SpellStats spell) => new CardStats
        {
            Name = spell.Name, Description = spell.Description, Type = CardType.Spell, Cost = spell.Cost,
            MaxOwnedCopies = spell.MaxOwnedCopies, MinRound = spell.MinRound, Spell = spell,
        };

        /// <summary>What it does, for the card text.</summary>
        public string EffectsText()
        {
            switch (Type)
            {
                case CardType.Spell: return Spell?.EffectsText() ?? "";
                case CardType.Item: return Item?.EffectsText() ?? "";
                case CardType.Economy: return $"+{BonusIncome} credits every round";
                case CardType.Training: return $"Fills one squad's XP bar, then recharges for {CooldownRounds} round{(CooldownRounds == 1 ? "" : "s")}";
            }
            var parts = new List<string>();
            if (AllDamageMultiplier != 1f) parts.Add($"{ItemStats.Percent(AllDamageMultiplier)} damage for all units");
            if (AllHealthMultiplier != 1f) parts.Add($"{ItemStats.Percent(AllHealthMultiplier)} health for all units");
            if (AllRangeBonus != 0f) parts.Add($"+{AllRangeBonus:0.#} m range for all ranged units");
            if (BonusIncome != 0) parts.Add($"+{BonusIncome} credits every round");
            if (ExtraSquadsPerRound != 0) parts.Add($"+{ExtraSquadsPerRound} squad per round");
            return string.Join(", ", parts);
        }

        public List<string> Validate()
        {
            var problems = new List<string>();
            string label = string.IsNullOrEmpty(Name) ? "(unnamed card)" : Name;
            if (string.IsNullOrEmpty(Name)) problems.Add("Card has no name.");
            if (Cost < 0) problems.Add($"{label}: cost cannot be negative.");
            if (MaxOwnedCopies < 1) problems.Add($"{label}: max copies must be at least 1.");
            if (Type == CardType.Spell && Spell == null) problems.Add($"{label}: a spell card needs a spell.");
            if (Type == CardType.Spell && Spell != null) problems.AddRange(Spell.Validate());
            if (Type == CardType.Item && Item == null) problems.Add($"{label}: an item card needs an item.");
            if (Type == CardType.Economy && BonusIncome <= 0) problems.Add($"{label}: an economy card needs bonus income.");
            if (Type == CardType.Training && CooldownRounds < 0) problems.Add($"{label}: cooldown cannot be negative.");
            return problems;
        }
    }
}
