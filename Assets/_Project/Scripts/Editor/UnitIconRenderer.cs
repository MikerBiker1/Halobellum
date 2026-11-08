// UnitIconRenderer.cs
// Renders a picture of every unit for the cards (M10) into Resources/UnitIcons/<unit name>.png: 256 x 256 with a clear
// background, from a 3/4 front view, lit like a studio portrait. Run it again whenever a unit gets a real model:
//   Menu:          Halobellum > Art > Render Unit Icons
//   Command line:  -executeMethod Halobellum.EditorTools.UnitIconRenderer.RenderAllFromCommandLine
// It goes through every UnitDefinition asset:
//   - a baked body (the Marine, the Grunts): its baked mesh through Halobellum/BakedCrowd, held halfway through its shooting clip, aiming
//     and firing (the owner, D85; it was its standing frame). That is the moment the baker lines its weapon up between the hands, so the
//     weapon sits right. A body whose attack is a swing or a slam (the Zealot, the Abomination) shows that instead;
//   - a real model (keepsOwnMaterials): its prefab;
//   - anything else is still a placeholder primitive, so it writes the designed fallback (UnitIcons.Compose), in its faction's colour.
// How it renders: each unit alone in an empty scene, 4 times the size of the icon. The framing is found from the picture
// itself: a first render finds where the model is, the camera moves to fill the frame with it, and so on a few times. So a
// model's own bounds don't matter. Rendered once on black and once on white: a pixel that changes is background. That
// doesn't depend on how the render pipeline treats alpha. Then it is shrunk by 4 with premultiplied colours for clean edges.
// Two things are there so that a picture still reads at the size of a shop row (about 40 pixels). A tall thin figure, like a
// soldier, is framed from the waist up. A dark picture is brightened to a common level (Brighten).
// A baked body wears its unit's bakedTint, as it does in battle.
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
        /// <summary>A figure narrower than this for its height (width / height) is framed from the waist up.</summary>
        private const float TallFigure = 0.5f;
        /// <summary>How much of a tall figure's height shows, from the top, and how much of the frame's height that fills.</summary>
        private const float PortraitKeep = 0.6f, PortraitFill = 0.96f;
        /// <summary>The part of a waist-up picture, at the bottom, over which the figure fades out instead of being cut off.</summary>
        private const float PortraitFade = 0.2f;
        /// <summary>The average brightness (0 to 255, as displayed) a darker picture is brought up to, and the most it may be multiplied by.</summary>
        private const float WantedBrightness = 100f, MostBrighten = 2.6f;
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
                    png = RenderBody(unit, BuildBaked(unit, IconRow(unit)));
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

        /// <summary>
        /// For choosing a baked body's icon moment: renders the units named in HB_ICON_TRY (comma separated) at nine moments of their shooting
        /// clip, 10% to 90%, into HB_ICON_TRY_OUT (a folder outside the project), as &lt;unit&gt;_&lt;percent&gt;.png. Writes no icon.
        /// </summary>
        public static void TryMomentsFromCommandLine()
        {
            int exitCode = 1;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.46f, 0.47f, 0.5f);
                RenderSettings.fog = false;
                string folder = Environment.GetEnvironmentVariable("HB_ICON_TRY_OUT");
                Directory.CreateDirectory(folder);
                var names = new HashSet<string>(Environment.GetEnvironmentVariable("HB_ICON_TRY").Split(',').Select(n => n.Trim()));
                foreach (string guid in AssetDatabase.FindAssets("t:UnitDefinition"))
                {
                    var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (unit == null || !names.Remove(unit.unitName) || unit.bakedBody == null || !unit.bakedBody.IsReady) continue;
                    BakedClip shooting = unit.bakedBody.Clip(BakedAnimation.Shooting);
                    for (int percent = 10; percent <= 90; percent += 10)
                    {
                        int row = shooting.firstRow + Mathf.Min(shooting.frames - 1, shooting.frames * percent / 100);
                        byte[] png = RenderBody(unit, BuildBaked(unit, row));
                        if (png != null) File.WriteAllBytes(Path.Combine(folder, $"{UnitIcons.FileName(unit.unitName)}_{percent}.png"), png);
                    }
                }
                exitCode = 0;
            }
            catch (Exception e)
            {
                Debug.LogError("Trying icon moments failed: " + e);
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

        private static GameObject BuildBaked(UnitDefinition unit, int row)
        {
            BakedBodyDefinition baked = unit.bakedBody;
            var body = new GameObject("Icon subject");
            body.AddComponent<MeshFilter>().sharedMesh = baked.mesh;
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = baked.Materials;
            var frame = new Vector4(row, 1f, 0f, 0f);
            var block = new MaterialPropertyBlock();
            block.SetVector(AnimNowId, frame);
            block.SetVector(AnimOldId, frame);
            block.SetVector(AnimBlendId, Vector4.zero);
            block.SetColor(TintId, unit.bakedTint);
            renderer.SetPropertyBlock(block);
            Shader.SetGlobalFloat(BattleTimeId, 0f);
            return body;
        }

        /// <summary>
        /// Units whose icon shows another moment of their shooting clip than the middle, as a share of it: a swing or a slam reads best at another
        /// point (chosen from TryMomentsFromCommandLine's pictures).
        /// </summary>
        private static readonly Dictionary<string, float> IconMoments = new Dictionary<string, float>
        {
            { "Zealot", 0.4f },            // The sword thrust out in front; at the middle it is hidden behind him.
            { "Flood Abomination", 0.5f }, // Reared up, both arms raised over its head before the slam (D93).
            { "Stalker Form", 0.18f },     // Its first lunge, arms and tusks striking (D90); at the middle it is a one-armed swipe.
            { "Combat Form", 0.23f },      // Its first shot, the claw held out and jolting (D92); at the middle it is between shots.
            { "Infested Elite", 0.23f },   // The same with its plasma rifle.
            { "Seeder", 0.36f },           // Squeezing out its spore, the tendrils lashing (D93).
            { "Mantis", 0.53f },           // Both arms aimed, the rocket arm kicking up as a rocket leaves (D97).
            { "Pure Form Tank", 0.47f },   // The club swept round in front (D93).
            { "Juggernaut", 0.44f },       // Both blades down in front of it as it lunges (D107).
        };

        /// <summary>The baked row a unit's icon shows: a moment of its shooting clip (the middle, unless IconMoments says), or its still frame without one.</summary>
        public static int IconRow(UnitDefinition unit)
        {
            BakedBodyDefinition baked = unit.bakedBody;
            BakedClip shooting = baked.Clip(BakedAnimation.Shooting);
            if (shooting != null && shooting.frames > 0)
            {
                float share = IconMoments.TryGetValue(unit.unitName, out float moment) ? moment : 0.5f;
                return shooting.firstRow + Mathf.Clamp(Mathf.FloorToInt(shooting.frames * share), 0, shooting.frames - 1);
            }
            BakedClip still = baked.Clip(BakedAnimation.Still);
            return still != null ? still.firstRow : 0;
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
                bool portrait = FrameTallFigure(camera, ref target, forward, ref distance);
                PlaceCamera(camera, target, forward, distance);

                int big = IconSize * Supersample;
                bool[] final = RenderSolid(camera, big, out Color32[] colors);
                if (!SolidBounds(final, big, out _, out _, out _, out _)) return null;
                Brighten(final, colors);
                return Shrink(final, colors, big, portrait ? PortraitFade : 0f);
            }
            finally
            {
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(subject);
            }
        }

        /// <summary>
        /// A tall thin figure that fills the frame from top to bottom is a sliver at the size of a shop row. This moves the camera
        /// in, to show it from the waist up. Returns false, and leaves the camera alone, for any other shape.
        /// </summary>
        private static bool FrameTallFigure(Camera camera, ref Vector3 target, Vector3 forward, ref float distance)
        {
            const int probe = IconSize;
            PlaceCamera(camera, target, forward, distance);
            if (!SolidBounds(RenderSolid(camera, probe, out _), probe, out int minX, out int minY, out int maxX, out int maxY)) return false;
            float figureHeight = maxY - minY + 1;
            if ((maxX - minX + 1) / figureHeight >= TallFigure) return false;

            float viewHeight = 2f * distance * Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
            float perPixel = viewHeight / probe;
            float newViewHeight = PortraitKeep * figureHeight * perPixel / PortraitFill;
            // The top of the head a little under the top of the frame. Rows run from the bottom.
            float figureTop = (maxY + 1 - probe * 0.5f) * perPixel;
            target += camera.transform.up * (figureTop + (1f - PortraitFill) * newViewHeight - newViewHeight * 0.5f);
            distance *= newViewHeight / viewHeight;

            // Centre what still shows, from side to side.
            PlaceCamera(camera, target, forward, distance);
            if (SolidBounds(RenderSolid(camera, probe, out _), probe, out minX, out _, out maxX, out _))
            {
                target += camera.transform.right * (((minX + maxX + 1) * 0.5f - probe * 0.5f) / probe * newViewHeight);
            }
            return true;
        }

        /// <summary>
        /// Brings a dark picture up to a common brightness. A real model's own paint can be nearly black, which is lost on a dark
        /// row. The curve lifts shadows and midtones, and can't blow out what is already bright. A bright picture is left alone.
        /// </summary>
        private static void Brighten(bool[] solid, Color32[] colors)
        {
            long sum = 0, count = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                if (!solid[i]) continue;
                sum += colors[i].r * 30 + colors[i].g * 59 + colors[i].b * 11;
                count += 100;
            }
            if (count == 0) return;
            float mean = sum / (float)count;
            if (mean >= WantedBrightness) return;
            float gain = Mathf.Min(WantedBrightness / Mathf.Max(1f, mean), MostBrighten);
            var curve = new byte[256];
            for (int i = 0; i < 256; i++) curve[i] = (byte)Mathf.RoundToInt(255f * (1f - Mathf.Pow(1f - i / 255f, gain)));
            for (int i = 0; i < colors.Length; i++)
            {
                if (!solid[i]) continue;
                Color32 c = colors[i];
                colors[i] = new Color32(curve[c.r], curve[c.g], curve[c.b], c.a);
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

        /// <summary>Shrinks the picture by Supersample. `fadeBottom` is the part of its height, at the bottom, that fades out.</summary>
        private static byte[] Shrink(bool[] solid, Color32[] colors, int big, float fadeBottom)
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
                    int alpha = count * 255 / samples;
                    if (fadeBottom > 0f) alpha = Mathf.RoundToInt(alpha * Mathf.SmoothStep(0f, 1f, (y + 0.5f) / (fadeBottom * IconSize)));
                    pixels[y * IconSize + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)alpha);
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
