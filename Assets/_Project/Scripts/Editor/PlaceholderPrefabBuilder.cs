// PlaceholderPrefabBuilder.cs
// Creates a placeholder prefab for every unit asset in Data/Units/Placeholder that has no prefab
// yet, and assigns it to the asset's Prefab field (Bible Section 4, rule 1: a unit is a data asset
// plus a prefab). Each prefab is one Unity primitive sized from the unit's collision radius:
// capsule for infantry, box for vehicles, flattened sphere for air units. Swapping in real art
// later only means assigning a different prefab to the unit asset.
//   Menu: Halobellum > Placeholders > Create Missing Placeholder Prefabs
//   Command line: -executeMethod Halobellum.EditorTools.PlaceholderPrefabBuilder.BuildFromCommandLine
// Depends on: HeadlessRunnerTool (unit loading), BattleSceneBuilder (material path),
//             Halobellum.Presentation (PlaceholderArt), Halobellum.Units, UnityEditor.

using System;
using System.IO;
using Halobellum.Core;
using Halobellum.Presentation;
using Halobellum.Units;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Halobellum.EditorTools
{
    public static class PlaceholderPrefabBuilder
    {
        public const string PrefabFolder = "Assets/_Project/Prefabs/Units";

        [MenuItem("Halobellum/Placeholders/Create Missing Placeholder Prefabs")]
        public static void BuildFromMenu()
        {
            int created = Build();
            Debug.Log($"Created {created} placeholder prefab(s) in {PrefabFolder}.");
        }

        public static void BuildFromCommandLine()
        {
            int exitCode = 0;
            try { Debug.Log($"Created {Build()} placeholder prefab(s)."); }
            catch (Exception e) { Debug.LogError("Creating placeholder prefabs failed: " + e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Creates and assigns prefabs for placeholder units that have none. Returns how many were created.</summary>
        public static int Build()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(BattleSceneBuilder.PlaceholderMaterialPath);
            if (material == null) throw new InvalidOperationException("Missing " + BattleSceneBuilder.PlaceholderMaterialPath + " (rebuild the battle scene first).");

            int created = 0;
            foreach (UnitDefinition unit in HeadlessRunnerTool.LoadUnitsInFolder(HeadlessRunnerTool.PlaceholderFolder))
            {
                if (unit.prefab != null) continue;
                unit.prefab = CreatePrefab(unit, material);
                EditorUtility.SetDirty(unit);
                created++;
            }
            AssetDatabase.SaveAssets();
            return created;
        }

        private static GameObject CreatePrefab(UnitDefinition unit, Material material)
        {
            UnitStats stats = unit.ToStats();
            var root = new GameObject(unit.name);
            try
            {
                Transform mesh = PlaceholderArt.CreatePart("Mesh", root.transform, PlaceholderArt.BodyMesh(stats), material);
                mesh.localScale = PlaceholderArt.BodyScale(stats);
                mesh.localPosition = new Vector3(0f, stats.IsAir ? 0f : PlaceholderArt.BodyHeight(stats) * 0.5f, 0f);

                Directory.CreateDirectory(PrefabFolder);
                string path = $"{PrefabFolder}/{unit.name}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
