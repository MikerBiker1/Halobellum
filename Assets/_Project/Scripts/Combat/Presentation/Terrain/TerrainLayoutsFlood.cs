// TerrainLayoutsFlood.cs
// The Flood's three layouts (M10). Creep: living biomass over everything, veined, dotted with pustules and rimmed with tendrils and swollen pods. Wreck: a place the Flood has taken: brown rot and pools of slime over the broken
// concrete of a dead base, with rusted containers, strangled masts and the great ribs of a hull. Hive: raw sinew in deep red-brown under a bruised sky, cut by trenches of rot, with tall ribbed spires standing over it. All original shapes.
// The floor stays mid-toned on purpose: pale Infection Forms and dark Flood units both have to show on it.
// Looks only. Depends on: TerrainLayout, TerrainProps, TerrainCanvas, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class CreepLayout : TerrainLayout
    {
        private static readonly Color32 Skin = new Color32(112, 112, 56, 0);
        private static readonly Color32 Pale = new Color32(186, 176, 102, 0);
        private static readonly Color32 RootColor = new Color32(84, 62, 44, 0);
        private static readonly Color32 PodGlow = new Color32(226, 226, 130, 60);

        public override string Name => "Creep";

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float rot = TerrainNoise.Fbm((x + seedX) / 30f, (z + seedZ) / 30f, 3);
            second = TerrainNoise.Smooth(0.56f, 0.66f, rot) * 0.9f;
            float sinew = TerrainNoise.Fbm((x - seedZ) / 42f, (z + seedX) / 42f, 3);
            third = TerrainNoise.Smooth(0.6f, 0.68f, sinew) * 0.85f;
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            var path = new List<Vector2>();
            var branch = new List<Vector2>();

            // Nests of pustules with veins spreading from them over the floor.
            for (int nest = 0; nest < 6; nest++)
            {
                float nx = r.Range(-74f, 74f), nz = c.Z(r.Range(14f, 78f));
                int veins = 3 + r.Int(3);
                for (int v = 0; v < veins; v++)
                {
                    TerrainCanvas.Wander(path, r, new Vector2(nx, nz), r.Range(0f, 6.28f), 4f, 7 + r.Int(10), 0.42f);
                    c.KeepOnFloor(path, 0.5f);
                    float width = r.Range(0.4f, 0.85f);
                    Vein(c, path, width);
                    if (path.Count > 4)
                    {
                        TerrainCanvas.Wander(branch, r, path[2 + r.Int(path.Count - 3)], r.Range(0f, 6.28f), 3f, 3 + r.Int(6), 0.5f);
                        c.KeepOnFloor(branch, 0.5f);
                        Vein(c, branch, width * 0.5f);
                    }
                }
                c.PaintDisc(nx, nz, r.Range(7f, 12f), 2, 0.8f);
                int heads = 3 + r.Int(4);
                for (int h = 0; h < heads; h++)
                    Pustule(c, r, nx + r.Range(-5f, 5f), nz + r.Range(-5f, 5f), r.Range(0.5f, 1.3f));
            }

            // Stray heads and dark slick patches.
            for (int i = 0; i < 14; i++) Pustule(c, r, r.Range(-78f, 78f), c.Z(r.Range(6f, 80f)), r.Range(0.3f, 0.7f));
            for (int i = 0; i < 22; i++) Stain(c, r.Range(-78f, 78f), c.Z(r.Range(6f, 80f)), r.Range(1.5f, 5f), C(30, 22, 12, 120));

            // A wall of biomass around the field, low and lumpy, with tendrils over it.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 3f; v < E + 6f; v += 8f)
                    if (r.Chance(0.8f)) TerrainProps.Mound(c, r, k * (W + r.Range(4.5f, 8f)), c.Z(v), r.Range(2f, 3.6f), Skin, Pale);
            }
            for (float x = -W; x < W + 1f; x += 8f)
                if (r.Chance(0.8f)) TerrainProps.Mound(c, r, x, c.Z(E + r.Range(c.Side > 0 ? 4.5f : 7.5f, 9f)), r.Range(1.8f, c.Side > 0 ? 3.6f : 2.6f), Skin, Pale);

            // Arching tendrils and rearing ones.
            for (int i = 0; i < 26; i++)
            {
                AroundArena(c, r, 11f, 70f, out float x, out float z);
                TerrainProps.Tendril(c, r, x, z, r.Range(0f, 360f), r.Range(14f, 38f), r.Range(5f, 16f), r.Range(0.7f, 1.7f), RootColor, Pale);
            }
            for (int i = 0; i < 30; i++)
            {
                AroundArena(c, r, 10f, 90f, out float x, out float z);
                TerrainProps.RearingTendril(c, r, x, z, r.Range(6f, 18f), r.Range(0.5f, 1.2f), RootColor, Pale);
            }

            // Spore pods.
            for (int i = 0; i < 26; i++)
            {
                AroundArena(c, r, 11f, 100f, out float x, out float z);
                TerrainProps.Pod(c, r, x, z, r.Range(1.3f, 3.4f), Skin, PodGlow);
            }

            // Far: bigger tendrils, like the land itself reaching up.
            for (int i = 0; i < 24; i++)
            {
                AroundArena(c, r, 80f, 420f, out float x, out float z);
                TerrainProps.Tendril(c, r, x, z, r.Range(0f, 360f), r.Range(50f, 130f), r.Range(22f, 54f), r.Range(2.4f, 5f), RootColor, Pale);
            }
            for (int i = 0; i < 30; i++)
            {
                AroundArena(c, r, 60f, 300f, out float x, out float z);
                TerrainProps.Mound(c, r, x, z, r.Range(4f, 11f), Skin, Pale);
            }
        }
    }

    public sealed class WreckLayout : TerrainLayout
    {
        private static readonly Color32 Skin = new Color32(98, 92, 52, 0);
        private static readonly Color32 Pale = new Color32(168, 158, 92, 0);
        private static readonly Color32 Rust = new Color32(92, 66, 48, 0);
        private static readonly Color32 PaleRust = new Color32(126, 104, 80, 0);
        private static readonly Color32 PodGlow = new Color32(226, 226, 110, 90);

        public override string Name => "Wreck";
        public override float FlatBand => 22f;

        public override TerrainTheme Mood(TerrainTheme theme)
        {
            TerrainTheme mood = theme.Clone();
            mood.SkyTop = new Color(0.1f, 0.08f, 0.06f);
            mood.SkyHorizon = new Color(0.42f, 0.36f, 0.2f);
            mood.FogColor = new Color(0.34f, 0.31f, 0.18f);
            mood.Haze = new Color(0.5f, 0.44f, 0.26f);
            mood.SunColor = new Color(0.95f, 0.86f, 0.62f);
            mood.SunAngles = new Vector2(52f, -14f);
            return mood;
        }

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float rot = TerrainNoise.Fbm((x + seedX) / 34f, (z + seedZ) / 34f, 3);
            second = TerrainNoise.Smooth(0.34f, 0.44f, rot); // Mostly rot, with living mass in between.
            float sinew = TerrainNoise.Fbm((x - seedZ) / 28f, (z + seedX) / 28f, 3);
            third = TerrainNoise.Smooth(0.66f, 0.74f, sinew) * 0.8f;
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            var path = new List<Vector2>();

            // What's left of the concrete: scattered, tilted-looking slabs in rows that have drifted.
            for (int ix = 0; ix < 20; ix++)
            {
                for (int iv = 0; iv < 9; iv++)
                {
                    float x = -76f + 8f * (ix + 0.5f);
                    float v = 10f + 8f * (iv + 0.5f);
                    float keep = TerrainNoise.Fbm((x + 400f) / 22f + r.Range(0f, 0.2f), (c.Z(v) + 300f) / 22f, 2);
                    if (keep < 0.52f || r.Chance(0.25f)) continue;
                    c.Decals.Uv = new Vector2(0f, 0.95f);
                    float size = r.Range(5f, 7.6f);
                    c.Decals.FlatRect(D, x + r.Range(-0.6f, 0.6f), c.Z(v) + r.Range(-0.6f, 0.6f), size, size * r.Range(0.75f, 1f), r.Range(-7f, 7f), Grey(r, 96, 122, 6));
                    if (r.Chance(0.5f))
                    {
                        // Biomass creeping across a slab's corner.
                        c.Decals.Uv = new Vector2(0f, 0.4f);
                        c.Decals.FlatDisc(D, x + r.Range(-3f, 3f), c.Z(v) + r.Range(-3f, 3f), r.Range(2f, 3.6f), r.Range(1.8f, 3f), C(62, 62, 28, 220), C(62, 62, 28, 0), 9, r.Range(0f, 90f));
                    }
                }
            }
            c.Decals.Uv = Vector2.zero;

            // Pools of slime: wet and olive, in overlapping uneven blobs, with a faint sheen. Mid-toned and see-through: a dark form must show on them, and they must not look like vehicles.
            for (int i = 0; i < 9; i++)
            {
                float x = r.Range(-70f, 70f), z = c.Z(r.Range(12f, 76f));
                float radius = r.Range(2.2f, 5.5f);
                for (int lobe = 0; lobe < 3; lobe++)
                {
                    float lx = x + r.Range(-radius, radius) * 0.7f, lz = z + r.Range(-radius, radius) * 0.5f, lr = radius * r.Range(0.55f, 0.95f);
                    c.Decals.FlatDisc(D, lx, lz, lr * 1.45f, lr * 1.15f, C(74, 72, 38, 110), C(74, 72, 38, 0), 10, lx * 3f);
                }
                for (int lobe = 0; lobe < 3; lobe++)
                {
                    float lx = x + r.Range(-radius, radius) * 0.5f, lz = z + r.Range(-radius, radius) * 0.35f, lr = radius * r.Range(0.5f, 0.8f);
                    c.Decals.FlatDisc(D, lx, lz, lr, lr * 0.75f, C(88, 94, 48, 150), C(84, 88, 44, 95), 10, lx * 3f);
                }
                c.Glow.Uv = new Vector2(0.6f, r.Range(0f, 6.28f));
                c.Glow.FlatDisc(G, x - radius * 0.2f, z + radius * 0.15f, radius * 0.5f, radius * 0.3f, C(150, 170, 80, 26), C(150, 170, 80, 0), 8, x * 3f);
                c.Glow.Uv = Vector2.zero;
            }

            // Veins here and there.
            for (int i = 0; i < 14; i++)
            {
                TerrainCanvas.Wander(path, r, new Vector2(r.Range(-78f, 78f), c.Z(r.Range(10f, 78f))), r.Range(0f, 6.28f), 4f, 6 + r.Int(10), 0.42f);
                c.KeepOnFloor(path, 0.5f);
                Vein(c, path, r.Range(0.35f, 0.8f));
            }
            for (int i = 0; i < 10; i++) Pustule(c, r, r.Range(-78f, 78f), c.Z(r.Range(6f, 80f)), r.Range(0.4f, 0.8f));

            // Old scorch marks.
            for (int i = 0; i < 12; i++) Crater(c, r.Range(-74f, 74f), c.Z(r.Range(12f, 78f)), r.Range(1.5f, 3.6f), C(70, 56, 40));

            // Burst barricades and heaps around the field.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 5f; v < E + 4f; v += 9f)
                {
                    if (r.Chance(0.5f)) TerrainProps.Barricade(c, r, k * (W + 4f), c.Z(v), 90f + r.Range(-30f, 30f));
                    else if (r.Chance(0.6f)) TerrainProps.Mound(c, r, k * (W + 5.5f), c.Z(v), r.Range(1.6f, 3f), Skin, Pale);
                }
            }
            for (float x = -W; x < W; x += 9f)
            {
                if (r.Chance(0.45f)) TerrainProps.Barricade(c, r, x, c.Z(E + 4.5f), r.Range(-30f, 30f));
                else if (r.Chance(0.6f)) TerrainProps.Mound(c, r, x, c.Z(E + 7.5f), r.Range(1.4f, 2.4f), Skin, Pale);
            }

            // Dead containers.
            for (int i = 0; i < 18; i++)
            {
                AroundArena(c, r, 10f, 60f, out float x, out float z);
                TerrainProps.DeadContainer(c, r, x, z, r.Range(0f, 360f), Skin, Pale);
            }

            // Strangled masts.
            for (int i = 0; i < 12; i++)
            {
                AroundArena(c, r, 10f, 100f, out float x, out float z);
                TerrainProps.DeadMast(c, r, x, z, r.Range(9f, 20f), Skin, Pale);
            }

            // The great ribs of a hull: a long line of them across the land to the north, or to the sides on the player's side.
            float along = r.Range(-40f, 40f);
            if (c.Side > 0)
            {
                for (int i = 0; i < 9; i++)
                {
                    float x = along - 80f + i * 20f;
                    float height = 22f + Mathf.Sin(i * 0.7f) * 6f;
                    TerrainProps.Rib(c, r, x, c.Z(E + 34f + i * 1.5f), 0f, 34f, height, Rust, PaleRust);
                }
            }
            int sideOf = r.Chance(0.5f) ? -1 : 1;
            for (int i = 0; i < 8; i++)
            {
                TerrainProps.Rib(c, r, sideOf * (W + 44f), c.Z(10f + i * 16f), 90f, 32f, 20f + Mathf.Sin(i) * 5f, Rust, PaleRust);
            }

            // Pods, tendrils and rubble.
            for (int i = 0; i < 18; i++)
            {
                AroundArena(c, r, 10f, 110f, out float x, out float z);
                TerrainProps.Pod(c, r, x, z, r.Range(1.2f, 3f), Skin, PodGlow);
            }
            for (int i = 0; i < 16; i++)
            {
                AroundArena(c, r, 12f, 90f, out float x, out float z);
                TerrainProps.Tendril(c, r, x, z, r.Range(0f, 360f), r.Range(14f, 34f), r.Range(4f, 13f), r.Range(0.6f, 1.4f), Rust, Pale);
            }
            for (int i = 0; i < 36; i++)
            {
                AroundArena(c, r, 20f, 280f, out float x, out float z);
                TerrainProps.Boulder(c, r, x, z, r.Range(1.5f, 5f), C(84, 72, 54));
            }
            for (int i = 0; i < 16; i++)
            {
                AroundArena(c, r, 80f, 400f, out float x, out float z);
                TerrainProps.Rib(c, r, x, z, r.Range(0f, 180f), r.Range(40f, 90f), r.Range(22f, 50f), Rust, PaleRust);
            }
        }
    }

    public sealed class HiveLayout : TerrainLayout
    {
        private static readonly Color32 Skin = new Color32(96, 74, 56, 0);
        private static readonly Color32 Pale = new Color32(176, 152, 106, 0);
        private static readonly Color32 Root = new Color32(70, 46, 38, 0);
        private static readonly Color32 PodGlow = new Color32(226, 216, 110, 110);

        /// <summary>How many metres a trench takes to open up from nothing: 3 of its 7 m steps.</summary>
        private const float TrenchTaper = 21f;

        public override string Name => "Hive";

        public override TerrainTheme Mood(TerrainTheme theme)
        {
            TerrainTheme mood = theme.Clone();
            mood.SkyTop = new Color(0.1f, 0.05f, 0.07f);
            mood.SkyHorizon = new Color(0.4f, 0.24f, 0.2f);
            mood.FogColor = new Color(0.32f, 0.2f, 0.17f);
            mood.Haze = new Color(0.46f, 0.32f, 0.24f);
            mood.Cloud = new Color(0.22f, 0.12f, 0.12f);
            mood.SunColor = new Color(0.98f, 0.88f, 0.76f);
            mood.SunAngles = new Vector2(54f, 12f);
            return mood;
        }

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float rot = TerrainNoise.Fbm((x + seedX) / 26f, (z + seedZ) / 26f, 3);
            second = TerrainNoise.Smooth(0.54f, 0.62f, rot) * 0.9f;
            float sinew = TerrainNoise.Fbm((x - seedZ) / 44f, (z + seedX) / 44f, 3);
            third = TerrainNoise.Smooth(0.32f, 0.44f, sinew); // Mostly sinew.
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            var path = new List<Vector2>();

            // Trenches of rot across the floor, with pale raised lips.
            for (int i = 0; i < 6; i++)
            {
                float heading = 1.57f + r.Range(-0.5f, 0.5f);
                TerrainCanvas.Wander(path, r, new Vector2(-W + r.Range(-4f, 10f), c.Z(r.Range(12f, 76f))), heading, 7f, 22, 0.3f);
                c.KeepOnFloor(path, 0.5f);
                if (path.Count < 2) continue;
                c.Decals.Uv = new Vector2(0f, 0.3f);
                c.Decals.FlatRibbon(D, path, 4.2f, 2.2f, C(142, 118, 80, 90), true, taperEnds: TrenchTaper);
                c.Decals.FlatRibbon(D, path, 2.4f, 0.9f, C(72, 46, 36, 185), true, taperEnds: TrenchTaper);
                c.Decals.Uv = Vector2.zero;
            }

            // Veins and stray heads.
            for (int i = 0; i < 18; i++)
            {
                TerrainCanvas.Wander(path, r, new Vector2(r.Range(-78f, 78f), c.Z(r.Range(8f, 78f))), r.Range(0f, 6.28f), 4f, 6 + r.Int(9), 0.45f);
                c.KeepOnFloor(path, 0.5f);
                Vein(c, path, r.Range(0.4f, 0.8f));
            }
            for (int i = 0; i < 16; i++) Pustule(c, r, r.Range(-78f, 78f), c.Z(r.Range(6f, 80f)), r.Range(0.35f, 0.8f));

            // Low swellings all round the floor.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 3f; v < E + 6f; v += 7f)
                    if (r.Chance(0.85f)) TerrainProps.Mound(c, r, k * (W + r.Range(4.5f, 8f)), c.Z(v), r.Range(1.8f, 3.4f), Skin, Pale);
            }
            for (float x = -W; x < W + 1f; x += 7f)
                if (r.Chance(0.85f)) TerrainProps.Mound(c, r, x, c.Z(E + (c.Side > 0 ? r.Range(4.5f, 8f) : r.Range(7f, 9f))), r.Range(1.6f, c.Side > 0 ? 3.2f : 2.3f), Skin, Pale);

            // The spires: a thicket beside the field and to the far side of it, tall enough to read as the land's own.
            for (int k = -1; k <= 1; k += 2)
            {
                for (int i = 0; i < 6; i++)
                    TerrainProps.HiveSpire(c, r, k * (W + r.Range(16f, 46f)), c.Z(r.Range(4f, E + 24f)), r.Range(16f, 34f), Skin, Pale, PodGlow);
            }
            if (c.Side > 0)
            {
                for (int i = 0; i < 10; i++)
                    TerrainProps.HiveSpire(c, r, r.Range(-W - 20f, W + 20f), c.Z(E + r.Range(16f, 50f)), r.Range(20f, 44f), Skin, Pale, PodGlow);
            }
            for (int i = 0; i < 26; i++)
            {
                AroundArena(c, r, 50f, 380f, out float x, out float z);
                TerrainProps.HiveSpire(c, r, x, z, r.Range(30f, 80f), Skin, Pale, PodGlow);
            }

            // Reaching tendrils, pods and heaps of mass.
            for (int i = 0; i < 26; i++)
            {
                AroundArena(c, r, 10f, 90f, out float x, out float z);
                TerrainProps.RearingTendril(c, r, x, z, r.Range(6f, 16f), r.Range(0.5f, 1.1f), Root, Pale);
            }
            for (int i = 0; i < 18; i++)
            {
                AroundArena(c, r, 11f, 90f, out float x, out float z);
                TerrainProps.Pod(c, r, x, z, r.Range(1.2f, 3f), Skin, PodGlow);
            }
            for (int i = 0; i < 24; i++)
            {
                AroundArena(c, r, 40f, 300f, out float x, out float z);
                TerrainProps.Mound(c, r, x, z, r.Range(4f, 10f), Skin, Pale);
            }
        }
    }
}
