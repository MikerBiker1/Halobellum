// TerrainProps.cs
// The things that stand around the arena (M10), made from a few boxes, tubes and lumps with colours in their vertices: human bases and farms, alien crystals and pylons, Flood tendrils and
// pods. Each one first asks the canvas for a place to stand and returns false, drawing nothing, if it can't have it. All shapes are original and simple; low on triangles, since they are small at the game's camera.
// Looks only. Depends on: TerrainCanvas, TerrainMeshBuilder, TerrainNoise, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class TerrainProps
    {
        public static Color32 C(int r, int g, int b, int glow = 0) => new Color32((byte)r, (byte)g, (byte)b, (byte)glow);

        private static Color32 Vary(TerrainRandom r, Color32 c, float amount = 0.12f)
        {
            float f = 1f + r.Range(-amount, amount);
            return new Color32((byte)Mathf.Min(255f, c.r * f), (byte)Mathf.Min(255f, c.g * f), (byte)Mathf.Min(255f, c.b * f), c.a);
        }

        private static readonly List<Vector3> Path = new List<Vector3>();
        private static readonly List<float> Radii = new List<float>();

        // ------------------------------------------------------------------ UNSC: the base

        /// <summary>A concrete road barrier.</summary>
        public static bool Barricade(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (!c.Stand(x, z, 2.1f, 1.1f, 0.4f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32 color = Vary(r, C(150, 148, 138), 0.1f);
            c.Solid.Box(Vector3.zero, new Vector3(4f, 0.45f, 1.1f), color, 0.8f);
            c.Solid.Box(new Vector3(0f, 0.45f, 0f), new Vector3(3.8f, 0.65f, 0.5f), color, 0.9f);
            c.Solid.Box(new Vector3(0f, 0.8f, 0f), new Vector3(1.6f, 0.12f, 0.52f), C(186, 160, 40));
            return true;
        }

        /// <summary>A curved heap of sandbags.</summary>
        public static bool Sandbags(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float length = 7f)
        {
            if (!c.Stand(x, z, length * 0.55f, 1.2f, 0.4f)) return false;
            c.PlaceSolid(x, z, yaw);
            int count = Mathf.RoundToInt(length / 0.9f);
            for (int layer = 0; layer < 2; layer++)
            {
                for (int i = 0; i < count - layer; i++)
                {
                    float t = i + layer * 0.5f - (count - 1) * 0.5f;
                    var at = new Vector3(t * 0.9f, 0.27f + layer * 0.46f, t * t * 0.03f);
                    Color32 bag = Vary(r, C(146, 128, 92), 0.1f);
                    c.Solid.Lump(at, new Vector3(0.52f, 0.28f, 0.42f), bag, 6);
                }
            }
            return true;
        }

        /// <summary>A shipping container, or a stack of them. Colours are plain paints; no logos.</summary>
        public static bool Containers(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, int high, int wide = 1)
        {
            float height = high * 2.9f;
            float radius = Mathf.Max(7.2f, wide * 1.9f + 6f);
            if (!c.Stand(x, z, radius, height, 0.55f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32[] paints = { C(120, 66, 44), C(58, 84, 106), C(62, 92, 66), C(160, 128, 58), C(108, 110, 112), C(136, 56, 50) };
            for (int w = 0; w < wide; w++)
            {
                float offset = (w - (wide - 1) * 0.5f) * 3.2f;
                int stack = w == 0 ? high : Mathf.Max(1, high - r.Int(2));
                for (int h = 0; h < stack; h++)
                {
                    Color32 paint = Vary(r, paints[r.Int(paints.Length)], 0.08f);
                    float slip = h == 0 ? 0f : r.Range(-0.25f, 0.25f);
                    var foot = new Vector3(slip, h * 2.9f, offset);
                    c.Solid.Box(foot, new Vector3(12.2f, 2.9f, 2.9f), paint);
                    // Corrugation, cheaply: darker bands on the long sides.
                    Color32 dark = new Color32((byte)(paint.r * 0.72f), (byte)(paint.g * 0.72f), (byte)(paint.b * 0.72f), 0);
                    c.Solid.Box(foot + new Vector3(0f, 0.25f, 0f), new Vector3(11.2f, 2.4f, 2.96f), dark);
                }
            }
            return true;
        }

        /// <summary>A tall floodlight mast with a lamp head that glows.</summary>
        public static bool LightMast(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float height = 13f)
        {
            if (!c.Stand(x, z, 1.1f, height, 0.45f)) return false;
            c.PlaceSolid(x, z, yaw);
            c.Solid.Box(Vector3.zero, new Vector3(1.2f, 0.4f, 1.2f), C(90, 92, 92));
            c.Solid.Cylinder(Vector3.zero, 0.2f, 0.13f, height, 5, C(120, 124, 126), false);
            c.Solid.Box(new Vector3(0f, height, 0f), new Vector3(3f, 0.7f, 0.5f), C(70, 72, 74));
            c.Solid.Box(new Vector3(0f, height + 0.08f, -0.26f), new Vector3(2.7f, 0.5f, 0.06f), C(255, 244, 210, 255));
            Vector3 lamp = new Vector3(x, c.GroundAt(x, z) + height + 0.3f, z);
            c.Glow.Uv = Vector2.zero;
            c.Glow.Halo(lamp, 3.2f, C(255, 232, 180, 80));
            c.Decals.FlatDisc(TerrainCanvas.DecalHeight, x, z - 7f, 16f, 16f, new Color32(255, 240, 200, 26), new Color32(255, 240, 200, 0));
            return true;
        }

        /// <summary>A lattice radio mast, very tall, with a red beacon. Only for the land to the north and sides.</summary>
        public static bool RadioMast(TerrainCanvas c, TerrainRandom r, float x, float z, float height = 38f)
        {
            if (!c.Stand(x, z, 3f, height, 0.5f)) return false;
            c.PlaceSolid(x, z);
            float leg = 2.2f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var foot = new Vector3(Mathf.Cos(a) * leg, 0f, Mathf.Sin(a) * leg);
                c.Solid.Tube(new[] { foot, new Vector3(0f, height, 0f) }, new[] { 0.14f, 0.05f }, 4, C(170, 60, 50), C(210, 210, 206));
            }
            for (float y = 6f; y < height - 6f; y += 7f)
            {
                float w = leg * (1f - y / height) * 1.3f;
                c.Solid.Box(new Vector3(0f, y, 0f), new Vector3(w, 0.1f, w), C(180, 180, 176));
            }
            Vector3 top = new Vector3(x, c.GroundAt(x, z) + height, z);
            c.Glow.Uv = new Vector2(0.8f, r.Range(0f, 6f));
            c.Glow.Halo(top, 3.5f, C(255, 60, 40, 200));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A prefab hut with a flat roof, a door and lit windows.</summary>
        public static bool Hut(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float width = 10f, float depth = 6f)
        {
            float height = 3.4f;
            if (!c.Stand(x, z, Mathf.Max(width, depth) * 0.62f, height, 0.5f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32 wall = Vary(r, C(130, 134, 120), 0.1f);
            c.Solid.Box(Vector3.zero, new Vector3(width, height, depth), wall);
            c.Solid.Box(new Vector3(0f, height, 0f), new Vector3(width + 0.5f, 0.3f, depth + 0.5f), Vary(r, C(84, 88, 84)));
            for (float wx = -width * 0.35f; wx < width * 0.36f; wx += width * 0.35f)
                c.Solid.Box(new Vector3(wx, 1.4f, -depth * 0.5f - 0.02f), new Vector3(1.1f, 0.8f, 0.05f), C(250, 226, 160, 150));
            return true;
        }

        /// <summary>A big hangar with an arched roof. Only for the land to the north and sides.</summary>
        public static bool Hangar(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            float width = 26f, depth = 16f, wall = 6f, arc = 5f;
            if (!c.Stand(x, z, width * 0.6f, wall + arc, 0.7f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32 side = Vary(r, C(112, 118, 110), 0.08f);
            c.Solid.Box(Vector3.zero, new Vector3(width, wall, depth), side);
            Color32 roof = Vary(r, C(90, 96, 92), 0.08f);
            int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float a0 = Mathf.PI * i / steps, a1 = Mathf.PI * (i + 1) / steps;
                float x0 = -Mathf.Cos(a0) * width * 0.5f, x1 = -Mathf.Cos(a1) * width * 0.5f;
                float y0 = wall + Mathf.Sin(a0) * arc, y1 = wall + Mathf.Sin(a1) * arc;
                float hz = depth * 0.5f;
                Color32 band = i % 2 == 0 ? roof : new Color32((byte)(roof.r * 0.92f), (byte)(roof.g * 0.92f), (byte)(roof.b * 0.92f), 0);
                c.Solid.Quad(new Vector3(x0, y0, -hz), new Vector3(x0, y0, hz), new Vector3(x1, y1, hz), new Vector3(x1, y1, -hz), band);
                c.Solid.Tri(new Vector3(x0, y0, -hz), new Vector3(x1, y1, -hz), new Vector3(0f, wall, -hz), side);
                c.Solid.Tri(new Vector3(x1, y1, hz), new Vector3(x0, y0, hz), new Vector3(0f, wall, hz), side);
            }
            c.Solid.Box(new Vector3(0f, 0f, -depth * 0.5f - 0.05f), new Vector3(width * 0.62f, wall * 0.86f, 0.12f), C(58, 62, 62));
            return true;
        }

        /// <summary>A pair of fat fuel tanks in a bund.</summary>
        public static bool FuelTanks(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (!c.Stand(x, z, 8f, 5.2f, 0.55f)) return false;
            c.PlaceSolid(x, z, yaw);
            for (int i = -1; i <= 1; i += 2)
            {
                var at = new Vector3(i * 3.3f, 0f, 0f);
                c.Solid.Cylinder(at, 2.7f, 2.7f, 4.4f, 12, Vary(r, C(186, 186, 178), 0.05f));
                c.Solid.Lump(at + Vector3.up * 4.4f, new Vector3(2.7f, 0.7f, 2.7f), C(170, 172, 166), 12);
            }
            c.Solid.Box(Vector3.zero, new Vector3(13f, 0.5f, 7.4f), C(120, 120, 112));
            return true;
        }

        /// <summary>A run of chain-link fence with posts and rails between two points.</summary>
        public static void Fence(TerrainCanvas c, TerrainRandom r, float ax, float az, float bx, float bz, float gapChance = 0.06f, bool wooden = false)
        {
            float length = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
            int spans = Mathf.Max(1, Mathf.RoundToInt(length / 4.5f));
            float postHeight = wooden ? 1.5f : 2.6f;
            Color32 post = wooden ? C(104, 80, 54) : C(136, 140, 142);
            Color32 rail = wooden ? C(122, 94, 62) : C(168, 172, 174);
            float previousY = 0f;
            bool havePrevious = false;
            float px = 0f, pz = 0f;
            for (int i = 0; i <= spans; i++)
            {
                float t = i / (float)spans;
                float x = ax + (bx - ax) * t, z = az + (bz - az) * t;
                bool skip = r.Chance(gapChance) || !c.IsClear(x, z, 0.2f, postHeight);
                if (skip)
                {
                    havePrevious = false;
                    continue;
                }
                float y = c.GroundAt(x, z);
                c.Solid.Place(new Vector3(x, y, z));
                c.Solid.Box(Vector3.zero, new Vector3(0.22f, postHeight, 0.22f), post);
                if (havePrevious)
                {
                    c.Solid.Place(Vector3.zero);
                    for (int k = 1; k <= 2; k++)
                    {
                        float h = postHeight * (k == 1 ? 0.5f : 0.95f);
                        Rail(c, new Vector3(px, previousY + h, pz), new Vector3(x, y + h, z), 0.07f, rail);
                    }
                }
                havePrevious = true;
                px = x;
                pz = z;
                previousY = y;
            }
        }

        private static void Rail(TerrainCanvas c, Vector3 a, Vector3 b, float half, Color32 color)
        {
            Vector3 along = (b - a).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, along).normalized * half;
            Vector3 up = Vector3.up * half;
            float aoHeight = c.Solid.AoHeight;
            c.Solid.AoHeight = 0f;
            c.Solid.Quad(a - side + up, b - side + up, b + side + up, a + side + up, color);
            c.Solid.Quad(a - side - up, b - side - up, b - side + up, a - side + up, color);
            c.Solid.Quad(a + side + up, b + side + up, b + side - up, a + side - up, color);
            c.Solid.AoHeight = aoHeight;
        }

        /// <summary>A round bushy tree. sway makes its crown stir.</summary>
        public static bool Tree(TerrainCanvas c, TerrainRandom r, float x, float z, float size = 1f)
        {
            float height = 7f * size;
            if (!c.Stand(x, z, 2.4f * size, height, 0.4f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f), size);
            c.Solid.Uv = Vector2.zero;
            c.Solid.Cylinder(Vector3.zero, 0.34f, 0.2f, 3.2f, 5, C(84, 64, 46), false);
            Color32 leaf = Vary(r, r.Chance(0.2f) ? C(120, 122, 54) : C(66, 96, 44), 0.15f);
            Color32 light = new Color32((byte)Mathf.Min(255, leaf.r * 1.45f), (byte)Mathf.Min(255, leaf.g * 1.45f), (byte)Mathf.Min(255, leaf.b * 1.35f), 0);
            c.Solid.Uv = new Vector2(0.06f, r.Range(0f, 6f));
            c.Solid.Lump(new Vector3(0f, 4.6f, 0f), new Vector3(2.5f, 2.3f, 2.5f), leaf, 8, 0.2f, r.Int(100), light);
            c.Solid.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A rounded boulder, half sunk in the ground.</summary>
        public static bool Boulder(TerrainCanvas c, TerrainRandom r, float x, float z, float size, Color32 color)
        {
            if (!c.Stand(x, z, size, size * 1.2f, 0.45f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            color.a = 0;
            Color32 top = new Color32((byte)Mathf.Min(255, color.r * 1.25f), (byte)Mathf.Min(255, color.g * 1.25f), (byte)Mathf.Min(255, color.b * 1.25f), 0);
            c.Solid.Lump(Vector3.up * (size * 0.25f), new Vector3(size, size * 0.75f, size * 0.85f), color, 8, 0.3f, r.Int(100), top);
            return true;
        }

        // ------------------------------------------------------------------ UNSC: the colony

        /// <summary>A grain silo with a cone on top. Comes in pairs when wide.</summary>
        public static bool Silo(TerrainCanvas c, TerrainRandom r, float x, float z, float height = 11f)
        {
            if (!c.Stand(x, z, 3.4f, height + 2.2f, 0.6f)) return false;
            c.PlaceSolid(x, z);
            Color32 skin = Vary(r, C(176, 180, 176), 0.07f);
            c.Solid.Cylinder(Vector3.zero, 2.7f, 2.7f, height, 10, skin, false);
            c.Solid.Cylinder(Vector3.up * height, 2.95f, 0.1f, 2.2f, 10, C(136, 74, 54));
            return true;
        }

        /// <summary>A rounded prefab colony home: a half-barrel with a lit door.</summary>
        public static bool Hab(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (!c.Stand(x, z, 6.6f, 4.2f, 0.55f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32 skin = Vary(r, C(206, 200, 184), 0.06f);
            Color32 band = C(78, 108, 118);
            int steps = 6;
            float hz = 5.2f, hx = 3.3f, top = 4f;
            for (int i = 0; i < steps; i++)
            {
                float a0 = Mathf.PI * i / steps, a1 = Mathf.PI * (i + 1) / steps;
                float x0 = -Mathf.Cos(a0) * hx, x1 = -Mathf.Cos(a1) * hx;
                float y0 = Mathf.Sin(a0) * top, y1 = Mathf.Sin(a1) * top;
                c.Solid.Quad(new Vector3(x0, y0, -hz), new Vector3(x0, y0, hz), new Vector3(x1, y1, hz), new Vector3(x1, y1, -hz), i == steps / 2 ? band : skin);
                c.Solid.Tri(new Vector3(x0, y0, -hz), new Vector3(x1, y1, -hz), new Vector3(0f, 0f, -hz), skin);
                c.Solid.Tri(new Vector3(x1, y1, hz), new Vector3(x0, y0, hz), new Vector3(0f, 0f, hz), skin);
            }
            c.Solid.Box(new Vector3(0f, 0f, -hz - 0.05f), new Vector3(1.3f, 2.2f, 0.1f), C(250, 220, 150, 170));
            return true;
        }

        /// <summary>A barn: a box with a pitched roof.</summary>
        public static bool Barn(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            float width = 14f, depth = 9f, wall = 4.4f, peak = 3f;
            if (!c.Stand(x, z, width * 0.62f, wall + peak, 0.6f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32 skin = Vary(r, C(136, 62, 48), 0.1f);
            Color32 roof = Vary(r, C(96, 94, 90), 0.08f);
            c.Solid.Box(Vector3.zero, new Vector3(width, wall, depth), skin);
            float hw = width * 0.52f, hd = depth * 0.55f;
            var a = new Vector3(-hw, wall, -hd); var b = new Vector3(hw, wall, -hd);
            var e = new Vector3(hw, wall, hd); var f = new Vector3(-hw, wall, hd);
            var ridgeL = new Vector3(-hw, wall + peak, 0f); var ridgeR = new Vector3(hw, wall + peak, 0f);
            c.Solid.Quad(a, ridgeL, ridgeR, b, roof);
            c.Solid.Quad(e, ridgeR, ridgeL, f, roof);
            c.Solid.Tri(a, f, ridgeL, skin);
            c.Solid.Tri(e, b, ridgeR, skin);
            return true;
        }

        /// <summary>A three-bladed wind turbine. The blades don't turn. Only for the land to the north and sides.</summary>
        public static bool Turbine(TerrainCanvas c, TerrainRandom r, float x, float z, float height = 30f)
        {
            if (!c.Stand(x, z, 3f, height + 12f, 0.35f)) return false;
            c.PlaceSolid(x, z);
            Color32 white = C(226, 228, 224);
            c.Solid.Cylinder(Vector3.zero, 0.75f, 0.35f, height, 7, white, false);
            var hub = new Vector3(0f, height, -0.8f);
            c.Solid.Lump(hub, new Vector3(0.7f, 0.7f, 1.3f), white, 6);
            float spin = r.Range(0f, 120f) * Mathf.Deg2Rad;
            for (int i = 0; i < 3; i++)
            {
                float a = spin + i * Mathf.PI * 2f / 3f;
                var tip = hub + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 12f;
                var side = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * 0.55f;
                c.Solid.Tri(hub - side, hub + side, tip, white);
                c.Solid.Tri(hub + side, hub - side, tip, white);
            }
            return true;
        }

        /// <summary>A water tower on four legs.</summary>
        public static bool WaterTower(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            float height = 15f;
            if (!c.Stand(x, z, 4f, height, 0.5f)) return false;
            c.PlaceSolid(x, z);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                var foot = new Vector3(Mathf.Cos(a) * 3f, 0f, Mathf.Sin(a) * 3f);
                c.Solid.Tube(new[] { foot, new Vector3(foot.x * 0.55f, 11f, foot.z * 0.55f) }, new[] { 0.22f, 0.16f }, 4, C(110, 112, 108), C(110, 112, 108));
            }
            c.Solid.Lump(Vector3.up * 12.6f, new Vector3(3.2f, 2.6f, 3.2f), C(186, 196, 190), 10);
            return true;
        }

        /// <summary>A pylon carrying power lines.</summary>
        public static bool PowerPole(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, Vector3? wireFrom, out Vector3 wireTo, float height = 9f)
        {
            wireTo = default;
            if (!c.Stand(x, z, 0.6f, height, 0.35f)) return false;
            c.PlaceSolid(x, z, yaw);
            c.Solid.Cylinder(Vector3.zero, 0.2f, 0.12f, height, 5, C(96, 76, 56), false);
            c.Solid.Box(new Vector3(0f, height - 0.8f, 0f), new Vector3(3.2f, 0.16f, 0.16f), C(96, 76, 56));
            float y = c.GroundAt(x, z) + height - 0.7f;
            wireTo = new Vector3(x, y, z);
            if (wireFrom.HasValue)
            {
                c.Solid.Place(Vector3.zero);
                Rail(c, wireFrom.Value, wireTo, 0.04f, C(40, 40, 44));
            }
            return true;
        }

        // ------------------------------------------------------------------ UNSC: the quarry

        /// <summary>A rolling crane: two legged frames and a beam across them. Only for the land to the north and sides.</summary>
        public static bool Gantry(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float span = 22f, float height = 13f)
        {
            if (!c.Stand(x, z, span * 0.62f, height + 1f, 0.5f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32 paint = Vary(r, C(196, 150, 44), 0.08f);
            Color32 steel = C(88, 90, 92);
            for (int k = -1; k <= 1; k += 2)
            {
                float lx = k * span * 0.5f;
                c.Solid.Box(new Vector3(lx, 0f, 0f), new Vector3(1.6f, 0.6f, 7f), steel);
                c.Solid.Tube(new[] { new Vector3(lx, 0f, -3f), new Vector3(lx, height, 0f) }, new[] { 0.3f, 0.2f }, 4, paint, paint);
                c.Solid.Tube(new[] { new Vector3(lx, 0f, 3f), new Vector3(lx, height, 0f) }, new[] { 0.3f, 0.2f }, 4, paint, paint);
            }
            c.Solid.Box(new Vector3(0f, height, 0f), new Vector3(span + 3f, 1.1f, 1.1f), paint);
            float cart = r.Range(-span * 0.35f, span * 0.35f);
            c.Solid.Box(new Vector3(cart, height - 1.1f, 0f), new Vector3(2.4f, 1.1f, 1.8f), steel);
            return true;
        }

        /// <summary>A long sloping conveyor on legs, from low at one end to high at the other.</summary>
        public static bool Conveyor(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float length = 26f, float height = 8f)
        {
            if (!c.Stand(x, z, length * 0.55f, height, 0.4f)) return false;
            c.PlaceSolid(x, z, yaw);
            float half = length * 0.5f;
            Vector3 low = new Vector3(-half, 1.2f, 0f), high = new Vector3(half, height, 0f);
            c.Solid.Tube(new[] { low, high }, new[] { 0.9f, 0.9f }, 4, C(92, 96, 94), C(116, 118, 112), false, 0.9f);
            for (int i = 1; i <= 3; i++)
            {
                float t = i / 3.6f;
                Vector3 at = Vector3.Lerp(low, high, t);
                c.Solid.Box(new Vector3(at.x, 0f, 0f), new Vector3(0.35f, at.y, 1.5f), C(76, 78, 78), 0.7f);
            }
            return true;
        }

        /// <summary>A heap of broken rock or spoil.</summary>
        public static bool Heap(TerrainCanvas c, TerrainRandom r, float x, float z, float size, Color32 color)
        {
            if (!c.Stand(x, z, size, size * 0.7f, 0.5f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            color.a = 0;
            Color32 top = new Color32((byte)Mathf.Min(255, color.r * 1.2f), (byte)Mathf.Min(255, color.g * 1.2f), (byte)Mathf.Min(255, color.b * 1.2f), 0);
            c.Solid.Lump(Vector3.zero, new Vector3(size, size * 0.65f, size * 0.9f), color, 10, 0.2f, r.Int(100), top);
            return true;
        }

        // ------------------------------------------------------------------ Covenant

        /// <summary>A cluster of glowing crystal shards.</summary>
        public static bool CrystalCluster(TerrainCanvas c, TerrainRandom r, float x, float z, float size, Color32 glow, Color32 body)
        {
            float height = 6.5f * size;
            if (!c.Stand(x, z, 3.2f * size, height, 0.45f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            int count = 3 + r.Int(4);
            for (int i = 0; i < count; i++)
            {
                float a = r.Range(0f, 6.28f);
                float d = i == 0 ? 0f : r.Range(0.8f, 2.6f) * size;
                var foot = new Vector3(Mathf.Cos(a) * d, -0.2f, Mathf.Sin(a) * d);
                float h = (i == 0 ? 1f : r.Range(0.35f, 0.75f)) * height;
                var lean = new Vector3(foot.x, 0f, foot.z) * (0.05f / Mathf.Max(0.4f, size));
                lean += new Vector3(r.Range(-0.1f, 0.1f), 0f, r.Range(-0.1f, 0.1f));
                c.Solid.Shard(foot, (i == 0 ? 0.95f : r.Range(0.35f, 0.7f)) * size, h, lean, 5, body, glow, r.Range(0f, 6.28f));
            }
            c.Glow.Uv = new Vector2(0.35f, r.Range(0f, 6.28f));
            c.Glow.Halo(new Vector3(x, c.GroundAt(x, z) + height * 0.5f, z), 4.2f * size, new Color32(glow.r, glow.g, glow.b, 26));
            c.Glow.Uv = Vector2.zero;
            c.Decals.FlatDisc(TerrainCanvas.DecalHeight, x, z, 7f * size, 7f * size, new Color32(glow.r, glow.g, glow.b, 46), new Color32(glow.r, glow.g, glow.b, 0));
            return true;
        }

        /// <summary>A tall curved metal blade, bowing inwards, with a glowing tip: a signature of the alien sites.</summary>
        public static bool CurvedPylon(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float height, Color32 metal, Color32 glow)
        {
            if (!c.Stand(x, z, 1.7f, height, 0.5f)) return false;
            c.PlaceSolid(x, z, yaw);
            Path.Clear();
            Radii.Clear();
            int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Path.Add(new Vector3(0f, t * height, t * t * height * 0.34f));
                Radii.Add(Mathf.Lerp(1.35f, 0.16f, t * t));
            }
            Color32 tip = new Color32((byte)(metal.r * 1.2f), (byte)(metal.g * 1.2f), (byte)(metal.b * 1.25f), 0);
            c.Solid.Tube(Path, Radii, 6, metal, tip, false, 0.34f);
            // A strip of light up the inside of the curve.
            for (int i = 2; i < steps; i += 2)
            {
                Vector3 p = Path[i] + new Vector3(0f, 0f, -Radii[i] * 0.3f);
                c.Solid.Lump(p, new Vector3(0.28f, 0.5f, 0.28f), glow, 5);
            }
            Vector3 world = TipWorld(x, z, yaw, Path[steps], c);
            c.Glow.Uv = new Vector2(0.4f, r.Range(0f, 6.28f));
            c.Glow.Halo(world, 2.2f, new Color32(glow.r, glow.g, glow.b, 100));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        private static Vector3 TipWorld(float x, float z, float yaw, Vector3 local, TerrainCanvas c)
        {
            float cs = Mathf.Cos(yaw * Mathf.Deg2Rad), sn = Mathf.Sin(yaw * Mathf.Deg2Rad);
            return new Vector3(x + local.x * cs + local.z * sn, c.GroundAt(x, z) + local.y, z - local.x * sn + local.z * cs);
        }

        /// <summary>A jagged fin of rock.</summary>
        public static bool RockFin(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float height, Color32 color)
        {
            if (!c.Stand(x, z, height * 0.5f, height, 0.5f)) return false;
            c.PlaceSolid(x, z, yaw);
            color.a = 0;
            Color32 top = new Color32((byte)Mathf.Min(255, color.r * 1.35f), (byte)Mathf.Min(255, color.g * 1.35f), (byte)Mathf.Min(255, color.b * 1.4f), 0);
            c.Solid.Shard(Vector3.zero, height * 0.3f, height, new Vector3(r.Range(-0.15f, 0.15f), 0f, r.Range(-0.15f, 0.15f)), 4, color, top, r.Range(0f, 1f));
            c.Solid.Shard(new Vector3(height * 0.3f, 0f, 0f), height * 0.2f, height * 0.6f, new Vector3(0.2f, 0f, 0f), 4, color, top, r.Range(0f, 1f));
            return true;
        }

        /// <summary>A smooth dark monolith with a floating glowing ball over it.</summary>
        public static bool Obelisk(TerrainCanvas c, TerrainRandom r, float x, float z, float height, Color32 metal, Color32 glow)
        {
            if (!c.Stand(x, z, 2.3f, height + 3f, 0.5f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 90f));
            c.Solid.Box(Vector3.zero, new Vector3(3.2f, height, 3.2f), metal, 0.42f);
            c.Solid.Box(Vector3.zero, new Vector3(4.6f, 0.5f, 4.6f), metal, 0.8f);
            c.Solid.Uv = new Vector2(0.25f, r.Range(0f, 6.28f));
            c.Solid.Lump(Vector3.up * (height + 2f), Vector3.one * 0.85f, glow, 8);
            c.Solid.Uv = Vector2.zero;
            c.Glow.Uv = new Vector2(0.45f, r.Range(0f, 6.28f));
            c.Glow.Halo(new Vector3(x, c.GroundAt(x, z) + height + 2f, z), 2.6f, new Color32(glow.r, glow.g, glow.b, 90));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A soaring pair of curved blades that lean together: a gate. Only for the land to the sides and north.</summary>
        public static bool Arch(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float height, Color32 metal, Color32 glow)
        {
            float span = height * 0.62f;
            if (!c.Stand(x, z, span * 0.62f, height, 0.45f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32 tip = new Color32((byte)(metal.r * 1.3f), (byte)(metal.g * 1.3f), (byte)(metal.b * 1.3f), 0);
            for (int k = -1; k <= 1; k += 2)
            {
                Path.Clear();
                Radii.Clear();
                int steps = 10;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float a = t * Mathf.PI * 0.5f;
                    Path.Add(new Vector3(k * Mathf.Cos(a) * span * 0.5f, Mathf.Sin(a) * height, 0f));
                    Radii.Add(Mathf.Lerp(1.9f, 0.3f, t));
                }
                c.Solid.Tube(Path, Radii, 6, metal, tip, false, 0.4f);
            }
            Vector3 top = new Vector3(x, c.GroundAt(x, z) + height * 0.9f, z);
            c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
            c.Glow.Halo(top, height * 0.12f, new Color32(glow.r, glow.g, glow.b, 46));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A low round stepped dais of dark metal, with a ring of light.</summary>
        public static bool Dais(TerrainCanvas c, TerrainRandom r, float x, float z, float radius, Color32 metal, Color32 glow)
        {
            if (!c.Stand(x, z, radius, 2.2f, 0.4f)) return false;
            c.PlaceSolid(x, z);
            c.Solid.Cylinder(Vector3.zero, radius, radius * 0.92f, 1f, 16, metal);
            c.Solid.Cylinder(Vector3.up * 1f, radius * 0.72f, radius * 0.66f, 1.1f, 16, metal);
            c.Glow.Uv = new Vector2(0.3f, r.Range(0f, 6f));
            float y = c.GroundAt(x, z) + 2.12f;
            c.Glow.FlatRing(y, x, z, radius * 0.5f, radius * 0.56f, new Color32(glow.r, glow.g, glow.b, 200), 20);
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        // ------------------------------------------------------------------ Flood

        /// <summary>A thick tendril arching out of the ground and back in. Only ever stands off the arena.</summary>
        public static bool Tendril(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float length, float height, float thickness, Color32 root, Color32 tip)
        {
            if (!c.Stand(x, z, length * 0.55f, height, 0.45f)) return false;
            c.PlaceSolid(x, z, yaw);
            Path.Clear();
            Radii.Clear();
            int steps = 10;
            float wobbleA = r.Range(0.7f, 1.4f), wobbleB = r.Range(0f, 6.28f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float along = (t - 0.5f) * length;
                // Uneven: rears up steeply at the root, sags on the way down.
                float y = Mathf.Sin(Mathf.Pow(t, 0.75f) * Mathf.PI) * height * (1f + 0.25f * Mathf.Sin(t * 9f + wobbleB)) - 0.3f;
                float side = Mathf.Sin(t * 6.28f * wobbleA + wobbleB) * length * 0.12f;
                Path.Add(new Vector3(along, y, side));
                Radii.Add(thickness * 1.5f * Mathf.Lerp(1.15f, 0.45f, t) * (0.85f + 0.3f * Mathf.Sin(t * 19f + wobbleB)));
            }
            c.Solid.Uv = new Vector2(0.09f * thickness, r.Range(0f, 6.28f));
            c.Solid.Tube(Path, Radii, 6, root, tip);
            c.Solid.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A single tendril rearing up from the ground, tapering to a point, stirring slowly.</summary>
        public static bool RearingTendril(TerrainCanvas c, TerrainRandom r, float x, float z, float height, float thickness, Color32 root, Color32 tip)
        {
            if (!c.Stand(x, z, thickness * 2.4f + height * 0.4f, height, 0.45f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            Path.Clear();
            Radii.Clear();
            int steps = 9;
            float bend = r.Range(0.1f, 0.34f) * height;
            float phase = r.Range(0f, 6.28f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Path.Add(new Vector3(Mathf.Sin(t * 2.2f + phase) * bend * t, t * height - 0.3f, t * t * bend));
                Radii.Add(thickness * Mathf.Lerp(1f, 0.1f, t));
            }
            c.Solid.Uv = new Vector2(0.14f * thickness, r.Range(0f, 6.28f));
            c.Solid.Tube(Path, Radii, 5, root, tip);
            c.Solid.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A swollen pod with a pale glowing top.</summary>
        public static bool Pod(TerrainCanvas c, TerrainRandom r, float x, float z, float size, Color32 skin, Color32 glow)
        {
            if (!c.Stand(x, z, size * 1.2f, size * 2.2f, 0.45f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            c.Solid.Lump(Vector3.up * (size * 0.8f), new Vector3(size, size * 1.15f, size), skin, 10, 0.18f, r.Int(100), glow);
            c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
            c.Glow.Halo(new Vector3(x, c.GroundAt(x, z) + size * 1.6f, z), size * 1.5f, new Color32(glow.r, glow.g, glow.b, 36));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A tall ribbed spire of hardened biomass with a glowing crown of pods. Only for the land to the north and sides.</summary>
        public static bool HiveSpire(TerrainCanvas c, TerrainRandom r, float x, float z, float height, Color32 skin, Color32 pale, Color32 glow)
        {
            float bend = height * 0.12f;
            if (!c.Stand(x, z, height * 0.22f + bend, height, 0.55f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            Path.Clear();
            Radii.Clear();
            int steps = 10;
            float width = height * 0.14f;
            float phase = r.Range(0f, 6.28f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Path.Add(new Vector3(Mathf.Sin(t * 3f + phase) * bend * t, t * height, Mathf.Cos(t * 2.3f + phase) * bend * t));
                float rib = 1f + 0.18f * Mathf.Sin(t * 28f);
                Radii.Add(width * Mathf.Lerp(1f, 0.28f, t) * rib);
            }
            c.Solid.Tube(Path, Radii, 7, skin, pale);
            Vector3 crown = Path[steps];
            int pods = 3 + r.Int(3);
            for (int i = 0; i < pods; i++)
            {
                float a = i * 6.28f / pods;
                var at = crown + new Vector3(Mathf.Cos(a) * width * 0.4f, r.Range(-1.4f, 0.6f), Mathf.Sin(a) * width * 0.4f);
                c.Solid.Lump(at, Vector3.one * width * 0.3f, pale, 6, 0f, 0, glow);
            }
            return true;
        }

        /// <summary>A heap of swollen lumps: biomass piled up on the ground.</summary>
        public static bool Mound(TerrainCanvas c, TerrainRandom r, float x, float z, float size, Color32 skin, Color32 pale)
        {
            if (!c.Stand(x, z, size * 1.4f, size * 1.3f, 0.5f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            int count = 3 + r.Int(3);
            for (int i = 0; i < count; i++)
            {
                float a = r.Range(0f, 6.28f);
                float d = i == 0 ? 0f : r.Range(0.3f, 0.9f) * size;
                float s = (i == 0 ? 1f : r.Range(0.4f, 0.7f)) * size;
                c.Solid.Lump(new Vector3(Mathf.Cos(a) * d, s * 0.3f, Mathf.Sin(a) * d), new Vector3(s, s * 0.8f, s), skin, 8, 0.25f, r.Int(100), pale);
            }
            return true;
        }

        /// <summary>A curved rib of a great wreck, rising from the ground. A pair of these in a row looks like a hull picked clean.</summary>
        public static bool Rib(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float span, float height, Color32 color, Color32 end)
        {
            if (!c.Stand(x, z, span * 0.55f, height, 0.45f)) return false;
            c.PlaceSolid(x, z, yaw);
            Path.Clear();
            Radii.Clear();
            int steps = 9;
            float broken = r.Range(0.72f, 1f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps * broken;
                float a = t * Mathf.PI;
                Path.Add(new Vector3(-Mathf.Cos(a) * span * 0.5f, Mathf.Sin(a) * height, 0f));
                Radii.Add(Mathf.Lerp(0.85f, 0.45f, Mathf.Sin(a)));
            }
            c.Solid.Tube(Path, Radii, 5, color, end);
            return true;
        }

        /// <summary>A rusted container on its side, half swallowed by biomass.</summary>
        public static bool DeadContainer(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, Color32 skin, Color32 pale)
        {
            if (!c.Stand(x, z, 7.2f, 4.4f, 0.5f)) return false;
            c.PlaceSolid(x, z, yaw);
            Color32[] paints = { C(96, 58, 40), C(72, 66, 54), C(84, 74, 44) };
            Color32 paint = Vary(r, paints[r.Int(paints.Length)], 0.12f);
            float tilt = r.Range(0.3f, 1.0f);
            // A tipped box: a skew made by hand.
            var foot = new Vector3(0f, -0.3f, 0f);
            c.Solid.Box(foot, new Vector3(12.2f, 2.9f + tilt, 2.9f), paint, 0.92f);
            c.Solid.Lump(new Vector3(r.Range(-5f, -2f), 0.6f, r.Range(-1.5f, 1.5f)), new Vector3(3.6f, 2.2f, 2.8f), skin, 8, 0.3f, r.Int(100), pale);
            c.Solid.Lump(new Vector3(r.Range(2f, 5f), 3f, 0f), new Vector3(2.2f, 1.2f, 1.8f), skin, 7, 0.3f, r.Int(100), pale);
            return true;
        }

        /// <summary>A mast or tower bent over and strangled by a tendril.</summary>
        public static bool DeadMast(TerrainCanvas c, TerrainRandom r, float x, float z, float height, Color32 skin, Color32 pale)
        {
            if (!c.Stand(x, z, Mathf.Max(2.2f, height * 0.55f), height, 0.45f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            Path.Clear();
            Radii.Clear();
            for (int i = 0; i <= 5; i++)
            {
                float t = i / 5f;
                Path.Add(new Vector3(t * t * height * 0.5f, t * height * (1f - t * 0.25f), 0f));
                Radii.Add(Mathf.Lerp(0.34f, 0.14f, t));
            }
            c.Solid.Tube(Path, Radii, 5, C(84, 74, 62), C(108, 94, 78));
            // The strangler: a spiral around it.
            var coil = new List<Vector3>();
            var coilRadii = new List<float>();
            for (int i = 0; i <= 18; i++)
            {
                float t = i / 18f;
                float a = t * 15f;
                Vector3 p = new Vector3(t * t * height * 0.45f, t * height * 0.72f * (1f - t * 0.2f), 0f);
                coil.Add(p + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * Mathf.Lerp(0.8f, 0.3f, t));
                coilRadii.Add(Mathf.Lerp(0.36f, 0.12f, t));
            }
            c.Solid.Tube(coil, coilRadii, 4, skin, pale);
            return true;
        }
    }
}
