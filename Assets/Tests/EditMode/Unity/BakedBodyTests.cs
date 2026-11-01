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
        public void TheMarineUnit_UsesIt_AndOtherUnitsDoNotYet()
        {
            UnitDefinition[] units = Resources.FindObjectsOfTypeAll<UnitDefinition>();
            var marineUnit = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Marine.asset");
            Assert.AreSame(Marine(), marineUnit.bakedBody);
            var spartan = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_Project/Data/Units/UNSC/Spartan.asset");
            Assert.IsNull(spartan.bakedBody);
        }
    }
}
