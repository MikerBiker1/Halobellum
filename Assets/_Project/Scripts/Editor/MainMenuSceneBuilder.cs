// MainMenuSceneBuilder.cs
// Builds Scenes/MainMenu.unity from code (M10), so it can always be made again exactly: a low camera on a darkening battlefield, looking
// toward the sun. Marines and Scorpions in front; a burnt-out Warthog smoking on the left; Wraiths further on; a huge Scarab standing in
// the haze, all four legs on the ground; ridges behind it, smoke on the horizon, embers in the air, Pelicans crossing the sky. The models are the project's own; the
// ground, ridges, rocks, sky, smoke and glow are all made here or by MenuSky.shader, and written to Art/Menu. The moving parts are in
// MenuBackdrop and the screen in MainMenuHud.
//   Menu: Halobellum > Scenes > Rebuild Main Menu Scene
//   Command line: -executeMethod Halobellum.EditorTools.MainMenuSceneBuilder.BuildFromCommandLine
//   Or ...BuildAndPreviewFromCommandLine, which also renders the picture to menu_preview*.png next to the project, for a quick look.
// Depends on: BattleSceneBuilder (scene paths, build settings), MenuBackdrop, MainMenuHud, MenuLook, Halobellum.Units (BakedBodyDefinition),
//             URP (post-processing), UnityEditor.

