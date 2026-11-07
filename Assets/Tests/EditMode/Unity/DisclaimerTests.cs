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
            Assert.AreEqual("WELCOME TO HALOBELLUM", lines[0]);
            CollectionAssert.IsSubsetOf(new[] { "Unofficial Fan Project", "AI-Assisted Development", "Public Availability", "Thank you for playing, and enjoy the battle!" }, lines);
            StringAssert.Contains("Claude Opus 5.5", string.Join(" ", lines));
            StringAssert.Contains("Created by Miker", string.Join(" ", lines));
            StringAssert.EndsWith(Disclaimer.Link, lines.Last());
            Assert.AreEqual("https://www.xbox.com/en-US/developers/rules", Disclaimer.Link);
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
