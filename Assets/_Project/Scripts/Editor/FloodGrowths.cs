// FloodGrowths.cs
// Flood growths on infested vehicles (decision D37: they differed from the clean ones only by a green tint). The pieces are the owner's
// Meshy growths, prepared by Tools/Art/units/Growths.py: big pods split open at the top (Growth_Big_*) and small bubbles (Growth_Small_*),
// each standing up on its own origin, where it meets a surface. RealModelBuilder calls this while it builds an infested vehicle's prefab:
// a few clumps, each a big pod or two ringed with bubbles, are set where rays from above meet the vehicle's own surface, tilted to it.
// Clumps spread over the vehicle, and how many and how big follow its size, so the same works on whatever body a vehicle has. Wheels are
// left bare; a pod on a turret is parented to it and turns with it. The choice is the same on every build (seeded by the unit's name).
// Depends on: UnityEngine (an editor tool: it reads meshes, which a built game can't).

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Halobellum.EditorTools
{
    public static class FloodGrowths
    {
        /// <summary>The prepared growths' folder in Art/Imported/Models.</summary>
        public const string Folder = "Growths";

        /// <summary>A big pod across, as a share of the vehicle's longest side, and the least and most in metres.</summary>
        private const float PodShare = 0.12f, LeastPod = 0.5f, MostPod = 1.3f;
        /// <summary>Of the footprint, how far in from each edge rays are cast: the edges are mostly curved sides.</summary>
        private const float Inset = 0.15f;
        private const int Grid = 9;
        /// <summary>How steep a surface may be, as the up part of its normal: a clump's middle wants a flattish top, a bubble less so.</summary>
        private const float FlatForClump = 0.45f, FlatForBubble = 0.2f;

        private struct Hit
        {
            public Vector3 Point, Normal;
            public Transform Part;
        }

        /// <summary>How many pods it set: big and small.</summary>
        public struct Result
        {
            public int Big, Small, Triangles;
        }

        /// <summary>Sets growths on the vehicle under root (built, scaled and standing on the ground), from the pieces under the growths' model.</summary>
        public static Result AddTo(GameObject root, GameObject pieces, string unitName)
        {
            var result = new Result();
            var big = new List<Transform>();
            var small = new List<Transform>();
            foreach (MeshRenderer renderer in pieces.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name.StartsWith("Growth_Big_")) big.Add(renderer.transform);
                else if (renderer.name.StartsWith("Growth_Small_")) small.Add(renderer.transform);
            }
            big.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            small.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (big.Count == 0 || small.Count == 0) throw new InvalidOperationException("The growths' model has no Growth_Big_ or Growth_Small_ pieces.");
            float largest = 0f;
            foreach (Transform piece in big) largest = Mathf.Max(largest, Size(piece));

            var surface = new Surface(root);
            Bounds bounds = surface.Bounds;
            float length = Mathf.Max(bounds.size.x, bounds.size.z);
            float pod = Mathf.Clamp(length * PodShare, LeastPod, MostPod);
            float factor = pod / largest;
            int clumps = length < 5f ? 3 : length < 8f ? 4 : 5;
            var random = new Random(Seed(unitName));

            // Where a clump can go: a grid of rays from above, kept where they meet a flattish top.
            var spots = new List<Hit>();
            for (int i = 0; i < Grid; i++)
            {
                for (int j = 0; j < Grid; j++)
                {
                    float x = Mathf.Lerp(bounds.min.x + bounds.size.x * Inset, bounds.max.x - bounds.size.x * Inset, i / (Grid - 1f));
                    float z = Mathf.Lerp(bounds.min.z + bounds.size.z * Inset, bounds.max.z - bounds.size.z * Inset, j / (Grid - 1f));
                    if (surface.CastDown(x, z, out Hit hit) && hit.Normal.y >= FlatForClump) spots.Add(hit);
                }
            }
            if (spots.Count == 0) return result;

            // The clumps spread out: the first anywhere, each next as far as can be from those before it.
            var chosen = new List<Hit> { spots[random.Next(spots.Count)] };
            while (chosen.Count < clumps && chosen.Count < spots.Count)
            {
                Hit furthest = spots[0];
                float best = -1f;
                foreach (Hit spot in spots)
                {
                    float nearest = float.MaxValue;
                    foreach (Hit taken in chosen) nearest = Mathf.Min(nearest, Vector3.Distance(spot.Point, taken.Point));
                    if (nearest > best)
                    {
                        best = nearest;
                        furthest = spot;
                    }
                }
                chosen.Add(furthest);
            }

            foreach (Hit centre in chosen)
            {
                Set(big[random.Next(big.Count)], centre, factor * Between(random, 0.85f, 1.1f), random, ref result.Triangles);
                result.Big++;
                int bubbles = 3 + random.Next(4);
                for (int b = 0; b < bubbles; b++)
                {
                    float angle = (float)(random.NextDouble() * Math.PI * 2.0), reach = pod * Between(random, 0.45f, 0.95f);
                    Vector3 at = centre.Point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach;
                    if (!surface.CastDown(at.x, at.z, out Hit hit) || hit.Normal.y < FlatForBubble || Mathf.Abs(hit.Point.y - centre.Point.y) > pod) continue;
                    // Now and then a second big pod, smaller, in place of a bubble.
                    bool second = b == 0 && random.NextDouble() < 0.5;
                    Transform piece = second ? big[random.Next(big.Count)] : small[random.Next(small.Count)];
                    Set(piece, hit, factor * (second ? Between(random, 0.55f, 0.7f) : Between(random, 0.9f, 1.5f)), random, ref result.Triangles);
                    if (second) result.Big++;
                    else result.Small++;
                }
            }
            return result;
        }

        /// <summary>A copy of a piece, standing out of the surface where the ray met it, turned at random around its own up.</summary>
        private static void Set(Transform piece, Hit hit, float factor, Random random, ref int triangles)
        {
            GameObject copy = Object.Instantiate(piece.gameObject);
            copy.name = "Growth";
            float yaw = (float)(random.NextDouble() * 360.0);
            copy.transform.SetPositionAndRotation(hit.Point, Quaternion.FromToRotation(Vector3.up, hit.Normal) * Quaternion.AngleAxis(yaw, Vector3.up) * piece.rotation);
            copy.transform.localScale = piece.lossyScale * factor;
            copy.transform.SetParent(hit.Part, true);
            triangles += copy.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
        }

        private static float Size(Transform piece)
        {
            Vector3 size = piece.GetComponent<MeshRenderer>().bounds.size;
            return Mathf.Max(size.x, size.y, size.z);
        }

        private static float Between(Random random, float low, float high) => low + (float)random.NextDouble() * (high - low);

        /// <summary>A seed from the unit's name that is the same on every machine and run (string.GetHashCode isn't promised to be).</summary>
        private static int Seed(string name)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in name) hash = (hash ^ c) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        /// <summary>The vehicle's triangles in world space, to cast rays at. Wheels are left out: they spin.</summary>
        private sealed class Surface
        {
            private readonly List<Vector3> _points = new List<Vector3>();
            private readonly List<Transform> _parts = new List<Transform>();
            public Bounds Bounds { get; }

            public Surface(GameObject root)
            {
                bool first = true;
                Bounds bounds = default;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    Renderer renderer = filter.GetComponent<Renderer>();
                    if (renderer == null || filter.sharedMesh == null) continue;
                    if (first) bounds = renderer.bounds;
                    else bounds.Encapsulate(renderer.bounds);
                    first = false;
                    if (OnAWheel(filter.transform)) continue;
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    int[] triangles = filter.sharedMesh.triangles;
                    Matrix4x4 world = filter.transform.localToWorldMatrix;
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        _points.Add(world.MultiplyPoint3x4(vertices[triangles[t]]));
                        _points.Add(world.MultiplyPoint3x4(vertices[triangles[t + 1]]));
                        _points.Add(world.MultiplyPoint3x4(vertices[triangles[t + 2]]));
                        _parts.Add(filter.transform);
                    }
                }
                Bounds = bounds;
            }

            private static bool OnAWheel(Transform part)
            {
                for (Transform t = part; t != null; t = t.parent) if (t.name.StartsWith("Wheel_")) return true;
                return false;
            }

            /// <summary>The highest point of the surface under this spot, its normal facing up, and the part it is on.</summary>
            public bool CastDown(float x, float z, out Hit hit)
            {
                hit = default;
                var origin = new Vector3(x, Bounds.max.y + 1f, z);
                float nearest = float.MaxValue;
                for (int i = 0, t = 0; i < _points.Count; i += 3, t++)
                {
                    Vector3 a = _points[i], b = _points[i + 1], c = _points[i + 2];
                    if (!RayDown(origin, a, b, c, out float distance) || distance >= nearest) continue;
                    nearest = distance;
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    if (normal.y < 0f) normal = -normal;
                    hit = new Hit { Point = origin + Vector3.down * distance, Normal = normal, Part = _parts[t] };
                }
                return nearest < float.MaxValue;
            }

            /// <summary>Where a ray straight down from origin meets the triangle (Moller and Trumbore's test, with the direction fixed).</summary>
            private static bool RayDown(Vector3 origin, Vector3 a, Vector3 b, Vector3 c, out float distance)
            {
                distance = 0f;
                Vector3 direction = Vector3.down;
                Vector3 ab = b - a, ac = c - a;
                Vector3 p = Vector3.Cross(direction, ac);
                float det = Vector3.Dot(ab, p);
                if (Mathf.Abs(det) < 1e-9f) return false;
                float inverse = 1f / det;
                Vector3 s = origin - a;
                float u = Vector3.Dot(s, p) * inverse;
                if (u < 0f || u > 1f) return false;
                Vector3 q = Vector3.Cross(s, ab);
                float v = Vector3.Dot(direction, q) * inverse;
                if (v < 0f || u + v > 1f) return false;
                distance = Vector3.Dot(ac, q) * inverse;
                return distance > 0f;
            }
        }
    }
}
