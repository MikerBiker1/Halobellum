// TerrainInteriorFlood.cs
// The Flood-infested ship (D80): a deck of a UNSC or a Covenant ship that the Flood has taken. The hull's own floor (grimy gunmetal deck plates, or tarnished purple metal
// with a geometric inlay) is swallowed from the edges and corners inwards by living flesh: swollen cells and blisters in tan, salmon and red-brown with dark creases and
// wet highlights, and darker decaying crust where it is oldest. The middle of each deployment zone stays mostly hull, mid-toned, so that units read on it; the darkness
// lives outside the arena, where the floor of the yards falls away into murk under a dark ceiling and a reddish-brown haze.
// Veins creep across the floor, pustules sit at the fronts, and deck plates lie lifted and cracked with flesh oozing round their edges.
// Around the field: the room's walls, with sheets of mass draped down them to fat swollen feet, roots and hanging drips, shelf fungus, and great tendrils arching from
// them over the yards; the hull's own architecture still showing through the growth (UNSC bulkheads and pillars with hazard banding, half-swallowed containers with
// tendrils draped over them, gantries and a great crane at the far end; Covenant curved ribs, bulging pillars with dim violet slits, overgrown pointed arches);
// clusters of swollen infection pods with glowing cores and the sickly yellow-green or amber light they throw, fungal stalks with caps glowing underneath, bulbous sacs,
// tendrils hanging between structures and drifting spores. A few dying lights of the old ship still flicker: cold white on a UNSC hull, violet on a Covenant one.
// All shapes and textures are original and abstract: no markings, no glyphs, no text.
// Textures: Tools/TerrainGen/make_interior_flood.py.
// Looks only. Depends on: TerrainLook, TerrainLayout, TerrainTheme, TerrainCanvas, TerrainMeshBuilder, TerrainProps (tendrils), TerrainNoise, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class FloodInterior
    {
        private static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        public static TerrainLook Create(FloodHull hull)
        {
            bool covenant = hull == FloodHull.Covenant;
            return new TerrainLook
            {
                Id = covenant ? "FloodOnCovenant" : "FloodOnUnsc",
                Name = covenant ? "Flood-Infested Covenant Ship" : "Flood-Infested UNSC Ship",
                Theme = new TerrainTheme
                {
                    Faction = BattleFactions.Flood,
                    // Layer 0 is the hull of this ship, 1 the living flesh, 2 the old decaying crust.
                    Layers = new[] { covenant ? "InteriorFloodHullCovenant" : "InteriorFloodHullUnsc", "InteriorFloodBiomass", "InteriorFloodRot" },
                    // UNSC: 4 m plates, two to a tile of 8 m. Covenant: 5 m panels, two to a tile of 10 m. Flesh cells of about 1.8 m in an 11 m tile, crust a little finer.
                    Tiles = new Vector3(covenant ? 1f / 10f : 1f / 8f, 1f / 11f, 1f / 6f),
                    GroundTint = Color.white,
                    Hills = TerrainHillStyle.Flat,
                    HillHeight = 0f,
                    FarHeight = 0f,
                    // The ceiling and the air: a murky, nearly black red-brown.
                    SkyTop = Rgb(8, 6, 6),
                    SkyHorizon = Rgb(30, 20, 18),
                    Cloud = Rgb(20, 14, 12),
                    CloudAmount = 0f,
                    MoonAmount = 0f,
                    // A plain, faintly warm key light, bright enough that units stay lit; dim, warm grey ambient. No green: the eeriness comes from the dark and the glows.
                    SunColor = Rgb(255, 240, 230),
                    SunIntensity = 1f,
                    SunAngles = new Vector2(56f, -22f),
                    AmbientSky = Rgb(108, 98, 104),
                    AmbientEquator = Rgb(94, 80, 80),
                    AmbientGround = Rgb(56, 42, 42),
                    // Fog by distance from the middle: the floor's corners barely, the walls deep in it.
                    FogColor = Rgb(22, 15, 14),
                    FogStart = 108f,
                    FogEnd = 285f,
                    FogAmount = 1f,
                    Haze = Rgb(70, 44, 36),
                    HazeAmount = 0.26f,
                    // Spores.
                    MoteColor = Rgb(212, 226, 118),
                    MoteCount = 170,
                    // The hull's metal (polished on a Covenant ship), wet glistening flesh, the duller crust.
                    Gloss = new Vector4(covenant ? 0.45f : 0.22f, 0.75f, 0.2f, 28f),
                    GlossColor = new Color(1f, 0.88f, 0.82f, 0.05f),
                    GlossLightAngles = new Vector2(44f, 180f),
                    Vignette = 0.35f,
                    VignetteColor = Rgb(10, 5, 5),
                    ColorFilter = new Color(1f, 0.97f, 0.955f),
                    Contrast = 12f,
                    Saturation = 0f,
                    Exposure = -0.05f,
                },
                Layout = new FloodShipLayout(hull),
            };
        }
    }

    public sealed class FloodShipLayout : TerrainLayout
    {
        private const float FloorX = 80f;
        private const float FloorZ = 82f;
        /// <summary>Flyers deploy in strips 84 to 92 m from the middle line: nothing taller than LowTop has any part within this, alongside the floor.</summary>
        private const float FlankClear = 96.6f;
        /// <summary>The most anything near the flank strips may rise (the rule is 1.5 m; this leaves a margin).</summary>
        private const float LowTop = 1.4f;
        /// <summary>Where the side walls stand, and their inner faces.</summary>
        private const float WallX = 148f;
        private const float WallFarV = FloorZ + 56f;

        // The Flood.
        private static readonly Color32 Skin = S(156, 112, 94);
        private static readonly Color32 SkinPale = S(206, 166, 146);
        private static readonly Color32 SkinPink = S(178, 110, 102);
        private static readonly Color32 SkinDark = S(104, 64, 58);
        private static readonly Color32 SkinRed = S(130, 72, 64);
        private static readonly Color32 RootColor = S(100, 56, 52);
        private static readonly Color32 TipColor = S(180, 130, 112);
        private static readonly Color32 PodSkin = S(204, 160, 132);
        private static readonly Color32 GlowGreen = S(206, 236, 112, 190);
        private static readonly Color32 GlowAmber = S(255, 182, 84, 190);
        private static readonly Color32 SacSkin = S(184, 116, 108, 24);
        private static readonly Color32 SacTop = S(226, 180, 164, 70);
        private static readonly Color32 Stalk = S(200, 188, 160);
        private static readonly Color32 StalkTop = S(172, 158, 126);
        private static readonly Color32 Cap = S(132, 94, 76);
        private static readonly Color32 Gill = S(214, 226, 140, 80);
        private static readonly Color32 ShelfTop = S(130, 98, 76);
        private static readonly Color32 ShelfUnder = S(210, 196, 152, 40);

        // A UNSC hull.
        private static readonly Color32 Steel = S(104, 110, 116);
        private static readonly Color32 SteelDark = S(66, 70, 76);
        private static readonly Color32 SteelPale = S(142, 146, 150);
        private static readonly Color32 Hazard = S(204, 162, 40);
        private static readonly Color32 HazardDark = S(40, 38, 34);
        private static readonly Color32 ColdLamp = S(222, 234, 255, 220);
        private static readonly Color32 DeadLamp = S(60, 62, 66);
        private static readonly Color32 Cold = new Color32(206, 226, 255, 255);

        // A Covenant hull.
        private static readonly Color32 Violet = S(114, 78, 148);
        private static readonly Color32 VioletDark = S(72, 48, 98);
        private static readonly Color32 VioletPale = S(158, 126, 188);
        private static readonly Color32 VioletLit = S(172, 100, 244, 150);
        private static readonly Color32 VioletDead = S(62, 42, 82);
        private static readonly Color32 Dying = new Color32(172, 104, 255, 255);

        // The light the Flood gives.
        private static readonly Color32 Sickly = new Color32(200, 236, 96, 255);
        private static readonly Color32 Amber = new Color32(255, 176, 72, 255);

        private static readonly List<Vector3> Path = new List<Vector3>();
        private static readonly List<float> Radii = new List<float>();
        /// <summary>A drip's own path, so that a drip can hang from a path still being read.</summary>
        private static readonly List<Vector3> DripPath = new List<Vector3>();
        private static readonly List<float> DripRadii = new List<float>();
        private static readonly List<Vector2> Line = new List<Vector2>();
        private static readonly List<Vector2> Branch = new List<Vector2>();
        /// <summary>High points of the structures around this side (world), for tendrils hung between them.</summary>
        private static readonly List<Vector3> Tops = new List<Vector3>();

        private readonly FloodHull _hull;

        public FloodShipLayout(FloodHull hull) => _hull = hull;

        public override string Name => _hull == FloodHull.Covenant ? "Flood-Infested Covenant Ship" : "Flood-Infested UNSC Ship";

        private bool OnCovenant => _hull == FloodHull.Covenant;

        // ------------------------------------------------------------------ the ground

        public override void BaseWeights(float x, float z, float height, float outside, float seedX, float seedZ, out float second, out float third)
        {
            float spread = TerrainNoise.Fbm((x + seedX) / 24f, (z + seedZ) / 24f, 3);
            float lobes = TerrainNoise.Fbm((x - seedZ) / 9f, (z + seedX) / 9f, 2);
            float rotNoise = TerrainNoise.Fbm((x + seedZ) / 15f, (z - seedX) / 15f, 3);
            // How far in from the floor's edges, measured so that corners count as deeper in the mass: small near any edge, smallest in a corner,
            // about 27 m in the middle of a deployment zone and 40 m at the middle of the field. Zero anywhere off the floor.
            float dx = Mathf.Max(0f, FloorX - Mathf.Abs(x)), dz = Mathf.Max(0f, FloorZ - Mathf.Abs(z));
            float inward = dx * dz / Mathf.Max(0.01f, dx + dz);
            // The front of the mass wanders between about 4 and 20 m in, in blobby lobes.
            float front = 4f + 34f * Mathf.Max(0f, spread - 0.28f) + 8f * (lobes - 0.5f);
            float mass = TerrainNoise.Smooth(front + 2.5f, front - 2.5f, inward);
            // Crust where the mass is old, and a darker decaying fringe along its front.
            float rot = Mathf.Clamp01(TerrainNoise.Smooth(0.55f, 0.68f, rotNoise) * 0.6f * mass + mass * (1f - mass) * 0.9f);
            if (outside > 0f)
            {
                // Off the floor the deck shows through the mass in patches, fewer further out; it fades in over the first metres so the floor's edge isn't a hard line.
                float hullShows = TerrainNoise.Smooth(0.54f, 0.66f, spread) * (1f - 0.6f * TerrainNoise.Smooth(30f, 80f, outside));
                float away = TerrainNoise.Smooth(0f, 12f, outside);
                float outerMass = 1f - hullShows;
                float outerRot = TerrainNoise.Smooth(0.52f, 0.68f, rotNoise) * 0.55f * outerMass;
                second = mass + (outerMass - mass) * away;
                third = rot + (outerRot - rot) * away;
                return;
            }
            second = mass;
            third = rot;
        }

        public override void Furnish(TerrainCanvas c, TerrainRandom r)
        {
            Tops.Clear();
            PaintFloor(c, r);
            if (OnCovenant) CovenantFloor(c, r);
            else UnscFloor(c, r);
            LiftedPlates(c, r);
            Creep(c, r);
            DarkenYards(c);
            Rails(c, r);
            Walls(c, r);
            if (OnCovenant) CovenantWreckage(c, r);
            else UnscWreckage(c, r);
            Growth(c, r);
        }

        /// <summary>How much biomass and rot already lie at a point of the control map.</summary>
        private static float MassAt(TerrainCanvas c, float x, float z)
        {
            int n = c.Resolution;
            int ix = Mathf.Clamp((int)(c.AxisX.ToU(x) * n), 0, n - 1);
            int iz = Mathf.Clamp((int)(c.AxisZ.ToU(z) * n), 0, n - 1);
            int i = iz * n + ix;
            return Mathf.Clamp01(c.Second[i] + c.Third[i]);
        }

        /// <summary>The floor of the yards falls away into the dark, more the further out: the arena itself keeps its light.</summary>
        private static void DarkenYards(TerrainCanvas c)
        {
            int n = c.Resolution;
            float[] worldX = c.AxisX.World, worldZ = c.AxisZ.World;
            for (int iz = 0; iz < n; iz++)
            {
                float z = worldZ[iz];
                if (c.Side > 0 ? z <= 0f : z > 0f) continue;
                float oz = Mathf.Max(0f, Mathf.Abs(z) - FloorZ);
                int row = iz * n;
                for (int ix = 0; ix < n; ix++)
                {
                    float ox = Mathf.Max(0f, Mathf.Abs(worldX[ix]) - FloorX);
                    if (ox <= 0f && oz <= 0f) continue;
                    float outside = Mathf.Sqrt(ox * ox + oz * oz);
                    c.Shade[row + ix] *= 1f - 0.6f * TerrainNoise.Smooth(2f, 32f, outside);
                }
            }
        }

        /// <summary>A point just outside one of this side's floor edges, and the heading from it into the floor.</summary>
        private static void EdgePoint(TerrainCanvas c, TerrainRandom r, out Vector2 point, out float heading)
        {
            if (r.Chance(0.62f))
            {
                float k = r.Sign();
                point = new Vector2(k * (FloorX + 2f), c.Z(r.Range(4f, 80f)));
                heading = (k > 0f ? -1.5708f : 1.5708f) + r.Range(-0.5f, 0.5f);
            }
            else
            {
                point = new Vector2(r.Range(-76f, 76f), c.Z(FloorZ + 2f));
                heading = (c.Side > 0 ? 3.1416f : 0f) + r.Range(-0.5f, 0.5f);
            }
        }

        private void PaintFloor(TerrainCanvas c, TerrainRandom r)
        {
            // The middle of the deployment zone stays hull, so units read on it.
            c.PaintDisc(r.Range(-8f, 8f), c.Z(r.Range(36f, 46f)), r.Range(20f, 26f), 0, 0.6f);
            // The corners are the deepest in the mass.
            for (int k = -1; k <= 1; k += 2)
                c.PaintDisc(k * FloorX, c.Z(FloorZ), r.Range(16f, 24f), 1, 0.9f);
            // Tongues creeping in from the edges.
            int tongues = 8 + r.Int(4);
            for (int i = 0; i < tongues; i++)
            {
                EdgePoint(c, r, out Vector2 start, out float heading);
                TerrainCanvas.Wander(Line, r, start, heading, r.Range(4f, 6f), 2 + r.Int(4), 0.5f);
                float width = r.Range(6f, 10f);
                for (int p = 1; p < Line.Count; p++)
                {
                    float t = p / (float)(Line.Count - 1);
                    c.PaintLine(Line[p - 1].x, Line[p - 1].y, Line[p].x, Line[p].y, width * Mathf.Lerp(1f, 0.45f, t), 1, 0.85f);
                }
                Vector2 tip = Line[Line.Count - 1];
                c.PaintDisc(tip.x, tip.y, width * 0.4f, 1, 0.6f);
            }
            // Crust where the mass is oldest: at the edges, and out around the field.
            for (int i = 0; i < 8; i++)
            {
                EdgePoint(c, r, out Vector2 p, out float heading);
                float inward = r.Range(-2f, 7f);
                c.PaintDisc(p.x + Mathf.Sin(heading) * inward, p.y + Mathf.Cos(heading) * inward, r.Range(3f, 7f), 2, 0.7f);
            }
            for (int i = 0; i < 12; i++)
            {
                Around(c, r, out float x, out float z);
                c.PaintDisc(x, z, r.Range(5f, 12f), 2, 0.7f);
            }
        }

        /// <summary>A UNSC deck: worn painted lanes with cold strip lights set beside them, most dead, a few flickering, and a faint hazard band.</summary>
        private void UnscFloor(TerrainCanvas c, TerrainRandom r)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                float laneX = k * r.Range(26f, 34f);
                int segment = 0;
                for (float v = 4f; v < 79f; v += 9f, segment++)
                {
                    float z = c.Z(v + 3f);
                    if (MassAt(c, laneX, z) > 0.35f) continue;
                    c.Decals.Uv = new Vector2(0.45f, 0.2f);
                    c.Decals.FlatRect(D, laneX, z, 0.34f, 6f, 0f, C(176, 150, 64, 72));
                    c.Decals.Uv = Vector2.zero;
                    if (segment % 2 != 0) continue;
                    float lightX = laneX - k * 0.7f;
                    c.Decals.FlatRect(D, lightX, z, 0.24f, 5f, 0f, C(74, 76, 80, 140));
                    if (!r.Chance(0.45f)) continue;
                    Line.Clear();
                    Line.Add(new Vector2(lightX, z - 2.3f));
                    Line.Add(new Vector2(lightX, z + 2.3f));
                    LightStrip(c, Line, 0.07f, Cold, r.Range(0.7f, 0.98f), r.Range(0f, 6.28f));
                }
            }
            // A worn hazard band across part of the zone's far end, where the mass hasn't reached.
            float bandV = r.Range(70f, 76f);
            float z0 = Mathf.Min(c.Z(bandV), c.Z(bandV + 1.6f)), z1 = Mathf.Max(c.Z(bandV), c.Z(bandV + 1.6f));
            float startX = r.Range(-30f, 6f);
            Color32 paint = C(178, 148, 56, 78);
            c.Decals.Uv = new Vector2(0.5f, 0.2f);
            for (int s = 0; s < 18; s++)
            {
                float x = startX + s * 1.6f;
                if (MassAt(c, x, (z0 + z1) * 0.5f) > 0.35f) continue;
                c.Decals.FlatQuad(new Vector3(x, D, z0), new Vector3(x + 0.8f, D, z1), new Vector3(x + 1.6f, D, z1), new Vector3(x + 0.8f, D, z0), paint, paint, paint, paint);
            }
            c.Decals.Uv = Vector2.zero;
            // Old oil and grime.
            for (int i = 0; i < 10; i++) Stain(c, r.Range(-74f, 74f), c.Z(r.Range(8f, 78f)), r.Range(1.5f, 4f), C(60, 52, 48, 80));
        }

        /// <summary>A Covenant deck: energy channels along the field with their light broken into weak flickering pieces, and arcs of pale inlay.</summary>
        private void CovenantFloor(TerrainCanvas c, TerrainRandom r)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                for (int lane = 0; lane < 2; lane++)
                {
                    float x = k * (lane == 0 ? r.Range(18f, 26f) : r.Range(44f, 52f));
                    for (float v = 4f; v < 80f; v += 4f)
                    {
                        float z = c.Z(v + 2f);
                        if (MassAt(c, x, z) > 0.35f) continue;
                        c.Decals.Uv = new Vector2(0f, 0.3f);
                        c.Decals.FlatRect(D, x, z, 0.55f, 4.02f, 0f, C(66, 46, 88, 150));
                        c.Decals.FlatRect(D, x - 0.34f, z, 0.08f, 4.02f, 0f, C(160, 128, 190, 90));
                        c.Decals.FlatRect(D, x + 0.34f, z, 0.08f, 4.02f, 0f, C(160, 128, 190, 90));
                        c.Decals.Uv = Vector2.zero;
                        if (!r.Chance(0.4f)) continue;
                        float half = r.Range(0.8f, 1.9f);
                        Line.Clear();
                        Line.Add(new Vector2(x, z - half));
                        Line.Add(new Vector2(x, z + half));
                        LightStrip(c, Line, 0.07f, new Color32(160, 92, 246, 255), r.Range(0.6f, 0.9f), r.Range(0f, 6.28f));
                    }
                }
            }
            // Arcs of pale inlay where the deck is still clean.
            for (int i = 0; i < 14; i++)
            {
                float x = r.Range(-70f, 70f), z = c.Z(r.Range(10f, 76f));
                if (MassAt(c, x, z) > 0.3f) continue;
                float radius = r.Range(3f, 7f);
                float from = r.Range(0f, 360f);
                c.Decals.Uv = new Vector2(0.3f, 0.2f);
                c.Decals.FlatRing(D, x, z, radius - 0.12f, radius + 0.12f, C(160, 128, 190, 70), 24, from, from + r.Range(90f, 240f));
                c.Decals.Uv = Vector2.zero;
            }
            for (int i = 0; i < 10; i++) Stain(c, r.Range(-74f, 74f), c.Z(r.Range(8f, 78f)), r.Range(1.5f, 4f), C(70, 54, 64, 80));
        }

        private Color32 PlateColor(TerrainRandom r) =>
            OnCovenant ? C(108 + r.Int(14), 80 + r.Int(10), 136 + r.Int(16), 225) : C(98 + r.Int(16), 103 + r.Int(16), 110 + r.Int(16), 225);

        /// <summary>Deck plates lifted and cracked by what grew under them, with flesh oozing out round their edges.</summary>
        private void LiftedPlates(TerrainCanvas c, TerrainRandom r)
        {
            int placed = 0;
            for (int i = 0; i < 16 && placed < 8; i++)
            {
                float x = r.Range(-72f, 72f), z = c.Z(r.Range(24f, 76f));
                if (MassAt(c, x, z) > 0.5f) continue;
                LiftedPlate(c, r, x, z, r.Range(2.4f, 4.2f), r.Range(1.8f, 3.2f));
                placed++;
            }
        }

        /// <summary>One lifted plate: a soft halo of ooze, the wet hollow it lay in, the plate itself shifted and turned with a lit edge, a shadowed edge and a crack,
        /// and beads of flesh squeezed out along its sides. Never near-black where it is solid.</summary>
        private void LiftedPlate(TerrainCanvas c, TerrainRandom r, float x, float z, float w, float d)
        {
            float yaw = r.Range(-25f, 25f);
            c.Decals.Uv = new Vector2(0f, 0.3f);
            c.Decals.FlatDisc(D, x, z, w * 0.95f, d * 0.95f, C(150, 96, 84, 150), C(150, 96, 84, 0), 12, yaw);
            c.Decals.FlatRect(D, x, z, w * 1.04f, d * 1.04f, yaw, C(94, 56, 54, 175));
            float px = x + r.Range(-0.5f, 0.5f), pz = z + r.Range(-0.4f, 0.4f), pyaw = yaw + r.Range(-14f, 14f);
            c.Decals.Uv = new Vector2(0.15f, 0.85f);
            Color32 plate = PlateColor(r);
            c.Decals.FlatRect(D, px, pz, w * 0.94f, d * 0.92f, pyaw, plate);
            // The edge tipped up towards the light, and the one tipped away.
            float cs = Mathf.Cos(pyaw * Mathf.Deg2Rad), sn = Mathf.Sin(pyaw * Mathf.Deg2Rad);
            float ox = sn * d * 0.4f, oz = cs * d * 0.4f;
            c.Decals.Uv = Vector2.zero;
            c.Decals.FlatRect(D, px + ox, pz + oz, w * 0.92f, 0.16f, pyaw, C(184, 180, 178, 170));
            c.Decals.FlatRect(D, px - ox, pz - oz, w * 0.92f, 0.22f, pyaw, C(52, 42, 42, 150));
            // A crack across it.
            Line.Clear();
            float a = r.Range(-0.6f, 0.6f);
            for (int p = 0; p <= 3; p++)
            {
                float t = p / 3f - 0.5f;
                Line.Add(new Vector2(px + (cs * t * w * 0.9f) + r.Range(-0.15f, 0.15f), pz - (sn * t * w * 0.9f) + t * a + r.Range(-0.15f, 0.15f)));
            }
            c.Decals.FlatRibbon(D, Line, 0.12f, 0.05f, C(70, 46, 46, 160));
            // Flesh squeezed out along its sides.
            int beads = 4 + r.Int(4);
            for (int b = 0; b < beads; b++)
            {
                float t = r.Range(-0.5f, 0.5f);
                float side = r.Sign();
                float bx = px + cs * t * w + sn * side * d * 0.5f, bz = pz - sn * t * w + cs * side * d * 0.5f;
                float br = r.Range(0.25f, 0.6f);
                c.Decals.FlatDisc(D, bx, bz, br * 1.3f, br, C(178, 112, 98, 215), C(178, 112, 98, 0), 9, r.Range(0f, 180f));
                c.Decals.FlatDisc(D, bx - br * 0.2f, bz + br * 0.2f, br * 0.4f, br * 0.32f, C(222, 182, 166, 200), C(222, 182, 166, 0), 6);
            }
            c.Decals.Uv = Vector2.zero;
        }

        /// <summary>Veins creeping in from the mass towards the middle of the zone, glowing pustules at its front, and wet stains along it.</summary>
        private void Creep(TerrainCanvas c, TerrainRandom r)
        {
            float targetZ = c.Z(41f);
            int veins = 0;
            for (int attempt = 0; attempt < 90 && veins < 14; attempt++)
            {
                float x = r.Range(-78f, 78f), z = c.Z(r.Range(3f, 80f));
                if (MassAt(c, x, z) < 0.55f) continue;
                float heading = Mathf.Atan2(r.Range(-24f, 24f) - x, targetZ - z) + r.Range(-0.4f, 0.4f);
                TerrainCanvas.Wander(Line, r, new Vector2(x, z), heading, r.Range(2.5f, 4f), 4 + r.Int(7), 0.45f);
                c.KeepOnFloor(Line, 0.5f);
                if (Line.Count < 2) continue;
                float width = r.Range(0.35f, 0.75f);
                Vein(c, Line, width);
                if (Line.Count > 4 && r.Chance(0.6f))
                {
                    TerrainCanvas.Wander(Branch, r, Line[1 + r.Int(Line.Count - 2)], heading + r.Range(-1.2f, 1.2f), 2.5f, 3 + r.Int(4), 0.5f);
                    c.KeepOnFloor(Branch, 0.5f);
                    Vein(c, Branch, width * 0.55f);
                }
                veins++;
            }

            // Pustules in clusters along the front of the mass, a few with a sickly glow.
            int clusters = 0;
            for (int attempt = 0; attempt < 60 && clusters < 9; attempt++)
            {
                float x = r.Range(-76f, 76f), z = c.Z(r.Range(4f, 79f));
                float m = MassAt(c, x, z);
                if (m < 0.35f || m > 0.85f) continue;
                int heads = 2 + r.Int(4);
                for (int h = 0; h < heads; h++)
                    FloodPustule(c, r, Mathf.Clamp(x + r.Range(-2.5f, 2.5f), -78f, 78f), z + r.Range(-2.5f, 2.5f), r.Range(0.22f, 0.5f), h == 0);
                Stain(c, x, z, r.Range(2f, 3.5f), C(90, 58, 52, 80));
                clusters++;
            }
        }

        /// <summary>A pustule on the floor: a pale wet head in a dark ring; a lit one glows sickly yellow-green or amber.</summary>
        private static void FloodPustule(TerrainCanvas c, TerrainRandom r, float x, float z, float radius, bool lit)
        {
            c.Decals.Uv = Vector2.zero;
            c.Decals.FlatDisc(D, x, z, radius * 1.7f, radius * 1.55f, C(70, 40, 38, 160), C(70, 40, 38, 0), 10);
            c.Decals.FlatDisc(D, x, z, radius, radius * 0.92f, C(206, 170, 140), C(150, 104, 90), 10);
            c.Decals.FlatDisc(D, x - radius * 0.2f, z + radius * 0.25f, radius * 0.4f, radius * 0.34f, C(236, 214, 196), C(206, 170, 140, 0), 8);
            Color32 glow = r.Chance(0.6f) ? Sickly : Amber;
            c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
            c.Glow.FlatDisc(G, x, z, radius * (lit ? 3.4f : 2.2f), radius * (lit ? 3.2f : 2.1f), new Color32(glow.r, glow.g, glow.b, (byte)(lit ? 46 : 18)), new Color32(glow.r, glow.g, glow.b, 0), 8);
            c.Glow.Uv = Vector2.zero;
        }

        // ------------------------------------------------------------------ placing things around the field

        /// <summary>True if a thing this tall may stand here, and reserves the place: it keeps the flank strips open as well as the floor.</summary>
        private static bool Tall(TerrainCanvas c, float x, float z, float radius, float height, float shadow = 0.5f) =>
            KeepsFlanksOpen(x, z, radius, height) && c.Stand(x, z, radius, height, shadow);

        /// <summary>The same test without reserving the place, for props that reserve it themselves.</summary>
        private static bool MayStand(TerrainCanvas c, float x, float z, float radius, float height) =>
            KeepsFlanksOpen(x, z, radius, height) && c.IsClear(x, z, radius, height);

        private static bool KeepsFlanksOpen(float x, float z, float radius, float height) =>
            height <= LowTop || Mathf.Abs(x) - radius >= FlankClear || Mathf.Abs(z) - radius >= FloorZ + 0.5f;

        /// <summary>True if a point (world) of something this thick keeps off the floor and the flank strips, and can't hide the floor from the camera (the south lean).</summary>
        private static bool Fits(TerrainCanvas c, Vector3 p, float radius)
        {
            float ax = Mathf.Abs(p.x), az = Mathf.Abs(p.z), top = p.y + radius;
            if (top > -0.5f && ax - radius < FloorX + 0.5f && az - radius < FloorZ + 0.5f) return false;
            if (top > LowTop && ax - radius < FlankClear && az - radius < FloorZ + 0.5f) return false;
            if (p.z < 0f && az + radius > FloorZ && ax - radius < FloorX + 0.5f && top * TerrainCanvas.SouthLean > az - radius - FloorZ + 1.2f) return false;
            return true;
        }

        private static bool PathFits(TerrainCanvas c, List<Vector3> points, List<float> radii)
        {
            for (int i = 0; i < points.Count; i++)
                if (!Fits(c, points[i], radii[i])) return false;
            return true;
        }

        /// <summary>A point in this side's surroundings: to the sides beyond the flank strips, or beyond the far end.</summary>
        private static void Around(TerrainCanvas c, TerrainRandom r, out float x, out float z)
        {
            if (r.Chance(0.6f))
            {
                x = r.Sign() * r.Range(100f, 140f);
                z = c.Z(r.Range(0f, FloorZ + 46f));
            }
            else
            {
                x = r.Range(-128f, 128f);
                z = c.Z(FloorZ + r.Range(6f, 48f));
            }
        }

        /// <summary>A point in the yards near the field, where the battle camera sees things best: just beyond the flank strips, or just beyond the far end.</summary>
        private static void Near(TerrainCanvas c, TerrainRandom r, out float x, out float z)
        {
            if (r.Chance(0.65f))
            {
                x = r.Sign() * r.Range(99f, 120f);
                z = c.Z(r.Range(0f, FloorZ + 20f));
            }
            else
            {
                x = r.Range(-110f, 110f);
                z = c.Z(FloorZ + r.Range(5f, 26f));
            }
        }

        /// <summary>A point in the low band alongside the floor, under the flank strips: only low things go here.</summary>
        private static void Beside(TerrainCanvas c, TerrainRandom r, out float x, out float z)
        {
            x = r.Sign() * r.Range(83f, 95f);
            z = c.Z(r.Range(1f, 80f));
        }

        /// <summary>A colour for something solid; glow is how much it shines by itself.</summary>
        private static Color32 S(int r, int g, int b, int glow = 0) => new Color32((byte)r, (byte)g, (byte)b, (byte)glow);

        private static Color32 Vary(TerrainRandom r, Color32 c, float amount = 0.1f)
        {
            float f = 1f + r.Range(-amount, amount);
            return new Color32((byte)Mathf.Min(255f, c.r * f), (byte)Mathf.Min(255f, c.g * f), (byte)Mathf.Min(255f, c.b * f), c.a);
        }

        private static Color32 Darker(Color32 c, float f) => new Color32((byte)(c.r * f), (byte)(c.g * f), (byte)(c.b * f), c.a);

        private static Color32 Flesh(TerrainRandom r)
        {
            float pick = r.Value();
            return Vary(r, pick < 0.4f ? Skin : pick < 0.75f ? SkinPink : SkinRed, 0.08f);
        }

        /// <summary>Where a point given in a thing's own coordinates (as placed by PlaceSolid at x, z, yaw) is in the world.</summary>
        private static Vector3 ToWorld(TerrainCanvas c, float x, float z, float yaw, Vector3 local)
        {
            float cs = Mathf.Cos(yaw * Mathf.Deg2Rad), sn = Mathf.Sin(yaw * Mathf.Deg2Rad);
            return new Vector3(x + local.x * cs + local.z * sn, c.GroundAt(x, z) + local.y, z - local.x * sn + local.z * cs);
        }

        /// <summary>Puts the solid mesh's next pieces in world coordinates.</summary>
        private static void PlaceWorld(TerrainCanvas c) => c.Solid.Place(Vector3.zero);

        /// <summary>
        /// A tube through local points whose section stays square to a fixed side direction (local), so that a blade never turns over however its path bends,
        /// as a tube's section can where its path passes upright. Flat-shaded. flatten squashes the section along side.
        /// </summary>
        private static void Blade(TerrainCanvas c, List<Vector3> points, List<float> radii, Vector3 side, float flatten, int sides, Color32 start, Color32 end)
        {
            int n = points.Count;
            var rings = new Vector3[n * sides];
            for (int i = 0; i < n; i++)
            {
                Vector3 along = (i == 0 ? points[1] - points[0] : i == n - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1]).normalized;
                Vector3 across = (side - along * Vector3.Dot(side, along)).normalized;
                Vector3 up = Vector3.Cross(along, across);
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    rings[i * sides + s] = points[i] + (across * (Mathf.Cos(a) * flatten) + up * Mathf.Sin(a)) * radii[i];
                }
            }
            for (int i = 0; i < n - 1; i++)
            {
                Color32 color = Color32.Lerp(start, end, (i + 0.5f) / (n - 1));
                for (int s = 0; s < sides; s++)
                {
                    int s2 = (s + 1) % sides;
                    c.Solid.Quad(rings[i * sides + s], rings[i * sides + s2], rings[(i + 1) * sides + s2], rings[(i + 1) * sides + s], color);
                }
            }
        }

        /// <summary>A dying lamp of the old ship: a soft light in the air that flickers and fades.</summary>
        private static void BrokenLight(TerrainCanvas c, TerrainRandom r, Vector3 at, Color32 color, float size)
        {
            c.Glow.Uv = new Vector2(r.Range(0.75f, 0.98f), r.Range(0f, 6.28f));
            c.Glow.Halo(at, size, new Color32(color.r, color.g, color.b, (byte)r.Range(44f, 70f)));
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>Low rails along the floor's sides, where the deck steps down; broken off in places and swallowed in others.</summary>
        private void Rails(TerrainCanvas c, TerrainRandom r)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = 2.2f; v < FloorZ - 1f; v += 4.3f)
                {
                    if (r.Chance(0.25f)) continue;
                    float x = k * 83.6f, z = c.Z(v);
                    if (MassAt(c, x, z) > 0.95f && r.Chance(0.4f)) continue;
                    if (!c.Stand(x, z, 2.05f, 1.1f, 0.15f)) continue;
                    c.PlaceSolid(x, z, 0f);
                    if (OnCovenant)
                    {
                        // A low curved barrier with a dim strip of light on its inner face.
                        c.Solid.Box(Vector3.zero, new Vector3(0.5f, 0.75f, 3.8f), Vary(r, Violet, 0.08f), 0.55f);
                        c.Solid.Box(new Vector3(-k * 0.19f, 0.42f, 0f), new Vector3(0.04f, 0.08f, 3.4f), r.Chance(0.6f) ? VioletLit : VioletDead);
                    }
                    else
                    {
                        for (int p = -1; p <= 1; p += 2)
                            c.Solid.Cylinder(new Vector3(0f, 0f, p * 1.9f), 0.07f, 0.07f, 1.05f, 6, Steel, false);
                        c.Solid.Box(new Vector3(0f, 0.97f, 0f), new Vector3(0.1f, 0.09f, 3.9f), r.Chance(0.5f) ? Hazard : SteelPale);
                        if (r.Chance(0.7f)) c.Solid.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.07f, 0.07f, 3.9f), Steel);
                    }
                    if (r.Chance(0.4f))
                        c.Solid.Lump(new Vector3(0f, 0.15f, r.Range(-1.2f, 1.2f)), new Vector3(0.75f, 0.55f, 1f), Flesh(r), 7, 0.2f, r.Int(100), SkinPale);
                }
            }
        }

        // ------------------------------------------------------------------ the room

        /// <summary>The room's walls: down both sides and across the far end, far enough out that the camera sees them over the surroundings.</summary>
        private void Walls(TerrainCanvas c, TerrainRandom r)
        {
            const float length = 20f, step = 20.6f;
            for (int k = -1; k <= 1; k += 2)
            {
                for (float v = step * 0.5f; v < FloorZ + 74f; v += step)
                    WallPanel(c, r, k * (WallX + r.Range(-1f, 1f)), c.Z(v), k > 0 ? 0f : 180f, length, r.Range(24f, 34f));
            }
            for (int i = -6; i <= 6; i++)
                WallPanel(c, r, i * step, c.Z(WallFarV + r.Range(-1f, 1f)), c.Side > 0 ? -90f : 90f, length, r.Range(24f, 34f));
        }

        /// <summary>One panel of a wall, its inner face towards local -x: the hull's own architecture, with flesh draped down it, drips and shelf fungus.</summary>
        private void WallPanel(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float length, float height)
        {
            if (!Tall(c, x, z, length * 0.5f + 0.2f, height, 0.3f)) return;
            c.PlaceSolid(x, z, yaw);
            float hl = length * 0.5f;
            float ledgeY, ledgeFront, faceX;
            if (OnCovenant)
            {
                // A battered purple wall, a curved rib rising from it, slits of dying violet light and a band low down.
                c.Solid.Box(new Vector3(1f, 0f, 0f), new Vector3(5f, height, length), Vary(r, S(92, 62, 124), 0.06f));
                c.Solid.Box(new Vector3(1.6f, height, 0f), new Vector3(3.8f, 2f, length), VioletDark, 0.6f);
                c.Solid.Box(new Vector3(-1.55f, 0f, 0f), new Vector3(0.6f, 1.4f, length), Violet);
                c.Solid.Box(new Vector3(-1.86f, 0.55f, 0f), new Vector3(0.04f, 0.3f, length * 0.86f), r.Chance(0.55f) ? VioletLit : VioletDead);
                c.Solid.Box(new Vector3(-1.2f, height * 0.46f, 0f), new Vector3(1f, 0.5f, length * 0.9f), VioletPale);
                // A rib leaning out over the yard from one end of the panel, if it can't hide the floor.
                if (r.Chance(0.45f)) WallRib(c, r, x, z, yaw, hl - 1.2f, height * r.Range(0.6f, 0.85f));
                for (int s = 0; s < 3; s++)
                {
                    float sz = -hl + 4f + s * (length - 8f) / 2f;
                    bool lit = r.Chance(0.5f);
                    c.Solid.Box(new Vector3(-1.56f, 2.4f, sz), new Vector3(0.12f, height * 0.32f, 0.5f), lit ? VioletLit : VioletDead);
                    if (lit && r.Chance(0.5f)) BrokenLight(c, r, ToWorld(c, x, z, yaw, new Vector3(-2.2f, 2.4f + height * 0.16f, sz)), Dying, 3.6f);
                }
                if (r.Chance(0.4f))
                {
                    Vector3 pool = ToWorld(c, x, z, yaw, new Vector3(-6f, 0f, r.Range(-hl + 3f, hl - 3f)));
                    LightPool(c, pool.x, pool.z, 7f, Dying, 0.5f, 0.85f);
                }
                ledgeY = height * 0.46f;
                ledgeFront = -1.6f;
                faceX = -1.5f;
            }
            else
            {
                // A steel bulkhead: slab, kick plate with a hazard band over it, pilasters, a ledge and an upper band, a vent, sometimes a lamp.
                c.Solid.Box(new Vector3(0.6f, 0f, 0f), new Vector3(3f, height, length), Vary(r, SteelDark, 0.06f));
                c.Solid.Box(new Vector3(-1.4f, 0f, 0f), new Vector3(1f, 2.6f, length), Steel);
                c.Solid.Box(new Vector3(-1.05f, 2.6f, 0f), new Vector3(0.3f, 0.9f, length), HazardDark);
                for (int s = 0; s < 6; s++)
                    c.Solid.Box(new Vector3(-1.22f, 2.66f, -hl + 1.6f + s * (length - 3.2f) / 5f), new Vector3(0.06f, 0.78f, 1.1f), Hazard);
                for (int p = -1; p <= 1; p++)
                    c.Solid.Box(new Vector3(-1.4f, 0f, p * (hl - 0.8f)), new Vector3(1.6f, height, 1.6f), Steel, 0.92f);
                c.Solid.Box(new Vector3(-1.2f, height * 0.42f, 0f), new Vector3(1.2f, 0.7f, length), SteelPale);
                c.Solid.Box(new Vector3(-1f, height * 0.78f, 0f), new Vector3(0.8f, 1.2f, length), Steel);
                c.Solid.Box(new Vector3(-1f, height * 0.5f, r.Range(-4f, 4f)), new Vector3(0.3f, 4f, 5f), S(54, 56, 60));
                if (r.Chance(0.6f))
                {
                    bool lit = r.Chance(0.75f);
                    float lz = r.Range(-hl + 3f, hl - 3f), ly = height * 0.62f;
                    c.Solid.Box(new Vector3(-1.1f, ly, lz), new Vector3(0.45f, 0.45f, 3.4f), lit ? ColdLamp : DeadLamp);
                    if (lit)
                    {
                        BrokenLight(c, r, ToWorld(c, x, z, yaw, new Vector3(-2f, ly + 0.2f, lz)), Cold, 5f);
                        Vector3 pool = ToWorld(c, x, z, yaw, new Vector3(-9f, 0f, lz));
                        LightPool(c, pool.x, pool.z, 9f, Cold, 0.45f, 0.9f);
                    }
                }
                ledgeY = height * 0.42f;
                ledgeFront = -1.7f;
                faceX = -2.2f;
            }
            // Flesh draped down the inner face to a fat swollen foot, drips from the ledge, and shelf fungus.
            if (r.Chance(0.62f))
            {
                float mz = r.Range(-hl + 4.5f, hl - 4.5f);
                Drape(c, r, faceX, mz, r.Range(height * 0.6f, height * 0.97f), r.Range(3f, 4.6f));
                if (r.Chance(0.3f)) Drape(c, r, faceX, mz + r.Sign() * r.Range(3f, 4.5f), r.Range(height * 0.3f, height * 0.6f), r.Range(1.4f, 2.2f));
            }
            int drips = r.Int(4);
            for (int i = 0; i < drips; i++)
                Drip(c, r, new Vector3(ledgeFront, ledgeY, r.Range(-hl + 1f, hl - 1f)), r.Range(1.2f, 4f), r.Range(0.14f, 0.26f));
            if (r.Chance(0.6f)) ShelfFungus(c, r, faceX + 0.1f, r.Range(-hl + 2f, hl - 2f), r.Range(2f, height * 0.5f));
            Tops.Add(ToWorld(c, x, z, yaw, new Vector3(faceX - 0.5f, height * r.Range(0.6f, 0.9f), r.Range(-hl, hl))));
        }

        /// <summary>A curved Covenant rib rising from a wall panel (placed at x, z, yaw) at local z, leaning out over the yard and narrowing; left out where it could hide the floor.</summary>
        private static void WallRib(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw, float rz, float rise)
        {
            Path.Clear();
            Radii.Clear();
            float angle = 0.3f, px = -1.2f, py = 0f;
            const int steps = 6;
            float stepLength = rise / steps * 1.1f;
            for (int i = 0; i <= steps; i++)
            {
                Path.Add(new Vector3(px, py, rz));
                Radii.Add(Mathf.Lerp(1.1f, 0.4f, i / (float)steps));
                px -= Mathf.Sin(angle) * stepLength;
                py += Mathf.Cos(angle) * stepLength;
                angle += 0.12f;
            }
            for (int i = 0; i < Path.Count; i++)
                if (!Fits(c, ToWorld(c, x, z, yaw, Path[i]), Radii[i])) return;
            Blade(c, Path, Radii, Vector3.forward, 0.5f, 5, Violet, VioletPale);
        }

        /// <summary>
        /// A sheet of flesh draped down a face at faceX (local; the face looks towards -x): several swollen folds hanging side by side and overlapping, so that the
        /// creases between them shade like a curtain of meat. Each fold has its own colour, from dark red-brown where it clings at the top to wet salmon and pale
        /// lower down; some reach the floor and swell into a fat foot of lumps, others stop short and taper into a hanging drip, so the lower edge is ragged.
        /// width is about half the sheet's width: the folds stay within about 1.5 width of faceZ and 1.6 m in front of the face, and the foot lumps rise about 0.6 width.
        /// </summary>
        private static void Drape(TerrainCanvas c, TerrainRandom r, float faceX, float faceZ, float top, float width)
        {
            int folds = Mathf.Min(5, 2 + (int)(width * 0.7f));
            float span = width * 1.6f, pitch = span / folds;
            float foldRadius = pitch * 0.8f;
            int middle = r.Int(folds);
            int feet = 0;
            const int steps = 5;
            for (int f = 0; f < folds; f++)
            {
                float fz = faceZ - span * 0.5f + pitch * (f + 0.5f) + r.Range(-0.12f, 0.12f) * pitch;
                float rad = foldRadius * r.Range(0.85f, 1.15f);
                float thick = r.Range(0.42f, 0.6f);
                // The middle folds hang lowest; the others stop short at different heights, so the hem is ragged.
                bool floor = f == middle || r.Chance(Mathf.Abs(f - middle) <= 1 ? 0.55f : 0.25f);
                float foldTop = top * r.Range(0.9f, 1.02f);
                float bottom = floor ? -0.25f : foldTop * r.Range(0.22f, 0.62f);
                float length = foldTop - bottom;
                if (length < 2.2f) bottom = Mathf.Max(-0.25f, foldTop - 2.2f);
                length = foldTop - bottom;
                float wob = r.Range(0f, 6.28f);
                Path.Clear();
                Radii.Clear();
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    // Slack at the bottom: it sags a little away from the face, never so steeply that the fold's section turns.
                    float sag = (floor ? 0.35f : 0.18f) * t * t;
                    float swell = 0.9f + 0.18f * Mathf.Sin(t * 5.5f + wob);
                    float taper = floor ? Mathf.Lerp(0.85f, 1.2f, t) : (i == steps ? 0.12f : i == steps - 1 ? 0.55f : Mathf.Lerp(0.85f, 1.05f, t));
                    float radius = rad * swell * taper;
                    Path.Add(new Vector3(faceX - rad * thick * 0.8f - 0.05f - sag, foldTop - length * t, fz + Mathf.Sin(t * 2.3f + wob) * pitch * 0.18f));
                    Radii.Add(radius);
                }
                // Light and dark folds by turns, each with its own shade: the creases between them read.
                float pick = r.Value();
                Color32 lower = (f + middle) % 2 == 0 ? Vary(r, pick < 0.5f ? SkinPink : Skin, 0.08f) : Vary(r, pick < 0.5f ? SkinPale : S(196, 134, 118), 0.06f);
                Color32 upper = Color32.Lerp(lower, r.Chance(0.5f) ? SkinRed : SkinDark, r.Range(0.35f, 0.55f));
                c.Solid.Tube(Path, Radii, 6, upper, lower, false, thick);
                if (!floor)
                {
                    // A wet bead at the end of the drip.
                    Vector3 tip = Path[steps];
                    float bead = rad * r.Range(0.2f, 0.3f);
                    c.Solid.Lump(tip + Vector3.down * bead * 0.4f, new Vector3(bead, bead * 1.3f, bead), SkinPale, 5);
                }
                else if (feet < 3)
                {
                    feet++;
                    float s = width * r.Range(0.4f, 0.6f);
                    var at = new Vector3(faceX - 0.4f - s * 0.45f, s * 0.22f, fz + r.Range(-0.3f, 0.3f) * pitch);
                    c.Solid.Lump(at, new Vector3(s * 0.9f, s * 0.65f, s * 1.1f), Flesh(r), 7, 0.22f, r.Int(100), SkinPale);
                }
                // Now and then a swollen blister on a fold.
                if (r.Chance(0.35f))
                {
                    int at = 1 + r.Int(steps - 2);
                    float b = rad * r.Range(0.35f, 0.55f);
                    Vector3 p = Path[at];
                    c.Solid.Lump(new Vector3(p.x - Radii[at] * thick * 0.6f, p.y, p.z + r.Range(-0.4f, 0.4f) * Radii[at]), new Vector3(b * 0.75f, b, b), SkinPink, 6, 0.15f, r.Int(100), SkinPale);
                }
            }
        }

        /// <summary>A drip of mass hanging from a point (local), stirring slowly, with a bead at its end.</summary>
        private static void Drip(TerrainCanvas c, TerrainRandom r, Vector3 from, float length, float radius)
        {
            DripPath.Clear();
            DripRadii.Clear();
            float bend = r.Range(-0.2f, 0.2f);
            for (int i = 0; i <= 3; i++)
            {
                float t = i / 3f;
                DripPath.Add(from + new Vector3(bend * t * t, -length * t, bend * 0.5f * t));
                DripRadii.Add(radius * Mathf.Lerp(1f, 0.4f, t));
            }
            c.Solid.Uv = new Vector2(0.04f, r.Range(0f, 6.28f));
            c.Solid.Tube(DripPath, DripRadii, 4, SkinDark, SkinPink);
            c.Solid.Lump(DripPath[3] + Vector3.down * radius * 0.3f, new Vector3(radius * 0.75f, radius, radius * 0.75f), SkinPale, 5);
            c.Solid.Uv = Vector2.zero;
        }

        /// <summary>Brackets of shelf fungus up a face at faceX: flat, brown above, pale and faintly glowing underneath.</summary>
        private static void ShelfFungus(TerrainCanvas c, TerrainRandom r, float faceX, float faceZ, float y0)
        {
            int count = 2 + r.Int(3);
            for (int i = 0; i < count; i++)
            {
                float w = r.Range(1f, 2f);
                var at = new Vector3(faceX - w * 0.45f, y0 + i * r.Range(0.6f, 1.1f), faceZ + r.Range(-1.6f, 1.6f));
                c.Solid.Lump(at, new Vector3(w, w * 0.16f, w * 0.85f), ShelfUnder, 6, 0.12f, r.Int(100), ShelfTop);
            }
        }

        /// <summary>A great tendril from high on a wall, arching out over the yard and burrowing into its floor with a swollen knot, a drip or two hanging from it.</summary>
        private void WallTendril(TerrainCanvas c, TerrainRandom r, Vector3 from, Vector3 to)
        {
            if (!MayStand(c, to.x, to.z, 2.6f, 3f)) return;
            Vector3 mid = (from + to) * 0.5f + Vector3.up * r.Range(3f, 7f);
            Path.Clear();
            Radii.Clear();
            const int steps = 11;
            float thick = r.Range(0.9f, 1.4f), phase = r.Range(0f, 6.28f);
            Vector3 side = Vector3.Cross(to - from, Vector3.up).normalized;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = (1 - t) * (1 - t) * from + 2f * (1 - t) * t * mid + t * t * to;
                p += side * Mathf.Sin(t * 7f + phase) * 0.6f;
                Path.Add(p);
                Radii.Add(thick * Mathf.Lerp(1f, 0.55f, t) * (0.85f + 0.3f * Mathf.Sin(t * 13f + phase)));
            }
            if (!PathFits(c, Path, Radii)) return;
            c.Stand(to.x, to.z, 2.6f, 3f, 0.3f);
            PlaceWorld(c);
            c.Solid.Tube(Path, Radii, 7, RootColor, TipColor);
            Vector3 low = Path[steps - 2];
            c.Solid.Lump(new Vector3(to.x, thick * 0.5f, to.z), new Vector3(thick * 2f, thick * 1.2f, thick * 2f), Flesh(r), 8, 0.25f, r.Int(100), SkinPale);
            int drips = 1 + r.Int(2);
            for (int i = 0; i < drips; i++)
            {
                Vector3 at = Path[3 + r.Int(5)];
                if (at.y < 4f) continue;
                Drip(c, r, at + Vector3.down * Radii[3] * 0.6f, r.Range(1.5f, Mathf.Min(5f, at.y - 1.5f)), r.Range(0.18f, 0.3f));
            }
            Tops.Add(low);
        }

        /// <summary>A tendril hung between two high points (world), sagging between them, with drips from its lowest part.</summary>
        private void HangingTendril(TerrainCanvas c, TerrainRandom r, Vector3 a, Vector3 b)
        {
            float span = Vector3.Distance(a, b);
            if (span < 6f || span > 46f) return;
            float sag = span * r.Range(0.18f, 0.32f);
            Path.Clear();
            Radii.Clear();
            const int steps = 9;
            float thick = r.Range(0.35f, 0.7f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
                Path.Add(p);
                Radii.Add(thick * (1.15f - 0.5f * Mathf.Sin(t * Mathf.PI)));
            }
            if (Path[steps / 2].y < 2.5f || !PathFits(c, Path, Radii)) return;
            PlaceWorld(c);
            c.Solid.Uv = new Vector2(0.06f, r.Range(0f, 6.28f));
            c.Solid.Tube(Path, Radii, 5, RootColor, SkinPink);
            c.Solid.Uv = Vector2.zero;
            Vector3 low = Path[steps / 2];
            if (low.y > 4f) Drip(c, r, low + Vector3.down * thick * 0.6f, r.Range(1f, Mathf.Min(4f, low.y - 2f)), thick * 0.5f);
        }

        // ------------------------------------------------------------------ what the Flood has left of a UNSC ship

        private void UnscWreckage(TerrainCanvas c, TerrainRandom r)
        {
            Crane(c, r);
            for (int i = 0; i < 6; i++)
            {
                Near(c, r, out float x, out float z);
                BulkheadPillar(c, r, x, z);
            }
            for (int i = 0; i < 8; i++)
            {
                Near(c, r, out float x, out float z);
                ContainerStack(c, r, x, z);
            }
            for (int i = 0; i < 2; i++)
            {
                Around(c, r, out float x, out float z);
                Gantry(c, r, x, z);
            }
            for (int i = 0; i < 5; i++)
            {
                Around(c, r, out float x, out float z);
                ToppledPillar(c, r, x, z, false);
            }
            for (int i = 0; i < 8; i++)
            {
                Around(c, r, out float x, out float z);
                BurstCrate(c, r, x, z, false);
            }
            for (int i = 0; i < 6; i++)
            {
                Beside(c, r, out float x, out float z);
                BurstCrate(c, r, x, z, true);
            }
        }

        /// <summary>A great gantry crane across the space beyond the far end: two braced legs, a bridge with hazard stripes, a trolley with its hook hanging,
        /// tendrils hanging from the bridge and mass climbing the legs.</summary>
        private void Crane(TerrainCanvas c, TerrainRandom r)
        {
            float height = r.Range(17f, 21f), span = r.Range(48f, 60f);
            float cx = r.Range(-30f, 30f), v = FloorZ + r.Range(26f, 34f), z = c.Z(v);
            float half = span * 0.5f;
            // The legs, and the space under the bridge, must be clear.
            for (int k = -1; k <= 1; k += 2)
                if (!c.IsClear(cx + k * half, z, 3.2f, height + 3f)) return;
            for (int k = -1; k <= 1; k += 2) c.Stand(cx + k * half, z, 3.2f, height + 3f, 0.4f);
            for (float s = -half + 8f; s < half - 4f; s += 8f) c.Stand(cx + s, z, 3.5f, height + 3f, 0f);
            c.PlaceSolid(cx, z, 0f);
            for (int k = -1; k <= 1; k += 2)
            {
                float lx = k * half;
                for (int p = -1; p <= 1; p += 2)
                    c.Solid.Box(new Vector3(lx, 0f, p * 1.3f), new Vector3(1.1f, height, 1.1f), Steel);
                c.Solid.Box(new Vector3(lx, 0f, 0f), new Vector3(2.6f, 0.8f, 4.6f), SteelDark);
                // Cross bracing between the two posts of a leg.
                for (int b = 0; b < 3; b++)
                {
                    float y0 = 1.5f + b * (height - 3f) / 3f, y1 = y0 + (height - 3f) / 3f;
                    Path.Clear();
                    Radii.Clear();
                    Path.Add(new Vector3(lx, y0, b % 2 == 0 ? -1.3f : 1.3f));
                    Path.Add(new Vector3(lx, y1, b % 2 == 0 ? 1.3f : -1.3f));
                    Radii.Add(0.16f);
                    Radii.Add(0.16f);
                    c.Solid.Tube(Path, Radii, 4, SteelDark, SteelDark);
                }
                for (int b = 0; b < 4; b++)
                    c.Solid.Box(new Vector3(lx, 0.9f + b * 0.55f, -1.95f), new Vector3(1.2f, 0.28f, 0.06f), b % 2 == 0 ? Hazard : HazardDark);
                // Mass climbing the leg.
                Drape(c, r, lx - 0.6f, 0f, height * r.Range(0.4f, 0.75f), r.Range(1.4f, 2.2f));
            }
            // The bridge, with a band of hazard stripes along its face towards the field.
            float facing = -c.Side;
            c.Solid.Box(new Vector3(0f, height, 0f), new Vector3(span + 2f, 2.4f, 3.6f), Steel);
            c.Solid.Box(new Vector3(0f, height + 2.4f, 0f), new Vector3(span + 2f, 0.4f, 2.6f), SteelPale);
            for (float s = -half + 1f; s < half - 1f; s += 2.4f)
                c.Solid.Box(new Vector3(s, height + 0.7f, facing * 1.82f), new Vector3(1.2f, 1f, 0.06f), Hazard);
            // The trolley, its cable and the hook.
            float tx = r.Range(-half * 0.6f, half * 0.6f);
            c.Solid.Box(new Vector3(tx, height - 1.6f, 0f), new Vector3(3.4f, 1.6f, 3f), SteelDark);
            float drop = r.Range(height * 0.35f, height * 0.6f);
            Path.Clear();
            Radii.Clear();
            Path.Add(new Vector3(tx, height - 1.6f, 0f));
            Path.Add(new Vector3(tx, height - 1.6f - drop, 0f));
            Radii.Add(0.07f);
            Radii.Add(0.07f);
            c.Solid.Tube(Path, Radii, 4, SteelDark, SteelDark);
            float hy = height - 1.6f - drop;
            c.Solid.Box(new Vector3(tx, hy - 1.2f, 0f), new Vector3(1f, 1.2f, 0.8f), Hazard);
            Path.Clear();
            Radii.Clear();
            for (int i = 0; i <= 5; i++)
            {
                float a = i / 5f * 4f;
                Path.Add(new Vector3(tx + Mathf.Sin(a) * 0.7f, hy - 1.6f - (1f - Mathf.Cos(a)) * 0.7f, 0f));
                Radii.Add(0.16f);
            }
            c.Solid.Tube(Path, Radii, 4, SteelPale, SteelPale);
            // Tendrils hanging from the bridge, and a few draped over it.
            int hanging = 4 + r.Int(4);
            for (int i = 0; i < hanging; i++)
            {
                float hx = r.Range(-half + 2f, half - 2f);
                float length = r.Range(height * 0.25f, height * 0.7f);
                Path.Clear();
                Radii.Clear();
                for (int p = 0; p <= 4; p++)
                {
                    float t = p / 4f;
                    Path.Add(new Vector3(hx + Mathf.Sin(t * 3f + i) * 0.4f * t, height - t * length, Mathf.Cos(t * 2.4f + i) * 0.4f * t));
                    Radii.Add(Mathf.Lerp(0.45f, 0.1f, t));
                }
                c.Solid.Uv = new Vector2(0.1f, r.Range(0f, 6.28f));
                c.Solid.Tube(Path, Radii, 5, RootColor, TipColor);
                c.Solid.Uv = Vector2.zero;
            }
            for (int i = 0; i < 3; i++)
                Cord(c, r, r.Range(-half + 3f, half - 3f), height + 2.8f, 1.8f, 2.8f, r.Range(0.35f, 0.6f));
            for (int i = 0; i < 2; i++)
                c.Solid.Lump(new Vector3(r.Range(-half * 0.7f, half * 0.7f), height + 2.6f, 0f), new Vector3(r.Range(1.6f, 2.6f), 1f, 1.6f), Flesh(r), 8, 0.25f, r.Int(100), SkinPale);
            // A dying work light on the bridge.
            Vector3 lamp = ToWorld(c, cx, z, 0f, new Vector3(r.Range(-half * 0.8f, half * 0.8f), height - 0.4f, facing * 2f));
            c.Glow.Uv = new Vector2(0.9f, r.Range(0f, 6.28f));
            c.Glow.Halo(lamp, 4f, new Color32(Cold.r, Cold.g, Cold.b, 60));
            c.Glow.Uv = Vector2.zero;
            LightPool(c, lamp.x, lamp.z - facing * 4f, 9f, Cold, 0.45f, 0.9f);
            Tops.Add(ToWorld(c, cx, z, 0f, new Vector3(-half, height + 1f, 0f)));
            Tops.Add(ToWorld(c, cx, z, 0f, new Vector3(half, height + 1f, 0f)));
        }

        /// <summary>A cord of mass draped over a thing at local x, from the floor on one side up over its top (height top, half its depth halfDepth) and down the other side.</summary>
        private static void Cord(TerrainCanvas c, TerrainRandom r, float x, float top, float halfDepth, float spread, float radius)
        {
            Path.Clear();
            Radii.Clear();
            float wob = r.Range(-0.6f, 0.6f);
            // Always running towards +z, so the tube never turns over.
            Path.Add(new Vector3(x + wob, -0.2f, -halfDepth - spread));
            Path.Add(new Vector3(x + wob * 0.6f, top * 0.45f, -halfDepth - radius * 0.6f));
            Path.Add(new Vector3(x + wob * 0.3f, top + radius * 0.6f, -halfDepth * 0.6f));
            Path.Add(new Vector3(x, top + radius * 0.8f, 0f));
            Path.Add(new Vector3(x - wob * 0.3f, top + radius * 0.6f, halfDepth * 0.6f));
            Path.Add(new Vector3(x - wob * 0.6f, top * 0.45f, halfDepth + radius * 0.6f));
            Path.Add(new Vector3(x - wob, -0.2f, halfDepth + spread));
            for (int i = 0; i < Path.Count; i++) Radii.Add(radius * (i == 0 || i == Path.Count - 1 ? 1.6f : 1f));
            c.Solid.Tube(Path, Radii, 5, RootColor, Skin);
        }

        /// <summary>A UNSC pillar: a square steel column on a plinth, banded with hazard paint near its foot, a bracket at its top or broken off,
        /// with flesh draped down one face and a tendril coiled round it.</summary>
        private void BulkheadPillar(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            float height = r.Range(9f, 18f);
            // The plinth and coil reach 2.6 m, the flesh at its foot 4 m.
            if (!Tall(c, x, z, 4.1f, height + 1.6f, 0.45f)) return;
            float yaw = r.Range(0f, 90f);
            c.PlaceSolid(x, z, yaw);
            c.Solid.Box(Vector3.zero, new Vector3(3.6f, 1f, 3.6f), SteelDark, 0.9f);
            c.Solid.Box(Vector3.zero, new Vector3(2.2f, height, 2.2f), Vary(r, Steel, 0.06f));
            for (int b = 0; b < 5; b++)
                c.Solid.Box(new Vector3(0f, 1.2f + b * 0.5f, 0f), new Vector3(2.26f, 0.5f, 2.26f), b % 2 == 0 ? Hazard : HazardDark);
            c.Solid.Box(new Vector3(0f, height * 0.62f, 0f), new Vector3(2.5f, 0.5f, 2.5f), SteelPale);
            if (r.Chance(0.55f)) c.Solid.Box(new Vector3(0f, height, 0f), new Vector3(3.4f, 1f, 3.4f), SteelDark, 1.1f);
            else c.Solid.Shard(new Vector3(0f, height - 0.1f, 0f), 1.1f, r.Range(0.8f, 1.6f), new Vector3(r.Range(-0.3f, 0.3f), 0f, r.Range(-0.3f, 0.3f)), 4, Steel, SteelPale, 0.785f);
            Drape(c, r, -1.1f, 0f, height * r.Range(0.55f, 0.95f), r.Range(1.3f, 1.9f));
            Path.Clear();
            Radii.Clear();
            float turns = r.Range(7f, 11f), phase = r.Range(0f, 6.28f);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float a = phase + t * turns;
                Path.Add(new Vector3(Mathf.Cos(a) * 1.5f, t * height * 0.8f, Mathf.Sin(a) * 1.5f));
                Radii.Add(Mathf.Lerp(0.36f, 0.1f, t));
            }
            c.Solid.Tube(Path, Radii, 4, RootColor, TipColor);
            if (r.Chance(0.35f))
            {
                Vector3 lamp = ToWorld(c, x, z, yaw, new Vector3(-1.3f, height * 0.75f, 0f));
                BrokenLight(c, r, lamp, Cold, 3.5f);
                LightPool(c, lamp.x, lamp.z, 6f, Cold, 0.4f, 0.9f);
            }
            Tops.Add(ToWorld(c, x, z, yaw, new Vector3(0f, height * 0.9f, 0f)));
        }

        /// <summary>A stack of containers in faded paints, half swallowed: flesh heaped round their feet and tendrils draped over them. Plain colours: no markings.</summary>
        private static void ContainerStack(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            int high = 1 + r.Int(2);
            // Boxes reach 6.9 m from the middle, lumps 8.5 m; cords rise a metre over the top box.
            if (!Tall(c, x, z, 8.8f, high * 2.9f + 1.4f, 0.55f)) return;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            Color32[] paints = { S(92, 98, 104), S(84, 94, 84), S(112, 92, 62), S(104, 70, 56), S(72, 84, 104) };
            for (int h = 0; h < high; h++)
            {
                Color32 paint = Vary(r, paints[r.Int(paints.Length)], 0.08f);
                var foot = new Vector3(h == 0 ? 0f : r.Range(-0.6f, 0.6f), h * 2.9f, h == 0 ? 0f : r.Range(-0.3f, 0.3f));
                c.Solid.Box(foot, new Vector3(12.2f, 2.9f, 2.9f), paint);
                c.Solid.Box(foot + new Vector3(0f, 0.25f, 0f), new Vector3(11.2f, 2.4f, 2.96f), Darker(paint, 0.72f));
                if (r.Chance(0.5f)) c.Solid.Box(foot + new Vector3(5.6f, 0.2f, 0f), new Vector3(0.4f, 2.5f, 3.02f), Hazard);
            }
            float top = high * 2.9f;
            int cords = 2 + r.Int(2);
            for (int i = 0; i < cords; i++) Cord(c, r, r.Range(-5f, 5f), top, 1.45f, r.Range(1.2f, 2.4f), r.Range(0.3f, 0.55f));
            // Swallowed at one end: flesh heaped against it.
            float end = r.Sign() * r.Range(4f, 6f);
            int lumps = 2 + r.Int(2);
            for (int i = 0; i < lumps; i++)
            {
                float s = r.Range(1.4f, 2.4f);
                c.Solid.Lump(new Vector3(end + r.Range(-1.5f, 1.5f), s * 0.25f + (i == 0 ? 0f : r.Range(0f, top * 0.6f)), r.Range(-1.8f, 1.8f)), new Vector3(s, s * 0.8f, s), Flesh(r), 8, 0.25f, r.Int(100), SkinPale);
            }
            if (r.Chance(0.5f)) Drip(c, r, new Vector3(r.Range(-4f, 4f), top - 0.2f, 1.6f), r.Range(1f, top * 0.6f), 0.2f);
        }

        /// <summary>A gantry: two legs and a beam with a hazard stripe, braced, with tendrils hanging from it and mass at its feet.</summary>
        private void Gantry(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            float span = r.Range(14f, 20f), height = r.Range(10f, 15f);
            if (!Tall(c, x, z, span * 0.5f + 3.2f, height + 2.5f, 0.45f)) return;
            float yaw = r.Range(0f, 360f);
            c.PlaceSolid(x, z, yaw);
            float half = span * 0.5f;
            for (int k = -1; k <= 1; k += 2)
            {
                c.Solid.Box(new Vector3(k * half, 0f, 0f), new Vector3(1f, height, 1.4f), Steel);
                c.Solid.Box(new Vector3(k * half, 0f, 0f), new Vector3(1.8f, 0.6f, 2.2f), SteelDark);
                Path.Clear();
                Radii.Clear();
                Path.Add(new Vector3(k * half, height - 3.5f, 0f));
                Path.Add(new Vector3(k * (half - 3.5f), height, 0f));
                Radii.Add(0.18f);
                Radii.Add(0.18f);
                c.Solid.Tube(Path, Radii, 6, SteelDark, SteelDark);
                float s = r.Range(1.2f, 1.8f);
                c.Solid.Lump(new Vector3(k * half + r.Range(-0.8f, 0.8f), s * 0.3f, r.Range(-0.8f, 0.8f)), new Vector3(s, s * 0.9f, s), Flesh(r), 8, 0.25f, r.Int(100), SkinPale);
            }
            c.Solid.Box(new Vector3(0f, height, 0f), new Vector3(span + 1.2f, 1.2f, 1.6f), Steel);
            for (int s = 0; s < 5; s++)
                c.Solid.Box(new Vector3(-half + 1.5f + s * (span - 3f) / 4f, height + 0.25f, -0.82f), new Vector3(1.2f, 0.7f, 0.06f), Hazard);
            // Tendrils hanging from the beam, stirring.
            int hanging = 2 + r.Int(3);
            for (int i = 0; i < hanging; i++)
            {
                float hx = r.Range(-half + 1.5f, half - 1.5f);
                float drop = r.Range(height * 0.35f, height * 0.8f);
                Path.Clear();
                Radii.Clear();
                for (int p = 0; p <= 4; p++)
                {
                    float t = p / 4f;
                    Path.Add(new Vector3(hx + Mathf.Sin(t * 3f + i) * 0.5f * t, height - t * drop, Mathf.Cos(t * 2.4f + i) * 0.4f * t));
                    Radii.Add(Mathf.Lerp(0.42f, 0.08f, t));
                }
                c.Solid.Uv = new Vector2(0.12f, r.Range(0f, 6.28f));
                c.Solid.Tube(Path, Radii, 5, RootColor, TipColor);
                c.Solid.Uv = Vector2.zero;
            }
            Tops.Add(ToWorld(c, x, z, yaw, new Vector3(0f, height + 0.6f, 0f)));
        }

        /// <summary>A pillar lying where it fell, with bands near its ends showing through the mass on it: hazard yellow on a UNSC ship, inlay and dim light on a Covenant one.</summary>
        private static void ToppledPillar(TerrainCanvas c, TerrainRandom r, float x, float z, bool covenant)
        {
            float length = r.Range(8f, 14f), radius = r.Range(0.9f, 1.4f);
            float sink = radius * 0.25f;
            if (!Tall(c, x, z, length * 0.5f + 1.5f, radius * 2f - sink + 1.6f, 0.4f)) return;
            c.PlaceSolid(x, z, r.Range(0f, 360f), 1f, sink);
            Color32 body = covenant ? Vary(r, Violet, 0.08f) : Vary(r, Steel, 0.08f);
            for (int k = -1; k <= 1; k += 2)
            {
                Path.Clear();
                Radii.Clear();
                Path.Add(new Vector3(0f, radius, 0f));
                Path.Add(new Vector3(k * length * 0.5f, radius, 0f));
                Radii.Add(radius);
                Radii.Add(radius);
                c.Solid.Tube(Path, Radii, 8, body, body, true);
                for (int b = 0; b < 3; b++)
                {
                    float at = k * (length * 0.5f - 0.6f - b * 0.55f);
                    Path.Clear();
                    Radii.Clear();
                    Path.Add(new Vector3(at - 0.22f, radius, 0f));
                    Path.Add(new Vector3(at + 0.22f, radius, 0f));
                    Radii.Add(radius + 0.04f);
                    Radii.Add(radius + 0.04f);
                    Color32 band = covenant ? (b == 1 ? (r.Chance(0.5f) ? VioletLit : VioletDead) : VioletPale) : (b == 1 ? HazardDark : Hazard);
                    c.Solid.Tube(Path, Radii, 8, band, band);
                }
            }
            // Tendrils draped over it, across its length.
            int cords = 1 + r.Int(2);
            for (int i = 0; i < cords; i++)
            {
                Path.Clear();
                Radii.Clear();
                float cx = r.Range(-length * 0.3f, length * 0.3f), cr = r.Range(0.25f, 0.45f);
                for (int p = 0; p <= 6; p++)
                {
                    float a = Mathf.PI * (1f - p / 6f);
                    float reach = radius + cr * 0.6f;
                    float y = radius + Mathf.Sin(a) * reach;
                    float zz = Mathf.Cos(a) * reach * (p == 0 || p == 6 ? 1.5f : 1f);
                    Path.Add(new Vector3(cx + r.Range(-0.2f, 0.2f), p == 0 || p == 6 ? -0.1f : y, -zz));
                    Radii.Add(cr);
                }
                c.Solid.Tube(Path, Radii, 5, RootColor, Skin);
            }
        }

        /// <summary>A crate burst open from inside: walls without a lid, the lid knocked down beside it, mass swelling out of it. Within 1.45 size of its middle, 1.25 size high.</summary>
        private void BurstCrate(TerrainCanvas c, TerrainRandom r, float x, float z, bool low)
        {
            float s = low ? r.Range(0.8f, 1.05f) : r.Range(1.4f, 2.2f);
            if (!Tall(c, x, z, s * 1.45f, s * 1.25f, low ? 0.25f : 0.4f)) return;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            Color32 paint = OnCovenant ? Vary(r, Violet, 0.08f) : Vary(r, S(92, 100, 86), 0.1f);
            Color32 inner = Darker(paint, 0.7f);
            float h = s * 0.9f, t = s * 0.08f, half = s * 0.5f;
            c.Solid.Box(new Vector3(0f, 0f, -half + t * 0.5f), new Vector3(s, h, t), paint);
            c.Solid.Box(new Vector3(0f, 0f, half - t * 0.5f), new Vector3(s, h * r.Range(0.6f, 1f), t), paint);
            c.Solid.Box(new Vector3(-half + t * 0.5f, 0f, 0f), new Vector3(t, h, s - 2f * t), inner);
            if (r.Chance(0.6f)) c.Solid.Box(new Vector3(half - t * 0.5f, 0f, 0f), new Vector3(t, h * r.Range(0.5f, 1f), s - 2f * t), inner);
            else c.Solid.Box(new Vector3(half + s * 0.35f, 0f, r.Range(-0.1f, 0.1f) * s), new Vector3(h, t, s * 0.9f), inner);
            c.Solid.Box(new Vector3(-s * 0.85f, 0f, r.Range(-0.1f, 0.1f) * s), new Vector3(s * 0.95f, t, s * 0.95f), paint);
            if (!OnCovenant && r.Chance(0.5f)) c.Solid.Box(new Vector3(0f, h * 0.55f, -half - 0.01f), new Vector3(s * 0.8f, h * 0.14f, t), Hazard);
            c.Solid.Lump(new Vector3(0f, s * 0.75f, 0f), new Vector3(s * 0.46f, s * 0.4f, s * 0.46f), SkinPink, 8, 0.2f, r.Int(100), SkinPale);
        }

        // ------------------------------------------------------------------ what the Flood has left of a Covenant ship

        private void CovenantWreckage(TerrainCanvas c, TerrainRandom r)
        {
            // Great curved ribs down the sides, leaning in over the space beyond the flank strips.
            for (int k = -1; k <= 1; k += 2)
            {
                for (int i = 0; i < 3; i++)
                    CurvedRib(c, r, k * r.Range(122f, 132f), c.Z(r.Range(6f, 120f)), k > 0 ? 0f : 180f);
            }
            // Overgrown arches: beyond the far end, facing the field, and along the sides.
            for (int i = 0; i < 2; i++)
                OvergrownArch(c, r, r.Range(-70f, 70f), c.Z(FloorZ + r.Range(26f, 38f)), 0f);
            for (int i = 0; i < 2; i++)
                OvergrownArch(c, r, r.Sign() * r.Range(112f, 126f), c.Z(r.Range(10f, 100f)), 90f);
            for (int i = 0; i < 7; i++)
            {
                Near(c, r, out float x, out float z);
                BulgePillar(c, r, x, z);
            }
            for (int i = 0; i < 5; i++)
            {
                Around(c, r, out float x, out float z);
                ToppledPillar(c, r, x, z, true);
            }
            for (int i = 0; i < 8; i++)
            {
                Around(c, r, out float x, out float z);
                CovenantCrates(c, r, x, z, false);
            }
            for (int i = 0; i < 6; i++)
            {
                Beside(c, r, out float x, out float z);
                if (r.Chance(0.5f)) CovenantCrates(c, r, x, z, true);
                else BurstCrate(c, r, x, z, true);
            }
        }

        /// <summary>A great curved rib of the hull: a blade rising from local +x and leaning over towards local -x, with dying light down both faces and mass on it.</summary>
        private void CurvedRib(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            float height = r.Range(18f, 26f);
            Path.Clear();
            Radii.Clear();
            const int steps = 9;
            float angle = 0.2f, px = 6f, py = -0.3f, stepLength = height / steps * 1.12f;
            for (int i = 0; i <= steps; i++)
            {
                Path.Add(new Vector3(px, py, 0f));
                Radii.Add(Mathf.Lerp(1.8f, 0.6f, i / (float)steps));
                px -= Mathf.Sin(angle) * stepLength;
                py += Mathf.Cos(angle) * stepLength;
                angle += 0.12f;
            }
            // Only its foot takes up ground (it may lean over a wall's foot or a smaller thing); the rest of it must keep clear of the floor and the flank strips.
            for (int i = 0; i < Path.Count; i++)
                if (!Fits(c, ToWorld(c, x, z, yaw, Path[i]), Radii[i])) return;
            if (!Tall(c, x + (yaw == 0f ? 6f : -6f), z, 3f, height, 0.4f)) return;
            c.PlaceSolid(x, z, yaw);
            Blade(c, Path, Radii, Vector3.forward, 0.42f, 6, Vary(r, VioletDark, 0.06f), Vary(r, Violet, 0.06f));
            bool lit = r.Chance(0.6f);
            Color32 strip = lit ? VioletLit : VioletDead;
            for (int side = -1; side <= 1; side += 2)
            {
                var face = new List<Vector3>(Path.Count);
                var faceRadii = new List<float>(Path.Count);
                for (int i = 0; i < Path.Count; i++)
                {
                    face.Add(Path[i] + new Vector3(0f, 0f, side * (Radii[i] * 0.42f + 0.05f)));
                    faceRadii.Add(0.1f);
                }
                Blade(c, face, faceRadii, Vector3.forward, 1f, 4, strip, strip);
            }
            if (lit && r.Chance(0.6f)) BrokenLight(c, r, ToWorld(c, x, z, yaw, Path[steps / 2] + new Vector3(-1.5f, 0f, 0f)), Dying, 3.4f);
            // Flesh heaped at its foot, and drips from its overhang.
            float s = r.Range(1.8f, 2.6f);
            c.Solid.Lump(new Vector3(6f, s * 0.3f, 0f), new Vector3(s, s * 0.85f, s), Flesh(r), 8, 0.25f, r.Int(100), SkinPale);
            int drips = 1 + r.Int(3);
            for (int i = 0; i < drips; i++)
            {
                Vector3 at = Path[4 + r.Int(steps - 4)];
                Drip(c, r, at + Vector3.down * 0.5f, r.Range(1.5f, Mathf.Min(6f, at.y - 2f)), r.Range(0.2f, 0.32f));
            }
            Tops.Add(ToWorld(c, x, z, yaw, Path[steps - 1]));
        }

        /// <summary>A pointed Covenant arch, two curved blades meeting overhead, a slit of dim violet light up each; overgrown, with a sheet of flesh hanging from
        /// its point and tendrils dripping from it. It spans along local x.</summary>
        private void OvergrownArch(TerrainCanvas c, TerrainRandom r, float x, float z, float yaw)
        {
            float half = r.Range(7f, 9f), height = r.Range(15f, 20f);
            float reach = yaw == 0f ? half + 2f : 2.5f;
            float across = yaw == 0f ? 2.5f : half + 2f;
            // Its footprint as a circle round it, then the south lean for its real extent.
            if (!MayStand(c, x, z, Mathf.Max(reach, across), height + 1f)) return;
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 foot = ToWorld(c, x, z, yaw, new Vector3(k * half, 0f, 0f));
                Vector3 apex = ToWorld(c, x, z, yaw, new Vector3(0f, height, 0f));
                if (!Fits(c, foot, 1.8f) || !Fits(c, apex, 1.8f) || !Fits(c, (foot + apex) * 0.5f + Vector3.up * 2f, 1.8f)) return;
            }
            c.Stand(x, z, Mathf.Max(reach, across), height + 1f, 0.4f);
            c.PlaceSolid(x, z, yaw);
            for (int k = -1; k <= 1; k += 2)
            {
                Path.Clear();
                Radii.Clear();
                const int steps = 7;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    // A bulging curve from the foot to the point: steep at first, leaning in towards the top.
                    float px = k * half * (1f - Mathf.Pow(t, 1.6f));
                    float py = height * Mathf.Sin(t * 1.5708f);
                    Path.Add(new Vector3(px, py - 0.2f, 0f));
                    Radii.Add(Mathf.Lerp(1.6f, 0.7f, t));
                }
                Blade(c, Path, Radii, Vector3.forward, 0.5f, 6, Vary(r, VioletDark, 0.06f), Vary(r, Violet, 0.06f));
                Color32 slitColor = r.Chance(0.65f) ? VioletLit : VioletDead;
                for (int face = -1; face <= 1; face += 2)
                {
                    var slit = new List<Vector3>(Path.Count);
                    var slitRadii = new List<float>(Path.Count);
                    for (int i = 0; i < Path.Count - 1; i++)
                    {
                        slit.Add(Path[i] + new Vector3(0f, 0f, face * (Radii[i] * 0.5f + 0.04f)));
                        slitRadii.Add(0.12f);
                    }
                    Blade(c, slit, slitRadii, Vector3.forward, 1f, 4, slitColor, VioletDead);
                }
                float s = r.Range(1.6f, 2.4f);
                c.Solid.Lump(new Vector3(k * half, s * 0.25f, r.Range(-1f, 1f)), new Vector3(s, s * 0.8f, s), Flesh(r), 8, 0.25f, r.Int(100), SkinPale);
            }
            c.Solid.Lump(new Vector3(0f, height - 0.2f, 0f), new Vector3(1.6f, 1.3f, 1.4f), Flesh(r), 8, 0.25f, r.Int(100), SkinPale);
            // A sheet of flesh hanging from the point, and drips.
            Path.Clear();
            Radii.Clear();
            float sheet = height * r.Range(0.3f, 0.55f);
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                Path.Add(new Vector3(Mathf.Sin(t * 2f) * 0.3f, height - 0.6f - t * sheet, 0f));
                Radii.Add(Mathf.Lerp(1.4f, 0.4f, t * t));
            }
            // Wide across the arch, thin through it.
            Blade(c, Path, Radii, Vector3.forward, 0.35f, 6, SkinRed, SkinPink);
            for (int i = 0; i < 3; i++)
                Drip(c, r, new Vector3(r.Range(-half * 0.5f, half * 0.5f), height * r.Range(0.75f, 0.9f), 0f), r.Range(1.5f, 4.5f), r.Range(0.18f, 0.28f));
            if (r.Chance(0.6f)) BrokenLight(c, r, ToWorld(c, x, z, yaw, new Vector3(0f, height * 0.6f, 0f)), Dying, 3.8f);
            Tops.Add(ToWorld(c, x, z, yaw, new Vector3(0f, height - 1f, 0f)));
        }

        /// <summary>A bulging Covenant pillar, swelling in its middle and narrowing to its cap, with slits of dim violet light round its waist; overgrown,
        /// with flesh draped down it, a coil round it and mass at its foot.</summary>
        private void BulgePillar(TerrainCanvas c, TerrainRandom r, float x, float z)
        {
            float h = r.Range(12f, 18f);
            // The shaft and coil reach 3.3 m, the flesh at its foot 5 m.
            if (!Tall(c, x, z, 5.1f, h + 1.2f, 0.45f)) return;
            float yaw = r.Range(0f, 360f);
            c.PlaceSolid(x, z, yaw);
            c.Solid.Cylinder(Vector3.zero, 3f, 2.7f, 0.7f, 8, VioletDark);
            Path.Clear();
            Radii.Clear();
            float swell = r.Range(0.92f, 1.08f);
            Path.Add(new Vector3(0f, 0.7f, 0f)); Radii.Add(1.9f);
            Path.Add(new Vector3(0f, 2f, 0f)); Radii.Add(1.6f);
            Path.Add(new Vector3(0f, 0.3f * h, 0f)); Radii.Add(2.4f * swell);
            Path.Add(new Vector3(0f, 0.5f * h, 0f)); Radii.Add(2.6f * swell);
            Path.Add(new Vector3(0f, 0.68f * h, 0f)); Radii.Add(2f * swell);
            Path.Add(new Vector3(0f, 0.84f * h, 0f)); Radii.Add(1.2f);
            Path.Add(new Vector3(0f, 0.94f * h, 0f)); Radii.Add(1f);
            Path.Add(new Vector3(0f, h, 0f)); Radii.Add(1.6f);
            c.Solid.Tube(Path, Radii, 8, Vary(r, Violet, 0.06f), VioletPale, true);
            bool lit = r.Chance(0.7f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.5708f + 0.4f;
                float rs = 2.45f * swell;
                c.Solid.Lump(new Vector3(Mathf.Cos(a) * rs, 0.49f * h, Mathf.Sin(a) * rs), new Vector3(0.2f, 0.13f * h, 0.2f), lit && i != 2 ? VioletLit : VioletDead, 6);
            }
            if (lit)
            {
                Vector3 glow = ToWorld(c, x, z, yaw, new Vector3(0f, 0.49f * h, 0f));
                BrokenLight(c, r, glow, Dying, 4.4f);
                LightPool(c, glow.x, glow.z, 6.5f, Dying, 0.45f, 0.85f);
            }
            Drape(c, r, -2.1f, 0f, h * r.Range(0.5f, 0.9f), r.Range(1.4f, 2f));
            Path.Clear();
            Radii.Clear();
            float turns = r.Range(6f, 10f), phase = r.Range(0f, 6.28f);
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float a = phase + t * turns;
                float y = 0.7f + t * h * 0.75f;
                float rr = y < 2f ? 1.9f : y < 0.68f * h ? 2.6f * swell + 0.2f : Mathf.Lerp(2.3f, 1.4f, (y - 0.68f * h) / (0.3f * h));
                Path.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
                Radii.Add(Mathf.Lerp(0.38f, 0.1f, t));
            }
            c.Solid.Tube(Path, Radii, 4, RootColor, TipColor);
            Tops.Add(ToWorld(c, x, z, yaw, new Vector3(0f, h * 0.92f, 0f)));
        }

        /// <summary>Covenant crates, chamfered and banded with dim light: a few together, or one low one by the floor.</summary>
        private static void CovenantCrates(TerrainCanvas c, TerrainRandom r, float x, float z, bool low)
        {
            int count = low ? 1 : 2 + r.Int(3);
            if (!Tall(c, x, z, low ? 1.6f : 4.2f, low ? 1.2f : 2.8f, low ? 0.25f : 0.45f)) return;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            const float w = 2.2f, d = 1.4f;
            for (int i = 0; i < count; i++)
            {
                bool stacked = !low && i == count - 1 && count > 2;
                Vector3 foot = stacked ? new Vector3(r.Range(-0.6f, 0.6f), 1.1f, r.Range(-0.4f, 0.4f))
                    : low ? Vector3.zero : new Vector3(r.Range(-2f, 2f), 0f, r.Range(-2f, 2f));
                c.Solid.Box(foot, new Vector3(w, 1.1f, d), Vary(r, Violet, 0.08f), 0.78f);
                c.Solid.Box(foot + new Vector3(0f, 0.3f, -d * 0.5f + 0.02f), new Vector3(w * 0.8f, 0.08f, 0.06f), r.Chance(0.5f) ? VioletLit : VioletDead);
            }
            if (r.Chance(0.6f))
            {
                float s = low ? 0.7f : r.Range(1f, 1.5f);
                c.Solid.Lump(new Vector3(r.Range(-0.6f, 0.6f), s * 0.3f, r.Range(-0.6f, 0.6f)), new Vector3(s, s * 0.7f, s), Flesh(r), 7, 0.2f, r.Int(100), SkinPale);
            }
        }

        // ------------------------------------------------------------------ the Flood's own growths

        private void Growth(TerrainCanvas c, TerrainRandom r)
        {
            // Great tendrils from the walls, arching over the yards into their floor.
            for (int i = 0; i < 7; i++)
            {
                float k = r.Sign(), v = r.Range(0f, FloorZ + 30f);
                var from = new Vector3(k * (WallX - 3f), r.Range(10f, 22f), c.Z(v));
                var to = new Vector3(k * r.Range(103f, 122f), -0.4f, c.Z(v + r.Range(-14f, 14f)));
                WallTendril(c, r, from, to);
            }
            for (int i = 0; i < 2; i++)
            {
                float wx = r.Range(-100f, 100f);
                var from = new Vector3(wx, r.Range(10f, 18f), c.Z(WallFarV - 3f));
                var to = new Vector3(wx + r.Range(-14f, 14f), -0.4f, c.Z(FloorZ + r.Range(16f, 30f)));
                WallTendril(c, r, from, to);
            }
            // Tendrils hung between the structures and the walls.
            int hung = 0;
            for (int attempt = 0; attempt < 30 && hung < 7 && Tops.Count > 1; attempt++)
            {
                Vector3 a = Tops[r.Int(Tops.Count)], b = Tops[r.Int(Tops.Count)];
                if (a == b) continue;
                int before = c.Solid.VertexCount;
                HangingTendril(c, r, a, b);
                if (c.Solid.VertexCount > before) hung++;
            }

            // Clusters of swollen infection pods with glowing cores, most in the yards close to the field, a few low ones beside it.
            for (int i = 0; i < 12; i++)
            {
                Near(c, r, out float x, out float z);
                PodCluster(c, r, x, z, r.Range(1.1f, 1.9f), false);
            }
            for (int i = 0; i < 3; i++)
            {
                Around(c, r, out float x, out float z);
                PodCluster(c, r, x, z, r.Range(1.2f, 2f), false);
            }
            for (int i = 0; i < 6; i++)
            {
                Beside(c, r, out float x, out float z);
                PodCluster(c, r, x, z, r.Range(0.45f, 0.62f), true);
            }

            // Bulbous sacs.
            for (int i = 0; i < 7; i++)
            {
                Near(c, r, out float x, out float z);
                Sac(c, r, x, z, r.Range(1.6f, 2.8f));
            }

            // Fungal stalks with caps.
            for (int i = 0; i < 10; i++)
            {
                Near(c, r, out float x, out float z);
                FungusCluster(c, r, x, z, r.Range(3f, 7f));
            }
            for (int i = 0; i < 2; i++)
            {
                Around(c, r, out float x, out float z);
                FungusCluster(c, r, x, z, r.Range(3f, 6f));
            }
            for (int i = 0; i < 5; i++)
            {
                Beside(c, r, out float x, out float z);
                FungusCluster(c, r, x, z, r.Range(0.8f, 1.1f));
            }

            // Tendrils arching out of the floor and rearing ones.
            ArchingTendrils(c, r, 6, 24);
            for (int i = 0; i < 5; i++)
            {
                Around(c, r, out float x, out float z);
                float height = r.Range(5f, 12f), thickness = r.Range(0.5f, 1f);
                if (!MayStand(c, x, z, height * 0.5f + thickness * 2.4f, height)) continue;
                TerrainProps.RearingTendril(c, r, x, z, height, thickness, RootColor, TipColor);
            }

            // Swollen mounds: heaps of mass along the walls and out around the field, low ones beside it.
            for (int i = 0; i < 4; i++)
                Mound(c, r, r.Sign() * r.Range(118f, 136f), c.Z(r.Range(0f, FloorZ + 50f)), r.Range(3.4f, 5.4f));
            for (int i = 0; i < 5; i++)
            {
                Around(c, r, out float x, out float z);
                Mound(c, r, x, z, r.Range(1.8f, 3.8f));
            }
            for (int i = 0; i < 7; i++)
            {
                Beside(c, r, out float x, out float z);
                Mound(c, r, x, z, r.Range(0.7f, 1.05f));
            }

            // Spores drifting up from the growths near the field.
            for (int i = 0; i < 40; i++)
            {
                Near(c, r, out float x, out float z);
                Color32 spore = r.Chance(0.7f) ? Sickly : Amber;
                c.Glow.Mote(new Vector3(x, r.Range(1f, 9f), z), r.Range(0.14f, 0.3f), new Color32(spore.r, spore.g, spore.b, (byte)r.Range(120f, 200f)),
                    new Vector3(r.Range(1f, 3f), r.Range(0.6f, 2f), r.Range(0.06f, 0.18f)), r.Range(0f, 6.28f));
            }
        }

        /// <summary>
        /// Up to count tendrils arching beside the field, mostly lengthwise in the space between the flank strips and the walls, a few out around the far end.
        /// Each one's x is picked so that its whole reach fits between the flank strips and the walls, as a random spot there would mostly be refused.
        /// </summary>
        private static void ArchingTendrils(TerrainCanvas c, TerrainRandom r, int count, int attempts)
        {
            int placed = 0;
            for (int attempt = 0; attempt < attempts && placed < count; attempt++)
            {
                float length = r.Range(14f, 24f), height = r.Range(5f, 11f), thickness = r.Range(0.7f, 1.2f);
                // How far its body reaches from its middle: half its length, its sideways wobble and its root's thickness.
                float reach = length * 0.55f + thickness * 2f;
                float x, z, yaw;
                if (attempt % 4 != 3)
                {
                    x = r.Sign() * r.Range(FlankClear + reach, 137.5f - reach);
                    z = c.Z(r.Range(0f, FloorZ + 30f));
                    yaw = 90f + r.Range(-35f, 35f);
                }
                else
                {
                    Around(c, r, out x, out z);
                    yaw = r.Range(0f, 360f);
                }
                if (!MayStand(c, x, z, reach, height * 1.3f + thickness * 2f)) continue;
                if (TerrainProps.Tendril(c, r, x, z, yaw, length, height, thickness, RootColor, TipColor)) placed++;
            }
        }

        /// <summary>A heap of swollen lumps. Its lumps reach at most 1.55 size from its middle and 1.3 size up.</summary>
        private static void Mound(TerrainCanvas c, TerrainRandom r, float x, float z, float size)
        {
            if (!Tall(c, x, z, size * 1.6f, size * 1.32f, 0.45f)) return;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            int count = 3 + r.Int(2);
            int segments = size < 1.2f ? 7 : 8;
            for (int i = 0; i < count; i++)
            {
                float a = r.Range(0f, 6.28f);
                float d = i == 0 ? 0f : r.Range(0.3f, 0.8f) * size;
                float s = (i == 0 ? 1f : r.Range(0.4f, 0.6f)) * size;
                c.Solid.Lump(new Vector3(Mathf.Cos(a) * d, s * 0.3f, Mathf.Sin(a) * d), new Vector3(s, s * 0.8f, s), Flesh(r), segments, 0.25f, r.Int(100), SkinPale);
            }
        }

        /// <summary>A cluster of swollen infection pods on a bed of mass, each with a glowing core in its open top, a haze of their light and a pool of it on the floor.</summary>
        private static void PodCluster(TerrainCanvas c, TerrainRandom r, float x, float z, float size, bool low)
        {
            // Pods stand up to 1.2 size out with up to 0.78 size of their own, and rise up to 1.8 size.
            if (!Tall(c, x, z, size * 2.2f, size * 2f, 0.4f)) return;
            float yaw = r.Range(0f, 360f);
            c.PlaceSolid(x, z, yaw);
            bool green = r.Chance(0.55f);
            Color32 glow = green ? GlowGreen : GlowAmber;
            Color32 light = green ? Sickly : Amber;
            c.Solid.Lump(new Vector3(0f, size * 0.15f, 0f), new Vector3(size * 1.6f, size * 0.55f, size * 1.6f), Flesh(r), low ? 7 : 8, 0.25f, r.Int(100), SkinDark);
            int pods = low ? 2 + r.Int(2) : 3 + r.Int(3);
            float ground = c.GroundAt(x, z);
            for (int i = 0; i < pods; i++)
            {
                float a = r.Range(0f, 6.28f);
                float d = i == 0 ? 0f : r.Range(0.6f, 1.2f) * size;
                float s = size * r.Range(0.55f, 0.85f) * (i == 0 ? 1.1f : 1f);
                var at = new Vector3(Mathf.Cos(a) * d, s * 0.9f, Mathf.Sin(a) * d);
                // The pod: pale swollen skin, its top translucent with the core's light.
                Color32 top = new Color32((byte)((PodSkin.r + glow.r) / 2), (byte)((PodSkin.g + glow.g) / 2), (byte)((PodSkin.b + glow.b) / 2), 70);
                c.Solid.Lump(at, new Vector3(s * 0.82f, s, s * 0.82f), Vary(r, PodSkin, 0.08f), low ? 7 : 8, 0.1f, r.Int(100), top);
                // The core showing in its open top.
                c.Solid.Lump(at + new Vector3(0f, s * 0.82f, 0f), new Vector3(s * 0.38f, s * 0.2f, s * 0.38f), glow, 6);
                if (!low && i < 2)
                {
                    c.Glow.Uv = new Vector2(0.45f, r.Range(0f, 6.28f));
                    c.Glow.Halo(ToWorld(c, x, z, yaw, at + new Vector3(0f, s * 0.95f, 0f)), s * 1.6f, new Color32(light.r, light.g, light.b, 56));
                    c.Glow.Uv = Vector2.zero;
                }
            }
            if (low)
            {
                c.Glow.Uv = new Vector2(0.45f, r.Range(0f, 6.28f));
                c.Glow.Halo(new Vector3(x, ground + size * 1.6f, z), size * 2f, new Color32(light.r, light.g, light.b, 44));
                c.Glow.Uv = Vector2.zero;
            }
            LightPool(c, x, z, size * (low ? 3.5f : 5.5f), light, low ? 0.6f : 0.95f, 0.35f);
        }

        /// <summary>A bulbous sac of mass: a great swollen bladder, translucent and faintly lit from inside, on a bed of flesh with a smaller one beside it.</summary>
        private static void Sac(TerrainCanvas c, TerrainRandom r, float x, float z, float size)
        {
            if (!Tall(c, x, z, size * 1.9f, size * 2.2f, 0.4f)) return;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            c.Solid.Lump(new Vector3(0f, size * 0.1f, 0f), new Vector3(size * 1.5f, size * 0.45f, size * 1.4f), Flesh(r), 8, 0.25f, r.Int(100), SkinDark);
            c.Solid.Lump(new Vector3(0f, size * 1.05f, 0f), new Vector3(size * 0.95f, size, size * 0.9f), SacSkin, 10, 0.08f, r.Int(100), SacTop);
            float a = r.Range(0f, 6.28f), s = size * r.Range(0.4f, 0.55f);
            c.Solid.Lump(new Vector3(Mathf.Cos(a) * size * 1.1f, s * 0.9f, Mathf.Sin(a) * size * 1.1f), new Vector3(s * 0.9f, s, s * 0.9f), SacSkin, 9, 0.1f, r.Int(100), SacTop);
            // Veins over the big one.
            for (int v = 0; v < 3; v++)
            {
                Path.Clear();
                Radii.Clear();
                float va = r.Range(0f, 6.28f);
                for (int p = 0; p <= 5; p++)
                {
                    float phi = Mathf.Lerp(0.2f, 2.6f, p / 5f);
                    float ring = Mathf.Sin(phi);
                    Path.Add(new Vector3(Mathf.Cos(va + p * 0.12f) * size * 0.97f * ring, size * 1.05f + Mathf.Cos(phi) * size * 1.02f, Mathf.Sin(va + p * 0.12f) * size * 0.92f * ring));
                    Radii.Add(0.07f + 0.03f * size);
                }
                c.Solid.Tube(Path, Radii, 4, SkinRed, SkinDark);
            }
            c.Glow.Uv = new Vector2(0.3f, r.Range(0f, 6.28f));
            c.Glow.Halo(new Vector3(x, c.GroundAt(x, z) + size * 1.4f, z), size * 1.8f, new Color32(Amber.r, Amber.g, Amber.b, 30));
            c.Glow.Uv = Vector2.zero;
        }

        /// <summary>A clump of fungal stalks, each with a cap, pale and glowing sickly underneath. size is the tallest stalk's height.</summary>
        private static void FungusCluster(TerrainCanvas c, TerrainRandom r, float x, float z, float size)
        {
            // Stalks stand up to 0.4 size out and lean up to 0.2 size; caps reach 0.25 size + 0.3 beyond that and rise about 0.44 x their radius over a stalk's top.
            if (!Tall(c, x, z, size * 0.9f + 0.5f, size * 1.12f + 0.15f, 0.35f)) return;
            c.PlaceSolid(x, z, r.Range(0f, 360f));
            int stalks = size < 1.2f ? 2 + r.Int(2) : 3 + r.Int(3);
            for (int i = 0; i < stalks; i++)
            {
                float a = r.Range(0f, 6.28f);
                float d = i == 0 ? 0f : r.Range(0.1f, 0.4f) * size;
                float h = size * (i == 0 ? 1f : r.Range(0.45f, 0.9f));
                float lean = r.Range(0f, 0.2f) * h, la = r.Range(0f, 6.28f);
                var foot = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                Path.Clear();
                Radii.Clear();
                for (int p = 0; p <= 3; p++)
                {
                    float t = p / 3f;
                    Path.Add(foot + new Vector3(Mathf.Cos(la) * lean * t * t, h * t - (p == 0 ? 0.1f : 0f), Mathf.Sin(la) * lean * t * t));
                    Radii.Add((h * 0.055f + 0.05f) * Mathf.Lerp(1.4f, 0.8f, t));
                }
                c.Solid.Tube(Path, Radii, 6, Stalk, StalkTop);
                float capR = h * 0.25f + 0.25f;
                c.Solid.Lump(Path[3] + new Vector3(0f, capR * 0.1f, 0f), new Vector3(capR, capR * 0.32f, capR), Gill, 8, 0.12f, r.Int(100), Vary(r, Cap, 0.12f));
            }
            if (size > 2f)
            {
                c.Glow.Uv = new Vector2(0.3f, r.Range(0f, 6.28f));
                c.Glow.Halo(new Vector3(x, c.GroundAt(x, z) + size * 0.85f, z), size * 0.6f, new Color32(Sickly.r, Sickly.g, Sickly.b, 30));
                c.Glow.Uv = Vector2.zero;
            }
        }

        // ------------------------------------------------------------------ across no man's land

        public override void NoMansLand(TerrainCanvas c, TerrainRandom r)
        {
            float W = c.HalfWidth;
            // Deck plates lifted and cracked, flesh oozing round them.
            for (int i = 0; i < 12; i++)
            {
                float x = r.Range(-W + 4f, W - 4f), z = r.Range(-18f, 18f) * r.Range(0.3f, 1f);
                LiftedPlate(c, r, x, z, r.Range(2f, 4f), r.Range(1.4f, 3f));
            }
            // Scorched holes.
            for (int i = 0; i < 10; i++)
                Crater(c, r.Range(-W + 4f, W - 4f), r.Range(-15f, 15f) * r.Value(), r.Range(1.1f, 2.6f), OnCovenant ? C(116, 92, 128) : C(112, 104, 96));
            // Flesh splattered across the line, in clumps.
            for (int i = 0; i < 12; i++)
            {
                float x = r.Range(-W + 3f, W - 3f), z = r.Range(-17f, 17f) * r.Range(0.3f, 1f);
                c.PaintDisc(x, z, r.Range(2f, 4.5f), 1, 0.65f);
                int blobs = 3 + r.Int(4);
                c.Decals.Uv = new Vector2(0f, 0.3f);
                for (int b = 0; b < blobs; b++)
                {
                    float bx = x + r.Range(-3.5f, 3.5f), bz = z + r.Range(-3f, 3f), br = r.Range(0.3f, 1.2f);
                    c.Decals.FlatDisc(D, bx, bz, br * 1.4f, br, C(164, 102, 90, 180), C(164, 102, 90, 0), 9, r.Range(0f, 180f));
                }
                c.Decals.Uv = Vector2.zero;
            }
            // Burst pods: a torn pale skin opened like petals round a wet hollow, with a glow left in it, and a splash of fluid round it.
            for (int i = 0; i < 10; i++)
            {
                float x = r.Range(-W + 4f, W - 4f), z = r.Range(-16f, 16f) * r.Range(0.3f, 1f);
                float radius = r.Range(0.6f, 1.2f);
                bool green = r.Chance(0.55f);
                Color32 light = green ? Sickly : Amber;
                c.Decals.Uv = new Vector2(0f, 0.3f);
                c.Decals.FlatDisc(D, x, z, radius * 2.6f, radius * 2.2f, C(150, 104, 80, 120), C(150, 104, 80, 0), 12, r.Range(0f, 180f));
                int petals = 4 + r.Int(3);
                float spin = r.Range(0f, 360f);
                for (int p = 0; p < petals; p++)
                {
                    float a = spin + p * 360f / petals + r.Range(-12f, 12f);
                    float ar = a * Mathf.Deg2Rad;
                    c.Decals.FlatRect(D, x + Mathf.Cos(ar) * radius * 1.15f, z + Mathf.Sin(ar) * radius * 1.15f, radius * 0.55f, radius * 0.9f, 90f - a, C(204, 160, 132, 200));
                }
                c.Decals.FlatRing(D, x, z, radius * 0.7f, radius, C(214, 174, 150, 200), 12);
                c.Decals.FlatDisc(D, x, z, radius * 0.72f, radius * 0.66f, C(96, 62, 54, 170), C(120, 80, 64, 150), 10);
                c.Decals.Uv = Vector2.zero;
                c.Glow.Uv = new Vector2(0.5f, r.Range(0f, 6.28f));
                c.Glow.FlatDisc(G, x, z, radius * 2f, radius * 1.9f, new Color32(light.r, light.g, light.b, 40), new Color32(light.r, light.g, light.b, 0), 10);
                c.Glow.Uv = Vector2.zero;
            }
            // Veins across the line, and a few glowing heads.
            for (int i = 0; i < 6; i++)
            {
                float x = r.Range(-W + 6f, W - 6f);
                float heading = r.Chance(0.5f) ? r.Range(-0.6f, 0.6f) : 3.1416f + r.Range(-0.6f, 0.6f);
                TerrainCanvas.Wander(Line, r, new Vector2(x, r.Range(-8f, 8f)), heading, 3f, 4 + r.Int(5), 0.5f);
                c.KeepOnFloor(Line, 0.5f);
                Vein(c, Line, r.Range(0.3f, 0.6f));
            }
            for (int i = 0; i < 8; i++) FloodPustule(c, r, r.Range(-W + 3f, W - 3f), r.Range(-16f, 16f), r.Range(0.22f, 0.45f), r.Chance(0.4f));
            // A darker churned band along the line itself.
            for (float x = -W; x <= W; x += 12f)
                c.Darken(x + r.Range(-4f, 4f), r.Range(-3f, 3f), r.Range(6f, 11f), r.Range(0.08f, 0.2f));
        }
    }
}
