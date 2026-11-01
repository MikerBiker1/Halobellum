// AnimationBaker.cs
// Turns a rigged model and its clips into what BakedCrowd.shader draws (M9, decisions D41 and D42), for every
// Baked Body asset in Resources/BakedBodies:
//   - a mesh that names two bones per vertex, with finger bones merged into the hands (too small to see at
//     game distance) and, if asked, a plain rifle made of boxes merged into the right hand;
//   - a texture holding every kept bone's matrix for every frame of every clip;
//   - the clip table, and a material.
// Moving and shooting clips play on the spot: the battle moves the unit, so any forward drift in the clip is
// removed. The held standing frame is the moment of the still clip when the feet are closest together.
//   Menu: Halobellum > Art > Bake Animated Bodies
//   Command line: -executeMethod Halobellum.EditorTools.AnimationBaker.BakeAllFromCommandLine
// Depends on: Halobellum.Units (BakedBodyDefinition), UnityEditor.

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
    public static class AnimationBaker
    {
        public const string BodiesFolder = "Assets/_Project/Resources/BakedBodies";
        public const string ModelsFolder = "Assets/_Project/Art/Imported/Models";
        private const string ShaderName = "Halobellum/BakedCrowd";
        private static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        [MenuItem("Halobellum/Art/Bake Animated Bodies")]
        public static void BakeAll()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BakedBodyDefinition), new[] { BodiesFolder }))
            {
                var body = AssetDatabase.LoadAssetAtPath<BakedBodyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Bake(body);
                Debug.Log($"Baked {body.name}: {body.mesh.triangles.Length / 3} triangles, {body.clips.Sum(c => c.frames)} frames.");
            }
            AssetDatabase.SaveAssets();
        }

        public static void BakeAllFromCommandLine()
        {
            int exit = 0;
            try { BakeAll(); }
            catch (Exception e) { Debug.LogError("Baking failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        /// <summary>A body with no source set uses the FBX and PNG in Art/Imported/Models/&lt;its name&gt;.</summary>
        private static void FindSource(BakedBodyDefinition body)
        {
            string folder = $"{ModelsFolder}/{body.name}";
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (string file in Directory.GetFiles(folder))
            {
                string path = file.Replace('\\', '/');
                if (body.sourceModel == null && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) body.sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (body.albedo == null && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) body.albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        public static void Bake(BakedBodyDefinition body)
        {
            FindSource(body);
            if (body.sourceModel == null) throw new InvalidOperationException($"{body.name}: no source model, and none in {ModelsFolder}/{body.name}.");
            string modelPath = AssetDatabase.GetAssetPath(body.sourceModel);
            AnimationClip[] sources = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Source(string name) => sources.FirstOrDefault(c => c.name == name) ?? throw new InvalidOperationException($"{body.name}: no clip named {name} in {modelPath}.");

            GameObject model = Object.Instantiate(body.sourceModel);
            try
            {
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                SkinnedMeshRenderer skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin == null) throw new InvalidOperationException($"{body.name}: {modelPath} has no skinned mesh.");

                Transform[] bones = skin.bones;
                Matrix4x4[] bindPoses = skin.sharedMesh.bindposes;
                int[] keptIndex = KeptBones(bones, out List<int> kept);

                var mesh = new Mesh { name = body.name + " baked", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                var clips = new List<BakedClip>();
                var rows = new List<Color[]>();
                Matrix4x4[] Pose() => kept.Select(b => model.transform.worldToLocalMatrix * bones[b].localToWorldMatrix * bindPoses[b]).ToArray();

                BuildMesh(body, mesh, skin, keptIndex, bones, bindPoses, Source, model);

                int hips = Array.IndexOf(bones, skin.rootBone);
                Vector3 restHips = bindPoses[hips].inverse.MultiplyPoint3x4(Vector3.zero);

                void AddClip(BakedAnimation role, AnimationClip source, bool loops, bool onTheSpot)
                {
                    int frames = Mathf.Max(1, Mathf.RoundToInt(source.length * body.bakedFps));
                    clips.Add(new BakedClip { role = role, firstRow = rows.Count, frames = frames, loops = loops });
                    for (int f = 0; f < frames; f++)
                    {
                        // A loop must not repeat its first frame at the end; a one-shot must reach its last.
                        float time = source.length * (loops ? f / (float)frames : f / Mathf.Max(1f, frames - 1f));
                        source.SampleAnimation(model, time);
                        rows.Add(Row(Pose(), onTheSpot ? Drift(model, bones[hips], restHips) : Matrix4x4.identity));
                    }
                }

                AddClip(BakedAnimation.Moving, Source(body.movingClip), true, true);
                AddClip(BakedAnimation.Shooting, Source(body.shootingClip), true, true);
                AddClip(BakedAnimation.Dead, Source(body.deathClip), false, false);

                // The held frame: the moment of the still clip when the feet are closest together.
                AnimationClip still = Source(body.stillClip);
                Transform leftFoot = bones.FirstOrDefault(b => b.name == "LeftFoot"), rightFoot = bones.FirstOrDefault(b => b.name == "RightFoot");
                float bestTime = 0f, bestGap = float.MaxValue;
                for (int i = 0; i < 40; i++)
                {
                    float time = still.length * i / 40f;
                    still.SampleAnimation(model, time);
                    float gap = leftFoot != null && rightFoot != null ? Vector3.Distance(leftFoot.position, rightFoot.position) : 0f;
                    if (gap < bestGap)
                    {
                        bestGap = gap;
                        bestTime = time;
                    }
                }
                still.SampleAnimation(model, bestTime);
                clips.Add(new BakedClip { role = BakedAnimation.Still, firstRow = rows.Count, frames = 1, loops = false });
                rows.Add(Row(Pose(), Drift(model, bones[hips], restHips)));

                var texture = new Texture2D(kept.Count * 3, rows.Count, TextureFormat.RGBAFloat, false, true)
                {
                    name = body.name + " animation",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };
                texture.SetPixels(rows.SelectMany(r => r).ToArray());
                texture.Apply(false, false);

                string folder = Path.Combine(BodiesFolder, body.name).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(BodiesFolder, body.name);
                AssetDatabase.CreateAsset(mesh, $"{folder}/{body.name}_Mesh.asset");
                AssetDatabase.CreateAsset(texture, $"{folder}/{body.name}_Animation.asset");
                var material = new Material(Shader.Find(ShaderName)) { name = body.name, enableInstancing = true };
                material.SetTexture("_MainTex", body.albedo);
                material.SetTexture("_AnimTex", texture);
                material.SetFloat("_AnimFps", body.bakedFps);
                AssetDatabase.CreateAsset(material, $"{folder}/{body.name}_Material.mat");

                body.mesh = mesh;
                body.material = material;
                body.clips = clips;
                EditorUtility.SetDirty(body);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        /// <summary>Removes what a clip moved the hips sideways or forward, so it plays on the spot.</summary>
        private static Matrix4x4 Drift(GameObject model, Transform hips, Vector3 restHips)
        {
            Vector3 now = model.transform.InverseTransformPoint(hips.position);
            return Matrix4x4.Translate(new Vector3(restHips.x - now.x, 0f, restHips.z - now.z));
        }

        private static Color[] Row(Matrix4x4[] pose, Matrix4x4 shift)
        {
            var row = new Color[pose.Length * 3];
            for (int b = 0; b < pose.Length; b++)
            {
                Matrix4x4 m = shift * pose[b];
                row[b * 3] = new Color(m.m00, m.m01, m.m02, m.m03);
                row[b * 3 + 1] = new Color(m.m10, m.m11, m.m12, m.m13);
                row[b * 3 + 2] = new Color(m.m20, m.m21, m.m22, m.m23);
            }
            return row;
        }

        /// <summary>Which baked bone each source bone becomes: fingers join the parent they hang from.</summary>
        private static int[] KeptBones(Transform[] bones, out List<int> kept)
        {
            kept = new List<int>();
            var result = new int[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                bool finger = bones[b].name.Contains("Hand") && FingerNames.Any(f => bones[b].name.Contains(f));
                result[b] = finger ? -1 : kept.Count;
                if (!finger) kept.Add(b);
            }
            for (int b = 0; b < bones.Length; b++)
            {
                if (result[b] >= 0) continue;
                Transform parent = bones[b].parent;
                while (parent != null && (Array.IndexOf(bones, parent) < 0 || result[Array.IndexOf(bones, parent)] < 0)) parent = parent.parent;
                result[b] = parent != null ? result[Array.IndexOf(bones, parent)] : 0;
            }
            return result;
        }

        private static void BuildMesh(BakedBodyDefinition body, Mesh mesh, SkinnedMeshRenderer skin, int[] keptIndex, Transform[] bones,
            Matrix4x4[] bindPoses, Func<string, AnimationClip> source, GameObject model)
        {
            Mesh from = skin.sharedMesh;
            var positions = new List<Vector3>(from.vertices);
            var normals = new List<Vector3>(from.normals);
            var uvs = new List<Vector2>(from.uv);
            var colors = new List<Color>(Enumerable.Repeat(new Color(0f, 0f, 0f, 0f), positions.Count));
            var boneIds = new List<Vector2>(positions.Count);
            var weights = new List<Vector2>(positions.Count);
            foreach (BoneWeight w in from.boneWeights)
            {
                // Add up what merged bones share, then keep the two strongest.
                var sum = new Dictionary<int, float>();
                void Add(int bone, float weight)
                {
                    if (weight <= 0f) return;
                    sum.TryGetValue(keptIndex[bone], out float total);
                    sum[keptIndex[bone]] = total + weight;
                }
                Add(w.boneIndex0, w.weight0);
                Add(w.boneIndex1, w.weight1);
                Add(w.boneIndex2, w.weight2);
                Add(w.boneIndex3, w.weight3);
                KeyValuePair<int, float>[] top = sum.OrderByDescending(p => p.Value).Take(2).ToArray();
                int second = top.Length > 1 ? top[1].Key : top[0].Key;
                float share = top.Length > 1 ? top[0].Value / (top[0].Value + top[1].Value) : 1f;
                boneIds.Add(new Vector2(top[0].Key, second));
                weights.Add(new Vector2(share, 0f));
            }
            var triangles = new List<int>(from.triangles);

            if (body.holdsRifle)
            {
                int hand = Array.FindIndex(bones, b => b.name == "RightHand");
                int otherHand = Array.FindIndex(bones, b => b.name == "LeftHand");
                if (hand < 0 || otherHand < 0) throw new InvalidOperationException($"{body.name}: a rifle needs RightHand and LeftHand bones.");

                // Aim along the line between the hands, halfway through the shooting clip, then keep it fixed to the hand.
                AnimationClip aim = source(body.shootingClip);
                aim.SampleAnimation(model, aim.length * 0.5f);
                Vector3 direction = (bones[otherHand].position - bones[hand].position).normalized;
                Matrix4x4 rifleToWorld = Matrix4x4.TRS(bones[hand].position, Quaternion.LookRotation(direction, Vector3.up), Vector3.one);
                Matrix4x4 rifleToMesh = bindPoses[hand].inverse * bones[hand].worldToLocalMatrix * rifleToWorld;
                var dark = new Color(0.1f, 0.11f, 0.12f, 1f);
                void Box(Vector3 centre, Vector3 size) => AddBox(positions, normals, uvs, colors, boneIds, weights, triangles, rifleToMesh, centre, size, dark, keptIndex[hand]);
                Box(new Vector3(0f, 0.03f, 0.14f), new Vector3(0.05f, 0.08f, 0.42f));   // Body.
                Box(new Vector3(0f, 0.04f, 0.46f), new Vector3(0.025f, 0.025f, 0.24f)); // Barrel.
                Box(new Vector3(0f, 0.01f, -0.16f), new Vector3(0.04f, 0.09f, 0.2f));   // Stock.
                Box(new Vector3(0f, -0.06f, 0.16f), new Vector3(0.035f, 0.13f, 0.06f)); // Magazine.
                Box(new Vector3(0f, -0.05f, 0f), new Vector3(0.035f, 0.1f, 0.04f));     // Grip.
            }

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, boneIds);
            mesh.SetUVs(2, weights);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            // Generous: the shader moves the vertices, and a falling body leaves its rest pose far behind.
            mesh.bounds = new Bounds(Vector3.up, new Vector3(5f, 5f, 5f));
        }

        private static void AddBox(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs, List<Color> colors, List<Vector2> boneIds,
            List<Vector2> weights, List<int> triangles, Matrix4x4 toMesh, Vector3 centre, Vector3 size, Color color, int bone)
        {
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 face in faces)
            {
                Vector3 u = Mathf.Abs(face.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(face, u);
                int first = positions.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    float a = corner == 0 || corner == 3 ? -0.5f : 0.5f, b = corner < 2 ? -0.5f : 0.5f;
                    Vector3 local = centre + Vector3.Scale(face * 0.5f + u * a + v * b, size);
                    positions.Add(toMesh.MultiplyPoint3x4(local));
                    normals.Add(toMesh.MultiplyVector(face).normalized);
                    uvs.Add(Vector2.zero);
                    colors.Add(color);
                    boneIds.Add(new Vector2(bone, bone));
                    weights.Add(new Vector2(1f, 0f));
                }
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }
        }
    }
}
