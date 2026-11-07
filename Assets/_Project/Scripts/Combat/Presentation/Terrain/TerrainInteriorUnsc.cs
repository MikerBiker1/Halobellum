// TerrainInteriorUnsc.cs
// The UNSC military installation (D80): the whole field is one hangar deck inside a ship or a base. Gunmetal steel deck plates of 4 m with structural seams every 16 m with small cool lights at their crossings, worn traffic lanes,
// yellow and dark hazard bands along the back of each zone, lift pads and turntables in the corners, vent grilles, faint painted lanes, cool light strips set into the deck beyond the floor's edges,
// and pools of light from lamps overhead. Around it: a low rail beyond the flank strips, a grated service trench with tall pillars banded in yellow and dark, yards of cargo containers, crates on
// pallets, weapon racks, generators, compressors, tanks with pipes, tow tractors and gantry cranes, a dropship hanging in its cradle at each end, and ribbed bulkheads with lit doors, catwalks
// and a great bay door, under a dark grey-blue ceiling. Every shape is a plain box, tube or flat paint: no logos, emblems, glyphs or text, and no real craft or vehicle.
// The ground textures come from Tools/TerrainGen/make_interior_unsc.py. Looks only: nothing here touches the simulation.
// Depends on: TerrainLook, TerrainLayout, TerrainTheme, TerrainCanvas, TerrainMeshBuilder, TerrainNoise, TerrainRandom, BattleFactions, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class UnscInterior
    {
        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        public static TerrainLook Create() => new TerrainLook
        {
            Id = "UnscInstallation",
            Name = "UNSC Installation",
            Theme = new TerrainTheme
            {
                Faction = BattleFactions.Unsc,
                Layers = new[] { "InteriorUnscDeck", "InteriorUnscGrate", "InteriorUnscWorn" },
                // Deck plates of 4 m (a tile of 16 m holds four by four, each with its own split, hatch, vent or tread, so nothing repeats closer than 16 m),
                // grate panels of 4 m (the grate is drawn on a turned lattice that lines up with the world), worn steel every 10 m.
                Tiles = new Vector3(1f / 16f, 1f / 20f, 1f / 10f),
                GroundTint = Color.white,
                Hills = TerrainHillStyle.Flat,
                HillHeight = 0f,
                FarHeight = 0f,
                // The ceiling: a dark grey-blue, only seen in low views.
                SkyTop = Rgb(14, 17, 22),
                SkyHorizon = Rgb(36, 40, 48),
                Cloud = Color.black,
                CloudAmount = 0f,
                MoonAmount = 0f,
                // Clean, cool industrial light from high overhead.
                SunColor = Rgb(240, 246, 255),
                SunIntensity = 1.15f,
                SunAngles = new Vector2(62f, -24f),
                AmbientSky = Rgb(138, 148, 164),
                AmbientEquator = Rgb(126, 130, 140),
                AmbientGround = Rgb(80, 80, 84),
                // Dark steel air: the deck beyond the bulkheads melts into it.
                FogColor = Rgb(26, 29, 34),
                FogStart = 140f,
                FogEnd = 320f,
                FogAmount = 1f,
                Haze = Rgb(92, 100, 112),
                HazeAmount = 0.06f,
                MoteColor = Rgb(236, 232, 214),
                MoteCount = 46,
                // Deck plates shine a little (raised plates more than seams), grating and worn steel hardly at all.
                Gloss = new Vector4(0.3f, 0.1f, 0.16f, 30f),
                GlossColor = new Color(0.86f, 0.92f, 1f, 0.05f),
                GlossLightAngles = new Vector2(48f, 180f),
                // A mild, cool mood.
                Vignette = 0.25f,
                VignetteColor = new Color(0.02f, 0.03f, 0.05f),
                ColorFilter = new Color(0.96f, 0.98f, 1f),
                Contrast = 6f,
                Saturation = -4f,
                Exposure = 0f,
            },
            Layout = new UnscInstallationLayout(),
        };
    }

    public sealed class UnscInstallationLayout : TerrainLayout
    {
        public override string Name => "UNSC Installation";

        // Where things stand, in metres from the middle line (v) or out from the middle (x).
        private const float RailX = 94.5f;
        private const float RailEndV = 86f;
        private const float PillarX = 100f;
        private const float SideWallX = 128f;
        private const float WallHeight = 28f;
        private const float RoofReach = 120f;
        /// <summary>The dark roof over everything behind the bulkheads, a little above the tops of their beams.</summary>
        private const float RoofY = WallHeight + 4.7f;

        // Solid colours: alpha is glow, so plain surfaces have 0.
        private static readonly Color32 WallGrey = S(122, 128, 136);
        private static readonly Color32 Panel = S(142, 148, 156);
        private static readonly Color32 Panel2 = S(134, 140, 148);
        private static readonly Color32 Rib = S(104, 110, 118);
        private static readonly Color32 Rib2 = S(120, 126, 134);
        private static readonly Color32 Dark = S(74, 79, 86);
        private static readonly Color32 VentDark = S(54, 58, 64);
        private static readonly Color32 Steel = S(132, 136, 142);
        private static readonly Color32 Yellow = S(196, 156, 46);
        private static readonly Color32 HazardDark = S(40, 40, 38);
        private static readonly Color32 Roof = S(30, 33, 38);
        private static readonly Color32 DoorDark = S(80, 86, 94);
        private static readonly Color32 Pallet = S(104, 92, 74);
        private static readonly Color32 Gun = S(48, 50, 54);
        private static readonly Color32 Tyre = S(36, 37, 40);
        private static readonly Color32 Hull = S(98, 106, 92);
        private static readonly Color32 Lamp = S(196, 224, 255, 230);
        private static readonly Color32 Amber = S(255, 168, 56, 230);
        private static readonly Color32 Window = S(120, 150, 184, 120);

        // Light on the deck.
        private static readonly Color32 CoolLight = new Color32(196, 222, 255, 255);
        private static readonly Color32 PoolLight = new Color32(226, 236, 255, 255);

        // ------------------------------------------------------------------ ground layers

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float ax = Mathf.Abs(x), az = Mathf.Abs(z);
            // Grated service trenches: alongside the field beyond the rail, and across each end.
            float sideTrench = Band(ax, 96f, 104f) * (1f - TerrainNoise.Smooth(91.5f, 92.5f, az));
            float endTrench = Band(az, 88f, 92f) * (1f - TerrainNoise.Smooth(103.5f, 104.5f, ax));
            // Beyond the bulkheads, service grating (mostly under the roofs and the dark).
            float beyond = Mathf.Max(TerrainNoise.Smooth(131f, 134f, ax), TerrainNoise.Smooth(117f, 120f, az));
            second = Mathf.Max(Mathf.Max(sideTrench, endTrench), beyond);

            // Worn, oily steel fills the yards around the machinery beyond the trenches, never the floor or the flank strips (the floor's wear is paint: see Floor).
            // Each ground meets the next in a sharp step under a steel curb, since the ground shader roughens any weight between 0 and 1 with fine noise, which on steel reads as gravel.
            float yard = Mathf.Max(TerrainNoise.Smooth(103.6f, 104.6f, ax), TerrainNoise.Smooth(91.6f, 92.6f, az));
            third = yard * (1f - second);
        }

        private static float Band(float value, float from, float to) =>
            TerrainNoise.Smooth(from - 0.5f, from + 0.5f, value) * (1f - TerrainNoise.Smooth(to - 0.5f, to + 0.5f, value));

        // ------------------------------------------------------------------ one half

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            float endV = EndWallV(c);
            Floor(c, r);
            Edges(c, r);
            // Big things first, so that the yards fill in around them.
            Walls(c, r, endV);
            Pillars(c, r, endV);
            Rails(c);
            Beacons(c, r);
            Gantries(c, r, endV);
            EndYard(c, r, endV);
            SideYards(c, r, endV);
        }

        /// <summary>
        /// The end bulkhead's face. To the north it stands close, so the camera sees its doors; to the south it must stand back further, since tall things in line with the floor
        /// there could hide the field (TerrainCanvas.SouthLean).
        /// </summary>
        private static float EndWallV(TerrainCanvas c) => c.Side > 0 ? 108.5f : 114f;

        // ------------------------------------------------------------------ the floor

        private static void Floor(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;

            // Traffic lanes up the zone and one across it, worn by wheels: soft grime in two tracks, paint over the deck so its seams still show.
            float laneX = 20f + r.Int(3) * 8f;
            float[] laneEnd = new float[2];
            var track = new List<Vector2>(4);
            for (int k = 0; k < 2; k++)
            {
                float sign = k == 0 ? -1f : 1f;
                laneEnd[k] = sign * laneX + r.Range(-5f, 5f);
                for (int t = -1; t <= 1; t += 2)
                    Wear(c, r, track, sign * laneX + t * 1.6f, c.Z(2.5f), laneEnd[k] + t * 1.6f, c.Z(E - 0.5f), 1.9f);
                Wear(c, r, track, sign * laneX, c.Z(2.5f), laneEnd[k], c.Z(E - 0.5f), 6.5f);
            }
            float crossV = r.Range(36f, 56f), crossEnd = crossV + r.Range(-4f, 4f);
            for (int t = -1; t <= 1; t += 2)
                Wear(c, r, track, -W + 0.5f, c.Z(crossV + t * 1.5f), W - 0.5f, c.Z(crossEnd + t * 1.5f), 1.8f);

            // Oil and grime, never dark enough to hide anything.
            for (int i = 0; i < 16; i++)
                Stain(c, r.Range(-W + 3f, W - 3f), c.Z(r.Range(6f, E - 3f)), r.Range(0.8f, 2.8f), C(48, 46, 44, 60 + r.Int(56)));

            StructuralSeams(c);

            // Faint, worn dashes along the lanes.
            c.Decals.Uv = new Vector2(0.45f, 0.25f);
            Color32 dash = C(214, 216, 208, 78);
            for (int k = 0; k < 2; k++)
            {
                float sign = k == 0 ? -1f : 1f;
                for (float v = 8f; v < E - 6f; v += 7f)
                {
                    float t = (v - 3f) / (E - 4f);
                    float x = Mathf.Lerp(sign * laneX, laneEnd[k], t);
                    c.Decals.FlatRect(D, x, c.Z(v), 0.28f, 3.4f, 0f, dash);
                }
            }
            c.Decals.Uv = Vector2.zero;

            // Lift pads and turntables in the corners.
            var pads = new List<Vector2>();
            float padSide = r.Sign();
            pads.Add(new Vector2(padSide * r.Range(54f, 62f), r.Range(58f, 68f)));
            if (r.Chance(0.7f)) pads.Add(new Vector2(-padSide * r.Range(54f, 62f), r.Range(22f, 66f)));
            foreach (Vector2 pad in pads)
            {
                if (r.Chance(0.55f)) LiftPad(c, r, pad.x, c.Z(pad.y));
                else Turntable(c, r, pad.x, c.Z(pad.y));
            }
            SeamLights(c, pads);

            // Vent grilles and hatches, set into deck plates.
            for (int i = 0; i < 14; i++)
            {
                float x = (r.Int(38) - 19) * 4f + 2f;
                float v = r.Int(19) * 4f + 6f;
                bool near = false;
                foreach (Vector2 pad in pads)
                    if (Mathf.Abs(pad.x - x) < 11f && Mathf.Abs(pad.y - v) < 11f) near = true;
                if (near || Mathf.Abs(x) > W - 4f || v > E - 4f) continue;
                if (i % 3 == 2) Hatch(c, x, c.Z(v));
                else Vent(c, x, c.Z(v), r.Chance(0.5f));
            }

            // Pools of light from lamps overhead.
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iv = 0; iv < 2; iv++)
                    LightPool(c, ix * 48f + r.Range(-3f, 3f), c.Z(26f + iv * 32f + r.Range(-3f, 3f)), 16f, PoolLight, 0.3f);
            }
        }

        /// <summary>A soft band of wheel grime from a to b, gently wavering, faded in and out along its length. Paint: never dark or solid enough to hide a unit.</summary>
        private static void Wear(TerrainCanvas c, TerrainRandom r, List<Vector2> points, float ax, float az, float bx, float bz, float width)
        {
            points.Clear();
            const int steps = 6;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float wobble = i == 0 || i == steps ? 0f : r.Range(-0.5f, 0.5f);
                float dx = bx - ax, dz = bz - az, length = Mathf.Max(0.01f, Mathf.Sqrt(dx * dx + dz * dz));
                points.Add(new Vector2(ax + dx * t - dz / length * wobble, az + dz * t + dx / length * wobble));
            }
            c.Decals.Uv = new Vector2(0.15f, 0.45f);
            c.Decals.FlatRibbon(D, points, width, width * r.Range(0.8f, 1.2f), C(56, 54, 52, 26 + r.Int(14)), true, C(56, 54, 52, 16 + r.Int(16)), 6f);
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>The heavier seams of the deck's frame, every 16 m: a darker groove with a lit lip on its far side.</summary>
        private static void StructuralSeams(TerrainCanvas c)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            Color32 groove = C(72, 75, 80, 150);
            Color32 lip = C(156, 160, 166, 54);
            c.Decals.Uv = new Vector2(0f, 0.2f);
            float mid = (E + 2f) * 0.5f, length = E - 2.6f;
            for (int i = -4; i <= 4; i++)
            {
                float x = i * 16f;
                c.Decals.FlatRect(D, x, c.Z(mid), 0.2f, length, 0f, groove);
                c.Decals.FlatRect(D, x + 0.17f, c.Z(mid), 0.07f, length, 0f, lip);
            }
            for (int j = 1; j <= 4; j++)
            {
                float z = c.Z(4f + j * 16f);
                c.Decals.FlatRect(D, 0f, z, W * 2f - 0.6f, 0.2f, 0f, groove);
                c.Decals.FlatRect(D, 0f, z + 0.17f, W * 2f - 0.6f, 0.07f, 0f, lip);
            }
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>
        /// A small, dim, cool light set flush into the deck at each crossing of the 16 m seams, slowly breathing, kept off the lift pads and turntables.
        /// Its phase comes from where it stands, not from the random stream, so nothing else in the layout moves.
        /// </summary>
        private static void SeamLights(TerrainCanvas c, List<Vector2> pads)
        {
            Color32 heart = C(196, 222, 255, 90), rim = C(196, 222, 255, 0);
            for (int i = -4; i <= 4; i++)
            {
                for (int j = 1; j <= 4; j++)
                {
                    float x = i * 16f, v = 4f + j * 16f;
                    bool near = false;
                    foreach (Vector2 pad in pads)
                        if (Mathf.Abs(pad.x - x) < 11f && Mathf.Abs(pad.y - v) < 11f) near = true;
                    if (near || Mathf.Abs(x) > c.HalfWidth - 2f || v > c.EdgeV - 2f) continue;
                    c.Glow.Uv = new Vector2(0.6f, Mathf.Repeat((i + 5) * 2.39f + j * 1.31f, 6.28f));
                    c.Glow.FlatDisc(G, x, c.Z(v), 0.22f, 0.22f, heart, rim, 8);
                }
            }
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>A square lift platform: four leaves, a dark gap round it, a hazard border, chevrons and amber corner lights.</summary>
        private static void LiftPad(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            const float size = 12f, half = 6f;
            Stain(c, x, z, 10f, C(56, 54, 52, 34));
            c.Decals.Uv = new Vector2(0f, 0.3f);
            c.Decals.FlatRect(D, x, z, size, size, 0f, C(98, 101, 106, 120));
            Color32 gap = C(66, 69, 74, 170);
            c.Decals.FlatRect(D, x, z, 0.16f, size, 0f, gap);
            c.Decals.FlatRect(D, x, z, size, 0.16f, 0f, gap);
            c.Decals.FlatRect(D, x - half, z, 0.26f, size + 0.26f, 0f, gap);
            c.Decals.FlatRect(D, x + half, z, 0.26f, size + 0.26f, 0f, gap);
            c.Decals.FlatRect(D, x, z - half, size + 0.26f, 0.26f, 0f, gap);
            c.Decals.FlatRect(D, x, z + half, size + 0.26f, 0.26f, 0f, gap);

            c.Decals.Uv = new Vector2(0.28f, 0.2f);
            const float band = 0.8f, o = half + 0.65f;
            HazardBand(c, x - o - 0.4f, z - o, x + o + 0.4f, z - o, band, 1.4f, 190);
            HazardBand(c, x - o - 0.4f, z + o, x + o + 0.4f, z + o, band, 1.4f, 190);
            HazardBand(c, x - o, z - o + 0.4f, x - o, z + o - 0.4f, band, 1.4f, 190);
            HazardBand(c, x + o, z - o + 0.4f, x + o, z + o - 0.4f, band, 1.4f, 190);
            // Chevrons pointing onto the pad from two sides.
            Color32 chevron = C(206, 168, 56, 150);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 3; i++)
                    Chevron(c, x + s * (half - 1.4f - i * 0.8f), z, s > 0 ? 270f : 90f, 2f, 0.3f, chevron);
            }
            c.Decals.Uv = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                float cx = x + ((i & 1) == 0 ? -1f : 1f) * (half - 0.6f);
                float cz = z + ((i & 2) == 0 ? -1f : 1f) * (half - 0.6f);
                c.Glow.Uv = new Vector2(0.6f, r.Range(0f, 6.28f));
                c.Glow.FlatDisc(G, cx, cz, 0.32f, 0.32f, C(255, 176, 70, 150), C(255, 176, 70, 0), 8);
            }
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>A round turntable: a plate with radial seams in a dark gap, a ring of hazard sectors, chevrons towards its hub and amber lights.</summary>
        private static void Turntable(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            const float radius = 6.5f;
            c.Decals.Uv = new Vector2(0f, 0.3f);
            c.Decals.FlatDisc(D, x, z, radius, radius, C(100, 103, 108, 115), C(100, 103, 108, 115), 32);
            Color32 gap = C(64, 67, 72, 180);
            c.Decals.FlatRing(D, x, z, radius - 0.12f, radius + 0.12f, gap, 40);
            for (int i = 0; i < 3; i++)
                c.Decals.FlatRect(D, x, z, 0.14f, radius * 2f - 0.5f, i * 60f + 15f, C(70, 73, 78, 150));
            c.Decals.FlatDisc(D, x, z, 1.3f, 1.3f, C(128, 132, 138, 140), C(128, 132, 138, 140), 16);
            c.Decals.FlatRing(D, x, z, 1.3f, 1.45f, gap, 20);

            c.Decals.Uv = new Vector2(0.28f, 0.2f);
            Color32 yellow = C(200, 162, 50, 190), dark = C(56, 55, 52, 190);
            for (int i = 0; i < 24; i++)
                c.Decals.FlatRing(D, x, z, radius + 0.45f, radius + 1.25f, i % 2 == 0 ? yellow : dark, 3, i * 15f, i * 15f + 15f);
            Color32 chevron = C(206, 168, 56, 140);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                float ra = a * Mathf.Deg2Rad;
                // Each points in towards the hub.
                Chevron(c, x + Mathf.Cos(ra) * 3.9f, z + Mathf.Sin(ra) * 3.9f, 270f - a, 1.4f, 0.28f, chevron);
            }
            c.Decals.Uv = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                c.Glow.Uv = new Vector2(0.6f, r.Range(0f, 6.28f));
                c.Glow.FlatDisc(G, x + Mathf.Cos(a) * (radius + 1.7f), z + Mathf.Sin(a) * (radius + 1.7f), 0.3f, 0.3f, C(255, 176, 70, 150), C(255, 176, 70, 0), 8);
            }
            c.Glow.Uv = Vector2.zero;
            c.Decals.FlatRing(D, x, z, radius + 1.3f, radius + 3.4f, C(56, 54, 52, 36), 24, 0f, 360f, C(56, 54, 52, 0));
        }

        /// <summary>A floor vent: a lighter frame, a dark recess and slats.</summary>
        private static void Vent(TerrainCanvas c, float x, float z, bool alongZ)
        {
            float w = alongZ ? 1.6f : 3.2f, d = alongZ ? 3.2f : 1.6f;
            c.Decals.Uv = new Vector2(0f, 0.25f);
            c.Decals.FlatRect(D, x, z, w + 0.3f, d + 0.3f, 0f, C(136, 140, 146, 90));
            c.Decals.FlatRect(D, x, z, w, d, 0f, C(76, 79, 84, 160));
            Color32 slat = C(124, 128, 134, 150);
            const int slats = 7;
            for (int i = 0; i < slats; i++)
            {
                float t = (i + 0.5f) / slats - 0.5f;
                if (alongZ) c.Decals.FlatRect(D, x, z + t * d, w - 0.2f, 0.09f, 0f, slat);
                else c.Decals.FlatRect(D, x + t * w, z, 0.09f, d - 0.2f, 0f, slat);
            }
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>A small square hatch with a seam and four bolts.</summary>
        private static void Hatch(TerrainCanvas c, float x, float z)
        {
            c.Decals.Uv = new Vector2(0f, 0.25f);
            c.Decals.FlatRect(D, x, z, 1.9f, 1.9f, 0f, C(70, 73, 78, 140));
            c.Decals.FlatRect(D, x, z, 1.7f, 1.7f, 0f, C(116, 120, 126, 150));
            Color32 bolt = C(150, 154, 160, 170);
            for (int i = 0; i < 4; i++)
                c.Decals.FlatRect(D, x + ((i & 1) == 0 ? -0.62f : 0.62f), z + ((i & 2) == 0 ? -0.62f : 0.62f), 0.14f, 0.14f, 0f, bolt);
            c.Decals.FlatRect(D, x, z - 0.3f, 0.6f, 0.12f, 0f, C(80, 83, 88, 150));
            c.Decals.Uv = Vector2.zero;
        }

        // ------------------------------------------------------------------ the floor's edges

        private static void Edges(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;

            // A thin yellow line just inside each long edge, with hazard strips at the back corners.
            c.Decals.Uv = new Vector2(0.3f, 0.2f);
            for (int k = -1; k <= 1; k += 2)
            {
                c.Decals.FlatRect(D, k * (W - 0.7f), c.Z(E * 0.5f + 1f), 0.22f, E - 2f, 0f, C(204, 166, 54, 170));
                HazardBand(c, k * (W - 1.5f), c.Z(E - 13f), k * (W - 1.5f), c.Z(E - 0.6f), 0.9f, 1.3f, 170);
            }
            // The back of the zone: a hazard band just beyond the floor's edge, between two steel strips.
            c.Decals.Uv = new Vector2(0.22f, 0.2f);
            HazardBand(c, -W - 1.6f, c.Z(E + 1.1f), W + 1.6f, c.Z(E + 1.1f), 1.2f, 1.5f, 220);
            c.Decals.FlatRect(D, 0f, c.Z(E + 0.35f), W * 2f + 3.2f, 0.22f, 0f, C(150, 154, 160, 200));
            c.Decals.FlatRect(D, 0f, c.Z(E + 1.85f), W * 2f + 3.2f, 0.22f, 0f, C(150, 154, 160, 200));

            // Edges of the flank strips, where flyers deploy: thin dashed lines only.
            c.Decals.Uv = new Vector2(0.35f, 0.2f);
            Color32 flank = C(200, 164, 56, 110);
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 2f; v < E; v += 5f)
                {
                    c.Decals.FlatRect(D, k * 84f, c.Z(v), 0.2f, 2.6f, 0f, flank);
                    c.Decals.FlatRect(D, k * 92f, c.Z(v), 0.2f, 2.6f, 0f, flank);
                }
            }

            // Steel curbs framing the grated trenches, crisp over the soft edge of the grating.
            c.Decals.Uv = new Vector2(0f, 0.4f);
            Color32 curb = C(132, 136, 142, 235);
            for (int k = -1; k <= 1; k += 2)
            {
                c.Decals.FlatRect(D, k * 95.9f, c.Z(44f), 0.4f, 88f, 0f, curb);
                c.Decals.FlatRect(D, k * 104.1f, c.Z(46f), 0.4f, 92f, 0f, curb);
            }
            c.Decals.FlatRect(D, 0f, c.Z(87.9f), 191.8f, 0.4f, 0f, curb);
            c.Decals.FlatRect(D, 0f, c.Z(92.1f), 208.6f, 0.4f, 0f, curb);
            c.Decals.Uv = Vector2.zero;

            // Light strips set into the deck: along both long edges just beyond the floor, and across the back beyond the hazard band.
            var points = new List<Vector2>(2);
            for (int k = -1; k <= 1; k += 2)
            {
                points.Clear();
                points.Add(new Vector2(k * (W + 1.2f), c.Z(0.4f)));
                points.Add(new Vector2(k * (W + 1.2f), c.Z(E + 2.6f)));
                LightStrip(c, points, 0.12f, CoolLight);
            }
            points.Clear();
            points.Add(new Vector2(-W - 1.2f, c.Z(E + 2.6f)));
            points.Add(new Vector2(W + 1.2f, c.Z(E + 2.6f)));
            LightStrip(c, points, 0.12f, CoolLight);
            // And a row of amber markers along the far side of the end trench.
            for (float x = -96f; x <= 96f; x += 12f)
            {
                c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
                c.Glow.FlatDisc(G, x, c.Z(92.6f), 0.28f, 0.28f, C(255, 176, 70, 170), C(255, 176, 70, 0), 8);
            }
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>A band of diagonal yellow and dark stripes from a to b. Flat paint.</summary>
        private static void HazardBand(TerrainCanvas c, float ax, float az, float bx, float bz, float width, float period, byte alpha)
        {
            float dx = bx - ax, dz = bz - az;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            if (length < 0.01f) return;
            dx /= length;
            dz /= length;
            float sx = -dz, sz = dx, half = width * 0.5f;
            Color32 yellow = C(200, 162, 50, alpha), dark = C(56, 55, 52, alpha);
            FlatQuadCw(c.Decals, ax - sx * half, az - sz * half, ax + sx * half, az + sz * half, bx + sx * half, bz + sz * half, bx - sx * half, bz - sz * half, dark);
            float stripe = period * 0.5f;
            for (float s = 0f; s + width + stripe <= length + 0.001f; s += period)
            {
                float s0 = s, s1 = s + stripe, t0 = s + width, t1 = s + width + stripe;
                FlatQuadCw(c.Decals,
                    ax + dx * s0 - sx * half, az + dz * s0 - sz * half,
                    ax + dx * t0 + sx * half, az + dz * t0 + sz * half,
                    ax + dx * t1 + sx * half, az + dz * t1 + sz * half,
                    ax + dx * s1 - sx * half, az + dz * s1 - sz * half, yellow);
            }
        }

        /// <summary>A painted chevron pointing along yaw (degrees, 0 is +z).</summary>
        private static void Chevron(TerrainCanvas c, float x, float z, float yaw, float size, float thickness, Color32 color)
        {
            float a = yaw * Mathf.Deg2Rad;
            float fx = Mathf.Sin(a), fz = Mathf.Cos(a), sx = Mathf.Cos(a), sz = -Mathf.Sin(a);
            float h = size * 0.5f;
            for (int s = -1; s <= 1; s += 2)
            {
                float w = s * h;
                // Tip, arm end, arm end set back, tip set back.
                float p0 = h, q0 = 0f, p1 = -h, q1 = w, p2 = -h - thickness, q2 = w, p3 = h - thickness, q3 = 0f;
                FlatQuadCw(c.Decals,
                    x + fx * p0 + sx * q0, z + fz * p0 + sz * q0,
                    x + fx * p1 + sx * q1, z + fz * p1 + sz * q1,
                    x + fx * p2 + sx * q2, z + fz * p2 + sz * q2,
                    x + fx * p3 + sx * q3, z + fz * p3 + sz * q3, color);
            }
        }

        /// <summary>A flat four-sided patch of paint, its corners in order either way round: turned clockwise as seen from above, so that it faces up.</summary>
        private static void FlatQuadCw(TerrainMeshBuilder m, float x0, float z0, float x1, float z1, float x2, float z2, float x3, float z3, Color32 color)
        {
            float area = (x0 * z1 - x1 * z0) + (x1 * z2 - x2 * z1) + (x2 * z3 - x3 * z2) + (x3 * z0 - x0 * z3);
            var a = new Vector3(x0, D, z0);
            var b = new Vector3(x1, D, z1);
            var e = new Vector3(x2, D, z2);
            var f = new Vector3(x3, D, z3);
            if (area <= 0f) m.FlatQuad(a, b, e, f, color, color, color, color);
            else m.FlatQuad(f, e, b, a, color, color, color, color);
        }

        // ------------------------------------------------------------------ bulkheads

        private static void Walls(TerrainCanvas c, TerrainRandom r, float endV)
        {
            int doorPhase = r.Int(3);
            float lastSideV = 4f;
            // Side bulkheads, facing the field.
            for (int k = -1; k <= 1; k += 2)
            {
                int i = 0;
                for (float v = 4f; v <= endV - 6.8f; v += 8f, i++)
                {
                    lastSideV = v;
                    float x = k * SideWallX, z = c.Z(v);
                    if (!c.Stand(x, z, 3.95f, WallHeight + 5f, 0.3f)) continue;
                    int kind = (i + doorPhase + (k > 0 ? 1 : 0)) % 3 == 0 ? 1 : 0;
                    WallSegment(c, r, x, z, k * 90f, kind, 0f);
                }
            }
            // The end bulkhead, with a great bay door in its middle.
            float endYaw = c.Side > 0 ? 0f : 180f;
            float localSign = c.Side > 0 ? 1f : -1f;
            for (int j = 0; j < 32; j++)
            {
                float x = -124f + j * 8f;
                float z = c.Z(endV);
                if (!c.Stand(x, z, 3.95f, WallHeight + 5f, 0.3f)) continue;
                bool bay = Mathf.Abs(x) < 13f;
                float post = bay && Mathf.Abs(x) > 8f ? Mathf.Sign(x) * localSign : 0f;
                WallSegment(c, r, x, z, endYaw, bay ? 2 : 0, post);
                if (Mathf.Abs(x) > 123f) Corner(c, Mathf.Sign(x), lastSideV + 4f, endV);
            }
        }

        /// <summary>
        /// One 8 m piece of bulkhead, its face at the given point, turned so that its front (local -z) faces the field. kind: 0 plain, 1 with a lit door, 2 part of the bay door.
        /// post: -1 or +1 puts the bay door's frame post at that end of the piece (local x).
        /// </summary>
        private static void WallSegment(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, int kind, float post)
        {
            TerrainMeshBuilder m = c.Solid;
            c.PlaceSolid(x, z, yaw);
            const float H = WallHeight;
            m.Box(new Vector3(0f, 0f, 1.6f), new Vector3(8f, H, 3.2f), Vary(r, WallGrey, 0.04f));
            // A header at the top with a lit line under it.
            m.Box(new Vector3(0f, H - 2.4f, -0.5f), new Vector3(8f, 2.4f, 1f), Dark);
            m.Box(new Vector3(0f, H - 2.62f, -0.55f), new Vector3(8f, 0.16f, 0.9f), Lamp);
            // The ceiling's edge: a sloped soffit from the header out to the beam on the ribs' chamfers, and a dark roof over everything behind.
            m.Quad(new Vector3(-4f, H, -1f), new Vector3(-4f, H + 4f, -5.4f), new Vector3(4f, H + 4f, -5.4f), new Vector3(4f, H, -1f), Rib);
            m.Box(new Vector3(0f, H + 3.4f, -5.4f), new Vector3(8f, 1.2f, 1.6f), Dark);
            m.Quad(new Vector3(-4f, RoofY, -6.2f), new Vector3(-4f, RoofY, RoofReach), new Vector3(4f, RoofY, RoofReach), new Vector3(4f, RoofY, -6.2f), Roof);

            float ribFrom = 0f;
            if (kind == 2)
            {
                BayDoor(c, m, r, x, z, yaw, post);
                ribFrom = 17.2f;
            }
            else
            {
                if (kind == 1)
                {
                    Door(c, m, r, x, z, yaw);
                    m.Box(new Vector3(-3.3f, 0f, -0.5f), new Vector3(1.4f, 1.3f, 1f), Dark);
                    m.Box(new Vector3(3.3f, 0f, -0.5f), new Vector3(1.4f, 1.3f, 1f), Dark);
                    m.Box(new Vector3(-3.3f, 1.3f, -0.5f), new Vector3(1.4f, 0.2f, 1f), Yellow);
                    m.Box(new Vector3(3.3f, 1.3f, -0.5f), new Vector3(1.4f, 0.2f, 1f), Yellow);
                }
                else
                {
                    // A plinth with yellow trim, and a raised panel with a vent.
                    m.Box(new Vector3(0f, 0f, -0.5f), new Vector3(8f, 1.3f, 1f), Dark);
                    m.Box(new Vector3(0f, 1.3f, -0.5f), new Vector3(8f, 0.2f, 1f), Yellow);
                    m.Box(new Vector3(0f, 2.6f, -0.2f), new Vector3(6f, 6.4f, 0.4f), Panel);
                    m.Box(new Vector3(-1.6f, 4.2f, -0.44f), new Vector3(1.6f, 2.4f, 0.1f), VentDark);
                }
                // The upper panel with a vent block.
                m.Box(new Vector3(0f, 13.4f, -0.2f), new Vector3(6f, 9f, 0.4f), Panel2);
                m.Box(new Vector3(0f, 16f, -0.45f), new Vector3(3.2f, 2.2f, 0.12f), VentDark);
                // A catwalk at 10 m on legs, with a yellow handrail.
                m.Box(new Vector3(0f, 10f, -1.3f), new Vector3(8f, 0.3f, 2.6f), Dark);
                m.Box(new Vector3(0f, 11.15f, -2.55f), new Vector3(8f, 0.1f, 0.1f), Yellow);
                m.Box(new Vector3(0f, 10.7f, -2.55f), new Vector3(8f, 0.07f, 0.07f), Steel);
                m.Box(new Vector3(-2f, 0f, -2.45f), new Vector3(0.22f, 10f, 0.22f), Rib2);
                m.Box(new Vector3(2f, 0f, -2.45f), new Vector3(0.22f, 10f, 0.22f), Rib2);
                m.Box(new Vector3(-2f, 10.3f, -2.55f), new Vector3(0.1f, 0.85f, 0.1f), Steel);
                m.Box(new Vector3(2f, 10.3f, -2.55f), new Vector3(0.1f, 0.85f, 0.1f), Steel);
            }

            // The rib at this piece's left end, with a yellow stripe and lights, leaning in at the top.
            m.Box(new Vector3(-4f, ribFrom, -0.9f), new Vector3(1.6f, 24f - ribFrom, 1.8f), Rib);
            float stripeFrom = Mathf.Max(ribFrom, 1.5f);
            m.Box(new Vector3(-4f, stripeFrom, -1.83f), new Vector3(0.45f, 22.5f - stripeFrom, 0.08f), Yellow);
            if (kind != 2) m.Box(new Vector3(-4f, 5f, -1.9f), new Vector3(0.3f, 1.4f, 0.1f), Lamp);
            Beam(m, new Vector3(-4f, 23.6f, -0.9f), new Vector3(-4f, H + 4f, -5.4f), 1.6f, 1.8f, Rib);
            m.Box(new Vector3(-4f, 20f, -1.9f), new Vector3(0.3f, 0.6f, 0.1f), Lamp);
        }

        /// <summary>A lit door at the foot of a bulkhead piece, and a pool of light in front of it.</summary>
        private static void Door(TerrainCanvas c, TerrainMeshBuilder m, TerrainRandom r, float x, float z, float yaw)
        {
            m.Box(new Vector3(0f, 0f, -0.15f), new Vector3(4.6f, 6.2f, 0.3f), DoorDark);
            m.Box(new Vector3(0f, 0f, -0.32f), new Vector3(0.08f, 6.2f, 0.04f), Dark);
            m.Box(new Vector3(0f, 2.8f, -0.32f), new Vector3(4.4f, 0.12f, 0.04f), Dark);
            m.Box(new Vector3(-2.65f, 0f, -0.35f), new Vector3(0.7f, 7f, 0.7f), Rib);
            m.Box(new Vector3(2.65f, 0f, -0.35f), new Vector3(0.7f, 7f, 0.7f), Rib);
            m.Box(new Vector3(0f, 6.6f, -0.35f), new Vector3(6f, 0.8f, 0.7f), Rib);
            m.Box(new Vector3(-2.25f, 0.2f, -0.72f), new Vector3(0.12f, 6.2f, 0.06f), Lamp);
            m.Box(new Vector3(2.25f, 0.2f, -0.72f), new Vector3(0.12f, 6.2f, 0.06f), Lamp);
            m.Box(new Vector3(0f, 6.42f, -0.72f), new Vector3(4.6f, 0.12f, 0.06f), Lamp);
            m.Box(new Vector3(0f, 7.6f, -0.3f), new Vector3(2.2f, 0.5f, 0.1f), Yellow);
            m.Box(new Vector3(0f, 8.2f, -0.45f), new Vector3(0.4f, 0.3f, 0.3f), Amber);
            c.Glow.Uv = new Vector2(0.7f, r.Range(0f, 6.28f));
            c.Glow.Halo(At(x, z, yaw, new Vector3(0f, 8.35f, -0.7f)), 1.4f, C(255, 170, 64, 120));
            c.Glow.Uv = Vector2.zero;
            Vector3 front = At(x, z, yaw, new Vector3(0f, 0f, -4.5f));
            LightPool(c, front.x, front.z, 6.5f, CoolLight, 0.45f);
        }

        /// <summary>A piece of the great bay door: ribbed dark leaves with hazard stripes at their foot, a lit lintel, a control room window, and the frame post at the door's ends.</summary>
        private static void BayDoor(TerrainCanvas c, TerrainMeshBuilder m, TerrainRandom r, float x, float z, float yaw, float post)
        {
            m.Box(new Vector3(0f, 0f, -0.12f), new Vector3(8f, 15f, 0.24f), DoorDark);
            m.Box(new Vector3(0f, 0f, -0.27f), new Vector3(0.18f, 15f, 0.06f), Dark);
            for (int i = 1; i <= 3; i++)
                m.Box(new Vector3(0f, i * 3.6f, -0.33f), new Vector3(7.6f, 0.35f, 0.2f), Rib2);
            // Diagonal stripes on the leaves' foot.
            const float zf = -0.26f;
            for (float sx = -4f; sx + 1.2f <= 4.01f; sx += 1.6f)
                m.Quad(new Vector3(sx, 0.1f, zf), new Vector3(sx + 0.6f, 1.4f, zf), new Vector3(sx + 1.2f, 1.4f, zf), new Vector3(sx + 0.6f, 0.1f, zf), Yellow);
            // Lintel and the window over it.
            m.Box(new Vector3(0f, 15f, -0.8f), new Vector3(8f, 2.2f, 1.6f), Dark);
            m.Box(new Vector3(0f, 15f, -1.62f), new Vector3(8f, 0.22f, 0.06f), Lamp);
            m.Box(new Vector3(0f, 17.2f, -0.2f), new Vector3(8f, 0.4f, 0.4f), Yellow);
            m.Box(new Vector3(0f, 19.4f, -0.25f), new Vector3(6f, 2f, 0.3f), Window);
            m.Box(new Vector3(0f, 22.6f, -0.2f), new Vector3(6f, 3f, 0.4f), Panel2);
            if (post != 0f)
            {
                float px = post * 4f;
                m.Box(new Vector3(px, 0f, -0.9f), new Vector3(2.2f, 17.2f, 1.8f), Rib);
                m.Box(new Vector3(px - post * 1.16f, 0.4f, -1f), new Vector3(0.14f, 15.8f, 0.14f), Lamp);
                for (int i = 0; i < 6; i++)
                    m.Box(new Vector3(px, 0.4f + i * 0.5f, -0.9f), new Vector3(2.3f, 0.5f, 1.86f), i % 2 == 0 ? Yellow : HazardDark);
                m.Box(new Vector3(px, 17.2f, -0.9f), new Vector3(0.6f, 0.5f, 0.6f), Amber);
                c.Glow.Uv = new Vector2(0.85f, r.Range(0f, 6.28f));
                c.Glow.Halo(At(x, z, yaw, new Vector3(px, 17.6f, -1.2f)), 2.6f, C(255, 160, 50, 170));
                c.Glow.Uv = Vector2.zero;
            }
            Vector3 front = At(x, z, yaw, new Vector3(0f, 0f, -6f));
            LightPool(c, front.x, front.z, 7f, CoolLight, 0.35f);
        }

        /// <summary>The block where a side bulkhead meets the end one, with the roof over the corner beyond.</summary>
        private static void Corner(TerrainCanvas c, float sign, float fromV, float endV)
        {
            TerrainMeshBuilder m = c.Solid;
            float near = Mathf.Min(fromV, endV - 4f), far = endV + 3.2f;
            float midV = (near + far) * 0.5f;
            // Gated like everything solid. The block's own footprint overlaps the bulkhead pieces either side of it (by design), so the reservation is a small circle
            // in its outer half, which those pieces' circles (at x 128 and 124, radius 3.95) never reach.
            if (!c.Stand(sign * 132.6f, c.Z(midV), 0.5f, WallHeight + 4.6f, 0f)) return;
            c.PlaceSolid(sign * 130.6f, c.Z(midV));
            m.Box(Vector3.zero, new Vector3(5.2f, WallHeight + 4.6f, far - near), WallGrey);
            m.Box(new Vector3(0f, 1.3f, 0f), new Vector3(5.3f, 0.2f, far - near + 0.1f), Yellow);
            m.Box(new Vector3(0f, WallHeight - 2.62f, 0f), new Vector3(5.3f, 0.16f, far - near + 0.1f), Lamp);
            // The corner's roofs, out to where the others reach.
            m.Place(Vector3.zero);
            const float y = RoofY;
            float x0 = sign > 0f ? 128f : -128f - RoofReach, x1 = sign > 0f ? 128f + RoofReach : -128f;
            float za = c.Z(endV - 6.2f), zb = c.Z(endV + RoofReach);
            RoofQuad(m, x0, Mathf.Min(za, zb), x1, Mathf.Max(za, zb), y);
            float zc = c.Z(near), zd = c.Z(endV);
            float xs0 = sign > 0f ? SideWallX + 4f : -SideWallX - RoofReach, xs1 = sign > 0f ? SideWallX + RoofReach : -SideWallX - 4f;
            RoofQuad(m, xs0, Mathf.Min(zc, zd), xs1, Mathf.Max(zc, zd), y);
        }

        /// <summary>A flat roof between two corners in world metres, facing up. The solid mesh must be placed at the origin.</summary>
        private static void RoofQuad(TerrainMeshBuilder m, float minX, float minZ, float maxX, float maxZ, float y)
        {
            m.Quad(new Vector3(minX, y, minZ), new Vector3(minX, y, maxZ), new Vector3(maxX, y, maxZ), new Vector3(maxX, y, minZ), Roof);
        }

        // ------------------------------------------------------------------ pillars, the rail and beacons

        /// <summary>Tall pillars in the side trench, banded yellow and dark at the foot and higher up, with lamps facing along the field and beams between them.</summary>
        private static void Pillars(TerrainCanvas c, TerrainRandom r, float endV)
        {
            const float height = 30f;
            float[] vs = { 12f, 36f, 60f, 84f };
            for (int k = -1; k <= 1; k += 2)
            {
                for (int i = 0; i < vs.Length; i++)
                {
                    float v = vs[i], x = k * PillarX, z = c.Z(v);
                    if (!c.Stand(x, z, 2f, height + 1.2f, 0.22f)) continue;
                    c.PlaceSolid(x, z);
                    TerrainMeshBuilder m = c.Solid;
                    m.Box(Vector3.zero, new Vector3(3.4f, 0.7f, 3.4f), Dark);
                    m.Box(new Vector3(0f, 0.7f, 0f), new Vector3(2.4f, height - 0.7f, 2.4f), Rib);
                    m.Box(new Vector3(0f, 0.7f, 0f), new Vector3(2f, height - 0.7f, 2.7f), Rib2);
                    m.Box(new Vector3(0f, 0.7f, 0f), new Vector3(2.7f, height - 0.7f, 2f), Rib2);
                    for (int b = 0; b < 6; b++)
                        m.Box(new Vector3(0f, 1f + b * 0.45f, 0f), new Vector3(2.84f, 0.45f, 2.84f), b % 2 == 0 ? Yellow : HazardDark);
                    for (int b = 0; b < 4; b++)
                        m.Box(new Vector3(0f, 14f + b * 0.45f, 0f), new Vector3(2.84f, 0.45f, 2.84f), b % 2 == 0 ? Yellow : HazardDark);
                    // Lamps on the faces towards and away from the field.
                    m.Box(new Vector3(-1.5f, 11.4f, 0f), new Vector3(0.3f, 0.6f, 1.4f), Lamp);
                    m.Box(new Vector3(1.5f, 11.4f, 0f), new Vector3(0.3f, 0.6f, 1.4f), Lamp);
                    // Capital, and the beams to the next pillar out and, from the first, in to the middle.
                    m.Box(new Vector3(0f, height, 0f), new Vector3(3.4f, 1.2f, 3.4f), Dark);
                    float next = i + 1 < vs.Length ? vs[i + 1] : endV - 1f;
                    float length = next - v;
                    m.Box(new Vector3(0f, height + 0.1f, c.Side * length * 0.5f), new Vector3(1.2f, 1f, length), Rib);
                    if (i == 0) m.Box(new Vector3(0f, height + 0.1f, -c.Side * v * 0.5f), new Vector3(1.2f, 1f, v), Rib);
                    LightPool(c, k * 88f, z, 10f, CoolLight, 0.3f);
                    Stain(c, x + r.Range(-3f, 3f), z + r.Range(-3f, 3f), r.Range(1.5f, 3f), C(48, 46, 44, 80));
                }
            }
        }

        /// <summary>A low steel rail beyond each flank strip: a curb, posts and a yellow handrail, all under 1.5 m.</summary>
        private static void Rails(TerrainCanvas c)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * RailX;
                for (float v = 2f; v <= RailEndV; v += 4f)
                {
                    float z = c.Z(v);
                    if (!c.Stand(x, z, 1.95f, 1.2f, 0.2f)) continue;
                    c.PlaceSolid(x, z);
                    TerrainMeshBuilder m = c.Solid;
                    m.Box(Vector3.zero, new Vector3(0.6f, 0.14f, 4f), Dark);
                    m.Box(new Vector3(0f, 0f, -1.9f), new Vector3(0.16f, 1.1f, 0.16f), Steel);
                    m.Box(new Vector3(0f, 0f, 0.1f), new Vector3(0.16f, 1.1f, 0.16f), Steel);
                    m.Box(new Vector3(0f, 1f, 0f), new Vector3(0.12f, 0.1f, 4f), Yellow);
                    m.Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 0.07f, 4f), Steel);
                }
            }
        }

        /// <summary>Amber warning beacons on posts at the rails' far ends and at the corners of the end trench.</summary>
        private static void Beacons(TerrainCanvas c, TerrainRandom r)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                Beacon(c, r, k * RailX, c.Z(RailEndV + 3.6f), 3.2f);
                Beacon(c, r, k * 106f, c.Z(93.6f), 4f);
            }
        }

        private static bool Beacon(TerrainCanvas c, TerrainRandom r, float x, float z, float height)
        {
            if (!c.Stand(x, z, 0.6f, height + 0.5f, 0.2f)) return false;
            c.PlaceSolid(x, z);
            TerrainMeshBuilder m = c.Solid;
            m.Box(Vector3.zero, new Vector3(0.8f, 0.2f, 0.8f), Dark);
            m.Box(new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, height - 0.2f, 0.2f), Steel);
            for (int i = 0; i < 4; i++)
                m.Box(new Vector3(0f, 0.6f + i * 0.4f, 0f), new Vector3(0.26f, 0.2f, 0.26f), Yellow);
            m.Box(new Vector3(0f, height, 0f), new Vector3(0.42f, 0.4f, 0.42f), Amber);
            c.Glow.Uv = new Vector2(0.85f, r.Range(0f, 6.28f));
            c.Glow.Halo(new Vector3(x, height + 0.2f, z), 2.2f, C(255, 160, 50, 180));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        // ------------------------------------------------------------------ the yards

        /// <summary>A gantry crane over each side yard, on two A-frame legs, its trolley holding a crate.</summary>
        private static void Gantries(TerrainCanvas c, TerrainRandom r, float endV)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                if (!r.Chance(0.8f)) continue;
                float v = r.Range(18f, endV - 30f);
                float z = c.Z(v);
                float inner = k * 106f, outer = k * 122f;
                if (!c.IsClear(inner, z, 0.9f, 15.5f) || !c.IsClear(outer, z, 0.9f, 15.5f)) continue;
                if (!StandLong(c, inner, z, 90f, 2.9f, 0.6f, 15.5f, 0.3f)) continue;
                if (!StandLong(c, outer, z, 90f, 2.9f, 0.6f, 15.5f, 0.3f)) continue;
                float trolley = r.Range(-5f, 5f);
                // Keep the deck under the hanging crate clear.
                c.Stand(k * 114f + trolley, z, 1.8f, 7f, 0f);
                c.PlaceSolid(k * 114f, z);
                TerrainMeshBuilder m = c.Solid;
                Color32 paint = Vary(r, Yellow, 0.06f);
                for (int s = -1; s <= 1; s += 2)
                {
                    float lx = s * 8f;
                    m.Box(new Vector3(lx, 0f, 0f), new Vector3(0.9f, 0.5f, 6.4f), Dark);
                    Beam(m, new Vector3(lx, 0.5f, -2.6f), new Vector3(lx, 14.5f, -0.4f), 0.6f, 0.6f, paint);
                    Beam(m, new Vector3(lx, 0.5f, 2.6f), new Vector3(lx, 14.5f, 0.4f), 0.6f, 0.6f, paint);
                    m.Box(new Vector3(lx, 7f, 0f), new Vector3(0.4f, 0.4f, 2.6f), paint);
                    m.Box(new Vector3(lx, 0.5f, 2.62f), new Vector3(0.62f, 2f, 0.62f), HazardDark);
                }
                m.Box(new Vector3(0f, 14.5f, 0f), new Vector3(17.8f, 1.3f, 1.7f), paint);
                m.Box(new Vector3(0f, 15.8f, 0f), new Vector3(17.2f, 0.2f, 0.5f), Dark);
                m.Box(new Vector3(trolley, 13.4f, 0f), new Vector3(2.2f, 1.1f, 2.2f), Dark);
                m.Box(new Vector3(trolley, 7.8f, 0f), new Vector3(0.08f, 5.6f, 0.08f), Dark);
                m.Box(new Vector3(trolley, 7f, 0f), new Vector3(0.6f, 0.8f, 0.4f), paint);
                m.Box(new Vector3(trolley, 4.6f, 0f), new Vector3(2.4f, 2.2f, 2.4f), Vary(r, S(86, 96, 66), 0.06f));
                m.Box(new Vector3(trolley, 5.5f, 0f), new Vector3(2.44f, 0.18f, 2.44f), Yellow);
                for (int s = -1; s <= 1; s += 2)
                {
                    m.Box(new Vector3(s * 8.4f, 15.8f, 0f), new Vector3(0.4f, 0.35f, 0.4f), Amber);
                    c.Glow.Uv = new Vector2(0.85f, r.Range(0f, 6.28f));
                    c.Glow.Halo(new Vector3(k * 114f + s * 8.4f, 16.2f, z), 1.8f, C(255, 160, 50, 160));
                }
                c.Glow.Uv = Vector2.zero;
                // Its rails on the deck.
                c.Decals.Uv = new Vector2(0f, 0.4f);
                c.Decals.FlatRect(D, inner, z, 0.5f, 26f, 0f, C(66, 69, 74, 200));
                c.Decals.FlatRect(D, outer, z, 0.5f, 26f, 0f, C(66, 69, 74, 200));
                c.Decals.FlatRect(D, inner + 0.3f, z, 0.1f, 26f, 0f, C(160, 164, 170, 150));
                c.Decals.FlatRect(D, outer + 0.3f, z, 0.1f, 26f, 0f, C(160, 164, 170, 150));
                c.Decals.Uv = Vector2.zero;
            }
        }

        /// <summary>The end yard: a dropship in its cradle, a row of containers and machinery before the bulkhead, and a few low stores in front.</summary>
        private static void EndYard(TerrainCanvas c, TerrainRandom r, float endV)
        {
            float yaw = c.Side > 0 ? 0f : 180f;
            float dropV = c.Side > 0 ? 96f : 100.4f;
            float dropX = r.Sign() * r.Range(30f, 52f);
            if (r.Chance(c.Side > 0 ? 0.92f : 0.65f)) Dropship(c, r, dropX, c.Z(dropV), yaw);

            // The back row along the bulkhead.
            float backV = endV - 8.4f;
            float x = -102f + r.Range(0f, 6f);
            while (x < 100f)
            {
                int kind = BackRowKind(r);
                float half = HalfLength(kind);
                float at = x + half;
                PlaceItem(c, r, kind, at, c.Z(backV + r.Range(-0.8f, 0.8f)), r.Chance(0.5f) ? 0f : 180f, 3);
                x = at + half + r.Range(2f, 6f);
            }
            // A few low stores in front, leaving the space just behind the trench open.
            x = -96f + r.Range(0f, 8f);
            float frontV = c.Side > 0 ? 95.4f : 97f;
            while (x < 96f)
            {
                int kind = 5 + r.Int(3);
                float half = HalfLength(kind);
                float at = x + half;
                if (r.Chance(0.55f)) PlaceItem(c, r, kind, at, c.Z(frontV + r.Range(-0.5f, 0.8f)), r.Chance(0.5f) ? 0f : 180f, 1);
                x = at + half + r.Range(6f, 16f);
            }
        }

        /// <summary>The side yards: an outer lane of big things by the bulkhead and an inner lane of smaller ones by the trench.</summary>
        private static void SideYards(TerrainCanvas c, TerrainRandom r, float endV)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                // Outer lane: containers, tanks, compressors, generators.
                float v = r.Range(1f, 5f);
                while (v < endV - 4f)
                {
                    int kind = OuterKind(r);
                    float half = HalfLength(kind), width = HalfWidth(kind);
                    float at = v + half;
                    float x = k * (124f - 4.2f - width - r.Range(0f, 2.5f));
                    PlaceItem(c, r, kind, x, c.Z(at), k * 90f + (r.Chance(0.5f) ? 0f : 180f), 3);
                    v = at + half + r.Range(1.5f, 5f);
                }
                // Inner lane: crates, racks, tow tractors, generators, short containers.
                v = r.Range(1f, 6f);
                while (v < endV - 6f)
                {
                    int kind = InnerKind(r);
                    float half = HalfLength(kind), width = HalfWidth(kind);
                    float at = v + half;
                    float x = k * (105.6f + width + r.Range(0f, 2f));
                    if (r.Chance(0.85f)) PlaceItem(c, r, kind, x, c.Z(at), k * 90f + (r.Chance(0.5f) ? 0f : 180f), 2);
                    v = at + half + r.Range(2.5f, 7f);
                }
                // Lamps over the yard, and grime.
                for (float lv = 10f; lv < endV - 6f; lv += 24f)
                    LightPool(c, k * 114f, c.Z(lv + r.Range(-4f, 4f)), 11f, PoolLight, 0.28f);
                for (int i = 0; i < 6; i++)
                    Stain(c, k * r.Range(106f, 124f), c.Z(r.Range(4f, endV - 6f)), r.Range(1.2f, 3.4f), C(46, 44, 42, 90));
            }
        }

        // What stands in the yards: 0 long containers, 1 short containers, 2 generator, 3 compressor, 4 tanks, 5 crates on pallets, 6 weapon rack, 7 tow tractor.
        private static int BackRowKind(TerrainRandom r)
        {
            float roll = r.Value();
            if (roll < 0.38f) return 0;
            if (roll < 0.52f) return 1;
            if (roll < 0.66f) return 2;
            if (roll < 0.8f) return 3;
            return 4;
        }

        private static int OuterKind(TerrainRandom r)
        {
            float roll = r.Value();
            if (roll < 0.42f) return 0;
            if (roll < 0.56f) return 1;
            if (roll < 0.7f) return 4;
            if (roll < 0.84f) return 3;
            return 2;
        }

        private static int InnerKind(TerrainRandom r)
        {
            float roll = r.Value();
            if (roll < 0.32f) return 5;
            if (roll < 0.52f) return 6;
            if (roll < 0.68f) return 7;
            if (roll < 0.84f) return 2;
            return 1;
        }

        private static float HalfLength(int kind)
        {
            switch (kind)
            {
                case 0: return 6.2f;
                case 1: return 3.15f;
                case 2: return 3.3f;
                case 3: return 3.1f;
                case 4: return 5.4f;
                case 5: return 1.5f;
                case 6: return 2.4f;
                default: return 2.6f;
            }
        }

        private static float HalfWidth(int kind)
        {
            switch (kind)
            {
                case 0:
                case 1: return 2.6f;
                case 2: return 1.7f;
                case 3: return 1.4f;
                case 4: return 2.4f;
                case 5: return 1.5f;
                case 6: return 0.7f;
                default: return 1.2f;
            }
        }

        private static bool PlaceItem(TerrainCanvas c, TerrainRandom r, int kind, float x, float z, float yaw, int maxHigh)
        {
            switch (kind)
            {
                case 0: return Containers(c, r, x, z, yaw, 1 + r.Int(maxHigh), 1 + r.Int(2), 12.2f);
                case 1: return Containers(c, r, x, z, yaw, 1 + r.Int(Mathf.Min(2, maxHigh)), 1 + r.Int(2), 6.1f);
                case 2: return Generator(c, r, x, z, yaw);
                case 3: return Compressor(c, r, x, z, yaw);
                case 4: return Tanks(c, r, x, z, yaw);
                case 5: return Crates(c, r, x, z, yaw);
                case 6: return Rack(c, r, x, z, yaw);
                default: return Tow(c, r, x, z, yaw);
            }
        }

        // ------------------------------------------------------------------ props

        /// <summary>A stack of cargo containers in muted paints with yellow trim, long side along local x. Fewer high where the floor's view needs it.</summary>
        private static bool Containers(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, int high, int wide, float length)
        {
            const float tier = 2.62f;
            float halfWidth = wide * 1.3f;
            // South of the floor and in line with it, only as high as the camera's view allows.
            float room = Room(c, x, Mathf.Abs(z), halfWidth * 1.15f + 0.15f);
            high = Mathf.Min(high, Mathf.FloorToInt(room / tier));
            if (high < 1) return false;
            if (!StandLong(c, x, z, yaw, length * 0.5f + 0.15f, halfWidth, high * tier, 0.5f)) return false;
            c.PlaceSolid(x, z, yaw);
            for (int w = 0; w < wide; w++)
            {
                float offset = (w - (wide - 1) * 0.5f) * 2.6f;
                int stack = w == 0 ? high : Mathf.Max(1, high - r.Int(2));
                for (int h = 0; h < stack; h++)
                {
                    float slip = h == 0 ? 0f : r.Range(-0.25f, 0.25f);
                    Container(c.Solid, r, new Vector3(slip, h * tier, offset), length);
                }
            }
            return true;
        }

        private static readonly Color32[] ContainerPaints = { S(86, 96, 66), S(108, 112, 116), S(150, 92, 50), S(70, 82, 96), S(132, 122, 94), S(96, 104, 80) };

        private static void Container(TerrainMeshBuilder m, TerrainRandom r, Vector3 foot, float length)
        {
            Color32 paint = Vary(r, ContainerPaints[r.Int(ContainerPaints.Length)], 0.07f);
            const float h = 2.6f, w = 2.44f;
            m.Box(foot, new Vector3(length, h, w), paint);
            // Corrugation, cheaply: a darker inset band on the long sides.
            m.Box(foot + new Vector3(0f, 0.2f, 0f), new Vector3(length - 0.5f, h - 0.4f, w + 0.06f), Shade(paint, 0.74f));
            // Doors at one end, and yellow trim along the top edge or at the corners.
            m.Box(foot + new Vector3(length * 0.5f, 0.15f, 0f), new Vector3(0.06f, h - 0.3f, w - 0.25f), Shade(paint, 0.62f));
            if (r.Chance(0.6f)) m.Box(foot + new Vector3(0f, h - 0.16f, 0f), new Vector3(length + 0.04f, 0.16f, w + 0.04f), Yellow);
            else
            {
                for (int i = -1; i <= 1; i += 2)
                    m.Box(foot + new Vector3(i * (length * 0.5f - 0.15f), 0f, 0f), new Vector3(0.32f, h + 0.02f, w + 0.04f), Yellow);
            }
        }

        /// <summary>A generator on a skid: louvred housing, radiator, exhaust and a small amber status light.</summary>
        private static bool Generator(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (Room(c, x, Mathf.Abs(z), 2.1f) < 4.8f) return false;
            if (!StandLong(c, x, z, yaw, 3.3f, 1.7f, 4.8f, 0.45f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            Color32 paint = Vary(r, r.Chance(0.6f) ? S(88, 98, 72) : S(112, 116, 120), 0.06f);
            m.Box(Vector3.zero, new Vector3(6.4f, 0.3f, 3.2f), Dark);
            m.Box(new Vector3(0f, 0.3f, 0f), new Vector3(6f, 2.5f, 2.9f), paint);
            m.Box(new Vector3(0f, 2.8f, 0f), new Vector3(6.1f, 0.14f, 3f), Yellow);
            for (int s = -1; s <= 1; s += 2)
                m.Box(new Vector3(1f, 0.9f, s * 1.47f), new Vector3(2.6f, 1.3f, 0.06f), VentDark);
            m.Box(new Vector3(3.05f, 0.5f, 0f), new Vector3(0.14f, 2.1f, 2.5f), Dark);
            m.Cylinder(new Vector3(-1.9f, 2.94f, 0.7f), 0.24f, 0.2f, 1.7f, 6, Dark);
            m.Box(new Vector3(-2.3f, 1.2f, -1.47f), new Vector3(0.9f, 0.7f, 0.06f), Dark);
            m.Box(new Vector3(-2.3f, 1.65f, -1.5f), new Vector3(0.14f, 0.14f, 0.04f), Amber);
            m.Tube(new[] { new Vector3(-3.1f, 0.8f, 0.4f), new Vector3(-3.6f, 0.4f, 0.4f), new Vector3(-3.6f, 0.08f, 1.2f) }, new[] { 0.08f, 0.08f, 0.08f }, 6, Tyre, Tyre);
            return true;
        }

        /// <summary>An air compressor: a horizontal tank on saddles with a motor on top.</summary>
        private static bool Compressor(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (Room(c, x, Mathf.Abs(z), 1.8f) < 3.4f) return false;
            if (!StandLong(c, x, z, yaw, 3.1f, 1.4f, 3.4f, 0.4f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            Color32 paint = Vary(r, r.Chance(0.55f) ? S(150, 94, 52) : S(90, 100, 70), 0.06f);
            m.Box(Vector3.zero, new Vector3(6f, 0.3f, 2.6f), Dark);
            m.Box(new Vector3(-1.6f, 0.3f, 0f), new Vector3(0.5f, 0.9f, 1.8f), Rib);
            m.Box(new Vector3(1.6f, 0.3f, 0f), new Vector3(0.5f, 0.9f, 1.8f), Rib);
            m.Tube(new[] { new Vector3(-2.4f, 1.55f, 0f), new Vector3(2.4f, 1.55f, 0f) }, new[] { 1.05f, 1.05f }, 8, paint, paint);
            m.Lump(new Vector3(-2.4f, 1.55f, 0f), new Vector3(0.45f, 1.05f, 1.05f), paint, 8);
            m.Lump(new Vector3(2.4f, 1.55f, 0f), new Vector3(0.45f, 1.05f, 1.05f), paint, 8);
            m.Tube(new[] { new Vector3(-0.2f, 1.55f, 0f), new Vector3(0.25f, 1.55f, 0f) }, new[] { 1.09f, 1.09f }, 8, Yellow, Yellow);
            m.Box(new Vector3(-0.8f, 2.45f, 0f), new Vector3(2.2f, 0.85f, 1.2f), Dark);
            m.Cylinder(new Vector3(0.9f, 2.45f, 0f), 0.42f, 0.42f, 0.5f, 8, Steel);
            m.Box(new Vector3(-0.8f, 2.9f, -0.62f), new Vector3(0.12f, 0.12f, 0.04f), Amber);
            return true;
        }

        /// <summary>Two or three upright tanks in a row with yellow bands and ladders, joined by a header pipe that elbows down to the deck.</summary>
        private static bool Tanks(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (Room(c, x, Mathf.Abs(z), 5.4f) < 9.6f) return false;
            if (!StandLong(c, x, z, yaw, 5.4f, 2.2f, 9.6f, 0.45f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            int count = 2 + r.Int(2);
            float spacing = count == 3 ? 3.5f : 4.2f;
            Color32 paint = Vary(r, r.Chance(0.5f) ? S(136, 140, 144) : S(98, 108, 84), 0.05f);
            const float radius = 1.5f;
            float span = (count - 1) * spacing * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float lx = -span + i * spacing;
                float h = r.Range(6f, 8.2f);
                m.Box(new Vector3(lx, 0f, 0f), new Vector3(3.4f, 0.4f, 3.4f), Dark);
                m.Cylinder(new Vector3(lx, 0.4f, 0f), radius, radius, h, 10, paint, false);
                m.Lump(new Vector3(lx, 0.4f + h, 0f), new Vector3(radius, 0.55f, radius), paint, 10);
                m.Cylinder(new Vector3(lx, 1.6f, 0f), radius + 0.04f, radius + 0.04f, 0.35f, 10, Yellow, false);
                m.Cylinder(new Vector3(lx, h - 0.8f, 0f), radius + 0.04f, radius + 0.04f, 0.3f, 10, Yellow, false);
                m.Box(new Vector3(lx, 0.4f, -radius - 0.08f), new Vector3(0.5f, h, 0.06f), Steel);
                m.Tube(new[] { new Vector3(lx, 4.2f, 1.9f), new Vector3(lx, 4.2f, radius * 0.9f) }, new[] { 0.18f, 0.18f }, 6, Steel, Steel);
            }
            float end = span + 1.4f;
            m.Tube(new[] { new Vector3(-end, 4.2f, 1.9f), new Vector3(end, 4.2f, 1.9f), new Vector3(end + 0.9f, 3.6f, 1.9f), new Vector3(end + 0.9f, 0.1f, 1.9f) },
                new[] { 0.22f, 0.22f, 0.22f, 0.22f }, 6, Steel, Steel);
            m.Box(new Vector3(end + 0.9f, 1.2f, 1.9f), new Vector3(0.6f, 0.6f, 0.6f), Yellow);
            return true;
        }

        private static readonly Color32[] DrumPaints = { S(140, 80, 46), S(120, 64, 54), S(90, 100, 70), S(70, 84, 100) };

        /// <summary>Two by two pallets of ammunition crates, fuel drums or flat cases.</summary>
        private static bool Crates(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (Room(c, x, Mathf.Abs(z), 2.1f) < 1.5f) return false;
            if (!c.Stand(x, z, 2.1f, 1.5f, 0.3f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (r.Chance(0.15f)) continue;
                    float px = (i - 0.5f) * 1.45f, pz = (j - 0.5f) * 1.45f;
                    m.Box(new Vector3(px, 0f, pz), new Vector3(1.3f, 0.14f, 1.3f), Pallet);
                    int kind = r.Int(3);
                    if (kind == 0)
                    {
                        int high = 1 + r.Int(2);
                        Color32 olive = Vary(r, S(86, 96, 66), 0.08f);
                        for (int h = 0; h < high; h++)
                        {
                            float y = 0.14f + h * 0.62f;
                            m.Box(new Vector3(px, y, pz), new Vector3(1.2f, 0.6f, 1.1f), olive);
                            m.Box(new Vector3(px, y + 0.24f, pz), new Vector3(1.22f, 0.1f, 1.12f), Yellow);
                        }
                    }
                    else if (kind == 1)
                    {
                        Color32 drum = Vary(r, DrumPaints[r.Int(DrumPaints.Length)], 0.06f);
                        for (int a = -1; a <= 1; a += 2)
                        {
                            for (int b = -1; b <= 1; b += 2)
                                m.Cylinder(new Vector3(px + a * 0.31f, 0.14f, pz + b * 0.31f), 0.28f, 0.28f, 0.88f, 6, drum);
                        }
                    }
                    else
                    {
                        m.Box(new Vector3(px, 0.14f, pz), new Vector3(1.25f, 0.45f, 1f), S(70, 74, 78));
                        m.Box(new Vector3(px, 0.59f, pz), new Vector3(1.1f, 0.4f, 0.9f), Vary(r, S(64, 72, 58), 0.08f));
                    }
                }
            }
            return true;
        }

        /// <summary>A weapon rack: a frame with abstract long shapes leaning in it, beside a locker with a yellow stripe.</summary>
        private static bool Rack(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (Room(c, x, Mathf.Abs(z), 1.2f) < 2.3f) return false;
            if (!StandLong(c, x, z, yaw, 2.4f, 0.7f, 2.3f, 0.3f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            m.Box(new Vector3(-0.6f, 0f, 0f), new Vector3(3f, 0.18f, 0.8f), Dark);
            m.Box(new Vector3(-2f, 0.18f, 0.25f), new Vector3(0.12f, 1.5f, 0.12f), Steel);
            m.Box(new Vector3(0.8f, 0.18f, 0.25f), new Vector3(0.12f, 1.5f, 0.12f), Steel);
            m.Box(new Vector3(-0.6f, 1.5f, 0.25f), new Vector3(3f, 0.1f, 0.1f), Steel);
            int count = 4 + r.Int(4);
            for (int i = 0; i < count; i++)
            {
                float lx = -1.85f + i * 0.38f;
                Beam(m, new Vector3(lx, 0.2f, -0.2f), new Vector3(lx + 0.04f, 1.6f, 0.18f), 0.09f, 0.16f, Gun);
            }
            m.Box(new Vector3(1.75f, 0f, 0f), new Vector3(1.1f, 2.1f, 0.7f), Vary(r, Panel, 0.05f));
            m.Box(new Vector3(1.75f, 1.7f, -0.36f), new Vector3(1.12f, 0.12f, 0.04f), Yellow);
            m.Box(new Vector3(1.75f, 0.1f, -0.37f), new Vector3(0.04f, 1.5f, 0.03f), Dark);
            return true;
        }

        /// <summary>A tow tractor: a low yellow body, a cab, four wheels and an amber beacon on its roof.</summary>
        private static bool Tow(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (Room(c, x, Mathf.Abs(z), 1.5f) < 2.9f) return false;
            if (!StandLong(c, x, z, yaw, 2.6f, 1.2f, 2.9f, 0.35f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            Color32 body = Vary(r, S(178, 140, 46), 0.06f);
            m.Box(new Vector3(0f, 0.42f, 0f), new Vector3(4.6f, 0.7f, 2f), body);
            m.Box(new Vector3(-1.2f, 1.12f, 0f), new Vector3(2f, 0.5f, 1.9f), body, 0.9f);
            m.Box(new Vector3(1.2f, 1.12f, 0f), new Vector3(1.4f, 1.3f, 1.8f), Dark, 0.88f);
            m.Box(new Vector3(1.2f, 2.42f, 0f), new Vector3(1.6f, 0.12f, 1.95f), body);
            m.Box(new Vector3(1.86f, 1.5f, 0f), new Vector3(0.06f, 0.7f, 1.4f), Window);
            m.Box(new Vector3(-2.5f, 0.45f, 0f), new Vector3(0.6f, 0.15f, 0.3f), Dark);
            m.Box(new Vector3(0f, 0.42f, -1.01f), new Vector3(4.2f, 0.16f, 0.04f), HazardDark);
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                    m.Tube(new[] { new Vector3(i * 1.5f, 0.48f, j * 0.82f), new Vector3(i * 1.5f, 0.48f, j * 1.12f) }, new[] { 0.48f, 0.48f }, 8, Tyre, Tyre, true);
            }
            m.Box(new Vector3(1.2f, 2.54f, 0f), new Vector3(0.3f, 0.22f, 0.3f), Amber);
            c.Glow.Uv = new Vector2(0.85f, r.Range(0f, 6.28f));
            c.Glow.Halo(At(x, z, yaw, new Vector3(1.2f, 2.7f, 0f)), 1.2f, C(255, 160, 50, 150));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A generic boxy dropship hanging in a yellow cradle: no real design, just a hull, wings, engine pods and a tail.</summary>
        private static bool Dropship(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (Room(c, x, Mathf.Abs(z), 8.4f) < 12.6f) return false;
            if (!c.Stand(x, z, 8.4f, 12.6f, 0.45f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            // The cradle.
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    m.Box(new Vector3(i * 6.2f, 0f, j * 4.6f), new Vector3(1.2f, 0.4f, 1.2f), Dark);
                    m.Box(new Vector3(i * 6.2f, 0.4f, j * 4.6f), new Vector3(0.7f, 11.2f, 0.7f), Rib);
                    m.Box(new Vector3(i * 6.2f, 1.2f, j * 4.6f), new Vector3(0.76f, 1.6f, 0.76f), Yellow);
                    Beam(m, new Vector3(i * 3.4f, 11.6f, j * 4.6f), new Vector3(i * 3f, 8.1f, j * 1.3f), 0.35f, 0.35f, Dark);
                }
                m.Box(new Vector3(i * 6.2f, 11.6f, 0f), new Vector3(0.9f, 0.9f, 10.1f), Yellow);
            }
            for (int j = -1; j <= 1; j += 2)
                m.Box(new Vector3(0f, 11.6f, j * 4.6f), new Vector3(13.4f, 0.9f, 0.9f), Yellow);

            // The craft.
            Color32 hull = Vary(r, Hull, 0.05f), hullDark = Shade(hull, 0.78f);
            m.Box(new Vector3(0f, 5f, 0f), new Vector3(7f, 0.4f, 2.2f), Dark);
            m.Box(new Vector3(0f, 5.4f, 0f), new Vector3(10f, 2.8f, 3.2f), hull, 0.86f);
            m.Box(new Vector3(6.2f, 5.6f, 0f), new Vector3(2.4f, 2.2f, 2.8f), hull, 0.62f);
            m.Box(new Vector3(5.6f, 6.9f, 0f), new Vector3(1.2f, 0.45f, 2.3f), Window);
            m.Box(new Vector3(-6.3f, 6.2f, 0f), new Vector3(2.8f, 1.6f, 1.8f), hull, 0.8f);
            m.Box(new Vector3(-7.6f, 7.8f, 0f), new Vector3(1.4f, 2.4f, 0.3f), hullDark, 0.7f);
            m.Box(new Vector3(-7.4f, 7f, 0f), new Vector3(1.2f, 0.2f, 4.4f), hullDark);
            m.Box(new Vector3(-0.6f, 6.6f, 0f), new Vector3(3.6f, 0.35f, 10.2f), hullDark);
            for (int j = -1; j <= 1; j += 2)
            {
                m.Box(new Vector3(0.6f, 5.7f, j * 1.62f), new Vector3(4.6f, 1.7f, 0.06f), hullDark);
                m.Tube(new[] { new Vector3(-2.8f, 6.3f, j * 5.5f), new Vector3(1.8f, 6.3f, j * 5.5f) }, new[] { 0.85f, 1f }, 8, hullDark, hull, true);
                m.Lump(new Vector3(-2.85f, 6.3f, j * 5.5f), new Vector3(0.3f, 0.8f, 0.8f), Dark, 8);
                m.Box(new Vector3(1.6f, 6.1f, j * 5.5f), new Vector3(0.5f, 0.4f, 2.05f), Yellow);
            }
            // Beacons on top of the cradle.
            for (int i = -1; i <= 1; i += 2)
            {
                m.Box(new Vector3(i * 6.2f, 12.05f, 4.6f), new Vector3(0.4f, 0.35f, 0.4f), Amber);
                c.Glow.Uv = new Vector2(0.85f, r.Range(0f, 6.28f));
                c.Glow.Halo(At(x, z, yaw, new Vector3(i * 6.2f, 12.6f, 4.6f)), 2f, C(255, 160, 50, 160));
            }
            c.Glow.Uv = Vector2.zero;
            Stain(c, x + r.Range(-2f, 2f), z + r.Range(-2f, 2f), 4f, C(48, 46, 44, 90));
            LightPool(c, x, z, 11f, PoolLight, 0.35f);
            return true;
        }

        // ------------------------------------------------------------------ no man's land

        /// <summary>Across the middle: a keel seam with bolt rows along the line, scorches, blast marks, dents, scattered bolts and debris, all within |z| &lt; 20.</summary>
        public override void NoMansLand(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth;
            // The keel seam.
            c.Decals.Uv = new Vector2(0f, 0.35f);
            c.Decals.FlatRect(D, 0f, 0f, W * 2f - 0.4f, 1.9f, 0f, C(94, 97, 102, 130));
            c.Decals.FlatRect(D, 0f, -0.95f, W * 2f - 0.4f, 0.16f, 0f, C(64, 67, 72, 200));
            c.Decals.FlatRect(D, 0f, 0.95f, W * 2f - 0.4f, 0.16f, 0f, C(64, 67, 72, 200));
            c.Decals.FlatRect(D, 0f, 1.09f, W * 2f - 0.4f, 0.06f, 0f, C(160, 164, 170, 70));
            Color32 bolt = C(146, 150, 156, 170);
            for (float x = -W + 1f; x < W - 0.5f; x += 2f)
            {
                c.Decals.FlatRect(D, x, -0.55f, 0.16f, 0.16f, 0f, bolt);
                c.Decals.FlatRect(D, x + 1f, 0.55f, 0.16f, 0.16f, 0f, bolt);
            }

            // Scorches.
            int scorches = 4 + r.Int(3);
            for (int i = 0; i < scorches; i++)
            {
                float x = r.Range(-W + 4f, W - 4f), z = r.Range(-16f, 16f), radius = r.Range(1.6f, 4f);
                Scorch(c, r, x, z, radius);
                c.Darken(x, z, radius * 2f, r.Range(0.04f, 0.08f));
            }
            // Blast marks: a scorch, a pitted heart with a bright scraped rim, and dark streaks thrown out.
            int blasts = 2 + r.Int(2);
            var ray = new List<Vector2>(2);
            for (int i = 0; i < blasts; i++)
            {
                float x = r.Range(-W + 6f, W - 6f), z = r.Range(-14f, 14f), radius = r.Range(1.2f, 2.4f);
                Scorch(c, r, x, z, radius * 1.3f);
                c.Decals.Uv = new Vector2(0f, 0.5f);
                int rays = 5 + r.Int(4);
                float spin = r.Range(0f, 6.28f);
                for (int k = 0; k < rays; k++)
                {
                    float a = spin + (k + r.Range(-0.4f, 0.4f)) / rays * Mathf.PI * 2f;
                    float reach = radius * r.Range(1.5f, 2.8f);
                    float bend = r.Range(-0.25f, 0.25f);
                    ray.Clear();
                    ray.Add(new Vector2(x + Mathf.Cos(a) * radius * 0.5f, z + Mathf.Sin(a) * radius * 0.5f));
                    ray.Add(new Vector2(x + Mathf.Cos(a + bend * 0.5f) * (radius + reach) * 0.5f, z + Mathf.Sin(a + bend * 0.5f) * (radius + reach) * 0.5f));
                    ray.Add(new Vector2(x + Mathf.Cos(a + bend) * reach, z + Mathf.Sin(a + bend) * reach));
                    c.Decals.FlatRibbon(D, ray, radius * 0.7f, radius * 0.15f, C(40, 36, 34, 70), true, C(40, 36, 34, 0));
                }
                c.Decals.FlatDisc(D, x, z, radius * 0.5f, radius * 0.45f, C(60, 58, 58, 110), C(60, 58, 58, 80), 10);
                c.Decals.FlatRing(D, x, z, radius * 0.5f, radius * 0.85f, C(170, 172, 176, 110), 16, 0f, 360f, C(170, 172, 176, 0));
                c.Decals.Uv = Vector2.zero;
            }
            // Dents, each with a bright edge where the light catches it.
            int dents = 7 + r.Int(5);
            for (int i = 0; i < dents; i++)
            {
                float x = r.Range(-W + 3f, W - 3f), z = r.Range(-18f, 18f), radius = r.Range(0.6f, 1.3f);
                c.Decals.Uv = new Vector2(0f, 0.3f);
                c.Decals.FlatDisc(D, x, z, radius, radius * 0.85f, C(84, 87, 92, 120), C(84, 87, 92, 0), 10);
                c.Decals.FlatRing(D, x, z, radius * 0.7f, radius * 0.95f, C(176, 180, 186, 90), 8, 200f, 340f, C(176, 180, 186, 0));
                c.Decals.Uv = Vector2.zero;
            }
            // Scattered bolts and debris.
            Color32[] bits = { C(70, 72, 76, 210), C(150, 154, 160, 200), C(186, 150, 52, 190), C(110, 78, 56, 190) };
            c.Decals.Uv = new Vector2(0f, 0.3f);
            int pieces = 60 + r.Int(30);
            for (int i = 0; i < pieces; i++)
            {
                float x = r.Range(-W + 2f, W - 2f), z = r.Range(-19f, 19f) * Mathf.Sqrt(r.Value());
                float size = r.Range(0.1f, 0.6f);
                c.Decals.FlatRect(D, x, z, size, size * r.Range(0.25f, 1f), r.Range(0f, 180f), bits[r.Int(bits.Length)]);
            }
            for (int i = 0; i < 50; i++)
                c.Decals.FlatRect(D, r.Range(-W + 2f, W - 2f), r.Range(-18f, 18f), 0.12f, 0.12f, r.Range(0f, 90f), C(122, 124, 128, 220));
            c.Decals.Uv = Vector2.zero;
        }

        private static void Scorch(TerrainCanvas c, TerrainRandom r, float x, float z, float radius)
        {
            float yaw = r.Range(0f, 180f);
            c.Decals.Uv = new Vector2(0f, 0.6f);
            c.Decals.FlatDisc(D, x, z, radius * 1.6f, radius * 1.3f, C(36, 32, 30, 70), C(36, 32, 30, 0), 12, yaw);
            c.Decals.FlatDisc(D, x, z, radius * 0.8f, radius * 0.7f, C(40, 37, 35, 95), C(40, 37, 35, 25), 10, yaw);
            c.Decals.Uv = Vector2.zero;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The tallest a thing of this radius may be at x, v metres out on this side: south of the floor and in line with it, it must stand back by TerrainCanvas.SouthLean per metre of height
        /// (as TerrainCanvas.Stand checks); anywhere else there is no limit.
        /// </summary>
        private static float Room(TerrainCanvas c, float x, float v, float radius)
        {
            if (c.Side > 0 || Mathf.Abs(x) >= c.HalfWidth + radius + TerrainCanvas.ArenaMargin) return 999f;
            return (v - c.HalfLength - radius - 0.3f) / TerrainCanvas.SouthLean - 0.2f;
        }

        /// <summary>
        /// Reserves a long footprint as a row of circles along local x turned by yaw, so that long things can stand close beside others. False, reserving nothing, if any circle is taken.
        /// </summary>
        private static bool StandLong(TerrainCanvas c, float x, float z, float yaw, float halfLength, float halfWidth, float height, float shadow)
        {
            float radius = halfWidth * 1.15f + 0.15f;
            if (halfLength <= radius * 2f) return c.Stand(x, z, Mathf.Max(radius, halfLength), height, shadow);
            float span = halfLength - radius;
            int count = 1 + Mathf.FloorToInt(span / (radius + 0.01f));
            float dx = Mathf.Cos(yaw * Mathf.Deg2Rad), dz = -Mathf.Sin(yaw * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                float t = -span + 2f * span * i / (count - 1);
                if (!c.IsClear(x + dx * t, z + dz * t, radius, height)) return false;
            }
            for (int i = 0; i < count; i++)
            {
                float t = -span + 2f * span * i / (count - 1);
                c.Stand(x + dx * t, z + dz * t, radius, height, shadow);
            }
            return true;
        }

        /// <summary>A solid colour: alpha is how much it glows.</summary>
        private static Color32 S(int r, int g, int b, int glow = 0) => new Color32((byte)r, (byte)g, (byte)b, (byte)glow);

        private static Color32 Shade(Color32 color, float f) =>
            new Color32((byte)Mathf.Clamp(color.r * f, 0f, 255f), (byte)Mathf.Clamp(color.g * f, 0f, 255f), (byte)Mathf.Clamp(color.b * f, 0f, 255f), color.a);

        private static Color32 Vary(TerrainRandom r, Color32 color, float amount) => Shade(color, 1f + r.Range(-amount, amount));

        /// <summary>The world point of a local point of a thing standing at x, z turned by yaw (as TerrainMeshBuilder.Place turns it), on flat ground.</summary>
        private static Vector3 At(float x, float z, float yaw, Vector3 local)
        {
            float cs = Mathf.Cos(yaw * Mathf.Deg2Rad), sn = Mathf.Sin(yaw * Mathf.Deg2Rad);
            return new Vector3(x + local.x * cs + local.z * sn, local.y, z - local.x * sn + local.z * cs);
        }

        /// <summary>A square beam between two local points, width across it sideways and thick across it in the upright plane: braces, chamfered ribs, leaning legs.</summary>
        private static void Beam(TerrainMeshBuilder m, Vector3 a, Vector3 b, float width, float thick, Color32 color)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            if (length < 0.001f) return;
            Vector3 dir = along / length;
            Vector3 side = Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.Cross(Vector3.up, dir);
            side = (side - dir * Vector3.Dot(side, dir)).normalized;
            Vector3 up = Vector3.Cross(dir, side);
            Vector3 sx = side * (width * 0.5f), sy = up * (thick * 0.5f);
            // Laid out as TerrainMeshBuilder.Box lays out its corners, with side as x, up as y and the beam's length as z: b bottom, t top.
            Vector3 b0 = a - sx - sy, b1 = a + sx - sy, b2 = b + sx - sy, b3 = b - sx - sy;
            Vector3 t0 = a - sx + sy, t1 = a + sx + sy, t2 = b + sx + sy, t3 = b - sx + sy;
            m.Quad(t0, t3, t2, t1, color);
            m.Quad(b0, t0, t1, b1, color);
            m.Quad(b1, t1, t2, b2, color);
            m.Quad(b2, t2, t3, b3, color);
            m.Quad(b3, t3, t0, b0, color);
            m.Quad(b0, b1, b2, b3, color);
        }
    }
}
