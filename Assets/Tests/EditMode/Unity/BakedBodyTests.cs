// BakedBodyTests.cs
// The baked Marine (M9, decisions D41 and D42): it uses the clips the owner chose, the baked data fits together,
// and the rifle is part of its mesh. And the other baked bodies: each holds its own weapon (M12: the Elite's plasma rifle,
// the rifle Jackal's beam rifle) and the Flamethrower Trooper shoots from the front of the gun that is part of its model.
// Depends on: Halobellum.Units, NUnit, UnityEngine.

using System.Linq;
using Halobellum.Units;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class BakedBodyTests
    {
        private static BakedBodyDefinition Marine() => Resources.Load<BakedBodyDefinition>("BakedBodies/Marine");

        [Test]
        public void Marine_UsesTheClipsTheOwnerChose()
        {
            BakedBodyDefinition marine = Marine();
            Assert.IsNotNull(marine);
            Assert.AreEqual("Run_and_Shoot", marine.movingClip);
            Assert.AreEqual("Walk_Forward_While_Shooting", marine.shootingClip);
            Assert.AreEqual("Dead", marine.deathClip);
            Assert.AreEqual("Walking", marine.stillClip);
            Assert.Greater(marine.transitionSeconds, 0f, "Animations cross-fade, they don't snap.");
        }

        [Test]
        public void Marine_IsBaked_AndItsClipsFitTheTexture()
        {
            BakedBodyDefinition marine = Marine();
            Assert.IsTrue(marine.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            var texture = (Texture2D)marine.material.GetTexture("_AnimTex");
            Assert.IsNotNull(texture);
            Assert.IsTrue(marine.material.enableInstancing, "Big armies need GPU instancing.");
            Assert.IsNotNull(marine.material.GetTexture("_MainTex"));
            Assert.AreEqual(marine.bakedFps, marine.material.GetFloat("_AnimFps"));

            int nextRow = 0;
            foreach (BakedClip clip in marine.clips)
            {
                Assert.AreEqual(nextRow, clip.firstRow, "Clips follow each other in the texture.");
                Assert.Greater(clip.frames, 0);
                nextRow += clip.frames;
            }
            Assert.AreEqual(texture.height, nextRow);

            Assert.AreEqual(1, marine.Clip(BakedAnimation.Still).frames, "A single held frame.");
            Assert.IsFalse(marine.Clip(BakedAnimation.Still).loops);
            Assert.IsTrue(marine.Clip(BakedAnimation.Moving).loops);
            Assert.IsTrue(marine.Clip(BakedAnimation.Shooting).loops);
            Assert.IsFalse(marine.Clip(BakedAnimation.Dead).loops, "It falls once and stays down.");
        }

        [Test]
        public void Marine_FiresFromTheTipOfItsRifle()
        {
            BakedBodyDefinition marine = Marine();
            Assert.IsTrue(marine.hasMuzzle);
            Assert.AreEqual("Weapon_AssaultRifle", marine.weaponName);
            Assert.Greater(marine.muzzle.z, 0.3f, "Out in front of the body.");
            Assert.Greater(marine.muzzle.y, 0.8f);
            Assert.Less(marine.muzzle.y, 2f);
            Assert.Less(Mathf.Abs(marine.muzzle.x), 0.5f);
        }

        [TestCase("Grunt", "Weapon_PlasmaPistol")]
        [TestCase("FuelRodGrunt", "Weapon_FuelRod")]
        public void Grunts_ShareABody_EachWithItsOwnWeapon(string name, string weapon)
        {
            BakedBodyDefinition body = Resources.Load<BakedBodyDefinition>("BakedBodies/" + name);
            Assert.IsTrue(body.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            Assert.AreEqual(weapon, body.weaponName);
            Assert.AreEqual("Grunt", body.modelFolder);
            Assert.IsTrue(body.hasMuzzle);
            Assert.Greater(body.muzzle.z, 0.1f, "Out in front of the body.");
            Assert.Less(body.muzzle.y, 1.5f, "A Grunt is short.");
            Assert.Less(body.mesh.triangles.Length / 3, 4600);
            Assert.IsNotNull(body.Clip(BakedAnimation.Dead));
        }

        [TestCase("SpellUnits/ODST", "ODST", "Weapon_SMG", 2f)]
        [TestCase("Covenant/JackalBeamRifle", "Jackal", "Weapon_BeamRifle", 2f)]
        [TestCase("Covenant/JackalShield", "JackalShield", "Weapon_PlasmaPistol", 2f)]
        [TestCase("Banished/BruteGrenadier", "BruteGrenadier", "Weapon_RocketLauncher", 2.8f)]
        [TestCase("FloodUNSC/InfestedMarine", "InfestedMarine", "Weapon_AssaultRifle", 2f)]
        [TestCase("Covenant/Elite", "Elite", "Weapon_PlasmaRifle", 2.6f)]
        [TestCase("UNSC/FlamethrowerTrooper", "FlamethrowerTrooper", "", 2f)]
        public void TheThirdBatch_IsBaked_AndItsUnitsUseIt(string unit, string name, string weapon, float height)
        {
            BakedBodyDefinition body = Resources.Load<BakedBodyDefinition>("BakedBodies/" + name);
            Assert.IsTrue(body.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            Assert.AreSame(body, UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDefinition>($"Assets/_Project/Data/Units/{unit}.asset").bakedBody);
            Assert.AreEqual(weapon, body.weaponName);
            Assert.AreEqual(height, body.bodyHeight, 0.05f, "How tall it is decides where its health bar goes.");
            Assert.IsTrue(body.hasMuzzle);
            Assert.Less(Mathf.Abs(body.muzzle.x), 1f);
            Assert.Greater(body.muzzle.z, 0f, "Its shots start ahead of its middle.");
            Assert.Less(body.mesh.triangles.Length / 3, 7500);
            Assert.IsNotNull(body.Clip(BakedAnimation.Dead));
        }

        [Test]
        public void TheShieldJackalHoldsAPlasmaPistol_AndTheOtherALongBeamRifle()
        {
            BakedBodyDefinition rifle = Resources.Load<BakedBodyDefinition>("BakedBodies/Jackal");
            BakedBodyDefinition shield = Resources.Load<BakedBodyDefinition>("BakedBodies/JackalShield");
            Assert.AreEqual("Weapon_PlasmaPistol", shield.weaponName);
            Assert.AreEqual("Weapon_BeamRifle", rifle.weaponName);
            Assert.GreaterOrEqual(rifle.weaponLength, 1.2f, "A sniper rifle, longer than the pistol.");
            Assert.IsFalse(rifle.weaponInForwardHand, "Held in both hands.");
            Assert.Greater(rifle.muzzle.z, shield.muzzle.z, "Its muzzle reaches further forward.");
        }

        [Test]
        public void FlamethrowerTrooper_ShootsFromTheFrontOfTheGunInItsModel()
        {
            BakedBodyDefinition body = Resources.Load<BakedBodyDefinition>("BakedBodies/FlamethrowerTrooper");
            Assert.IsTrue(body.IsReady, "Run Halobellum > Art > Bake Animated Bodies.");
            Assert.IsTrue(body.muzzleAtFront);
            Assert.IsTrue(string.IsNullOrEmpty(body.weaponName), "The gun is part of the model.");
            Assert.IsTrue(body.hasMuzzle);
            Assert.Greater(body.muzzle.z, 0.6f, "The gun reaches out in front of him.");
            Assert.Less(body.muzzle.y, 1.2f, "Held low, at the hip.");
            Assert.AreEqual(1, body.mesh.subMeshCount);
        }

        [Test]
        public void TheJackalsShareAModel_AndOnlyTheShieldedOneHasAShield()
        {
            BakedBodyDefinition rifle = Resources.Load<BakedBodyDefinition>("BakedBodies/Jackal");
            BakedBodyDefinition shield = Resources.Load<BakedBodyDefinition>("BakedBodies/JackalShield");
            Assert.AreEqual("Jackal", rifle.modelFolder);
            Assert.AreEqual("Jackal", shield.modelFolder);
            Assert.AreEqual(1, rifle.mesh.subMeshCount);
            Assert.IsNull(rifle.glassMaterial);
            Assert.AreEqual(1, rifle.Materials.Length);

            Assert.AreEqual(2, shield.mesh.subMeshCount);
            Assert.AreEqual(375, shield.mesh.GetTriangles(1).Length / 3, "The file's whole shield.");
            Assert.AreEqual(2, shield.Materials.Length);
            Material glass = shield.glassMaterial;
            Assert.AreEqual(3000, glass.renderQueue, "Drawn over what is behind it.");
            Assert.AreEqual(1f, glass.GetFloat("_Glass"));
            Assert.AreEqual(0f, glass.GetFloat("_ZWrite"));
            Assert.IsTrue(glass.enableInstancing, "Big armies need GPU instancing.");
            Assert.AreEqual(0f, shield.material.GetFloat("_Glass"), "The body itself stays solid.");
            Assert.AreEqual(1f, shield.material.GetFloat("_ZWrite"));
        }

        [Test]
        public void Marine_Mesh_NamesBonesInTheTexture_AndHoldsARifle()
        {
            BakedBodyDefinition marine = Marine();
            Mesh mesh = marine.mesh;
            int bones = ((Texture2D)marine.material.GetTexture("_AnimTex")).width / 3;
            var ids = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(1, ids);
            Assert.AreEqual(mesh.vertexCount, ids.Count);
            Assert.IsTrue(ids.All(b => b.x >= 0f && b.x < bones && b.y >= 0f && b.y < bones));
            Assert.Less(bones, 52, "Finger bones are merged into the hands.");
            Assert.Greater(mesh.colors.Count(c => c.a > 0.5f), 0, "The rifle's vertices have a flat colour.");
        }

        [Test]
        public void TheMarineUnit_UsesIt_AndTheFlamethrowerTrooperHasItsOwn()
        {
            var marineUnit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Marine.asset");
            Assert.AreSame(Marine(), marineUnit.bakedBody);
            var trooper = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/FlamethrowerTrooper.asset");
            Assert.AreSame(Resources.Load<BakedBodyDefinition>("BakedBodies/FlamethrowerTrooper"), trooper.bakedBody);
        }
    }
}
