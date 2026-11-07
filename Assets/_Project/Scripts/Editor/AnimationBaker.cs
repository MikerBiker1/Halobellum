// AnimationBaker.cs
// Turns a rigged model and its clips into what BakedCrowd.shader draws (M9, decisions D41 and D42), for every
// Baked Body asset in Resources/BakedBodies:
//   - a mesh that names two bones per vertex, with finger bones merged into the hands (too small to see at
//     game distance) and, if it has one, a prepared weapon merged into the right hand, and a shield on the left in a second, see-through sub-mesh;
//     a body whose gun is part of its own model (the Flamethrower Trooper) shoots from its furthest-forward point instead (muzzleAtFront),
//     and a creature from one of its bones at the moment it shoots (muzzleBone: the Pure Form Ranged's spines, D90);
//   - a texture holding every kept bone's matrix for every frame of every clip;
//   - the clip table, and a material (cut out by its texture's alpha for a body with fronds, D90).
// Moving and shooting clips play on the spot: the battle moves the unit, so any forward drift in the clip is
// removed. The held standing frame is the moment of the still clip when the feet are closest together.
//   Menu: Halobellum > Art > Bake Animated Bodies
//   Command line: -executeMethod Halobellum.EditorTools.AnimationBaker.BakeAllFromCommandLine (or BakeNamedFromCommandLine for the bodies
//   named in HB_BODIES, comma separated, such as Spartan)
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

        /// <summary>Bakes only the bodies that have never been baked, leaving the others as they are (M10).</summary>
        public static void BakeNewFromCommandLine()
        {
            int exit = 0;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BakedBodyDefinition), new[] { BodiesFolder }))
                {
                    var body = AssetDatabase.LoadAssetAtPath<BakedBodyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (body.IsReady) continue;
                    Bake(body);
                    Debug.Log($"Baked {body.name}: {body.mesh.triangles.Length / 3} triangles.");
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError("Baking failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        /// <summary>Bakes only the bodies named in the environment's HB_BODIES, comma separated, such as Spartan (D85).</summary>
        public static void BakeNamedFromCommandLine()
        {
            int exit = 0;
            try
            {
                string named = Environment.GetEnvironmentVariable("HB_BODIES");
                if (string.IsNullOrEmpty(named)) throw new ArgumentException("Name the bodies in HB_BODIES, such as Spartan.");
                foreach (string name in named.Split(','))
                {
                    var body = AssetDatabase.LoadAssetAtPath<BakedBodyDefinition>($"{BodiesFolder}/{name.Trim()}.asset");
                    if (body == null) throw new ArgumentException("No baked body named " + name);
                    Bake(body);
                    Debug.Log($"Baked {body.name}: {body.mesh.triangles.Length / 3} triangles, {body.clips.Sum(c => c.frames)} frames.");
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception e) { Debug.LogError("Baking failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        public static void BakeAllFromCommandLine()
        {
            int exit = 0;
            try { BakeAll(); }
            catch (Exception e) { Debug.LogError("Baking failed: " + e); exit = 1; }
            EditorApplication.Exit(exit);
        }

        /// <summary>A body with no source set uses the FBX and PNG in Art/Imported/Models/&lt;its model folder, or else its name&gt;.</summary>
        private static void FindSource(BakedBodyDefinition body)
        {
            string folder = $"{ModelsFolder}/{(string.IsNullOrEmpty(body.modelFolder) ? body.name : body.modelFolder)}";
            if (!AssetDatabase.IsValidFolder(folder)) return;
            if (body.albedo == null && !string.IsNullOrEmpty(body.albedoFile)) body.albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{body.albedoFile}");
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
            AnimationClip Source(string name) => sources.FirstOrDefault(c => c.name == name) ?? throw new InvalidOperationException($"{body.name}: no clip named {name} in {modelPath}. It has: {string.Join(", ", sources.Select(c => c.name))}.");

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

                Vector3? muzzle = BuildMesh(body, mesh, skin, keptIndex, bones, bindPoses, Source, model);

                int hips = Array.IndexOf(bones, skin.rootBone);
                // Where the hips stand at rest, in the model's space: a bind pose is in the skinned mesh's own space, which is the model's for
                // Meshy's files but not for one whose mesh hangs under a turned and scaled rig, as Blender writes them (the Flamethrower Trooper's).
                Vector3 restHips = model.transform.InverseTransformPoint(skin.transform.TransformPoint(bindPoses[hips].inverse.MultiplyPoint3x4(Vector3.zero)));
                Transform hipsBone = bones[hips];
                // A creature rigged by Tools/Art/creature_rig.py (D90) stands on a still bone named Root, the parent of its Body, which does all
                // the swaying. The importer drops Root from the skin, as it moves no vertex; it is the one to measure drift by, and as it never
                // moves, nothing is taken off: the body's sway and lunges stay.
                Transform standing = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Body" && t.parent != null && t.parent.name == "Root" && t.GetComponent<Renderer>() == null)?.parent;
                if (standing != null)
                {
                    hipsBone = standing;
                    restHips = model.transform.InverseTransformPoint(standing.position);
                }
                body.hasMuzzle = muzzle.HasValue;
                if (muzzle.HasValue)
                {
                    // BuildMesh left the model in the pose the weapon was aimed in; shooting plays on the spot.
                    body.muzzle = Drift(model, hipsBone, restHips).MultiplyPoint3x4(model.transform.InverseTransformPoint(muzzle.Value));
                }

                void AddClip(BakedAnimation role, AnimationClip source, bool loops, bool onTheSpot, float blendShare = 0f)
                {
                    int frames = Mathf.Max(1, Mathf.RoundToInt(source.length * body.bakedFps));
                    clips.Add(new BakedClip { role = role, firstRow = rows.Count, frames = frames, loops = loops });
                    // A clip made to be played once is eased, over its last share, back into its first pose, bone by bone, so it loops
                    // without a jump (Meshy's swing for the Brute Grenadier, 2026-11-07).
                    Transform[] all = model.GetComponentsInChildren<Transform>();
                    var firstPosition = new Vector3[all.Length];
                    var firstRotation = new Quaternion[all.Length];
                    if (blendShare > 0f)
                    {
                        source.SampleAnimation(model, 0f);
                        for (int i = 0; i < all.Length; i++) { firstPosition[i] = all[i].localPosition; firstRotation[i] = all[i].localRotation; }
                    }
                    int blendFrom = Mathf.RoundToInt(frames * (1f - blendShare));
                    for (int f = 0; f < frames; f++)
                    {
                        // A loop must not repeat its first frame at the end; a one-shot must reach its last.
                        float time = source.length * (loops ? f / (float)frames : f / Mathf.Max(1f, frames - 1f));
                        source.SampleAnimation(model, time);
                        if (blendShare > 0f && f >= blendFrom)
                        {
                            float weight = Mathf.SmoothStep(0f, 1f, (f - blendFrom + 1f) / (frames - blendFrom + 1f));
                            for (int i = 0; i < all.Length; i++)
                            {
                                all[i].localPosition = Vector3.Lerp(all[i].localPosition, firstPosition[i], weight);
                                all[i].localRotation = Quaternion.Slerp(all[i].localRotation, firstRotation[i], weight);
                            }
                        }
                        rows.Add(Row(Pose(), onTheSpot ? Drift(model, hipsBone, restHips) : Matrix4x4.identity));
                    }
                }

                AddClip(BakedAnimation.Moving, Source(body.movingClip), true, true);
                AddClip(BakedAnimation.Shooting, Source(body.shootingClip), true, true);
                AddClip(BakedAnimation.Dead, Source(body.deathClip), false, false);
                // A close-in blow and a charge, for a body that has them (the Hunters, D87). The blow keeps its step in (it starts and ends
                // in place); the charge plays on the spot, as the battle moves the unit.
                if (!string.IsNullOrEmpty(body.meleeClip)) AddClip(BakedAnimation.Melee, Source(body.meleeClip), true, false, body.meleeBlendShare);
                if (!string.IsNullOrEmpty(body.chargeClip)) AddClip(BakedAnimation.Charge, Source(body.chargeClip), true, true);

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
                rows.Add(Row(Pose(), Drift(model, hipsBone, restHips)));

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
                material.SetFloat("_Cutout", body.cutout ? 1f : 0f);
                AssetDatabase.CreateAsset(material, $"{folder}/{body.name}_Material.mat");

                body.glassMaterial = null;
                if (mesh.subMeshCount > 1)
                {
                    // Over the battlefield and everything opaque, seen from both sides, and writing no depth.
                    var glass = new Material(material) { name = body.name + " glass", enableInstancing = true, renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent };
                    glass.SetFloat("_Glass", 1f);
                    glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    glass.SetFloat("_ZWrite", 0f);
                    glass.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                    AssetDatabase.CreateAsset(glass, $"{folder}/{body.name}_Glass.mat");
                    body.glassMaterial = glass;
                }

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

        /// <summary>Builds the mesh. Returns where the weapon's muzzle is in the world in the shooting pose, if it holds one.</summary>
        private static Vector3? BuildMesh(BakedBodyDefinition body, Mesh mesh, SkinnedMeshRenderer skin, int[] keptIndex, Transform[] bones,
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

            Vector3? muzzle = null;
            if (string.IsNullOrEmpty(body.weaponName) && !string.IsNullOrEmpty(body.muzzleBone))
            {
                // A creature's own: where the bone is at the moment of the shot (D90). The rig gives it a bone of its own for this, which
                // moves no vertex, so it is looked for in the whole rig and not only among the skin's bones.
                Transform bone = model.GetComponentsInChildren<Transform>().FirstOrDefault(b => b.name == body.muzzleBone);
                if (bone == null) throw new InvalidOperationException($"{body.name}: no bone named {body.muzzleBone} for its shots.");
                AnimationClip aim = source(body.shootingClip);
                aim.SampleAnimation(model, aim.length * (body.shotMoment >= 0f ? body.shotMoment : 0.5f));
                muzzle = bone.position;
            }
            else if (string.IsNullOrEmpty(body.weaponName) && body.muzzleAtFront)
            {
                // The body's own gun: its tip is the vertex furthest forward halfway through the shooting clip, skinned as the shader will.
                AnimationClip aim = source(body.shootingClip);
                aim.SampleAnimation(model, aim.length * 0.5f);
                Matrix4x4[] skinning = bones.Select((b, i) => b.localToWorldMatrix * bindPoses[i]).ToArray();
                BoneWeight[] boneWeights = from.boneWeights;
                float front = float.MinValue;
                for (int i = 0; i < positions.Count; i++)
                {
                    BoneWeight w = boneWeights[i];
                    Vector3 v = positions[i];
                    Vector3 world = skinning[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + skinning[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                        + skinning[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + skinning[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                    float ahead = model.transform.InverseTransformPoint(world).z;
                    if (ahead > front)
                    {
                        front = ahead;
                        muzzle = world;
                    }
                }
            }
            else if (string.IsNullOrEmpty(body.weaponName) && body.muzzleInRightHand)
            {
                int hand = Array.FindIndex(bones, b => b.name == "RightHand");
                if (hand < 0) throw new InvalidOperationException($"{body.name}: shots from the right hand need a RightHand bone.");
                AnimationClip aim = source(body.shootingClip);
                aim.SampleAnimation(model, aim.length * 0.5f);
                muzzle = bones[hand].position;
            }
            else if (!string.IsNullOrEmpty(body.weaponName))
            {
                int hand = Array.FindIndex(bones, b => b.name == "RightHand");
                int otherHand = Array.FindIndex(bones, b => b.name == "LeftHand");
                if (hand < 0 || otherHand < 0) throw new InvalidOperationException($"{body.name}: a weapon needs RightHand and LeftHand bones.");

                // Aim along the line between the hands, halfway through the shooting clip, then keep it fixed to the hand.
                AnimationClip aim = source(body.shootingClip);
                aim.SampleAnimation(model, aim.length * 0.5f);
                // A small weapon in the forward hand simply points forward: the line between the hands means nothing for it.
                Vector3 direction = body.weaponInForwardHand ? model.transform.forward : (bones[otherHand].position - bones[hand].position).normalized;
                int holding = body.weaponInForwardHand ? otherHand : hand;
                if (body.weaponAlongForearm)
                {
                    direction = (bones[hand].position - bones[hand].parent.position).normalized;
                    holding = hand;
                }
                Matrix4x4 weaponToWorld = Matrix4x4.TRS(bones[holding].position, Quaternion.LookRotation(direction, Vector3.up), Vector3.one);
                muzzle = AddProp(body, body.weaponName, body.weaponLength, false, weaponToWorld, bindPoses[holding].inverse * bones[holding].worldToLocalMatrix, keptIndex[holding], positions, normals, uvs, colors, boneIds, weights, triangles);
            }

            var glass = new List<int>();
            if (!string.IsNullOrEmpty(body.shieldName))
            {
                int arm = Array.FindIndex(bones, b => b.name == "LeftHand");
                if (arm < 0) throw new InvalidOperationException($"{body.name}: a shield needs a LeftHand bone.");
                AnimationClip aim = source(body.shootingClip);
                aim.SampleAnimation(model, aim.length * 0.5f);
                Matrix4x4 shieldToWorld = Matrix4x4.TRS(bones[arm].position + model.transform.TransformVector(body.shieldOffset), Quaternion.LookRotation(model.transform.forward, Vector3.up), Vector3.one);
                AddProp(body, body.shieldName, body.shieldDiameter, true, shieldToWorld, bindPoses[arm].inverse * bones[arm].worldToLocalMatrix, keptIndex[arm], positions, normals, uvs, colors, boneIds, weights, glass);
            }

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, boneIds);
            mesh.SetUVs(2, weights);
            mesh.SetColors(colors);
            mesh.subMeshCount = glass.Count > 0 ? 2 : 1;
            mesh.SetTriangles(triangles, 0);
            if (glass.Count > 0) mesh.SetTriangles(glass, 1);
            // Generous: the shader moves the vertices, and a falling body leaves its rest pose far behind.
            Matrix4x4 meshToModel = model.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            float highest = positions.Max(p => meshToModel.MultiplyPoint3x4(p).y), lowest = positions.Min(p => meshToModel.MultiplyPoint3x4(p).y);
            body.bodyHeight = highest - lowest;
            float reach = Mathf.Max(5f, body.bodyHeight * 2.5f);
            mesh.bounds = new Bounds(Vector3.up * Mathf.Max(1f, body.bodyHeight * 0.5f), new Vector3(reach, reach, reach));
            return muzzle;
        }

        /// <summary>
        /// Merges a prepared weapon or shield into the mesh, fixed to one bone. Its origin is its grip, it points along +Z, and a node
        /// "Muzzle" marks the tip of its barrel. Returns where that tip is in the world. A weapon's size is its length; a shield's, its width,
        /// and a shield's vertices say in their colour's alpha how much they show: more at the rim, as a field of energy does.
        /// </summary>
        private static Vector3 AddProp(BakedBodyDefinition body, string prop, float size, bool shield, Matrix4x4 weaponToWorld, Matrix4x4 worldToMesh, int bone, List<Vector3> positions, List<Vector3> normals,
            List<Vector2> uvs, List<Color> colors, List<Vector2> boneIds, List<Vector2> weights, List<int> triangles)
        {
            string path = $"{ModelsFolder}/{prop}/{prop}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException($"{body.name}: no weapon at {path}.");
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            GameObject weapon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            try
            {
                weapon.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                MeshFilter filter = weapon.GetComponentInChildren<MeshFilter>();
                Transform tip = weapon.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Muzzle");
                if (filter == null || tip == null) throw new InvalidOperationException($"{body.name}: {path} needs a mesh and a node named Muzzle.");

                Mesh from = filter.sharedMesh;
                Matrix4x4 meshToWeapon = filter.transform.localToWorldMatrix;
                Vector3[] vertices = from.vertices;
                float Extent(Func<Vector3, float> along) => vertices.Select(v => along(meshToWeapon.MultiplyPoint3x4(v))).Max() - vertices.Select(v => along(meshToWeapon.MultiplyPoint3x4(v))).Min();
                float length = shield ? Mathf.Max(Extent(v => v.x), Extent(v => v.y)) : Extent(v => v.z);
                Matrix4x4 sized = Matrix4x4.Scale(Vector3.one * (size / length));
                Matrix4x4 toMesh = worldToMesh * weaponToWorld * sized * meshToWeapon;

                int first = positions.Count;
                Vector3[] fromNormals = from.normals;
                Color[] fromColors = from.colors;
                for (int i = 0; i < vertices.Length; i++)
                {
                    positions.Add(toMesh.MultiplyPoint3x4(vertices[i]));
                    normals.Add(toMesh.MultiplyVector(fromNormals[i]).normalized);
                    uvs.Add(Vector2.zero);
                    Color colour = fromColors.Length > i ? fromColors[i] : new Color(0.02f, 0.02f, 0.02f);
                    float rim = shield ? new Vector2(meshToWeapon.MultiplyPoint3x4(vertices[i]).x, meshToWeapon.MultiplyPoint3x4(vertices[i]).y).magnitude / (length * 0.5f) : 0f;
                    colors.Add(new Color(colour.r, colour.g, colour.b, shield ? Mathf.Lerp(0.3f, 0.85f, rim * rim * rim) : 1f)); // Alpha above 0 tells the shader to use this colour.
                    boneIds.Add(new Vector2(bone, bone));
                    weights.Add(new Vector2(1f, 0f));
                }
                foreach (int index in from.triangles) triangles.Add(first + index);
                return (weaponToWorld * sized).MultiplyPoint3x4(tip.position);
            }
            finally
            {
                Object.DestroyImmediate(weapon);
            }
        }
    }
}
