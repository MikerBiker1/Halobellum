// WeaponModelsTests.cs
// The owner's weapon models of D131: the Zealot's twinblade, its blades kept whole and glowing (a white core in cyan halos, in the body's
// see-through sub-mesh), only its hilt reduced; the MA5B the Marines carry, light enough for 18 to a squad; and the Spartan Fireteam's
// battle rifle in place of the assault rifle it borrowed. The Spartan Heavy Weapons' machine gun, whole (D132).
// Depends on: Halobellum.Units, Halobellum.EditorTools (AnimationBaker.Glows), NUnit, UnityEditor (loading the assets).

using System.Linq;
using Halobellum.EditorTools;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class WeaponModelsTests
    {
        private static Mesh WeaponMesh(string weapon)
        {
            string path = $"{AnimationBaker.ModelsFolder}/{weapon}/{weapon}.fbx";
            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
            Assert.IsNotNull(mesh, path);
            return mesh;
        }

        [Test]
        public void TheTwinblade_KeepsItsBladesWhole_AndMarksThemToGlow()
        {
            Mesh sword = WeaponMesh("Weapon_EnergySword");
            Color[] colours = sword.colors;
            int[] triangles = sword.triangles;
            int glowing = 0;
            for (int t = 0; t < triangles.Length; t += 3)
                if (AnimationBaker.Glows((colours[triangles[t]] + colours[triangles[t + 1]] + colours[triangles[t + 2]]) / 3f)) glowing++;
            Assert.Greater(glowing, 1500, "The blades as they came, not reduced.");
            Assert.Less(triangles.Length / 3 - glowing, 1200, "Only the hilt is reduced.");
        }

        [Test]
        public void TheZealot_DrawsItsBladesAsLight()
        {
            BakedBodyDefinition zealot = Resources.Load<BakedBodyDefinition>("BakedBodies/Zealot");
            Assert.IsTrue(zealot.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            Assert.AreEqual("Weapon_EnergySword", zealot.weaponName);
            Assert.IsTrue(zealot.weaponGlows);
            Assert.AreEqual(2, zealot.mesh.subMeshCount, "The blades in the see-through sub-mesh.");
            Assert.IsNotNull(zealot.glassMaterial);
            Assert.AreEqual(1f, zealot.glassMaterial.GetFloat("_Glass"), "Unlit, in its own colours.");
            Assert.Greater(zealot.mesh.GetTriangles(1).Length / 3, 4 * 1500, "Each blade face as a core and three halos.");
            Assert.AreEqual(2, zealot.Materials.Length);
        }

        [Test]
        public void TheMarinesRifle_IsTheMA5B_LightEnoughForASquadOf18()
        {
            Assert.Less(WeaponMesh("Weapon_AssaultRifle").triangles.Length / 3, 1000);
            foreach (string body in new[] { "Marine", "InfestedMarine" })
                Assert.AreEqual("Weapon_AssaultRifle", Resources.Load<BakedBodyDefinition>("BakedBodies/" + body).weaponName, body);
        }

        [Test]
        public void TheHeavy_CarriesTheWholeMachineGun_AndNoRockets()
        {
            BakedBodyDefinition heavy = Resources.Load<BakedBodyDefinition>("BakedBodies/SpartanHeavyWeapons");
            Assert.AreEqual("Weapon_Chaingun", heavy.weaponName);
            Assert.Greater(WeaponMesh("Weapon_Chaingun").triangles.Length / 3, 10000, "Whole, as the owner asked: shield, mount and all (D132).");
            var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/Noble/SpartanHeavyWeapons.asset");
            Assert.AreEqual(1, unit.ToStats().Weapons.Length, "Its chaingun only: no rocket pod (D132).");
        }

        [Test]
        public void TheFireteam_CarriesTheBattleRifle()
        {
            BakedBodyDefinition fireteam = Resources.Load<BakedBodyDefinition>("BakedBodies/SpartanFireteam");
            Assert.AreEqual("Weapon_BattleRifle", fireteam.weaponName);
            Assert.IsTrue(fireteam.hasMuzzle);
            Assert.Greater(fireteam.muzzle.z, 0.5f, "Its shots leave the barrel, out in front.");
            Assert.Less(WeaponMesh("Weapon_BattleRifle").triangles.Length / 3, 1500);
        }
    }
}
