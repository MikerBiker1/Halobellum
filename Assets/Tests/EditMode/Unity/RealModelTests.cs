// RealModelTests.cs
// The real, unanimated Scorpion model (M9, decision D44): its prefab is sized for the game, stands on the ground,
// and has its own paint.
// Depends on: Halobellum.Units, NUnit, UnityEditor, UnityEngine.

using System.Linq;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class RealModelTests
    {
        private static UnitDefinition Scorpion() => AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Scorpion.asset");

        [Test]
        public void Scorpion_UsesItsModel_WithSeparateParts_AndKeepsItsOwnMaterials()
        {
            UnitDefinition unit = Scorpion();
            Assert.IsTrue(unit.keepsOwnMaterials);
            Assert.IsNotNull(unit.prefab);
            StringAssert.Contains("_Model", unit.prefab.name);
            Assert.Greater(unit.prefab.GetComponentsInChildren<MeshRenderer>().Length, 5, "Its parts stay separate, for later.");
        }

        [Test]
        public void Scorpion_IsPainted_NotWhite()
        {
            foreach (Material material in Scorpion().prefab.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials))
            {
                Assert.IsNotNull(material);
                Assert.Less(material.color.grayscale, 0.5f, material.name + " should have its paint.");
            }
        }

        [Test]
        public void Scorpion_IsSizedForTheGame_AndStandsOnTheGround()
        {
            GameObject tank = Object.Instantiate(Scorpion().prefab);
            try
            {
                MeshRenderer[] renderers = tank.GetComponentsInChildren<MeshRenderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (MeshRenderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.AreEqual(6.2f, Mathf.Max(bounds.size.x, bounds.size.z), 0.05f);
                Assert.AreEqual(0f, bounds.min.y, 0.02f, "Its base is on the ground.");
                Assert.AreEqual(0f, bounds.center.x, 0.02f);
                Assert.AreEqual(0f, bounds.center.z, 0.02f);
                Assert.Greater(bounds.size.y, 1.5f);
            }
            finally
            {
                Object.DestroyImmediate(tank);
            }
        }
    }
}
