// GoliathBodyTests.cs
// The Goliath, rigged and animated (D139): the owner found its old animation really bad (four limbs swinging stiffly on its static model)
// and made it melee only, with a charge like the Hunters'. Tools/Art/units/GoliathAnimated.py gives its static model a skeleton and five
// clips, and the crowd baker plays them: a knuckle-walk on its fists and hind legs, its slam one blow long and landing on the blow, a
// gallop for its charge, an idle it plays looped (stillLoops) instead of holding one frame, and its fall.
// Depends on: Halobellum.Units, Halobellum.Core, NUnit, UnityEditor (loading the assets).

using System.Linq;
using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class GoliathBodyTests
    {
        private static (UnitDefinition Unit, BakedBodyDefinition Body) Load()
        {
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/LekgoloColony/Goliath.asset");
            var body = Resources.Load<BakedBodyDefinition>("BakedBodies/Goliath");
            Assert.IsNotNull(body);
            Assert.AreSame(body, unit.bakedBody, "Its data points at the body.");
            Assert.IsTrue(body.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            return (unit, body);
        }

        private static float Seconds(BakedBodyDefinition body, BakedAnimation role) => body.Clip(role).frames / (float)body.bakedFps;

        [Test]
        public void TheGoliath_HasItsOwnWalk_Slam_Charge_Idle_AndFall()
        {
            (_, BakedBodyDefinition body) = Load();
            Assert.AreEqual("GoliathAnimated", body.modelFolder);
            Assert.AreEqual("Rig|Walk", body.movingClip);
            Assert.AreEqual("Rig|Slam", body.shootingClip, "Its only attack is its slam.");
            Assert.AreEqual("Rig|Charge", body.chargeClip);
            Assert.AreEqual("Rig|Idle", body.stillClip);
            Assert.AreEqual("Rig|Dead", body.deathClip);
            foreach (BakedAnimation role in new[] { BakedAnimation.Moving, BakedAnimation.Shooting, BakedAnimation.Charge, BakedAnimation.Still })
                Assert.IsTrue(body.Clip(role).loops, $"Its {role} clip loops.");
            Assert.IsFalse(body.Clip(BakedAnimation.Dead).loops);
            Assert.AreEqual(6f, body.bodyHeight, 0.05f, "As tall as its static model stood.");
        }

        [Test]
        public void ItsIdle_IsPlayedLooped_NotHeldAsOneFrame()
        {
            (_, BakedBodyDefinition body) = Load();
            Assert.IsTrue(body.stillLoops);
            Assert.AreEqual(3f, Seconds(body, BakedAnimation.Still), 0.05f, "Three seconds of breathing and looking about.");
        }

        [Test]
        public void ItsSlam_IsOneBlowLong_AndLandsOnTheBlow()
        {
            (UnitDefinition unit, BakedBodyDefinition body) = Load();
            WeaponStats slam = unit.ToStats().Weapons.First(w => w.Name == "Fist Slam");
            Assert.AreEqual(DamageType.Melee, slam.DamageType);
            Assert.AreEqual(slam.FireInterval, Seconds(body, BakedAnimation.Shooting), 0.04f, "One blow to the clip (HB_SLAM_SECONDS in its script).");
            Assert.AreEqual(0.5f, body.shotMoment, 0.001f, "Both fists hit the ground halfway through.");
        }

        [Test]
        public void ItsRig_WalksOnItsFists_AndStandsOnAStillRoot()
        {
            (_, BakedBodyDefinition body) = Load();
            Transform[] rig = body.sourceModel.GetComponentsInChildren<Transform>();
            Assert.IsTrue(rig.Any(t => t.name == "Body" && t.parent != null && t.parent.name == "Root"), "creature_rig.py's still Root, over the Body that sways.");
            foreach (string bone in new[] { "ArmL_1", "ArmR_1", "FistL", "FistR", "LegL_1", "LegR_1", "FootL", "FootR", "Head" })
                Assert.IsTrue(rig.Any(t => t.name == bone), bone);
        }
    }
}
