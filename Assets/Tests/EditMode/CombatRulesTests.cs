// CombatRulesTests.cs
// Checks the arena size worked out from squares (decision D4): 60 squares wide, two 30-square
// deployment zones with 2 squares of no man's land between them, 2 m per square.
// Depends on: Halobellum.Core, NUnit.

using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class CombatRulesTests
    {
        [Test]
        public void Arena_IsSixtyByTwoBlocksOfThirtyPlusTwo()
        {
            CombatRules rules = TestRules.Default();
            Assert.AreEqual(62, rules.ArenaLengthSquares);
            Assert.AreEqual(120f, rules.ArenaWidth, 1e-4f);
            Assert.AreEqual(124f, rules.ArenaLength, 1e-4f);
            Assert.AreEqual(2f, rules.DeploymentZoneFront, 1e-4f, "Each zone starts 1 square (2 m) from the centre line.");
        }

        [Test]
        public void Arena_FollowsTheSquareSize()
        {
            CombatRules rules = TestRules.Default();
            rules.SquareSize = 3f;
            Assert.AreEqual(180f, rules.ArenaWidth, 1e-4f);
            Assert.AreEqual(186f, rules.ArenaLength, 1e-4f);
        }
    }
}
