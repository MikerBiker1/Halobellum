// DisplayChoiceTests.cs
// Tests Settings > Screen's choosing, with made-up lists of screens: which is the main one, the first start, a saved choice surviving a change of order and a screen being gone,
// two screens of the same model, what the row says, and the size a window takes on a screen. The real move can only be checked in a build: see -screenDemo.
// Depends on: Halobellum.Core (DisplayChoice), NUnit.

using System.Collections.Generic;
using Halobellum.Core;
using NUnit.Framework;

namespace Halobellum.Tests
{
    public class DisplayChoiceTests
    {
        private static DisplayOption Dell(int place = 0) => new DisplayOption("DELL S3222HG", 1920, 1080, 1920, 1032, place);
        private static DisplayOption Lg(int place = 1) => new DisplayOption("LG ULTRAWIDE", 2560, 1080, 2560, 1032, place);
        private static DisplayOption Hp(int place = 2) => new DisplayOption("HP E242", 1920, 1200, 1920, 1152, place);

        /// <summary>Three screens, as Windows sees them with the Dell as its main one, in an order with the main one neither first nor last.</summary>
        private static List<DisplayOption> ThreeScreens() => DisplayChoice.MarkMain(new[] { Lg(0), Dell(1), Hp(2) }, 1920, 1080);

        // ------------------------------------------------------------------ the main screen

        [Test]
        public void TheMainScreen_IsTheOneOfTheSizeWindowsGives_WhereverItIsInTheList()
        {
            Assert.AreEqual(1, DisplayChoice.MainIndex(ThreeScreens()));
            Assert.AreEqual(0, DisplayChoice.MainIndex(DisplayChoice.MarkMain(new[] { Dell(0), Lg(1), Hp(2) }, 1920, 1080)));
            Assert.AreEqual(2, DisplayChoice.MainIndex(DisplayChoice.MarkMain(new[] { Lg(0), Hp(1), Dell(2) }, 1920, 1080)));
            Assert.AreEqual(1, DisplayChoice.MainIndex(DisplayChoice.MarkMain(new[] { Lg(0), Hp(1), Dell(2) }, 1920, 1200)));
        }

        [Test]
        public void TheMainScreen_IsTheFirstInTheList_IfWindowsGaveNoSizeThatIsThere()
        {
            Assert.AreEqual(0, DisplayChoice.MainIndex(DisplayChoice.MarkMain(new[] { Lg(0), Dell(1) }, 0, 0)));
            Assert.AreEqual(0, DisplayChoice.MainIndex(DisplayChoice.MarkMain(new[] { Lg(0), Dell(1) }, 1024, 768)));
        }

        [Test]
        public void OnlyOneScreen_IsMarkedMain()
        {
            var twoOfOneSize = DisplayChoice.MarkMain(new[] { new DisplayOption("A", 1920, 1080, 1920, 1032, 0), new DisplayOption("B", 1920, 1080, 1920, 1032, 1) }, 1920, 1080);
            Assert.IsTrue(twoOfOneSize[0].IsMain);
            Assert.IsFalse(twoOfOneSize[1].IsMain);
        }

        [Test]
        public void AnEmptyList_DoesNotFail()
        {
            Assert.AreEqual(0, DisplayChoice.MarkMain(new List<DisplayOption>(), 1920, 1080).Count);
            Assert.AreEqual(-1, DisplayChoice.MainIndex(new List<DisplayOption>()));
            Assert.AreEqual(-1, DisplayChoice.Choose(new List<DisplayOption>(), "x|1|1|0"));
        }

        // ------------------------------------------------------------------ the first start, and a saved choice

        [TestCase(null)]
        [TestCase("")]
        public void NeverChosen_GoesToTheMainScreen(string saved)
        {
            Assert.AreEqual(1, DisplayChoice.Choose(ThreeScreens(), saved));
        }

        [Test]
        public void ASavedChoice_IsFound_AfterTheOrderChanges()
        {
            string saved = DisplayChoice.SavedForm(Hp());
            Assert.AreEqual(2, DisplayChoice.Choose(ThreeScreens(), saved));
            Assert.AreEqual(0, DisplayChoice.Choose(new[] { Hp(0), Lg(1), Dell(2) }, saved));
        }

