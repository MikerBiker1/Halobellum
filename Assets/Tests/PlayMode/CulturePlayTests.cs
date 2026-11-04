// CulturePlayTests.cs
// Decimals use a point in play mode, whatever language the PC is set to (M11): the culture is already right when these tests start, the test runner has kept it, and it
// still holds a frame later. Apply is also shown turning a comma back into a point, so that the tests mean something on a PC whose language already uses a point.
// In the editor EditorCulture sets the same culture first, so the culture being right says nothing about GameCulture's start-up hook. All these tests can show of the hook
// is that Unity is told to run it at the right moment and that it works when called. Whether it works in a real build is read from GameCulture's line in the player's log.
// Depends on: Halobellum.Presentation, NUnit, Unity Test Framework.

using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Halobellum.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Halobellum.Tests
{
    public class CulturePlayTests
    {
        private CultureInfo _current, _ui, _newThreads, _newThreadsUi;

        [SetUp]
        public void RememberTheCultures()
        {
            _current = CultureInfo.CurrentCulture;
            _ui = CultureInfo.CurrentUICulture;
            _newThreads = CultureInfo.DefaultThreadCurrentCulture;
            _newThreadsUi = CultureInfo.DefaultThreadCurrentUICulture;
        }

        // Puts back what was there rather than calling Apply, which would hide a missing hook from any test that runs after this one.
        [TearDown]
        public void PutTheCulturesBack()
        {
            CultureInfo.CurrentCulture = _current;
            CultureInfo.CurrentUICulture = _ui;
            CultureInfo.DefaultThreadCurrentCulture = _newThreads;
            CultureInfo.DefaultThreadCurrentUICulture = _newThreadsUi;
        }

        [Test]
        public void ADecimalIsWrittenWithAPoint()
        {
            Assert.AreEqual("1.5", $"{1.5f:0.0}");
        }

        [Test]
        public void EveryCultureIsSetWhenTheTestsStart()
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

        [Test]
        public void TheGameTurnsACommaIntoAPointAsItsVeryFirstStep()
        {
            MethodInfo hook = typeof(GameCulture).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).SingleOrDefault(method =>
                method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>()?.loadType == RuntimeInitializeLoadType.SubsystemRegistration);
            Assert.IsNotNull(hook, "GameCulture has a hook that Unity runs at SubsystemRegistration");

            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = comma;

            hook.Invoke(null, null);

            Assert.AreEqual("1.5", $"{1.5f:0.0}");
        }
    }
}
