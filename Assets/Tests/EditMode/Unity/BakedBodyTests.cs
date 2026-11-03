// BakedBodyTests.cs
// The baked Marine (M9, decisions D41 and D42): it uses the clips the owner chose, the baked data fits together,
// and the rifle is part of its mesh.
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
        public void TheMarineUnit_UsesIt_AndAUnitStillWithoutAModelDoesNot()
        {
            UnitDefinition[] units = Resources.FindObjectsOfTypeAll<UnitDefinition>();
            var marineUnit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Marine.asset");
            Assert.AreSame(Marine(), marineUnit.bakedBody);
            var mantis = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Mantis.asset");
            Assert.IsNull(mantis.bakedBody);
        }
    }
}