        [Test]
        public void ASavedScreen_AtAnotherResolution_IsStillThatScreen()
        {
            string saved = DisplayChoice.SavedForm(Lg());
            var lowered = new DisplayOption("LG ULTRAWIDE", 1920, 1080, 1920, 1032, 1);
            Assert.AreEqual(1, DisplayChoice.Choose(new[] { Dell(0).AsMain(), lowered }, saved));
        }

        [Test]
        public void ASavedScreen_ThatIsGone_FallsBackToTheMainScreen()
        {
            string saved = DisplayChoice.SavedForm(Hp());
            List<DisplayOption> left = DisplayChoice.MarkMain(new[] { Lg(0), Dell(1) }, 1920, 1080);
            Assert.AreEqual(-1, DisplayChoice.FindSaved(left, saved));
            Assert.AreEqual(1, DisplayChoice.Choose(left, saved));
        }

        [TestCase("nonsense")]
        [TestCase("a|b|c|d")]
        [TestCase("|||")]
        public void ASavedText_ThatMeansNothing_FallsBackToTheMainScreen(string saved)
        {
            Assert.AreEqual(1, DisplayChoice.Choose(ThreeScreens(), saved));
        }

        [Test]
        public void TwoScreensOfOneModel_AreToldApartByTheirPlace_WhileTheyKeepIt()
        {
            var first = new DisplayOption("HP E242", 1920, 1200, 1920, 1152, 1);
            var second = new DisplayOption("HP E242", 1920, 1200, 1920, 1152, 2);
            var list = new[] { Dell(0).AsMain(), first, second };
            Assert.AreEqual(1, DisplayChoice.Choose(list, DisplayChoice.SavedForm(first)));
            Assert.AreEqual(2, DisplayChoice.Choose(list, DisplayChoice.SavedForm(second)));
            Assert.AreEqual(0, DisplayChoice.Choose(new[] { second }, DisplayChoice.SavedForm(first)), "The same model and size, if the place is gone.");
        }

        [Test]
        public void AScreenWithNoName_IsOnlyKnownByItsSize()
        {
            var nameless = new DisplayOption("", 1280, 1024, 1280, 976, 1);
            var list = new[] { Dell(0).AsMain(), nameless };
            Assert.AreEqual(1, DisplayChoice.Choose(list, DisplayChoice.SavedForm(nameless)));
            var otherSize = new DisplayOption("", 1024, 768, 1024, 720, 1);
            Assert.AreEqual(0, DisplayChoice.Choose(new[] { Dell(0).AsMain(), otherSize }, DisplayChoice.SavedForm(nameless)), "Another size is another screen, as far as anyone can tell.");
        }

        [Test]
        public void AScreensName_WithTheSeparatorInIt_IsStillFound()
        {
            var odd = new DisplayOption("Maker|Model 3", 1920, 1080, 1920, 1032, 1);
            Assert.AreEqual(1, DisplayChoice.Choose(new[] { Dell(0).AsMain(), odd }, DisplayChoice.SavedForm(odd)));
        }

        // ------------------------------------------------------------------ the row

        [Test]
        public void TheList_StartsWithTheMainScreen_WhateverOrderItCameIn()
        {
            foreach (DisplayOption[] order in new[] { new[] { Lg(0), Dell(1), Hp(2) }, new[] { Hp(0), Dell(1), Lg(2) }, new[] { Dell(0), Hp(1), Lg(2) } })
            {
                List<DisplayOption> shown = DisplayChoice.Ordered(DisplayChoice.MarkMain(order, 1920, 1080));
                Assert.AreEqual("DELL S3222HG", shown[0].Name);
                Assert.AreEqual("HP E242", shown[1].Name);
                Assert.AreEqual("LG ULTRAWIDE", shown[2].Name);
            }
        }