using System;
using System.Collections.Generic;
using System.IO;
using Halobellum.UI;
using Halobellum.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Halobellum.EditorTools
{
    public static class MainMenuSceneBuilder
    {
        private const string ArtFolder = "Assets/_Project/Art/Menu";
        private const string UnitFolder = "Assets/_Project/Prefabs/Units/";
        private const string FontFolder = "Assets/_Project/Art/Imported/Fonts/";
        private const string MarinePath = "Assets/_Project/Resources/BakedBodies/Marine.asset";
        private const string LookPath = "Assets/_Project/Resources/MenuLook.asset";

        // Where the sun is: low, ahead of the camera and to the right.
        private const float SunElevation = 8.5f, SunAzimuth = 24f;
        private static readonly Vector3 CameraSpot = new Vector3(2f, 0f, -44f);
        // How far the Scarab's feet go into the ground, in metres: enough that no light shows under them.
        private const float FootSink = 0.03f;

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
                MenuBackdrop backdrop = Build();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ws_menu_out"));
                if (!Directory.Exists(folder)) folder = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                Preview(backdrop, 3f, Path.Combine(folder, "menu_preview_a.png"));
                Preview(backdrop, 10f, Path.Combine(folder, "menu_preview_b.png"));
                Preview(backdrop, 21f, Path.Combine(folder, "menu_preview_c.png"));
            }
            catch (Exception e) { Debug.LogError("Previewing the main menu failed: " + e); exitCode = 1; }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Renders the scene as it is this many seconds after the menu opens.</summary>
        private static void Preview(MenuBackdrop backdrop, float time, string path)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float oldShadows = pipeline != null ? pipeline.shadowDistance : 0f;
            if (pipeline != null) pipeline.shadowDistance = backdrop.shadowDistance;

            backdrop.RefreshLight();
            backdrop.SetUpFigures();
            foreach (ParticleSystem particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) particles.Simulate(25f, true, true);
            backdrop.ApplyAt(time);

            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Camera view = backdrop.view;
            view.targetTexture = target;
            view.Render();
            view.targetTexture = null;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            if (pipeline != null) pipeline.shadowDistance = oldShadows;
            Debug.Log("Built preview " + path);
        }

        // ------------------------------------------------------------------ the scene

        public static MenuBackdrop Build()
        {
            Directory.CreateDirectory(ArtFolder);
            EnsureMenuLook();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Vector3 sun = Quaternion.Euler(-SunElevation, SunAzimuth, 0f) * Vector3.forward;
            SetUpSkyAndLight(sun);

            var backdropObject = new GameObject("Menu Backdrop");
            var backdrop = backdropObject.AddComponent<MenuBackdrop>();
            backdropObject.AddComponent<MainMenuHud>();

            BuildCamera(backdrop);
            BuildVolume();
            BuildGround();
            BuildRidges();
            BuildRocks();
            PlaceArmy(backdrop);
            BuildSmokeAndEmbers();
            BuildFlyers(backdrop);

            EditorSceneManager.SaveScene(scene, BattleSceneBuilder.MainMenuScenePath);
            BattleSceneBuilder.UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            return backdrop;
        }

        private static void EnsureMenuLook()
        {
            var look = AssetDatabase.LoadAssetAtPath<MenuLook>(LookPath);
            if (look == null)
            {
                look = ScriptableObject.CreateInstance<MenuLook>();
                AssetDatabase.CreateAsset(look, LookPath);
            }
            look.titleFont = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "Audiowide/Audiowide-Regular.ttf");
            look.headingFont = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "SairaSemiCondensed/SairaSemiCondensed-Bold.ttf");
            look.textFont = AssetDatabase.LoadAssetAtPath<Font>(FontFolder + "ChakraPetch/ChakraPetch-SemiBold.ttf");
            EditorUtility.SetDirty(look);
        }

        private static void SetUpSkyAndLight(Vector3 sun)
        {
            string shaderPath = ArtFolder + "/MenuSky.shader";
            AssetDatabase.ImportAsset(shaderPath);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null) throw new InvalidOperationException("Missing " + shaderPath);
            Material sky = SaveMaterial("MenuSky", new Material(shader));
            sky.SetVector("_SunDirection", sun);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.32f, 0.5f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.32f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.1f, 0.085f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.66f, 0.42f, 0.36f);
            RenderSettings.fogDensity = 0.0031f;

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.6f, 0.34f);
            light.intensity = 2.4f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
            lightObject.transform.rotation = Quaternion.LookRotation(-sun);
            RenderSettings.sun = light;

            // A cool, weak light from the darkening sky behind the camera, so that what faces us doesn't go black.
            var fillObject = new GameObject("Sky Fill");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.6f, 0.52f, 0.72f);
            fill.intensity = 0.55f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(34f, 150f, 0f);
        }

        private static void BuildCamera(MenuBackdrop backdrop)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 3000f;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            backdrop.view = camera;
            backdrop.cameraPosition = CameraSpot + Vector3.up * (GroundHeight(CameraSpot.x, CameraSpot.z) + 1.3f);
            backdrop.lookAt = new Vector3(16f, 15f, 120f);
            backdrop.ApplyAt(0f);
        }

        private static void BuildVolume()
        {
            string path = ArtFolder + "/MenuVolume.asset";
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
            bloom.threshold.value = 1.05f;
            bloom.intensity.value = 0.9f;
            bloom.scatter.value = 0.78f;
            bloom.tint.value = new Color(1f, 0.82f, 0.68f);
            Vignette vignette = Add<Vignette>();
            vignette.intensity.value = 0.34f;
            vignette.smoothness.value = 0.5f;
            vignette.color.value = new Color(0.03f, 0.015f, 0.04f);
            ColorAdjustments color = Add<ColorAdjustments>();
            color.postExposure.value = 0.55f;
            color.contrast.value = 14f;
            color.saturation.value = 8f;
            FilmGrain grain = Add<FilmGrain>();
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.value = 0.18f;
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

        /// <summary>The ground's height: gentle swells and shell holes near the camera, rising to hills further off.</summary>
        private static float GroundHeight(float x, float z)
        {
            float distance = new Vector2(x - CameraSpot.x, z - CameraSpot.z).magnitude;
            float swell = (Layers(x * 0.009f, z * 0.009f, 4) - 0.5f) * 11f;
            float grit = (Layers(x * 0.07f, z * 0.07f, 3) - 0.5f) * 0.9f;
            float hills = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(210f, 780f, distance)) * (Layers(x * 0.0035f + 5f, z * 0.0035f, 4)) * 95f;
            return swell + grit + hills;
        }

        private static void BuildGround()
        {
            const int cells = 250;
            const float reachSide = 1400f, reachFar = 1500f, reachBack = 170f;
            int columns = cells + 1;
            var vertices = new Vector3[columns * columns];
            var uv = new Vector2[vertices.Length];
            for (int row = 0; row < columns; row++)
            {
                // The rows and columns crowd together near the camera, where the shape shows most.
                float v = row / (float)cells;
                float z = v < 0.12f ? Mathf.Lerp(-reachBack, 0f, v / 0.12f) : Mathf.Pow((v - 0.12f) / 0.88f, 2.1f) * reachFar;
                z += CameraSpot.z;
                for (int column = 0; column < columns; column++)
                {
                    float u = column / (float)cells * 2f - 1f;
                    float x = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), 2.2f) * reachSide + CameraSpot.x;
                    vertices[row * columns + column] = new Vector3(x, GroundHeight(x, z), z);
                    uv[row * columns + column] = new Vector2(x / 400f + 0.5f, (z + 100f) / 400f);
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
            var mesh = new Mesh { name = "MenuGround", indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            SaveAsset(mesh, ArtFolder + "/MenuGround.asset");

            Texture2D map = SavePng("MenuGroundMap", MakeGroundMap(1024), true, TextureWrapMode.Repeat);
            Texture2D detail = SavePng("MenuGroundGrain", MakeGrain(512), false, TextureWrapMode.Repeat);
            Material material = SaveMaterial("MenuGround", new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.SetTexture("_BaseMap", map);
            material.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.74f));
            material.SetFloat("_Smoothness", 0.06f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetTexture("_DetailAlbedoMap", detail);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(40f, 40f));
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

        /// <summary>The ground's colours, tiled over 400 m: dry earth, darker burnt patches, pale dust.</summary>
        private static Color[] MakeGroundMap(int size)
        {
            var pixels = new Color[size * size];
            var earth = new Color(0.36f, 0.27f, 0.21f);
            var burnt = new Color(0.1f, 0.085f, 0.08f);
            var dust = new Color(0.56f, 0.45f, 0.36f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float patches = Tiled(u, v, 5, 4);
                    float fine = Tiled(u + 0.3f, v + 0.7f, 28, 3);
                    Color color = Color.Lerp(earth, burnt, Mathf.SmoothStep(0.45f, 0.62f, patches));
                    color = Color.Lerp(color, dust, Mathf.SmoothStep(0.5f, 0.72f, Tiled(u + 0.6f, v + 0.1f, 9, 4)) * 0.75f);
                    color *= 0.72f + fine * 0.56f;
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

        /// <summary>Layered noise that tiles over 0 to 1 (a Perlin lookup blended with itself across the seams).</summary>
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

        /// <summary>Three bands of far hills, as dark curtains; the haze paints them paler with distance.</summary>
        private static void BuildRidges()
        {
            Material material = SaveMaterial("MenuRidge", new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.SetColor("_BaseColor", new Color(0.15f, 0.12f, 0.15f));
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(material);

            float[] distances = { 620f, 880f, 1300f };
            float[] heights = { 62f, 118f, 215f };
            for (int band = 0; band < distances.Length; band++)
            {
                const int segments = 260;
                var vertices = new Vector3[(segments + 1) * 2];
                var triangles = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float angle = Mathf.Lerp(-95f, 95f, i / (float)segments) * Mathf.Deg2Rad;
                    float x = Mathf.Sin(angle) * distances[band] + CameraSpot.x;
                    float z = Mathf.Cos(angle) * distances[band] + CameraSpot.z;
                    // Broad shoulders with sharp ridged crags on them.
                    float rough = Layers(i * 0.017f * (band + 1) + band * 7f, band * 3.3f, 4);
                    float crag = 1f - Mathf.Abs(Layers(i * 0.055f + band * 13f, band * 1.7f, 4) - 0.5f) * 3.2f;
                    float fine = 1f - Mathf.Abs(Layers(i * 0.21f + band * 5f, band * 2.9f, 2) - 0.5f) * 3f;
                    float top = heights[band] * Mathf.Max(0f, rough * 0.95f - 0.14f + crag * crag * 0.36f + fine * fine * 0.09f);
                    vertices[i * 2] = new Vector3(x, -30f, z);
                    vertices[i * 2 + 1] = new Vector3(x, top + 8f, z);
                    if (i == segments) continue;
                    int k = i * 6, a = i * 2;
                    triangles[k] = a; triangles[k + 1] = a + 1; triangles[k + 2] = a + 2;
                    triangles[k + 3] = a + 2; triangles[k + 4] = a + 1; triangles[k + 5] = a + 3;
                }
                var mesh = new Mesh { name = "MenuRidge" + band, vertices = vertices, triangles = triangles };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                SaveAsset(mesh, $"{ArtFolder}/MenuRidge{band}.asset");
                var ridge = new GameObject("Ridge " + band);
                ridge.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = ridge.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>A lumpy, flat-shaded stone, shared by every rock in the scene.</summary>
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
            var mesh = new Mesh { name = "MenuRock" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildRocks()
        {
            Mesh mesh = MakeRockMesh();
            SaveAsset(mesh, ArtFolder + "/MenuRock.asset");
            Material material = SaveMaterial("MenuRock", new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.SetColor("_BaseColor", new Color(0.26f, 0.19f, 0.15f));
            material.SetFloat("_Smoothness", 0.1f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var parent = new GameObject("Rocks").transform;
            var random = new System.Random(4021);
            float Range(float from, float to) => from + (float)random.NextDouble() * (to - from);
            for (int i = 0; i < 150; i++)
            {
                float z = CameraSpot.z + 5f + Mathf.Pow(Range(0f, 1f), 1.7f) * 240f;
                float x = CameraSpot.x + Range(-1f, 1f) * (10f + (z - CameraSpot.z) * 0.8f);
                float size = Range(0.15f, 0.8f) * (Range(0f, 1f) > 0.92f ? 2.2f : 1f);
                if (z - CameraSpot.z < 14f && size > 0.45f) size = 0.45f; // Nothing big right in front of the lens.
                var rock = new GameObject("Rock");
                rock.transform.SetParent(parent, false);
                rock.transform.position = new Vector3(x, GroundHeight(x, z) + size * 0.1f, z);
                rock.transform.rotation = Quaternion.Euler(Range(-25f, 25f), Range(0f, 360f), Range(-25f, 25f));
                rock.transform.localScale = new Vector3(size * Range(0.9f, 1.6f), size * Range(0.45f, 0.9f), size * Range(0.9f, 1.6f));
                rock.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = rock.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = size > 0.7f ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ the armies

        private static GameObject PlaceModel(string prefab, Transform parent, float x, float z, float yaw, float scale, Vector3? tilt = null, float lift = 0f)
        {
            string path = UnitFolder + prefab + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + path);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            model.transform.SetPositionAndRotation(new Vector3(x, GroundHeight(x, z) + lift, z), Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt ?? Vector3.zero));
            model.transform.localScale *= scale;

            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            Debug.Log($"Menu model {prefab}: size {bounds.size}, scale {scale}");
            return model;
        }

        /// <summary>
        /// Stands the model on the ground as it really is under each foot. The ground slopes about two metres across its stance, so the body
        /// goes level at the middle height and each leg slides up or down at the hip until its lowest point sits just in the ground.
        /// </summary>
        private static void PlantLegs(Transform model, List<Transform> legs)
        {
            Mesh ground = GameObject.Find("Ground").GetComponent<MeshFilter>().sharedMesh;
            Vector3[] groundPoints = ground.vertices;
            int[] groundTriangles = ground.triangles;
            var under = new float[legs.Count];
            float middle = 0f;
            for (int i = 0; i < legs.Count; i++)
            {
                Vector3 foot = LowestPoint(legs[i]);
                under[i] = MeshHeight(groundPoints, groundTriangles, foot.x, foot.z);
                middle += under[i] / legs.Count;
            }
            model.position += Vector3.up * (middle - LowestPoint(legs[0]).y);
            for (int i = 0; i < legs.Count; i++)
            {
                float slide = under[i] - FootSink - LowestPoint(legs[i]).y;
                legs[i].position += Vector3.up * slide;
                Debug.Log($"Menu Scarab: {legs[i].name} slid {slide:0.00} m onto the ground at {under[i]:0.00}.");
            }
        }

        /// <summary>The lowest vertex of this part of a model, in the world. A model's meshes can be read here in the editor, though not in a game.</summary>
        private static Vector3 LowestPoint(Transform part)
        {
            Matrix4x4 world = part.localToWorldMatrix;
            var lowest = new Vector3(0f, float.MaxValue, 0f);
            foreach (Vector3 vertex in part.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Vector3 point = world.MultiplyPoint3x4(vertex);
                if (point.y < lowest.y) lowest = point;
            }
            return lowest;
        }

        /// <summary>The height of the ground's triangles here: between its vertices they run a little off GroundHeight.</summary>
        private static float MeshHeight(Vector3[] points, int[] triangles, float x, float z)
        {
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = points[triangles[t]], b = points[triangles[t + 1]], c = points[triangles[t + 2]];
                float area = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(area) < 1e-6f) continue;
                float shareA = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / area;
                float shareB = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / area;
                if (shareA < 0f || shareB < 0f || shareA + shareB > 1f) continue;
                return shareA * a.y + shareB * b.y + (1f - shareA - shareB) * c.y;
            }
            throw new InvalidOperationException($"No ground at {x}, {z}.");
        }

        private static void PlaceArmy(MenuBackdrop backdrop)
        {
            var army = new GameObject("Army").transform;

            // The walker, far off, standing in the haze. This spot is where it was a few seconds into its old walk, the picture that was praised.
            GameObject scarab = PlaceModel("Scarab_Model", army, 57.5f, 169f, 192f, 3.7f);
            backdrop.walker = scarab.transform;
            var legs = new List<Transform>();
            Transform muzzle = null;
            foreach (Transform node in scarab.GetComponentsInChildren<Transform>(true))
            {
                if (node.name.StartsWith("Swing_")) legs.Add(node);
                else if (muzzle == null && node.name.StartsWith("Muzzle_")) muzzle = node;
            }
            PlantLegs(scarab.transform, legs);
            Debug.Log($"Menu Scarab: {legs.Count} legs, muzzle {(muzzle != null ? muzzle.name : "none")}");
            BuildBeam(backdrop, muzzle != null ? muzzle : scarab.transform);

            // Its escort.
            PlaceModel("Wraith_Model", army, -46f, 128f, 172f, 1.5f, null, 0.8f);
            PlaceModel("Wraith_Model", army, 20f, 150f, 196f, 1.5f, null, 0.8f);
            PlaceModel("Wraith_Model", army, -102f, 176f, 160f, 1.5f, null, 0.8f);
            PlaceModel("Wraith_Model", army, 118f, 165f, 200f, 1.5f, null, 0.8f);

            // The near line.
            PlaceModel("Scorpion_Model", army, 19f, -6f, 8f, 1f);
            PlaceModel("Scorpion_Model", army, 46f, 24f, -6f, 1f);
            PlaceModel("Scorpion_Model", army, -12f, 42f, 12f, 1f);
            PlaceModel("Warthog_Model", army, -4.5f, -9f, 64f, 1.25f, new Vector3(14f, 0f, -22f), -0.2f);

            // Marines holding the front, seen from behind.
            var marine = AssetDatabase.LoadAssetAtPath<BakedBodyDefinition>(MarinePath);
            if (marine == null || !marine.IsReady) throw new InvalidOperationException("Missing a baked Marine at " + MarinePath);
            BakedClip clip = marine.Clip(BakedAnimation.Still);
            Vector3[] spots =
            {
                new Vector3(6.3f, 0f, -37f), new Vector3(9.4f, 0f, -33.5f), new Vector3(5.2f, 0f, -30.5f), new Vector3(12f, 0f, -29.6f),
                new Vector3(8.6f, 0f, -27.2f), new Vector3(15.4f, 0f, -25.5f), new Vector3(11.6f, 0f, -23.5f), new Vector3(18.8f, 0f, -22f),
                new Vector3(5.6f, 0f, -22f), new Vector3(15.6f, 0f, -18.5f), new Vector3(22f, 0f, -26f), new Vector3(9f, 0f, -17f),
            };
            var figures = new List<MenuBackdrop.Figure>();
            var random = new System.Random(77);
            for (int i = 0; i < spots.Length; i++)
            {
                var body = new GameObject("Marine " + i);
                body.transform.SetParent(army, false);
                float x = spots[i].x, z = spots[i].z;
                body.transform.position = new Vector3(x, GroundHeight(x, z) - 0.03f, z);
                body.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 34f - 10f, 0f);
                body.AddComponent<MeshFilter>().sharedMesh = marine.mesh;
                var renderer = body.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = marine.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                figures.Add(new MenuBackdrop.Figure
                {
                    renderer = renderer,
                    firstRow = clip.firstRow,
                    frames = clip.frames,
                    startTime = -(float)random.NextDouble() * clip.frames / marine.bakedFps,
                    // Their shader has no fog or real lighting: this keeps them dark against the light, as everything facing away from it is.
                    tint = new Color(0.2f, 0.17f, 0.18f, 1f),
                });
            }
            backdrop.figures = figures.ToArray();
        }

        private static void BuildBeam(MenuBackdrop backdrop, Transform from)
        {
            Texture2D glow = SavePng("MenuGlow", MakeGlow(128), true, TextureWrapMode.Clamp);
            Material material = MakeParticleMaterial("MenuBeam", glow, additive: true);
            material.SetColor("_BaseColor", new Color(1.1f, 1.7f, 6f, 1f));
            EditorUtility.SetDirty(material);

            var beamObject = new GameObject("Beam");
            var line = beamObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            backdrop.beam = line;
            backdrop.beamFrom = from;
            backdrop.beamTo = new Vector3(-30f, GroundHeight(-30f, 95f), 95f);

            var lightObject = new GameObject("Beam Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.55f, 0.65f, 1f);
            light.range = 90f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            backdrop.beamLight = light;
        }

        // ------------------------------------------------------------------ smoke and embers

        private static Material MakeParticleMaterial(string name, Texture2D texture, bool additive)
        {
            Material material = SaveMaterial(name, new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", additive ? (float)BlendMode.SrcAlpha : (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildSmokeAndEmbers()
        {
            Texture2D puff = SavePng("MenuSmoke", MakePuff(256), true, TextureWrapMode.Clamp);
            Material smoke = MakeParticleMaterial("MenuSmoke", puff, additive: false);
            Texture2D glow = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/MenuGlow.png");
            Material ember = MakeParticleMaterial("MenuEmber", glow, additive: true);

            var parent = new GameObject("Smoke and Embers").transform;
            var wreckSmoke = new Color(0.09f, 0.075f, 0.08f, 0.5f);
            var farSmoke = new Color(0.14f, 0.1f, 0.1f, 0.2f);
            AddSmoke(parent, smoke, new Vector3(-4.5f, GroundHeight(-4.5f, -9f) + 1.2f, -9f), 0.8f, 1.7f, 1.6f, 9f, 15f, wreckSmoke, 5f);
            AddSmoke(parent, smoke, new Vector3(-150f, GroundHeight(-150f, 260f), 260f), 6f, 4.2f, 8f, 34f, 36f, farSmoke, 12f);
            AddSmoke(parent, smoke, new Vector3(-280f, GroundHeight(-280f, 390f), 390f), 9f, 5f, 11f, 46f, 40f, farSmoke, 12f);
            AddSmoke(parent, smoke, new Vector3(190f, GroundHeight(190f, 340f), 340f), 8f, 4.6f, 10f, 42f, 38f, farSmoke, 12f);
            AddSmoke(parent, smoke, new Vector3(-30f, GroundHeight(-30f, 95f), 95f), 3f, 2.6f, 5f, 18f, 18f, new Color(0.13f, 0.1f, 0.11f, 0.4f), 4f);

            var emberObject = new GameObject("Embers");
            emberObject.transform.SetParent(parent, false);
            emberObject.transform.position = CameraSpot + new Vector3(4f, 3f, 38f);
            var particles = emberObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(6f, 2.6f, 0.8f, 1f), new Color(4f, 1.2f, 0.3f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.gravityModifier = -0.012f;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 24f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(80f, 8f, 70f);
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.2f, -0.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 0.25f;
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(Color.white, 0.15f, 0.7f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ember;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static ParticleSystem.MinMaxGradient Fade(Color color, float inUntil, float outFrom)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inUntil), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static void AddSmoke(Transform parent, Material material, Vector3 at, float baseWidth, float speed, float startSize, float endSize, float lifetime, Color color, float rate)
        {
            var smokeObject = new GameObject("Smoke");
            smokeObject.transform.SetParent(parent, false);
            smokeObject.transform.position = at;
            var particles = smokeObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.8f, startSize * 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.CeilToInt(rate * lifetime * 1.2f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = baseWidth;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-speed * 0.5f, -speed * 0.15f); // The wind blows from the sun's side.
            velocity.y = new ParticleSystem.MinMaxCurve(speed * 0.2f, speed * 0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(-speed * 0.1f, speed * 0.1f);
            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(endSize / startSize, AnimationCurve.EaseInOut(0f, startSize / endSize, 1f, 1f));
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(Color.white, 0.08f, 0.5f);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>A soft puff of smoke: dark, all its shape in the alpha, broken up at the edge by noise.</summary>
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

        // ------------------------------------------------------------------ the Pelicans

        private static void BuildFlyers(MenuBackdrop backdrop)
        {
            var glowTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/MenuGlow.png");
            Material glowMaterial = MakeParticleMaterial("MenuEngine", glowTexture, additive: true);
            glowMaterial.SetColor("_BaseColor", new Color(5f, 2.2f, 0.8f, 1f));
            EditorUtility.SetDirty(glowMaterial);

            var parent = new GameObject("Flyers").transform;
            var flyers = new List<MenuBackdrop.Flyer>();
            var glows = new List<Transform>();

            void Group(string prefab, Vector3 from, Vector3 to, float period, float start, float duration, float bank, float scale, params Vector3[] places)
            {
                for (int i = 0; i < places.Length; i++)
                {
                    GameObject model = PlaceModel(prefab, parent, 0f, 0f, 0f, scale);
                    AddEngineGlows(model, glowMaterial, glows);
                    flyers.Add(new MenuBackdrop.Flyer
                    {
                        body = model.transform,
                        from = from + places[i],
                        to = to + places[i],
                        period = period,
                        start = start,
                        duration = duration,
                        bank = bank,
                        bobPhase = i * 1.7f,
                    });
                }
            }

            // Three close by, crossing in front of the sun: the first pass is under way when the menu opens.
            Group("Pelican_Model", new Vector3(-235f, 40f, 84f), new Vector3(330f, 56f, 138f), 46f, 7f, 21f, -7f, 1.5f,
                Vector3.zero, new Vector3(-30f, -5f, 24f), new Vector3(-62f, 6f, -20f));
            // Two passing right overhead, going away toward the Scarab.
            Group("Pelican_Model", new Vector3(14f, 15f, -75f), new Vector3(120f, 74f, 420f), 73f, -18f, 24f, 8f, 1.5f,
                Vector3.zero, new Vector3(26f, 6f, -22f));
            // A long way off, the other way across the sky.
            Group("Pelican_Model", new Vector3(560f, 118f, 480f), new Vector3(-560f, 92f, 430f), 61f, -6f, 40f, 0f, 1.5f,
                Vector3.zero, new Vector3(40f, 4f, 22f), new Vector3(-34f, -4f, 30f), new Vector3(78f, 7f, 48f));

            backdrop.flyers = flyers.ToArray();
            backdrop.glows = glows.ToArray();
        }

        /// <summary>Two engine glows at the tail of an aircraft, which is taken to fly along +z.</summary>
        private static void AddEngineGlows(GameObject model, Material material, List<Transform> glows)
        {
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            for (int side = -1; side <= 1; side += 2)
            {
                var glow = new GameObject("Engine Glow");
                glow.transform.SetParent(model.transform, false);
                glow.transform.position = bounds.center + new Vector3(side * bounds.extents.x * 0.32f, bounds.extents.y * 0.1f, -bounds.extents.z * 0.92f);
                glow.transform.localScale = Vector3.one * Mathf.Max(bounds.size.x, bounds.size.z) * 0.13f;
                glow.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = glow.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                glows.Add(glow.transform);
            }
        }

        // ------------------------------------------------------------------ saving

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
