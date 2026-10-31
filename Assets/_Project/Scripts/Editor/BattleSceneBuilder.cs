// BattleSceneBuilder.cs
// Builds the battle scene (Assets/_Project/Scenes/Battle.unity) from code, so it can always be
// recreated exactly: a directional light, the Main Camera with the pan/zoom controller, the
// "Battle" object (BattleController and its views, wired to the Combat Rules asset and the
// balanced 300 vs 300 test Battle Setup), and the HUD. It also puts the scene first in the build settings.
//   Menu: Halobellum > Scenes > Rebuild Battle Scene
//   Command line: -executeMethod Halobellum.EditorTools.BattleSceneBuilder.BuildFromCommandLine
// Depends on: Halobellum.Presentation, Halobellum.UI, Halobellum.Units, UnityEditor.

using System;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Presentation;
using Halobellum.UI;
using Halobellum.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Halobellum.EditorTools
{
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Battle.unity";
        public const string TestBattlePath = "Assets/_Project/Data/TestBattle_Balanced.asset";
        public const string PlaceholderMaterialPath = "Assets/_Project/Art/Placeholder/PlaceholderLit.mat";

        [MenuItem("Halobellum/Scenes/Rebuild Battle Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log("Battle scene rebuilt at " + ScenePath + ". Press Play to watch the battle.");
        }

        public static void BuildFromCommandLine()
        {
            int exitCode = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogError("Building the battle scene failed: " + e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }

        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Load the assets after creating the scene: opening a new scene unloads unreferenced assets.
            var rules = AssetDatabase.LoadAssetAtPath<CombatRulesDefinition>(CombatRulesDefinition.DefaultAssetPath);
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetupDefinition>(TestBattlePath);
            if (rules == null) throw new InvalidOperationException("Missing " + CombatRulesDefinition.DefaultAssetPath);
            if (setup == null) throw new InvalidOperationException("Missing " + TestBattlePath);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None; // Placeholder art; no shadows keeps 600 units cheap.
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var battleObject = new GameObject("Battle");
            var battle = battleObject.AddComponent<BattleController>(); // Adds its required view components too.
            SetReference(battle, "combatRules", rules);
            SetReference(battle, "battleSetup", setup);
            SetReference(battle, "placeholderMaterial", EnsurePlaceholderMaterial());
            SetReference(battleObject.AddComponent<BattleBenchmark>(), "battle", battle);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 1500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.12f);
            cameraObject.AddComponent<AudioListener>();
            SetReference(cameraObject.AddComponent<BattleCameraController>(), "battle", battle);

            var hud = new GameObject("HUD");
            SetReference(hud.AddComponent<BattleHud>(), "battle", battle);
            SetReference(hud.AddComponent<SquadStatsWindow>(), "battle", battle);

            EditorSceneManager.SaveScene(scene, ScenePath);

            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Loads the shared placeholder URP Lit material, creating it the first time.</summary>
        private static Material EnsurePlaceholderMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderMaterialPath);
            if (material != null) return material;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("Could not find the URP Lit shader.");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PlaceholderMaterialPath) ?? "Assets");
            material = new Material(lit) { name = "PlaceholderLit", color = Color.white };
            AssetDatabase.CreateAsset(material, PlaceholderMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void SetReference(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException($"{component.GetType().Name} has no field '{field}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (property.objectReferenceValue == null) throw new InvalidOperationException($"Could not set {component.GetType().Name}.{field}.");
        }
    }
}
