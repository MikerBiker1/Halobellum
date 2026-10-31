// CombatRulesTests.cs
// Checks the arena size worked out from squares (decision D4, enlarged 2026-10-31): 80 squares
// wide, two 40-square deployment zones with 2 squares of no man's land between them, 2 m per square.
// Depends on: Halobellum.Core, NUnit.

using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class CombatRulesTests
    {
        [Test]
        public void Arena_IsEightyByTwoZonesOfFortyPlusTwo()
        {
            CombatRules rules = TestRules.Default();
            Assert.AreEqual(82, rules.ArenaLengthSquares);
            Assert.AreEqual(160f, rules.ArenaWidth, 1e-4f);
            Assert.AreEqual(164f, rules.ArenaLength, 1e-4f);
            Assert.AreEqual(2f, rules.DeploymentZoneFront, 1e-4f, "Each zone starts 1 square (2 m) from the centre line.");
        }

        [Test]
        public void Arena_FollowsTheSquareSize()
        {
            CombatRules rules = TestRules.Default();
            rules.SquareSize = 3f;
            Assert.AreEqual(240f, rules.ArenaWidth, 1e-4f);
            Assert.AreEqual(246f, rules.ArenaLength, 1e-4f);
        }
    }
}
