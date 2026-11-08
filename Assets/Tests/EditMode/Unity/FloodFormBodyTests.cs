// FloodFormBodyTests.cs
// The Spawner, the Pure Form Ranged and the Stalker Form as animated bodies (D90), rigged in Blender by Tools/Art/creature_rig.py: each unit
// has its baked body with its clips, each clip as long as the battle beat it keeps time with (the Stalker's three blows to three strikes of
// its Talons, the Ranged Form's shot to its reload, the Spawner's walk and lash to its spawns), the fronds cut out, the Ranged Form's shard
// leaving from the top of its column at the end of its thrust, and every one of the Spawner's tendrils with bones of its own.
// The Combat Form and the Infested Elite (D92), one model rigged the same way: each walks at its own pace, swipes with claw and whips one
// blow a strike, the Combat Form shooting from its claw and the Elite from the plasma rifle in its right hand.
// The Seeder, the Pure Form Tank and the Abomination (D93): the Seeder a flier centred on its middle, each of its tendril strands with a
// chain; the Tank's sweep one blow long; the Abomination's slam (with its whip) one slam long, on Meshy's rig put under a still Root.
// The Juggernaut (D107): its scissor strike one blow long, both arms chains of their own, a pounce for its leap.
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

        [TestCase("Flood/CombatForm", "CombatForm", 2.4f)]
        [TestCase("FloodCovenant/InfestedElite", "InfestedElite", 2.0f)]
        public void TheCombatFormsModel_WalksSwipesAndShoots_ForBothUnits(string unitPath, string bodyName, float speed)
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load(unitPath, bodyName);
            UnitStats stats = unit.ToStats();
            Assert.AreEqual(speed, stats.MoveSpeed, 0.001f, "Its walk is made for this speed.");
            Assert.AreEqual("CombatFormAnimated", body.modelFolder, "Both share one rigged model.");
            Assert.IsNotNull(body.Clip(BakedAnimation.Melee), "It swipes up close.");
            Assert.AreEqual(DamageType.Melee, stats.Weapons[0].DamageType, "The claw leads, so the swipe plays in a fight.");
            // Two blows to the swipe: as long as two strikes of the claw, give or take the pace the game keeps (the Elite's claw is a little faster).
            float swipe = Seconds(body, BakedAnimation.Melee);
            Assert.AreEqual(2, Mathf.RoundToInt(swipe / stats.Weapons[0].FireInterval));
            Assert.AreEqual(3f * stats.Weapons[1].FireInterval, Seconds(body, BakedAnimation.Shooting), 0.04f, "Three shots of its gun to a clip.");
            Assert.That(body.shotMoment, Is.InRange(0f, 0.5f));
            Assert.IsTrue(body.cutout, "The fronds on its stalks are see-through.");
            Assert.IsTrue(body.hasMuzzle);
            Assert.Greater(body.muzzle.z, 0.5f, "Its shots leave ahead of it.");
            Assert.Greater(body.muzzle.y, 1.2f, "From the arm held out, not its feet.");
        }

        [Test]
        public void TheInfestedElite_HoldsItsPlasmaRifle_TheCombatFormItsClaw()
        {
            var elite = Resources.Load<BakedBodyDefinition>("BakedBodies/InfestedElite");
            var form = Resources.Load<BakedBodyDefinition>("BakedBodies/CombatForm");
            Assert.AreEqual("Weapon_PlasmaRifle", elite.weaponName);
            Assert.IsTrue(elite.weaponAlongForearm, "Held in the right hand along the forearm.");
            Assert.Greater(elite.mesh.vertexCount, form.mesh.vertexCount, "The rifle is merged into its mesh.");
            Assert.IsEmpty(form.weaponName);
            Assert.AreEqual("Muzzle", form.muzzleBone, "The Combat Form shoots from its claw.");
            Assert.AreNotEqual(elite.movingClip, form.movingClip, "The Elite walks slower, its rifle carried forward.");
        }

        [Test]
        public void TheSeeder_FloatsWithItsTendrilsOnTheirOwn_AndSpitsOnItsShots()
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load("Flood/Seeder", "Seeder");
            UnitStats stats = unit.ToStats();
            Assert.IsTrue(stats.IsAir);
            Assert.AreEqual(stats.Weapons[0].FireInterval, Seconds(body, BakedAnimation.Shooting), 0.04f, "One spore to the clip.");
            Assert.That(body.shotMoment, Is.InRange(0.2f, 0.5f));
            Assert.Less(Mathf.Abs(body.mesh.vertices.Average(v => v.y)), 0.5f, "Centred on its middle, as the game flies it.");
            Assert.IsTrue(body.hasMuzzle);
            Assert.Greater(body.muzzle.z, 3f, "Its spore leaves from the tip of its head.");
            int strands = body.sourceModel.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Tendril_") && t.name.EndsWith("_0"));
            Assert.GreaterOrEqual(strands, 10, "Its tendrils have chains of their own.");
        }

        [Test]
        public void TheJuggernaut_RunsOnItsLegs_WithBothArmsWrithing_AndPouncesOnItsLeap()
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load("FloodUNSC/Juggernaut", "Juggernaut");
            UnitStats stats = unit.ToStats();
            Assert.Greater(stats.LeapRange, 0f, "It leaps.");
            Assert.IsNotNull(body.Clip(BakedAnimation.Charge), "Its pounce, played while it leaps.");
            Assert.IsTrue(body.Clip(BakedAnimation.Charge).loops);
            Transform[] rig = body.sourceModel.GetComponentsInChildren<Transform>();
            Assert.GreaterOrEqual(rig.Count(t => t.name.StartsWith("Low_")), 8, "Its low arm a chain of its own, shoulder to blade.");
            Assert.GreaterOrEqual(rig.Count(t => t.name.StartsWith("High_")), 7, "And its high arm.");
            Assert.IsTrue(rig.Any(t => t.name == "LegL_1") && rig.Any(t => t.name == "LegR_1"), "Legs that bend.");
        }

        [TestCase("Flood/PureFormTank", "PureFormTank", 0.5f)]
        [TestCase("Flood/Abomination", "Abomination", 0.62f)]
        [TestCase("FloodUNSC/Juggernaut", "Juggernaut", 0.42f)]
        public void ABigFloodForm_StrikesOnItsWeaponsBeat(string unitPath, string bodyName, float moment)
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load(unitPath, bodyName);
            UnitStats stats = unit.ToStats();
            Assert.AreEqual(DamageType.Melee, stats.Weapons[0].DamageType);
            Assert.AreEqual(stats.Weapons[0].FireInterval, Seconds(body, BakedAnimation.Shooting), 0.04f, "One blow to the clip, timed to land on it.");
            Assert.AreEqual(moment, body.shotMoment, 0.001f);
        }

        [TestCase("StalkerForm")]
        [TestCase("PureFormRanged")]
        [TestCase("Spawner")]
        [TestCase("CombatForm")]
        [TestCase("Seeder")]
        [TestCase("PureFormTank")]
        [TestCase("Abomination")]
        [TestCase("Juggernaut")]
        public void ACreatureRig_StandsOnAStillRoot(string body)
        {
            // creature_rig.py's convention: Root, still, over Body, which sways; the baker measures drift by Root, so the sway stays.
            // (The mesh is called Body too, a child of the model's own root.)
            var baked = Resources.Load<BakedBodyDefinition>("BakedBodies/" + body);
            Assert.IsTrue(baked.sourceModel.GetComponentsInChildren<Transform>().Any(t => t.name == "Body" && t.parent != null && t.parent.name == "Root"));
        }
    }
}
