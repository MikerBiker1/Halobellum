// TerrainInteriorCovenant.cs
// The Covenant warship look (D80): a great hall deep inside an alien ship. A polished blue-violet deck of long lens-shaped panels and pointed arcs (InteriorCovenantHull),
// darker inlaid rings (InteriorCovenantInlay) and a pale plated walkway right round the field (InteriorCovenantPlate). On the floor: two nested chevrons pointing at the
// middle line, a faint ring in each half's back third, lens panels down the sides, all cut as seams with thin cyan and violet light in a few of them.
// Around it: the hull's leaning walls, with ribs rising from them and curving in over the sides like a corridor's frames, bulging pillars with violet slits, light orbs
// floating over pedestals, hanging fins, grav lifts of faint light, a stepped curved balcony and a great elliptical arch at each far end, low consoles and a glowing rail
// along the flyers' strips. Deep indigo air, a cool lavender light. All shapes and textures are original and abstract: no emblems, glyphs or markings.
// Looks only. Depends on: TerrainLook, TerrainLayout, TerrainTheme, TerrainCanvas, TerrainMeshBuilder, TerrainNoise, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class CovenantInterior
    {
        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        public static TerrainLook Create() => new TerrainLook
        {
            Id = "CovenantWarship",
            Name = "Covenant Warship",
            Theme = new TerrainTheme
            {
                Faction = BattleFactions.Covenant,
                Layers = new[] { "InteriorCovenantHull", "InteriorCovenantInlay", "InteriorCovenantPlate" },
                // The deck's tile is 26 m: lens panels 13 m long, their chains 13 m apart. Scales of the inlay 1.5 m, plates of the walkway 1.75 m.
                Tiles = new Vector3(1f / 26f, 1f / 12f, 1f / 14f),
                GroundTint = Color.white,
                Hills = TerrainHillStyle.Flat,
                HillHeight = 0f,
                FarHeight = 0f,
                // The ceiling, seen only in low views: deep indigo.
                SkyTop = Rgb(14, 9, 30),
                SkyHorizon = Rgb(42, 28, 76),
                Cloud = Color.black,
                CloudAmount = 0f,
                // A cool lavender-white key light from behind the camera's usual view, so units are lit on the side one sees.
                SunColor = Rgb(236, 228, 255),
                SunIntensity = 1.1f,
                SunAngles = new Vector2(50f, -35f),
                AmbientSky = Rgb(120, 122, 180),
                AmbientEquator = Rgb(102, 96, 146),
                AmbientGround = Rgb(56, 44, 76),
                FogColor = Rgb(30, 20, 56),
                FogStart = 135f,
                FogEnd = 520f,
                FogAmount = 1f,
                Haze = Rgb(80, 78, 150),
                HazeAmount = 0.05f,
                MoteColor = Rgb(120, 230, 255),
                MoteCount = 60,
                // A polished deck: the hull and the inlay shine, the walkway plates less, with a cool blue-white sheen. The highlight's light is in the north, so its
                // glint lies on the far half of the usual view rather than under the middle of the fight; broad enough (w 28) to be seen on screen.
                Gloss = new Vector4(0.3f, 0.4f, 0.3f, 40f),
                GlossColor = new Color(0.6f, 0.7f, 0.95f, 0.05f),
                GlossLightAngles = new Vector2(40f, 180f),
                Vignette = 0.3f,
                VignetteColor = Rgb(18, 8, 34),
                ColorFilter = new Color(0.97f, 0.955f, 1f),
                Contrast = 8f,
                Saturation = 6f,
                Exposure = 0f,
            },
            Layout = new CovenantWarshipLayout(),
        };
    }

    public sealed class CovenantWarshipLayout : TerrainLayout
    {
        // Paint on the deck.
        private static readonly Color32 Groove = new Color32(34, 26, 58, 255);
        private static readonly Color32 Lip = new Color32(170, 160, 214, 255);
        // A faint tint of darker blue metal for inlaid bands, laid as a decal: painting a layer in at part weight lets the ground shader break it into gravel.
        private static readonly Color32 InlayTint = new Color32(40, 46, 96, 255);
        // Light.
        private static readonly Color32 Cyan = new Color32(96, 226, 255, 255);
        private static readonly Color32 Violet = new Color32(184, 104, 255, 255);
        private static readonly Color32 Magenta = new Color32(226, 92, 236, 255);
        private static readonly Color32 SlitCore = new Color32(110, 50, 190, 255);
        // Metal: alpha 0, as it doesn't glow.
        private static readonly Color32 MetalMid = new Color32(74, 66, 110, 0);
        private static readonly Color32 MetalLight = new Color32(108, 100, 150, 0);
        private static readonly Color32 MetalDark = new Color32(46, 40, 72, 0);
        private static readonly Color32 Pale = new Color32(150, 142, 184, 0);
        private static readonly Color32 Banner = new Color32(84, 52, 126, 0);
        private static readonly Color32 BannerLow = new Color32(58, 38, 92, 0);

        private static readonly List<Vector2> Line = new List<Vector2>();
        private static readonly List<Vector2> Line2 = new List<Vector2>();
        private static readonly List<Vector2> Profile = new List<Vector2>();
        private static readonly List<Vector2> Profile2 = new List<Vector2>();
        private static readonly List<float> Widths = new List<float>();
        private static readonly List<float> Widths2 = new List<float>();
        private static readonly List<Vector3> BandInner = new List<Vector3>();
        private static readonly List<Vector3> BandOuter = new List<Vector3>();
        private static readonly List<Vector3> BandNormal = new List<Vector3>();
        private static readonly List<Vector3> Path = new List<Vector3>();
        private static readonly List<float> Radii = new List<float>();

        public override string Name => "Covenant Warship";

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            second = 0f;
            third = 0f;
            if (outside <= 0f) return;
            // A pale plated walkway right round the floor: the flyers' strips lie on it.
            third = TerrainNoise.Smooth(1.6f, 2.6f, outside) * (1f - TerrainNoise.Smooth(11.4f, 12.4f, outside));
            // Beyond it a band of darker inlaid metal, then the hull's deck again under the walls.
            second = TerrainNoise.Smooth(13.5f, 15f, outside) * (1f - TerrainNoise.Smooth(25f, 27f, outside)) * 0.85f;
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            FloorPanels(c, r);
            EdgeLights(c, r);
            FarEnd(c, r);
            Sides(c, r);
            Sparks(c, r);
        }

        // ------------------------------------------------------------------ the deck

        private static void FloorPanels(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            float back = E - 3f;
            float phase = r.Range(0f, 6.28f);

            // Two nested chevrons, pointing at the middle line, the band between them tinted a darker blue.
            float tipOuter = r.Range(24f, 29f), tipInner = tipOuter + r.Range(7f, 9f);
            float spreadOuter = r.Range(48f, 54f), spreadInner = spreadOuter - r.Range(11f, 13f);
            float tipMid = (tipOuter + tipInner) * 0.5f, spreadMid = (spreadOuter + spreadInner) * 0.5f;
            c.Decals.Uv = new Vector2(0f, 0.3f);
            for (int k = -1; k <= 1; k += 2)
            {
                Chevron(c, Line, k, tipMid, spreadMid, back);
                c.Decals.FlatRibbon(D, Line, 7f, 7f, A(InlayTint, 55), true);
            }
            c.Decals.Uv = Vector2.zero;
            for (int k = -1; k <= 1; k += 2)
            {
                Chevron(c, Line, k, tipOuter, spreadOuter, back);
                FloorSeam(c, Line, Cyan, 140, phase + k);
                Chevron(c, Line, k, tipInner, spreadInner, back);
                FloorSeam(c, Line, Violet, 100, phase + 2f + k);
            }

            // A threshold seam across the back, and a border seam down each side.
            Straight(Line, -W + 1f, c.Z(back), W - 1f, c.Z(back), 12);
            FloorSeam(c, Line, Cyan, 0, 0f);
            for (int k = -1; k <= 1; k += 2)
            {
                Straight(Line, k * (W - 3f), c.Z(2f), k * (W - 3f), c.Z(back), 12);
                FloorSeam(c, Line, Cyan, 0, 0f);
            }

            // A faint ring in the back third, inside the chevrons.
            RingMotif(c, r, r.Range(-4f, 4f), c.Z(r.Range(58f, 63f)), r.Range(11f, 12.5f));

            // Long lens panels down the sides, a node at each end, a faint violet line inside.
            for (int k = -1; k <= 1; k += 2)
            {
                float lx = k * r.Range(62f, 66f);
                float from = r.Range(12f, 18f), to = r.Range(66f, 72f), bulge = r.Range(4.8f, 6f);
                float lensPhase = r.Range(0f, 6.28f);
                for (int s = -1; s <= 1; s += 2)
                {
                    Lens(Line, lx, c.Z(from), lx, c.Z(to), bulge * s, 24);
                    FloorSeam(c, Line, Cyan, 0, 0f);
                    Lens(Line, lx, c.Z(from), lx, c.Z(to), bulge * s * 0.62f, 24);
                    FloorGlowLine(c, Line, Violet, 70, lensPhase + s);
                }
                Node(c, r, lx, c.Z(from));
                Node(c, r, lx, c.Z(to));
            }
        }

        /// <summary>One arm of a chevron, from the back of the side to its tip on the middle of the field.</summary>
        private static void Chevron(TerrainCanvas c, List<Vector2> points, int k, float tip, float spread, float back)
        {
            Bezier(points, new Vector2(k * spread, c.Z(back)), new Vector2(k * spread * 0.82f, c.Z(tip + (back - tip) * 0.3f)), new Vector2(0f, c.Z(tip)), 16);
        }

        private static void RingMotif(TerrainCanvas c, TerrainRandom r, float x, float z, float radius)
        {
            // Its floor tinted a darker blue, its middle only faintly lighter (a full disc of the pale plate reads as a target from far out). Decals, not paint,
            // so that it stays smooth metal: a layer painted in at part weight turns to gravel.
            c.Decals.Uv = new Vector2(0f, 0.3f);
            c.Decals.FlatDisc(D, x, z, radius, radius, A(InlayTint, 55), A(InlayTint, 55), 64);
            c.Decals.FlatDisc(D, x, z, radius * 0.38f, radius * 0.38f, A(Lip, 34), A(Lip, 26), 32);
            c.Decals.Uv = Vector2.zero;
            RingSeam(c, x, z, radius, 64);
            RingSeam(c, x, z, radius - 1.4f, 64);
            RingSeam(c, x, z, radius * 0.38f, 32);
            // Lighter tiles set in a broken ring between them.
            int pieces = 10 + 2 * r.Int(3);
            float each = 360f / pieces, start = r.Range(0f, each);
            c.Decals.Uv = new Vector2(0f, 0.3f);
            for (int p = 0; p < pieces; p++)
            {
                float a = start + p * each;
                c.Decals.FlatRing(D, x, z, radius * 0.56f, radius * 0.7f, new Color32(150, 140, 196, 55), 4, a + 2f, a + each - 2f);
            }
            c.Decals.Uv = Vector2.zero;
            float phase = r.Range(0f, 6.28f);
            c.Glow.Uv = new Vector2(0.35f, phase);
            GlowRing(c, x, z, radius - 0.7f, Violet, 110, 64);
            c.Glow.Uv = new Vector2(0.35f, phase + 2f);
            GlowRing(c, x, z, radius * 0.38f + 0.45f, Cyan, 90, 32);
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>A seam cut in the deck: a soft shadow either side, a dark cut, a lit lip on one side, and maybe a thin line of light in it.</summary>
        private static void FloorSeam(TerrainCanvas c, List<Vector2> points, Color32 glow, int glowAlpha, float phase)
        {
            if (points.Count < 2) return;
            c.Decals.Uv = new Vector2(0f, 0.25f);
            c.Decals.FlatRibbon(D, points, 0.9f, 0.9f, A(Groove, 55), true);
            c.Decals.FlatRibbon(D, points, 0.16f, 0.16f, A(Groove, 165));
            Offset(points, 0.2f, Line2);
            c.Decals.FlatRibbon(D, Line2, 0.07f, 0.07f, A(Lip, 80));
            c.Decals.Uv = Vector2.zero;
            if (glowAlpha > 0) FloorGlowLine(c, points, glow, glowAlpha, phase);
        }

        /// <summary>A thin line of light on the deck with a faint spill, fading at its ends, pulsing slowly.</summary>
        private static void FloorGlowLine(TerrainCanvas c, List<Vector2> points, Color32 color, int alpha, float phase)
        {
            if (points.Count < 2) return;
            c.Glow.Uv = new Vector2(0.35f, phase);
            c.Glow.FlatRibbon(G, points, 1.1f, 1.1f, A(color, alpha / 9), true, null, 1.5f);
            c.Glow.FlatRibbon(G, points, 0.08f, 0.08f, A(color, alpha), false, null, 1.5f);
            c.Glow.Uv = Vector2.zero;
        }

        private static void RingSeam(TerrainCanvas c, float x, float z, float radius, int segments)
        {
            c.Decals.Uv = new Vector2(0f, 0.25f);
            c.Decals.FlatRing(D, x, z, radius - 0.45f, radius, A(Groove, 0), segments, 0f, 360f, A(Groove, 55));
            c.Decals.FlatRing(D, x, z, radius, radius + 0.45f, A(Groove, 55), segments, 0f, 360f, A(Groove, 0));
            c.Decals.FlatRing(D, x, z, radius - 0.08f, radius + 0.08f, A(Groove, 165), segments);
            c.Decals.FlatRing(D, x, z, radius + 0.16f, radius + 0.23f, A(Lip, 80), segments);
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>A thin ring of light with a faint spill either side. The caller sets the glow's uv.</summary>
        private static void GlowRing(TerrainCanvas c, float x, float z, float radius, Color32 color, int alpha, int segments)
        {
            c.Glow.FlatRing(G, x, z, radius - 1f, radius - 0.06f, A(color, 0), segments, 0f, 360f, A(color, alpha / 8));
            c.Glow.FlatRing(G, x, z, radius - 0.06f, radius + 0.06f, A(color, alpha), segments);
            c.Glow.FlatRing(G, x, z, radius + 0.06f, radius + 1f, A(color, alpha / 8), segments, 0f, 360f, A(color, 0));
        }

        /// <summary>A small round fitting where seams meet.</summary>
        private static void Node(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            c.Decals.Uv = new Vector2(0f, 0.25f);
            c.Decals.FlatDisc(D, x, z, 1.1f, 1.1f, A(Groove, 120), A(Groove, 0), 12);
            c.Decals.FlatRing(D, x, z, 0.62f, 0.72f, A(Lip, 90), 16);
            c.Decals.Uv = Vector2.zero;
            c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
            c.Glow.FlatDisc(G, x, z, 0.32f, 0.32f, A(Cyan, 130), A(Cyan, 0), 8);
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>Strips of light just off the floor's edge and at the walkway's outer edge, with studs of light that chase along the sides.</summary>
        private static void EdgeLights(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            float phase = r.Range(0f, 6.28f);
            for (int k = -1; k <= 1; k += 2)
            {
                Straight(Line, k * (W + 1f), c.Z(0f), k * (W + 1f), c.Z(E + 1f), 2);
                LightStrip(c, Line, 0.12f, Cyan, 0.25f, phase);
                Straight(Line, k * (W + 12.6f), c.Z(0f), k * (W + 12.6f), c.Z(E + 12.6f), 2);
                LightStrip(c, Line, 0.14f, Violet, 0.3f, phase + 1.5f);
                for (int i = 0; i < 21; i++)
                {
                    float v = 2f + i * 4f;
                    c.Glow.Uv = new Vector2(0.85f, -v * 0.3f);
                    c.Glow.FlatDisc(G, k * (W + 1.8f), c.Z(v), 0.22f, 0.22f, A(Cyan, 200), A(Cyan, 0), 8);
                }
            }
            c.Glow.Uv = Vector2.zero;
            Straight(Line, -(W + 1f), c.Z(E + 1f), W + 1f, c.Z(E + 1f), 2);
            LightStrip(c, Line, 0.12f, Cyan, 0.25f, phase);
            Straight(Line, -(W + 12.6f), c.Z(E + 12.6f), W + 12.6f, c.Z(E + 12.6f), 2);
            LightStrip(c, Line, 0.14f, Violet, 0.3f, phase + 1.5f);
        }

        // ------------------------------------------------------------------ across the middle

        public override void NoMansLand(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth;
            // The deck a little dulled along the line.
            for (float x = -W; x <= W; x += 12f)
                c.Darken(x + r.Range(-4f, 4f), r.Range(-2.5f, 2.5f), r.Range(6f, 10f), r.Range(0.06f, 0.14f));

            // Panels cracked by the fighting.
            for (int i = 0; i < 9; i++)
            {
                TerrainCanvas.Wander(Line, r, new Vector2(r.Range(-W + 6f, W - 6f), r.Range(-14f, 14f)), r.Range(0f, 6.28f), r.Range(1.2f, 2f), 6 + r.Int(8), 0.7f);
                c.KeepOnFloor(Line, 2f);
                Crack(c, Line);
            }

            // Broken energy lines: a dark seam with only pieces of its light left, flickering.
            for (int i = 0; i < 8; i++)
            {
                float z = r.Range(-16f, 16f);
                float x0 = r.Range(-W + 6f, W - 20f);
                float length = r.Range(6f, 16f);
                Color32 color = r.Chance(0.5f) ? Cyan : Violet;
                Straight(Line, x0, z, x0 + length, z, 6);
                FloorSeam(c, Line, color, 0, 0f);
                float x = x0;
                while (x < x0 + length - 0.5f)
                {
                    float dash = Mathf.Min(r.Range(0.8f, 2.6f), x0 + length - x);
                    Line.Clear();
                    Line.Add(new Vector2(x, z));
                    Line.Add(new Vector2(x + dash, z));
                    c.Glow.Uv = new Vector2(0.9f, r.Range(0f, 6.28f));
                    c.Glow.FlatRibbon(G, Line, 0.6f, 0.6f, A(color, 14), true);
                    c.Glow.FlatRibbon(G, Line, 0.08f, 0.08f, A(color, 150), false, null, 0.3f);
                    x += dash + r.Range(0.4f, 1.8f);
                }
            }
            c.Glow.Uv = Vector2.zero;

            // Plasma scorches with violet rims, most near the line.
            for (int i = 0; i < 11; i++)
            {
                float x = r.Range(-W + 4f, W - 4f);
                float z = r.Range(-18f, 18f) * (0.35f + 0.65f * r.Value());
                Scorch(c, r, x, z, r.Range(1.2f, 3.4f));
            }
        }

        private static void Crack(TerrainCanvas c, List<Vector2> points)
        {
            if (points.Count < 2) return;
            c.Decals.Uv = new Vector2(0f, 0.3f);
            c.Decals.FlatRibbon(D, points, 0.6f, 0.4f, new Color32(40, 30, 60, 45), true, null, 1f);
            c.Decals.FlatRibbon(D, points, 0.13f, 0.08f, new Color32(30, 22, 46, 150), false, null, 1f);
            Offset(points, 0.14f, Line2);
            c.Decals.FlatRibbon(D, Line2, 0.05f, 0.04f, A(Lip, 50), false, null, 1f);
            c.Decals.Uv = Vector2.zero;
        }

        private static void Scorch(TerrainCanvas c, TerrainRandom r, float x, float z, float radius)
        {
            var soot = new Color32(42, 26, 60, 80);
            // A dull heat-tinted rim, a little darker than the deck and barely glowing, so that a scorch never reads as a unit's marker or a spell's ring.
            var rim = new Color32(86, 68, 116, 255);
            c.Decals.Uv = new Vector2(0f, 0.6f);
            c.Decals.FlatDisc(D, x, z, radius * 2f, radius * 1.7f, soot, A(soot, 0), 16, r.Range(0f, 180f));
            c.Decals.FlatRing(D, x, z, radius * 0.75f, radius * 1.05f, A(rim, 0), 18, 0f, 360f, A(rim, 60));
            c.Decals.FlatRing(D, x, z, radius * 1.05f, radius * 1.45f, A(rim, 60), 18, 0f, 360f, A(rim, 0));
            c.Decals.FlatDisc(D, x, z, radius * 0.75f, radius * 0.68f, new Color32(34, 24, 48, 130), new Color32(40, 28, 56, 70), 12, r.Range(0f, 180f));
            c.Decals.Uv = Vector2.zero;
            c.Glow.Uv = new Vector2(0.8f, r.Range(0f, 6.28f));
            c.Glow.FlatRing(G, x, z, radius * 0.85f, radius * 1.1f, A(Violet, 0), 18, 0f, 360f, A(Violet, 9));
            c.Glow.FlatRing(G, x, z, radius * 1.1f, radius * 1.5f, A(Violet, 9), 18, 0f, 360f, A(Violet, 0));
            c.Glow.Uv = Vector2.zero;
        }

        // ------------------------------------------------------------------ around the field

        private static void Sides(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth, E = c.EdgeV;
            int liftAt = r.Int(2);
            for (int k = -1; k <= 1; k += 2)
            {
                float yaw = k > 0 ? 270f : 90f; // Local +z towards the field.
                // Local +x of a side wall runs along world z: towards this half's far end on one side of the field, towards the middle on the other.
                float outward = k > 0 ? c.Side : -c.Side;

                // The hull's wall, leaning in as it rises, a rib at each join rising from it and curving in over the side.
                for (int i = 0; i < 8; i++)
                {
                    float v = 10f + i * 20f;
                    float ribX = i < 7 ? 10f * outward : float.NaN;
                    Wall(c, r, k * 144f, c.Z(v), yaw, 20f, r.Range(26f, 32f), ribX, 34f, r.Range(40f, 46f));
                }
                if (c.Side < 0)
                {
                    // The rib on the middle line, once for both halves. It stands between the two halves' walls, whose places cover it.
                    c.PlaceSolid(k * 144f, 0f, yaw);
                    Vector3 tip = Rib(c.Solid, 0f, 34f, r.Range(40f, 46f));
                    TipHalo(c, r, k * 144f, 0f, yaw, tip);
                }
                // Floor lines of light running out from the field to each rib's foot.
                for (int i = 0; i <= 6; i++)
                {
                    float v = i * 20f;
                    if (i == 0 && c.Side > 0) continue;
                    Straight(Line, k * (W + 13.2f), c.Z(v), k * 142.5f, c.Z(v), 3);
                    LightStrip(c, Line, 0.1f, Cyan, 0.4f, v * 0.21f);
                }

                // Lifts first: they take the most room.
                Lift(c, r, k * 116f, c.Z(liftAt == 0 ? 40f : 60f));
                for (int i = 0; i < 5; i++) Pillar(c, r, k * 106f, c.Z(10f + i * 20f), yaw);
                for (int i = 0; i < 4; i++) OrbPedestal(c, r, k * 99f, c.Z(20f + i * 20f), i % 2 == 0 ? Violet : Cyan);
                for (int i = 0; i < 6; i++)
                {
                    if (r.Chance(0.8f)) Fin(c, r, k * 125f, c.Z(10f + i * 20f), yaw);
                }
                for (int i = 0; i < 4; i++) Lectern(c, r, k * r.Range(108f, 122f), c.Z(r.Range(6f, 110f)), yaw + r.Range(-20f, 20f));
                for (int i = 0; i < 8; i++)
                {
                    float v = 10f + i * 20f;
                    LightPool(c, k * 138f, c.Z(v), 10f, Violet, 0.35f, 0.2f);
                }
                Rail(c, k * 94f);
            }
        }

        private static void FarEnd(TerrainCanvas c, TerrainRandom r)
        {
            float E = c.EdgeV;
            float yaw = c.Side > 0 ? 180f : 0f; // Facing the field.
            Balcony(c, r, 0f, c.Z(E + 26f), yaw);
            GreatArch(c, r, c.Z(E + 46f), yaw);
            // The far wall. Its local +x runs west in the north and east in the south, so each segment's rib goes on the end that puts them on the joins.
            float ribEnd = c.Side > 0 ? -12.5f : 12.5f;
            for (int i = 0; i < 11; i++)
            {
                float x = -125f + i * 25f;
                Wall(c, r, x, c.Z(E + 68f), yaw, 25f, r.Range(28f, 34f), i < 10 ? ribEnd : float.NaN, 14f, r.Range(34f, 38f));
            }
            for (int k = -1; k <= 1; k += 2)
            {
                for (int i = 0; i < 3; i++)
                    Lectern(c, r, k * (30f + i * 17f + r.Range(-2f, 2f)), c.Z(E + 8f + r.Range(0f, 4f)), yaw + k * r.Range(-15f, 15f));
                Pillar(c, r, k * r.Range(46f, 56f), c.Z(E + 20f), yaw);
                Pillar(c, r, k * r.Range(84f, 92f), c.Z(E + 24f), yaw);
                OrbPedestal(c, r, k * r.Range(66f, 74f), c.Z(E + 14f), Violet);
                for (int i = 0; i < 2; i++)
                {
                    if (r.Chance(0.75f)) Fin(c, r, k * (40f + i * 44f + r.Range(-4f, 4f)), c.Z(E + 56f), yaw);
                }
            }
        }

        /// <summary>A stretch of the hull's wall, leaning in as it rises, with a cornice, a band of light and a tall lens of violet light; and a rib at local x ribX unless it is NaN.</summary>
        private static bool Wall(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float width, float height, float ribX, float ribInward, float ribHeight)
        {
            const float lean = 6f;
            if (!StandRow(c, x, z, yaw, width * 0.5f, lean * 0.5f, 3.3f, height + 1.5f, 0.2f)) return false;
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            const int rows = 5, cols = 4;
            float half = width * 0.5f;
            for (int j = 0; j < rows; j++)
            {
                float y0 = height * j / rows, y1 = height * (j + 1) / rows;
                float z0 = WallLean(y0, height, lean), z1 = WallLean(y1, height, lean);
                Color32 tone = Color32.Lerp(MetalDark, MetalLight, (j + 0.5f) / rows);
                for (int i = 0; i < cols; i++)
                {
                    float x0 = -half + width * i / cols, x1 = -half + width * (i + 1) / cols;
                    Face(m, new Vector3(x0, y0, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y0, z0), Vector3.forward, Vary(r, tone, 0.05f));
                }
            }
            m.Box(new Vector3(0f, 0f, 1f), new Vector3(width, 1.3f, 2f), MetalDark);
            m.Box(new Vector3(0f, height - 0.2f, lean), new Vector3(width + 0.3f, 1.6f, 3.2f), MetalMid, 0.9f);

            // A band of light a third of the way up.
            float by = height * 0.3f, bz = WallLean(by, height, lean) + 0.15f;
            Face(m, new Vector3(-half, by, bz), new Vector3(-half, by + 0.32f, bz), new Vector3(half, by + 0.32f, bz), new Vector3(half, by, bz), Vector3.forward, A(Violet, 210));

            // A tall lens of soft violet light with a bright core. Never quite closed at its ends, so that no quad is degenerate.
            float l0 = height * 0.42f, l1 = height * 0.84f;
            const int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps, t1 = (i + 1f) / steps;
                float ya = Mathf.Lerp(l0, l1, t0), yb = Mathf.Lerp(l0, l1, t1);
                float wa = Mathf.Max(0.12f, 1.5f * Mathf.Sin(t0 * Mathf.PI)), wb = Mathf.Max(0.12f, 1.5f * Mathf.Sin(t1 * Mathf.PI));
                float za = WallLean(ya, height, lean), zb = WallLean(yb, height, lean);
                Face(m, new Vector3(-wa, ya, za + 0.2f), new Vector3(-wb, yb, zb + 0.2f), new Vector3(wb, yb, zb + 0.2f), new Vector3(wa, ya, za + 0.2f), Vector3.forward, A(SlitCore, 170));
                Face(m, new Vector3(-wa * 0.25f, ya, za + 0.26f), new Vector3(-wb * 0.25f, yb, zb + 0.26f), new Vector3(wb * 0.25f, yb, zb + 0.26f), new Vector3(wa * 0.25f, ya, za + 0.26f),
                    Vector3.forward, A(Violet, 255));
            }

            if (!float.IsNaN(ribX))
            {
                Vector3 tip = Rib(m, ribX, ribInward, ribHeight);
                TipHalo(c, r, x, z, yaw, tip);
            }
            return true;
        }

        private static float WallLean(float y, float height, float lean)
        {
            float t = y / height;
            return lean * t * t;
        }

        /// <summary>A rib of the hull, in the current placement: it rises from the wall's foot at local x and curves in towards the field (local +z), a strip of light along its inner edge. Returns where its tip hangs.</summary>
        private static Vector3 Rib(TerrainMeshBuilder m, float localX, float inward, float height)
        {
            Profile.Clear();
            Widths.Clear();
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                float a = t * Mathf.PI * 0.5f;
                Profile.Add(new Vector2(0.9f + inward * (1f - Mathf.Cos(a)), height * Mathf.Sin(a)));
                Widths.Add(Mathf.Lerp(3.4f, 1.5f, t));
            }
            var origin = new Vector3(localX, 0f, 0f);
            Band(m, origin, Vector3.forward, Profile, Widths, 1.7f, MetalMid, MetalLight, MetalDark);
            // The strip of light: along the inner edge of the upper part, standing just proud of both broad faces.
            ShiftProfile(Profile, Widths, 0.45f, 3, Profile2, Widths2, 0.3f);
            Band(m, origin, Vector3.forward, Profile2, Widths2, 1.78f, A(Violet, 200), A(Violet, 200), A(Violet, 200));
            var tip = new Vector3(localX, height - 1.6f, 0.9f + inward);
            m.Lump(tip, new Vector3(0.8f, 1.5f, 0.8f), A(Violet, 230), 8);
            return tip;
        }

        private static void TipHalo(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, Vector3 local)
        {
            c.Glow.Uv = new Vector2(0.4f, r.Range(0f, 6.28f));
            c.Glow.Halo(LocalToWorld(c, x, z, yaw, local), 3.2f, A(Violet, 70));
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>A pillar of dark metal that bulges and tapers, with violet slits towards the field and a lighter crown.</summary>
        private static bool Pillar(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            float h = r.Range(15f, 19f);
            if (!c.Stand(x, z, 3.2f, h + 1f, 0.35f)) return false;
            c.PlaceSolid(x, z, yaw);
            c.Solid.Cylinder(Vector3.zero, 3.1f, 2.8f, 0.7f, 8, MetalDark);
            Path.Clear();
            Radii.Clear();
            float swell = r.Range(0.92f, 1.08f);
            Path.Add(new Vector3(0f, 0.7f, 0f)); Radii.Add(2f);
            Path.Add(new Vector3(0f, 2f, 0f)); Radii.Add(1.7f);
            Path.Add(new Vector3(0f, 0.3f * h, 0f)); Radii.Add(2.5f * swell);
            Path.Add(new Vector3(0f, 0.5f * h, 0f)); Radii.Add(2.7f * swell);
            Path.Add(new Vector3(0f, 0.68f * h, 0f)); Radii.Add(2.1f * swell);
            Path.Add(new Vector3(0f, 0.84f * h, 0f)); Radii.Add(1.25f);
            Path.Add(new Vector3(0f, 0.94f * h, 0f)); Radii.Add(1f);
            Path.Add(new Vector3(0f, h, 0f)); Radii.Add(1.7f);
            // Dark metal, not pale: lighter tones read as stone vases from the game's camera.
            c.Solid.Tube(Path, Radii, 8, MetalDark, MetalMid, true);
            c.Solid.Lump(new Vector3(0f, h + 0.3f, 0f), new Vector3(2f, 0.55f, 2f), MetalLight, 8);
            for (int i = -1; i <= 1; i++)
            {
                float a = Mathf.PI * 0.5f + i * 0.62f;
                float rs = 2.5f * swell;
                c.Solid.Lump(new Vector3(Mathf.Cos(a) * rs, 0.49f * h, Mathf.Sin(a) * rs), new Vector3(0.2f, 0.13f * h, 0.2f), A(Violet, 255), 6);
            }
            c.Glow.Uv = new Vector2(0.3f, r.Range(0f, 6.28f));
            c.Glow.Halo(LocalToWorld(c, x, z, yaw, new Vector3(0f, 0.49f * h, 3.2f)), 3.4f, A(Violet, 26));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A slim pedestal with three prongs and an orb of light floating over them.</summary>
        private static bool OrbPedestal(TerrainCanvas c, TerrainRandom r, float x, float z, Color32 glow)
        {
            if (!c.Stand(x, z, 1.4f, 5.6f, 0.3f)) return false;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            c.Solid.Cylinder(Vector3.zero, 1.1f, 0.55f, 2.5f, 8, MetalMid);
            c.Solid.Lump(new Vector3(0f, 2.6f, 0f), new Vector3(1f, 0.28f, 1f), Pale, 8);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.094f;
                var foot = new Vector3(Mathf.Cos(a) * 0.72f, 2.7f, Mathf.Sin(a) * 0.72f);
                var end = foot + new Vector3(Mathf.Cos(a) * 0.25f, 1.1f, Mathf.Sin(a) * 0.25f);
                c.Solid.Tube(new[] { foot, end }, new[] { 0.12f, 0.04f }, 6, MetalLight, Pale);
            }
            const float orbY = 4.7f;
            c.Solid.Uv = new Vector2(0.12f, r.Range(0f, 6.28f));
            c.Solid.Lump(new Vector3(0f, orbY, 0f), Vector3.one * 0.62f, A(glow, 255), 8);
            c.Solid.Uv = Vector2.zero;
            c.Glow.Uv = new Vector2(0.4f, r.Range(0f, 6.28f));
            c.Glow.Halo(new Vector3(x, c.GroundAt(x, z) + orbY, z), 2.6f, A(glow, 80));
            c.Glow.Uv = Vector2.zero;
            LightPool(c, x, z, 4.5f, glow, 0.45f, 0.3f);
            return true;
        }

        /// <summary>A fin hung like a banner from a bar high on the hall, pointed at its foot, a stripe of light down its face.</summary>
        private static bool Fin(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            float top = r.Range(30f, 36f), bottom = top - r.Range(17f, 22f);
            if (!c.Stand(x, z, 2.2f, top + 1f, 0.15f)) return false;
            c.PlaceSolid(x, z, yaw);
            c.Solid.Box(new Vector3(0f, top, 0f), new Vector3(4.4f, 0.5f, 0.8f), MetalDark);
            Profile.Clear();
            Widths.Clear();
            Profile2.Clear();
            Widths2.Clear();
            const int n = 9;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                float y = top - t * (top - bottom);
                Profile.Add(new Vector2(0f, y));
                Widths.Add(Mathf.Lerp(3.2f, 3.8f, Mathf.Sin(t * Mathf.PI)) * (1f - t * t) + 0.15f);
                Profile2.Add(new Vector2(0f, y));
                Widths2.Add(0.24f * (1f - t * 0.8f));
            }
            Band(c.Solid, Vector3.zero, Vector3.right, Profile, Widths, 0.35f, Banner, BannerLow, Banner);
            Band(c.Solid, new Vector3(0f, 0f, 0.2f), Vector3.right, Profile2, Widths2, 0.06f, A(Cyan, 220), A(Cyan, 220), A(Cyan, 220));
            return true;
        }

        /// <summary>A grav lift: a round pad ringed with light under a tall faint column of light, rings rising in it.</summary>
        private static bool Lift(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            if (!c.Stand(x, z, 4.6f, 2f, 0.2f)) return false;
            c.PlaceSolid(x, z);
            c.Solid.Cylinder(Vector3.zero, 4.4f, 4f, 0.5f, 10, MetalDark);
            c.Solid.Cylinder(new Vector3(0f, 0.5f, 0f), 3.4f, 3.2f, 0.25f, 10, Pale);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.047f;
                c.Solid.Lump(new Vector3(Mathf.Cos(a) * 3.75f, 0.6f, Mathf.Sin(a) * 3.75f), new Vector3(0.3f, 0.18f, 0.3f), A(Cyan, 255), 5);
            }
            float phase = r.Range(0f, 6.28f);
            float baseY = c.GroundAt(x, z) + 0.8f;
            c.Glow.Uv = new Vector2(0.25f, phase);
            GlowColumn(c, x, baseY, z, 3f, 34f, A(Cyan, 30), 10);
            for (int i = 0; i < 6; i++)
            {
                c.Glow.Uv = new Vector2(0.9f, phase - i * 0.9f);
                c.Glow.FlatRing(baseY + 2f + i * 5f, x, z, 2.7f, 3f, A(Cyan, 110 - i * 15), 20);
            }
            c.Glow.Uv = new Vector2(0.4f, phase);
            c.Glow.Halo(new Vector3(x, baseY + 0.5f, z), 5f, A(Cyan, 60));
            c.Glow.Uv = Vector2.zero;
            LightPool(c, x, z, 9f, Cyan, 0.6f, 0.2f);
            c.PaintDisc(x, z, 7f, 1);
            return true;
        }

        /// <summary>A faint upright column of added light, open at both ends, fading out upwards. Its faces have no normals, so they don't drift (TerrainGlow).</summary>
        private static void GlowColumn(TerrainCanvas c, float x, float y, float z, float radius, float height, Color32 color, int sides)
        {
            Color32 clear = A(color, 0);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var b0 = new Vector3(x + Mathf.Cos(a0) * radius, y, z + Mathf.Sin(a0) * radius);
                var b1 = new Vector3(x + Mathf.Cos(a1) * radius, y, z + Mathf.Sin(a1) * radius);
                Vector3 t0 = b0 + Vector3.up * height, t1 = b1 + Vector3.up * height;
                // Both ways round, so that it shows from inside and out.
                c.Glow.FlatQuad(b0, t0, t1, b1, color, clear, clear, color);
                c.Glow.FlatQuad(b0, b1, t1, t0, color, color, clear, clear);
            }
        }

        /// <summary>A smooth low console: a flattened body on a stem, a slanted face of light towards the field.</summary>
        private static bool Lectern(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            if (!c.Stand(x, z, 2f, 1.9f, 0.3f)) return false;
            c.PlaceSolid(x, z, yaw);
            c.Solid.Cylinder(Vector3.zero, 0.9f, 0.45f, 0.8f, 8, MetalMid);
            c.Solid.Lump(new Vector3(0f, 1.05f, 0f), new Vector3(1.9f, 0.42f, 1.1f), MetalLight, 10, 0f, 0, Pale);
            Face(c.Solid, new Vector3(-0.8f, 1.38f, 0.75f), new Vector3(-0.8f, 1.62f, 0.25f), new Vector3(0.8f, 1.62f, 0.25f), new Vector3(0.8f, 1.38f, 0.75f),
                new Vector3(0f, 0.7f, 0.7f), A(Cyan, 190));
            c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
            c.Glow.Halo(LocalToWorld(c, x, z, yaw, new Vector3(0f, 1.8f, 0.5f)), 1.4f, A(Cyan, 40));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A low rail of posts with a glowing bar, along the outside of the flyers' strip. Lower than 1.5 m, so it may stand this close.</summary>
        private static void Rail(TerrainCanvas c, float x)
        {
            bool havePrevious = false;
            float px = 0f, pz = 0f;
            for (int i = 0; i < 14; i++)
            {
                float z = c.Z(3f + i * 7f);
                if (!c.Stand(x, z, 0.35f, 1.3f, 0f))
                {
                    havePrevious = false;
                    continue;
                }
                c.PlaceSolid(x, z);
                c.Solid.Cylinder(Vector3.zero, 0.24f, 0.14f, 1.05f, 6, MetalMid);
                c.Solid.Lump(new Vector3(0f, 1.1f, 0f), new Vector3(0.2f, 0.12f, 0.2f), Pale, 6);
                if (havePrevious)
                    c.Solid.Tube(new[] { new Vector3(0f, 1.1f, 0f), new Vector3(px - x, 1.1f, pz - z) }, new[] { 0.09f, 0.09f }, 6, A(Cyan, 120), A(Cyan, 120));
                havePrevious = true;
                px = x;
                pz = z;
            }
        }

        /// <summary>A shallow stepped half-round balcony facing the field, a grav lift of light in its middle between two curved blades.</summary>
        private static bool Balcony(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            const float radius = 19f, step = 3.2f, rise = 0.55f;
            const int steps = 4, segments = 20;
            if (!c.Stand(x, z, radius, steps * rise, 0.3f)) return false;
            c.PaintDisc(x, z, radius + 4f, 2);
            c.PlaceSolid(x, z, yaw);
            TerrainMeshBuilder m = c.Solid;
            for (int s = 0; s < steps; s++)
            {
                // The top step keeps a tiny hole in its middle, so that no quad is degenerate.
                float outer = radius - s * step, inner = s < steps - 1 ? outer - step : 0.3f;
                float y0 = s * rise, y1 = (s + 1) * rise;
                Color32 top = s % 2 == 0 ? Pale : MetalLight;
                for (int i = 0; i < segments; i++)
                {
                    float a0 = Mathf.PI * i / segments, a1 = Mathf.PI * (i + 1) / segments;
                    float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                    Face(m, new Vector3(c0 * inner, y1, s0 * inner), new Vector3(c0 * outer, y1, s0 * outer), new Vector3(c1 * outer, y1, s1 * outer), new Vector3(c1 * inner, y1, s1 * inner),
                        Vector3.up, top);
                    float am = (a0 + a1) * 0.5f;
                    Face(m, new Vector3(c0 * outer, y0, s0 * outer), new Vector3(c0 * outer, y1, s0 * outer), new Vector3(c1 * outer, y1, s1 * outer), new Vector3(c1 * outer, y0, s1 * outer),
                        new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)), MetalMid);
                }
                Face(m, new Vector3(-outer, y0, 0f), new Vector3(-outer, y1, 0f), new Vector3(outer, y1, 0f), new Vector3(outer, y0, 0f), Vector3.back, MetalDark);
            }
            // Lines of light along the edges of the steps, on the half towards the field.
            float from = c.Side > 0 ? 180f : 0f;
            c.Glow.Uv = new Vector2(0.3f, r.Range(0f, 6.28f));
            for (int s = 0; s < steps; s++)
            {
                float outer = radius - s * step;
                c.Glow.FlatRing(c.GroundAt(x, z) + (s + 1) * rise + 0.02f, x, z, outer - 0.32f, outer - 0.14f, A(s % 2 == 0 ? Violet : Cyan, 150), 32, from, from + 180f);
            }
            c.Glow.Uv = Vector2.zero;

            // Two blades curving in over the lift, standing on the top step.
            float topY = steps * rise;
            for (int k = -1; k <= 1; k += 2)
            {
                Profile.Clear();
                Widths.Clear();
                const int n = 9;
                float bladeHeight = r.Range(22f, 26f);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (n - 1f);
                    Profile.Add(new Vector2(4.5f * t * t, topY + bladeHeight * t));
                    Widths.Add(Mathf.Lerp(2.4f, 0.35f, t));
                }
                // u runs towards the middle, so the blade bows inwards, its inner face lit.
                Band(m, new Vector3(k * 7f, 0f, 2f), new Vector3(-k, 0f, 0f), Profile, Widths, 0.8f, MetalMid, MetalLight, A(Violet, 120));
            }
            float baseY = c.GroundAt(x, z) + topY + 0.05f;
            Vector3 middle = LocalToWorld(c, x, z, yaw, new Vector3(0f, 0f, 2f));
            float phase = r.Range(0f, 6.28f);
            c.Glow.Uv = new Vector2(0.25f, phase);
            GlowColumn(c, middle.x, baseY, middle.z, 2.6f, 40f, A(Violet, 34), 10);
            for (int i = 0; i < 7; i++)
            {
                c.Glow.Uv = new Vector2(0.9f, phase - i * 0.8f);
                c.Glow.FlatRing(baseY + 2f + i * 5f, middle.x, middle.z, 2.3f, 2.6f, A(Violet, 120 - i * 14), 20);
            }
            c.Glow.Uv = new Vector2(0.4f, phase);
            c.Glow.Halo(new Vector3(middle.x, baseY + 1f, middle.z), 5f, A(Magenta, 60));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        /// <summary>A great elliptical arch across the far end of the hall, a line of magenta light along its inner edge and an orb of light hanging under its crown.</summary>
        private static bool GreatArch(TerrainCanvas c, TerrainRandom r, float z, float yaw)
        {
            const float span = 118f, depth = 4f;
            float height = r.Range(50f, 55f);
            if (!c.IsClear(-span, z, 4f, height) || !c.IsClear(span, z, 4f, height)) return false;
            c.Stand(-span, z, 4f, height, 0.3f);
            c.Stand(span, z, 4f, height, 0.3f);
            c.PlaceSolid(0f, z, yaw);
            Profile.Clear();
            Widths.Clear();
            const int n = 28;
            for (int i = 0; i < n; i++)
            {
                // From one foot over to the other, turning clockwise, so that the band's inner edge is the arch's underside.
                float a = Mathf.PI * (1f - i / (n - 1f));
                Profile.Add(new Vector2(Mathf.Cos(a) * span, Mathf.Sin(a) * height));
                Widths.Add(Mathf.Lerp(5.5f, 3.6f, Mathf.Sin(a)));
            }
            Band(c.Solid, Vector3.zero, Vector3.right, Profile, Widths, depth, MetalMid, MetalLight, MetalDark);
            // The line of light along its underside, just proud of both faces.
            ShiftProfile(Profile, Widths, 0.7f, 1, Profile2, Widths2, 0.45f);
            Profile2.RemoveAt(Profile2.Count - 1);
            Widths2.RemoveAt(Widths2.Count - 1);
            Band(c.Solid, Vector3.zero, Vector3.right, Profile2, Widths2, depth + 0.1f, A(Magenta, 210), A(Magenta, 210), A(Magenta, 210));
            var orb = new Vector3(0f, height - 6f, 0f);
            c.Solid.Lump(orb, new Vector3(1.6f, 2.1f, 1.6f), A(Violet, 255), 10);
            c.Solid.Tube(new[] { orb + Vector3.up * 1.8f, new Vector3(0f, height - 1.6f, 0f) }, new[] { 0.25f, 0.5f }, 6, MetalMid, MetalLight);
            c.Glow.Uv = new Vector2(0.35f, r.Range(0f, 6.28f));
            c.Glow.Halo(LocalToWorld(c, 0f, z, yaw, orb), 6f, A(Violet, 70));
            c.Glow.Uv = Vector2.zero;
            return true;
        }

        private static void Sparks(TerrainCanvas c, TerrainRandom r)
        {
            for (int i = 0; i < 26; i++)
            {
                float x = r.Range(-140f, 140f), v = r.Range(0f, 140f);
                c.Glow.Mote(new Vector3(x, r.Range(2f, 22f), c.Z(v)), r.Range(0.14f, 0.3f), A(Violet, (int)r.Range(110f, 200f)),
                    new Vector3(r.Range(1f, 3.5f), r.Range(0.6f, 2.5f), r.Range(0.06f, 0.2f)), r.Range(0f, 6.28f));
            }
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>A quad that faces outward whichever way round its corners were given. Corners go round the quad in order; it is judged by its diagonals.</summary>
        private static void Face(TerrainMeshBuilder m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color32 color)
        {
            if (Vector3.Dot(Vector3.Cross(c - a, d - b), outward) >= 0f) m.Quad(a, b, c, d, color);
            else m.Quad(a, d, c, b, color);
        }

        /// <summary>
        /// A flat-sided band along a curve in an upright plane, in the current placement: ribs, arches, blades. profile is (along axis, up) in local metres from origin, widths its
        /// width across the curve at each point (never 0), depth its thickness across the plane. inside colours the edge on the right of the curve's direction (its concave side
        /// when it turns clockwise).
        /// </summary>
        private static void Band(TerrainMeshBuilder m, Vector3 origin, Vector3 axis, List<Vector2> profile, List<float> widths, float depth,
            Color32 bottom, Color32 top, Color32 inside)
        {
            int n = profile.Count;
            if (n < 2) return;
            Vector3 across = new Vector3(axis.z, 0f, -axis.x) * (depth * 0.5f);
            BandInner.Clear();
            BandOuter.Clear();
            BandNormal.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector2 along = (i == 0 ? profile[1] - profile[0] : i == n - 1 ? profile[n - 1] - profile[n - 2] : profile[i + 1] - profile[i - 1]).normalized;
                var normal = new Vector2(-along.y, along.x);
                Vector2 o = profile[i] + normal * (widths[i] * 0.5f);
                Vector2 q = profile[i] - normal * (widths[i] * 0.5f);
                BandOuter.Add(origin + axis * o.x + Vector3.up * o.y);
                BandInner.Add(origin + axis * q.x + Vector3.up * q.y);
                BandNormal.Add(axis * normal.x + Vector3.up * normal.y);
            }
            for (int i = 0; i < n - 1; i++)
            {
                Color32 color = Color32.Lerp(bottom, top, (i + 0.5f) / (n - 1));
                Vector3 oa = BandOuter[i], ob = BandOuter[i + 1], ia = BandInner[i], ib = BandInner[i + 1];
                Vector3 normal = BandNormal[i] + BandNormal[i + 1];
                Face(m, oa + across, ia + across, ib + across, ob + across, across, color);
                Face(m, oa - across, ia - across, ib - across, ob - across, -across, color);
                Face(m, oa + across, ob + across, ob - across, oa - across, normal, color);
                Face(m, ia + across, ia - across, ib - across, ib + across, -normal, inside);
            }
            Vector3 startOut = BandOuter[0] - BandOuter[1] + BandInner[0] - BandInner[1];
            Face(m, BandOuter[0] + across, BandOuter[0] - across, BandInner[0] - across, BandInner[0] + across, startOut, bottom);
            Vector3 endOut = BandOuter[n - 1] - BandOuter[n - 2] + BandInner[n - 1] - BandInner[n - 2];
            Face(m, BandOuter[n - 1] + across, BandOuter[n - 1] - across, BandInner[n - 1] - across, BandInner[n - 1] + across, endOut, top);
        }

        /// <summary>A narrow strip along the inner edge of a band's profile (Band's inside), from point first on: its middle fromEdge in from that edge.</summary>
        private static void ShiftProfile(List<Vector2> profile, List<float> widths, float fromEdge, int first, List<Vector2> shifted, List<float> shiftedWidths, float width)
        {
            shifted.Clear();
            shiftedWidths.Clear();
            int n = profile.Count;
            for (int i = Mathf.Max(0, first); i < n; i++)
            {
                Vector2 along = (i == 0 ? profile[1] - profile[0] : i == n - 1 ? profile[n - 1] - profile[n - 2] : profile[i + 1] - profile[i - 1]).normalized;
                var normal = new Vector2(-along.y, along.x);
                shifted.Add(profile[i] - normal * (widths[i] * 0.5f - fromEdge));
                shiftedWidths.Add(width);
            }
        }

        /// <summary>Reserves a row of places along a wall's local x, all or none, so that a long thin thing doesn't claim one wide disc. The places never overlap each other.</summary>
        private static bool StandRow(TerrainCanvas c, float x, float z, float yaw, float halfLength, float forward, float radius, float height, float shadow)
        {
            float cs = Mathf.Cos(yaw * Mathf.Deg2Rad), sn = Mathf.Sin(yaw * Mathf.Deg2Rad);
            float spacing = radius * 2.02f;
            int count = 1 + Mathf.Max(0, Mathf.FloorToInt((halfLength * 2f - radius * 2f) / spacing));
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    float lx = (i - (count - 1) * 0.5f) * spacing;
                    float wx = x + lx * cs + forward * sn, wz = z - lx * sn + forward * cs;
                    if (pass == 0)
                    {
                        if (!c.IsClear(wx, wz, radius, height)) return false;
                    }
                    else c.Stand(wx, wz, radius, height, shadow);
                }
            }
            return true;
        }

        /// <summary>Where a local point of a thing placed at (x, z) turned by yaw lies in the world (as TerrainMeshBuilder.Place turns it).</summary>
        private static Vector3 LocalToWorld(TerrainCanvas c, float x, float z, float yaw, Vector3 local)
        {
            float cs = Mathf.Cos(yaw * Mathf.Deg2Rad), sn = Mathf.Sin(yaw * Mathf.Deg2Rad);
            return new Vector3(x + local.x * cs + local.z * sn, c.GroundAt(x, z) + local.y, z - local.x * sn + local.z * cs);
        }

        // ------------------------------------------------------------------ paths and colours

        private static void Straight(List<Vector2> points, float ax, float az, float bx, float bz, int pieces)
        {
            points.Clear();
            for (int i = 0; i <= pieces; i++)
            {
                float t = i / (float)pieces;
                points.Add(new Vector2(Mathf.Lerp(ax, bx, t), Mathf.Lerp(az, bz, t)));
            }
        }

        private static void Bezier(List<Vector2> points, Vector2 a, Vector2 control, Vector2 b, int pieces)
        {
            points.Clear();
            for (int i = 0; i <= pieces; i++)
            {
                float t = i / (float)pieces, u = 1f - t;
                points.Add(a * (u * u) + control * (2f * u * t) + b * (t * t));
            }
        }

        /// <summary>One side of a lens from (ax, az) to (bx, bz), bulging out by bulge (its sign picks the side).</summary>
        private static void Lens(List<Vector2> points, float ax, float az, float bx, float bz, float bulge, int pieces)
        {
            points.Clear();
            var a = new Vector2(ax, az);
            var b = new Vector2(bx, bz);
            Vector2 direction = (b - a).normalized;
            var side = new Vector2(direction.y, -direction.x);
            for (int i = 0; i <= pieces; i++)
            {
                float t = i / (float)pieces;
                points.Add(Vector2.Lerp(a, b, t) + side * (bulge * Mathf.Sin(t * Mathf.PI)));
            }
        }

        /// <summary>A copy of a path moved sideways, the same way FlatRibbon measures its sides.</summary>
        private static void Offset(List<Vector2> points, float distance, List<Vector2> moved)
        {
            moved.Clear();
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 along = (i == 0 ? points[1] - points[0] : i == n - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1]).normalized;
                moved.Add(points[i] + new Vector2(along.y, -along.x) * distance);
            }
        }

        private static Color32 A(Color32 color, int alpha) => new Color32(color.r, color.g, color.b, (byte)Mathf.Clamp(alpha, 0, 255));

        private static Color32 Vary(TerrainRandom r, Color32 color, float amount)
        {
            float f = 1f + r.Range(-amount, amount);
            return new Color32((byte)Mathf.Min(255f, color.r * f), (byte)Mathf.Min(255f, color.g * f), (byte)Mathf.Min(255f, color.b * f), color.a);
        }
    }
}
