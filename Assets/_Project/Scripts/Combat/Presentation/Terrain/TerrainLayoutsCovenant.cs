// TerrainLayoutsCovenant.cs
// The Covenant's two layouts (M10). Seam Flats: smooth violet rock split by thin glowing cyan seams, drifted with pale sand, ringed by crystal shards and curved pylons. Sanctum: a built site: dark polished plates set in great
// rings with magenta light, with tall curved gates, obelisks and stepped daises around it. All shapes are original and only meant to feel alien.
// Looks only. Depends on: TerrainLayout, TerrainProps, TerrainCanvas, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class SeamFlatsLayout : TerrainLayout
    {
        private static readonly Color32 Cyan = new Color32(90, 232, 255, 255);
        private static readonly Color32 CrystalBody = new Color32(84, 92, 166, 90);
        private static readonly Color32 CrystalTip = new Color32(170, 246, 255, 255);
        private static readonly Color32 Metal = new Color32(92, 80, 134, 0);
        private static readonly Color32 PylonGlow = new Color32(120, 240, 255, 255);

        public override string Name => "Seam Flats";

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float plates = TerrainNoise.Fbm((x + seedX) / 48f, (z + seedZ) / 48f, 3);
            second = TerrainNoise.Smooth(0.54f, 0.62f, plates) * 0.9f;
            float drifts = TerrainNoise.Fbm((x * 0.6f - seedZ) / 36f, (z + seedX) / 18f, 3);
            third = TerrainNoise.Smooth(0.56f, 0.68f, drifts) * 0.85f;
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            var path = new List<Vector2>();
            var branch = new List<Vector2>();
            Color32 groove = C(26, 18, 44);

            // Seams run in from the outer land across the floor, forking as they go.
            for (int i = 0; i < 7; i++)
            {
                float heading = (c.Side > 0 ? 3.14f : 0f) + r.Range(-0.55f, 0.55f);
                TerrainCanvas.Wander(path, r, new Vector2(r.Range(-W, W), c.Z(E + 26f)), heading, 7f, 16, 0.34f);
                TrimInner(path, 8f);
                float width = r.Range(0.22f, 0.36f);
                Seam(c, path, Cyan, width, r.Range(0f, 6.28f), groove);
                int forks = 1 + r.Int(3);
                for (int f = 0; f < forks && path.Count > 4; f++)
                {
                    Vector2 from = path[1 + r.Int(path.Count - 2)];
                    float side = heading + r.Sign() * r.Range(0.5f, 1.1f);
                    TerrainCanvas.Wander(branch, r, from, side, 5f, 5 + r.Int(6), 0.42f);
                    TrimInner(branch, 8f);
                    Seam(c, branch, Cyan, width * 0.6f, r.Range(0f, 6.28f), groove);
                }
            }

            // Small pits of sand and shards flat in the rock.
            for (int i = 0; i < 26; i++)
                Stain(c, r.Range(-78f, 78f), c.Z(r.Range(8f, 80f)), r.Range(1.5f, 5.5f), C(24, 18, 40, 110));

            // Curved pylons in a row down each side and along the back.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 14f; v < E + 10f; v += 21f)
                    TerrainProps.CurvedPylon(c, r, k * (W + 7f), c.Z(v), k * 90f, 17f + r.Range(-2f, 3f), Metal, PylonGlow);
            }
            for (float x = -W + 12f; x < W; x += 26f)
                TerrainProps.CurvedPylon(c, r, x, c.Z(E + (c.Side > 0 ? 7f : 16f)), c.Side > 0 ? 180f : 0f, c.Side > 0 ? 18f : 11f, Metal, PylonGlow);

            // Crystal clusters near and far.
            for (int i = 0; i < 34; i++)
            {
                AroundArena(c, r, 5f, 90f, out float x, out float z);
                TerrainProps.CrystalCluster(c, r, x, z, r.Range(0.7f, 2.2f), CrystalTip, CrystalBody);
            }
            for (int i = 0; i < 24; i++)
            {
                AroundArena(c, r, 60f, 360f, out float x, out float z);
                TerrainProps.CrystalCluster(c, r, x, z, r.Range(2f, 4.5f), CrystalTip, CrystalBody);
            }

            // Fins of rock breaking the land.
            Color32 fin = C(78, 66, 106);
            for (int i = 0; i < 46; i++)
            {
                AroundArena(c, r, 16f, 260f, out float x, out float z);
                TerrainProps.RockFin(c, r, x, z, r.Range(0f, 360f), r.Range(5f, 18f), fin);
            }
        }

        private static void TrimInner(List<Vector2> points, float inner)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (Mathf.Abs(points[i].y) >= inner) continue;
                points.RemoveRange(i, points.Count - i);
                return;
            }
        }
    }

    public sealed class SanctumLayout : TerrainLayout
    {
        private static readonly Color32 Magenta = new Color32(255, 70, 214, 255);
        private static readonly Color32 Metal = new Color32(62, 50, 96, 0);
        private static readonly Color32 PaleMetal = new Color32(110, 96, 150, 0);
        private static readonly Color32 CrystalBody = new Color32(120, 60, 130, 90);
        private static readonly Color32 CrystalTip = new Color32(255, 150, 240, 255);

        public override string Name => "Sanctum";
        public override float FlatBand => 26f;

        public override TerrainTheme Mood(TerrainTheme theme)
        {
            TerrainTheme mood = theme.Clone();
            mood.SkyTop = new Color(0.12f, 0.05f, 0.24f);
            mood.SkyHorizon = new Color(0.72f, 0.36f, 0.62f);
            mood.FogColor = new Color(0.55f, 0.3f, 0.52f);
            mood.Cloud = new Color(0.8f, 0.42f, 0.66f);
            mood.SunAngles = new Vector2(46f, -20f);
            mood.MoteColor = new Color(1f, 0.5f, 0.9f);
            return mood;
        }

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float plates = TerrainNoise.Fbm((x + seedX) / 60f, (z + seedZ) / 60f, 2);
            second = TerrainNoise.Smooth(0.3f, 0.44f, plates) * (1f - TerrainNoise.Smooth(0f, 60f, outside) * 0.8f);
            float drifts = TerrainNoise.Fbm((x * 0.5f - seedZ) / 44f, (z + seedX) / 22f, 3);
            third = TerrainNoise.Smooth(0.62f, 0.74f, drifts) * 0.7f;
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;

            // The great circle in the middle of the zone, and a smaller one either side.
            Rings(c, r, 0f, c.Z(44f), 34f, 3);
            for (int k = -1; k <= 1; k += 2) Rings(c, r, k * 62f, c.Z(26f), 13f, 2);
            for (int k = -1; k <= 1; k += 2) Rings(c, r, k * 62f, c.Z(66f), 13f, 2);

            // Straight paths of light joining them.
            c.Glow.Uv = new Vector2(0.25f, r.Range(0f, 6.28f));
            for (int k = -1; k <= 1; k += 2)
            {
                float mid = c.Z(46f);
                c.Decals.Uv = new Vector2(0f, 0.7f);
                c.Decals.FlatRect(D, k * 62f, mid, 5f, 14f, 0f, C(50, 40, 76));
                c.Decals.Uv = Vector2.zero;
                c.Glow.FlatRect(G, k * 62f, mid, 0.25f, 14f, 0f, new Color32(Magenta.r, Magenta.g, Magenta.b, 190));
            }
            c.Glow.Uv = Vector2.zero;

            // Tall curved gates along each side.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 18f; v < E + 12f; v += 36f)
                    TerrainProps.Arch(c, r, k * (W + 17f), c.Z(v), 90f, 34f + r.Range(-3f, 4f), Metal, Magenta);
            }
            if (c.Side > 0)
            {
                TerrainProps.Arch(c, r, 0f, c.Z(E + 26f), 0f, 56f, Metal, Magenta);
                TerrainProps.Dais(c, r, r.Range(-W, -30f), c.Z(E + 24f), 13f, PaleMetal, Magenta);
                TerrainProps.Dais(c, r, r.Range(30f, W), c.Z(E + 24f), 13f, PaleMetal, Magenta);
            }
            else
            {
                TerrainProps.Dais(c, r, r.Range(-W, -30f), c.Z(E + 18f), 11f, PaleMetal, Magenta);
                TerrainProps.Dais(c, r, r.Range(30f, W), c.Z(E + 18f), 11f, PaleMetal, Magenta);
            }

            // Obelisks in lines around the field.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 6f; v < E + 8f; v += 18f)
                    TerrainProps.Obelisk(c, r, k * (W + 5.5f), c.Z(v), 8f, Metal, Magenta);
            }
            for (float x = -W + 8f; x < W; x += 20f)
                TerrainProps.Obelisk(c, r, x, c.Z(E + (c.Side > 0 ? 5.5f : 12f)), c.Side > 0 ? 8f : 5f, Metal, Magenta);

            // Beyond: wild crystals, low walls and fins.
            for (int i = 0; i < 26; i++)
            {
                AroundArena(c, r, 30f, 300f, out float x, out float z);
                TerrainProps.CrystalCluster(c, r, x, z, r.Range(1.2f, 4f), CrystalTip, CrystalBody);
            }
            Color32 fin = C(70, 56, 98);
            for (int i = 0; i < 34; i++)
            {
                AroundArena(c, r, 30f, 300f, out float x, out float z);
                TerrainProps.RockFin(c, r, x, z, r.Range(0f, 360f), r.Range(6f, 22f), fin);
            }
            for (int i = 0; i < 8; i++)
            {
                AroundArena(c, r, 40f, 200f, out float x, out float z);
                TerrainProps.CurvedPylon(c, r, x, z, r.Range(0f, 360f), r.Range(24f, 44f), Metal, Magenta);
            }
        }

        private void Rings(TerrainCanvas c, TerrainRandom r, float x, float z, float radius, int count)
        {
            float phase = r.Range(0f, 6.28f);
            c.Decals.Uv = new Vector2(0f, 0.7f);
            for (int i = 0; i < count; i++)
            {
                float outer = radius * (1f - i / (float)count);
                float inner = outer - radius * 0.14f;
                int pieces = 10 + (count - i) * 4;
                float each = 360f / pieces;
                for (int p = 0; p < pieces; p++)
                {
                    float from = p * each + 0.6f;
                    float tone = (r.Value() - 0.5f) * 12f;
                    c.Decals.FlatRing(D, x, z, inner, outer, C(Mathf.RoundToInt(62 + tone), Mathf.RoundToInt(50 + tone), Mathf.RoundToInt(92 + tone)), 4, from, from + each - 1.2f);
                }
            }
            c.Decals.Uv = Vector2.zero;
            c.Glow.Uv = new Vector2(0.3f, phase);
            for (int i = 0; i < count; i++)
            {
                float outer = radius * (1f - i / (float)count);
                float line = i == 0 ? 0.22f : 0.14f;
                c.Glow.FlatRing(G, x, z, outer - line, outer + line, new Color32(Magenta.r, Magenta.g, Magenta.b, 215), 56);
                c.Glow.FlatRing(G, x, z, outer + line, outer + line + 1.8f, new Color32(Magenta.r, Magenta.g, Magenta.b, 40), 56, 0f, 360f, new Color32(Magenta.r, Magenta.g, Magenta.b, 0));
                c.Glow.FlatRing(G, x, z, outer - line - 1.8f, outer - line, new Color32(Magenta.r, Magenta.g, Magenta.b, 0), 56, 0f, 360f, new Color32(Magenta.r, Magenta.g, Magenta.b, 40));
            }
            c.Glow.FlatDisc(G, x, z, radius * 0.11f, radius * 0.11f, new Color32(Magenta.r, Magenta.g, Magenta.b, 120), new Color32(Magenta.r, Magenta.g, Magenta.b, 0), 12);
            c.Glow.Uv = Vector2.zero;
        }
    }
}
