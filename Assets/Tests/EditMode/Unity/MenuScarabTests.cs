// MenuScarabTests.cs
// The Scarab in the classic main menu's picture (M10; the menu kept as MainMenuClassic since D94): it stands still with all four legs on the ground. These read the model's vertices against the
// ground's triangles in the real scene, which only the editor can do: a game can't read a model's meshes. Here the Scarab keeps its legs
// as they are in the model, so that a leg here is only a small slide away from the prefab's. A leg is the walker's Leg_ node with its knee and
// foot under it (D84); in battle WalkerLegs bends them, here nothing does.
// Depends on: Halobellum.UI (MenuBackdrop), Unity Test Framework, UnityEditor (opening the scene).

using System.Collections.Generic;
using Halobellum.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halobellum.Tests
{
    public class MenuScarabTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenuClassic.unity"; // The classic menu, kept since D94.
        private const string PrefabPath = "Assets/_Project/Prefabs/Units/Scarab_Model.prefab";

        private MenuBackdrop _backdrop;

        [SetUp]
        public void OpenTheMenu()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _backdrop = Object.FindAnyObjectByType<MenuBackdrop>();
            Assert.IsNotNull(_backdrop);
            Assert.IsNotNull(_backdrop.walker);
        }

        [TearDown]
        public void LeaveNoSceneOpen() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static List<Transform> LegsOf(Transform model)
        {
            var legs = new List<Transform>();
            foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
            {
                if (node.name.StartsWith("Leg_")) legs.Add(node);
            }
            return legs;
        }

        private static Vector3 LowestPoint(Transform leg)
        {
            var lowest = new Vector3(0f, float.MaxValue, 0f);
            foreach (MeshFilter filter in leg.GetComponentsInChildren<MeshFilter>(true))
            {
                Matrix4x4 world = filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = world.MultiplyPoint3x4(vertex);
                    if (point.y < lowest.y) lowest = point;
                }
            }
            return lowest;
        }

        private static float GroundUnder(Vector3 point)
        {
            Mesh ground = GameObject.Find("Ground").GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = ground.vertices;
            int[] triangles = ground.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                float area = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(area) < 1e-6f) continue;
                float shareA = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / area;
                float shareB = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / area;
                if (shareA < 0f || shareB < 0f || shareA + shareB > 1f) continue;
                return shareA * a.y + shareB * b.y + (1f - shareA - shareB) * c.y;
            }
            Assert.Fail($"No ground under {point}.");
            return 0f;
        }

        [Test]
        public void AllFourLegs_TouchTheGround()
        {
            List<Transform> legs = LegsOf(_backdrop.walker);
            Assert.AreEqual(4, legs.Count);
            _backdrop.ApplyAt(0f);
            var feet = new List<Vector2>();
            foreach (Transform leg in legs)
            {
                Vector3 foot = LowestPoint(leg);
                float gap = foot.y - GroundUnder(foot);
                Assert.AreEqual(0f, gap, 0.1f, $"{leg.name}'s lowest point against the ground, in metres.");
                feet.Add(new Vector2(foot.x, foot.z));
            }
            for (int i = 0; i < feet.Count; i++)
            {
                for (int j = i + 1; j < feet.Count; j++) Assert.Greater(Vector2.Distance(feet[i], feet[j]), 20f, "Four feet, each its own: not one leg counted twice.");
            }
        }

        [Test]
        public void TheScarab_StandsStill_WhileTheClockRuns()
        {
            Transform model = _backdrop.walker;
            List<Transform> legs = LegsOf(model);
            _backdrop.ApplyAt(0f);
            Vector3 position = model.position;
            Quaternion rotation = model.rotation;
            var poses = new List<(Vector3 place, Quaternion turn)>();
            foreach (Transform leg in legs) poses.Add((leg.position, leg.rotation));

            foreach (float time in new[] { 1f, 8.5f, 10f, 37f, 90f, 600f })
            {
                _backdrop.ApplyAt(time);
                Assert.AreEqual(position, model.position, $"It doesn't walk (at {time} s).");
                Assert.AreEqual(rotation, model.rotation);
                for (int i = 0; i < legs.Count; i++)
                {
                    Assert.AreEqual(poses[i].place, legs[i].position, $"{legs[i].name} stays (at {time} s).");
                    Assert.AreEqual(poses[i].turn, legs[i].rotation, $"{legs[i].name} doesn't swing (at {time} s).");
                }
            }
        }

        [Test]
        public void TheLegsKeepTheModelsPose_ApartFromASmallSlide()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            List<Transform> theirs = LegsOf(prefab.transform);
            List<Transform> ours = LegsOf(_backdrop.walker);
            Assert.AreEqual(theirs.Count, ours.Count);
            Vector3 tilt = _backdrop.walker.rotation.eulerAngles;
            Assert.AreEqual(0f, Mathf.DeltaAngle(tilt.x, 0f), 0.01f, "The body stands level.");
            Assert.AreEqual(0f, Mathf.DeltaAngle(tilt.z, 0f), 0.01f, "The body stands level.");
            for (int i = 0; i < ours.Count; i++)
            {
                Assert.AreEqual(theirs[i].name, ours[i].name);
                Assert.AreEqual(theirs[i].localRotation, ours[i].localRotation, ours[i].name + " isn't turned.");
                Vector3 slide = ours[i].localPosition - theirs[i].localPosition;
                Assert.AreEqual(0f, slide.x, 1e-3f, ours[i].name);
                Assert.AreEqual(0f, slide.z, 1e-3f, ours[i].name);
                float metres = Mathf.Abs(slide.y) * ours[i].parent.lossyScale.y;
                Assert.Less(metres, 1.5f, ours[i].name + " stays joined to the body: the hips stand about 20 m up.");
            }
        }
    }
}
