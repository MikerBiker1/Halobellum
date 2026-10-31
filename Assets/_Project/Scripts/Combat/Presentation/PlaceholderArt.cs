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
        private static Mesh _capsule, _cube, _sphere, _quad, _cylinder;
        private static Material _baseMaterial;
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        public static Mesh Capsule { get { Load(); return _capsule; } }
        public static Mesh Cube { get { Load(); return _cube; } }
        public static Mesh Sphere { get { Load(); return _sphere; } }
        public static Mesh Quad { get { Load(); return _quad; } }
        public static Mesh Cylinder { get { Load(); return _cylinder; } }

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
            return stats.ArmorType == ArmorType.Unarmored ? Capsule : Cube;
        }

        /// <summary>Local scale of the body mesh, so its footprint matches the collision radius.</summary>
        public static Vector3 BodyScale(UnitStats stats)
        {
            float r = stats.CollisionRadius;
            if (stats.IsAir) return new Vector3(2f * r, 0.6f * r, 2f * r);
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
