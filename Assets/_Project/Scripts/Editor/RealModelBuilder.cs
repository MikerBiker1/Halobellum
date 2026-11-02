// RealModelBuilder.cs
// Makes a game-ready prefab from a real model that isn't animated, such as a vehicle (M9, decision D44): it finds
// the FBX in Art/Imported/Models/<unit>, scales it to its length in the game, stands it on the ground centred on
// the unit's position, and points the unit's data asset at it. The unit then keeps the model's own materials.
//   Menu: Halobellum > Art > Build Real Model Prefabs
//   Command line: -executeMethod Halobellum.EditorTools.RealModelBuilder.BuildAllFromCommandLine
// Depends on: Halobellum.Units (UnitDefinition), UnityEditor.

using System;
using System.IO;
using System.Linq;
using Halobellum.Units;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Halobellum.EditorTools
{
    public static class RealModelBuilder
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Units";

        /// <summary>The unit's data asset, its length in the game in metres, and any turn needed so that it faces forward.</summary>
        private static readonly (string asset, float length, float yaw)[] Models =
        {
            ("Assets/_Project/Data/Units/UNSC/Scorpion.asset", 6.2f, 0f),
        };

        [MenuItem("Halobellum/Art/Build Real Model Prefabs")]
        public static void BuildAll()
        {
            foreach (var (asset, length, yaw) in Models) Build(asset, length, yaw);
            AssetDatabase.SaveAssets();
        }

        public static void BuildAllFromCommandLine()
        {
            int exit = 0;
            try { BuildAll(); }
            catch (Exception e) { Debug.LogError("Building real models failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        private static void Build(string assetPath, float length, float yaw)
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(assetPath);
            string unitName = Path.GetFileNameWithoutExtension(assetPath);
            string fbx = Directory.GetFiles($"{AnimationBaker.ModelsFolder}/{unitName}", "*.fbx").First().Replace('\\', '/');

            // The materials live inside the model, not in a folder Unity extracts next to it.
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
            {
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }

            var root = new GameObject(unitName + "_Model");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                model.transform.SetParent(root.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

                Bounds bounds = BoundsOf(root);
                model.transform.localScale *= length / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = BoundsOf(root);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

                string materials = $"{Path.GetDirectoryName(fbx).Replace('\\', '/')}/GameMaterials";
                if (!AssetDatabase.IsValidFolder(materials)) AssetDatabase.CreateFolder(Path.GetDirectoryName(fbx).Replace('\\', '/'), "GameMaterials");
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Matte(m, materials)).ToArray();
                }

                string path = $"{PrefabFolder}/{unitName}_Model.prefab";
                unit.prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                unit.keepsOwnMaterials = true;
                EditorUtility.SetDirty(unit);
                bounds = BoundsOf(root);
                Debug.Log($"Built {path}: size {bounds.size}, materials " + string.Join(", ", root.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct().Select(m => $"{m.name} {m.shader.name} {m.color}")));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A matte copy of an imported material: as imported they are shiny enough to mirror the pale sky,
        /// which turns olive paint steel grey. Glass stays shiny. Paint is brightened a little to read at the game camera.
        /// </summary>
        private static Material Matte(Material imported, string folder)
        {
            string path = $"{folder}/{imported.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            bool glass = imported.name.Contains("glass");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = imported.name };
            Color colour = imported.color;
            material.SetColor("_BaseColor", glass ? colour : new Color(colour.r * 1.6f, colour.g * 1.6f, colour.b * 1.6f, 1f));
            material.SetFloat("_Smoothness", glass ? 0.8f : 0.15f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", glass ? 1f : 0f);
            material.SetFloat("_EnvironmentReflections", glass ? 1f : 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Bounds BoundsOf(GameObject root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
