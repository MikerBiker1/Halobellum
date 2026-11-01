// CardDefinition.cs
// The data asset for one non-spell card (Bible Section 10, decision D33): an item, an economy card, a
// buff or Intensive Training. A faction asset lists the cards it can be offered, next to its spells;
// items, economy and buff cards are shared by both factions. Assets live in Data/Cards/.
// Fields left at 1 (multipliers) or 0 (bonuses) change nothing. ToStats() copies the values into plain
// C# (Core/CardStats). Create one with right-click > Create > Halobellum > Card.
// Depends on: Halobellum.Core (CardStats, ItemStats), UnityEngine.

using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Units
{
    [CreateAssetMenu(menuName = "Halobellum/Card", fileName = "NewCard")]
    public sealed class CardDefinition : ScriptableObject
    {
        public string cardName;
        [TextArea] public string description;
        [Tooltip("Item, Economy, Buff or Training (spells have their own assets, Data/Spells).")]
        public CardType type = CardType.Item;
        [Tooltip("Shown on the card, in the Inventory and over the squad (Art/UI/Items).")]
        public Texture2D icon;

        [Header("Card")]
        [Tooltip("Credits paid when the card is taken.")]
        public int cost;
        [Tooltip("How many times a player can take it.")]
        public int maxOwnedCopies = 1;
        [Tooltip("The first round it can be offered.")]
        public int minRound;

        [Header("Item (affects the squad carrying it)")]
        public float healthMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float moveSpeedMultiplier = 1f;
        [Tooltip("Metres added to every weapon's range.")]
        public float rangeBonus;
        [Tooltip("Share of the damage dealt that comes back as health.")]
        public float lifestealShare;
        [Tooltip("Share of its maximum health recovered per second.")]
        public float healPerSecondShare;
        [Tooltip("The squad can be moved during every deployment phase.")]
        public bool freeDeployment;

        [Header("Economy and buffs (for the whole army)")]
        [Tooltip("Extra credits every round.")]
        public int bonusIncome;
        public float allDamageMultiplier = 1f;
        public float allHealthMultiplier = 1f;
        [Tooltip("Metres added to the weapons of all ranged units.")]
        public float allRangeBonus;
        [Tooltip("More squads can be deployed per round.")]
        public int extraSquadsPerRound;

        [Header("Training")]
        [Tooltip("Rounds it recharges after use.")]
        public int cooldownRounds;

        public CardStats ToStats()
        {
            string title = string.IsNullOrEmpty(cardName) ? name : cardName;
            var card = new CardStats
            {
                Name = title,
                Description = description ?? "",
                Type = type,
                Cost = cost,
                MaxOwnedCopies = maxOwnedCopies,
                MinRound = minRound,
                BonusIncome = bonusIncome,
                AllDamageMultiplier = allDamageMultiplier,
                AllHealthMultiplier = allHealthMultiplier,
                AllRangeBonus = allRangeBonus,
                ExtraSquadsPerRound = extraSquadsPerRound,
                CooldownRounds = cooldownRounds,
            };
            if (type == CardType.Item)
            {
                card.Item = new ItemStats
                {
                    Name = title,
                    Description = description ?? "",
                    HealthMultiplier = healthMultiplier,
                    DamageMultiplier = damageMultiplier,
                    MoveSpeedMultiplier = moveSpeedMultiplier,
                    RangeBonus = rangeBonus,
                    LifestealShare = lifestealShare,
                    HealPerSecondShare = healPerSecondShare,
                    FreeDeployment = freeDeployment,
                };
            }
            return card;
        }
    }
}
