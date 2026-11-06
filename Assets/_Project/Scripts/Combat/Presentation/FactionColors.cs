// FactionColors.cs
// The accent colour of each faction, in one place: the HUD's fallback icons, card tints and sub-faction banners (UnitIcons), and the
// lights of each side's towers (TowerArt). A sub-faction wears its parent faction's colour. Looks only.
// Depends on: BattleFactions, Halobellum.Core (Team), UnityEngine.

using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class FactionColors
    {
        public static readonly Color Unsc = new Color(0.9f, 0.68f, 0.16f);
        public static readonly Color Covenant = new Color(0.62f, 0.42f, 1f);
        public static readonly Color Flood = new Color(0.72f, 0.8f, 0.24f);
        /// <summary>Anything without a faction: placeholders, and both sides of an Unlock Skirmish.</summary>
        public static readonly Color Neutral = new Color(0.6f, 0.68f, 0.76f);

        /// <summary>A faction's colour by its name (UNSC, Covenant, Flood), or the neutral one.</summary>
        public static Color Of(string faction)
        {
            switch (faction)
            {
                case BattleFactions.Unsc: return Unsc;
                case BattleFactions.Covenant: return Covenant;
                case BattleFactions.Flood: return Flood;
                default: return Neutral;
            }
        }

        /// <summary>
        /// The colour of a side's towers in the shown battle: its faction's. In Unlock Skirmish both sides stand on one land and neither has a
        /// faction of its own, so their towers are neutral.
        /// </summary>
        public static Color OfTowers(Team team) => BattleFactions.PerUnitLooks ? Neutral : Of(BattleFactions.For(team));
    }
}
