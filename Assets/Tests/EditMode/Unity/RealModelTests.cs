// RealModelTests.cs
// The real models that aren't baked for crowds (M9, decisions D44 and D45): each prefab is sized for the game, stands on the
// ground (or is centred on its flying height), has its own materials, and isn't too heavy to draw. The Flood's growths on an infested
// vehicle (FloodGrowths) are left out of where it stands; they are tested on their own: there, on the surface, and only on infested ones.
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
            "FloodUNSC/InfestedWarthog", "FloodUNSC/InfestedScorpion", "FloodUNSC/InfestedHornet",
            "FloodCovenant/InfestedWraith", "FloodCovenant/InfestedBanshee",
            "UNSC/Mantis", "UNSC/Wolverine", "Banished/Marauder", "Banished/Reaver", "Covenant/Vampire", "Flood/CombatForm",
            "FloodCovenant/Spawner", "FloodUNSC/Juggernaut", "LekgoloColony/Goliath",
            "ArmoredDivision/Gremlin", "FloodCovenant/InfestedElite",
        };

        /// <summary>What sits on a model besides it: the Flood's growths (D37) and a held weapon (D82), left out of its size and its materials' check.</summary>
        private static bool IsExtra(Renderer renderer) => renderer.name == "Growth" || renderer.GetComponentsInParent<Transform>(true).Any(t => t.name.StartsWith("Weapon_"));

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
                Bounds bounds = renderers.First(r => !IsExtra(r)).bounds;
                foreach (MeshRenderer renderer in renderers)
                {
                    if (IsExtra(renderer)) continue;
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

        private static readonly string[] Infested =
        {
            "FloodUNSC/InfestedWarthog", "FloodUNSC/InfestedScorpion", "FloodUNSC/InfestedHornet", "FloodCovenant/InfestedWraith",
            "FloodCovenant/InfestedBanshee", "FloodCovenant/InfestedGhost", "Flood/InfectedPelican", "Flood/InfectedPhantom",
        };

        [TestCaseSource(nameof(Infested))]
        public void InfestedVehicle_HasGrowths_OnItsSurface(string name)
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{name}.asset");
            GameObject model = Object.Instantiate(unit.prefab);
            try
            {
                MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();
                MeshRenderer[] growths = renderers.Where(r => r.name == "Growth").ToArray();
                MeshRenderer[] body = renderers.Where(r => r.name != "Growth").ToArray();
                Assert.GreaterOrEqual(growths.Length, 4, "A few clumps of growths.");
                Assert.LessOrEqual(growths.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3), 2500, "Modest.");
                Bounds hull = body[0].bounds;
                foreach (MeshRenderer renderer in body) hull.Encapsulate(renderer.bounds);
                foreach (MeshRenderer growth in growths)
                {
                    Assert.IsTrue(hull.Contains(growth.transform.position), $"{growth.transform.position} stands on the vehicle, inside {hull}.");
                    Assert.IsFalse(growth.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("Wheel_")), "Not on a wheel.");
                    Assert.AreEqual("Universal Render Pipeline/Lit", growth.sharedMaterial.shader.name);
                    StringAssert.StartsWith("Growths_", growth.sharedMaterial.name);
                }
                Assert.Less(Mathf.Max(growths.Max(g => g.bounds.size.x), growths.Max(g => g.bounds.size.z)), Mathf.Max(hull.size.x, hull.size.z) * 0.25f, "Small against the vehicle.");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void CleanVehicles_HaveNoGrowths()
        {
            foreach (string name in new[] { "UNSC/Warthog", "UNSC/Scorpion", "Covenant/Wraith", "UNSC/Pelican" })
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{name}.asset");
                Assert.IsFalse(unit.prefab.GetComponentsInChildren<Transform>().Any(t => t.name == "Growth"), name);
            }
        }

        [Test]
        public void Seeder_Flies_AndStandsOnItsMiddle()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Flood/Seeder.asset");
            Assert.IsTrue(unit.isAir);
            GameObject model = Object.Instantiate(unit.prefab);
            try
            {
                MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (MeshRenderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.AreEqual(0f, bounds.center.y, 0.02f);
                Assert.AreEqual(8f, Mathf.Max(bounds.size.x, bounds.size.z), 0.05f);
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void Condor_HasItsOwnModel_NotAGreyPelican()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/AerospaceCommand/CondorGunship.asset");
            Material material = unit.prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.AreEqual("Condor_0", material.name);
            Assert.AreEqual(1f, material.color.r, 0.01f, "It shows its texture as it is.");
            Assert.IsNotNull(unit.prefab.GetComponentsInChildren<Transform>().Single(t => t.name == "Turret_0").Find("Muzzle_0a"));
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
                Assert.Less(wraith.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponentsInParent<Transform>(true).All(t => !t.name.StartsWith("Driver_"))).Sum(f => f.sharedMesh.triangles.Length / 3), 8100); // The Wraith itself, not the Elite in its hatch (D103).
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
        public void Scorpion_ShowsItsOwnPaintedTextures_AndTheInfestedOneHasTheSameModelInFloodTendrils()
        {
            // The owner's updated model (M12) comes painted: its textures show as they are. The Infested Scorpion wears their Flood copies, beige
            // tendrils over the paint (D82; it was washed green before).
            var sick = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/FloodUNSC/InfestedScorpion.asset");
            Material[] clean = Scorpion().prefab.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            // The Flood's growths (D37) sit on the Infested one and are left out here.
            Material[] tinted = sick.prefab.GetComponentsInChildren<MeshRenderer>().Where(r => r.name != "Growth").SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            Assert.AreEqual(3, clean.Length, "Hull, turret and pods.");
            foreach (Material material in clean)
            {
                Assert.IsNotNull(material.GetTexture("_BaseMap"), material.name + " should have its paint.");
                Assert.AreEqual(1f, material.color.r, 0.01f, material.name + " shows its texture as it is.");
            }
            Assert.AreEqual(clean.Length, tinted.Length);
            foreach (Material material in tinted)
            {
                Assert.IsNotNull(material.GetTexture("_BaseMap"));
                StringAssert.EndsWith("_Flood", material.GetTexture("_BaseMap").name, material.name + " wears the texture's Flood copy.");
                Assert.Greater(material.color.g, 0.85f, material.name + " is no longer washed green.");
                Assert.Greater(material.color.b, 0.8f);
            }
            Assert.AreEqual(Scorpion().prefab.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length),
                sick.prefab.GetComponentsInChildren<MeshFilter>().Where(f => f.name != "Growth").Sum(f => f.sharedMesh.triangles.Length), "The same model.");
        }

        [TestCase("Covenant/Banshee")]
        [TestCase("FloodCovenant/InfestedBanshee")]
        [TestCase("Covenant/Ghost")]
        [TestCase("FloodCovenant/InfestedGhost")]
        public void BansheeAndGhost_HaveAThrusterAtTheBackOfEachSide(string name)
        {
            // The owner, 2026-11-07 (D82): the Banshee's wing tips and the Ghost's fins glow while they move (ModelParts lights them).
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{name}.asset");
            Transform[] thrusters = unit.prefab.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Thruster_")).ToArray();
            Assert.AreEqual(2, thrusters.Length);
            Bounds bounds = unit.prefab.GetComponentsInChildren<MeshRenderer>().Where(r => !IsExtra(r)).Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Assert.Less(thrusters[0].position.x * thrusters[1].position.x, 0f, "One on each side.");
            foreach (Transform thruster in thrusters)
            {
                Assert.Greater(Mathf.Abs(thruster.position.x), bounds.extents.x * 0.7f, "Out at the side.");
                Assert.Less(thruster.position.z, bounds.max.z - 1f, "Behind the front: at the back of its wing tip or fin.");
                Assert.IsTrue(float.TryParse(thruster.name.Substring("Thruster_".Length), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float size));
                Assert.That(size, Is.InRange(0.35f, 0.8f));
            }
        }

        [Test]
        public void InfestedElite_IsTheCombatForm_HoldingAPlasmaRifle_AndShootsFromItsTip()
        {
            // The owner, 2026-11-07 (D82): the Infested Elite is the Combat Form; it was the Elite's body washed green. And it holds its plasma rifle.
            // Since D92 both are animated from the Combat Form's rigged model, the Elite's with the rifle merged in; the static prefab stays as a fallback.
            var elite = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/FloodCovenant/InfestedElite.asset");
            var form = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Flood/CombatForm.asset");
            Assert.AreEqual("CombatFormAnimated", elite.bakedBody.modelFolder, "Not the Elite's body: the Combat Form's.");
            Assert.AreEqual(form.bakedBody.modelFolder, elite.bakedBody.modelFolder);
            Assert.AreEqual("Weapon_PlasmaRifle", elite.bakedBody.weaponName);
            Assert.IsTrue(elite.keepsOwnMaterials);
            Assert.AreEqual(form.prefab.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length),
                elite.prefab.GetComponentsInChildren<MeshFilter>().Where(f => !IsExtra(f.GetComponent<MeshRenderer>())).Sum(f => f.sharedMesh.triangles.Length), "The Combat Form's model.");
            MeshRenderer rifle = elite.prefab.GetComponentsInChildren<MeshRenderer>().Single(IsExtra);
            Transform muzzle = elite.prefab.GetComponentsInChildren<Transform>().Single(t => t.name == "Muzzle_1");
            Assert.AreEqual(0.85f, rifle.bounds.size.z, 0.15f, "A plasma rifle's length, pointing forward.");
            Assert.AreEqual(rifle.bounds.max.z, muzzle.position.z, 0.15f, "Its shots leave the rifle's tip.");
            Assert.Less(Vector3.Distance(rifle.bounds.center, muzzle.position), 0.6f);
        }

        [Test]
        public void Glow_IsOnForTheModels_ButNotTheInfestedOnes()
        {
            // The owner, 2026-11-07 (D82): glow on for every model that has one (it was only the towers'); the infested vehicles stay unlit.
            Material Body(string name) => AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{name}.asset").prefab
                .GetComponentsInChildren<MeshRenderer>().Where(r => !IsExtra(r)).SelectMany(r => r.sharedMaterials).First(m => m.GetTexture("_EmissionMap") != null);
            Assert.IsTrue(Body("Covenant/Banshee").IsKeywordEnabled("_EMISSION"));
            Assert.IsTrue(Body("Covenant/Wraith").IsKeywordEnabled("_EMISSION"));
            Assert.IsTrue(Body("UNSC/Hornet").IsKeywordEnabled("_EMISSION"));
            Assert.IsFalse(Body("FloodCovenant/InfestedBanshee").IsKeywordEnabled("_EMISSION"));
        }

        [Test]
        public void Scorpion_FiresBothGunsFromItsTurret()
        {
            Transform turret = Scorpion().prefab.GetComponentsInChildren<Transform>().Single(t => t.name == "Turret_0");
            Transform cannon = turret.Find("Muzzle_0"), coaxial = turret.Find("Muzzle_1");
            Assert.IsNotNull(cannon);
            Assert.IsNotNull(coaxial, "The coaxial gun's muzzle turns with the turret.");
            Assert.Greater(cannon.position.z, coaxial.position.z, "The cannon reaches further forward.");
            Assert.Greater(cannon.position.z, 2f, "Forward in the game is +Z.");
        }

        [Test]
        public void Gremlin_HasItsOwnModel_WithATurningRailgunAndSixWheels()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/ArmoredDivision/Gremlin.asset");
            StringAssert.EndsWith("_Model", unit.prefab.name);
            Transform[] nodes = unit.prefab.GetComponentsInChildren<Transform>();
            Transform turret = nodes.Single(t => t.name == "Turret_0");
            Transform muzzle = turret.Find("Muzzle_0");
            Assert.IsNotNull(muzzle, "The railgun's muzzle turns with it.");
            Assert.Greater(muzzle.position.z, turret.position.z, "It points forward, +Z in the game.");
            Assert.AreEqual(6, nodes.Count(t => t.name.StartsWith("Wheel_")));
            Assert.IsNotNull(nodes.First(t => t.name.StartsWith("Body")).GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"));
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
