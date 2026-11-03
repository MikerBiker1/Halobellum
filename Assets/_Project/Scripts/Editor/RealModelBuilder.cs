// RealModelBuilder.cs
// Makes game-ready prefabs from real models that aren't baked for crowds, such as vehicles and big Flood forms (M9,
// decisions D44 and D45). Each comes from a folder in Art/Imported/Models holding what Tools/Art/model_tools.py prepared:
// an FBX, its textures and materials.json. This scales the model to its size in the game, stands it on the ground centred
// on the unit's position (air units on their middle), makes its matte materials, and points the unit's data asset at it.
// The unit then keeps the model's own materials. Sizes in the table below are the place to tune how big a unit looks.
//   Menu: Halobellum > Art > Build Real Model Prefabs
//   Command line: -executeMethod Halobellum.EditorTools.RealModelBuilder.BuildAllFromCommandLine
// Depends on: Halobellum.Units (UnitDefinition), UnityEditor.

using System;
using System.Collections.Generic;
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
        private const string UnitsFolder = "Assets/_Project/Data/Units";

        /// <summary>One prefab: its unit, the model's folder, its size in metres (longest side across the ground, or else its height), and a colour over its own.</summary>
        private sealed class Entry
        {
            public string Unit, Folder;
            public float Length, Height;
            public Color Tint = Color.white;
        }

        private static readonly Color Infested = new Color(0.72f, 0.82f, 0.45f);

        private static readonly Entry[] Models =
        {
            new Entry { Unit = "UNSC/Scorpion", Folder = "Scorpion", Length = 6.2f },
            new Entry { Unit = "UNSC/Elephant", Folder = "Elephant", Length = 10f },
            new Entry { Unit = "UNSC/Warthog", Folder = "Warthog", Length = 4.6f },
            new Entry { Unit = "UNSC/GaussWarthog", Folder = "GaussWarthog", Length = 4.6f },
            new Entry { Unit = "UNSC/Hornet", Folder = "Hornet", Length = 6f },
            new Entry { Unit = "UNSC/Pelican", Folder = "Pelican", Length = 10f },
            new Entry { Unit = "Covenant/Banshee", Folder = "Banshee", Length = 6.6f }, // Its wings are wider than it is long: 5.4 m nose to tail.
            new Entry { Unit = "Covenant/Wraith", Folder = "Wraith", Length = 6.2f },
            new Entry { Unit = "Covenant/Scarab", Folder = "Scarab", Length = 14f },
            new Entry { Unit = "Flood/InfectedPelican", Folder = "Pelican", Length = 10f, Tint = Infested },
            new Entry { Unit = "Flood/InfectedPhantom", Folder = "Phantom", Length = 12f, Tint = Infested },
            // Bigger than life, and lighter than its texture: at its real size a form is a speck from the game's camera.
            new Entry { Unit = "Flood/InfectionForms", Folder = "InfectionForms", Length = 2.2f, Tint = new Color(1.5f, 1.5f, 1.3f) },
            new Entry { Unit = "Flood/CarrierForm", Folder = "CarrierForm", Height = 2.3f },
            // These two have very dark textures, and read as black shapes from the game's camera without a lift.
            new Entry { Unit = "Flood/StalkerForm", Folder = "StalkerForm", Height = 2.4f, Tint = new Color(1.4f, 1.4f, 1.3f) },
            new Entry { Unit = "Flood/PureFormRanged", Folder = "PureFormRanged", Height = 2.9f, Tint = new Color(1.6f, 1.6f, 1.45f) },
            new Entry { Unit = "Flood/PureFormTank", Folder = "PureFormTank", Height = 3.8f },
            // Sub-factions (M10): their own models, and real models over again where that is honest.
            new Entry { Unit = "ArmoredDivision/Grizzly", Folder = "Grizzly", Length = 7.2f },
            new Entry { Unit = "Banished/Chopper", Folder = "Chopper", Length = 4.8f, Tint = new Color(1.4f, 1.35f, 1.25f) }, // A very dark texture: it read as a black shape.
            new Entry { Unit = "AerospaceCommand/CondorGunship", Folder = "Pelican", Length = 11f, Tint = new Color(0.72f, 0.76f, 0.84f) },
            new Entry { Unit = "FloodUNSC/InfestedWarthog", Folder = "Warthog", Length = 4.6f, Tint = Infested },
            new Entry { Unit = "FloodUNSC/InfestedScorpion", Folder = "Scorpion", Length = 6.2f, Tint = Infested },
            new Entry { Unit = "FloodUNSC/InfestedHornet", Folder = "Hornet", Length = 6f, Tint = Infested },
            new Entry { Unit = "FloodCovenant/InfestedWraith", Folder = "Wraith", Length = 6.2f, Tint = Infested },
            new Entry { Unit = "FloodCovenant/InfestedBanshee", Folder = "Banshee", Length = 6.6f, Tint = Infested },
        };

        [Serializable]
        private sealed class MaterialInfo
        {
            public string name;
            public float[] colour;
            public bool glass, cutout;
            public string texture, glowTexture;
            public float[] glow;
        }

        [Serializable]
        private sealed class MaterialList
        {
            public List<MaterialInfo> materials = new List<MaterialInfo>();
        }

        [MenuItem("Halobellum/Art/Build Real Model Prefabs")]
        public static void BuildAll()
        {
            foreach (Entry entry in Models)
            {
                // A unit whose model hasn't come in yet stays as it is; a unit baked for crowds doesn't use a prefab.
                if (AssetDatabase.IsValidFolder($"{AnimationBaker.ModelsFolder}/{entry.Folder}")) Build(entry);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Builds only what has no real prefab yet, leaving the others as they are (M10).</summary>
        public static void BuildNewFromCommandLine()
        {
            int exit = 0;
            try
            {
                foreach (Entry entry in Models)
                {
                    var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"{UnitsFolder}/{entry.Unit}.asset");
                    if (unit != null && unit.prefab != null && unit.prefab.name.EndsWith("_Model")) continue;
                    if (AssetDatabase.IsValidFolder($"{AnimationBaker.ModelsFolder}/{entry.Folder}")) Build(entry);
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError("Building real models failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        public static void BuildAllFromCommandLine()
        {
            int exit = 0;
            try { BuildAll(); }
            catch (Exception e) { Debug.LogError("Building real models failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        private static void Build(Entry entry)
        {
            string assetPath = $"{UnitsFolder}/{entry.Unit}.asset";
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(assetPath);
            if (unit == null) throw new InvalidOperationException("No unit at " + assetPath);
            string unitName = Path.GetFileNameWithoutExtension(assetPath);
            string folder = $"{AnimationBaker.ModelsFolder}/{entry.Folder}";
            string fbx = $"{folder}/{entry.Folder}.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) return; // Rigged and baked instead.

            // The materials live inside the model, not in a folder Unity extracts next to it.
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
            {
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }

            string json = $"{folder}/materials.json";
            MaterialList info = File.Exists(json) ? JsonUtility.FromJson<MaterialList>(File.ReadAllText(json)) : new MaterialList();

            var root = new GameObject(unitName + "_Model");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
                model.transform.SetParent(root.transform, false);

                Bounds bounds = BoundsOf(root);
                float scale = entry.Height > 0f ? entry.Height / bounds.size.y : entry.Length / Mathf.Max(bounds.size.x, bounds.size.z);
                model.transform.localScale *= scale;
                bounds = BoundsOf(root);
                float baseHeight = unit.isAir ? bounds.center.y : bounds.min.y;
                model.transform.localPosition -= new Vector3(bounds.center.x, baseHeight, bounds.center.z);

                string materials = $"{folder}/GameMaterials";
                if (!AssetDatabase.IsValidFolder(materials)) AssetDatabase.CreateFolder(folder, "GameMaterials");
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Matte(m, info, folder, materials, entry.Tint, entry.Tint != Color.white ? "_" + unitName : "")).ToArray();
                }

                string path = $"{PrefabFolder}/{unitName}_Model.prefab";
                unit.prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                unit.keepsOwnMaterials = true;
                EditorUtility.SetDirty(unit);
                bounds = BoundsOf(root);
                Debug.Log($"Built {path}: size {bounds.size}, {root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3)} triangles in {root.GetComponentsInChildren<MeshRenderer>().Length} parts");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A matte material from what materials.json says: as imported they are shiny enough to mirror the pale sky,
        /// which turns paint steel grey. Glass stays shiny.
        /// </summary>
        private static Material Matte(Material imported, MaterialList list, string folder, string materials, Color tint, string suffix)
        {
            string path = $"{materials}/{imported.name}{suffix}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = imported.name + suffix };

            MaterialInfo info = list.materials.FirstOrDefault(m => m.name == imported.name);
            bool glass = info?.glass ?? false;
            Color colour = info?.colour != null && info.colour.Length == 3 ? new Color(info.colour[0], info.colour[1], info.colour[2]).gamma : imported.color;
            material.SetColor("_BaseColor", new Color(colour.r * tint.r, colour.g * tint.g, colour.b * tint.b, 1f));
            if (!string.IsNullOrEmpty(info?.texture)) material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{info.texture}"));
            material.SetFloat("_Smoothness", glass ? 0.8f : 0.15f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_SpecularHighlights", glass ? 1f : 0f);
            material.SetFloat("_EnvironmentReflections", glass ? 1f : 0f);

            if (info != null && info.cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.SetFloat("_Cull", 0f);
                material.EnableKeyword("_ALPHATEST_ON");
            }
            bool hasGlowColour = info?.glow != null && info.glow.Length == 3;
            if (!string.IsNullOrEmpty(info?.glowTexture) || hasGlowColour)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", hasGlowColour ? new Color(info.glow[0], info.glow[1], info.glow[2]) : Color.white);
                if (!string.IsNullOrEmpty(info.glowTexture)) material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{info.glowTexture}"));
            }

            if (isNew) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
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
