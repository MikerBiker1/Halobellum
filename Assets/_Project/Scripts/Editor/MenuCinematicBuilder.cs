// MenuCinematicBuilder.cs
// Builds Scenes/MainMenu.unity from code (D94): the main menu as a cinematic battlefield, a battle of the three sides under a storm at dusk,
// seen in a loop of five camera shots that fade into one another (MenuCinematic moves it all). Vehicles only, no soldiers or Flood forms
// (the owner's word, D94). The UNSC holds the west behind a line of concrete barriers and ruins: Scorpions, Mantises, Warthogs, an Elephant,
// a Pelican hovering, Pelicans and Hornets overhead. The Covenant comes from the north-east under its spires: Wraiths lobbing plasma mortars,
// Ghosts waiting and racing in, Banshees diving, Phantoms hovering, and a Scarab striding through the middle of the field on planted feet,
// sweeping the human line with its beam. The Flood holds the south-east among growths of its biomass, spores drifting over it: infested
// Wraiths, Scorpions, Warthogs and Ghosts, an infected Phantom, infested Banshees and Hornets and an infected Pelican in the air. Only the
// vehicles' own guns fire, now and then: tank shells and mortars burst where they land; blasts go off among each army; fires burn in the
// wrecks; smoke rises in columns and fog lies low; far off, lightning strikes, flashes light the horizon and an alien warship glasses the land
// beyond the hills (MenuCinematicSky.shader). Every shot keeps its subject in the right half of the screen, where the menu's own dark panel
// (MainMenuHud) leaves the picture clear.
// The models are the project's own; the ground, ruins, growths, spires, sky, smoke and glows are made here and written to
// Art/MenuCinematic. The menu before this one is kept as MainMenuClassic (MainMenuSceneBuilder), chosen in Settings > Menu background.
//   Menu: Halobellum > Scenes > Rebuild Main Menu Scene
//   Command line: -executeMethod Halobellum.EditorTools.MenuCinematicBuilder.BuildFromCommandLine
//   Or ...BuildAndPreviewFromCommandLine, which also renders a picture of each shot (with the menu's dark panel laid over) to
//   menu_cinematic_*.png, in the folder after -menuPreviewOut or next to the project.
// Depends on: BattleSceneBuilder (scene paths, build settings), MenuCinematic, MainMenuHud,
//             URP (post-processing), UnityEditor.

