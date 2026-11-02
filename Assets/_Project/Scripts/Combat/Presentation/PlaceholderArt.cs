// PlaceholderArt.cs
// Placeholder visuals made from Unity's built-in primitives (Bible Section 12: colored primitives,
// one color per side, size from the unit's data). Provides shared meshes, shared materials and the
// body shape and size for a unit, so hundreds of bodies share a few materials and render cheaply
// (URP's SRP Batcher).
// Shapes: unarmored bodies are capsules (infantry), armored bodies are boxes (vehicles), air units
// are flattened spheres. Size follows the unit's collision radius.
// Depends on: Halobellum.Core (UnitStats), UnityEngine.

using System.Collections.Generic;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class PlaceholderArt
    {
        /// <summary>How far out the hole of the selection ring reaches, as a share of the ring's radius.</summary>
        public const float RingHoleShare = 0.72f;

        private static Mesh _capsule, _cube, _sphere, _quad, _cylinder, _ring;
        private static Material _baseMaterial;
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        public static Mesh Capsule { get { Load(); return _capsule; } }
        public static Mesh Cube { get { Load(); return _cube; } }
        public static Mesh Sphere { get { Load(); return _sphere; } }
        public static Mesh Quad { get { Load(); return _quad; } }
        public static Mesh Cylinder { get { Load(); return _cylinder; } }
        /// <summary>A flat ring 1 unit across, lying on its XZ plane and facing up.</summary>
        public static Mesh Ring => _ring != null ? _ring : (_ring = MakeRing(48));

        private static Mesh MakeRing(int segments)
        {
            var vertices = new Vector3[segments * 2];
            var normals = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var way = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.5f;
                vertices[i * 2] = way;
                vertices[i * 2 + 1] = way * RingHoleShare;
                normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                int next = (i + 1) % segments;
                int t = i * 6;
                triangles[t] = i * 2;
                triangles[t + 1] = i * 2 + 1;
                triangles[t + 2] = next * 2;
                triangles[t + 3] = next * 2;
                triangles[t + 4] = i * 2 + 1;
                triangles[t + 5] = next * 2 + 1;
            }
            var mesh = new Mesh { name = "Selection Ring", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A shared material of this color (one per color, reused by every caller).</summary>
        public static Material MaterialFor(Color color)
        {
            Load();
            if (Materials.TryGetValue(color, out Material material) && material != null) return material;
            // No GPU instancing flag: URP batches these with the SRP Batcher, and an instancing shader
            // variant would be stripped from builds because no material asset in the project uses it.
            material = new Material(_baseMaterial) { name = "Placeholder " + ColorUtility.ToHtmlStringRGB(color), color = color };
            Materials[color] = material;
            return material;
        }

        public static Mesh BodyMesh(UnitStats stats)
        {
            if (stats.IsAir) return Sphere;
            return stats.IsFlood || stats.ArmorType == ArmorType.Unarmored ? Capsule : Cube; // Flood: all flesh, no boxes.
        }

        /// <summary>Local scale of the body mesh, so its footprint matches the collision radius.</summary>
        public static Vector3 BodyScale(UnitStats stats)
        {
            float r = stats.CollisionRadius;
            if (stats.IsAir) return new Vector3(2f * r, 0.6f * r, 2f * r);
            if (stats.IsFlood) return new Vector3(2f * r, (stats.Role == UnitRole.Titan ? 2.4f : 1.3f) * r, 2f * r); // The Abomination towers.
            switch (stats.ArmorType)
            {
                case ArmorType.Unarmored: return new Vector3(2f * r, 1.8f * r, 2f * r); // Capsule mesh is 2 units tall: 3.6 x radius.
                case ArmorType.Light: return new Vector3(1.4f * r, 0.9f * r, 2f * r);
                default: return new Vector3(1.6f * r, 1.1f * r, 2f * r);
            }
        }

        /// <summary>Height of the top of the body above its base, for placing health bars.</summary>
        public static float BodyHeight(UnitStats stats)
        {
            Vector3 scale = BodyScale(stats);
            return BodyMesh(stats) == Capsule ? scale.y * 2f : scale.y;
        }

        /// <summary>Creates a child object with a mesh and shared material, without a collider.</summary>
        public static Transform CreatePart(string name, Transform parent, Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
        }

        /// <summary>
        /// The URP Lit material asset every placeholder material is copied from (set by the
        /// BattleController from the scene). A real asset is needed: in a built game, Unity's
        /// primitives come with a built-in material that URP cannot draw (it shows magenta).
        /// </summary>
        public static void SetBaseMaterial(Material material)
        {
            if (material == null || material == _baseMaterial) return;
            _baseMaterial = material;
            Materials.Clear();
        }

        private static void Load()
        {
            if (_capsule != null && _baseMaterial != null) return;
            _capsule = TakeMesh(PrimitiveType.Capsule, out Material primitiveMaterial);
            if (_baseMaterial == null) _baseMaterial = primitiveMaterial; // Fallback (fine in the editor).
            _cube = TakeMesh(PrimitiveType.Cube, out _);
            _sphere = TakeMesh(PrimitiveType.Sphere, out _);
            _quad = TakeMesh(PrimitiveType.Quad, out _);
            _cylinder = TakeMesh(PrimitiveType.Cylinder, out _);
        }

        // Unity's primitives come with the render pipeline's default material; borrow both, then
        // throw the temporary object away.
        private static Mesh TakeMesh(PrimitiveType type, out Material material)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            material = temp.GetComponent<MeshRenderer>().sharedMaterial;
            if (Application.isPlaying) Object.Destroy(temp);
            else Object.DestroyImmediate(temp);
            return mesh;
        }
    }
}
