// ScarabWalkTests.cs
// The Scarab's walk (D84): its real prefab, walked by WalkerLegs as the battle walks it. A planted foot stays where it was put while the body
// moves over it, one foot is in the air at a time, the cycle keeps pace with the ground covered at any speed, the body sways and dips with the
// steps, a stop finishes the step in the air and the feet settle, and it steps around when turning on the spot. Its weapons' muzzles are where
// they should be for its shots.
// Depends on: Halobellum.Presentation (ModelParts, WalkerLegs), Halobellum.Units, NUnit, UnityEditor (loading the prefab).

using System.Collections.Generic;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class ScarabWalkTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Units/Scarab_Model.prefab";
        private const float Speed = 0.75f; // The Scarab's own move speed, m/s.
        private const float Frame = 1f / 30f;

        private GameObject _root;
        private Transform _body;
        private WalkerLegs _walker;
        private float _time;

        [SetUp]
        public void PlaceTheScarab()
        {
            _root = new GameObject("Scarab under test");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);
            _body = Object.Instantiate(prefab, _root.transform, false).transform;
            _body.name = "Body";
            ModelParts parts = ModelParts.Find(_root.transform, _body, false, 0f);
            Assert.IsNotNull(parts);
            _walker = parts.Walker;
            Assert.IsNotNull(_walker, "The Scarab's model has a walker's legs.");
            _time = 0f;
            _walker.Hold(_time);
        }

        [TearDown]
        public void Remove() => Object.DestroyImmediate(_root);

        /// <summary>One frame: the unit moves and turns this much, then its legs follow, as UnitViewManager and ModelParts do in battle.</summary>
        private void Step(Vector3 move, float turnDegrees = 0f)
        {
            _root.transform.position += move;
            _root.transform.rotation = Quaternion.Euler(0f, turnDegrees, 0f) * _root.transform.rotation;
            _time += Frame;
            _walker.Animate(Frame, _time);
        }

        [Test]
        public void ItHasFourLegs_AndStandsOnThem()
        {
            Assert.AreEqual(4, _walker.LegCount);
            for (int i = 0; i < 4; i++)
            {
                Vector3 tip = _walker.FootTip(i);
                Assert.AreEqual(0f, tip.y, 0.02f, $"Leg {i}'s foot is on the ground.");
                Assert.Less(Vector3.Distance(tip, _walker.FootTarget(i)), 0.02f, $"Leg {i} reaches its foot.");
                Assert.Less(_walker.Stretch(i), 0.95f, $"Leg {i} has room to stretch as it stands.");
            }
        }

        [Test]
        public void WalkingAtItsSpeed_NoPlantedFootSlides_AndEachLegSteps()
        {
            var plantedAt = new Vector3?[4];
            var steps = new int[4];
            var wasPlanted = new bool[4];
            for (int i = 0; i < 4; i++) wasPlanted[i] = true;
            float mostStretch = 0f;
            for (int frame = 0; frame < 30 * 16; frame++)
            {
                Step(_root.transform.forward * (Speed * Frame));
                int inAir = 0;
                for (int i = 0; i < 4; i++)
                {
                    bool planted = _walker.IsPlanted(i);
                    Vector3 tip = _walker.FootTip(i);
                    if (planted)
                    {
                        Assert.Less(Vector3.Distance(tip, _walker.FootTarget(i)), 0.01f, $"Leg {i} reaches its planted foot (frame {frame}).");
                        Assert.AreEqual(0f, tip.y, 0.01f, $"Leg {i}'s planted foot is on the ground (frame {frame}).");
                        if (wasPlanted[i] && plantedAt[i].HasValue)
                            Assert.Less(Vector3.Distance(tip, plantedAt[i].Value), 0.01f, $"Leg {i}'s planted foot doesn't slide (frame {frame}).");
                        if (!wasPlanted[i] || !plantedAt[i].HasValue) plantedAt[i] = tip;
                        mostStretch = Mathf.Max(mostStretch, _walker.Stretch(i));
                    }
                    else
                    {
                        inAir++;
                        if (wasPlanted[i]) steps[i]++;
                    }
                    wasPlanted[i] = planted;
                }
                Assert.LessOrEqual(inAir, 1, $"One foot in the air at a time (frame {frame}).");
            }
            float cycles = Speed * 16f / _walker.Stride;
            for (int i = 0; i < 4; i++) Assert.AreEqual(cycles, steps[i], 1.01f, $"Leg {i} steps once a cycle.");
            Assert.Less(mostStretch, 0.98f, "No planted leg is stretched straight.");
        }

        [Test]
        public void TheCycle_KeepsPaceWithTheGround_AtAnySpeed()
        {
            int Count(float speed, float metres)
            {
                PlaceAgain();
                int steps = 0;
                bool was = true;
                for (float covered = 0f; covered < metres; covered += speed * Frame)
                {
                    Step(_root.transform.forward * (speed * Frame));
                    bool planted = _walker.IsPlanted(0);
                    if (was && !planted) steps++;
                    was = planted;
                }
                return steps;
            }
            int slow = Count(Speed * 0.6f, 15f), usual = Count(Speed, 15f), fast = Count(Speed * 1.5f, 15f);
            Assert.AreEqual(usual, slow, 1, "Slower, it steps as often per metre.");
            Assert.AreEqual(usual, fast, 1, "Faster, it steps as often per metre.");
            Assert.AreEqual(15f / _walker.Stride, usual, 1.01f);
        }

        private void PlaceAgain()
        {
            Object.DestroyImmediate(_root);
            PlaceTheScarab();
        }

        [Test]
        public void TheBody_SwaysAndDips_WithTheSteps()
        {
            float restY = 0f;
            float leftMost = 0f, rightMost = 0f, lowest = float.MaxValue, highest = float.MinValue;
            for (int frame = 0; frame < 30 * 12; frame++)
            {
                Step(_root.transform.forward * (Speed * Frame));
                if (frame == 0) restY = _body.localPosition.y;
                if (frame < 30 * 3) continue; // Under way.
                Vector3 at = _body.localPosition;
                leftMost = Mathf.Min(leftMost, at.x);
                rightMost = Mathf.Max(rightMost, at.x);
                lowest = Mathf.Min(lowest, at.y);
                highest = Mathf.Max(highest, at.y);
            }
            Assert.Less(leftMost, -0.12f, "It leans its weight to the left.");
            Assert.Greater(rightMost, 0.12f, "And to the right.");
            Assert.Greater(highest - lowest, 0.04f, "It dips as a foot lifts.");
            Assert.Less(highest - lowest, 0.5f, "Only a little.");
            Assert.Less(restY, -0.3f, "It stands a little crouched, so its legs have room.");
        }

        [Test]
        public void Stopping_FinishesTheStep_AndTheFeetSettle()
        {
            for (int frame = 0; frame < 30 * 7; frame++) Step(_root.transform.forward * (Speed * Frame));
            for (int frame = 0; frame < 30 * 8; frame++) Step(Vector3.zero);
            var feet = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(_walker.IsPlanted(i), $"Leg {i} stands.");
                feet[i] = _walker.FootTip(i);
            }
            Vector3 body = _body.localPosition;
            for (int frame = 0; frame < 30 * 2; frame++) Step(Vector3.zero);
            for (int i = 0; i < 4; i++) Assert.Less(Vector3.Distance(feet[i], _walker.FootTip(i)), 0.01f, $"Leg {i} stays still.");
            Assert.AreEqual(0f, body.x, 0.01f, "Standing, it doesn't lean.");
            Assert.Less(Vector3.Distance(body, _body.localPosition), 0.05f, "Standing, it only breathes.");
        }

        [Test]
        public void TurningOnTheSpot_ItStepsAround()
        {
            int steps = 0;
            bool was = true;
            for (int frame = 0; frame < 30 * 3; frame++)
            {
                Step(Vector3.zero, 60f * Frame); // Its full turning speed: 180 degrees in 3 s.
                bool planted = _walker.IsPlanted(1);
                if (was && !planted) steps++;
                was = planted;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 tip = _walker.FootTip(i);
                    Assert.IsFalse(float.IsNaN(tip.x) || float.IsNaN(tip.y), "The legs stay whole.");
                    if (_walker.IsPlanted(i)) Assert.AreEqual(0f, tip.y, 0.02f, $"Leg {i}'s planted foot stays on the ground.");
                }
            }
            Assert.Greater(steps, 0, "It steps as it turns.");
        }

        [Test]
        public void ItsMuzzles_AreWhereItsWeaponsFire()
        {
            var nodes = new Dictionary<string, Transform>();
            foreach (Transform node in _body.GetComponentsInChildren<Transform>(true)) nodes[node.name] = node;
            Assert.IsTrue(nodes.ContainsKey("Muzzle_0") && nodes.ContainsKey("Muzzle_1") && nodes.ContainsKey("Muzzle_2") && nodes.ContainsKey("Turret_1"));
            Vector3 beam = _root.transform.InverseTransformPoint(nodes["Muzzle_0"].position);
            Assert.Greater(beam.z, 5f, "The focus beam leaves the front.");
            Assert.AreEqual(0f, beam.x, 0.05f);
            Assert.IsTrue(nodes["Muzzle_1"].IsChildOf(nodes["Turret_1"]) && nodes["Muzzle_2"].IsChildOf(nodes["Turret_1"]), "The plasma guns turn with the cap.");
            Vector3 left = _root.transform.InverseTransformPoint(nodes["Muzzle_1"].position), right = _root.transform.InverseTransformPoint(nodes["Muzzle_2"].position);
            Assert.Less(left.x, -0.5f, "The left plasma gun is on its left.");
            Assert.Greater(right.x, 0.5f, "The right one on its right.");
            Assert.Greater(left.y, 9f, "Up on the mast.");
        }
    }
}
