// TerrainBuilder.cs
// Builds a whole map from two factions and a seed (M10): the height of the land, the ground mesh, the control map, the sky dome and haze, and whatever the two layouts put on their halves. Team A's half is to the south, Team B's
// to the north, and they blend across no man's land. The same arguments give the same map. The fighting floor is exactly flat: hills rise only outside it.
// It makes meshes and one texture, and sets up no scene objects: BattlefieldView shows them.
// Looks only: it reads no battle state and changes none. Depends on: TerrainCanvas, TerrainLayout, TerrainTheme, TerrainNoise, Halobellum.Core, UnityEngine.

using System.Threading.Tasks;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    /// <summary>What a map is made from.</summary>
    public struct TerrainRequest
    {
        public string FactionA, FactionB;
        public int Seed;
        /// <summary>Which layout each side gets, or -1 for the seed to pick.</summary>
        public int LayoutA, LayoutB;
        public float HalfWidth, ZoneDepth, Gap;
        /// <summary>An interior look for the whole field (D80), or null for the outdoor maps of D47, each half its own faction's land. The layouts are then ignored.</summary>
        public TerrainLook Look;
    }

    /// <summary>A built map: its meshes and control map, and what it is made of.</summary>
    public sealed class TerrainMap
    {
        public Mesh Ground, Solid, Decals, Glow, Haze, Sky;
        public Texture2D Control;
        public TerrainTheme ThemeA, ThemeB;
        /// <summary>The theme adjusted by Team A's layout: the sky, sun and air of the whole map.</summary>
        public TerrainTheme Atmosphere;
        public TerrainLayout LayoutA, LayoutB;
        /// <summary>The interior look of the whole field (D80), or null on an outdoor map.</summary>
        public TerrainLook Look;
        public float BuildMilliseconds;

        /// <summary>What the map is, for logs and tests.</summary>
        public string Describe() => Look != null ? $"{Look.Name} ({Look.Id})" : $"{ThemeA.Faction} {LayoutA.Name} against {ThemeB.Faction} {LayoutB.Name}";

        public int TriangleCount => Triangles(Ground) + Triangles(Solid) + Triangles(Decals) + Triangles(Glow) + Triangles(Haze) + Triangles(Sky);

        private static int Triangles(Mesh mesh) => mesh == null ? 0 : (int)mesh.GetIndexCount(0) / 3;

        public void Destroy()
        {
            Kill(Ground); Kill(Solid); Kill(Decals); Kill(Glow); Kill(Haze); Kill(Sky); Kill(Control);
        }

        private static void Kill(Object thing)
        {
            if (thing == null) return;
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }
    }

    public static class TerrainBuilder
    {
        private const int ControlResolution = 1024;

        public static TerrainMap Build(TerrainRequest request)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            TerrainLook look = request.Look;
            TerrainTheme themeA, themeB;
            TerrainLayout layoutA, layoutB;
            if (look != null)
            {
                // One look for the whole field: the same ground, layout and light on both sides.
                themeA = themeB = look.Theme;
                layoutA = layoutB = look.Layout;
            }
            else
            {
                themeA = TerrainThemes.For(request.FactionA);
                themeB = TerrainThemes.For(request.FactionB);
                var layoutsA = TerrainLayouts.For(request.FactionA);
                var layoutsB = TerrainLayouts.For(request.FactionB);
                layoutA = layoutsA[Mathf.Clamp(request.LayoutA >= 0 ? request.LayoutA : TerrainLayouts.Pick(request.FactionA, request.Seed, Team.A), 0, layoutsA.Count - 1)];
                layoutB = layoutsB[Mathf.Clamp(request.LayoutB >= 0 ? request.LayoutB : TerrainLayouts.Pick(request.FactionB, request.Seed, Team.B), 0, layoutsB.Count - 1)];
            }
            TerrainTheme atmosphere = layoutA.Mood(themeA);

            var seeds = new TerrainRandom(request.Seed, 101);
            float seedAx = seeds.Range(0f, 9000f), seedAz = seeds.Range(0f, 9000f), seedBx = seeds.Range(0f, 9000f), seedBz = seeds.Range(0f, 9000f);
            float halfLength = request.ZoneDepth + request.Gap * 0.5f;

            float Height(float x, float z) => HeightAt(x, z, request.HalfWidth, halfLength, themeA, themeB, layoutA.FlatBand, layoutB.FlatBand, seedAx, seedAz, seedBx, seedBz);

            var canvas = new TerrainCanvas(request.HalfWidth, request.ZoneDepth, request.Gap, ControlResolution, Height)
            {
                ShadowPerMetre = ShadowDirection(atmosphere),
            };
            PaintBase(canvas, layoutA, layoutB, Height, seedAx, seedAz, seedBx, seedBz);

            canvas.Side = -1;
            layoutA.Furnish(canvas, new TerrainRandom(request.Seed, 211));
            canvas.Side = 1;
            layoutB.Furnish(canvas, new TerrainRandom(request.Seed, 307));
            if (look != null)
            {
                canvas.Side = 0;
                look.Layout.NoMansLand(canvas, new TerrainRandom(request.Seed, 401));
            }
            else Battlescars(canvas, themeA, themeB, new TerrainRandom(request.Seed, 401));
            FloorEdge(canvas);
            Motes(canvas, themeA, themeB, atmosphere, new TerrainRandom(request.Seed, 503));

            var map = new TerrainMap
            {
                ThemeA = themeA,
                ThemeB = themeB,
                Atmosphere = atmosphere,
                LayoutA = layoutA,
                LayoutB = layoutB,
                Look = look,
                Ground = BuildGround(canvas, Height, layoutA.FlatBand, layoutB.FlatBand),
                Solid = canvas.Solid.ToMesh("Terrain Solid"),
                Decals = canvas.Decals.ToMesh("Terrain Decals"),
                Glow = canvas.Glow.ToMesh("Terrain Glow"),
                Haze = BuildHaze(request.HalfWidth, halfLength),
                Sky = BuildSky(),
                Control = BuildControl(canvas, seedAx, seedAz),
            };
            map.BuildMilliseconds = (float)clock.Elapsed.TotalMilliseconds;
            return map;
        }

        // ------------------------------------------------------------------ the land

        /// <summary>The height of the land at a point. Zero over the arena and its flat band.</summary>
        private static float HeightAt(float x, float z, float halfWidth, float halfLength, TerrainTheme themeA, TerrainTheme themeB, float flatA, float flatB,
            float seedAx, float seedAz, float seedBx, float seedBz)
        {
            float ox = Mathf.Max(0f, Mathf.Abs(x) - halfWidth);
            float oz = Mathf.Max(0f, Mathf.Abs(z) - halfLength);
            float outside = Mathf.Sqrt(ox * ox + oz * oz);
            float flat = z < 0f ? flatA : flatB;
            if (outside <= flat) return 0f;

            float toB = TerrainNoise.Smooth(-70f, 70f, z);
            float h = 0f;
            if (toB < 1f) h += themeA.HillAt(x, z, outside, seedAx, seedAz) * (1f - toB);
            if (toB > 0f) h += themeB.HillAt(x, z, outside, seedBx, seedBz) * toB;

            h *= TerrainNoise.Smooth(flat, flat + 34f, outside);
            // The south never rises in front of the camera's default view: keep a low slope in line with the floor.
            if (z < -halfLength)
            {
                float sideways = Mathf.Max(0f, Mathf.Abs(x) - halfWidth);
                float cap = (-z - halfLength) * 0.16f + sideways * 0.5f;
                if (h > cap) h = cap;
            }
            return h;
        }

        private static Vector2 ShadowDirection(TerrainTheme theme)
        {
            Vector3 forward = Quaternion.Euler(theme.SunAngles.x, theme.SunAngles.y, 0f) * Vector3.forward;
            float flatten = Mathf.Max(0.3f, -forward.y);
            return new Vector2(forward.x, forward.z) / flatten;
        }

        private static void PaintBase(TerrainCanvas canvas, TerrainLayout layoutA, TerrainLayout layoutB, System.Func<float, float, float> height,
            float seedAx, float seedAz, float seedBx, float seedBz)
        {
            int resolution = canvas.Resolution;
            float halfWidth = canvas.HalfWidth, halfLength = canvas.HalfLength;
            float[] worldX = canvas.AxisX.World, worldZ = canvas.AxisZ.World;
            float[] second = canvas.Second, third = canvas.Third, shade = canvas.Shade;
            Parallel.For(0, resolution, iz =>
            {
                float z = worldZ[iz];
                int row = iz * resolution;
                for (int ix = 0; ix < resolution; ix++)
                {
                    float x = worldX[ix];
                    float ox = Mathf.Max(0f, Mathf.Abs(x) - halfWidth);
                    float oz = Mathf.Max(0f, Mathf.Abs(z) - halfLength);
                    float outside = Mathf.Sqrt(ox * ox + oz * oz);
                    float h = outside > 0f ? height(x, z) : 0f;
                    float toB = SideAt(x, z, seedAx, seedAz);
                    float second1 = 0f, third1 = 0f, second2 = 0f, third2 = 0f;
                    if (toB < 0.999f) layoutA.BaseWeights(x, z, h, outside, seedAx, seedAz, out second1, out third1);
                    if (toB > 0.001f) layoutB.BaseWeights(x, z, h, outside, seedBx, seedBz, out second2, out third2);
                    second[row + ix] = second1 + (second2 - second1) * toB;
                    third[row + ix] = third1 + (third2 - third1) * toB;
                    // Valleys a little darker, so that hills read from above.
                    shade[row + ix] = outside > 0f ? 0.84f + 0.16f * TerrainNoise.Smooth(0f, 16f, h) : 1f;
                }
            });
        }

        /// <summary>How far towards Team B's ground a point is: 0 is all Team A's, 1 all Team B's. A rough, wandering line across no man's land.</summary>
        public static float SideAt(float x, float z, float seedX, float seedZ)
        {
            float wander = (TerrainNoise.Fbm((x + seedX) / 38f, seedZ * 0.01f, 3) - 0.5f) * 30f;
            return TerrainNoise.Smooth(-12f, 12f, z + wander);
        }

        private static Texture2D BuildControl(TerrainCanvas canvas, float seedX, float seedZ)
        {
            int resolution = canvas.Resolution;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Terrain Control",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = texture.GetRawTextureData<Color32>();
            float[] worldX = canvas.AxisX.World, worldZ = canvas.AxisZ.World;
            for (int iz = 0; iz < resolution; iz++)
            {
                int row = iz * resolution;
                float z = worldZ[iz];
                for (int ix = 0; ix < resolution; ix++)
                {
                    int i = row + ix;
                    float toB = SideAt(worldX[ix], z, seedX, seedZ);
                    pixels[i] = new Color32(
                        (byte)(Mathf.Clamp01(canvas.Second[i]) * 255f),
                        (byte)(Mathf.Clamp01(canvas.Third[i]) * 255f),
                        (byte)(toB * 255f),
                        (byte)(Mathf.Clamp01(canvas.Shade[i]) * 255f));
                }
            }
            texture.Apply(false, false);
            return texture;
        }

        private static Mesh BuildGround(TerrainCanvas canvas, System.Func<float, float, float> height, float flatA, float flatB)
        {
            float[] xs = GridLines(canvas.HalfWidth, Mathf.Min(flatA, flatB));
            float[] zs = GridLines(canvas.HalfLength, Mathf.Min(flatA, flatB));
            int nx = xs.Length, nz = zs.Length;
            var positions = new Vector3[nx * nz];
            var normals = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            var triangles = new int[(nx - 1) * (nz - 1) * 6];
            var heights = new float[nx * nz];
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++) heights[j * nx + i] = height(xs[i], zs[j]);
            }
            int t = 0;
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int v = j * nx + i;
                    positions[v] = new Vector3(xs[i], heights[v] + TerrainCanvas.GroundHeight, zs[j]);
                    uvs[v] = new Vector2(canvas.AxisX.ToU(xs[i]), canvas.AxisZ.ToU(zs[j]));
                    int il = Mathf.Max(0, i - 1), ir = Mathf.Min(nx - 1, i + 1), jd = Mathf.Max(0, j - 1), ju = Mathf.Min(nz - 1, j + 1);
                    float dx = (heights[j * nx + ir] - heights[j * nx + il]) / Mathf.Max(0.01f, xs[ir] - xs[il]);
                    float dz = (heights[ju * nx + i] - heights[jd * nx + i]) / Mathf.Max(0.01f, zs[ju] - zs[jd]);
                    normals[v] = new Vector3(-dx, 1f, -dz).normalized;
                    if (i == nx - 1 || j == nz - 1) continue;
                    // Clockwise from above.
                    triangles[t++] = v;
                    triangles[t++] = v + nx;
                    triangles[t++] = v + nx + 1;
                    triangles[t++] = v;
                    triangles[t++] = v + nx + 1;
                    triangles[t++] = v + 1;
                }
            }
            var mesh = new Mesh { name = "Terrain Ground", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            return mesh;
        }

        /// <summary>Positions of grid lines along an axis: few over the flat floor, then close together where the land starts to rise, spreading out to the far land.</summary>
        private static float[] GridLines(float half, float flat)
        {
            var lines = new System.Collections.Generic.List<float>();
            float edge = half + flat;
            int inner = Mathf.Max(2, Mathf.RoundToInt(edge / 24f));
            for (int i = 0; i < inner; i++) lines.Add(edge * i / inner);
            float at = edge, step = 5f;
            while (at < TerrainAxis.FarReach)
            {
                lines.Add(at);
                at += step;
                step *= 1.05f;
            }
            lines.Add(TerrainAxis.FarReach);
            var all = new float[lines.Count * 2 - 1];
            for (int i = 0; i < lines.Count; i++)
            {
                all[lines.Count - 1 - i] = -lines[i];
                all[lines.Count - 1 + i] = lines[i];
            }
            return all;
        }

        private static Mesh BuildHaze(float halfWidth, float halfLength)
        {
            float[] axis = { -1400f, -800f, -500f, -320f, -220f, -160f, -130f, -105f, -90f, 90f, 105f, 130f, 160f, 220f, 320f, 500f, 800f, 1400f };
            int n = axis.Length;
            var positions = new Vector3[n * n];
            var colors = new Color32[n * n];
            var triangles = new System.Collections.Generic.List<int>((n - 1) * (n - 1) * 6);
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float x = axis[i], z = axis[j];
                    float ox = Mathf.Max(0f, Mathf.Abs(x) - halfWidth), oz = Mathf.Max(0f, Mathf.Abs(z) - halfLength);
                    float outside = Mathf.Sqrt(ox * ox + oz * oz);
                    float strength = TerrainNoise.Smooth(14f, 90f, outside) * (1f - TerrainNoise.Smooth(600f, 1300f, outside));
                    positions[j * n + i] = new Vector3(x, 6f, z);
                    colors[j * n + i] = new Color32(255, 255, 255, (byte)(strength * 255f));
                }
            }
            for (int j = 0; j < n - 1; j++)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    // Nothing over the arena itself.
                    if (axis[i] < 0f && axis[i + 1] > 0f && axis[j] < 0f && axis[j + 1] > 0f) continue;
                    int v = j * n + i;
                    triangles.Add(v); triangles.Add(v + n); triangles.Add(v + n + 1);
                    triangles.Add(v); triangles.Add(v + n + 1); triangles.Add(v + 1);
                }
            }
            var mesh = new Mesh { name = "Terrain Haze" };
            mesh.SetVertices(positions);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            return mesh;
        }

        private static Mesh BuildSky()
        {
            const int rings = 8, segments = 24;
            var positions = new Vector3[(rings + 1) * segments];
            var triangles = new int[rings * segments * 6];
            for (int r = 0; r <= rings; r++)
            {
                // From a little below the horizon up to the top.
                float a = Mathf.Lerp(-0.25f, Mathf.PI * 0.5f, r / (float)rings);
                for (int s = 0; s < segments; s++)
                {
                    float b = s * Mathf.PI * 2f / segments;
                    positions[r * segments + s] = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(b));
                }
            }
            int t = 0;
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int s2 = (s + 1) % segments;
                    int v0 = r * segments + s, v1 = r * segments + s2, v2 = (r + 1) * segments + s2, v3 = (r + 1) * segments + s;
                    triangles[t++] = v0; triangles[t++] = v2; triangles[t++] = v1;
                    triangles[t++] = v0; triangles[t++] = v3; triangles[t++] = v2;
                }
            }
            var mesh = new Mesh { name = "Terrain Sky" };
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0, false);
            // It is moved to the camera in its shader, so it must never be culled.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }

        // ------------------------------------------------------------------ across the middle

        /// <summary>Shell holes, scorches and rutted earth across no man's land, on either side of the line.</summary>
        private static void Battlescars(TerrainCanvas c, TerrainTheme themeA, TerrainTheme themeB, TerrainRandom r)
        {
            float W = c.HalfWidth;
            for (int i = 0; i < 26; i++)
            {
                float x = r.Range(-W + 3f, W - 3f);
                float z = r.Range(-20f, 20f) * r.Value();
                float radius = r.Range(1.1f, 3.3f);
                c.Decals.Uv = new Vector2(0f, 0.7f);
                c.Decals.FlatDisc(TerrainCanvas.DecalHeight, x, z, radius * 2.1f, radius * 1.8f, new Color32(20, 16, 14, 150), new Color32(20, 16, 14, 0), 12, x);
                c.Decals.FlatDisc(TerrainCanvas.DecalHeight, x, z, radius * 0.95f, radius * 0.85f, new Color32(14, 12, 10, 190), new Color32(30, 24, 18, 110), 10, x);
                c.Decals.FlatRing(TerrainCanvas.DecalHeight, x, z, radius * 0.95f, radius * 1.35f, new Color32(120, 104, 84, 90), 12, 0f, 360f, new Color32(120, 104, 84, 0));
                c.Decals.Uv = Vector2.zero;
            }
            // A band of churned, darker ground along the line itself.
            for (float x = -W; x <= W; x += 11f)
                c.Darken(x + r.Range(-4f, 4f), r.Range(-3f, 3f), r.Range(7f, 13f), r.Range(0.12f, 0.3f));
        }

        /// <summary>The floor's edge: a soft dark line all round, so that one can see where the arena ends. Every map has it.</summary>
        private static void FloorEdge(TerrainCanvas c)
        {
            float W = c.HalfWidth;
            float halfLength = c.HalfLength;
            Color32 edge = new Color32(10, 8, 8, 70);
            c.Decals.FlatRect(TerrainCanvas.DecalHeight, 0f, -halfLength, W * 2f + 0.6f, 0.5f, 0f, edge);
            c.Decals.FlatRect(TerrainCanvas.DecalHeight, 0f, halfLength, W * 2f + 0.6f, 0.5f, 0f, edge);
            c.Decals.FlatRect(TerrainCanvas.DecalHeight, -W, 0f, 0.5f, halfLength * 2f, 0f, edge);
            c.Decals.FlatRect(TerrainCanvas.DecalHeight, W, 0f, 0.5f, halfLength * 2f, 0f, edge);
        }

        /// <summary>Tiny specks floating slowly over the map: dust, sparks of light, spores. They are too small to hide anything.</summary>
        private static void Motes(TerrainCanvas c, TerrainTheme themeA, TerrainTheme themeB, TerrainTheme atmosphere, TerrainRandom r)
        {
            int count = Mathf.Max(themeA.MoteCount, themeB.MoteCount);
            for (int i = 0; i < count; i++)
            {
                float x = r.Range(-c.HalfWidth - 90f, c.HalfWidth + 90f);
                float z = r.Range(-c.HalfLength - 30f, c.HalfLength + 130f);
                Color color = (z < 0f ? themeA : themeB).MoteColor;
                if ((z < 0f ? themeA : themeB).MoteCount < count && r.Value() > (z < 0f ? themeA : themeB).MoteCount / (float)count) continue;
                var mote = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), (byte)r.Range(90f, 190f));
                c.Glow.Mote(new Vector3(x, r.Range(1.5f, 16f), z), r.Range(0.16f, 0.42f), mote, new Vector3(r.Range(1.5f, 5f), r.Range(0.5f, 2.5f), r.Range(0.05f, 0.22f)), r.Range(0f, 6.28f));
            }
        }
    }
}
