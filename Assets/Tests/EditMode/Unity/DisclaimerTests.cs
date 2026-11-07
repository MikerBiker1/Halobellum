// DisclaimerTests.cs
// The owner's disclaimer (D96): its words in order, shown once each time the game starts, before the menu, but never in a batch run or a
// picture run, unless asked for with -disclaimerPicture.
// Depends on: Halobellum.UI (Disclaimer), NUnit.

using System.Linq;
using Halobellum.UI;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class DisclaimerTests
    {
        [Test]
        public void ItSays_TheOwnersWords_InOrder()
        {
            string[] lines = Disclaimer.Text.ToArray();
            Assert.AreEqual("WELCOME TO", lines[0]);
            Assert.AreEqual(MainMenuHud.Title, lines[1], "HALOBELLUM, drawn as the menu's title.");
            StringAssert.Contains("Please do not publicly share screenshots, recordings, gameplay videos, download links or game files without permission. The less attention this unofficial project attracts, the better its chances of remaining available.",
                lines.First(l => l.StartsWith("As an unofficial fan creation")), "The owner's addition ends Public Availability (D100).");
            CollectionAssert.IsSubsetOf(new[] { "Unofficial Fan Project", "AI-Assisted Development", "Public Availability", "Thank you for playing, and enjoy the battle!" }, lines);
            StringAssert.Contains("Claude Opus 5.5", string.Join(" ", lines));
            StringAssert.Contains("Created by Miker", string.Join(" ", lines));
            StringAssert.EndsWith(Disclaimer.Link, lines.Last());
            Assert.AreEqual("https://www.xbox.com/en-US/developers/rules", Disclaimer.Link);
        }

        [Test]
        public void TheMenu_ShowsItsVersion() => Assert.AreEqual("Version " + UnityEngine.Application.version, MainMenuHud.VersionLabel);

        [Test]
        public void ABuildsVersion_IsItsBuildNumber_GivenOrCountedByGit()
        {
            Assert.AreEqual("378", Halobellum.EditorTools.PlayerBuildTool.BuildNumber(new[] { "-buildOutput", "x.exe", "-buildNumber", "378" }));
            string counted = Halobellum.EditorTools.PlayerBuildTool.BuildNumber(new string[0]);
            if (counted != null) Assert.Greater(int.Parse(counted), 0, "In a Git checkout, the commits are counted.");
        }

        [TestCase(false, new string[0], true)]
        [TestCase(false, new[] { "-screen-width", "1600" }, true)]
        [TestCase(true, new string[0], false)]
        [TestCase(false, new[] { "-sandboxDemo", "C:/shots" }, false)]
        [TestCase(false, new[] { "-cardsDemo", "C:/shots" }, false)]
        [TestCase(false, new[] { "-gallery" }, false)]
        [TestCase(true, new[] { "-disclaimerPicture", "C:/shot.png" }, true)]
        public void ItShows_ForAPlayer_NotForTestsOrPictureRuns(bool batch, string[] args, bool shows) => Assert.AreEqual(shows, Disclaimer.ShouldShow(batch, args));
    }
}
