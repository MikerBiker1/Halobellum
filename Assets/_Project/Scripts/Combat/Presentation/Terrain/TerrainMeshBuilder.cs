// TerrainMeshBuilder.cs
// Collects triangles for one terrain mesh (M10): boxes, cylinders, tubes, lumps and shards for what stands on the terrain, and flat rectangles, discs, rings and
// ribbons for what is painted on it. Everything added goes into one mesh, so a whole map is a few draw calls. Colours go in as sRGB bytes; shaders turn them linear.
// What uv and the colour's alpha mean depends on the shader: see TerrainProp, TerrainDecal and TerrainGlow.
// Looks only. Depends on: UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class TerrainMeshBuilder
    {
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        // Where the next solid thing stands.
        private Vector3 _origin;
        private float _cos = 1f, _sin;
        private float _scale = 1f;

        /// <summary>The ground is this much darker at the foot of a solid thing, fading out by AoHeight.</summary>
        public float AoFloor = 0.62f;
        public float AoHeight = 2.5f;
        /// <summary>uv of what is added next (for solids: how much it sways, and its phase).</summary>
        public Vector2 Uv;

        public int TriangleCount => _triangles.Count / 3;
        public int VertexCount => _positions.Count;

        /// <summary>Everything added next is relative to here, turned by yaw degrees and scaled.</summary>
        public void Place(Vector3 origin, float yaw = 0f, float scale = 1f)
        {
            _origin = origin;
            _cos = Mathf.Cos(yaw * Mathf.Deg2Rad);
            _sin = Mathf.Sin(yaw * Mathf.Deg2Rad);
            _scale = scale;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(_positions);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0, true);
            return mesh;
        }

        private Vector3 World(Vector3 local) =>
            new Vector3(_origin.x + (local.x * _cos + local.z * _sin) * _scale, _origin.y + local.y * _scale, _origin.z + (-local.x * _sin + local.z * _cos) * _scale);

        private Vector3 Turn(Vector3 local) => new Vector3(local.x * _cos + local.z * _sin, local.y, -local.x * _sin + local.z * _cos);

        private Color32 Shaded(Color32 color, float localY)
        {
            if (AoHeight <= 0f) return color;
            float t = Mathf.Clamp01(localY * _scale / AoHeight);
            float f = Mathf.Lerp(AoFloor, 1f, t);
            return new Color32((byte)(color.r * f), (byte)(color.g * f), (byte)(color.b * f), color.a);
        }

        private int Vertex(Vector3 world, Vector3 normal, Color32 color)
        {
            _positions.Add(world);
            _normals.Add(normal);
            _colors.Add(color);
            _uvs.Add(Uv);
            return _positions.Count - 1;
        }

        // ------------------------------------------------------------------ solid things

        /// <summary>A flat-shaded triangle of local points, clockwise as seen from the front.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            Vector3 wa = World(a), wb = World(b), wc = World(c);
            Vector3 n = Vector3.Cross(wb - wa, wc - wa).normalized;
            _triangles.Add(Vertex(wa, n, Shaded(color, a.y)));
            _triangles.Add(Vertex(wb, n, Shaded(color, b.y)));
            _triangles.Add(Vertex(wc, n, Shaded(color, c.y)));
        }

        /// <summary>A flat-shaded quad of local points, clockwise as seen from the front.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            Vector3 wa = World(a), wb = World(b), wc = World(c), wd = World(d);
            Vector3 n = Vector3.Cross(wb - wa, wc - wa).normalized;
            int ia = Vertex(wa, n, Shaded(color, a.y));
            int ib = Vertex(wb, n, Shaded(color, b.y));
            int ic = Vertex(wc, n, Shaded(color, c.y));
            int id = Vertex(wd, n, Shaded(color, d.y));
            _triangles.Add(ia); _triangles.Add(ib); _triangles.Add(ic);
            _triangles.Add(ia); _triangles.Add(ic); _triangles.Add(id);
        }

        /// <summary>A box, its base at the local point. topScale narrows the top. No bottom.</summary>
        public void Box(Vector3 foot, Vector3 size, Color32 color, float topScale = 1f)
        {
            float hx = size.x * 0.5f, hz = size.z * 0.5f;
            float tx = hx * topScale, tz = hz * topScale;
            float y0 = foot.y, y1 = foot.y + size.y;
            float x = foot.x, z = foot.z;
            var b0 = new Vector3(x - hx, y0, z - hz); var b1 = new Vector3(x + hx, y0, z - hz);
            var b2 = new Vector3(x + hx, y0, z + hz); var b3 = new Vector3(x - hx, y0, z + hz);
            var t0 = new Vector3(x - tx, y1, z - tz); var t1 = new Vector3(x + tx, y1, z - tz);
            var t2 = new Vector3(x + tx, y1, z + tz); var t3 = new Vector3(x - tx, y1, z + tz);
            Quad(t0, t3, t2, t1, color); // Top.
            Quad(b0, t0, t1, b1, color); // Towards -z.
            Quad(b1, t1, t2, b2, color); // Towards +x.
            Quad(b2, t2, t3, b3, color); // Towards +z.
            Quad(b3, t3, t0, b0, color); // Towards -x.
        }

        /// <summary>A round post narrowing or widening upwards, smooth-sided, with a flat lid.</summary>
        public void Cylinder(Vector3 foot, float radiusBottom, float radiusTop, float height, int sides, Color32 color, bool lid = true)
        {
            var points = new[] { foot, foot + Vector3.up * height };
            var radii = new[] { radiusBottom, radiusTop };
            Tube(points, radii, sides, color, color, lid);
        }

        /// <summary>
        /// A smooth tube through local points, each with its own radius, its colour running from start to end: tendrils, curved pylons, ribs, trunks.
        /// flatten squashes its section across its path (below 1), for blades.
        /// </summary>
        public void Tube(IList<Vector3> points, IList<float> radii, int sides, Color32 start, Color32 end, bool lid = false, float flatten = 1f)
        {
            int rings = points.Count;
            int first = _positions.Count;
            for (int i = 0; i < rings; i++)
            {
                Vector3 along = i == 0 ? points[1] - points[0] : i == rings - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1];
                along.Normalize();
                Vector3 across = Mathf.Abs(along.y) > 0.9f ? Vector3.right : Vector3.Cross(Vector3.up, along).normalized;
                Vector3 up = Vector3.Cross(along, across).normalized;
                float r = radii[i];
                float t = rings > 1 ? i / (float)(rings - 1) : 0f;
                Color32 color = Shaded(Color32.Lerp(start, end, t), points[i].y);
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    Vector3 outward = across * (ca * flatten) + up * sa;
                    Vector3 normal = (across * ca + up * (sa * flatten)).normalized;
                    Vertex(World(points[i] + outward * r), Turn(normal), color);
                }
            }
            for (int i = 0; i < rings - 1; i++)
            {
                int ring = first + i * sides;
                for (int s = 0; s < sides; s++)
                {
                    int s2 = (s + 1) % sides;
                    int a = ring + s, b = ring + s2, c = ring + sides + s2, d = ring + sides + s;
                    // Clockwise seen from outside.
                    _triangles.Add(a); _triangles.Add(c); _triangles.Add(d);
                    _triangles.Add(a); _triangles.Add(b); _triangles.Add(c);
                }
            }
            if (!lid) return;
            int last = first + (rings - 1) * sides;
            Vector3 lidNormal = Turn((points[rings - 1] - points[rings - 2]).normalized);
            int centre = Vertex(World(points[rings - 1]), lidNormal, Shaded(end, points[rings - 1].y));
            int edge = _positions.Count;
            for (int s = 0; s < sides; s++) Vertex(_positions[last + s], lidNormal, _colors[last + s]);
            for (int s = 0; s < sides; s++)
            {
                _triangles.Add(centre);
                _triangles.Add(edge + s);
                _triangles.Add(edge + (s + 1) % sides);
            }
        }

        /// <summary>A rounded, optionally bumpy blob: boulders, pods, pustules, treetops. Its middle is at the local point.</summary>
        public void Lump(Vector3 centre, Vector3 radii, Color32 color, int segments, float bumpy = 0f, int seed = 0, Color32? topColor = null)
        {
            int rows = Mathf.Max(3, segments / 2 + 1);
            int first = _positions.Count;
            Color32 top = topColor ?? color;
            for (int r = 0; r <= rows; r++)
            {
                float v = r / (float)rows;
                float phi = v * Mathf.PI;
                float sp = Mathf.Sin(phi), cp = Mathf.Cos(phi);
                for (int s = 0; s < segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    var unit = new Vector3(Mathf.Cos(a) * sp, cp, Mathf.Sin(a) * sp);
                    float bump = 1f;
                    if (bumpy > 0f) bump += (TerrainNoise.Value(unit.x * 1.9f + seed * 0.37f + 5f, unit.z * 1.9f + unit.y * 1.3f + seed * 0.11f) - 0.5f) * 2f * bumpy;
                    Vector3 local = centre + Vector3.Scale(unit, radii) * bump;
                    var normal = new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized;
                    Vertex(World(local), Turn(normal), Shaded(Color32.Lerp(color, top, Mathf.Clamp01(cp * 0.5f + 0.5f)), local.y));
                }
            }
            for (int r = 0; r < rows; r++)
            {
                int row = first + r * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s2 = (s + 1) % segments;
                    int a = row + s, b = row + s2, c = row + segments + s2, d = row + segments + s;
                    _triangles.Add(a); _triangles.Add(b); _triangles.Add(c);
                    _triangles.Add(a); _triangles.Add(c); _triangles.Add(d);
                }
            }
        }

        /// <summary>A sharp faceted crystal, leaning towards lean, its colour from root to tip. alpha in the colours is how much it glows.</summary>
        public void Shard(Vector3 foot, float radius, float height, Vector3 lean, int sides, Color32 root, Color32 tip, float spin = 0f)
        {
            var shoulder = foot + Vector3.up * (height * 0.68f) + lean * (height * 0.68f);
            var peak = foot + Vector3.up * height + lean * height;
            float aoFloor = AoFloor;
            AoFloor = 0.8f;
            Color32 mid = Color32.Lerp(root, tip, 0.55f);
            for (int s = 0; s < sides; s++)
            {
                float a0 = spin + s * Mathf.PI * 2f / sides;
                float a1 = spin + (s + 1) * Mathf.PI * 2f / sides;
                var r0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                var r1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                // Clockwise from outside.
                Quad(foot + r0, shoulder + r0 * 0.8f, shoulder + r1 * 0.8f, foot + r1, s % 2 == 0 ? mid : Color32.Lerp(root, mid, 0.5f));
                Tri(shoulder + r0 * 0.8f, peak, shoulder + r1 * 0.8f, tip);
            }
            AoFloor = aoFloor;
        }

        // ------------------------------------------------------------------ flat things

        /// <summary>A flat square of any shape, clockwise as seen from above. Each corner has its own colour.</summary>
        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            int ia = Vertex(a, Vector3.zero, ca);
            int ib = Vertex(b, Vector3.zero, cb);
            int ic = Vertex(c, Vector3.zero, cc);
            int id = Vertex(d, Vector3.zero, cd);
            _triangles.Add(ia); _triangles.Add(ib); _triangles.Add(ic);
            _triangles.Add(ia); _triangles.Add(ic); _triangles.Add(id);
        }

        /// <summary>A flat rectangle, its middle at (x, z), turned by yaw degrees.</summary>
        public void FlatRect(float y, float x, float z, float width, float depth, float yaw, Color32 color)
        {
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            float hw = width * 0.5f, hd = depth * 0.5f;
            Vector3 P(float px, float pz) => new Vector3(x + px * c + pz * s, y, z - px * s + pz * c);
            FlatQuad(P(-hw, -hd), P(-hw, hd), P(hw, hd), P(hw, -hd), color, color, color, color);
        }

        /// <summary>A flat disc, its colour running from the middle to the edge. An edge with alpha 0 makes it soft.</summary>
        public void FlatDisc(float y, float x, float z, float radiusX, float radiusZ, Color32 middle, Color32 edge, int segments = 12, float yaw = 0f)
        {
            int centre = Vertex(new Vector3(x, y, z), Vector3.zero, middle);
            int first = _positions.Count;
            float c = Mathf.Cos(yaw * Mathf.Deg2Rad), s = Mathf.Sin(yaw * Mathf.Deg2Rad);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float px = Mathf.Cos(a) * radiusX, pz = Mathf.Sin(a) * radiusZ;
                Vertex(new Vector3(x + px * c + pz * s, y, z - px * s + pz * c), Vector3.zero, edge);
            }
            for (int i = 0; i < segments; i++)
            {
                _triangles.Add(centre);
                _triangles.Add(first + (i + 1) % segments);
                _triangles.Add(first + i);
            }
        }

        /// <summary>A flat ring or part of one, between two radii and two angles in degrees.</summary>
        public void FlatRing(float y, float x, float z, float inner, float outer, Color32 color, int segments = 48, float from = 0f, float to = 360f, Color32? outerColor = null)
        {
            int first = _positions.Count;
            Color32 far = outerColor ?? color;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(from, to, i / (float)segments) * Mathf.Deg2Rad;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                Vertex(new Vector3(x + ca * inner, y, z + sa * inner), Vector3.zero, color);
                Vertex(new Vector3(x + ca * outer, y, z + sa * outer), Vector3.zero, far);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = first + i * 2, b = a + 1, c = a + 3, d = a + 2;
                _triangles.Add(a); _triangles.Add(c); _triangles.Add(b);
                _triangles.Add(a); _triangles.Add(d); _triangles.Add(c);
            }
        }

        /// <summary>
        /// A flat ribbon along points (x, z), its width running from start to end. soft fades both of its edges out; otherwise it is hard-edged.
        /// </summary>
        public void FlatRibbon(float y, IList<Vector2> points, float widthStart, float widthEnd, Color32 color, bool soft = false, Color32? endColor = null)
        {
            int count = points.Count;
            if (count < 2) return;
            Color32 last = endColor ?? color;
            int across = soft ? 4 : 2;
            int first = _positions.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 along = i == 0 ? points[1] - points[0] : i == count - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1];
                along.Normalize();
                var side = new Vector2(along.y, -along.x);
                float t = i / (float)(count - 1);
                float half = Mathf.Lerp(widthStart, widthEnd, t) * 0.5f;
                Color32 c = Color32.Lerp(color, last, t);
                Vector2 p = points[i];
                if (soft)
                {
                    var clear = new Color32(c.r, c.g, c.b, 0);
                    Vertex(Flat(p - side * half, y), Vector3.zero, clear);
                    Vertex(Flat(p - side * (half * 0.3f), y), Vector3.zero, c);
                    Vertex(Flat(p + side * (half * 0.3f), y), Vector3.zero, c);
                    Vertex(Flat(p + side * half, y), Vector3.zero, clear);
                }
                else
                {
                    Vertex(Flat(p - side * half, y), Vector3.zero, c);
                    Vertex(Flat(p + side * half, y), Vector3.zero, c);
                }
            }
            for (int i = 0; i < count - 1; i++)
            {
                for (int k = 0; k < across - 1; k++)
                {
                    int a = first + i * across + k;
                    int b = a + 1;
                    int d = a + across;
                    int c = d + 1;
                    _triangles.Add(a); _triangles.Add(d); _triangles.Add(c);
                    _triangles.Add(a); _triangles.Add(c); _triangles.Add(b);
                }
            }
        }

        private static Vector3 Flat(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        // ------------------------------------------------------------------ light

        /// <summary>A soft round glow in the air around a world point, tilted to face the fixed battle camera.</summary>
        public void Halo(Vector3 centre, float radius, Color32 color, int segments = 8)
        {
            Vector3 right = Vector3.right * radius;
            Vector3 up = new Vector3(0f, 0.57f, 0.82f) * radius; // Up along the screen at the battle camera's pitch.
            Color32 clear = new Color32(color.r, color.g, color.b, 0);
            int middle = Vertex(centre, Vector3.zero, color);
            int first = _positions.Count;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vertex(centre + right * Mathf.Cos(a) + up * Mathf.Sin(a), Vector3.zero, clear);
            }
            for (int i = 0; i < segments; i++)
            {
                _triangles.Add(middle);
                _triangles.Add(first + (i + 1) % segments);
                _triangles.Add(first + i);
            }
        }

        /// <summary>A tiny drifting speck: a spore or a mote of dust. drift = (how far sideways, how far up and down, how fast).</summary>
        public void Mote(Vector3 position, float size, Color32 color, Vector3 drift, float phase)
        {
            Uv = new Vector2(0.4f, phase);
            Vector3 r = new Vector3(size, 0f, 0f);
            Vector3 u = new Vector3(0f, 0.57f, 0.82f) * size;
            var clear = new Color32(color.r, color.g, color.b, 0);
            int middle = Vertex(position, drift, color);
            int a = Vertex(position + r, drift, clear);
            int b = Vertex(position + u, drift, clear);
            int c = Vertex(position - r, drift, clear);
            int d = Vertex(position - u, drift, clear);
            _triangles.Add(middle); _triangles.Add(b); _triangles.Add(a);
            _triangles.Add(middle); _triangles.Add(c); _triangles.Add(b);
            _triangles.Add(middle); _triangles.Add(d); _triangles.Add(c);
            _triangles.Add(middle); _triangles.Add(a); _triangles.Add(d);
            Uv = Vector2.zero;
        }
    }
}
