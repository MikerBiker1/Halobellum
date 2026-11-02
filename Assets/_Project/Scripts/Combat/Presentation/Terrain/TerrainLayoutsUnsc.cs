// TerrainLayoutsUnsc.cs
// The UNSC's two layouts (M10). Firebase: a military base: a broken concrete apron with painted lines, gravel roads, barricades, containers, floodlights, huts, a radio mast. Colony: a farming settlement: mown grass with
// dirt tracks, crop fields, silos, round prefab homes, wind turbines, fences and trees. Original shapes and plain paint: no logos or markings from anywhere.
// Looks only. Depends on: TerrainLayout, TerrainProps, TerrainCanvas, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class FirebaseLayout : TerrainLayout
    {
        public override string Name => "Firebase";
        public override float FlatBand => 34f;

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float patches = TerrainNoise.Fbm((x + seedX) / 34f, (z + seedZ) / 34f, 3);
            float reach = TerrainNoise.Smooth(0f, 40f, outside);
            second = TerrainNoise.Smooth(0.52f - reach * 0.2f, 0.6f - reach * 0.2f, patches) * (0.3f + reach * 0.7f);
            float rocky = TerrainNoise.Fbm((x - seedZ) / 21f, (z + seedX) / 21f, 3);
            third = TerrainNoise.Smooth(0.58f, 0.68f, rocky) * 0.55f + TerrainNoise.Smooth(26f, 50f, height);
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            ConcreteApron(c, r);
            Lines(c, r);
            Grime(c, r);
            Perimeter(c, r);
            Beyond(c, r);
        }

        private static void ConcreteApron(TerrainCanvas c, TerrainRandom r)
        {
            c.Decals.Uv = new Vector2(0f, 0.95f);
            const float slab = 8f;
            const float gap = 0.28f;
            for (int ix = 0; ix < 18; ix++)
            {
                for (int iv = 0; iv < 8; iv++)
                {
                    float x = -72f + slab * (ix + 0.5f);
                    float v = 16f + slab * (iv + 0.5f);
                    // Whole near the base, broken up towards no man's land.
                    float missing = 0.03f + TerrainNoise.Smooth(36f, 16f, v) * 0.75f;
                    if (r.Chance(missing)) continue;
                    Color32 tone = Grey(r, 128, 158, 3);
                    c.Decals.FlatRect(D, x, c.Z(v), slab - gap, slab - gap, 0f, tone);
                }
            }
            // A few darker repairs.
            for (int i = 0; i < 16; i++)
            {
                float x = r.Range(-68f, 68f);
                float v = r.Range(26f, 76f);
                c.Decals.FlatRect(D, x, c.Z(v), r.Range(2.5f, 7f), r.Range(1.5f, 5f), 0f, Grey(r, 92, 112, 2));
            }
            c.Decals.Uv = Vector2.zero;
        }

        private static void Lines(TerrainCanvas c, TerrainRandom r)
        {
            Color32 yellow = C(214, 172, 44);
            Color32 white = C(226, 226, 214);
            c.Decals.Uv = new Vector2(0.28f, 0.25f);

            // Around the apron.
            c.Decals.FlatRect(D, -72.6f, c.Z(48f), 0.5f, 62f, 0f, yellow);
            c.Decals.FlatRect(D, 72.6f, c.Z(48f), 0.5f, 62f, 0f, yellow);
            c.Decals.FlatRect(D, 0f, c.Z(79.4f), 146f, 0.5f, 0f, yellow);

            // Dashed lanes up the middle.
            for (int lane = -1; lane <= 1; lane++)
            {
                float x = lane * 36f;
                for (float v = 36f; v < 76f; v += 6f) c.Decals.FlatRect(D, x, c.Z(v), 0.4f, 3.4f, 0f, white);
            }
            // Bays near the back edge.
            for (float x = -64f; x < 66f; x += 10f)
            {
                if (Mathf.Abs(x) < 16f) continue;
                c.Decals.FlatRect(D, x, c.Z(72.5f), 0.32f, 11f, 0f, white);
            }

            // Two landing circles.
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * 52f;
                float z = c.Z(50f);
                c.Decals.FlatRing(D, x, z, 9.2f, 10f, yellow, 40);
                c.Decals.FlatRing(D, x, z, 3.2f, 3.7f, white, 24);
                for (int a = 0; a < 4; a++)
                {
                    float angle = a * Mathf.PI * 0.5f;
                    c.Decals.FlatRect(D, x + Mathf.Cos(angle) * 6.3f, z + Mathf.Sin(angle) * 6.3f, a % 2 == 0 ? 3.2f : 0.5f, a % 2 == 0 ? 0.5f : 3.2f, 0f, white);
                }
            }

            // Hazard strips along the back corners.
            for (int k = -1; k <= 1; k += 2)
            {
                float start = k * 66f;
                for (int i = 0; i < 12; i++)
                {
                    Hazard(c, start + k * i * 0.9f, c.Z(77.8f), 1.5f, i % 2 == 0 ? yellow : C(30, 30, 28));
                }
            }
            c.Decals.Uv = Vector2.zero;
        }

        private static void Hazard(TerrainCanvas c, float x, float z, float depth, Color32 color)
        {
            float half = depth * 0.5f;
            var a = new Vector3(x - 0.45f, D, z - half);
            var b = new Vector3(x + 0.45f, D, z + half);
            var e = new Vector3(x + 1.35f, D, z + half);
            var f = new Vector3(x + 0.45f, D, z - half);
            c.Decals.FlatQuad(a, b, e, f, color, color, color, color);
        }

        private static void Grime(TerrainCanvas c, TerrainRandom r)
        {
            for (int i = 0; i < 30; i++)
            {
                Stain(c, r.Range(-76f, 76f), c.Z(r.Range(10f, 78f)), r.Range(1.2f, 5f), C(26, 24, 22, 120 + r.Int(60)));
            }
            var path = new List<Vector2>();
            for (int i = 0; i < 7; i++)
            {
                float heading = (c.Side > 0 ? 3.14f : 0f) + r.Range(-0.5f, 0.5f);
                TerrainCanvas.Wander(path, r, new Vector2(r.Range(-70f, 70f), c.Z(r.Range(70f, 78f))), heading, 6f, 11, 0.12f);
                c.KeepOnFloor(path, 2f);
                c.Decals.Uv = new Vector2(0.2f, 0.4f);
                c.Decals.FlatRibbon(D, path, 2.8f, 2.8f, C(30, 26, 22, 56), true);
                c.Decals.Uv = Vector2.zero;
            }
        }

        private static void Perimeter(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;

            // Gravel roads around the field and away from it.
            for (int k = -1; k <= 1; k += 2)
            {
                c.PaintLine(k * (W + 11f), c.Z(0f), k * (W + 11f), c.Z(E + 13f), 8f, 2);
                c.PaintLine(k * (W + 11f), c.Z(E + 13f), k * 12f, c.Z(E + 13f), 8f, 2);
            }
            c.PaintLine(0f, c.Z(E + 13f), r.Range(-40f, 40f), c.Z(E + 300f), 9f, 2);
            c.PaintLine(-(W + 11f), c.Z(r.Range(20f, 60f)), -(W + 500f), c.Z(r.Range(50f, 300f)), 9f, 2);

            // Concrete barriers right around the field, with gaps.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 8f; v < E; v += 8.2f)
                    if (r.Chance(0.82f)) TerrainProps.Barricade(c, r, k * (W + 4f), c.Z(v), 90f);
            }
            for (float x = -W + 2f; x < W; x += 8.2f)
                if (r.Chance(0.85f)) TerrainProps.Barricade(c, r, x, c.Z(E + 4f), 0f);

            // Sandbag nests at the corners.
            for (int k = -1; k <= 1; k += 2)
            {
                TerrainProps.Sandbags(c, r, k * (W + 6f), c.Z(E + 8f), k * 45f);
                TerrainProps.Sandbags(c, r, k * (W + 7f), c.Z(4f), 90f, 5f);
            }

            // Floodlights: corners and sides.
            for (int k = -1; k <= 1; k += 2)
            {
                TerrainProps.LightMast(c, r, k * (W + 5.5f), c.Z(E + 13f), 0f, 14f);
                TerrainProps.LightMast(c, r, k * (W + 5.5f), c.Z(44f), 0f, 13f);
            }

            // Containers along the sides and behind.
            for (int k = -1; k <= 1; k += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float v = r.Range(8f, E + 4f);
                    TerrainProps.Containers(c, r, k * (W + r.Range(17f, 30f)), c.Z(v), 90f + r.Range(-8f, 8f), 1 + r.Int(3), 1 + r.Int(3));
                }
            }
            for (int i = 0; i < 6; i++)
            {
                TerrainProps.Containers(c, r, r.Range(-W, W), c.Z(E + r.Range(19f, 32f)), r.Range(-8f, 8f), 1 + r.Int(c.Side > 0 ? 3 : 1), 1 + r.Int(2));
            }

            // Fences beyond them.
            float fenceOut = 37f;
            for (int k = -1; k <= 1; k += 2)
                TerrainProps.Fence(c, r, k * (W + fenceOut), c.Z(0f), k * (W + fenceOut), c.Z(E + fenceOut));
            TerrainProps.Fence(c, r, -(W + fenceOut), c.Z(E + fenceOut), W + fenceOut, c.Z(E + fenceOut));
        }

        private static void Beyond(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            // Huts and tanks.
            for (int i = 0; i < 6; i++)
            {
                AroundArena(c, r, 18f, 34f, out float x, out float z);
                TerrainProps.Hut(c, r, x, z, r.Int(4) * 90f, r.Range(8f, 13f), r.Range(5f, 7f));
            }
            for (int i = 0; i < 3; i++)
            {
                AroundArena(c, r, 18f, 34f, out float x, out float z);
                TerrainProps.FuelTanks(c, r, x, z, r.Int(4) * 90f);
            }
            if (c.Side > 0)
            {
                TerrainProps.Hangar(c, r, r.Range(-50f, -20f), c.Z(E + 24f), 0f);
                TerrainProps.Hangar(c, r, r.Range(20f, 60f), c.Z(E + 26f), 0f);
                TerrainProps.RadioMast(c, r, r.Range(-90f, 90f), c.Z(E + 22f), 40f);
            }
            TerrainProps.Hangar(c, r, r.Sign() * (W + 26f), c.Z(r.Range(25f, 60f)), 90f);
            TerrainProps.RadioMast(c, r, r.Sign() * (W + 28f), c.Z(r.Range(20f, 70f)), 34f);

            // Scrub and trees beyond the wire.
            for (int i = 0; i < 70; i++)
            {
                AroundArena(c, r, 40f, 220f, out float x, out float z);
                TerrainProps.Tree(c, r, x, z, r.Range(0.8f, 1.6f));
            }
            for (int i = 0; i < 24; i++)
            {
                AroundArena(c, r, 30f, 180f, out float x, out float z);
                TerrainProps.Boulder(c, r, x, z, r.Range(1f, 3.4f), C(122, 116, 104));
            }
        }
    }

    public sealed class ColonyLayout : TerrainLayout
    {
        public override string Name => "Colony";
        public override float FlatBand => 32f;

        public override TerrainTheme Mood(TerrainTheme theme)
        {
            TerrainTheme mood = theme.Clone();
            mood.SunColor = new Color(1f, 0.9f, 0.72f);
            mood.SunAngles = new Vector2(44f, -40f);
            mood.SkyHorizon = new Color(0.86f, 0.8f, 0.66f);
            mood.FogColor = new Color(0.8f, 0.77f, 0.66f);
            return mood;
        }

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float patches = TerrainNoise.Fbm((x + seedX) / 26f, (z + seedZ) / 26f, 3);
            second = TerrainNoise.Smooth(0.3f, 0.42f, patches);
            float rocky = TerrainNoise.Fbm((x - seedZ) / 40f, (z + seedX) / 40f, 3);
            third = TerrainNoise.Smooth(0.68f, 0.76f, rocky) * 0.7f + TerrainNoise.Smooth(34f, 60f, height);
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;

            // Mown stripes down the field.
            c.Decals.Uv = new Vector2(0f, 0.5f);
            for (float x = -W; x < W; x += 20f)
                c.Decals.FlatRect(D, x + 5f, c.Z(E * 0.55f), 10f, E * 0.9f, 0f, C(36, 54, 22, 34));
            c.Decals.Uv = Vector2.zero;

            // Dirt tracks crossing the field, as ruts.
            var path = new List<Vector2>();
            for (int i = 0; i < 5; i++)
            {
                float z0 = c.Z(r.Range(14f, 74f));
                float heading = 1.57f + r.Range(-0.35f, 0.35f);
                TerrainCanvas.Wander(path, r, new Vector2(-W - 2f, z0), heading, 12f, 15, 0.14f);
                for (int p = 0; p + 1 < path.Count; p++)
                    c.PaintLine(path[p].x, path[p].y, path[p + 1].x, path[p + 1].y, 6f, 0, 0.9f);
            }
            for (int i = 0; i < 3; i++)
            {
                float x0 = r.Range(-60f, 60f);
                float heading = (c.Side > 0 ? 0f : 3.14f) + r.Range(-0.3f, 0.3f);
                TerrainCanvas.Wander(path, r, new Vector2(x0, c.Z(10f)), heading, 12f, 12, 0.12f);
                for (int p = 0; p + 1 < path.Count; p++)
                    c.PaintLine(path[p].x, path[p].y, path[p + 1].x, path[p + 1].y, 5f, 0, 0.9f);
            }

            // Small concrete pads at the back of the field, with a painted edge.
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * r.Range(40f, 62f), z = c.Z(r.Range(60f, 70f));
                c.Decals.Uv = new Vector2(0f, 0.9f);
                for (int i = 0; i < 3; i++)
                    c.Decals.FlatRect(D, x + (i - 1) * 8.2f, z, 7.9f, 12f, 0f, Grey(r, 136, 160, 4));
                c.Decals.Uv = Vector2.zero;
            }

            // Crops, right up to the field.
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 2f; v < E + 16f; v += 26f) Field(c, r, k * (W + 18f), c.Z(v + 12f), 22f, 24f, true);
            }
            for (float x = -W + 4f; x < W; x += 34f) Field(c, r, x + 15f, c.Z(E + 17f), 30f, 19f, false);

            // Low wooden fences around the field.
            for (int k = -1; k <= 1; k += 2)
                TerrainProps.Fence(c, r, k * (W + 3.5f), c.Z(1f), k * (W + 3.5f), c.Z(E + 3.5f), 0.04f, true);
            TerrainProps.Fence(c, r, -(W + 3.5f), c.Z(E + 3.5f), W + 3.5f, c.Z(E + 3.5f), 0.04f, true);

            // The farmstead.
            for (int k = -1; k <= 1; k += 2)
            {
                TerrainProps.Silo(c, r, k * (W + 34f), c.Z(r.Range(10f, 30f)), r.Range(9f, 13f));
                TerrainProps.Silo(c, r, k * (W + 41f), c.Z(r.Range(12f, 30f)), r.Range(9f, 13f));
                TerrainProps.Barn(c, r, k * (W + 38f), c.Z(r.Range(46f, 76f)), 90f);
                TerrainProps.Hab(c, r, k * (W + r.Range(36f, 50f)), c.Z(r.Range(40f, 90f)), r.Range(0f, 360f));
                TerrainProps.Hab(c, r, k * (W + r.Range(36f, 50f)), c.Z(r.Range(40f, 90f)), r.Range(0f, 360f));
            }
            if (c.Side > 0)
            {
                TerrainProps.Barn(c, r, r.Range(-60f, -10f), c.Z(E + 45f), 0f);
                TerrainProps.Silo(c, r, r.Range(10f, 40f), c.Z(E + 42f), 12f);
                TerrainProps.Silo(c, r, r.Range(44f, 70f), c.Z(E + 44f), 13f);
                TerrainProps.WaterTower(c, r, r.Range(-90f, 90f), c.Z(E + 56f));
                for (int i = 0; i < 6; i++) TerrainProps.Hab(c, r, r.Range(-110f, 110f), c.Z(E + r.Range(44f, 74f)), r.Range(0f, 360f));
                for (int i = 0; i < 3; i++) TerrainProps.Turbine(c, r, r.Range(-160f, 160f), c.Z(E + r.Range(70f, 150f)));
            }
            for (int k = -1; k <= 1; k += 2) TerrainProps.Turbine(c, r, k * (W + r.Range(70f, 140f)), c.Z(r.Range(10f, 90f)));

            // Power lines along the back.
            Vector3? wire = null;
            for (float x = -W - 90f; x < W + 90f; x += 26f)
            {
                float v = E + 36f + Mathf.Sin(x * 0.03f) * 3f;
                if (c.Side < 0) v += 6f;
                wire = TerrainProps.PowerPole(c, r, x, c.Z(v), 0f, wire, out Vector3 at) ? at : (Vector3?)null;
            }

            // Orchards and hedges.
            for (int row = 0; row < 3; row++)
            {
                int k = r.Chance(0.5f) ? -1 : 1;
                float x = k * (W + r.Range(56f, 110f));
                float start = r.Range(0f, 70f);
                for (int i = 0; i < 12; i++) TerrainProps.Tree(c, r, x + r.Range(-0.4f, 0.4f), c.Z(start + i * 7f), r.Range(1f, 1.3f));
            }
            for (int i = 0; i < 90; i++)
            {
                AroundArena(c, r, 44f, 240f, out float x, out float z);
                TerrainProps.Tree(c, r, x, z, r.Range(0.9f, 1.8f));
            }
        }

        private static void Field(TerrainCanvas c, TerrainRandom r, float x, float z, float width, float depth, bool rowsAlongZ)
        {
            bool gold = r.Chance(0.45f);
            Color32 ground = gold ? C(122, 104, 54) : C(52, 76, 34);
            Color32 row = gold ? C(186, 160, 74) : C(82, 114, 48);
            Color32 row2 = gold ? C(166, 138, 60) : C(68, 100, 42);
            c.Decals.Uv = new Vector2(0f, 0.35f);
            c.Decals.FlatRect(D, x, z, width, depth, 0f, ground);
            int rows = Mathf.RoundToInt((rowsAlongZ ? width : depth) / 2f);
            for (int i = 0; i < rows; i++)
            {
                float t = (i + 0.5f) / rows - 0.5f;
                Color32 color = i % 2 == 0 ? row : row2;
                if (rowsAlongZ) c.Decals.FlatRect(D, x + t * width, z, 1.1f, depth - 0.6f, 0f, color);
                else c.Decals.FlatRect(D, x, z + t * depth, width - 0.6f, 1.1f, 0f, color);
            }
            c.Decals.Uv = Vector2.zero;
        }
    }
}
