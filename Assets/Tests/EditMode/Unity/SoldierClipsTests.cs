// SoldierClipsTests.cs
// The Spartans', the Elite's and the Zealot's own walks and shots (D129), made by Tools/Art/units/SoldierClips.py on Meshy's rigs in place of
// the library clips they shared with the Marines: each body plays its own clips from its shared model; a clip that shoots lasts one shot of
// its main weapon and kicks on it; each walk or run is timed to its unit's speed; the weapons sit where the owner asked (the Spartan's on
// its shoulder, the CQB's at the hip, the Elite's rifle held out in its right hand when firing; the Heavy's machine gun low in both hands,
// carried like a minigun, D132).
// Depends on: Halobellum.Units, Halobellum.Core, NUnit, UnityEditor (loading the assets).

using System.Linq;
using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class SoldierClipsTests
    {
        private static (UnitStats Stats, BakedBodyDefinition Body) Load(string unit, string body)
        {
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{unit}.asset");
            var baked = Resources.Load<BakedBodyDefinition>("BakedBodies/" + body);
            Assert.IsNotNull(baked, body);
            Assert.AreSame(baked, definition.bakedBody, "Its data points at the body.");
            Assert.IsTrue(baked.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            return (definition.ToStats(), baked);
        }

        private static float Seconds(BakedBodyDefinition body, BakedAnimation role) => body.Clip(role).frames / (float)body.bakedFps;

        [TestCase("UNSC/Spartan", "Spartan", "Carry_Walk", "Laser_Fire", "Carry_Stand")]
        [TestCase("Noble/SpartanHeavyWeapons", "SpartanHeavyWeapons", "Minigun_Walk", "Minigun_Fire", "Minigun_Stand")]
        [TestCase("Noble/SpartanFireteam", "SpartanFireteam", "Ready_Walk", "Rifle_Fire", "Ready_Stand")]
        [TestCase("Noble/SpartanGrenadier", "SpartanGrenadier", "Ready_Walk", "Rocket_Fire", "Ready_Stand")]
        [TestCase("Noble/SpartanSniper", "SpartanSniper", "Ready_Walk", "Sniper_Fire", "Ready_Stand")]
        [TestCase("Noble/SpartanCQB", "SpartanCQB", "Hip_Run", "Shotgun_Fire", "Hip_Stand")]
        [TestCase("Covenant/Elite", "Elite", "Elite_Walk", "Plasma_Fire", "Elite_Stand")]
        [TestCase("Covenant/Zealot", "Zealot", "Zealot_Run", "Right_Hand_Sword_Slash", "Zealot_Stand")]
        public void EachSoldier_PlaysItsOwnClips_FromItsModel(string unit, string name, string moving, string shooting, string still)
        {
            (UnitStats stats, BakedBodyDefinition body) = Load(unit, name);
            Assert.AreEqual("Armature|" + moving, body.movingClip);
            Assert.AreEqual("Armature|" + shooting, body.shootingClip);
            Assert.AreEqual("Armature|" + still, body.stillClip);
            Assert.AreEqual("Armature|Dead", body.deathClip, "Meshy's fall, kept.");
            string model = AssetDatabase.GetAssetPath(body.sourceModel);
            string[] clips = AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Select(c => c.name).ToArray();
            foreach (string clip in new[] { body.movingClip, body.shootingClip, body.stillClip, body.deathClip })
                CollectionAssert.Contains(clips, clip, model);
            foreach (BakedAnimation role in new[] { BakedAnimation.Still, BakedAnimation.Moving, BakedAnimation.Shooting, BakedAnimation.Dead })
                Assert.IsNotNull(body.Clip(role), $"{name} has its {role} clip.");
            Assert.IsFalse(body.Clip(BakedAnimation.Dead).loops);
            Assert.IsTrue(body.hasMuzzle);

            // A walk or run as long as two of its steps take at its speed: a stride of half to a whole body's height.
            float stride = stats.MoveSpeed * Seconds(body, BakedAnimation.Moving);
            Assert.That(stride / body.bodyHeight, Is.InRange(0.5f, 1.1f), $"{name}: {stride:0.00} m a cycle.");
        }

        [TestCase("UNSC/Spartan", "Spartan")]
        [TestCase("Noble/SpartanFireteam", "SpartanFireteam")]
        [TestCase("Noble/SpartanGrenadier", "SpartanGrenadier")]
        [TestCase("Noble/SpartanSniper", "SpartanSniper")]
        [TestCase("Noble/SpartanCQB", "SpartanCQB")]
        [TestCase("Covenant/Elite", "Elite")]
        public void AShootingClip_LastsOneShot_AndKicksOnIt(string unit, string name)
        {
            (UnitStats stats, BakedBodyDefinition body) = Load(unit, name);
            Assert.AreEqual(stats.Weapons[0].FireInterval, Seconds(body, BakedAnimation.Shooting), 1.5f / body.bakedFps, "One shot to the clip.");
            Assert.That(body.shotMoment, Is.InRange(0.05f, 0.2f), "The kick near its start; the clip is still at its middle, where the weapon is lined up.");
        }

        [Test]
        public void TheSpartans_WalkWithTheirWeaponsAsTheOwnerAsked()
        {
            BakedBodyDefinition Body(string n) => Resources.Load<BakedBodyDefinition>("BakedBodies/" + n);
            Assert.AreNotEqual(Body("Spartan").movingClip, Body("SpartanHeavyWeapons").movingClip, "The Heavy carries its gun low, like a minigun (D132).");
            Assert.AreEqual(Body("SpartanFireteam").movingClip, Body("SpartanSniper").movingClip, "Held low in front, aimed at nothing.");
            Assert.AreEqual(Body("SpartanFireteam").movingClip, Body("SpartanGrenadier").movingClip);
            Assert.AreNotEqual(Body("Spartan").movingClip, Body("SpartanFireteam").movingClip);
            Assert.AreEqual(-1f, Body("SpartanHeavyWeapons").shotMoment, "Its chaingun rattles on, too fast to kick on every shot.");
            Assert.Greater(Body("Spartan").muzzle.y, 1.7f, "Firing, the laser is shouldered at the height of the eyes.");
            Assert.Less(Body("SpartanCQB").muzzle.y, 1.5f, "The shotgun fires from the hip.");
        }

        [Test]
        public void TheElite_HoldsItsRifleInTheRightHand_AndRaisesItArmOutToFire()
        {
            BakedBodyDefinition elite = Resources.Load<BakedBodyDefinition>("BakedBodies/Elite");
            Assert.IsTrue(elite.weaponAlongForearm, "In the right hand, along the forearm.");
            Assert.IsFalse(elite.weaponInForwardHand);
            Assert.Greater(elite.muzzle.y, 1.8f, "At the height of its shoulder when firing.");
            Assert.Greater(elite.muzzle.z, 0.8f, "The arm straight out in front.");
        }
    }
}
