// VehicleCrewTests.cs
// The owner's vehicle round of 2026-11-07. The Mantis as an animated body (D97), rigged in Blender by Tools/Art/units/MantisAnimated.py: its
// clips, the firing clip twelve rounds of its chaingun long, the chaingun's shots leaving its right arm and the rockets' its left (a second
// muzzle, which a baked body could not have before). The Scarab's mouth (D98): a green light inside it, off until a beam lights it, and the
// two places either side that glow with it; and the beam's coils and swirls drawn as strands, which EffectKit can now draw. The drivers
// (D99): a soldier in every open seat, the right one for the side (a Marine, a Grunt, a Brute, a Combat Form in an infested vehicle), a
// gunner turning with its gun, and none in a closed vehicle.
// Depends on: Halobellum.Units, Halobellum.Core, Halobellum.Presentation (EffectKit, BeamDrawing), NUnit, UnityEditor (loading the assets).

using System.Linq;
using Halobellum.Core;
using Halobellum.Presentation;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class VehicleCrewTests
    {
        private static GameObject Prefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Units/{name}_Model.prefab");
            Assert.IsNotNull(prefab, name);
            return prefab;
        }

        private static string[] DriversOf(GameObject prefab) =>
            prefab.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Driver_")).Select(t => t.name.Substring("Driver_".Length)).OrderBy(n => n).ToArray();

        [Test]
        public void TheMantis_IsAnAnimatedBody_FiringTwelveRoundsAClip_FromBothArms()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Mantis.asset");
            var body = Resources.Load<BakedBodyDefinition>("BakedBodies/Mantis");
            Assert.IsNotNull(body);
            Assert.AreSame(body, unit.bakedBody, "Its data points at the body.");
            Assert.IsTrue(body.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            foreach (BakedAnimation role in new[] { BakedAnimation.Still, BakedAnimation.Moving, BakedAnimation.Shooting, BakedAnimation.Dead })
                Assert.IsNotNull(body.Clip(role), $"Its {role} clip.");
            Assert.IsFalse(body.Clip(BakedAnimation.Dead).loops);
            Assert.AreEqual(6f, body.bodyHeight, 0.2f, "As tall as the Mantis is in the game.");

            UnitStats stats = unit.ToStats();
            float chaingun = stats.Weapons[0].FireInterval;
            float firing = body.Clip(BakedAnimation.Shooting).frames / (float)body.bakedFps;
            Assert.AreEqual(12f * chaingun, firing, 1.5f / body.bakedFps, "Twelve rounds of the chaingun a clip.");
            Assert.That(body.shotMoment, Is.InRange(0f, 1f / 12f), "Timed by its first round.");
            Assert.AreEqual(stats.MoveSpeed, 1.5f, 0.01f, "The walk was made for this speed.");

            Assert.IsTrue(body.hasMuzzle && body.hasSecondMuzzle, "A muzzle for each weapon.");
            Assert.Greater(body.muzzle.x, 1f, "The chaingun is on its right arm.");
            Assert.Less(body.secondMuzzle.x, -1f, "The rockets are on its left.");
            Assert.Greater(body.muzzle.z, 1.5f, "Aimed ahead as it fires.");
            Assert.That(body.muzzle.y, Is.InRange(2.5f, 4.5f), "At its shoulders' height, not its feet.");
        }

        [Test]
        public void TheScarab_HasAGreenMouthGlow_OffUntilItsBeamLightsIt()
        {
            GameObject scarab = Prefab("Scarab");
            Light light = scarab.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.name == "MouthGlow");
            Assert.IsNotNull(light, "A light inside its mouth.");
            Assert.IsFalse(light.enabled, "Dark until the beam charges.");
            Assert.AreEqual(LightType.Point, light.type);
            Assert.Greater(light.color.g, light.color.r * 1.5f, "Green.");
            Transform muzzle = scarab.GetComponentsInChildren<Transform>(true).First(t => t.name == "Muzzle_0");
            Assert.Less(Vector3.Distance(light.transform.position, muzzle.position), 2f, "In the mouth the beam leaves.");
            Transform[] sides = scarab.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Mandible_")).ToArray();
            Assert.AreEqual(2, sides.Length, "Two glowing sides.");
            Assert.Less(sides[0].localPosition.x * sides[1].localPosition.x, 0f, "One either side of the mouth.");
        }

        [Test]
        public void TheBeam_DrawsItsCoilsAsStrands_AndEverythingGoesNextFrame()
        {
            var holder = new GameObject("Effects");
            var kit = new EffectKit(holder.transform);
            try
            {
                float emit = 0f;
                Vector3 scorch = Vector3.zero;
                var mandibles = new[] { new Vector3(-1f, 3f, 5f), new Vector3(1f, 3f, 5f) };
                kit.BeginFrame(1f, 0.02f, 0.02f);
                BeamDrawing.Charge(kit, new Vector3(0f, 4f, 6f), new Vector3(0f, 0f, 40f), 0.6f, 2f, Color.cyan, 1f, 1.3f, 0.02f, ref emit, mandibles);
                kit.EndFrame();
                Assert.AreEqual(4, kit.StrandsShown, "Three coils swirling in and a ring closing round the mouth.");
                kit.BeginFrame(1f, 0.02f, 0.02f);
                BeamDrawing.Fire(kit, new Vector3(0f, 4f, 6f), new Vector3(0f, 0f, 40f), 2f, 1.6f, Color.cyan, 1f, 1.4f, 0.02f, ref emit, ref scorch, mandibles);
                kit.EndFrame();
                Assert.AreEqual(3, kit.StrandsShown, "Three coils winding round the beam.");
                kit.BeginFrame(1f, 0.02f, 0.02f);
                kit.EndFrame();
                Assert.AreEqual(0, kit.StrandsShown, "Nothing stays once the beam is gone.");
            }
            finally
            {
                Object.DestroyImmediate(holder); // EffectKit.Destroy is for a game: in the editor its materials simply go with the test run.
            }
        }

        [Test]
        public void EveryOpenSeat_HasItsSoldier_AndAClosedVehicleNone()
        {
            CollectionAssert.AreEqual(new[] { "Marine_Drive", "Marine_Gun" }, DriversOf(Prefab("Warthog")));
            CollectionAssert.AreEqual(new[] { "Marine_Drive", "Marine_Gun" }, DriversOf(Prefab("GaussWarthog")));
            CollectionAssert.AreEqual(new[] { "CombatForm_Drive", "CombatForm_Gun" }, DriversOf(Prefab("InfestedWarthog")), "The Flood drives its own.");
            CollectionAssert.AreEqual(new[] { "Grunt_Ride" }, DriversOf(Prefab("Ghost")));
            CollectionAssert.AreEqual(new[] { "CombatForm_Ride" }, DriversOf(Prefab("InfestedGhost")));
            CollectionAssert.AreEqual(new[] { "Brute_Drive" }, DriversOf(Prefab("Marauder")), "The Banished's exclusive, a Brute's.");
            CollectionAssert.AreEqual(new[] { "Brute_Ride" }, DriversOf(Prefab("Chopper")));
            CollectionAssert.AreEqual(new[] { "Marine_Gun" }, DriversOf(Prefab("Elephant")));
            foreach (string closed in new[] { "Scorpion", "Wraith", "Banshee", "Hornet", "Pelican", "Grizzly", "Cobra", "Rhino" })
                Assert.IsEmpty(DriversOf(Prefab(closed)), closed + " shows no seat.");
        }

        [Test]
        public void AGunner_TurnsWithItsGun_AndADriverSitsInTheVehicle()
        {
            GameObject warthog = Prefab("Warthog");
            Transform gunner = warthog.GetComponentsInChildren<Transform>(true).First(t => t.name == "Driver_Marine_Gun");
            Assert.AreEqual("Turret_0", gunner.parent.name, "It turns with the gun.");
            Transform driver = warthog.GetComponentsInChildren<Transform>(true).First(t => t.name == "Driver_Marine_Drive");
            Assert.AreSame(warthog.transform, driver.parent);
            Assert.Less(driver.localPosition.x, 0f, "The driver sits on its left.");
            Assert.That(driver.localPosition.y, Is.InRange(0.6f, 1.6f), "In its seat, not under the floor or on the roof.");
            foreach (MeshRenderer renderer in warthog.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Driver_") || r.transform.parent.name.StartsWith("Driver_")))
            {
                Assert.IsNotNull(renderer.sharedMaterial.GetTexture("_BaseMap"), "In its own colours.");
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, "As the vehicle, no shadow.");
            }
        }
    }
}