        [Test]
        public void TheList_KeepsEachScreensPlaceInUnitysList()
        {
            List<DisplayOption> shown = DisplayChoice.Ordered(ThreeScreens());
            Assert.AreEqual(1, shown[0].LayoutIndex, "The move goes by the place it had in the list it came from.");
            Assert.AreEqual(2, shown[1].LayoutIndex);
            Assert.AreEqual(0, shown[2].LayoutIndex);
        }

        [Test]
        public void TheRowSays_NameAndSize_AndMarksTheMainScreen()
        {
            List<DisplayOption> shown = DisplayChoice.Ordered(ThreeScreens());
            Assert.AreEqual("DELL S3222HG  1920 x 1080  (main)", DisplayChoice.Label(shown, 0));
            Assert.AreEqual("HP E242  1920 x 1200", DisplayChoice.Label(shown, 1));
            Assert.AreEqual("LG ULTRAWIDE  2560 x 1080", DisplayChoice.Label(shown, 2));
        }

        [Test]
        public void TheRowSays_ANumberForASecondScreenOfOneModel_AndHasAWordForNoName()
        {
            var first = new DisplayOption("HP E242", 1920, 1200, 1920, 1152, 1);
            var second = new DisplayOption("HP E242", 1920, 1200, 1920, 1152, 2);
            var nameless = new DisplayOption("", 1280, 1024, 1280, 976, 3);
            List<DisplayOption> shown = DisplayChoice.Ordered(new[] { Dell(0).AsMain(), first, second, nameless });
            Assert.AreEqual("Screen 2  1280 x 1024", DisplayChoice.Label(shown, 1));
            Assert.AreEqual("HP E242 #1  1920 x 1200", DisplayChoice.Label(shown, 2));
            Assert.AreEqual("HP E242 #2  1920 x 1200", DisplayChoice.Label(shown, 3));
        }

        [Test]
        public void TheRowCanCutALongName_ButKeepsTheSizeAndTheMark()
        {
            DisplayOption longName = new DisplayOption("A very long name for a monitor", 1920, 1080, 1920, 1032).AsMain();
            Assert.AreEqual("A very...  1920 x 1080  (main)", DisplayChoice.Label(new[] { longName }, 0, 9));
        }

        // ------------------------------------------------------------------ the size on a screen

        [Test]
        public void AWindow_ThatFits_KeepsItsSize()
        {
            Assert.AreEqual((1600, 900), DisplayChoice.SizeOn(Dell(), 1600, 900, false, false));
        }

        [Test]
        public void AWindow_TooBigForTheScreen_IsCutToItsWorkArea_LeavingRoomForTheTitleBar()
        {
            // The window the owner's game remembers, 2560 x 1200, on his main screen.
            Assert.AreEqual((1920, 1032 - DisplayChoice.TitleBarRoom), DisplayChoice.SizeOn(Dell(), 2560, 1200, false, false));
        }

        [Test]
        public void Fullscreen_AtTheOldScreensOwnSize_TakesTheNewScreensOwnSize()
        {
            Assert.AreEqual((1920, 1200), DisplayChoice.SizeOn(Hp(), 1920, 1080, true, true));
            Assert.AreEqual((2560, 1080), DisplayChoice.SizeOn(Lg(), 1920, 1080, true, true));
        }

        [Test]
        public void Fullscreen_AtAChosenSize_KeepsIt_IfTheNewScreenCanShowIt()
        {
            Assert.AreEqual((1280, 720), DisplayChoice.SizeOn(Hp(), 1280, 720, true, false));
            Assert.AreEqual((1920, 1080), DisplayChoice.SizeOn(Dell(), 2560, 1080, true, false), "Too wide for this one.");
        }

        [Test]
        public void AWindow_GoesInTheMiddleOfTheWorkArea_WithItsTitleBarOnTheScreen()
        {
            Assert.AreEqual((160, 46), DisplayChoice.WindowPlace(Dell(), 1600, 900));
            Assert.AreEqual((0, 0), DisplayChoice.WindowPlace(Dell(), 1920, 1032 - DisplayChoice.TitleBarRoom));
        }
    }
}
