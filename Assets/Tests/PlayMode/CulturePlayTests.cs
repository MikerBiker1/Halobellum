// CulturePlayTests.cs
// Decimals use a point in the running game, whatever language the PC is set to (M11): GameCulture's start-up hook has already run when these tests start, the test runner
// has kept what it set, and it still holds a frame later. Apply is also shown turning a comma back into a point, so that the tests mean something on a PC whose language already uses a point.
// Depends on: Halobellum.Presentation, NUnit, Unity Test Framework.

using System.Collections;
using System.Globalization;
using System.Threading;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class CulturePlayTests
    {
        [Test]
        public void ADecimalIsWrittenWithAPoint()
        {
            Assert.AreEqual("1.5", $"{1.5f:0.0}");
        }

        [Test]
        public void TheStartUpHookHasSetEveryCulture()
        {
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.CurrentUICulture);
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.DefaultThreadCurrentCulture);
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.DefaultThreadCurrentUICulture);
        }

        [UnityTest]
        public IEnumerator ADecimalStillHasAPointAFrameLater()
        {
            yield return null;
            Assert.AreEqual("1.5", $"{1.5f:0.0}");
        }

        [Test]
        public void ApplyTurnsACommaBackIntoAPointOnThisThreadAndNewOnes()
        {
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            try
            {
                CultureInfo.CurrentCulture = comma;
                CultureInfo.DefaultThreadCurrentCulture = comma;
                Assert.AreEqual("1,5", $"{1.5f:0.0}", "the comma culture took hold, so the next check proves something");

                GameCulture.Apply();

                Assert.AreEqual("1.5", $"{1.5f:0.0}");
                string onNewThread = null;
                var thread = new Thread(() => onNewThread = $"{1.5f:0.0}");
                thread.Start();
                thread.Join();
                Assert.AreEqual("1.5", onNewThread);
            }
            finally
            {
                GameCulture.Apply();
            }
        }
    }
}
