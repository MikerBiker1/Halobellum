// CombatRulesAssetTests.cs
// Loads the real Data/CombatRules.asset and checks it holds the Bible's starting values and
// matches the TestRules fixture used by the plain C# tests. If a human approves a balance change,
// update the asset, TestRules and this test together.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.Core, NUnit, UnityEditor.

using System;
using Halobellum.Core;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class CombatRulesAssetTests
    {
        [Test]
        public void CombatRulesAsset_Exists_AndMatchesBibleStartingValues()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CombatRulesDefinition>(CombatRulesDefinition.DefaultAssetPath);
            Assert.IsNotNull(asset, "Missing " + CombatRulesDefinition.DefaultAssetPath);

            CombatRules fromAsset = asset.ToRules();
            CombatRules expected = TestRules.Default();

            Assert.AreEqual(expected.TicksPerSecond, fromAsset.TicksPerSecond);
            Assert.AreEqual(expected.RetargetIntervalSeconds, fromAsset.RetargetIntervalSeconds, 1e-5f);
            Assert.AreEqual(expected.ShieldRegenDelaySeconds, fromAsset.ShieldRegenDelaySeconds, 1e-5f);
            Assert.AreEqual(expected.BattleTimeLimitSeconds, fromAsset.BattleTimeLimitSeconds, 1e-5f);
            Assert.AreEqual(expected.MainWeaponFacingToleranceDegrees, fromAsset.MainWeaponFacingToleranceDegrees, 1e-5f);
            Assert.AreEqual(expected.AcquisitionRangeMultiplier, fromAsset.AcquisitionRangeMultiplier, 1e-5f);
            // Square size (D10) is meant to be tuned freely in the Inspector, so any positive value passes.
            Assert.Greater(fromAsset.SquareSize, 0f, "squareSize must be above 0.");
            Assert.AreEqual(expected.ArenaWidthSquares, fromAsset.ArenaWidthSquares);
            Assert.AreEqual(expected.DeploymentDepthSquares, fromAsset.DeploymentDepthSquares);
            Assert.AreEqual(expected.NoMansLandSquares, fromAsset.NoMansLandSquares);
            Assert.AreEqual(expected.SpatialCellSize, fromAsset.SpatialCellSize, 1e-5f);
            Assert.AreEqual(expected.SeparationPadding, fromAsset.SeparationPadding, 1e-5f);
            Assert.AreEqual(expected.SeparationWeight, fromAsset.SeparationWeight, 1e-5f);
            Assert.AreEqual(expected.AvoidanceWeight, fromAsset.AvoidanceWeight, 1e-5f);
            Assert.AreEqual(expected.AvoidanceLookaheadSeconds, fromAsset.AvoidanceLookaheadSeconds, 1e-5f);
            Assert.AreEqual(expected.CollisionIterations, fromAsset.CollisionIterations);
            Assert.AreEqual(expected.FormationGap, fromAsset.FormationGap, 1e-5f);
            Assert.AreEqual(expected.FormationLookaheadSeconds, fromAsset.FormationLookaheadSeconds, 1e-5f);
            Assert.AreEqual(expected.YieldSpeedRatio, fromAsset.YieldSpeedRatio, 1e-5f);
            Assert.AreEqual(expected.YieldWeight, fromAsset.YieldWeight, 1e-5f);
            Assert.AreEqual(expected.AdvanceSpeedMultiplier, fromAsset.AdvanceSpeedMultiplier, 1e-5f);
            Assert.AreEqual(expected.AdvanceRangeShare, fromAsset.AdvanceRangeShare, 1e-5f);

            foreach (ArmorType armor in Enum.GetValues(typeof(ArmorType)))
            {
                foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
                {
                    Assert.AreEqual(expected.DamageTable.Get(type, armor), fromAsset.DamageTable.Get(type, armor), 1e-5f, $"{type} vs {armor}");
                }
                Assert.AreEqual(expected.DamageTable.GetAntiArmor(armor), fromAsset.DamageTable.GetAntiArmor(armor), 1e-5f, $"Anti-armor vs {armor}");
            }
            foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
            {
                Assert.AreEqual(expected.DamageTable.GetShield(type), fromAsset.DamageTable.GetShield(type), 1e-5f, $"{type} vs shield");
            }
            Assert.AreEqual(expected.DamageTable.GetAntiArmorShield(), fromAsset.DamageTable.GetAntiArmorShield(), 1e-5f, "Anti-armor vs shield");
        }
    }
}
