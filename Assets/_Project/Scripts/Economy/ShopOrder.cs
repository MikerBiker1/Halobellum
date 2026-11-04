// ShopOrder.cs
// The order units are listed in for the player: by tier, then by price. Units equal in both keep the order they came in, which
// List.Sort would not promise. It sorts on the base Cost, so Elite Recruitment's higher price can't reorder a list.
// Depends on: Halobellum.Core (UnitStats).

using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public static class ShopOrder
    {
        /// <summary>Sorts the list in place by tier, then by price; ties keep their incoming order.</summary>
        public static void Sort(List<UnitStats> units)
        {
            for (int i = 1; i < units.Count; i++)
            {
                UnitStats unit = units[i];
                int at = i - 1;
                while (at >= 0 && ComesBefore(unit, units[at]))
                {
                    units[at + 1] = units[at];
                    at--;
                }
                units[at + 1] = unit;
            }
        }

        private static bool ComesBefore(UnitStats a, UnitStats b) => a.Tier != b.Tier ? a.Tier < b.Tier : a.Cost < b.Cost;
    }
}
