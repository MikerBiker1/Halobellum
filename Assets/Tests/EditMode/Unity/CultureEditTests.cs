// CultureEditTests.cs
// Decimals use a point in the editor, whatever language the PC is set to (M11): EditorCulture has already run when these tests start, and the test runner has kept
// what it set. Apply is also shown turning a comma back into a point, so that the tests mean something on a PC whose language already uses a point.
// Depends on: Halobellum.Presentation, NUnit.

using System.Globalization;
using System.Threading;
using Halobellum.Presentation;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class CultureEditTests
    {
        [Test]
        public void ADecimalIsWrittenWithAPoint()
        {
            Assert.AreEqual("1.5", $"{1.5f:0.0}");
        }

        [Test]
        public void TheEditorHookHasSetEveryCulture()
        {
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.CurrentCulture);
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.CurrentUICulture);
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.DefaultThreadCurrentCulture);
            Assert.AreEqual(CultureInfo.InvariantCulture, CultureInfo.DefaultThreadCurrentUICulture);
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
