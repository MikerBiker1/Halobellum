// TerrainLook.cs
// The interior looks of the battlefield (D80): one look for the whole field in a match, a deck inside a ship or a base, with its own ground, light, mood and
// surroundings. A UNSC military installation, a Covenant warship, and a ship the Flood has taken, built over UNSC or Covenant architecture.
// Which match gets which look (TerrainLooks.Pick), from the two factions and the match seed, so it never changes during a match:
//   - The same faction on both sides (this includes Unlock Skirmish, whose land the seed picks for both teams): that faction's look.
//   - Flood against UNSC: the Flood ship on a UNSC hull. Flood against Covenant: on a Covenant hull. Flood against Flood: the seed picks the hull.
//   - UNSC against Covenant: the seed picks the UNSC installation or the Covenant warship, half and half.
// The outdoor maps of D47 (TerrainLayouts) stay in the code, and the terrain gallery can still show them with -terrainOutdoor.
// Looks only: nothing here touches the simulation. Depends on: TerrainTheme, TerrainLayout, TerrainRandom, BattleFactions, and the three look files
// (TerrainInteriorUnsc.cs, TerrainInteriorCovenant.cs, TerrainInteriorFlood.cs).

using System.Collections.Generic;

namespace Halobellum.Presentation
{
    /// <summary>One interior look: what the whole field is made of.</summary>
    public sealed class TerrainLook
    {
        /// <summary>Short and fixed, for logs, tests and picture names.</summary>
        public string Id;
        /// <summary>What a player would call it.</summary>
        public string Name;
        /// <summary>The ground, light, sky (here a dark ceiling), air and mood. Its hills must be TerrainHillStyle.Flat.</summary>
        public TerrainTheme Theme;
        /// <summary>Furnishes both halves of the field (Side -1 then +1), then NoMansLand once.</summary>
        public TerrainLayout Layout;
    }

    public enum FloodHull
    {
        Unsc,
        Covenant,
    }

    public static class TerrainLooks
    {
        public static readonly TerrainLook UnscInstallation = UnscInterior.Create();
        public static readonly TerrainLook CovenantWarship = CovenantInterior.Create();
        public static readonly TerrainLook FloodOnUnsc = FloodInterior.Create(FloodHull.Unsc);
        public static readonly TerrainLook FloodOnCovenant = FloodInterior.Create(FloodHull.Covenant);

        public static readonly IReadOnlyList<TerrainLook> All = new[] { UnscInstallation, CovenantWarship, FloodOnUnsc, FloodOnCovenant };

        /// <summary>The look of a match between these factions with this seed. Anything unknown counts as the UNSC.</summary>
        public static TerrainLook Pick(string factionA, string factionB, int seed)
        {
            string a = Known(factionA), b = Known(factionB);
            bool coin = (TerrainRandom.Mix((uint)seed * 2246822519u + 977u) & 1u) == 0u;
            bool floodA = a == BattleFactions.Flood, floodB = b == BattleFactions.Flood;
            if (floodA && floodB) return coin ? FloodOnUnsc : FloodOnCovenant;
            if (floodA || floodB) return (floodA ? b : a) == BattleFactions.Covenant ? FloodOnCovenant : FloodOnUnsc;
            if (a == b) return LookOf(a);
            return coin ? UnscInstallation : CovenantWarship;
        }

        public static TerrainLook ById(string id)
        {
            foreach (TerrainLook look in All)
                if (look.Id == id) return look;
            return null;
        }

        private static TerrainLook LookOf(string faction) => faction == BattleFactions.Covenant ? CovenantWarship : UnscInstallation;

        private static string Known(string faction) =>
            faction == BattleFactions.Covenant || faction == BattleFactions.Flood ? faction : BattleFactions.Unsc;
    }
}
