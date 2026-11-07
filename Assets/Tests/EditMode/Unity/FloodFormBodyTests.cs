// FloodFormBodyTests.cs
// The Spawner, the Pure Form Ranged and the Stalker Form as animated bodies (D90), rigged in Blender by Tools/Art/creature_rig.py: each unit
// has its baked body with its clips, each clip as long as the battle beat it keeps time with (the Stalker's three blows to three strikes of
// its Talons, the Ranged Form's shot to its reload, the Spawner's walk and lash to its spawns), the fronds cut out, the Ranged Form's shard
// leaving from the top of its column at the end of its thrust, and every one of the Spawner's tendrils with bones of its own.
// Depends on: Halobellum.Units, Halobellum.Core, NUnit, UnityEditor (loading the assets).

using System.Linq;
using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class FloodFormBodyTests
    {
        private static (UnitDefinition Unit, BakedBodyDefinition Body) Load(string unit, string body)
        {
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{unit}.asset");
            var baked = Resources.Load<BakedBodyDefinition>("BakedBodies/" + body);
            Assert.IsNotNull(baked, body);
            Assert.AreSame(baked, definition.bakedBody, "Its data points at the body.");
            Assert.IsTrue(baked.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            foreach (BakedAnimation role in new[] { BakedAnimation.Still, BakedAnimation.Moving, BakedAnimation.Shooting, BakedAnimation.Dead })
                Assert.IsNotNull(baked.Clip(role), $"{body} has its {role} clip.");
            Assert.IsFalse(baked.Clip(BakedAnimation.Dead).loops);
            return (definition, baked);
        }

        private static float Seconds(BakedBodyDefinition body, BakedAnimation role) => body.Clip(role).frames / (float)body.bakedFps;

        [Test]
        public void TheStalkerForm_LungesThreeTimes_OneOnEachStrike()
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load("Flood/StalkerForm", "StalkerForm");
            UnitStats stats = unit.ToStats();
            Assert.AreEqual(DamageType.Melee, stats.Weapons[0].DamageType);
            Assert.AreEqual(3f * stats.Weapons[0].FireInterval, Seconds(body, BakedAnimation.Shooting), 0.02f, "Three blows, one a strike.");
            Assert.That(body.shotMoment, Is.InRange(0f, 1f / 3f), "Its first blow lands in the first third.");
            Assert.AreEqual(60, body.bakedFps, "It runs at 5 m/s: 30 frames a second would make its feet jump.");
            Assert.AreEqual(stats.MoveSpeed * Seconds(body, BakedAnimation.Moving) * 0.3f, 0.6f, 0.05f, "A stride's planted part as long as the clip says.");
            Assert.IsTrue(body.cutout, "Its fronds are see-through.");
            Assert.AreEqual(1f, body.material.GetFloat("_Cutout"));
        }

        [Test]
        public void ThePureFormRanged_ShootsFromItsColumn_AtTheEndOfItsThrust()
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load("Flood/PureFormRanged", "PureFormRanged");
            UnitStats stats = unit.ToStats();
            Assert.AreEqual(stats.Weapons[0].FireInterval, Seconds(body, BakedAnimation.Shooting), 0.04f, "One shot to the clip.");
            Assert.That(body.shotMoment, Is.InRange(0.5f, 0.7f), "After the recoil, at the end of the thrust.");
            Assert.AreEqual("Muzzle", body.muzzleBone);
            Assert.IsTrue(body.hasMuzzle);
            Assert.Greater(body.muzzle.y, 1f, "High on its column.");
            Assert.Greater(body.muzzle.z, 0.3f, "Thrown forward.");
            Assert.IsTrue(body.cutout);
            Assert.AreEqual(1f, body.material.GetFloat("_Cutout"));
        }

        [Test]
        public void TheSpawner_GivesBirthAsItSpawns_EachTendrilWithItsOwnBones()
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load("FloodCovenant/Spawner", "Spawner");
            UnitStats stats = unit.ToStats();
            Assert.Greater(stats.SpawnIntervalSeconds, 0f);
            Assert.That(body.spawnMoment, Is.InRange(0f, 1f));
            Assert.AreEqual(stats.SpawnIntervalSeconds, Seconds(body, BakedAnimation.Moving), 0.04f, "Its walk is one spawn long.");
            Assert.AreEqual(stats.SpawnIntervalSeconds, Seconds(body, BakedAnimation.Shooting), 0.04f, "So is its lash.");
            Assert.IsFalse(body.cutout);
            Assert.AreEqual(0f, body.material.GetFloat("_Cutout"));
            Transform[] rig = body.sourceModel.GetComponentsInChildren<Transform>();
            int tendrils = rig.Count(t => t.name.StartsWith("Tendril_") && t.name.EndsWith("_0"));
            Assert.GreaterOrEqual(tendrils, 45, "Nearly all of its 54 tendrils have a chain of their own.");
            Assert.IsTrue(rig.Any(t => t.name == "Belly_0") && rig.Any(t => t.name == "Belly_1"), "Its belly has bones to heave with.");
        }

        [TestCase("StalkerForm")]
        [TestCase("PureFormRanged")]
        [TestCase("Spawner")]
        public void ACreatureRig_StandsOnAStillRoot(string body)
        {
            // creature_rig.py's convention: Root, still, over Body, which sways; the baker measures drift by Root, so the sway stays.
            // (The mesh is called Body too, a child of the model's own root.)
            var baked = Resources.Load<BakedBodyDefinition>("BakedBodies/" + body);
            Assert.IsTrue(baked.sourceModel.GetComponentsInChildren<Transform>().Any(t => t.name == "Body" && t.parent != null && t.parent.name == "Root"));
        }
    }
}
