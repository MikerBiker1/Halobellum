// SettingsHeldForTests.cs
// While the PlayMode tests run, the settings stay in memory (GameSettings.HoldInMemory), since PlayerPrefs outlive a test and the editor's are shared with whoever
// presses Play in it. A test about what the sound code reads from PlayerPrefs itself puts that value back on its own.
// Depends on: Halobellum.Presentation, NUnit.

using Halobellum.Presentation;
using NUnit.Framework;

namespace Halobellum.Tests
{
    [SetUpFixture]
    public class SettingsHeldForTests
    {
        [OneTimeSetUp]
        public void Hold() => GameSettings.HoldInMemory();

        [OneTimeTearDown]
        public void LetGo() => GameSettings.ReleaseHold();
    }
}
