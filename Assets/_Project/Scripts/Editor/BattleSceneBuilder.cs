// BattleSceneBuilder.cs
// Builds the game's scenes from code, so they can always be recreated exactly:
//   Match.unity  - the game: starting draft, then rounds of deployment, battle and result against
//                  the do-nothing AI (MatchController, DeploymentInput, MatchHud).
//   Battle.unity - the balanced 300 vs 300 test battle, played at once (for battle testing).
//   Sandbox.unity - place two armies from every faction freely and run the battle (SandboxController,
//                  DeploymentInput, SandboxHud); reached from the draft screen.
// Both have a directional light, the Main Camera with the pan/zoom controller, the "Battle" object
// (BattleController and its views, wired to the Combat Rules asset) and a HUD. The Match scene is
// put first in the build settings, so a built game starts with a match.
//   Menu: Halobellum > Scenes > Rebuild Match Scene / Rebuild Battle Scene / Rebuild Sandbox Scene
//   Command line: -executeMethod Halobellum.EditorTools.BattleSceneBuilder.BuildFromCommandLine (all three)
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
        public const string MatchScenePath = "Assets/_Project/Scenes/Match.unity";
        public const string SandboxScenePath = "Assets/_Project/Scenes/Sandbox.unity";
        public const string TestBattlePath = "Assets/_Project/Data/TestBattle_Balanced.asset";
        public const string PlaceholderMaterialPath = "Assets/_Project/Art/Placeholder/PlaceholderLit.mat";
        public const string FactionFolder = "Assets/_Project/Data/Factions";

        [MenuItem("Halobellum/Scenes/Rebuild Battle Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log("Battle scene rebuilt at " + ScenePath + ". Press Play to watch the battle.");
        }

        [MenuItem("Halobellum/Scenes/Rebuild Match Scene")]
        public static void BuildMatchFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildMatch();
            Debug.Log("Match scene rebuilt at " + MatchScenePath + ". Press Play to start a match.");
        }

        [MenuItem("Halobellum/Scenes/Rebuild Sandbox Scene")]
        public static void BuildSandboxFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildSandbox();
            Debug.Log("Sandbox scene rebuilt at " + SandboxScenePath + ". Press Play to place two armies.");
        }

        /// <summary>Command line: rebuilds all three scenes.</summary>
        public static void BuildFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                Build();
                BuildMatch();
                BuildSandbox();
            }
            catch (Exception e) { Debug.LogError("Building the scenes failed: " + e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Builds Battle.unity: the 300 vs 300 test battle.</summary>
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetupDefinition>(TestBattlePath);
            if (setup == null) throw new InvalidOperationException("Missing " + TestBattlePath);

            BattleController battle = CreateCommon();
            SetReference(battle, "battleSetup", setup);
            SetReference(battle.gameObject.AddComponent<BattleBenchmark>(), "battle", battle);

            var hud = new GameObject("HUD");
            SetReference(hud.AddComponent<BattleHud>(), "battle", battle);
            SetReference(hud.AddComponent<SquadStatsWindow>(), "battle", battle);

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();
        }

        /// <summary>Every faction asset in Data/Factions, UNSC first.</summary>
        private static List<FactionDefinition> LoadFactions()
        {
            List<FactionDefinition> factions = AssetDatabase.FindAssets("t:" + nameof(FactionDefinition), new[] { FactionFolder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p.Contains("UNSC") ? 0 : 1).ThenBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<FactionDefinition>).ToList();
            if (factions.Count == 0) throw new InvalidOperationException("No factions in " + FactionFolder);
            return factions;
        }

        /// <summary>Builds Match.unity: the game.</summary>
        public static void BuildMatch()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var matchRules = AssetDatabase.LoadAssetAtPath<MatchRulesDefinition>(MatchRulesDefinition.DefaultAssetPath);
            if (matchRules == null) throw new InvalidOperationException("Missing " + MatchRulesDefinition.DefaultAssetPath);
            List<FactionDefinition> factions = LoadFactions();

            BattleController battle = CreateCommon();
            GameObject battleObject = battle.gameObject;
            var match = battleObject.AddComponent<MatchController>();
            SetReference(match, "matchRules", matchRules);
            SetReferenceList(match, "factions", factions.Cast<Object>().ToList());
            battleObject.AddComponent<DeploymentInput>();
            battleObject.AddComponent<MatchAutoplay>();

            var hud = new GameObject("HUD");
            var battleHud = hud.AddComponent<BattleHud>();
            SetReference(battleHud, "battle", battle);
            SetBool(battleHud, "showResultLine", false);
            SetReference(hud.AddComponent<SquadStatsWindow>(), "battle", battle);
            var matchHud = hud.AddComponent<MatchHud>();
            SetReference(matchHud, "match", match);
            SetReference(matchHud, "battle", battle);

            EditorSceneManager.SaveScene(scene, MatchScenePath);
            UpdateBuildSettings();
        }

        /// <summary>Builds Sandbox.unity: place two armies freely and run the battle.</summary>
        public static void BuildSandbox()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            List<FactionDefinition> factions = LoadFactions();

            BattleController battle = CreateCommon();
            GameObject battleObject = battle.gameObject;
            var sandbox = battleObject.AddComponent<SandboxController>();
            SetReferenceList(sandbox, "factions", factions.Cast<Object>().ToList());
            // The placeholder test units are offered in the sandbox only, never in matches.
            SetReferenceList(sandbox, "extraUnits", HeadlessRunnerTool.LoadUnitsInFolder(HeadlessRunnerTool.PlaceholderFolder).Cast<Object>().ToList());
            battleObject.AddComponent<DeploymentInput>();
            battleObject.AddComponent<SandboxAutoplay>();

            var hud = new GameObject("HUD");
            SetReference(hud.AddComponent<BattleHud>(), "battle", battle);
            SetReference(hud.AddComponent<SquadStatsWindow>(), "battle", battle);
            var sandboxHud = hud.AddComponent<SandboxHud>();
            SetReference(sandboxHud, "sandbox", sandbox);
            SetReference(sandboxHud, "battle", battle);

            EditorSceneManager.SaveScene(scene, SandboxScenePath);
            UpdateBuildSettings();
        }

        /// <summary>Light, camera and the Battle object shared by both scenes. Returns the BattleController.</summary>
        private static BattleController CreateCommon()
        {
            // Load assets after the new scene exists: opening a new scene unloads unreferenced assets.
            var rules = AssetDatabase.LoadAssetAtPath<CombatRulesDefinition>(CombatRulesDefinition.DefaultAssetPath);
            if (rules == null) throw new InvalidOperationException("Missing " + CombatRulesDefinition.DefaultAssetPath);

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None; // Placeholder art; no shadows keeps 600 units cheap.
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var battleObject = new GameObject("Battle");
            var battle = battleObject.AddComponent<BattleController>(); // Adds its required view components too.
            SetReference(battle, "combatRules", rules);
            SetReference(battle, "placeholderMaterial", EnsurePlaceholderMaterial());

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 1500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.12f);
            cameraObject.AddComponent<AudioListener>();
            SetReference(cameraObject.AddComponent<BattleCameraController>(), "battle", battle);
            return battle;
        }

        /// <summary>The game's scenes in build order: the Match first (a built game starts with a match).</summary>
        public static readonly string[] GameScenes = { MatchScenePath, SandboxScenePath, ScenePath };

        /// <summary>Match scene first (the game), then the Sandbox and the Battle test scene.</summary>
        private static void UpdateBuildSettings()
        {
            var ordered = new List<EditorBuildSettingsScene>();
            foreach (string path in GameScenes)
            {
                if (System.IO.File.Exists(path)) ordered.Add(new EditorBuildSettingsScene(path, true));
            }
            ordered.AddRange(EditorBuildSettings.scenes.Where(s => !GameScenes.Contains(s.path)));
            EditorBuildSettings.scenes = ordered.ToArray();
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

        private static void SetReferenceList(Object component, string field, List<Object> values)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray) throw new InvalidOperationException($"{component.GetType().Name} has no list '{field}'.");
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object component, string field, bool value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException($"{component.GetType().Name} has no field '{field}'.");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
