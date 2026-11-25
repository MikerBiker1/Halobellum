// HornetRotorTests.cs
// The Hornet's new model (D184): the first of the three Hornets in the owner's vehicleshumanfightershornet.glb, prepared by
// Tools/Art/units/Hornet.py. A rotor on each wing tip, as a Rotor_ node, which ModelParts spins all the time about the model's up axis, the
// two turning opposite ways; its rockets leave the launchers on its wing roots, ahead of the rotors.
// Depends on: Halobellum.Presentation (ModelParts), Halobellum.Units, NUnit, UnityEditor (loading the prefab).

using System.Linq;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class HornetRotorTests
    {
        private static GameObject Prefab()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Hornet.asset");
            Assert.IsNotNull(unit.prefab, "Run Halobellum > Art > Build Real Models.");
            return unit.prefab;
        }

        [Test]
        public void TheHornet_HasARotorOnEachWingTip_AndFiresFromItsWingRoots()
        {
            Transform[] nodes = Prefab().GetComponentsInChildren<Transform>(true);
            Transform left = nodes.Single(t => t.name == "Rotor_0"), right = nodes.Single(t => t.name == "Rotor_1");
            Assert.Less(left.position.x * right.position.x, 0f, "One on each side.");
            Assert.Greater(Mathf.Abs(left.position.x), 1.5f, "Out on the wing tips.");
            Assert.AreEqual(left.position.y, right.position.y, 0.05f);
            Transform[] muzzles = nodes.Where(t => t.name.StartsWith("Muzzle_0")).ToArray();
            Assert.AreEqual(2, muzzles.Length, "A launcher on each wing root.");
            foreach (Transform muzzle in muzzles)
            {
                Assert.Greater(muzzle.position.z, left.position.z, "Ahead of the rotors: forward in the game is +Z.");
                Assert.Less(Mathf.Abs(muzzle.position.x), Mathf.Abs(left.position.x), "Inside the rotors.");
            }
        }

        [Test]
        public void ItsRotors_SpinAboutTheUpAxis_TheTwoOppositeWays()
        {
            var root = new GameObject("Hornet under test");
            try
            {
                Transform body = Object.Instantiate(Prefab(), root.transform, false).transform;
                Transform[] rotors = { body.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Rotor_0"),
                                       body.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Rotor_1") };
                Quaternion[] rest = rotors.Select(r => r.localRotation).ToArray();
                Vector3[] up = rotors.Select(r => Quaternion.Inverse(r.rotation) * root.transform.up).ToArray();
                ModelParts parts = ModelParts.Find(root.transform, body, true, 0f);
                Assert.IsNotNull(parts);

                const float time = 0.1f;
                parts.Animate(null, 1f / 30f, time);
                float[] turned = new float[2];
                for (int i = 0; i < 2; i++)
                {
                    (Quaternion.Inverse(rest[i]) * rotors[i].localRotation).ToAngleAxis(out float angle, out Vector3 axis);
                    Assert.AreEqual(ModelParts.RotorDegreesPerSecond * time, angle, 1f, $"Rotor_{i} turns at its pace.");
                    Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(axis.normalized, up[i].normalized)), 0.01f, $"Rotor_{i} turns about the model's up.");
                    turned[i] = Mathf.Sign(Vector3.Dot(axis, up[i]));
                }
                Assert.AreEqual(-turned[0], turned[1], "The two turn opposite ways.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
