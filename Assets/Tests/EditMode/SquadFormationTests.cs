// SquadFormationTests.cs
// Tests squad formations: straight, perfectly aligned rows (1 row up to 4 bodies, 2 rows up to 12,
// 3 rows above), evenly spaced, and turned 90 degrees on request.
// Depends on: Halobellum.Combat, Halobellum.Core, NUnit.

using System.Collections.Generic;
using System.Linq;
using Halobellum.Combat;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SquadFormationTests
    {
        [TestCase(1, 1, 1)]
        [TestCase(2, 1, 2)]
        [TestCase(4, 1, 4)]
        [TestCase(6, 2, 3)]
        [TestCase(12, 2, 6)]
        [TestCase(18, 3, 6)]
        [TestCase(20, 3, 7)]
        public void RowsAndColumns(int squadSize, int rows, int columns)
        {
            Assert.AreEqual(rows, SquadFormation.Rows(squadSize));
            Assert.AreEqual(columns, SquadFormation.Columns(squadSize));
        }

        [Test]
        public void TwelveBodies_StandInTwoPerfectlyAlignedRowsOfSix()
        {
            List<Vec2> offsets = SquadFormation.Offsets(12, 0.5f, 0.5f, false);
            var rows = offsets.GroupBy(o => o.Y).OrderByDescending(g => g.Key).ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(1.5f, rows[0].Key - rows[1].Key, 1e-5f);

            float[] front = rows[0].Select(o => o.X).OrderBy(x => x).ToArray();
            float[] back = rows[1].Select(o => o.X).OrderBy(x => x).ToArray();
            Assert.AreEqual(6, front.Length);
            CollectionAssert.AreEqual(front, back, "Columns should line up exactly.");
            for (int i = 1; i < front.Length; i++) Assert.AreEqual(1.5f, front[i] - front[i - 1], 1e-5f);
        }

        [Test]
        public void Rotated90_SwapsWidthAndDepth()
        {
            List<Vec2> normal = SquadFormation.Offsets(12, 0.5f, 0.5f, false);
            List<Vec2> turned = SquadFormation.Offsets(12, 0.5f, 0.5f, true);
            float width = normal.Max(o => o.X) - normal.Min(o => o.X);
            float turnedDepth = turned.Max(o => o.Y) - turned.Min(o => o.Y);
            Assert.AreEqual(width, turnedDepth, 1e-5f);
        }
    }
}
