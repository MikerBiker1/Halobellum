// RealModelTests.cs
// The real models that aren't baked for crowds (M9, decisions D44 and D45): each prefab is sized for the game, stands on the
// ground (or is centred on its flying height), has its own materials, and isn't too heavy to draw.
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
        private static UnitDefinition Wraith() => AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Covenant/Wraith.asset");

        private static readonly string[] RealModels =
        {
            "UNSC/Scorpion", "UNSC/Elephant", "UNSC/Warthog", "UNSC/GaussWarthog", "UNSC/Hornet", "UNSC/Pelican",
            "Covenant/Banshee", "Covenant/Wraith", "Covenant/Scarab",
            "Flood/InfectedPelican", "Flood/InfectedPhantom", "Flood/InfectionForms", "Flood/CarrierForm", "Flood/StalkerForm", "Flood/PureFormRanged", "Flood/PureFormTank",
            "ArmoredDivision/Grizzly", "Banished/Chopper", "AerospaceCommand/CondorGunship",
            "FloodUNSC/InfestedWarthog", "FloodUNSC/InfestedScorpion", "FloodUNSC/InfestedHornet", "FloodUNSC/InfestedElephant",
            "FloodCovenant/InfestedWraith", "FloodCovenant/InfestedBanshee", "FloodCovenant/InfestedScarab",
        };

        [TestCaseSource(nameof(RealModels))]
        public void RealModel_IsSound(string name)
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{name}.asset");
            Assert.IsTrue(unit.keepsOwnMaterials, "It keeps its own materials.");
            StringAssert.EndsWith("_Model", unit.prefab.name);
            GameObject model = Object.Instantiate(unit.prefab);
            try
            {
                MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (MeshRenderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, renderer.name + " has lost a material.");
                        Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    }
                }
                Assert.AreEqual(0f, bounds.center.x, 0.02f);
                Assert.AreEqual(0f, bounds.center.z, 0.02f);
                Assert.AreEqual(0f, unit.isAir ? bounds.center.y : bounds.min.y, 0.02f, unit.isAir ? "Centred on its flying height." : "On the ground.");
                Assert.Less(model.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3), 20000);
                Assert.Greater(bounds.size.y, 0.5f, "It stands upright.");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void InfectedPelican_HasTheSameModelAsThePelican_InASickColour()
        {
            var clean = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Pelican.asset");
            var sick = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Flood/InfectedPelican.asset");
            Color a = clean.prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial.color;
            Color b = sick.prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial.color;
            Assert.AreEqual(1f, a.r, 0.01f, "The Pelican's texture shows as it is.");
            Assert.Less(b.r, a.r, "The infected one has a colour over it.");
        }

        [Test]
        public void Wraith_FacesForward_WithItsMuzzleAheadOfItsTurret_AndItsTexture()
        {
            GameObject wraith = Object.Instantiate(Wraith().prefab);
            try
            {
                Transform[] nodes = wraith.GetComponentsInChildren<Transform>();
                Transform muzzle = nodes.Single(t => t.name == "Muzzle_0");
                Transform turret = nodes.Single(t => t.name == "Turret_0");
                Assert.AreEqual(turret, muzzle.parent, "The muzzle turns with its turret.");
                Assert.Greater(muzzle.position.z, turret.position.z + 0.5f, "Forward in the game is +Z.");
                Assert.Greater(muzzle.position.y, 1.5f, "It fires from the top of the hull.");
                Assert.AreEqual(0f, Vector3.Angle(Vector3.up, turret.up), 1f, "A turret turns around the vertical.");
                Material hull = wraith.GetComponentsInChildren<MeshRenderer>().First(r => !r.name.StartsWith("Turret")).sharedMaterial;
                Assert.IsNotNull(hull.GetTexture("_BaseMap"));
                Assert.AreEqual(0.15f, hull.GetFloat("_Smoothness"), 0.01f, "Matte, or it mirrors the sky.");
                Assert.Less(wraith.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3), 8100);
            }
            finally
            {
                Object.DestroyImmediate(wraith);
            }
        }

        [Test]
        public void Scorpion_HasATurretAndWheelsThatMove()
        {
            Transform[] nodes = Scorpion().prefab.GetComponentsInChildren<Transform>();
            Assert.AreEqual(1, nodes.Count(t => t.name == "Turret_0"));
            Assert.Greater(nodes.Count(t => t.name.StartsWith("Wheel_")), 3);
            Transform turret = nodes.Single(t => t.name == "Turret_0");
            Assert.IsNotNull(turret.Find("Muzzle_0"), "The cannon's muzzle turns with the turret.");
        }

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
