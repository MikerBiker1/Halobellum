// SimRandomTests.cs
// Tests the seeded random number generator: same seed gives the same sequence, different seeds
// differ, and values stay in range.
// Depends on: Halobellum.Core, NUnit.

using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class SimRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new SimRandom(42);
            var b = new SimRandom(42);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void DifferentSeeds_DifferentSequences()
        {
            var a = new SimRandom(1);
            var b = new SimRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.NextFloat() == b.NextFloat()) same++;
            Assert.Less(same, 5);
        }

        [Test]
        public void NextFloat_IsBetweenZeroAndOne()
        {
            var r = new SimRandom(0);
            for (int i = 0; i < 10000; i++)
            {
                float f = r.NextFloat();
                Assert.GreaterOrEqual(f, 0f);
                Assert.Less(f, 1f);
            }
        }

        [Test]
        public void Chance_ZeroNever_OneAlways()
        {
            var r = new SimRandom(3);
            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(r.Chance(0f));
                Assert.IsTrue(r.Chance(1f));
            }
        }
    }
}
