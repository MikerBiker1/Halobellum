// RealModelBuilder.cs
// Makes game-ready prefabs from real models that aren't baked for crowds, such as vehicles and big Flood forms (M9,
// decisions D44 and D45). Each comes from a folder in Art/Imported/Models holding what Tools/Art/model_tools.py prepared:
// an FBX, its textures and materials.json. This scales the model to its size in the game, stands it on the ground centred
// on the unit's position (air units on their middle), makes its matte materials, and points the unit's data asset at it.
// The unit then keeps the model's own materials. Sizes in the table below are the place to tune how big a unit looks.
// The two towers (D55) aren't units: each becomes a prefab in Resources/Towers, named after its kind, which TowerArt finds there.
// An infested vehicle (one tinted Infested) also gets Flood growths on its surface (FloodGrowths, decision D37), and wears its textures' Flood copies,
// <texture>_Flood.png, with beige tendrils baked over the paint (Tools/Art/flood_tendrils.py, D82) instead of a green wash.
// Every model's glow (its glow texture or colour in materials.json) is on, except on the infested vehicles (D82). An entry with Thrusters gets a
// Thruster_<size> node at the back of each of its outermost parts (the Banshee's wing tips, the Ghost's fins), which ModelParts lights while it moves.
// An entry with a Prop holds that weapon model (a Weapon_ folder) at its Muzzle_1, which moves to the weapon's tip (the Infested Elite's plasma rifle).
// An entry with a Mouth gets a green light inside the mouth its Muzzle_0 is in, MouthGlow, and the two places its sides glow from, Mandible_0
// and _1, which CombatEffectsView lights as it charges and fires its beam (the Scarab, D97).
//   Menu: Halobellum > Art > Build Real Model Prefabs
//   Command line: -executeMethod Halobellum.EditorTools.RealModelBuilder.BuildAllFromCommandLine (or BuildTowersFromCommandLine for the towers alone,
//   BuildInfestedFromCommandLine for the infested vehicles alone, BuildUnitsFromCommandLine for the units named in HB_UNITS or after -units,
//   comma separated, such as Covenant/Scarab)
// Depends on: FloodGrowths, Halobellum.Units (UnitDefinition), UnityEditor.

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
        public const string TowerFolder = "Assets/_Project/Resources/Towers";

        /// <summary>
        /// The towers: the model's folder, named as its TowerKind, its longest side across the ground (a tower has 3 squares of 2 m), and how much its
        /// texture is lifted. The Command Center's gunmetal reads as a black shape from the game's camera without one.
        /// </summary>
        private static readonly (string Folder, float Length, float Lift)[] Towers = { ("CommandCenter", 5.8f, 1.6f), ("ResearchCenter", 5.8f, 1.25f) };

        /// <summary>One prefab: its unit, the model's folder, its size in metres (longest side across the ground, or else its height), and a colour over its own.</summary>
        private sealed class Entry
        {
            public string Unit, Folder;
            public float Length, Height;
            public Color Tint = Color.white;
            /// <summary>Glowing thrusters at the back of the outermost parts, lit while moving (the owner, 2026-11-07, D82).</summary>
            public bool Thrusters;
            /// <summary>A weapon held at Muzzle_1, a Weapon_ folder in Art/Imported/Models, and its length in metres.</summary>
            public string Prop;
            public float PropLength;
            /// <summary>Degrees the model is turned about the vertical, for one prepared facing the wrong way (the Combat Form: the owner, 2026-11-07).</summary>
            public float Yaw;
            /// <summary>A mouth that glows green inside as its beam charges and fires (the Scarab: the owner, 2026-11-07, D97).</summary>
            public bool Mouth;
        }

        /// <summary>
        /// The infested vehicles' tint, now only a little warmer: their textures' Flood copies carry the infestation (D82). Before, a green wash
        /// (0.72, 0.82, 0.45) that the owner turned down. Parts with no texture (glass, dark trim) take it as it is.
        /// </summary>
        private static readonly Color Infested = new Color(0.96f, 0.93f, 0.88f);
        /// <summary>The Phantom's texture is grey: a Covenant purple over it, for the Covenant's own (the Flood's is Infested).</summary>
        private static readonly Color CovenantHull = new Color(0.9f, 0.72f, 1.2f);
        /// <summary>The growths keep their own texture, a little greener, and lifted: as it is, the dark brown reads as black from the game's camera.</summary>
        private static readonly Color GrowthTint = new Color(1.15f, 1.25f, 0.85f);

        private static readonly Entry[] Models =
        {
            new Entry { Unit = "UNSC/Scorpion", Folder = "Scorpion", Length = 6.2f },
            new Entry { Unit = "UNSC/Elephant", Folder = "Elephant", Length = 10f },
            new Entry { Unit = "UNSC/Warthog", Folder = "Warthog", Length = 4.6f },
            new Entry { Unit = "UNSC/GaussWarthog", Folder = "GaussWarthog", Length = 4.6f },
            new Entry { Unit = "UNSC/Hornet", Folder = "Hornet", Length = 6f },
            new Entry { Unit = "UNSC/Pelican", Folder = "Pelican", Length = 10f },
            new Entry { Unit = "Covenant/Banshee", Folder = "Banshee", Length = 6.6f, Thrusters = true }, // Its wings are wider than it is long: 5.4 m nose to tail.
            new Entry { Unit = "Covenant/Wraith", Folder = "Wraith", Length = 6.2f },
            new Entry { Unit = "Covenant/Scarab", Folder = "Scarab", Length = 14f, Mouth = true },
            new Entry { Unit = "Flood/InfectedPelican", Folder = "Pelican", Length = 10f, Tint = Infested },
            new Entry { Unit = "Flood/InfectedPhantom", Folder = "Phantom", Length = 12f, Tint = Infested },
            new Entry { Unit = "Covenant/CovenantPhantom", Folder = "Phantom", Length = 12f, Tint = CovenantHull },
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
            new Entry { Unit = "AerospaceCommand/CondorGunship", Folder = "Condor", Length = 11f },
            new Entry { Unit = "FloodUNSC/InfestedWarthog", Folder = "Warthog", Length = 4.6f, Tint = Infested },
            new Entry { Unit = "FloodUNSC/InfestedScorpion", Folder = "Scorpion", Length = 6.2f, Tint = Infested },
            new Entry { Unit = "FloodUNSC/InfestedHornet", Folder = "Hornet", Length = 6f, Tint = Infested },
            new Entry { Unit = "FloodCovenant/InfestedWraith", Folder = "Wraith", Length = 6.2f, Tint = Infested },
            new Entry { Unit = "FloodCovenant/InfestedBanshee", Folder = "Banshee", Length = 6.6f, Tint = Infested, Thrusters = true },
            // The owner's second batch (M11).
            new Entry { Unit = "UNSC/ArcherMissilePod", Folder = "ArcherMissilePod", Height = 3.6f },
            new Entry { Unit = "ArmoredDivision/Cobra", Folder = "Cobra", Length = 6f },
            new Entry { Unit = "ArmoredDivision/Rhino", Folder = "Rhino", Length = 9.5f },
            new Entry { Unit = "AerospaceCommand/Sparrowhawk", Folder = "Sparrowhawk", Length = 7f },
            new Entry { Unit = "AerospaceCommand/Vulture", Folder = "Vulture", Length = 10f },
            new Entry { Unit = "AerospaceCommand/Longsword", Folder = "Longsword", Length = 11f },
            new Entry { Unit = "Covenant/Ghost", Folder = "Ghost", Length = 4.4f, Thrusters = true },
            new Entry { Unit = "FloodCovenant/InfestedGhost", Folder = "Ghost", Length = 4.4f, Tint = Infested, Thrusters = true },
            new Entry { Unit = "LekgoloColony/Skitterer", Folder = "Skitterer", Length = 2.6f },
            new Entry { Unit = "Flood/SwarmForm", Folder = "SwarmForm", Length = 3f },
            // The owner's third batch (M11). Radii, for comparison: Mantis 2, Wolverine 1.4, Marauder 1.8, Reaver 1.4, Vampire 1.8, Seeder 1.6, Spawner 2, Juggernaut 1.8, Combat Form 0.6, Goliath 2.4.
            new Entry { Unit = "UNSC/Mantis", Folder = "Mantis", Height = 6f, Tint = new Color(1.3f, 1.3f, 1.3f) }, // A dark texture, between the Chopper's and the Cobra's.
            new Entry { Unit = "UNSC/Wolverine", Folder = "Wolverine", Length = 5.5f },
            new Entry { Unit = "Banished/Marauder", Folder = "Marauder", Length = 5.8f },
            new Entry { Unit = "Banished/Reaver", Folder = "Reaver", Height = 5f },
            new Entry { Unit = "Covenant/Vampire", Folder = "Vampire", Length = 7.5f },
            new Entry { Unit = "Flood/Seeder", Folder = "Seeder", Length = 8f },
            new Entry { Unit = "Flood/CombatForm", Folder = "CombatForm", Height = 2.6f, Yaw = 180f }, // It came out of its preparation facing backwards.
            // The owner, 2026-11-07 (D82): the Infested Elite is the Combat Form, an infested Elite; it was the Elite's body washed green. It holds its plasma rifle.
            new Entry { Unit = "FloodCovenant/InfestedElite", Folder = "CombatForm", Height = 2.6f, Prop = "Weapon_PlasmaRifle", PropLength = 0.85f, Yaw = 180f },
            new Entry { Unit = "FloodCovenant/Spawner", Folder = "Spawner", Length = 9f },
            new Entry { Unit = "FloodUNSC/Juggernaut", Folder = "Juggernaut", Length = 8f },
            new Entry { Unit = "LekgoloColony/Goliath", Folder = "Goliath", Height = 6f },
            // The owner's fourth batch (M12). The Gremlin's 2.2 m width matches its radius of 1.1.
            new Entry { Unit = "ArmoredDivision/Gremlin", Folder = "Gremlin", Length = 4.6f },
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
            foreach ((string folder, float length, float lift) in Towers)
            {
                if (AssetDatabase.IsValidFolder($"{AnimationBaker.ModelsFolder}/{folder}")) BuildTower(folder, length, lift);
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

        /// <summary>Only the two towers' prefabs.</summary>
        public static void BuildTowersFromCommandLine()
        {
            int exit = 0;
            try
            {
                foreach ((string folder, float length, float lift) in Towers) BuildTower(folder, length, lift);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError("Building the towers failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        /// <summary>Only the units named in the environment's HB_UNITS or after -units on the command line, comma separated (Covenant/Scarab, D84).</summary>
        public static void BuildUnitsFromCommandLine()
        {
            int exit = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-units");
                string named = at >= 0 && at + 1 < args.Length ? args[at + 1] : Environment.GetEnvironmentVariable("HB_UNITS");
                if (string.IsNullOrEmpty(named)) throw new ArgumentException("Name the units in HB_UNITS or after -units, such as Covenant/Scarab.");
                foreach (string name in named.Split(','))
                {
                    Entry entry = Models.FirstOrDefault(e => e.Unit == name.Trim()) ?? throw new ArgumentException("No real model for " + name);
                    Build(entry);
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError("Building the named units failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        /// <summary>Only the infested vehicles, which carry the growths.</summary>
        public static void BuildInfestedFromCommandLine()
        {
            int exit = 0;
            try
            {
                foreach (Entry entry in Models.Where(e => e.Tint == Infested)) Build(entry);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError("Building the infested vehicles failed: " + e); exit = 1; }
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
            bool infested = entry.Tint == Infested;
            GameObject root = MakeModel(entry.Folder, unitName + "_Model", entry.Length, entry.Height, unit.isAir, entry.Tint, entry.Tint != Color.white ? "_" + unitName : "",
                glows: !infested, flood: infested, yaw: entry.Yaw);
            if (root == null) return; // Rigged and baked instead.
            try
            {
                if (infested) AddGrowths(root, unitName);
                if (entry.Thrusters) AddThrusters(root);
                if (!string.IsNullOrEmpty(entry.Prop)) AddProp(root, entry.Prop, entry.PropLength);
                if (entry.Mouth) AddMouth(root);
                string path = $"{PrefabFolder}/{unitName}_Model.prefab";
                unit.prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                unit.keepsOwnMaterials = true;
                EditorUtility.SetDirty(unit);
                Bounds bounds = BoundsOf(root);
                Debug.Log($"Built {path}: size {bounds.size}, {root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3)} triangles in {root.GetComponentsInChildren<MeshRenderer>().Length} parts");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Flood growths on an infested vehicle. Without the prepared growths it is only tinted, as before.</summary>
        private static void AddGrowths(GameObject root, string unitName)
        {
            GameObject pieces = MakeModel(FloodGrowths.Folder, "Growths", 1f, 0f, false, GrowthTint, "_Infested");
            if (pieces == null) return;
            try
            {
                FloodGrowths.Result result = FloodGrowths.AddTo(root, pieces, unitName);
                Debug.Log($"Growths on {unitName}: {result.Big} pods and {result.Small} bubbles, {result.Triangles} triangles");
            }
            finally
            {
                Object.DestroyImmediate(pieces);
            }
        }

        /// <summary>
        /// A Thruster_<size> node at the back of the outermost part on each side: the parts reaching furthest out (within 15% of the widest), where
        /// their back is, half way up them. Size is how wide the glow is at full, in metres.
        /// </summary>
        private static void AddThrusters(GameObject root)
        {
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            var points = new List<Vector3>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Matrix4x4 meshToRoot = toRoot * filter.transform.localToWorldMatrix;
                points.AddRange(filter.sharedMesh.vertices.Select(v => meshToRoot.MultiplyPoint3x4(v)));
            }
            float widest = points.Max(p => Mathf.Abs(p.x));
            foreach (float side in new[] { -1f, 1f })
            {
                List<Vector3> part = points.Where(p => p.x * side > widest * 0.85f).ToList();
                if (part.Count == 0) continue;
                float low = part.Min(p => p.y), high = part.Max(p => p.y);
                float size = Mathf.Clamp((high - low) * 1.6f, 0.35f, 0.8f);
                var node = new GameObject(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Thruster_{0:0.00}", size));
                node.transform.SetParent(root.transform, false);
                node.transform.localPosition = new Vector3(part.Average(p => p.x), (low + high) * 0.5f, part.Min(p => p.z) - 0.3f); // Just clear of the hull, so it shows.
                Debug.Log($"{root.name}: {node.name} at {node.transform.localPosition}");
            }
        }

        /// <summary>
        /// The mouth's glow, about its Muzzle_0 (metres, the model's axes, +Z its front): a green point light just inside the mouth, off until a
        /// beam lights it, and the two places either side, in the hollows between the jaws, that glow with it.
        /// </summary>
        private static void AddMouth(GameObject root)
        {
            Transform muzzle = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Muzzle_0");
            if (muzzle == null) throw new InvalidOperationException($"{root.name}: a mouth needs its Muzzle_0.");
            Vector3 mouth = root.transform.InverseTransformPoint(muzzle.position);
            var glow = new GameObject("MouthGlow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = mouth + new Vector3(0f, 0.35f, -1.1f);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.42f, 1f, 0.36f);
            light.range = 5.5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            for (int side = 0; side < 2; side++)
            {
                var place = new GameObject("Mandible_" + side);
                place.transform.SetParent(root.transform, false);
                place.transform.localPosition = mouth + new Vector3(side == 0 ? -1.25f : 1.25f, 0.75f, -0.55f);
            }
            Debug.Log($"{root.name}: a mouth glow at {glow.transform.localPosition}");
        }

        /// <summary>
        /// A weapon model held at the model's Muzzle_1, pointing forward with its tip just past it, in the weapon's own vertex colours; Muzzle_1
        /// then moves to the weapon's tip, so that shots leave the barrel.
        /// </summary>
        private static void AddProp(GameObject root, string prop, float length)
        {
            Transform muzzle = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Muzzle_1");
            string path = $"{AnimationBaker.ModelsFolder}/{prop}/{prop}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (muzzle == null || model == null) throw new InvalidOperationException($"{root.name}: needs a Muzzle_1 and {path}.");
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(weapon, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            weapon.name = prop;
            weapon.transform.SetParent(root.transform, false);
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;
            weapon.transform.localScale = Vector3.one;
            MeshFilter filter = weapon.GetComponentInChildren<MeshFilter>();
            Transform tip = weapon.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Muzzle");
            if (filter == null || tip == null) throw new InvalidOperationException($"{path} needs a mesh and a node named Muzzle.");
            Matrix4x4 meshToRoot = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            float[] along = filter.sharedMesh.vertices.Select(v => meshToRoot.MultiplyPoint3x4(v).z).ToArray();
            weapon.transform.localScale = Vector3.one * (length / Mathf.Max(0.001f, along.Max() - along.Min()));
            Vector3 wantedTip = root.transform.InverseTransformPoint(muzzle.position) + Vector3.forward * 0.1f;
            weapon.transform.localPosition += wantedTip - root.transform.InverseTransformPoint(tip.position);
            muzzle.position = tip.position;
            Object.DestroyImmediate(tip.gameObject);
            string materialPath = $"{AnimationBaker.ModelsFolder}/{prop}/{prop}_Held.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                // Opaque, and it shows the weapon's vertex colours, which is where its paint is.
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit")) { name = prop + "_Held" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            foreach (MeshRenderer renderer in weapon.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Debug.Log($"{root.name}: holds {prop}, tip at {root.transform.InverseTransformPoint(muzzle.position)}");
        }

        /// <summary>A tower's prefab, in Resources so that the game can find it by its kind's name. Its door faces +Z, as a unit's front does.</summary>
        private static void BuildTower(string folder, float length, float lift)
        {
            GameObject root = MakeModel(folder, folder, length, 0f, false, new Color(lift, lift, lift), "", glows: true);
            if (root == null) return;
            try
            {
                if (!AssetDatabase.IsValidFolder(TowerFolder)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Towers");
                string path = $"{TowerFolder}/{folder}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Bounds bounds = BoundsOf(root);
                Debug.Log($"Built {path}: size {bounds.size}, {root.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3)} triangles in {root.GetComponentsInChildren<MeshRenderer>().Length} parts");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The model in a folder of Art/Imported/Models, scaled to this length across the ground (or to this height, if one is given), stood on the
        /// ground and centred on the origin (an air unit on its middle), with its matte materials. Null if the folder has no FBX. The caller destroys it.
        /// With glows, a material with a glow texture or colour is switched on to glow (see Matte).
        /// </summary>
        private static GameObject MakeModel(string modelFolder, string rootName, float length, float height, bool isAir, Color tint, string materialSuffix, bool glows = false, bool flood = false, float yaw = 0f)
        {
            string folder = $"{AnimationBaker.ModelsFolder}/{modelFolder}";
            string fbx = $"{folder}/{modelFolder}.fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null) return null;

            // The materials live inside the model, not in a folder Unity extracts next to it.
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
            {
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }

            string json = $"{folder}/materials.json";
            MaterialList info = File.Exists(json) ? JsonUtility.FromJson<MaterialList>(File.ReadAllText(json)) : new MaterialList();

            var root = new GameObject(rootName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            model.transform.SetParent(root.transform, false);
            if (yaw != 0f) model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;

            Bounds bounds = BoundsOf(root);
            float scale = height > 0f ? height / bounds.size.y : length / Mathf.Max(bounds.size.x, bounds.size.z);
            model.transform.localScale *= scale;
            bounds = BoundsOf(root);
            float baseHeight = isAir ? bounds.center.y : bounds.min.y;
            model.transform.localPosition -= new Vector3(bounds.center.x, baseHeight, bounds.center.z);

            string materials = $"{folder}/GameMaterials";
            if (!AssetDatabase.IsValidFolder(materials)) AssetDatabase.CreateFolder(folder, "GameMaterials");
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Matte(m, info, folder, materials, tint, materialSuffix, glows, flood)).ToArray();
            }
            return root;
        }

        /// <summary>
        /// A matte material from what materials.json says: as imported they are shiny enough to mirror the pale sky,
        /// which turns paint steel grey. Glass stays shiny.
        /// </summary>
        private static Material Matte(Material imported, MaterialList list, string folder, string materials, Color tint, string suffix, bool glows, bool flood)
        {
            string path = $"{materials}/{imported.name}{suffix}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = imported.name + suffix };

            MaterialInfo info = list.materials.FirstOrDefault(m => m.name == imported.name);
            bool glass = info?.glass ?? false;
            Color colour = info?.colour != null && info.colour.Length == 3 ? new Color(info.colour[0], info.colour[1], info.colour[2]).gamma : imported.color;
            material.SetColor("_BaseColor", new Color(colour.r * tint.r, colour.g * tint.g, colour.b * tint.b, 1f));
            if (!string.IsNullOrEmpty(info?.texture))
            {
                // An infested vehicle wears the texture's Flood copy, if Tools/Art/flood_tendrils.py has made one.
                string texture = $"{folder}/{info.texture}";
                string floodCopy = $"{folder}/{Path.GetFileNameWithoutExtension(info.texture)}_Flood.png";
                if (flood && File.Exists(floodCopy)) texture = floodCopy;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            }
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
                // URP keeps _EMISSION only on a material flagged as emissive, and drops it otherwise when it first checks a new material, so it is
                // set both ways every time. On for the towers and, since D82 (the owner, 2026-11-07), every other model but the infested ones.
                if (glows) material.EnableKeyword("_EMISSION");
                else material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None;
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