using System;
using System.Collections.Generic;
using System.IO;
using Halobellum.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Halobellum.EditorTools
{
    public static class MenuCinematicBuilder
    {
        private const string ArtFolder = "Assets/_Project/Art/MenuCinematic";
        private const string UnitFolder = "Assets/_Project/Prefabs/Units/";
        private const string BodyFolder = "Assets/_Project/Resources/BakedBodies/";

        // The Scarab's walk, once each loop of shots, along flat ground through the middle of the field, toward the human line.
        private static readonly Vector2 WalkerFrom = new Vector2(130f, 110f), WalkerTo = new Vector2(35f, 22f);
        private const float WalkerScale = 3.4f;
        // The sun, low behind the Covenant; the warship further round, over the hills.
        private const float SunElevation = 3.5f, SunAzimuth = 35f;
        private const float ShipElevation = 5.2f, ShipAzimuth = 71f;
        private static readonly float[] ShotSeconds = { 11f, 10f, 10f, 10f, 11f };
        private static float Loop
        {
            get
            {
                float total = 0f;
                foreach (float seconds in ShotSeconds) total += seconds;
                return total;
            }
        }

        // The ground: a fine grid over the battle, coarser out to the hills.
        private const float InnerReach = 330f, OuterReach = 1850f;
        private const int InnerCells = 170, OuterCells = 64;
        private const float MapReach = 330f;

        private static readonly List<Vector4> Craters = new List<Vector4>(); // x, z, radius, depth.
        private static Mesh _quad;

        [MenuItem("Halobellum/Scenes/Rebuild Main Menu Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log("Main menu scene rebuilt at " + BattleSceneBuilder.MainMenuScenePath + ".");
        }

        public static void BuildFromCommandLine()
        {
            int exitCode = 0;
            try { Build(); }
            catch (Exception e) { Debug.LogError("Building the main menu failed: " + e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }

        public static void BuildAndPreviewFromCommandLine()
        {
            int exitCode = 0;
            try
            {
                MenuCinematic cinematic = Build();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-menuPreviewOut") folder = args[i + 1];
                }
                Directory.CreateDirectory(folder);
                float[] times = { 4f, 9f, 14f, 19f, 24.22f, 29f, 34f, 39f, 45f, 49.5f };
                foreach (float time in times) Preview(cinematic, time, Path.Combine(folder, $"menu_cinematic_{time:00.0}.png"));
            }
            catch (Exception e) { Debug.LogError("Previewing the main menu failed: " + e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Renders the scene as it is this many seconds after the menu opens, with the menu's dark panel laid over its left side.</summary>
        public static void Preview(MenuCinematic cinematic, float time, string path)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float oldShadows = pipeline != null ? pipeline.shadowDistance : 0f;
            if (pipeline != null) pipeline.shadowDistance = cinematic.shadowDistance;

            cinematic.RefreshLight();
            cinematic.Prepare();
            ParticleSystem[] systems = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (ParticleSystem particles in systems) particles.Simulate(20f, false, true);
            // The last few seconds stepped, so the walker is in its stride and the blasts have grown.
            float start = Mathf.Max(0f, time - 3f), step = 1f / 20f;
            cinematic.Advance(start);
            for (float t = start; t < time - 0.001f; t += step)
            {
                cinematic.Advance(step);
                foreach (ParticleSystem particles in systems) particles.Simulate(step, false, false);
            }

            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Camera view = cinematic.view;
            view.targetTexture = target;
            view.Render();
            view.targetTexture = null;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = null;
            // The menu's panel: dark at the left edge, fading out by 68% of the way across (MainMenuHud).
            Color[] pixels = image.GetPixels();
            for (int x = 0; x < width; x++)
            {
                float shade = 0.86f * Mathf.Clamp01(1f - x / (width * 0.68f));
                if (shade <= 0f) continue;
                for (int y = 0; y < height; y++)
                {
                    int i = y * width + x;
                    pixels[i] = Color.Lerp(pixels[i], new Color(0.015f, 0.015f, 0.03f), shade);
                }
            }
            image.SetPixels(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            if (pipeline != null) pipeline.shadowDistance = oldShadows;
            Debug.Log("Built preview " + path);
        }

        // ------------------------------------------------------------------ the scene

        public static MenuCinematic Build()
        {
            Directory.CreateDirectory(ArtFolder);
            _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCraters();

            var root = new GameObject("Menu Cinematic");
            var cinematic = root.AddComponent<MenuCinematic>();
            root.AddComponent<MainMenuHud>();

            var art = new Art();
            SetUpSkyAndLight(cinematic);
            BuildCamera(cinematic, art);
            BuildVolume();
            BuildGround();
            BuildRidges();
            var parts = new Parts();
            BuildScenery(art, parts);
            BuildUnsc(cinematic, art, parts);
            BuildCovenant(cinematic, art, parts);
            BuildFlood(cinematic, art, parts);
            BuildWalker(cinematic, art);
            BuildEffects(cinematic, art, parts);
            BuildFlyers(art, parts);
            BuildShots(cinematic);

            cinematic.figures = parts.figures.ToArray();
            cinematic.marchers = parts.marchers.ToArray();
            cinematic.flyers = parts.flyers.ToArray();
            cinematic.flickers = parts.flickers.ToArray();
            cinematic.blasts = parts.blasts.ToArray();
            cinematic.glows = parts.glows.ToArray();
            cinematic.hovers = parts.hovers.ToArray();
            Debug.Log($"Menu cinematic: {parts.figures.Count} figures, {parts.marchers.Count} marching, {parts.flyers.Count} flying, {parts.blasts.Count} kinds of blast, loop {Loop} s.");

            EditorSceneManager.SaveScene(scene, BattleSceneBuilder.MainMenuScenePath);
            BattleSceneBuilder.UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            return cinematic;
        }

        /// <summary>What the builder gathers for MenuCinematic as it goes.</summary>
        private sealed class Parts
        {
            public readonly List<MenuCinematic.Figure> figures = new List<MenuCinematic.Figure>();
            public readonly List<MenuCinematic.Marcher> marchers = new List<MenuCinematic.Marcher>();
            public readonly List<MenuCinematic.Flyer> flyers = new List<MenuCinematic.Flyer>();
            public readonly List<MenuCinematic.Flicker> flickers = new List<MenuCinematic.Flicker>();
            public readonly List<MenuCinematic.Blast> blasts = new List<MenuCinematic.Blast>();
            public readonly List<Transform> glows = new List<Transform>();
            public readonly List<Transform> hovers = new List<Transform>();
            public readonly Random random = new Random(9407);
            public float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);
        }

        /// <summary>The textures and materials the scene shares, made once.</summary>
        private sealed class Art
        {
            public Texture2D glow, puff, flame;
            public Material smoke, fog, ember, ash, spore;
            public Material shell, mortar, mortarTrail, fire, spark, blueSpark, flash, blueFlash, farFlash, beam, beamFire;
            public Material engine, covenantEngine, floodGlow, covenantGlow, redLight;
            public Material charred, concrete, rock, bark, growth, growthDark, spire, spireLine, metal;

            public Art()
            {
                glow = SavePng("CinematicGlow", MakeGlow(128), true, TextureWrapMode.Clamp);
                puff = SavePng("CinematicSmoke", MakePuff(256), true, TextureWrapMode.Clamp);
                flame = SavePng("CinematicFlame", MakeFlame(128), true, TextureWrapMode.Clamp);
                smoke = MakeParticleMaterial("CinematicSmoke", puff, false, Color.white);
                fog = MakeParticleMaterial("CinematicFog", puff, false, Color.white);
                ember = MakeParticleMaterial("CinematicEmber", glow, true, new Color(5f, 1.9f, 0.55f));
                ash = MakeParticleMaterial("CinematicAsh", glow, false, new Color(0.55f, 0.5f, 0.5f));
                spore = MakeParticleMaterial("CinematicSpore", glow, true, new Color(1.3f, 1.7f, 0.45f));
                shell = MakeParticleMaterial("CinematicShell", glow, true, new Color(6f, 2.8f, 0.9f));
                mortar = MakeParticleMaterial("CinematicMortar", glow, true, new Color(1.1f, 2.1f, 6.5f));
                mortarTrail = MakeParticleMaterial("CinematicMortarTrail", glow, true, new Color(0.4f, 0.9f, 3.2f));
                fire = MakeParticleMaterial("CinematicFire", flame, true, new Color(4.2f, 1.7f, 0.5f));
                spark = MakeParticleMaterial("CinematicSpark", glow, true, new Color(7f, 3.2f, 1f));
                blueSpark = MakeParticleMaterial("CinematicBlueSpark", glow, true, new Color(0.9f, 1.9f, 5.5f));
                flash = MakeParticleMaterial("CinematicFlash", glow, true, new Color(7f, 4.2f, 2.2f));
                blueFlash = MakeParticleMaterial("CinematicBlueFlash", glow, true, new Color(1.5f, 2.6f, 6.5f));
                farFlash = MakeParticleMaterial("CinematicFarFlash", glow, true, new Color(5f, 2.6f, 1.2f));
                beam = MakeParticleMaterial("CinematicBeam", glow, true, new Color(0.6f, 1.15f, 4.4f));
                beamFire = MakeParticleMaterial("CinematicBeamFire", flame, true, new Color(1.3f, 1.9f, 5f));
                engine = MakeParticleMaterial("CinematicEngine", glow, true, new Color(5f, 2.2f, 0.8f));
                covenantEngine = MakeParticleMaterial("CinematicCovenantEngine", glow, true, new Color(2.6f, 1.3f, 6f));
                floodGlow = MakeParticleMaterial("CinematicFloodGlow", glow, true, new Color(2.2f, 2.6f, 0.7f));
                covenantGlow = MakeParticleMaterial("CinematicCovenantGlow", glow, true, new Color(3.5f, 1.6f, 7f));
                redLight = MakeParticleMaterial("CinematicRedLight", glow, true, new Color(8f, 0.6f, 0.4f));
                charred = MakeLit("CinematicCharred", new Color(0.055f, 0.045f, 0.045f), 0.12f);
                concrete = MakeLit("CinematicConcrete", new Color(0.36f, 0.34f, 0.33f), 0.05f);
                rock = MakeLit("CinematicRock", new Color(0.24f, 0.19f, 0.16f), 0.1f);
                bark = MakeLit("CinematicBark", new Color(0.07f, 0.055f, 0.05f), 0.05f);
                growth = MakeLit("CinematicGrowth", new Color(0.5f, 0.44f, 0.27f), 0.55f);
                growthDark = MakeLit("CinematicGrowthDark", new Color(0.3f, 0.2f, 0.13f), 0.5f);
                metal = MakeLit("CinematicMetal", new Color(0.2f, 0.21f, 0.22f), 0.35f);
                spire = MakeLit("CinematicSpire", new Color(0.2f, 0.14f, 0.28f), 0.7f);
                spire.SetFloat("_Metallic", 0.6f);
                EditorUtility.SetDirty(spire);
                spireLine = MakeLit("CinematicSpireLine", new Color(0.3f, 0.15f, 0.5f), 0.8f);
                spireLine.EnableKeyword("_EMISSION");
                spireLine.SetColor("_EmissionColor", new Color(2.2f, 0.9f, 5f));
                spireLine.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(spireLine);
            }
        }

        private static void SetUpSkyAndLight(MenuCinematic cinematic)
        {
            string shaderPath = ArtFolder + "/MenuCinematicSky.shader";
            AssetDatabase.ImportAsset(shaderPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null) throw new InvalidOperationException("Missing " + shaderPath);
            Vector3 sun = Direction(SunAzimuth, SunElevation);
            Material sky = SaveMaterial("MenuCinematicSky", new Material(shader));
            sky.SetVector("_SunDirection", sun);
            sky.SetVector("_ShipDirection", Direction(ShipAzimuth, ShipElevation));
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.38f, 0.37f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.11f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.44f, 0.32f, 0.3f);
            RenderSettings.fogDensity = 0.002f;
            cinematic.ambientAbove = new Color(0.42f, 0.44f, 0.62f);
            cinematic.ambientMiddle = new Color(0.5f, 0.38f, 0.37f);
            cinematic.ambientBelow = new Color(0.13f, 0.11f, 0.11f);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.6f, 0.34f);
            light.intensity = 3.3f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            lightObject.transform.rotation = Quaternion.LookRotation(-Direction(SunAzimuth, 14f));
            RenderSettings.sun = light;

            // A cold light from the storm on the far side, so what faces away from the sun doesn't go black.
            var fillObject = new GameObject("Storm Fill");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.52f, 0.58f, 0.85f);
            fill.intensity = 1f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(32f, SunAzimuth + 180f, 0f);

            // The lightning: off until a stroke.
            var lightningObject = new GameObject("Lightning");
            var lightning = lightningObject.AddComponent<Light>();
            lightning.type = LightType.Directional;
            lightning.color = new Color(0.7f, 0.78f, 1f);
            lightning.intensity = 0f;
            lightning.shadows = LightShadows.None;
            lightning.enabled = false;
            lightningObject.transform.rotation = Quaternion.Euler(62f, 250f, 0f);
            cinematic.lightning = lightning;
            cinematic.lightningIntensity = 1.4f;
            cinematic.lightningTimes = new[] { 4.6f, 15.8f, 24.2f, 29.4f, 34.6f, 48.2f };
            cinematic.lightningDirections = new[]
            {
                Direction(68f, 14f), Direction(88f, 12f), Direction(236f, 15f), Direction(250f, 11f), Direction(148f, 13f), Direction(58f, 16f),
            };
        }

        /// <summary>A direction from its compass bearing (0 north, +z; 90 east, +x) and its height above the horizon, in degrees.</summary>
        private static Vector3 Direction(float azimuth, float elevation) => Quaternion.Euler(-elevation, azimuth, 0f) * Vector3.forward;

        private static void BuildCamera(MenuCinematic cinematic, Art art)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 4000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cinematic.view = camera;

            // Ash and embers drifting just in front of the lens, wherever it is.
            ParticleSystem ash = MakeParticles("Ash", cameraObject.transform, new Vector3(0f, 0f, 14f), Quaternion.identity, art.ash);
            ParticleSystem.MainModule main = ash.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f), new Color(0.8f, 0.75f, 0.75f, 0.9f));
            main.maxParticles = 400;
            Rate(ash, 45f);
            Box(ash, new Vector3(34f, 18f, 24f));
            Drift(ash, new Vector2(-1.4f, -0.4f), new Vector2(-0.5f, -0.1f), new Vector2(-0.3f, 0.3f));
            Wobble(ash, 0.35f, 0.3f);
            FadeInOut(ash, 0.2f, 0.75f);

            ParticleSystem embers = MakeParticles("Embers", cameraObject.transform, new Vector3(0f, -1f, 18f), Quaternion.identity, art.ember);
            main = embers.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.maxParticles = 200;
            Rate(embers, 14f);
            Box(embers, new Vector3(40f, 14f, 26f));
            Drift(embers, new Vector2(-1.2f, -0.3f), new Vector2(0.3f, 1f), new Vector2(-0.4f, 0.4f));
            Wobble(embers, 0.6f, 0.4f);
            FadeInOut(embers, 0.15f, 0.6f);
        }

        private static void BuildVolume()
        {
            string path = ArtFolder + "/MenuCinematicVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : VolumeComponent
            {
                T component = profile.Add<T>(true);
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
                return component;
            }

            Add<Tonemapping>().mode.value = TonemappingMode.ACES;
            Bloom bloom = Add<Bloom>();
            bloom.threshold.value = 1f;
            bloom.intensity.value = 1.15f;
            bloom.scatter.value = 0.76f;
            bloom.tint.value = new Color(1f, 0.84f, 0.72f);
            Vignette vignette = Add<Vignette>();
            vignette.intensity.value = 0.4f;
            vignette.smoothness.value = 0.48f;
            vignette.color.value = new Color(0.02f, 0.01f, 0.03f);
            ColorAdjustments color = Add<ColorAdjustments>();
            color.postExposure.value = 0.95f;
            color.contrast.value = 16f;
            color.saturation.value = -6f;
            SplitToning toning = Add<SplitToning>();
            toning.shadows.value = new Color(0.3f, 0.44f, 0.6f);
            toning.highlights.value = new Color(0.6f, 0.5f, 0.4f);
            toning.balance.value = -20f;
            ChromaticAberration aberration = Add<ChromaticAberration>();
            aberration.intensity.value = 0.12f;
            FilmGrain grain = Add<FilmGrain>();
            grain.type.value = FilmGrainLookup.Medium1;
            grain.intensity.value = 0.2f;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeObject = new GameObject("Post Processing");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ ground and ridges

        /// <summary>Layered noise, 0 to about 1.</summary>
        private static float Layers(float x, float z, int octaves)
        {
            float sum = 0f, weight = 0.5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Mathf.PerlinNoise(x + 37.1f * i + 100f, z + 19.7f * i + 100f) * weight;
                total += weight;
                x *= 2.03f;
                z *= 2.03f;
                weight *= 0.5f;
            }
            return sum / total;
        }

        /// <summary>How far a point is from the Scarab's walk, which runs on a little past both its ends.</summary>
        private static float FromWalk(float x, float z)
        {
            Vector2 point = new Vector2(x, z), along = WalkerTo - WalkerFrom;
            float share = Mathf.Clamp(Vector2.Dot(point - WalkerFrom, along) / along.sqrMagnitude, -0.25f, 1.25f);
            return Vector2.Distance(point, WalkerFrom + along * share);
        }

        private static void MakeCraters()
        {
            Craters.Clear();
            var random = new Random(551);
            while (Craters.Count < 46)
            {
                float x = (float)random.NextDouble() * 440f - 200f, z = (float)random.NextDouble() * 460f - 230f;
                if (FromWalk(x, z) < 40f) continue;
                float radius = 2.5f + (float)random.NextDouble() * 5f;
                Craters.Add(new Vector4(x, z, radius, radius * (0.16f + (float)random.NextDouble() * 0.08f)));
            }
        }

        /// <summary>
        /// The ground's height: flat along the Scarab's walk (its legs take the ground as flat at its feet), gentle swells and shell holes over the
        /// rest of the field, rising to hills beyond it.
        /// </summary>
        private static float GroundHeight(float x, float z)
        {
            float rough = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 48f, FromWalk(x, z)));
            float swell = (Layers(x * 0.011f, z * 0.011f, 4) - 0.5f) * 3.2f;
            float grit = (Layers(x * 0.09f, z * 0.09f, 3) - 0.5f) * 0.3f;
            float holes = 0f;
            foreach (Vector4 crater in Craters)
            {
                float d = new Vector2(x - crater.x, z - crater.y).magnitude / crater.z;
                if (d > 1.6f) continue;
                float bowl = d < 1f ? -crater.w * (1f - d * d) * (1f - d * d) : 0f;
                holes += bowl + crater.w * 0.35f * Mathf.Exp(-(d - 1f) * (d - 1f) / 0.05f);
            }
            float distance = new Vector2(x, z).magnitude;
            float hills = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(290f, 950f, distance)) * Layers(x * 0.0032f + 5f, z * 0.0032f, 4) * 150f;
            return (swell + grit + holes) * rough + hills;
        }

        private static Vector3 Ground(float x, float z, float up = 0f) => new Vector3(x, GroundHeight(x, z) + up, z);

        /// <summary>Where a grid line falls: evenly through the battle, then further and further apart out to the hills.</summary>
        private static float GridLine(int index)
        {
            int half = InnerCells / 2 + OuterCells;
            int k = index - half;
            float sign = Mathf.Sign(k);
            int a = Mathf.Abs(k);
            if (a <= InnerCells / 2) return sign * a * (InnerReach / (InnerCells / 2));
            float t = (a - InnerCells / 2) / (float)OuterCells;
            return sign * (InnerReach + (OuterReach - InnerReach) * (t * t * 0.85f + t * 0.15f));
        }

        private static void BuildGround()
        {
            int cells = InnerCells + OuterCells * 2, columns = cells + 1;
            var vertices = new Vector3[columns * columns];
            var uv = new Vector2[vertices.Length];
            for (int row = 0; row < columns; row++)
            {
                float z = GridLine(row);
                for (int column = 0; column < columns; column++)
                {
                    float x = GridLine(column);
                    vertices[row * columns + column] = new Vector3(x, GroundHeight(x, z), z);
                    uv[row * columns + column] = new Vector2((x + MapReach) / (MapReach * 2f), (z + MapReach) / (MapReach * 2f));
                }
            }
            var triangles = new int[cells * cells * 6];
            int t = 0;
            for (int row = 0; row < cells; row++)
            {
                for (int column = 0; column < cells; column++)
                {
                    int a = row * columns + column, b = a + 1, c = a + columns, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }
            var mesh = new Mesh { name = "CinematicGround", indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); // No tangents: the ground has no normal map, and they would add a third to the mesh's size.
            mesh.RecalculateBounds();
            SaveAsset(mesh, ArtFolder + "/CinematicGround.asset");

            Texture2D map = SavePng("CinematicGroundMap", MakeGroundMap(1024), true, TextureWrapMode.Clamp);
            Texture2D detail = SavePng("CinematicGroundGrain", MakeGrain(512), false, TextureWrapMode.Repeat);
            Material material = SaveMaterial("CinematicGround", new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.SetTexture("_BaseMap", map);
            material.SetColor("_BaseColor", new Color(0.92f, 0.82f, 0.76f));
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetTexture("_DetailAlbedoMap", detail);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(70f, 70f));
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.EnableKeyword("_DETAIL_MULX2");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);

            var ground = new GameObject("Ground");
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        /// <summary>
        /// The battle's ground, one picture over the whole field: ash and dry earth, burnt patches, pale dust, the shell holes dark with a churned
        /// rim, and the Flood's creep spreading over the south-east.
        /// </summary>
        private static Color[] MakeGroundMap(int size)
        {
            var pixels = new Color[size * size];
            var earth = new Color(0.33f, 0.27f, 0.22f);
            var burnt = new Color(0.075f, 0.065f, 0.065f);
            var dust = new Color(0.5f, 0.43f, 0.36f);
            var creep = new Color(0.44f, 0.38f, 0.22f);
            var vein = new Color(0.26f, 0.13f, 0.09f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wx = (x + 0.5f) / size * MapReach * 2f - MapReach, wz = (y + 0.5f) / size * MapReach * 2f - MapReach;
                    float patches = Layers(wx * 0.02f, wz * 0.02f, 4);
                    float fine = Layers(wx * 0.3f + 9f, wz * 0.3f, 3);
                    Color color = Color.Lerp(earth, burnt, Mathf.SmoothStep(0.5f, 0.66f, patches) * 0.85f);
                    color = Color.Lerp(color, dust, Mathf.SmoothStep(0.52f, 0.7f, Layers(wx * 0.035f + 4f, wz * 0.035f, 4)) * 0.6f);
                    // The creep: thickest deep in the south-east, ragged at its edge, veined.
                    float flood = Mathf.InverseLerp(150f, 20f, new Vector2(wx - 165f, wz + 165f).magnitude - Layers(wx * 0.03f, wz * 0.03f + 7f, 4) * 70f);
                    if (flood > 0f)
                    {
                        float veins = 1f - Mathf.Abs(Layers(wx * 0.12f + 3f, wz * 0.12f, 4) - 0.5f) * 6f;
                        Color c = Color.Lerp(creep, vein, Mathf.Clamp01(veins) * 0.8f);
                        color = Color.Lerp(color, c, Mathf.SmoothStep(0f, 1f, flood));
                    }
                    foreach (Vector4 crater in Craters)
                    {
                        float d = new Vector2(wx - crater.x, wz - crater.y).magnitude / crater.z;
                        if (d > 1.9f) continue;
                        color = Color.Lerp(color, burnt * 0.7f, Mathf.Clamp01(1.05f - d) * 0.9f);
                        color = Color.Lerp(color, dust * 0.8f, Mathf.Exp(-(d - 1.15f) * (d - 1.15f) / 0.04f) * 0.35f);
                        color = Color.Lerp(color, burnt, Mathf.Clamp01(1.9f - d) * Mathf.Clamp01(d - 1f) * 0.4f * fine);
                    }
                    color *= 0.75f + fine * 0.5f;
                    color.a = 1f;
                    pixels[y * size + x] = color;
                }
            }
            return pixels;
        }

        /// <summary>Fine grit, around middle grey: multiplied over the map close up.</summary>
        private static Color[] MakeGrain(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float value = 0.28f + Tiled(x / (float)size, y / (float)size, 24, 4) * 0.44f;
                    pixels[y * size + x] = new Color(value, value, value, 1f);
                }
            }
            return pixels;
        }

        /// <summary>Layered noise that tiles over 0 to 1.</summary>
        private static float Tiled(float u, float v, int cells, int octaves)
        {
            u = Mathf.Repeat(u, 1f);
            v = Mathf.Repeat(v, 1f);
            float a = Layers(u * cells, v * cells, octaves);
            float b = Layers((u - 1f) * cells, v * cells, octaves);
            float c = Layers(u * cells, (v - 1f) * cells, octaves);
            float d = Layers((u - 1f) * cells, (v - 1f) * cells, octaves);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Three rings of far mountains all round, as dark curtains with crags; the haze pales them with distance.</summary>
        private static void BuildRidges()
        {
            Material material = MakeLit("CinematicRidge", new Color(0.11f, 0.09f, 0.11f), 0f);
            float[] distances = { 950f, 1300f, 1720f };
            float[] heights = { 80f, 150f, 260f };
            for (int band = 0; band < distances.Length; band++)
            {
                const int segments = 720;
                var vertices = new Vector3[(segments + 1) * 2];
                var triangles = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i / (float)segments * Mathf.PI * 2f;
                    float x = Mathf.Sin(angle) * distances[band], z = Mathf.Cos(angle) * distances[band];
                    float u = i / (float)segments * 220f;
                    float rough = Mathf.Lerp(Layers(u * 0.06f * (band + 1) + band * 7f, band * 3.3f, 4), Layers((u - 220f) * 0.06f * (band + 1) + band * 7f, band * 3.3f, 4), i / (float)segments);
                    float crag = 1f - Mathf.Abs(Layers(u * 0.19f + band * 13f, band * 1.7f, 4) - 0.5f) * 3.2f;
                    float fine = 1f - Mathf.Abs(Layers(u * 0.7f + band * 5f, band * 2.9f, 2) - 0.5f) * 3f;
                    float top = heights[band] * Mathf.Max(0.05f, rough * 0.95f - 0.12f + crag * crag * 0.36f + fine * fine * 0.08f);
                    vertices[i * 2] = new Vector3(x, -40f, z);
                    vertices[i * 2 + 1] = new Vector3(x, top + 30f, z);
                    if (i == segments) continue;
                    int k = i * 6, a = i * 2;
                    triangles[k] = a; triangles[k + 1] = a + 2; triangles[k + 2] = a + 1;
                    triangles[k + 3] = a + 2; triangles[k + 4] = a + 3; triangles[k + 5] = a + 1;
                }
                var mesh = new Mesh { name = "CinematicRidge" + band, vertices = vertices, triangles = triangles };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                SaveAsset(mesh, $"{ArtFolder}/CinematicRidge{band}.asset");
                var ridge = new GameObject("Ridge " + band);
                ridge.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = ridge.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // ------------------------------------------------------------------ scenery: ruins, barriers, trees, rocks, spires, growths

        private static void BuildScenery(Art art, Parts parts)
        {
            var parent = new GameObject("Scenery").transform;
            float Range(float a, float b) => parts.Range(a, b);

            // The human line: concrete barriers, broken here and there.
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            for (float z = -84f; z <= 84f; z += 5.6f)
            {
                if (Range(0f, 1f) < 0.18f) continue;
                float x = -61f + Range(-0.6f, 0.6f);
                var barrier = Solid("Barrier", parent, cube, art.concrete, Ground(x, z, 0.38f), Quaternion.Euler(Range(-3f, 3f), Range(-6f, 6f), Range(-3f, 3f)), new Vector3(0.7f, 0.95f, 3.4f));
                barrier.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            }

            // Ruins behind the line: walls with broken tops.
            void Wall(float x, float z, float yaw, float width, float height, int seed)
            {
                Mesh mesh = MakeWallMesh(width, height, 0.6f, seed);
                SaveAsset(mesh, $"{ArtFolder}/CinematicWall{seed}.asset");
                GameObject wall = Solid("Ruin", parent, mesh, art.concrete, Ground(x, z, -0.4f), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                wall.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            }
            Wall(-108f, -58f, 12f, 16f, 7f, 1);
            Wall(-100f, -66f, 102f, 10f, 5.5f, 2);
            Wall(-122f, 28f, -8f, 20f, 9f, 3);
            Wall(-131f, 18f, 82f, 12f, 8f, 4);
            Wall(-96f, 66f, 30f, 14f, 6f, 5);
            Wall(-150f, -20f, 70f, 18f, 11f, 6);
            Wall(-78f, -88f, -20f, 9f, 4.5f, 7);

            // A broken comms tower, tall against the sky, its light blinking.
            Mesh tower = MakeTowerMesh();
            SaveAsset(tower, ArtFolder + "/CinematicTower.asset");
            Solid("Comms Tower", parent, tower, art.metal, Ground(-172f, 6f), Quaternion.Euler(0f, 20f, 2.5f), Vector3.one);
            AddGlowCard(parts, art.redLight, parent, Ground(-172f, 6f, 41.8f) + new Vector3(1.8f, 0f, 0f), 2.2f);

            // Dead trees, burnt to their trunks.
            var trees = new List<Mesh>();
            for (int i = 0; i < 4; i++)
            {
                Mesh mesh = MakeTreeMesh(70 + i);
                SaveAsset(mesh, $"{ArtFolder}/CinematicTree{i}.asset");
                trees.Add(mesh);
            }
            int placed = 0;
            while (placed < 34)
            {
                float x = Range(-260f, 270f), z = Range(-260f, 280f);
                if (FromWalk(x, z) < 30f || (x > -70f && x < 60f && z > -40f && z < 90f && Range(0f, 1f) < 0.7f)) continue;
                float scale = Range(0.7f, 1.4f);
                GameObject tree = Solid("Dead Tree", parent, trees[placed % trees.Count], art.bark, Ground(x, z, -0.3f), Quaternion.Euler(Range(-6f, 6f), Range(0f, 360f), Range(-6f, 6f)), Vector3.one * scale);
                tree.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                placed++;
            }

            // Rocks and rubble over the field.
            Mesh rock = MakeRockMesh();
            SaveAsset(rock, ArtFolder + "/CinematicRock.asset");
            for (int i = 0; i < 260; i++)
            {
                float x = Range(-240f, 260f), z = Range(-250f, 270f);
                if (FromWalk(x, z) < 22f) continue;
                float size = Range(0.2f, 1f) * (Range(0f, 1f) > 0.93f ? 2.4f : 1f);
                bool rubble = x < -50f && Range(0f, 1f) < 0.6f;
                GameObject stone = Solid(rubble ? "Rubble" : "Rock", parent, rubble ? cube : rock, rubble ? art.concrete : art.rock,
                    Ground(x, z, size * 0.1f), Quaternion.Euler(Range(-25f, 25f), Range(0f, 360f), Range(-25f, 25f)),
                    new Vector3(size * Range(0.9f, 1.6f), size * Range(0.45f, 0.9f), size * Range(0.9f, 1.6f)));
                stone.GetComponent<MeshRenderer>().shadowCastingMode = size > 0.8f ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }

            // The Covenant's spires, far behind its army, their seams glowing.
            void Spire(float x, float z, float height, float bend, int seed)
            {
                Mesh shell = MakeSpireMesh(height, bend, false);
                Mesh seam = MakeSpireMesh(height, bend, true);
                SaveAsset(shell, $"{ArtFolder}/CinematicSpire{seed}.asset");
                SaveAsset(seam, $"{ArtFolder}/CinematicSpireSeam{seed}.asset");
                Quaternion turn = Quaternion.Euler(0f, Mathf.Atan2(-x, -z) * Mathf.Rad2Deg, 0f);
                Solid("Spire", parent, shell, art.spire, Ground(x, z, -2f), turn, Vector3.one).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                Solid("Spire Seam", parent, seam, art.spireLine, Ground(x, z, -2f), turn, Vector3.one);
                AddGlowCard(parts, art.covenantGlow, parent, Ground(x, z, -2f) + turn * new Vector3(0f, height, bend * 0.92f), height * 0.12f);
                var lightObject = new GameObject("Spire Light");
                lightObject.transform.SetParent(parent, false);
                lightObject.transform.position = Ground(x, z, height * 0.4f);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.6f, 0.35f, 1f);
                light.range = height * 1.4f;
                light.intensity = 6f;
                light.shadows = LightShadows.None;
            }
            Spire(262f, 292f, 78f, 14f, 1);
            Spire(212f, 318f, 52f, 9f, 2);
            Spire(305f, 238f, 44f, -7f, 3);
        }

        private static GameObject Solid(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var thing = new GameObject(name);
            thing.transform.SetParent(parent, false);
            thing.transform.SetPositionAndRotation(position, rotation);
            thing.transform.localScale = scale;
            thing.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = thing.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return thing;
        }

        /// <summary>A glow that always faces the camera: a light on a tower, a spire's tip, a growth's pustules.</summary>
        private static void AddGlowCard(Parts parts, Material material, Transform parent, Vector3 position, float size)
        {
            var glow = new GameObject("Glow");
            glow.transform.SetParent(parent, false);
            glow.transform.position = position;
            glow.transform.localScale = Vector3.one * size;
            glow.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = glow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            parts.glows.Add(glow.transform);
        }

        // ------------------------------------------------------------------ the armies

        private static GameObject PlaceModel(string prefab, Transform parent, Vector3 position, float yaw, float scale, Vector3? tilt = null)
        {
            string path = UnitFolder + prefab + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + path);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            model.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt ?? Vector3.zero));
            model.transform.localScale *= scale;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.On;
            return model;
        }

        /// <summary>A vehicle on the ground here, sat on its lowest point.</summary>
        private static GameObject PlaceVehicle(string prefab, Transform parent, float x, float z, float yaw, float scale, Vector3? tilt = null, float lift = 0f)
        {
            GameObject model = PlaceModel(prefab, parent, Ground(x, z), yaw, scale, tilt);
            float lowest = float.MaxValue;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            if (lowest < float.MaxValue) model.transform.position += Vector3.up * (GroundHeight(x, z) - lowest + lift);
            return model;
        }

        /// <summary>A wreck: the model burnt black, sunk a little and tipped, burning.</summary>
        private static void PlaceWreck(Art art, Parts parts, Transform parent, string prefab, float x, float z, float yaw, float scale, Vector3 tilt, float fireSize)
        {
            GameObject model = PlaceVehicle(prefab, parent, x, z, yaw, scale, tilt, -0.35f);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = art.charred;
                renderer.sharedMaterials = materials;
            }
            if (fireSize > 0f) AddFire(art, parts, parent, Ground(x, z, 0.8f), fireSize);
        }

        /// <summary>
        /// Something going along the ground again and again, from here toward a point, at its own speed: its line is cut to a whole number of
        /// trips a loop, no longer than the distance given, and it follows the ground's rise and fall.
        /// </summary>
        private static void AddMarcher(Parts parts, Transform body, Vector2 from, Vector2 toward, float speed, float longest, float lift = 0f)
        {
            float loop = Loop;
            int trips = Mathf.Max(1, Mathf.CeilToInt(speed * loop / longest));
            float length = speed * loop / trips;
            Vector2 to = from + (toward - from).normalized * length;
            var heights = new float[24];
            for (int i = 0; i < heights.Length; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)(heights.Length - 1));
                heights[i] = GroundHeight(p.x, p.y) + lift;
            }
            parts.marchers.Add(new MenuCinematic.Marcher
            {
                body = body,
                from = new Vector3(from.x, heights[0], from.y),
                to = new Vector3(to.x, heights[heights.Length - 1], to.y),
                speed = speed,
                offset = parts.Range(0f, length),
                size = body.localScale,
                heights = heights,
            });
        }

        private static void BuildUnsc(MenuCinematic cinematic, Art art, Parts parts)
        {
            var army = new GameObject("UNSC").transform;

            // Armour along the line behind its barriers, facing east; the Elephant behind; a Pelican hovering low over the rear.
            PlaceVehicle("Scorpion_Model", army, -78f, -48f, 82f, 1f);
            PlaceVehicle("Scorpion_Model", army, -75f, -10f, 94f, 1f);
            PlaceVehicle("Scorpion_Model", army, -80f, 24f, 86f, 1f);
            PlaceVehicle("Scorpion_Model", army, -73f, 56f, 100f, 1f);
            PlaceVehicle("Mantis_Model", army, -70f, 4f, 84f, 1.25f);
            PlaceVehicle("Mantis_Model", army, -68f, 39f, 96f, 1.25f);
            PlaceVehicle("Elephant_Model", army, -138f, -8f, 72f, 1.5f);
            PlaceVehicle("Warthog_Model", army, -96f, -72f, 58f, 1.25f);
            PlaceVehicle("Warthog_Model", army, -101f, 44f, 116f, 1.25f);
            GameObject pelican = PlaceModel("Pelican_Model", army, Ground(-124f, -42f, 11f), 75f, 1.5f);
            parts.hovers.Add(pelican.transform);
            AddEngineGlows(pelican, art.engine, parts.glows);

            // Wrecks burning in front of the line and in no man's land.
            PlaceWreck(art, parts, army, "Warthog_Model", -86f, -1f, 140f, 1.25f, new Vector3(16f, 0f, -24f), 1.1f);
            PlaceWreck(art, parts, army, "Scorpion_Model", -28f, 24f, 60f, 1f, new Vector3(-6f, 0f, 9f), 1.6f);
            PlaceWreck(art, parts, army, "Mantis_Model", -44f, -52f, 30f, 1.25f, new Vector3(70f, 0f, 12f), 0.9f);

            // Warthogs racing along behind the line.
            for (int i = 0; i < 2; i++)
            {
                GameObject hog = PlaceVehicle("Warthog_Model", army, -112f, -100f, 0f, 1.25f);
                AddMarcher(parts, hog.transform, new Vector2(-112f - i * 14f, -110f), new Vector2(-104f - i * 14f, 120f), 12f, 210f);
            }

            // The tanks' and the Mantises' guns, now and then: a shell arcing a little and bursting where it lands.
            Vector3[] guns = { Ground(-75f, -48f, 2.6f), Ground(-72f, -10f, 2.6f), Ground(-77f, 24f, 2.6f), Ground(-70f, 56f, 2.6f), Ground(-67f, 4f, 4.2f), Ground(-65f, 39f, 4.2f) };
            float[] aims = { 104f, 66f, 58f, 72f, 118f, 62f };
            for (int i = 0; i < guns.Length; i++) AddShells(art, army, guns[i], aims[i], 0.16f + i * 0.015f, i);
        }

        private static void BuildCovenant(MenuCinematic cinematic, Art art, Parts parts)
        {
            var army = new GameObject("Covenant").transform;
            float Range(float a, float b) => parts.Range(a, b);
            float Face(float x, float z) => Mathf.Atan2(-60f - x, 0f - z) * Mathf.Rad2Deg + Range(-10f, 10f);

            // Wraiths lobbing their mortars at the human line, now and then.
            Vector2[] wraiths = { new Vector2(122f, 152f), new Vector2(168f, 122f), new Vector2(208f, 176f), new Vector2(152f, 208f), new Vector2(232f, 122f) };
            foreach (Vector2 spot in wraiths)
            {
                float yaw = Face(spot.x, spot.y);
                GameObject wraith = PlaceVehicle("Wraith_Model", army, spot.x, spot.y, yaw, 1.5f, null, 0.8f);
                parts.hovers.Add(wraith.transform);
                AddMortars(art, army, wraith.transform.position + Vector3.up * 3.2f, yaw, Range(0.09f, 0.13f));
            }
            // More armour around where the camera stands among them: a Wraith and Ghosts hovering, waiting to go.
            GameObject near = PlaceVehicle("Wraith_Model", army, 170f, 128f, Face(170f, 128f), 1.5f, null, 0.8f);
            parts.hovers.Add(near.transform);
            Vector2[] waiting = { new Vector2(185f, 150f), new Vector2(175f, 158f), new Vector2(158f, 172f), new Vector2(148f, 152f), new Vector2(166f, 140f) };
            foreach (Vector2 spot in waiting)
            {
                GameObject ghost = PlaceVehicle("Ghost_Model", army, spot.x, spot.y, Face(spot.x, spot.y), 1.3f, null, 0.7f);
                parts.hovers.Add(ghost.transform);
            }
            // Phantoms hovering over the army.
            GameObject phantom = PlaceModel("CovenantPhantom_Model", army, Ground(218f, 150f, 30f), 222f, 1.6f);
            parts.hovers.Add(phantom.transform);
            AddEngineGlows(phantom, art.covenantEngine, parts.glows);
            phantom = PlaceModel("CovenantPhantom_Model", army, Ground(140f, 238f, 40f), 205f, 1.6f);
            parts.hovers.Add(phantom.transform);
            AddEngineGlows(phantom, art.covenantEngine, parts.glows);

            // A burnt Ghost and a burning Wraith in no man's land.
            PlaceWreck(art, parts, army, "Ghost_Model", 18f, 72f, 200f, 1.3f, new Vector3(0f, 0f, 70f), 0.8f);
            PlaceWreck(art, parts, army, "Wraith_Model", 58f, 104f, 236f, 1.5f, new Vector3(-8f, 0f, 6f), 1.8f);

            // Ghosts racing in.
            for (int i = 0; i < 4; i++)
            {
                GameObject ghost = PlaceVehicle("Ghost_Model", army, 200f, 200f, 0f, 1.3f, null, 0.6f);
                Vector2 from = new Vector2(Range(170f, 240f), Range(140f, 250f));
                AddMarcher(parts, ghost.transform, from, new Vector2(-40f, Range(-40f, 80f)), 15f, 200f, 0.6f);
            }
        }

        private static void BuildFlood(MenuCinematic cinematic, Art art, Parts parts)
        {
            var horde = new GameObject("Flood").transform;
            float Range(float a, float b) => parts.Range(a, b);
            float Face(float x, float z) => Mathf.Atan2(-60f - x, 0f - z) * Mathf.Rad2Deg + Range(-12f, 12f);

            // The Flood's growths: bulbs and tendrils of biomass over the creep, some glowing.
            var growths = new List<Mesh>();
            for (int i = 0; i < 5; i++)
            {
                Mesh mesh = MakeGrowthMesh(300 + i);
                SaveAsset(mesh, $"{ArtFolder}/CinematicGrowth{i}.asset");
                growths.Add(mesh);
            }
            Vector2[] growthSpots =
            {
                new Vector2(198f, -150f), new Vector2(225f, -112f), new Vector2(178f, -205f), new Vector2(140f, -200f), new Vector2(240f, -180f),
                new Vector2(105f, -168f), new Vector2(210f, -70f), new Vector2(255f, -140f), new Vector2(160f, -235f), new Vector2(75f, -140f),
                new Vector2(120f, -60f), new Vector2(190f, -102f), new Vector2(262f, -95f), new Vector2(222f, -222f), new Vector2(92f, -205f),
                new Vector2(146f, -128f), new Vector2(112f, -138f),
            };
            for (int i = 0; i < growthSpots.Length; i++)
            {
                Vector2 spot = growthSpots[i];
                float scale = Range(0.8f, 1.8f) * (i < 4 ? 1.5f : 1f);
                GameObject growth = Solid("Flood Growth", horde, growths[i % growths.Count], i % 3 == 0 ? art.growthDark : art.growth,
                    Ground(spot.x, spot.y, -0.5f), Quaternion.Euler(0f, Range(0f, 360f), 0f), Vector3.one * scale);
                growth.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                if (i % 2 == 0) AddGlowCard(parts, art.floodGlow, horde, Ground(spot.x, spot.y, 1.6f * scale), 2.4f * scale);
            }

            // The infested vehicles, overgrown: a Wraith in front, Scorpions, Warthogs and another Wraith behind it; an infected Phantom over them.
            GameObject wraith = PlaceVehicle("InfestedWraith_Model", horde, 130f, -122f, 316f, 1.5f, null, 0.5f);
            parts.hovers.Add(wraith.transform);
            PlaceVehicle("InfestedScorpion_Model", horde, 118f, -90f, 310f, 1f);
            PlaceVehicle("InfestedScorpion_Model", horde, 152f, -142f, 308f, 1f);
            PlaceVehicle("InfestedWarthog_Model", horde, 176f, -142f, 326f, 1.25f);
            PlaceVehicle("InfestedWarthog_Model", horde, 160f, -102f, 318f, 1.25f);
            GameObject far = PlaceVehicle("InfestedWraith_Model", horde, 202f, -96f, 298f, 1.5f, null, 0.5f);
            parts.hovers.Add(far.transform);
            GameObject phantom = PlaceModel("InfectedPhantom_Model", horde, Ground(158f, -158f, 24f), Face(158f, -158f), 1.6f);
            parts.hovers.Add(phantom.transform);

            // Infested Ghosts racing at the human line.
            for (int i = 0; i < 4; i++)
            {
                GameObject ghost = PlaceVehicle("InfestedGhost_Model", horde, 180f, -180f, 0f, 1.3f, null, 0.6f);
                Vector2 from = new Vector2(Range(150f, 220f), Range(-210f, -120f));
                AddMarcher(parts, ghost.transform, from, new Vector2(-40f, Range(-70f, 0f)), 13f, 200f, 0.6f);
            }

            // Spores drifting over the creep.
            ParticleSystem spores = MakeParticles("Spores", horde, Ground(160f, -150f, 6f), Quaternion.identity, art.spore);
            ParticleSystem.MainModule main = spores.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.22f);
            main.maxParticles = 1400;
            main.prewarm = true;
            Rate(spores, 70f);
            Box(spores, new Vector3(150f, 12f, 150f));
            Drift(spores, new Vector2(-0.6f, 0.2f), new Vector2(0.05f, 0.5f), new Vector2(-0.2f, 0.4f));
            Wobble(spores, 0.5f, 0.15f);
            FadeInOut(spores, 0.2f, 0.7f);
        }

        private static void BuildWalker(MenuCinematic cinematic, Art art)
        {
            var root = new GameObject("Scarab").transform;
            GameObject body = PlaceModel("Scarab_Model", root, Vector3.zero, 0f, WalkerScale);
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            cinematic.walkerRoot = root;
            cinematic.walkerBody = body.transform;
            cinematic.walkerFrom = new Vector3(WalkerFrom.x, 0f, WalkerFrom.y);
            cinematic.walkerTo = new Vector3(WalkerTo.x, 0f, WalkerTo.y);
            Vector3 path = cinematic.walkerTo - cinematic.walkerFrom;
            root.SetPositionAndRotation(cinematic.walkerFrom, Quaternion.LookRotation(path));

            Transform muzzle = null;
            int legs = 0;
            foreach (Transform node in body.GetComponentsInChildren<Transform>(true))
            {
                if (node.name.StartsWith("Leg_")) legs++;
                else if (node.name.StartsWith("Muzzle_") && (muzzle == null || node.name == "Muzzle_0")) muzzle = node;
            }
            Debug.Log($"Menu cinematic Scarab: {legs} legs, muzzle {(muzzle != null ? muzzle.name : "none")}, walking {path.magnitude:0} m a loop.");

            var beamObject = new GameObject("Beam");
            var line = beamObject.AddComponent<LineRenderer>();
            line.sharedMaterial = art.beam;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            cinematic.beam = line;
            cinematic.beamFrom = muzzle != null ? muzzle : body.transform;
            // Its end sweeps across the ground in front of the human line, toward the camera's side of it.
            cinematic.beamFromPoint = Ground(-14f, 46f);
            cinematic.beamToPoint = Ground(-50f, -22f);
            cinematic.beamPeriod = Loop / 2f;
            cinematic.beamStart = 18.5f;
            cinematic.beamSeconds = 5f;
            cinematic.beamWidth = 3.4f;
            cinematic.beamLightIntensity = 110f;

            var lightObject = new GameObject("Beam Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.68f, 1f);
            light.range = 110f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            cinematic.beamLight = light;

            // Where it strikes: blue fire and sparks thrown up, the fire left burning along its path for a moment.
            ParticleSystem impact = MakeParticles("Beam Impact", null, cinematic.beamFromPoint, Quaternion.identity, art.beamFire);
            ParticleSystem.MainModule main = impact.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 600;
            main.gravityModifier = -0.15f;
            Rate(impact, 90f);
            ParticleSystem.ShapeModule shape = impact.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Grow(impact, 0.6f, 1.6f);
            FadeInOut(impact, 0.05f, 0.4f);
            ParticleSystem.EmissionModule emission = impact.emission;
            emission.enabled = false;
            ParticleSystem sparks = MakeParticles("Beam Sparks", impact.transform, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), art.blueSpark);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 30f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 500;
            Rate(sparks, 140f);
            ParticleSystem.ShapeModule cone = sparks.shape;
            cone.shapeType = ParticleSystemShapeType.Cone;
            cone.angle = 38f;
            cone.radius = 1.2f;
            Stretch(sparks, 0.05f);
            emission = sparks.emission;
            emission.enabled = false;
            cinematic.beamImpact = impact;
        }

        // ------------------------------------------------------------------ the fighting: fire, blasts, smoke, fog

        private static void BuildEffects(MenuCinematic cinematic, Art art, Parts parts)
        {
            var parent = new GameObject("Battle Effects").transform;

            // Blasts among each army, and far-off flashes on the horizon.
            AddBlast(art, parts, parent, "Shells on the Covenant", false, 2.7f, 0.2f, 26f,
                new Vector2(130f, 170f), new Vector2(178f, 138f), new Vector2(196f, 202f), new Vector2(108f, 124f), new Vector2(160f, 228f), new Vector2(222f, 150f),
                new Vector2(142f, 192f), new Vector2(186f, 166f), new Vector2(92f, 98f), new Vector2(205f, 128f));
            AddBlast(art, parts, parent, "Shells on the Flood", false, 2.4f, 1.1f, 26f,
                new Vector2(120f, -140f), new Vector2(150f, -112f), new Vector2(102f, -116f), new Vector2(166f, -160f), new Vector2(142f, -96f), new Vector2(186f, -126f),
                new Vector2(126f, -172f), new Vector2(104f, -150f), new Vector2(150f, -142f), new Vector2(160f, -84f));
            AddBlast(art, parts, parent, "Plasma on the line", true, 2.6f, 0.6f, 22f,
                new Vector2(-70f, -36f), new Vector2(-86f, 12f), new Vector2(-58f, 42f), new Vector2(-116f, -76f), new Vector2(-76f, 66f), new Vector2(-112f, -20f),
                new Vector2(-55f, -4f), new Vector2(-92f, 36f), new Vector2(-48f, 18f), new Vector2(-104f, 70f));
            var far = new List<Vector2>();
            for (int i = 0; i < 14; i++)
            {
                float angle = (-60f + i * 23f + parts.Range(-8f, 8f)) * Mathf.Deg2Rad;
                float distance = parts.Range(420f, 760f);
                far.Add(new Vector2(Mathf.Sin(angle) * distance, Mathf.Cos(angle) * distance));
            }
            AddFarFlashes(art, parts, parent, far.ToArray());

            // Fires burning on the field, besides the wrecks'.
            AddFire(art, parts, parent, Ground(118f, 186f, 0.4f), 1.4f);
            AddFire(art, parts, parent, Ground(-118f, 30f, 0.4f), 1f);
            AddFire(art, parts, parent, Ground(145f, -160f, 0.4f), 1.2f);
            AddFire(art, parts, parent, Ground(70f, -40f, 0.4f), 0.9f);
            AddFire(art, parts, parent, Ground(-4f, 110f, 0.4f), 1f);

            // Columns of smoke on the skyline.
            Vector3[] columns = { Ground(-220f, 180f), Ground(320f, 60f), Ground(260f, -260f), Ground(-160f, -260f), Ground(80f, 330f), Ground(420f, 260f), Ground(-330f, -40f), Ground(150f, 420f) };
            foreach (Vector3 column in columns) AddSmoke(art.smoke, parent, column, 9f, 5f, 12f, 52f, 40f, new Color(0.11f, 0.085f, 0.085f, 0.24f), 12f);

            // Fog lying low over each part of the field, tinted by what is in it.
            AddFog(art, parent, Ground(-90f, 0f, 1.5f), new Vector3(140f, 3f, 220f), new Color(0.42f, 0.32f, 0.3f, 0.07f));
            AddFog(art, parent, Ground(40f, 40f, 1.5f), new Vector3(180f, 3f, 200f), new Color(0.4f, 0.3f, 0.3f, 0.06f));
            AddFog(art, parent, Ground(165f, 180f, 1.5f), new Vector3(170f, 3f, 170f), new Color(0.34f, 0.28f, 0.4f, 0.07f));
            AddFog(art, parent, Ground(160f, -150f, 1.5f), new Vector3(170f, 3f, 170f), new Color(0.4f, 0.42f, 0.25f, 0.09f));
        }

        private static void AddFire(Art art, Parts parts, Transform parent, Vector3 at, float size)
        {
            ParticleSystem fire = MakeParticles("Fire", parent, at, Quaternion.Euler(-90f, 0f, 0f), art.fire);
            ParticleSystem.MainModule main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * size, 3.2f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f * size, 2.2f * size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 120;
            main.prewarm = true;
            Rate(fire, 40f);
            ParticleSystem.ShapeModule shape = fire.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.9f * size;
            Grow(fire, 1f, 0.3f);
            FadeInOut(fire, 0.1f, 0.5f);
            AddSmoke(art.smoke, parent, at + Vector3.up * 2f * size, 0.8f * size, 2.2f, 2f * size, 12f * size, 16f, new Color(0.07f, 0.06f, 0.06f, 0.45f), 5f);

            var lightObject = new GameObject("Fire Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = at + Vector3.up * 2.5f * size;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.2f);
            light.range = 16f * size;
            light.intensity = 5f * size;
            light.shadows = LightShadows.None;
            parts.flickers.Add(new MenuCinematic.Flicker { light = light, intensity = light.intensity, amount = 0.45f, speed = 8f + parts.Range(0f, 4f), phase = parts.Range(0f, 50f) });
        }

        /// <summary>A kind of blast going off at the next of these spots every so often: a flash, a fireball, sparks thrown up and smoke.</summary>
        private static void AddBlast(Art art, Parts parts, Transform parent, string name, bool plasma, float period, float offset, float lightIntensity, params Vector2[] spots)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            var systems = new List<ParticleSystem>();
            var counts = new List<int>();

            ParticleSystem flash = MakeParticles("Flash", group, Vector3.zero, Quaternion.identity, plasma ? art.blueFlash : art.flash);
            ParticleSystem.MainModule main = flash.main;
            main.startLifetime = 0.18f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(14f, 20f);
            main.maxParticles = 10;
            Burst(flash);
            Grow(flash, 0.6f, 1.2f);
            FadeInOut(flash, 0f, 0.1f);
            systems.Add(flash);
            counts.Add(1);

            ParticleSystem ball = MakeParticles("Fireball", group, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), plasma ? art.blueFlash : art.fire);
            main = ball.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 80;
            main.gravityModifier = -0.2f;
            Burst(ball);
            ParticleSystem.ShapeModule shape = ball.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1.5f;
            Grow(ball, 0.5f, 1.8f);
            FadeInOut(ball, 0f, 0.35f);
            systems.Add(ball);
            counts.Add(10);

            ParticleSystem sparks = MakeParticles("Debris", group, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), plasma ? art.blueSpark : art.spark);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 28f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 200;
            Burst(sparks);
            shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.8f;
            Stretch(sparks, 0.045f);
            systems.Add(sparks);
            counts.Add(26);

            ParticleSystem smoke = MakeParticles("Smoke", group, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), art.smoke);
            main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = plasma ? new Color(0.16f, 0.15f, 0.2f, 0.5f) : new Color(0.1f, 0.085f, 0.08f, 0.6f);
            main.maxParticles = 160;
            Burst(smoke);
            shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 2f;
            Grow(smoke, 0.6f, 2.6f);
            FadeInOut(smoke, 0.05f, 0.4f);
            Drift(smoke, new Vector2(-1.2f, -0.4f), new Vector2(0.4f, 1.2f), new Vector2(-0.3f, 0.3f));
            systems.Add(smoke);
            counts.Add(7);

            var lightObject = new GameObject("Blast Light");
            lightObject.transform.SetParent(group, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = plasma ? new Color(0.5f, 0.65f, 1f) : new Color(1f, 0.6f, 0.3f);
            light.range = 45f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            var positions = new Vector3[spots.Length];
            for (int i = 0; i < spots.Length; i++) positions[i] = Ground(spots[i].x, spots[i].y, 0.3f);
            parts.blasts.Add(new MenuCinematic.Blast
            {
                parts = systems.ToArray(),
                counts = counts.ToArray(),
                light = light,
                intensity = lightIntensity,
                lightSeconds = 0.6f,
                spots = positions,
                period = period,
                offset = offset,
            });
        }

        private static void AddFarFlashes(Art art, Parts parts, Transform parent, Vector2[] spots)
        {
            ParticleSystem flash = MakeParticles("Far Flashes", parent, Vector3.zero, Quaternion.identity, art.farFlash);
            ParticleSystem.MainModule main = flash.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(28f, 60f);
            main.maxParticles = 20;
            Burst(flash);
            Grow(flash, 0.5f, 1.3f);
            FadeInOut(flash, 0f, 0.15f);
            ParticleSystemRenderer renderer = flash.GetComponent<ParticleSystemRenderer>();
            renderer.maxParticleSize = 4f;
            var positions = new Vector3[spots.Length];
            for (int i = 0; i < spots.Length; i++) positions[i] = Ground(spots[i].x, spots[i].y, 8f);
            parts.blasts.Add(new MenuCinematic.Blast { parts = new[] { flash }, counts = new[] { 1 }, spots = positions, period = 0.47f, offset = 0.1f });
        }

        private static void AddFog(Art art, Transform parent, Vector3 at, Vector3 size, Color color)
        {
            ParticleSystem fog = MakeParticles("Fog", parent, at, Quaternion.identity, art.fog);
            ParticleSystem.MainModule main = fog.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(22f, 34f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = 260;
            main.prewarm = true;
            Rate(fog, 8f);
            Box(fog, size);
            Drift(fog, new Vector2(-1.1f, -0.4f), new Vector2(-0.05f, 0.08f), new Vector2(-0.3f, 0.3f));
            FadeInOut(fog, 0.25f, 0.65f);
            ParticleSystem.RotationOverLifetimeModule spin = fog.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        }

        private static void AddSmoke(Material material, Transform parent, Vector3 at, float baseWidth, float speed, float startSize, float endSize, float lifetime, Color color, float rate)
        {
            ParticleSystem particles = MakeParticles("Smoke", parent, at, Quaternion.identity, material);
            ParticleSystem.MainModule main = particles.main;
            main.prewarm = true;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.8f, startSize * 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = Mathf.CeilToInt(rate * lifetime * 1.2f);
            Rate(particles, rate);
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = baseWidth;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            // The wind blows from the north-east, from the Covenant's side.
            Drift(particles, new Vector2(-speed * 0.45f, -speed * 0.12f), new Vector2(speed * 0.2f, speed * 0.4f), new Vector2(-speed * 0.35f, -speed * 0.05f));
            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(endSize / startSize, AnimationCurve.EaseInOut(0f, startSize / endSize, 1f, 1f));
            FadeInOut(particles, 0.08f, 0.5f);
            particles.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.YoungestInFront;
        }

        // ------------------------------------------------------------------ the guns

        /// <summary>A tank's gun: a shell now and then with a flash at the muzzle, falling a little, bursting where it meets the ground.</summary>
        private static void AddShells(Art art, Transform parent, Vector3 muzzle, float yaw, float rate, int index)
        {
            ParticleSystem shells = MakeParticles("Shells", parent, muzzle, Quaternion.Euler(-2.2f, yaw, 0f), art.shell);
            ParticleSystem.MainModule main = shells.main;
            main.startLifetime = 3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(115f, 130f);
            main.startSize = 0.5f;
            main.gravityModifier = 0.3f;
            main.maxParticles = 20;
            Rate(shells, rate);
            ParticleSystem.ShapeModule shape = shells.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 1.5f;
            shape.radius = 0.05f;
            Stretch(shells, 0.06f);
            Impacts(art, shells, false);

            ParticleSystem flash = MakeParticles("Muzzle Flash", shells.transform, Vector3.zero, Quaternion.identity, art.flash);
            main = flash.main;
            main.startLifetime = 0.1f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.maxParticles = 4;
            Burst(flash, 1);
            FadeInOut(flash, 0f, 0.1f);
            ParticleSystem.SubEmittersModule sub = shells.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(flash, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        /// <summary>A Wraith's mortar: a slow glowing ball lobbed high, trailing light, bursting blue where it lands.</summary>
        private static void AddMortars(Art art, Transform parent, Vector3 at, float yaw, float rate)
        {
            ParticleSystem mortars = MakeParticles("Mortars", parent, at, Quaternion.Euler(-42f, yaw, 0f), art.mortar);
            ParticleSystem.MainModule main = mortars.main;
            main.startLifetime = 9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(36f, 44f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 1.8f);
            main.gravityModifier = 0.95f;
            main.maxParticles = 12;
            Rate(mortars, rate);
            ParticleSystem.ShapeModule shape = mortars.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.1f;
            ParticleSystem.TrailModule trails = mortars.trails;
            trails.enabled = true;
            trails.lifetime = 0.07f;
            trails.minVertexDistance = 0.6f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 0f));
            trails.inheritParticleColor = true;
            mortars.GetComponent<ParticleSystemRenderer>().trailMaterial = art.mortarTrail;
            Impacts(art, mortars, true);
        }

        /// <summary>Makes a gun's rounds burst where they meet the ground (taken as flat, at its middle height): a flash, fire and dirt.</summary>
        private static void Impacts(Art art, ParticleSystem rounds, bool plasma)
        {
            var plane = new GameObject("Ground Plane").transform;
            plane.SetParent(rounds.transform, false);
            plane.SetPositionAndRotation(new Vector3(rounds.transform.position.x, 0.2f, rounds.transform.position.z), Quaternion.identity);
            ParticleSystem.CollisionModule collision = rounds.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, plane);
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            collision.radiusScale = 0.1f;

            ParticleSystem burst = MakeParticles("Impact", rounds.transform, Vector3.zero, Quaternion.identity, plasma ? art.blueFlash : art.fire);
            ParticleSystem.MainModule main = burst.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 120;
            Burst(burst, 7);
            ParticleSystem.ShapeModule shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Grow(burst, 0.5f, 1.7f);
            FadeInOut(burst, 0f, 0.3f);

            ParticleSystem dirt = MakeParticles("Impact Dirt", rounds.transform, Vector3.zero, Quaternion.identity, art.smoke);
            main = dirt.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.14f, 0.11f, 0.09f, 0.65f);
            main.gravityModifier = 0.5f;
            main.maxParticles = 160;
            Burst(dirt, 6);
            shape = dirt.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Grow(dirt, 0.5f, 2.2f);
            FadeInOut(dirt, 0.02f, 0.4f);

            ParticleSystem.SubEmittersModule sub = rounds.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(burst, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
            sub.AddSubEmitter(dirt, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        // ------------------------------------------------------------------ aircraft

        private static void BuildFlyers(Art art, Parts parts)
        {
            var parent = new GameObject("Aircraft").transform;
            float loop = Loop;

            // Each group crosses once a loop, in the middle of its crossing at the moment given (in the shot that shows it best).
            void Group(string prefab, Material engine, Vector3 from, Vector3 to, float seconds, float middleAt, float bank, float scale, params Vector3[] places)
            {
                for (int i = 0; i < places.Length; i++)
                {
                    GameObject model = PlaceModel(prefab, parent, Vector3.zero, 0f, scale);
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (engine != null) AddEngineGlows(model, engine, parts.glows);
                    parts.flyers.Add(new MenuCinematic.Flyer
                    {
                        body = model.transform,
                        from = from + places[i],
                        to = to + places[i],
                        period = loop,
                        start = seconds * 0.5f - middleAt + i * 0.4f,
                        duration = seconds,
                        bank = bank,
                        bobPhase = i * 1.7f,
                    });
                }
            }

            // UNSC: Pelicans flying up to the line, a pair passing over it; Hornets going in; a Longsword high and fast.
            Group("Pelican_Model", art.engine, new Vector3(-330f, 34f, -300f), new Vector3(240f, 40f, -40f), 30f, 6f, -6f, 1.5f,
                Vector3.zero, new Vector3(-28f, -5f, 22f), new Vector3(-60f, 6f, -18f));
            Group("Pelican_Model", art.engine, new Vector3(-170f, 30f, -150f), new Vector3(60f, 48f, 160f), 16f, 16.5f, 8f, 1.5f,
                Vector3.zero, new Vector3(24f, 6f, -20f));
            Group("Hornet_Model", art.engine, new Vector3(-220f, 26f, 20f), new Vector3(300f, 40f, 240f), 13f, 25f, -10f, 1.3f,
                Vector3.zero, new Vector3(-14f, 3f, -12f));
            Group("Longsword_Model", art.engine, new Vector3(-420f, 150f, 380f), new Vector3(560f, 120f, -160f), 7f, 7f, -20f, 1.6f, Vector3.zero);
            Group("Hornet_Model", art.engine, new Vector3(-260f, 30f, -120f), new Vector3(280f, 44f, -230f), 12f, 36f, 10f, 1.3f,
                Vector3.zero, new Vector3(-16f, 4f, 10f));

            // Covenant: Banshees diving across the field at the human line, again and again.
            Group("Banshee_Model", art.covenantEngine, new Vector3(300f, 48f, 170f), new Vector3(-220f, 18f, -50f), 14f, 17f, 14f, 1.4f,
                Vector3.zero, new Vector3(16f, 4f, 10f), new Vector3(-10f, 6f, 20f), new Vector3(30f, -2f, -6f));
            Group("Banshee_Model", art.covenantEngine, new Vector3(260f, 40f, 290f), new Vector3(-80f, 24f, -20f), 13f, 26.5f, -12f, 1.4f,
                Vector3.zero, new Vector3(14f, 5f, -12f), new Vector3(-12f, 3f, 14f));
            Group("Banshee_Model", art.covenantEngine, new Vector3(260f, 50f, 140f), new Vector3(-200f, 26f, -110f), 11f, 46f, 16f, 1.4f,
                Vector3.zero, new Vector3(18f, 6f, 8f));

            // The Flood's infested fliers, sweeping over the swarm.
            Group("InfestedBanshee_Model", null, new Vector3(240f, 28f, -280f), new Vector3(-60f, 34f, 30f), 15f, 36.5f, -9f, 1.4f,
                Vector3.zero, new Vector3(-14f, 4f, -12f));
            Group("InfestedHornet_Model", null, new Vector3(260f, 36f, -150f), new Vector3(-120f, 30f, -260f), 14f, 34f, 8f, 1.3f,
                Vector3.zero, new Vector3(12f, -3f, 14f));
            Group("InfectedPelican_Model", null, new Vector3(320f, 70f, -340f), new Vector3(-260f, 60f, 60f), 28f, 4f, -4f, 1.5f, Vector3.zero);
        }

        /// <summary>Two engine glows at the tail of an aircraft, which is taken to fly along +z.</summary>
        private static void AddEngineGlows(GameObject model, Material material, List<Transform> glows)
        {
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            Vector3 size = model.transform.InverseTransformVector(bounds.size);
            for (int side = -1; side <= 1; side += 2)
            {
                var glow = new GameObject("Engine Glow");
                glow.transform.SetParent(model.transform, false);
                glow.transform.position = bounds.center + model.transform.rotation * new Vector3(side * bounds.extents.x * 0.32f, bounds.extents.y * 0.1f, -bounds.extents.z * 0.92f);
                glow.transform.localScale = Vector3.one * Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)) * 0.13f;
                glow.AddComponent<MeshFilter>().sharedMesh = _quad;
                var renderer = glow.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                glows.Add(glow.transform);
            }
        }

        // ------------------------------------------------------------------ the shots

        /// <summary>The walker's place at this moment of the loop.</summary>
        private static Vector3 WalkerAt(float time)
        {
            Vector2 at = Vector2.Lerp(WalkerFrom, WalkerTo, Mathf.Repeat(time, Loop) / Loop);
            return new Vector3(at.x, 0f, at.y);
        }

        /// <summary>Where to look from here so that this subject sits this share of the way from the middle to the right edge of a 16:9 screen.</summary>
        private static Vector3 Aim(Vector3 camera, Vector3 subject, float share, float fieldOfView)
        {
            float halfWidth = Mathf.Atan(Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * 16f / 9f);
            float angle = Mathf.Atan(share * Mathf.Tan(halfWidth)) * Mathf.Rad2Deg;
            return camera + Quaternion.AngleAxis(-angle, Vector3.up) * (subject - camera);
        }

        private static void BuildShots(MenuCinematic cinematic)
        {
            var shots = new List<MenuCinematic.Shot>();
            void Add(string name, Vector3 from, Vector3 to, Vector3 subjectFrom, Vector3 subjectTo, float share, float fieldOfView, float fieldOfViewTo)
            {
                float seconds = ShotSeconds[shots.Count];
                shots.Add(new MenuCinematic.Shot
                {
                    name = name,
                    seconds = seconds,
                    from = from,
                    to = to,
                    fromLook = Aim(from, subjectFrom, share, fieldOfView),
                    toLook = Aim(to, subjectTo, share, fieldOfViewTo),
                    fieldOfView = fieldOfView,
                    fieldOfViewTo = fieldOfViewTo,
                });
            }
            Vector3 Walker(float time, float up) => WalkerAt(time) + Vector3.up * up;

            // 1. The whole field from high behind the human line: a slow push in and down toward the walker.
            Add("Vista", Ground(-205f, -132f, 40f), Ground(-178f, -110f, 30f), Walker(0f, 10f), Walker(11f, 10f), 0.28f, 37f, 34f);
            // 2. Low along the human line as the tanks and soldiers fire, the walker far off across the field; the beam sweeps in front.
            Add("Line", Ground(-92f, -58f, 2.2f), Ground(-92f, -26f, 2.4f), Walker(11f, 16f), Walker(21f, 16f), 0.42f, 34f, 34f);
            // 3. Among the Covenant, behind its soldiers, the walker striding away from us toward the humans.
            Add("Covenant", Ground(206f, 212f, 2.6f), Ground(192f, 190f, 4.2f), Walker(21f, 18f), Walker(31f, 18f), 0.34f, 37f, 34f);
            // 4. Low on the Flood's creep, among its growths: an infested Wraith, Ghosts racing past toward the humans.
            Add("Flood", Ground(103f, -93f, 1.5f), Ground(111f, -101f, 2f), Ground(130f, -122f, 2.4f), Ground(130f, -122f, 2.8f), 0.4f, 38f, 36f);
            // 5. Up at the walker from the human side as it fires its beam across the field; the camera rises.
            Add("Titan", Ground(-12f, -38f, 1.7f), Ground(-6f, -32f, 6f), Walker(41f, 22f), Walker(52f, 26f), 0.42f, 44f, 41f);
            cinematic.shots = shots.ToArray();
            cinematic.fadeSeconds = 0.9f;
        }

        // ------------------------------------------------------------------ particle helpers

        private static ParticleSystem MakeParticles(string name, Transform parent, Vector3 position, Quaternion rotation, Material material)
        {
            var thing = new GameObject(name);
            if (parent != null) thing.transform.SetParent(parent, false);
            thing.transform.SetPositionAndRotation(position, rotation);
            if (parent != null && parent.GetComponent<Camera>() != null) thing.transform.SetLocalPositionAndRotation(position, rotation);
            var particles = thing.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 2f;
            return particles;
        }

        private static void Rate(ParticleSystem particles, float rate)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
        }

        /// <summary>
        /// No steady emission: with no count, it plays emitting nothing but what it is told to (a blast); with a count, it is a sub-emitter,
        /// giving out this many each time it fires and never playing on its own.
        /// </summary>
        private static void Burst(ParticleSystem particles, int count = 0)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            if (count <= 0) return;
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Box(ParticleSystem particles, Vector3 size)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
        }

        private static void Drift(ParticleSystem particles, Vector2 x, Vector2 y, Vector2 z)
        {
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(x.x, x.y);
            velocity.y = new ParticleSystem.MinMaxCurve(y.x, y.y);
            velocity.z = new ParticleSystem.MinMaxCurve(z.x, z.y);
        }

        private static void Wobble(ParticleSystem particles, float strength, float frequency)
        {
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
        }

        private static void Grow(ParticleSystem particles, float from, float to)
        {
            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            float most = Mathf.Max(from, to);
            grow.size = new ParticleSystem.MinMaxCurve(most, AnimationCurve.EaseInOut(0f, from / most, 1f, to / most));
        }

        private static void FadeInOut(ParticleSystem particles, float inUntil, float outFrom)
        {
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                inUntil > 0f
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inUntil), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
        }

        /// <summary>Drawn stretched along its flight, a streak.</summary>
        private static void Stretch(ParticleSystem particles, float velocityScale)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = 1f;
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>A wall with a broken top: a slab whose top edge falls away in steps and jags, in flat-shaded quads.</summary>
        private static Mesh MakeWallMesh(float width, float height, float thickness, int seed)
        {
            var random = new Random(seed * 31 + 7);
            int columns = Mathf.Max(4, Mathf.RoundToInt(width / 0.7f));
            var tops = new float[columns + 1];
            float level = height;
            for (int i = 0; i <= columns; i++)
            {
                if (random.NextDouble() < 0.18) level = height * (0.35f + (float)random.NextDouble() * 0.65f);
                tops[i] = Mathf.Max(0.6f, level - (float)random.NextDouble() * 0.7f);
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
            float half = thickness * 0.5f;
            for (int i = 0; i < columns; i++)
            {
                float x0 = -width * 0.5f + width * i / columns, x1 = -width * 0.5f + width * (i + 1) / columns;
                float y0 = tops[i], y1 = tops[i + 1];
                Quad(new Vector3(x0, 0f, -half), new Vector3(x0, y0, -half), new Vector3(x1, y1, -half), new Vector3(x1, 0f, -half));
                Quad(new Vector3(x1, 0f, half), new Vector3(x1, y1, half), new Vector3(x0, y0, half), new Vector3(x0, 0f, half));
                Quad(new Vector3(x0, y0, -half), new Vector3(x0, y0, half), new Vector3(x1, y1, half), new Vector3(x1, y1, -half));
            }
            float left = -width * 0.5f, right = width * 0.5f;
            Quad(new Vector3(left, 0f, half), new Vector3(left, tops[0], half), new Vector3(left, tops[0], -half), new Vector3(left, 0f, -half));
            Quad(new Vector3(right, 0f, -half), new Vector3(right, tops[columns], -half), new Vector3(right, tops[columns], half), new Vector3(right, 0f, half));
            var mesh = new Mesh { name = "CinematicWall" + seed };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A lattice mast, tapering, broken off near its top and leaning.</summary>
        private static Mesh MakeTowerMesh()
        {
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var parts = new List<CombineInstance>();
            const float height = 42f, bottom = 3.2f, top = 0.7f;
            Vector3 Corner(int corner, float y)
            {
                float half = Mathf.Lerp(bottom, top, y / height);
                return new Vector3((corner & 1) == 0 ? -half : half, y, (corner & 2) == 0 ? -half : half);
            }
            void Strut(Vector3 a, Vector3 b, float thickness)
            {
                Vector3 middle = (a + b) * 0.5f, along = b - a;
                parts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(middle, Quaternion.LookRotation(along), new Vector3(thickness, thickness, along.magnitude)) });
            }
            for (int corner = 0; corner < 4; corner++) Strut(Corner(corner, 0f), Corner(corner, height), 0.22f);
            int[] around = { 0, 1, 3, 2, 0 };
            for (float y = 0f; y < height - 1f; y += 4.5f)
            {
                for (int side = 0; side < 4; side++)
                {
                    Strut(Corner(around[side], y), Corner(around[side + 1], y), 0.12f);
                    Strut(Corner(around[side], y), Corner(around[side + 1], Mathf.Min(height, y + 4.5f)), 0.08f);
                }
            }
            var mesh = new Mesh { name = "CinematicTower" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A tube along a line of points, each with its own radius.</summary>
        private static Mesh MakeTube(IList<Vector3> spine, IList<float> radii, int sides)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < spine.Count; i++)
            {
                Vector3 along = (spine[Mathf.Min(i + 1, spine.Count - 1)] - spine[Mathf.Max(i - 1, 0)]).normalized;
                if (normal == Vector3.zero) normal = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                normal = Vector3.ProjectOnPlane(normal, along).normalized; // Carried along from the ring before, so the tube doesn't twist.
                Vector3 binormal = Vector3.Cross(along, normal);
                for (int s = 0; s < sides; s++)
                {
                    float angle = s / (float)sides * Mathf.PI * 2f;
                    vertices.Add(spine[i] + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radii[i]);
                }
                if (i == 0) continue;
                int ring = i * sides, previous = (i - 1) * sides;
                for (int s = 0; s < sides; s++)
                {
                    int next = (s + 1) % sides;
                    triangles.Add(previous + s); triangles.Add(ring + s); triangles.Add(ring + next);
                    triangles.Add(previous + s); triangles.Add(ring + next); triangles.Add(previous + next);
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>A burnt tree: a crooked trunk and a few broken limbs.</summary>
        private static Mesh MakeTreeMesh(int seed)
        {
            var random = new Random(seed);
            float R() => (float)random.NextDouble();
            var parts = new List<CombineInstance>();
            float height = 6f + R() * 5f;
            var spine = new List<Vector3>();
            var radii = new List<float>();
            Vector3 lean = new Vector3(R() - 0.5f, 0f, R() - 0.5f) * 0.25f;
            for (int i = 0; i <= 7; i++)
            {
                float t = i / 7f;
                spine.Add(new Vector3(Mathf.Sin(t * 5f + seed) * 0.25f, t * height, Mathf.Cos(t * 4f + seed) * 0.2f) + lean * t * height);
                radii.Add(Mathf.Lerp(0.38f, 0.05f, t));
            }
            parts.Add(new CombineInstance { mesh = MakeTube(spine, radii, 7), transform = Matrix4x4.identity });
            int limbs = 2 + random.Next(3);
            for (int b = 0; b < limbs; b++)
            {
                float t = 0.45f + R() * 0.4f;
                Vector3 start = Vector3.Lerp(spine[0], spine[spine.Count - 1], t);
                float angle = R() * Mathf.PI * 2f;
                Vector3 out1 = new Vector3(Mathf.Cos(angle), 0.6f + R() * 0.6f, Mathf.Sin(angle));
                float length = 1.5f + R() * 2.5f;
                var limb = new List<Vector3> { start, start + out1 * length * 0.5f, start + (out1 + Vector3.up * 0.3f) * length };
                var limbRadii = new List<float> { 0.13f, 0.08f, 0.02f };
                parts.Add(new CombineInstance { mesh = MakeTube(limb, limbRadii, 5), transform = Matrix4x4.identity });
            }
            var mesh = new Mesh { name = "CinematicTree" + seed };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            foreach (CombineInstance part in parts) Object.DestroyImmediate(part.mesh);
            return mesh;
        }

        /// <summary>A Covenant spire: a tall curved horn, sharp at its tip; or, seam, the thin glowing line running up its inner face.</summary>
        private static Mesh MakeSpireMesh(float height, float bend, bool seam)
        {
            var spine = new List<Vector3>();
            var radii = new List<float>();
            const int rings = 24;
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float radius = Mathf.Lerp(height * 0.075f, height * 0.004f, Mathf.Pow(t, 0.8f));
                Vector3 point = new Vector3(0f, t * height, bend * t * t);
                if (seam) point += new Vector3(0f, 0f, radius * 0.92f);
                spine.Add(point);
                radii.Add(seam ? Mathf.Max(0.12f, radius * 0.12f) : radius);
            }
            Mesh mesh = MakeTube(spine, radii, seam ? 5 : 12);
            mesh.name = seam ? "CinematicSpireSeam" : "CinematicSpire";
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A growth of Flood biomass: swollen bulbs heaped together and tendrils curling up out of them.</summary>
        private static Mesh MakeGrowthMesh(int seed)
        {
            var random = new Random(seed);
            float R() => (float)random.NextDouble();
            var parts = new List<CombineInstance>();
            var made = new List<Mesh>();
            Mesh bulb = MakeBulbMesh(seed);
            made.Add(bulb);
            int bulbs = 4 + random.Next(4);
            for (int i = 0; i < bulbs; i++)
            {
                float size = 0.8f + R() * 2.2f;
                var place = new Vector3((R() - 0.5f) * 5f, size * 0.25f, (R() - 0.5f) * 5f);
                parts.Add(new CombineInstance { mesh = bulb, transform = Matrix4x4.TRS(place, Quaternion.Euler(R() * 360f, R() * 360f, R() * 360f), new Vector3(size, size * (0.6f + R() * 0.5f), size)) });
            }
            int tendrils = 2 + random.Next(4);
            for (int i = 0; i < tendrils; i++)
            {
                float height = 3f + R() * 7f, curl = (R() - 0.5f) * 4f, angle = R() * Mathf.PI * 2f;
                var root = new Vector3((R() - 0.5f) * 3f, 0f, (R() - 0.5f) * 3f);
                var spine = new List<Vector3>();
                var radii = new List<float>();
                for (int k = 0; k <= 10; k++)
                {
                    float t = k / 10f;
                    var sideways = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (curl * t * t + Mathf.Sin(t * 6f + i) * 0.3f);
                    spine.Add(root + Vector3.up * height * t + sideways);
                    radii.Add(Mathf.Lerp(0.55f, 0.04f, t) * (1f + 0.25f * Mathf.Sin(t * 17f + i)));
                }
                Mesh tube = MakeTube(spine, radii, 7);
                made.Add(tube);
                parts.Add(new CombineInstance { mesh = tube, transform = Matrix4x4.identity });
            }
            var mesh = new Mesh { name = "CinematicGrowth" + seed, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            foreach (Mesh part in made) Object.DestroyImmediate(part);
            return mesh;
        }

        /// <summary>A lumpy round bulb.</summary>
        private static Mesh MakeBulbMesh(int seed)
        {
            const int rings = 10, sides = 14;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float polar = ring / (float)rings * Mathf.PI;
                for (int side = 0; side <= sides; side++)
                {
                    float around = side / (float)sides * Mathf.PI * 2f;
                    var direction = new Vector3(Mathf.Sin(polar) * Mathf.Cos(around), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(around));
                    float bulge = 0.75f + 0.5f * Mathf.PerlinNoise(direction.x * 2.1f + seed, direction.z * 2.1f + direction.y * 1.7f + seed * 0.37f);
                    vertices.Add(direction * bulge * 0.5f);
                }
            }
            for (int ring = 0; ring < rings; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int a = ring * (sides + 1) + side, b = a + 1, c = a + sides + 1, d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }
            var mesh = new Mesh { name = "CinematicBulb" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>A lumpy, flat-shaded stone.</summary>
        private static Mesh MakeRockMesh()
        {
            const int rings = 6, sides = 9;
            var points = new List<Vector3>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float polar = ring / (float)rings * Mathf.PI;
                for (int side = 0; side < sides; side++)
                {
                    float around = side / (float)sides * Mathf.PI * 2f;
                    var direction = new Vector3(Mathf.Sin(polar) * Mathf.Cos(around), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(around));
                    float bulge = 0.65f + 0.7f * Mathf.PerlinNoise(direction.x * 1.7f + 4f, direction.z * 1.7f + direction.y * 1.3f + 9f);
                    points.Add(direction * bulge);
                }
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Face(int a, int b, int c)
            {
                triangles.Add(vertices.Count); vertices.Add(points[a]);
                triangles.Add(vertices.Count); vertices.Add(points[b]);
                triangles.Add(vertices.Count); vertices.Add(points[c]);
            }
            for (int ring = 0; ring < rings; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int a = ring * sides + side, b = ring * sides + next, c = (ring + 1) * sides + side, d = (ring + 1) * sides + next;
                    Face(a, b, c);
                    Face(b, d, c);
                }
            }
            var mesh = new Mesh { name = "CinematicRock" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ textures

        /// <summary>A soft puff of smoke: all its shape in the alpha, broken up at the edge by noise.</summary>
        private static Color[] MakePuff(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(u * u + v * v);
                    float noise = Layers(x * 0.022f + 3f, y * 0.022f + 8f, 5);
                    float alpha = Mathf.Clamp01(1f - distance * (0.95f + (1f - noise) * 0.9f));
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    float shade = 0.72f + noise * 0.56f;
                    pixels[y * size + x] = new Color(shade, shade, shade, alpha);
                }
            }
            return pixels;
        }

        /// <summary>A round glow, white, bright in the middle.</summary>
        private static Color[] MakeGlow(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }
            return pixels;
        }

        /// <summary>A tongue of flame: a ragged glow, hottest low in the middle.</summary>
        private static Color[] MakeFlame(int size)
        {
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float noise = Layers(x * 0.05f + 11f, y * 0.05f + 3f, 4);
                    float width = 0.75f - (v + 1f) * 0.2f + (noise - 0.5f) * 0.4f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(0.05f, width)) * Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 0.9f);
                    float heat = Mathf.Clamp01(1.2f - (v + 1f) * 0.45f);
                    pixels[y * size + x] = new Color(1f, 0.7f + heat * 0.3f, 0.45f + heat * 0.55f, alpha * alpha);
                }
            }
            return pixels;
        }

        // ------------------------------------------------------------------ saving

        private static Material MakeParticleMaterial(string name, Texture2D texture, bool additive, Color color)
        {
            Material material = SaveMaterial(name, new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material MakeLit(string name, Color color, float smoothness)
        {
            Material material = SaveMaterial(name, new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SaveMaterial(string name, Material fresh)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            fresh.name = name;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            existing.shader = fresh.shader;
            existing.CopyPropertiesFromMaterial(fresh);
            existing.shaderKeywords = fresh.shaderKeywords;
            Object.DestroyImmediate(fresh);
            return existing;
        }

        private static void SaveAsset(Object asset, string path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        private static Texture2D SavePng(string name, Color[] pixels, bool sRgb, TextureWrapMode wrap)
        {
            string path = $"{ArtFolder}/{name}.png";
            int size = Mathf.RoundToInt(Mathf.Sqrt(pixels.Length));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = sRgb;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
