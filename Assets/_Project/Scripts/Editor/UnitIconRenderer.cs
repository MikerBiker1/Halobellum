// UnitIconRenderer.cs
// Renders a picture of every unit for the cards (M10) into Resources/UnitIcons/<unit name>.png: 256 x 256 with a clear
// background, from a 3/4 front view, lit like a studio portrait. Run it again whenever a unit gets a real model:
//   Menu:          Halobellum > Art > Render Unit Icons
//   Command line:  -executeMethod Halobellum.EditorTools.UnitIconRenderer.RenderAllFromCommandLine
// It goes through every UnitDefinition asset:
//   - a baked body (the Marine, the Grunts): its baked mesh through Halobellum/BakedCrowd, held on its still frame;
//   - a real model (keepsOwnMaterials): its prefab;
//   - anything else is still a placeholder primitive, so it writes the designed fallback (UnitIcons.Compose), in its faction's colour.
// How it renders: each unit alone in an empty scene, 4 times the size of the icon. The framing is found from the picture
// itself: a first render finds where the model is, the camera moves to fill the frame with it, and so on a few times. So a
// model's own bounds don't matter. Rendered once on black and once on white: a pixel that changes is background. That
// doesn't depend on how the render pipeline treats alpha. Then it is shrunk by 4 with premultiplied colours for clean edges.
// It also writes UnitIcons/index.txt: each unit, and how its icon was made. Needs a GPU, which batch mode has here (d3d12).
// Depends on: Halobellum.Units, Halobellum.UI (UnitIcons), UnityEditor, UnityEngine.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Halobellum.Core;
using Halobellum.UI;
using Halobellum.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Halobellum.EditorTools
{
    public static class UnitIconRenderer
    {
        public const string OutputFolder = "Assets/_Project/Resources/UnitIcons";
        private const int IconSize = 256, Supersample = 4;
        private const float CameraFov = 16f;
        /// <summary>How much of the frame the model fills.</summary>
        private const float Fill = 0.92f;
        private const float YawDegrees = 35f, PitchDegrees = 12f, AirPitchDegrees = 20f;
        /// <summary>A layer nothing else uses, so that only the unit is drawn and lit.</summary>
        private const int RenderLayer = 31;

        private static readonly int TintId = Shader.PropertyToID("_BaseColor");
        private static readonly int AnimNowId = Shader.PropertyToID("_AnimNow");
        private static readonly int AnimOldId = Shader.PropertyToID("_AnimOld");
        private static readonly int AnimBlendId = Shader.PropertyToID("_AnimBlend");
        private static readonly int BattleTimeId = Shader.PropertyToID("_BattleTime");

        [MenuItem("Halobellum/Art/Render Unit Icons")]
        public static void RenderAll()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string openScene = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutputFolder);

            var report = new StringBuilder();
            Dictionary<UnitDefinition, string> factions = FactionOfEachUnit();
            var done = new HashSet<string>();
            int rendered = 0, fallbacks = 0;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.47f, 0.5f);
            RenderSettings.fog = false;

            foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition").OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (unit == null || string.IsNullOrEmpty(unit.unitName) || !done.Add(unit.unitName)) continue;
                string path = $"{OutputFolder}/{UnitIcons.FileName(unit.unitName)}.png";
                byte[] png = null;
                string source = "fallback";
                if (unit.bakedBody != null && unit.bakedBody.IsReady)
                {
                    png = RenderBody(unit, BuildBaked(unit.bakedBody));
                    source = "baked";
                }
                else if (unit.prefab != null && unit.keepsOwnMaterials)
                {
                    png = RenderBody(unit, BuildModel(unit.prefab));
                    source = "model";
                }
                if (png == null)
                {
                    if (source != "fallback") Debug.LogWarning($"Unit icon: {unit.unitName} rendered nothing, using its fallback.");
                    source = "fallback";
                    UnitStats stats = unit.ToStats();
                    factions.TryGetValue(unit, out string faction);
                    Texture2D composed = UnitIcons.Compose(stats.Role, UnitIcons.BodyShapeFor(stats), faction ?? (stats.IsFlood ? "Flood" : null));
                    png = composed.EncodeToPNG();
                    Object.DestroyImmediate(composed);
                    fallbacks++;
                }
                else
                {
                    rendered++;
                }
                File.WriteAllBytes(path, png);
                report.AppendLine($"{unit.unitName}\t{source}");
            }

            File.WriteAllText(OutputFolder + "/index.txt", report.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"Built unit icons: {rendered} rendered, {fallbacks} fallbacks.\n{report}");
            if (!Application.isBatchMode && !string.IsNullOrEmpty(openScene)) EditorSceneManager.OpenScene(openScene);
        }

        public static void RenderAllFromCommandLine()
        {
            int exitCode = 1;
            try
            {
                RenderAll();
                exitCode = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("Unit icons failed: " + e);
            }
            EditorApplication.Exit(exitCode);
        }

        /// <summary>Which faction each unit belongs to: from the faction assets, and for a spell's dropped unit, from that faction's spells.</summary>
        private static Dictionary<UnitDefinition, string> FactionOfEachUnit()
        {
            var result = new Dictionary<UnitDefinition, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:FactionDefinition"))
            {
                var faction = AssetDatabase.LoadAssetAtPath<FactionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (faction == null) continue;
                foreach (UnitDefinition unit in faction.baseRoster)
                {
                    if (unit != null) result[unit] = faction.factionName;
                }
                foreach (FactionDefinition.SubFactionEntry sub in faction.subFactions)
                {
                    foreach (UnitDefinition unit in sub.exclusiveUnits)
                    {
                        if (unit != null) result[unit] = faction.factionName;
                    }
                }
                foreach (SpellDefinition spell in faction.spells)
                {
                    if (spell != null && spell.dropUnit != null && !result.ContainsKey(spell.dropUnit)) result[spell.dropUnit] = faction.factionName;
                }
            }
            return result;
        }

        private static GameObject BuildBaked(BakedBodyDefinition baked)
        {
            var body = new GameObject("Icon subject");
            body.AddComponent<MeshFilter>().sharedMesh = baked.mesh;
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = baked.material;
            BakedClip still = baked.Clip(BakedAnimation.Still);
            var frame = new Vector4(still != null ? still.firstRow : 0, 1f, 0f, 0f);
            var block = new MaterialPropertyBlock();
            block.SetVector(AnimNowId, frame);
            block.SetVector(AnimOldId, frame);
            block.SetVector(AnimBlendId, Vector4.zero);
            block.SetColor(TintId, new Color(1f, 1f, 1f, 0f));
            renderer.SetPropertyBlock(block);
            Shader.SetGlobalFloat(BattleTimeId, 0f);
            return body;
        }

        private static GameObject BuildModel(GameObject prefab)
        {
            GameObject body = Object.Instantiate(prefab);
            body.name = "Icon subject";
            return body;
        }

        private static byte[] RenderBody(UnitDefinition unit, GameObject subject)
        {
            var rig = new GameObject("Icon rig");
            try
            {
                foreach (Transform t in subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = RenderLayer;
                foreach (Renderer r in subject.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }

                Camera camera = rig.AddComponent<Camera>();
                camera.cullingMask = 1 << RenderLayer;
                camera.fieldOfView = CameraFov;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.enabled = false;

                // Looking back at the unit's front (it faces +Z), from a little to its left, and a little above.
                float pitch = unit.isAir ? AirPitchDegrees : PitchDegrees;
                Quaternion view = Quaternion.Euler(pitch, 180f + YawDegrees, 0f);
                Vector3 forward = view * Vector3.forward;
                camera.transform.rotation = view;

                AddLight(rig.transform, view, new Vector3(-0.55f, 0.65f, 0.52f), new Color(1f, 0.97f, 0.92f), 1.35f);
                AddLight(rig.transform, view, new Vector3(0.75f, 0.15f, 0.6f), new Color(0.72f, 0.82f, 1f), 0.45f);
                AddLight(rig.transform, view, new Vector3(0.1f, 0.5f, -0.85f), new Color(0.9f, 0.95f, 1f), 0.7f);

                Bounds bounds = BoundsOf(subject);
                Vector3 target = bounds.center;
                float distance = (bounds.extents.magnitude + 0.5f) / Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad) * 1.4f;

                // Find the framing from the picture itself.
                const int probe = IconSize;
                for (int pass = 0; pass < 6; pass++)
                {
                    PlaceCamera(camera, target, forward, distance);
                    bool[] solid = RenderSolid(camera, probe, out _);
                    if (!SolidBounds(solid, probe, out int minX, out int minY, out int maxX, out int maxY))
                    {
                        distance *= 2f;
                        continue;
                    }
                    bool touchesEdge = minX == 0 || minY == 0 || maxX == probe - 1 || maxY == probe - 1;
                    float viewHeight = 2f * distance * Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
                    float offsetX = ((minX + maxX + 1) * 0.5f - probe * 0.5f) / probe * viewHeight;
                    float offsetY = ((minY + maxY + 1) * 0.5f - probe * 0.5f) / probe * viewHeight;
                    target += camera.transform.right * offsetX + camera.transform.up * offsetY;
                    float size = Mathf.Max(maxX - minX + 1, maxY - minY + 1) / (float)probe;
                    distance = touchesEdge ? distance * 1.6f : distance * Mathf.Clamp(size / Fill, 0.2f, 1.6f);
                }
                PlaceCamera(camera, target, forward, distance);

                int big = IconSize * Supersample;
                bool[] final = RenderSolid(camera, big, out Color32[] colors);
                if (!SolidBounds(final, big, out _, out _, out _, out _)) return null;
                return Shrink(final, colors, big);
            }
            finally
            {
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(subject);
            }
        }

        private static void PlaceCamera(Camera camera, Vector3 target, Vector3 forward, float distance)
        {
            camera.transform.position = target - forward * distance;
            camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.05f);
            camera.farClipPlane = distance * 5f;
        }

        private static void AddLight(Transform parent, Quaternion view, Vector3 fromInViewSpace, Color color, float intensity)
        {
            var holder = new GameObject("Light");
            holder.transform.SetParent(parent, false);
            var light = holder.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << RenderLayer;
            // fromInViewSpace points from the subject towards the light, in the camera's axes: x right, y up, z towards the camera.
            Vector3 toLight = view * new Vector3(fromInViewSpace.x, fromInViewSpace.y, -fromInViewSpace.z);
            holder.transform.rotation = Quaternion.LookRotation(-toLight.normalized);
        }

        private static Bounds BoundsOf(GameObject subject)
        {
            Renderer[] renderers = subject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(subject.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        /// <summary>Renders on black and on white: a pixel the same in both is the model. Rows run from the bottom.</summary>
        private static bool[] RenderSolid(Camera camera, int size, out Color32[] colors)
        {
            Color32[] black = RenderPixels(camera, size, Color.black);
            Color32[] white = RenderPixels(camera, size, Color.white);
            var solid = new bool[black.Length];
            for (int i = 0; i < black.Length; i++)
            {
                solid[i] = Mathf.Abs(black[i].r - white[i].r) < 12 && Mathf.Abs(black[i].g - white[i].g) < 12 && Mathf.Abs(black[i].b - white[i].b) < 12;
            }
            colors = black;
            return solid;
        }

        private static Color32[] RenderPixels(Camera camera, int size, Color background)
        {
            RenderTexture target = RenderTexture.GetTemporary(new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 24) { sRGB = true });
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.backgroundColor = background;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var read = new Texture2D(size, size, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                read.Apply(false);
                Color32[] pixels = read.GetPixels32();
                Object.DestroyImmediate(read);
                return pixels;
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static bool SolidBounds(bool[] solid, int size, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = minY = size;
            maxX = maxY = -1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!solid[y * size + x]) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return maxX >= 0;
        }

        private static byte[] Shrink(bool[] solid, Color32[] colors, int big)
        {
            var pixels = new Color32[IconSize * IconSize];
            long allR = 0, allG = 0, allB = 0, allCount = 0;
            int samples = Supersample * Supersample;
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    int r = 0, g = 0, b = 0, count = 0;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            int index = (y * Supersample + sy) * big + x * Supersample + sx;
                            if (!solid[index]) continue;
                            r += colors[index].r;
                            g += colors[index].g;
                            b += colors[index].b;
                            count++;
                        }
                    }
                    if (count == 0) continue;
                    allR += r;
                    allG += g;
                    allB += b;
                    allCount += count;
                    pixels[y * IconSize + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)(count * 255 / samples));
                }
            }
            if (allCount > 0)
            {
                // Clear pixels take the model's average colour, so that scaling leaves no dark rim.
                var fringe = new Color32((byte)(allR / allCount), (byte)(allG / allCount), (byte)(allB / allCount), 0);
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a == 0) pixels[i] = fringe;
                }
            }
            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }
    }
}
