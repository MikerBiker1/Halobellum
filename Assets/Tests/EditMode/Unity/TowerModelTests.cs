// TowerModelTests.cs
// The towers' real models (D55): each prefab in Resources/Towers fits the tower's 3 squares, stands on the ground centred on its spot,
// faces the way a unit does, keeps its triangles modest, and has a glow mask that TowerArt lights in the faction's colour.
// Depends on: Halobellum.Presentation (TowerArt, FactionColors, BattleFactions), Halobellum.Core, NUnit, UnityEngine.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class TowerModelTests
    {
        private const float TowerWidth = 6f; // 3 squares of 2 m: MatchRules' towerSquares and squareSize.

        private static readonly TowerKind[] Kinds = { TowerKind.CommandCenter, TowerKind.ResearchCenter };

        [TestCaseSource(nameof(Kinds))]
        public void TowerModel_FitsItsSquares_StandsOnTheGround_AndHasAGlowMask(TowerKind kind)
        {
            GameObject prefab = TowerArt.PrefabFor(kind);
            Assert.IsNotNull(prefab, $"Resources/Towers/{kind} is built.");
            GameObject model = Object.Instantiate(prefab);
            try
            {
                MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (MeshRenderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.LessOrEqual(Mathf.Max(bounds.size.x, bounds.size.z), TowerWidth, "It leaves the squares around it free.");
                Assert.Greater(Mathf.Max(bounds.size.x, bounds.size.z), TowerWidth * 0.9f, "It fills its squares.");
                Assert.Greater(bounds.size.y, 4.5f, "It stands tall.");
                Assert.AreEqual(0f, bounds.min.y, 0.02f, "On the ground.");
                Assert.AreEqual(0f, bounds.center.x, 0.02f);
                Assert.AreEqual(0f, bounds.center.z, 0.02f);
                Assert.Less(model.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3), 8000);
                Material material = renderers[0].sharedMaterial;
                Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                Assert.IsNotNull(material.GetTexture("_BaseMap"), "It keeps its texture.");
                Assert.IsNotNull(material.GetTexture("_EmissionMap"), "Its highlights are a mask in its glow.");
                Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"), "The glow is switched on, or the mask would never show.");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [TestCaseSource(nameof(Kinds))]
        public void TowerArt_LightsTheMask_InTheFactionsColour(TowerKind kind)
        {
            var root = new GameObject("Tower");
            try
            {
                float top = TowerArt.Build(root.transform, kind, Color.blue, FactionColors.Covenant, 3f);
                Assert.Greater(top, 4.5f, "The health bar goes above the real model.");
                Transform body = root.transform.Find("Body");
                Assert.IsNotNull(body, "The real model, not plain shapes.");
                var block = new MaterialPropertyBlock();
                body.GetComponentInChildren<MeshRenderer>().GetPropertyBlock(block);
                Color glow = block.GetColor("_EmissionColor");
                Color wanted = TowerArt.GlowFor(kind, FactionColors.Covenant);
                Assert.AreEqual(wanted.r, glow.r, 0.01f);
                Assert.AreEqual(wanted.g, glow.g, 0.01f);
                Assert.AreEqual(wanted.b, glow.b, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TowerColours_FollowEachSidesFaction_AndAreNeutralInUnlockSkirmish()
        {
            try
            {
                BattleFactions.Set(BattleFactions.Flood, BattleFactions.Unsc);
                Assert.AreEqual(FactionColors.Flood, FactionColors.OfTowers(Team.A));
                Assert.AreEqual(FactionColors.Unsc, FactionColors.OfTowers(Team.B));
                BattleFactions.SetSkirmish(BattleFactions.Covenant, new Dictionary<string, string>());
                Assert.AreEqual(FactionColors.Neutral, FactionColors.OfTowers(Team.A), "Neither side has a faction of its own.");
                Assert.AreEqual(FactionColors.Neutral, FactionColors.OfTowers(Team.B));
            }
            finally
            {
                BattleFactions.Set(BattleFactions.Unsc, BattleFactions.Covenant);
            }
        }
    }
}
