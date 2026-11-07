// SandboxMaps.cs
// The maps the Sandbox can be set to (the owner, 2026-11-07, D82: the outdoor maps only in the Sandbox, "where you should change maps as you wish").
// Matches always fight in an interior look (D80, TerrainLooks.Pick); in the Sandbox the Map button goes through this list:
//   - As in a match: the look a match between the armies' factions would get, as before.
//   - Each of the four interior looks.
//   - Outdoor, the armies' lands: the outdoor map of D47, each half its own army's faction's land, the layouts picked by the seed.
//   - Each of the nine outdoor layouts, on both halves, in its faction's land.
// Looks only. Depends on: TerrainLooks, TerrainLayouts, BattleFactions.

using System.Collections.Generic;

namespace Halobellum.Presentation
{
    public sealed class SandboxMap
    {
        /// <summary>What the Map button says.</summary>
        public string Name;
        /// <summary>An interior look, or null for an outdoor map (or for "as in a match", which has neither).</summary>
        public TerrainLook Look;
        public bool Outdoor;
        /// <summary>For one outdoor layout: whose land it is, on both halves, and which of that land's layouts. Null land: each army's own, by the seed.</summary>
        public string Land;
        public int Layout = -1;
    }

    public static class SandboxMaps
    {
        private static List<SandboxMap> _all;

        /// <summary>The list, in the order the Map button goes through it. The first is "as in a match".</summary>
        public static IReadOnlyList<SandboxMap> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<SandboxMap>
                {
                    new SandboxMap { Name = "As in a match" },
                    new SandboxMap { Name = "UNSC Installation", Look = TerrainLooks.UnscInstallation },
                    new SandboxMap { Name = "Covenant Warship", Look = TerrainLooks.CovenantWarship },
                    new SandboxMap { Name = "Flood, UNSC ship", Look = TerrainLooks.FloodOnUnsc },
                    new SandboxMap { Name = "Flood, Covenant ship", Look = TerrainLooks.FloodOnCovenant },
                    new SandboxMap { Name = "Outdoor, both lands", Outdoor = true },
                };
                foreach (string land in new[] { BattleFactions.Unsc, BattleFactions.Covenant, BattleFactions.Flood })
                {
                    IReadOnlyList<TerrainLayout> layouts = TerrainLayouts.For(land);
                    for (int i = 0; i < layouts.Count; i++) _all.Add(new SandboxMap { Name = "Outdoor, " + layouts[i].Name, Outdoor = true, Land = land, Layout = i });
                }
                return _all;
            }
        }

        /// <summary>The map at this place in the list, wrapping around; null for "as in a match".</summary>
        public static SandboxMap At(int index)
        {
            int count = All.Count;
            int wrapped = ((index % count) + count) % count;
            return wrapped == 0 ? null : All[wrapped];
        }

        /// <summary>The Map button's name for this place in the list.</summary>
        public static string NameOf(int index)
        {
            int count = All.Count;
            return All[((index % count) + count) % count].Name;
        }
    }
}
