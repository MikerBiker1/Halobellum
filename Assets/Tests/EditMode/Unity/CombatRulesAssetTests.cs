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
            Assert.AreEqual(expected.ArenaWidth, fromAsset.ArenaWidth, 1e-5f);
            Assert.AreEqual(expected.ArenaLength, fromAsset.ArenaLength, 1e-5f);

            foreach (ArmorType armor in Enum.GetValues(typeof(ArmorType)))
            {
                foreach (DamageType type in Enum.GetValues(typeof(DamageType)))
                {
                    Assert.AreEqual(expected.DamageTable.Get(type, armor), fromAsset.DamageTable.Get(type, armor), 1e-5f, $"{type} vs {armor}");
                }
                Assert.AreEqual(expected.DamageTable.GetAntiArmor(armor), fromAsset.DamageTable.GetAntiArmor(armor), 1e-5f, $"Anti-armor vs {armor}");
            }
        }
    }
}
