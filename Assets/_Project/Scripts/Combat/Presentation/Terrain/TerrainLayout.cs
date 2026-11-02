// TerrainLayout.cs
// One way of laying out one faction's half of the field (M10): which grounds lie where, what is painted on the floor, what stands around it. Each faction has three, and the match seed picks one
// for each side, so that there are more maps. Layouts only draw on a TerrainCanvas; each works on either side of the field, since the canvas says which side it is on.
// Looks only: nothing here touches the simulation. Depends on: TerrainCanvas, TerrainTheme, BattleFactions, Halobellum.Core (Team), UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public abstract class TerrainLayout
    {
        protected const float D = TerrainCanvas.DecalHeight;
        protected const float G = TerrainCanvas.GlowHeight;

        public abstract string Name { get; }

        /// <summary>Metres of level land kept around the arena before the hills begin.</summary>
        public virtual float FlatBand => 16f;

        /// <summary>Adjusts the faction's light and sky for this layout, when it is the player's.</summary>
        public virtual TerrainTheme Mood(TerrainTheme theme) => theme;

        /// <summary>How much of the second and third ground lies at a point, before anything is painted on. Called from several threads, so it must only read its arguments.</summary>
        public abstract void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third);

        /// <summary>Paints the floor and puts up everything around it, on the canvas's current side.</summary>
        public abstract void Furnish(TerrainCanvas c, TerrainRandom r);

        // ------------------------------------------------------------------ shared by layouts

        protected static Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

        protected static Color32 Grey(TerrainRandom r, int low, int high, int warm = 0)
        {
            int v = low + r.Int(high - low + 1);
            return new Color32((byte)Mathf.Clamp(v + warm, 0, 255), (byte)Mathf.Clamp(v, 0, 255), (byte)Mathf.Clamp(v - warm, 0, 255), 255);
        }

        /// <summary>A soft dark stain on the floor.</summary>
        protected static void Stain(TerrainCanvas c, float x, float z, float radius, Color32 color)
        {
            c.Decals.Uv = new Vector2(0f, 0.5f);
            c.Decals.FlatDisc(D, x, z, radius, radius * 0.8f, color, new Color32(color.r, color.g, color.b, 0), 10, x * 17f);
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>A shell crater: a burnt scorch with a darker bowl and a lighter thrown-up rim.</summary>
        protected static void Crater(TerrainCanvas c, float x, float z, float radius, Color32 rim)
        {
            c.Decals.Uv = new Vector2(0f, 0.7f);
            c.Decals.FlatDisc(D, x, z, radius * 1.9f, radius * 1.7f, new Color32(22, 18, 16, 170), new Color32(22, 18, 16, 0), 14, x);
            c.Decals.FlatRing(D, x, z, radius * 0.7f, radius, new Color32(rim.r, rim.g, rim.b, 0), 14, 0f, 360f, new Color32(rim.r, rim.g, rim.b, 130));
            c.Decals.FlatRing(D, x, z, radius, radius * 1.3f, new Color32(rim.r, rim.g, rim.b, 130), 14, 0f, 360f, new Color32(rim.r, rim.g, rim.b, 0));
            c.Decals.FlatDisc(D, x, z, radius * 0.72f, radius * 0.66f, new Color32(12, 10, 10, 150), new Color32(12, 10, 10, 60), 10, x);
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>A thin glowing seam in dark rock.</summary>
        protected static void Seam(TerrainCanvas c, List<Vector2> points, Color32 glow, float width, float phase, Color32 groove)
        {
            if (points.Count < 2) return;
            c.Decals.Uv = Vector2.zero;
            c.Decals.FlatRibbon(D, points, width * 4f, width * 1.6f, new Color32(groove.r, groove.g, groove.b, 200), true);
            c.Decals.FlatRibbon(D, points, width * 1.3f, width * 0.5f, new Color32(groove.r, groove.g, groove.b, 255));
            c.Glow.Uv = new Vector2(0.35f, phase);
            c.Glow.FlatRibbon(G, points, width * 9f, width * 3f, new Color32(glow.r, glow.g, glow.b, 46), true);
            c.Glow.FlatRibbon(G, points, width * 0.8f, width * 0.28f, new Color32(glow.r, glow.g, glow.b, 235));
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>A vein of the Flood across the floor, swollen at its root.</summary>
        protected static void Vein(TerrainCanvas c, List<Vector2> points, float width)
        {
            if (points.Count < 2) return;
            c.Decals.Uv = new Vector2(0f, 0.3f);
            c.Decals.FlatRibbon(D, points, width * 2.6f, width * 0.8f, C(22, 12, 10, 170), true);
            c.Decals.FlatRibbon(D, points, width, width * 0.25f, C(74, 34, 36, 255));
            c.Decals.FlatRibbon(D, points, width * 0.45f, width * 0.1f, C(150, 88, 70, 255));
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>A flat pustule on the floor: a pale, wet head in a dark ring, with a faint glow.</summary>
        protected static void Pustule(TerrainCanvas c, TerrainRandom r, float x, float z, float radius)
        {
            c.Decals.Uv = Vector2.zero;
            c.Decals.FlatDisc(D, x, z, radius * 1.7f, radius * 1.55f, C(24, 16, 10, 210), C(24, 16, 10, 0), 10);
            c.Decals.FlatDisc(D, x, z, radius, radius * 0.92f, C(196, 196, 96), C(118, 110, 46), 10);
            c.Decals.FlatDisc(D, x - radius * 0.2f, z + radius * 0.25f, radius * 0.4f, radius * 0.34f, C(240, 240, 176), C(196, 196, 96, 0), 8);
            c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
            c.Glow.FlatDisc(G, x, z, radius * 2.5f, radius * 2.4f, C(170, 190, 60, 60), C(170, 190, 60, 0), 8);
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>Random point in this layout's land beyond the arena: x either side, v out from the middle line.</summary>
        protected static void AroundArena(TerrainCanvas c, TerrainRandom r, float near, float far, out float x, out float z)
        {
            // Half of it to the sides, half beyond the far edge.
            if (r.Chance(0.55f))
            {
                x = r.Sign() * (c.HalfWidth + r.Range(near, far));
                z = c.Z(r.Range(0f, c.EdgeV + far));
            }
            else
            {
                x = r.Range(-c.HalfWidth - far, c.HalfWidth + far);
                z = c.Z(c.EdgeV + r.Range(near, far));
            }
        }
    }

    /// <summary>Which layouts each faction has, and how a seed picks one.</summary>
    public static class TerrainLayouts
    {
        private static readonly TerrainLayout[] UnscLayouts = { new FirebaseLayout(), new ColonyLayout(), new QuarryLayout() };
        private static readonly TerrainLayout[] CovenantLayouts = { new SeamFlatsLayout(), new SanctumLayout(), new CrystalGardenLayout() };
        private static readonly TerrainLayout[] FloodLayouts = { new CreepLayout(), new WreckLayout(), new HiveLayout() };

        public static IReadOnlyList<TerrainLayout> For(string faction)
        {
            if (faction == BattleFactions.Covenant) return CovenantLayouts;
            if (faction == BattleFactions.Flood) return FloodLayouts;
            return UnscLayouts;
        }

        /// <summary>Which of a faction's layouts this seed gives to this side. Each side picks on its own.</summary>
        public static int Pick(string faction, int seed, Team team)
        {
            uint hash = TerrainRandom.Mix((uint)seed * 2654435761u + (team == Team.A ? 11u : 47u));
            return (int)(hash % (uint)For(faction).Count);
        }
    }
}
