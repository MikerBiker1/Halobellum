// PlaceholderRoundRobinTests.cs
// Checks the M1 acceptance setup: the three placeholder unit assets load and have valid data, and
// the headless runner can simulate them against each other using the real CombatRules asset.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.EditorTools (HeadlessRunnerTool), Halobellum.Combat, Halobellum.Units, NUnit.

using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.EditorTools;
using Halobellum.Units;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class PlaceholderRoundRobinTests
    {
        [Test]
        public void ThreePlaceholderUnits_LoadWithValidData()
        {
            List<UnitDefinition> units = HeadlessRunnerTool.LoadUnitsInFolder(HeadlessRunnerTool.PlaceholderFolder);
            Assert.AreEqual(3, units.Count);
            foreach (UnitDefinition unit in units)
            {
                CollectionAssert.IsEmpty(unit.ToStats().Validate(), unit.name);
            }
        }

        [Test]
        public void PlaceholderRoundRobin_SimulatesEveryPair()
        {
            var rules = HeadlessRunnerTool.LoadRules();
            var armies = new List<ArmySetup>();
            foreach (UnitDefinition unit in HeadlessRunnerTool.LoadUnitsInFolder(HeadlessRunnerTool.PlaceholderFolder))
            {
                armies.Add(ArmySetup.ForBudget(HeadlessRunnerTool.ValidatedStats(unit), HeadlessRunnerTool.DefaultBudget));
            }

            List<MatchupSummary> summaries = HeadlessBattleRunner.RunRoundRobin(rules, armies, HeadlessRunnerTool.DefaultSettings(battles: 2));
            Assert.AreEqual(3, summaries.Count);
            foreach (MatchupSummary summary in summaries)
            {
                Assert.AreEqual(2, summary.TeamAWins + summary.TeamBWins + summary.Draws);
                Assert.Greater(summary.AverageDamageDealtA + summary.AverageDamageDealtB, 0f, $"{summary.ArmyA} vs {summary.ArmyB} never fought.");
            }
        }
    }
}
