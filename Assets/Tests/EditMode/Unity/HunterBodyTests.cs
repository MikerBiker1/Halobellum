// HunterBodyTests.cs
// The Hunters as the game has them (D87): both hold fire with their gun inside 5 m, bash with their shield and charge along the ground; their
// baked bodies have the clips for it (guard, walk, aim, bash, charge, dead), the gun's shots start at its front, and their icon is the aim.
// Depends on: Halobellum.Units, Halobellum.Core, NUnit, UnityEditor (loading the assets).

using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class HunterBodyTests
    {
        [TestCase("Covenant/HunterPair", "Hunter")]
        [TestCase("LekgoloColony/HunterCaptain", "HunterCaptain")]
        public void AHunter_ChargesAndBashes_WithClipsForIt(string unit, string bodyName)
        {
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{unit}.asset");
            UnitStats stats = definition.ToStats();
            Assert.IsTrue(stats.LeapCharges, "Its leap is a charge.");
            Assert.AreEqual(5f, stats.LeapRange, 0.001f, "It charges at what comes within 5 m.");
            Assert.IsTrue(stats.SwitchesWeapons, "One weapon at a time.");
            Assert.AreEqual(2, stats.Weapons.Length);
            Assert.AreEqual(5f, stats.Weapons[0].MinRange, 0.001f, "Its gun holds fire inside 5 m.");
            Assert.AreEqual("Shield Bash", stats.Weapons[1].Name);
            Assert.AreEqual(DamageType.Melee, stats.Weapons[1].DamageType);
            Assert.LessOrEqual(stats.Weapons[1].Range, UnitUpgrades.MeleeRange);
            Assert.IsFalse(stats.Weapons[1].CanTarget(stats, targetIsAir: true), "A bash only reaches the ground.");

            BakedBodyDefinition body = Resources.Load<BakedBodyDefinition>("BakedBodies/" + bodyName);
            Assert.AreSame(body, definition.bakedBody);
            Assert.IsTrue(body.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            foreach (BakedAnimation role in new[] { BakedAnimation.Still, BakedAnimation.Moving, BakedAnimation.Shooting, BakedAnimation.Dead, BakedAnimation.Melee, BakedAnimation.Charge })
                Assert.IsNotNull(body.Clip(role), $"{bodyName} has its {role} clip.");
            Assert.IsTrue(body.hasMuzzle);
            Assert.Greater(body.muzzle.z, 0.5f, "Its shots start at the front of its gun, levelled ahead.");
            Assert.Greater(body.muzzle.y, body.bodyHeight * 0.4f, "At shoulder height, not at its feet.");
        }

        /// <summary>The Brute Grenadier's blade (D88, the owner): a melee second weapon, and Meshy's heavy swing to play while it cuts, eased into a loop.</summary>
        [Test]
        public void TheBruteGrenadier_CutsWithItsBlade_WithASwingClip()
        {
            var definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Banished/BruteGrenadier.asset");
            UnitStats stats = definition.ToStats();
            Assert.AreEqual(2, stats.Weapons.Length);
            Assert.AreEqual(2f, stats.Weapons[0].MinRange, 0.001f, "Its grenades cannot go off inside 2 m.");
            Assert.AreEqual("Brute Shot Blade", stats.Weapons[1].Name);
            Assert.AreEqual(DamageType.Melee, stats.Weapons[1].DamageType);
            Assert.LessOrEqual(stats.Weapons[1].Range, stats.Weapons[0].MinRange, "The blade covers what the grenades cannot.");

            BakedBodyDefinition body = Resources.Load<BakedBodyDefinition>("BakedBodies/BruteGrenadier");
            Assert.IsTrue(body.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            Assert.AreEqual("Heavy_Hammer_Swing", body.meleeClip);
            Assert.IsNotNull(body.Clip(BakedAnimation.Melee), "It swings while it cuts.");
            Assert.IsTrue(body.Clip(BakedAnimation.Melee).loops);
            Assert.Greater(body.meleeBlendShare, 0f, "Meshy's swing is made to be played once: its end is eased back into its start.");
        }
    }
}
