// CrowdBenchmark.cs
// Test tool for built games (M9 Pass 0): answers how real animated models should be drawn in big armies,
// before any are imported. It does nothing unless the game is started with "-crowdBench <output file>".
// It stands an original test figure (about 1500 triangles, 15 bones, walking) on the field in rising
// numbers, and measures the frame time of four ways of drawing it:
//   Primitives:  today's way, one capsule per body, no animation;
//   Skinned:     the usual Unity way, a SkinnedMeshRenderer and an animation per body, skinned on the CPU;
//   BakedViews:  one object per body as the game has now, with the animation baked into a texture (BakedCrowd.shader);
//   BakedDirect: the same shader, with no object per body at all;
//   Marine:      the real baked Marine, one object per body, playing one clip;
//   MarineFading: the same, with every body in the middle of a cross-fade all the time (the worst case).
// Results go to the output file, with a screenshot of each way next to it. Then the game quits.
// Depends on: PlaceholderArt, UnityEngine.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Halobellum.Units;
using Unity.Profiling;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class CrowdBenchmark : MonoBehaviour
    {
        private const string Flag = "-crowdBench";
        private const int Frames = 30, Fps = 30, RingSides = 10, RingCount = 5, BatchSize = 1023;
        private static readonly int[] Counts = { 300, 600, 1200 };

        /// <summary>Parent bone (-1 = none), where the bone sits relative to its parent, and the body part hanging from it.</summary>
        private static readonly (int parent, Vector3 at, float length, float radius)[] Skeleton =
        {
            (-1, new Vector3(0f, 0.95f, 0f), 0.16f, 0.14f),       // 0 hips
            (0, new Vector3(0f, 0.16f, 0f), 0.2f, 0.16f),         // 1 belly
            (1, new Vector3(0f, 0.2f, 0f), 0.2f, 0.19f),          // 2 chest
            (2, new Vector3(0f, 0.22f, 0f), 0.22f, 0.11f),        // 3 head
            (2, new Vector3(-0.24f, 0.16f, 0f), -0.3f, 0.06f),    // 4 left upper arm
            (4, new Vector3(0f, -0.3f, 0f), -0.28f, 0.05f),       // 5 left forearm
            (5, new Vector3(0f, -0.28f, 0f), -0.1f, 0.05f),       // 6 left hand
            (2, new Vector3(0.24f, 0.16f, 0f), -0.3f, 0.06f),     // 7 right upper arm
            (7, new Vector3(0f, -0.3f, 0f), -0.28f, 0.05f),       // 8 right forearm
            (8, new Vector3(0f, -0.28f, 0f), -0.1f, 0.05f),       // 9 right hand
            (0, new Vector3(-0.1f, 0f, 0f), -0.42f, 0.08f),       // 10 left thigh
            (10, new Vector3(0f, -0.42f, 0f), -0.42f, 0.06f),     // 11 left shin
            (11, new Vector3(0f, -0.42f, 0f), -0.11f, 0.06f),     // 12 left foot
            (0, new Vector3(0.1f, 0f, 0f), -0.42f, 0.08f),        // 13 right thigh
            (13, new Vector3(0f, -0.42f, 0f), -0.42f, 0.06f),     // 14 right shin
        };

        private readonly StringBuilder _report = new StringBuilder();
        private string _output;
        private Mesh _mesh;
        private Matrix4x4[] _bindPoses;
        private Texture2D _baked;
        private Material _bakedMaterial;
        private Material _litMaterial;
        private Transform _container;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfAsked()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0 || index + 1 >= args.Length) return;
            var host = new GameObject("Crowd Benchmark");
            DontDestroyOnLoad(host);
            host.AddComponent<CrowdBenchmark>()._output = args[index + 1];
        }

        private IEnumerator Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            // Only the crowd counts: no HUD, no match, no camera control.
            foreach (MonoBehaviour other in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (other != this) other.enabled = false;
            }
            BuildMesh();
            BakeAnimation();
            _litMaterial = PlaceholderArt.MaterialFor(new Color(0.25f, 0.45f, 0.95f));
            _report.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}");
            _report.AppendLine($"Test figure: {_mesh.triangles.Length / 3} triangles, {Skeleton.Length} bones");

            yield return Measure("Primitives", SpawnPrimitives, null);
            yield return Measure("Skinned", SpawnSkinned, null);
            yield return Measure("BakedViews", SpawnBakedViews, null);
            yield return Measure("BakedDirect", _ => { }, DrawBakedDirect);
            var marine = Resources.Load<BakedBodyDefinition>("BakedBodies/Marine");
            if (marine != null && marine.IsReady)
            {
                _report.AppendLine($"Real Marine: {marine.mesh.triangles.Length / 3} triangles");
                yield return Measure("Marine", count => SpawnMarines(marine, count, false), null);
                yield return Measure("MarineFading", count => SpawnMarines(marine, count, true), null);
            }

            File.WriteAllText(_output, _report.ToString());
            Application.Quit();
        }

        private IEnumerator Measure(string way, Action<int> spawn, Action<int> everyFrame)
        {
            foreach (int count in Counts)
            {
                if (_container != null) Destroy(_container.gameObject);
                _container = new GameObject("Crowd").transform;
                spawn(count);
                yield return Wait(1f, everyFrame, count);

                var times = new List<float>();
                using (ProfilerRecorder batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
                {
                    float until = Time.realtimeSinceStartup + 4f;
                    while (Time.realtimeSinceStartup < until)
                    {
                        everyFrame?.Invoke(count);
                        yield return null;
                        times.Add(Time.unscaledDeltaTime * 1000f);
                    }
                    times.Sort();
                    float mean = 0f;
                    foreach (float t in times) mean += t;
                    mean /= Mathf.Max(1, times.Count);
                    string drawn = batches.Valid ? batches.LastValue.ToString(CultureInfo.InvariantCulture) : "n/a";
                    _report.AppendLine(FormattableString.Invariant($"{way,-12} {count,5} bodies: {mean,7:0.00} ms mean, {times[(int)(times.Count * 0.95f)],7:0.00} ms 95th percentile, {1000f / mean,6:0} FPS, batches {drawn}"));
                }
                if (count == Counts[1])
                {
                    // A close look, to check the figures draw properly and walk the same way each time.
                    Transform view = Camera.main.transform;
                    view.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                    view.SetPositionAndRotation(new Vector3(0f, 5f, -24f), Quaternion.LookRotation(new Vector3(0f, -4f, 12f)));
                    yield return Wait(0.2f, everyFrame, count);
                    ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(_output) ?? "", $"crowd_{way}.png"));
                    yield return Wait(0.3f, everyFrame, count);
                    view.SetPositionAndRotation(position, rotation);
                }
            }
            if (_container != null) Destroy(_container.gameObject);
        }

        private static IEnumerator Wait(float seconds, Action<int> everyFrame, int count)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                everyFrame?.Invoke(count);
                yield return null;
            }
        }

        private static Vector3 Place(int index, int count)
        {
            int side = Mathf.CeilToInt(Mathf.Sqrt(count));
            const float spacing = 1.3f;
            return new Vector3((index % side - side * 0.5f) * spacing, 0f, (index / side - side * 0.5f) * spacing);
        }

        // ------------------------------------------------------------------ the four ways

        private void SpawnPrimitives(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Transform body = PlaceholderArt.CreatePart("Body", _container, PlaceholderArt.Capsule, _litMaterial);
                body.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                body.position = Place(i, count) + Vector3.up * 0.9f;
            }
        }

        private void SpawnSkinned(int count)
        {
            AnimationClip clip = BuildClip();
            for (int i = 0; i < count; i++)
            {
                var root = new GameObject("Body");
                root.transform.SetParent(_container, false);
                root.transform.position = Place(i, count);
                var bones = new Transform[Skeleton.Length];
                for (int b = 0; b < bones.Length; b++)
                {
                    bones[b] = new GameObject("Bone" + b).transform;
                    bones[b].SetParent(Skeleton[b].parent >= 0 ? bones[Skeleton[b].parent] : root.transform, false);
                    bones[b].localPosition = Skeleton[b].at;
                }
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = _mesh;
                skin.bones = bones;
                skin.rootBone = bones[0];
                skin.sharedMaterial = _litMaterial;
                skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skin.receiveShadows = false;
                skin.updateWhenOffscreen = true;
                var animation = root.AddComponent<Animation>();
                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                animation.AddClip(clip, "Walk");
                animation.wrapMode = WrapMode.Loop;
                animation.Play("Walk");
                animation["Walk"].time = (i % 30) / (float)Fps;
            }
        }

        private void SpawnBakedViews(int count)
        {
            var block = new MaterialPropertyBlock();
            int animId = Shader.PropertyToID("_AnimNow"), blendId = Shader.PropertyToID("_AnimBlend"), colorId = Shader.PropertyToID("_BaseColor");
            for (int i = 0; i < count; i++)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(_container, false);
                body.transform.position = Place(i, count);
                body.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var renderer = body.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _bakedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                block.SetVector(animId, new Vector4(0f, Frames, -(i % 30) / (float)Fps, 1f));
                block.SetVector(blendId, Vector4.zero);
                block.SetColor(colorId, new Color(0.25f, 0.45f, 0.95f, 1f));
                renderer.SetPropertyBlock(block);
            }
        }

        private void SpawnMarines(BakedBodyDefinition marine, int count, bool fading)
        {
            var block = new MaterialPropertyBlock();
            BakedClip shooting = marine.Clip(BakedAnimation.Shooting), moving = marine.Clip(BakedAnimation.Moving);
            for (int i = 0; i < count; i++)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(_container, false);
                body.transform.position = Place(i, count);
                body.AddComponent<MeshFilter>().sharedMesh = marine.mesh;
                var renderer = body.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = marine.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                block.SetVector("_AnimNow", new Vector4(shooting.firstRow, shooting.frames, -(i % 30) / 30f, 1f));
                block.SetVector("_AnimOld", new Vector4(moving.firstRow, moving.frames, -(i % 30) / 30f, 1f));
                // A fade so long that it never finishes in this test.
                block.SetVector("_AnimBlend", fading ? new Vector4(_time, 100000f, 0f, 0f) : Vector4.zero);
                block.SetColor("_BaseColor", new Color(0.25f, 0.45f, 0.95f, 0.45f));
                renderer.SetPropertyBlock(block);
            }
        }

        private float _time;

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            Shader.SetGlobalFloat("_BattleTime", _time);
        }

        private Matrix4x4[] _matrices = new Matrix4x4[0];
        private Vector4[] _anims;
        private Vector4[] _blends;
        private Vector4[] _colors;
        private MaterialPropertyBlock _directBlock;

        private void DrawBakedDirect(int count)
        {
            if (_directBlock == null) _directBlock = new MaterialPropertyBlock();
            if (_matrices.Length != count)
            {
                _matrices = new Matrix4x4[count];
                for (int i = 0; i < count; i++) _matrices[i] = Matrix4x4.Translate(Place(i, count));
            }
            if (_anims == null)
            {
                _anims = new Vector4[BatchSize];
                _blends = new Vector4[BatchSize];
                _colors = new Vector4[BatchSize];
                for (int i = 0; i < BatchSize; i++)
                {
                    _anims[i] = new Vector4(0f, Frames, -(i % 30) / (float)Fps, 1f);
                    _colors[i] = new Vector4(0.25f, 0.45f, 0.95f, 1f);
                }
            }
            _directBlock.SetVectorArray("_AnimNow", _anims);
            _directBlock.SetVectorArray("_AnimBlend", _blends);
            _directBlock.SetVectorArray("_BaseColor", _colors);
            var parameters = new RenderParams(_bakedMaterial)
            {
                matProps = _directBlock,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
            };
            for (int start = 0; start < count; start += BatchSize)
                Graphics.RenderMeshInstanced(parameters, _mesh, 0, _matrices, Mathf.Min(BatchSize, count - start), start);
        }

        // ------------------------------------------------------------------ the test figure

        /// <summary>Rotation of a bone at a phase of the walk (0 to 1), around the sideways axis.</summary>
        private static Quaternion WalkRotation(int bone, float phase)
        {
            float swing = Mathf.Sin(phase * Mathf.PI * 2f);
            switch (bone)
            {
                case 4: return Quaternion.Euler(swing * 32f, 0f, 0f);
                case 5: return Quaternion.Euler(-25f + swing * 15f, 0f, 0f);
                case 7: return Quaternion.Euler(-swing * 32f, 0f, 0f);
                case 8: return Quaternion.Euler(-25f - swing * 15f, 0f, 0f);
                case 10: return Quaternion.Euler(-swing * 32f, 0f, 0f);
                case 11: return Quaternion.Euler(20f + Mathf.Max(0f, swing) * 35f, 0f, 0f);
                case 13: return Quaternion.Euler(swing * 32f, 0f, 0f);
                case 14: return Quaternion.Euler(20f + Mathf.Max(0f, -swing) * 35f, 0f, 0f);
                default: return Quaternion.identity;
            }
        }

        private void BuildMesh()
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var weights = new List<BoneWeight>();
            var boneIds = new List<Vector2>();
            var boneWeights = new List<Vector2>();
            var triangles = new List<int>();

            // Where each bone sits in the body's own space, at rest.
            var rest = new Vector3[Skeleton.Length];
            for (int b = 0; b < rest.Length; b++) rest[b] = (Skeleton[b].parent >= 0 ? rest[Skeleton[b].parent] : Vector3.zero) + Skeleton[b].at;

            for (int b = 0; b < rest.Length; b++)
            {
                int first = positions.Count;
                for (int ring = 0; ring < RingCount; ring++)
                {
                    float t = ring / (RingCount - 1f);
                    float radius = Skeleton[b].radius * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow(t * 2f - 1f, 2f) * 0.7f));
                    for (int side = 0; side < RingSides; side++)
                    {
                        float angle = side * Mathf.PI * 2f / RingSides;
                        var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        positions.Add(rest[b] + Vector3.up * (Skeleton[b].length * t) + around * radius);
                        normals.Add(around);
                        weights.Add(new BoneWeight { boneIndex0 = b, weight0 = 1f });
                        boneIds.Add(new Vector2(b, b));
                        boneWeights.Add(new Vector2(1f, 0f));
                    }
                }
                for (int ring = 0; ring < RingCount - 1; ring++)
                {
                    for (int side = 0; side < RingSides; side++)
                    {
                        int next = (side + 1) % RingSides;
                        int a = first + ring * RingSides + side, c = first + ring * RingSides + next;
                        int d = first + (ring + 1) * RingSides + side, e = first + (ring + 1) * RingSides + next;
                        if (Skeleton[b].length >= 0f) triangles.AddRange(new[] { a, d, c, c, d, e });
                        else triangles.AddRange(new[] { a, c, d, c, e, d });
                    }
                }
            }

            _bindPoses = new Matrix4x4[rest.Length];
            for (int b = 0; b < rest.Length; b++) _bindPoses[b] = Matrix4x4.Translate(-rest[b]);

            _mesh = new Mesh { name = "Crowd test figure" };
            _mesh.SetVertices(positions);
            _mesh.SetNormals(normals);
            _mesh.SetUVs(1, boneIds);
            _mesh.SetUVs(2, boneWeights);
            _mesh.SetColors(new Color[positions.Count]);
            _mesh.boneWeights = weights.ToArray();
            _mesh.bindposes = _bindPoses;
            _mesh.SetTriangles(triangles, 0);
            _mesh.bounds = new Bounds(Vector3.up, new Vector3(2f, 3f, 2f));
        }

        /// <summary>Stores every bone's matrix (body space, from its rest pose) for every frame of the walk in a texture.</summary>
        private void BakeAnimation()
        {
            int bones = Skeleton.Length;
            var pixels = new Color[bones * 3 * Frames];
            var world = new Matrix4x4[bones];
            for (int frame = 0; frame < Frames; frame++)
            {
                float phase = frame / (float)Frames;
                for (int b = 0; b < bones; b++)
                {
                    Matrix4x4 local = Matrix4x4.TRS(Skeleton[b].at, WalkRotation(b, phase), Vector3.one);
                    world[b] = Skeleton[b].parent >= 0 ? world[Skeleton[b].parent] * local : local;
                    Matrix4x4 skin = world[b] * _bindPoses[b];
                    int at = frame * bones * 3 + b * 3;
                    pixels[at] = new Color(skin.m00, skin.m01, skin.m02, skin.m03);
                    pixels[at + 1] = new Color(skin.m10, skin.m11, skin.m12, skin.m13);
                    pixels[at + 2] = new Color(skin.m20, skin.m21, skin.m22, skin.m23);
                }
            }
            _baked = new Texture2D(bones * 3, Frames, TextureFormat.RGBAFloat, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _baked.SetPixels(pixels);
            _baked.Apply(false, true);

            _bakedMaterial = new Material(Resources.Load<Material>("Crowd/BakedCrowd"));
            _bakedMaterial.SetTexture("_AnimTex", _baked);
            _bakedMaterial.SetFloat("_FrameCount", Frames);
            _bakedMaterial.SetFloat("_AnimFps", Fps);
            _bakedMaterial.enableInstancing = true;
        }

        /// <summary>The same walk as a clip, for the normal way of animating.</summary>
        private static AnimationClip BuildClip()
        {
            var clip = new AnimationClip { legacy = true, wrapMode = WrapMode.Loop };
            var paths = new string[Skeleton.Length];
            for (int b = 0; b < paths.Length; b++)
            {
                paths[b] = (Skeleton[b].parent >= 0 ? paths[Skeleton[b].parent] + "/" : "") + "Bone" + b;
                var x = new AnimationCurve();
                var y = new AnimationCurve();
                var z = new AnimationCurve();
                var w = new AnimationCurve();
                for (int frame = 0; frame <= Frames; frame++)
                {
                    float time = frame / (float)Fps;
                    Quaternion rotation = WalkRotation(b, frame / (float)Frames);
                    x.AddKey(time, rotation.x);
                    y.AddKey(time, rotation.y);
                    z.AddKey(time, rotation.z);
                    w.AddKey(time, rotation.w);
                }
                clip.SetCurve(paths[b], typeof(Transform), "localRotation.x", x);
                clip.SetCurve(paths[b], typeof(Transform), "localRotation.y", y);
                clip.SetCurve(paths[b], typeof(Transform), "localRotation.z", z);
                clip.SetCurve(paths[b], typeof(Transform), "localRotation.w", w);
            }
            return clip;
        }
    }
}
