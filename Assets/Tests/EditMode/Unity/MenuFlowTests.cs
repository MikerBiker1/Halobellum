// MenuFlowTests.cs
// The main menu's rules that need no running game (M10): where each test tool goes, what the settings default to and how they are kept
// in range, how the HUD fits a window, and that the build starts in the MainMenu scene with every scene it can reach. Also how the HUD's parts
// keep clear of each other, the shop's order of tier then price, Unlock Skirmish's bar and hover window, the selection ring, and that the game's fonts keep a non-breaking space together.
// Settings tests hold the settings in memory, since PlayerPrefs outlive a test. The two that are about PlayerPrefs itself put back whatever was stored.
// Depends on: Halobellum.Presentation, Halobellum.UI, Halobellum.Core, Halobellum.Economy, Halobellum.Editor (scene paths), Halobellum.Units, NUnit.

using System;
using System.Collections.Generic;
using System.Linq;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.EditorTools;
using Halobellum.Presentation;
using Halobellum.UI;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class MenuFlowTests
    {
        private static readonly string[] Keys =
        {
            GameSettings.MasterVolumeKey, GameSettings.MusicVolumeKey, GameSettings.SfxVolumeKey, GameSettings.UiVolumeKey,
            GameSettings.CameraShakeKey, GameSettings.UiScaleKey,
        };

        [SetUp]
        public void HoldTheSettings() => GameSettings.HoldInMemory(startEmpty: true);

        [TearDown]
        public void LetGo() => GameSettings.ReleaseHold();

        [Test]
        public void TheChosenScreen_StartsEmpty_StaysInTheHold_AndResetToDefaultsLeavesIt()
        {
            bool savedBefore = PlayerPrefs.HasKey(GameSettings.ScreenKey);
            string valueBefore = PlayerPrefs.GetString(GameSettings.ScreenKey, "");
            Assert.AreEqual("", GameSettings.ChosenScreen, "Never chosen: the first start goes to the main screen.");

            GameSettings.ChosenScreen = "HP E242|1920|1200|2";
            GameSettings.ResetToDefaults();

            Assert.AreEqual("HP E242|1920|1200|2", GameSettings.ChosenScreen, "Reset to defaults leaves the window alone, and the screen with it.");
            Assert.AreEqual(savedBefore, PlayerPrefs.HasKey(GameSettings.ScreenKey), "Nothing reaches PlayerPrefs in a hold.");
            Assert.AreEqual(valueBefore, PlayerPrefs.GetString(GameSettings.ScreenKey, ""));
        }

        /// <summary>For the tests about PlayerPrefs itself: runs with no hold, and puts back what was stored.</summary>
        private static void WithTheRealStore(Action test)
        {
            GameSettings.ReleaseHold();
            bool[] had = Keys.Select(PlayerPrefs.HasKey).ToArray();
            float[] floats = Keys.Select(k => PlayerPrefs.GetFloat(k, 0f)).ToArray();
            int[] ints = Keys.Select(k => PlayerPrefs.GetInt(k, 0)).ToArray();
            try
            {
                test();
            }
            finally
            {
                for (int i = 0; i < Keys.Length; i++)
                {
                    PlayerPrefs.DeleteKey(Keys[i]);
                    if (!had[i]) continue;
                    if (Keys[i] == GameSettings.CameraShakeKey) PlayerPrefs.SetInt(Keys[i], ints[i]);
                    else PlayerPrefs.SetFloat(Keys[i], floats[i]);
                }
            }
        }

        [TestCase("-autoplay", GameFlow.MatchScene)]
        [TestCase("-crowdBench", GameFlow.MatchScene)]
        [TestCase("-sandboxDemo", GameFlow.SandboxScene)]
        [TestCase("-gallery", GameFlow.SandboxScene)]
        [TestCase("-benchmark", GameFlow.BattleScene)]
        [TestCase("-cardsDemo", GameFlow.MatchScene)]
        public void TestTools_SkipTheMenu(string flag, string scene)
        {
            Assert.AreEqual(scene, GameFlow.DirectSceneFor(new[] { "Halobellum.exe", flag, "somewhere" }));
            Assert.IsTrue(GameFlow.IsTestRun(new[] { flag }));
        }

        [TestCase("-menuDemo")]
        [TestCase("-terrainGallery")]
        [TestCase("-audioCapture")]
        public void TheOtherTestTools_CountAsTestRunsToo(string flag)
        {
            Assert.IsTrue(GameFlow.IsTestRun(new[] { "Halobellum.exe", flag, "somewhere" }));
        }

        [Test]
        public void WhileHeld_NothingReachesTheSavedSettings_AndLettingGoForgetsIt()
        {
            GameSettings.ReleaseHold();
            string[] floats = { GameSettings.MasterVolumeKey, GameSettings.MusicVolumeKey, GameSettings.SfxVolumeKey, GameSettings.UiVolumeKey, GameSettings.UiScaleKey };
            string[] ints = { GameSettings.CameraShakeKey, MatchController.DifficultyKey };
            string Saved() => string.Join(",", floats.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetFloat(k).ToString("R") : "none")) + "," + string.Join(",", ints.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k).ToString() : "none"));
            string before = Saved();
            float savedMusic = GameSettings.MusicVolume;
            AiDifficulty savedDifficulty = GameSettings.Difficulty;

            GameSettings.HoldInMemory();
            Assert.IsTrue(GameSettings.HeldInMemory);
            Assert.AreEqual(savedMusic, GameSettings.MusicVolume, "A hold starts from what was saved.");
            float changedMusic = savedMusic > 0.5f ? 0.125f : 0.875f;
            AiDifficulty changedDifficulty = savedDifficulty == AiDifficulty.Insane ? AiDifficulty.Easy : AiDifficulty.Insane; // Insane is the highest: it must get through the setting's limits (D123).
            GameSettings.MusicVolume = changedMusic;
            GameSettings.Difficulty = changedDifficulty;
            CameraShake.ShakeEnabled = !GameSettings.CameraShake; // What the K key does: it also saves.
            GameSettings.UiScale = 0.8f;
            Assert.AreEqual(changedMusic, GameSettings.MusicVolume, "The run sees its own changes.");
            Assert.AreEqual(changedDifficulty, GameSettings.Difficulty);
            Assert.AreEqual(0.8f, GameSettings.UiScale, 0.001f);
            Assert.AreEqual(before, Saved());
            GameSettings.ResetToDefaults();
            GameSettings.Save();
            Assert.AreEqual(before, Saved(), "Not even Reset to defaults gets through.");

            GameSettings.ReleaseHold();
            Assert.IsFalse(GameSettings.HeldInMemory);
            Assert.AreEqual(savedMusic, GameSettings.MusicVolume);
            Assert.AreEqual(savedDifficulty, GameSettings.Difficulty);
        }

        [Test]
        public void SandboxDemo_WinsOverAutoplay_AsItAlwaysHas()
        {
            Assert.AreEqual(GameFlow.SandboxScene, GameFlow.DirectSceneFor(new[] { "-autoplay", "-sandboxDemo", "x" }));
        }

        [Test]
        public void ANormalStart_AndTheMenuDemo_ShowTheMenu()
        {
            Assert.IsNull(GameFlow.DirectSceneFor(new[] { "Halobellum.exe" }));
            Assert.IsNull(GameFlow.DirectSceneFor(new[] { "Halobellum.exe", "-screen-width", "800" }));
            Assert.IsNull(GameFlow.DirectSceneFor(new[] { "Halobellum.exe", GameFlow.MenuDemoFlag, "x" }));
            Assert.IsTrue(GameFlow.IsTestRun(new[] { GameFlow.MenuDemoFlag }), "The demo must not change the saved window.");
            Assert.IsTrue(GameFlow.IsTestRun(new[] { GameFlow.ScreenDemoFlag }), "The screen demo must not move the window at start, or save the screen.");
            Assert.IsNull(GameFlow.DirectSceneFor(new[] { GameFlow.ScreenDemoFlag }), "It stays in the menu, which is what it tests.");
            Assert.IsFalse(GameFlow.IsTestRun(new[] { "Halobellum.exe" }));
        }

        [Test]
        public void OnlyTheBenchmarks_CountAsMeasuring()
        {
            Assert.IsTrue(GameFlow.IsMeasuring(new[] { "-crowdBench" }));
            Assert.IsTrue(GameFlow.IsMeasuring(new[] { "-benchmark" }));
            Assert.IsFalse(GameFlow.IsMeasuring(new[] { "-autoplay" }));
        }

        [Test]
        public void TheCommandLineWindow_IsNotUndoneBySavedSettings()
        {
            Assert.IsTrue(GameFlow.CommandLineSetsWindow(new[] { "-screen-width", "1600" }));
            Assert.IsTrue(GameFlow.CommandLineSetsWindow(new[] { "-screen-fullscreen", "0" }));
            Assert.IsTrue(GameFlow.CommandLineSetsWindow(new[] { "-monitor", "2" }), "Asking for a monitor on the command line also beats the saved screen.");
            Assert.IsFalse(GameFlow.CommandLineSetsWindow(new[] { "Halobellum.exe" }));
        }

        [Test]
        public void Settings_HaveTheAgreedKeysAndDefaults()
        {
            Assert.AreEqual("Volume.Master", GameSettings.MasterVolumeKey);
            Assert.AreEqual("Volume.Music", GameSettings.MusicVolumeKey);
            Assert.AreEqual("Volume.Sfx", GameSettings.SfxVolumeKey);
            Assert.AreEqual("Volume.Ui", GameSettings.UiVolumeKey);
            Assert.AreEqual("Fx.CameraShake", GameSettings.CameraShakeKey);
            Assert.AreEqual(1f, GameSettings.MasterVolume);
            Assert.AreEqual(0.6f, GameSettings.MusicVolume);
            Assert.AreEqual(1f, GameSettings.SfxVolume);
            Assert.AreEqual(1f, GameSettings.UiVolume);
            Assert.IsTrue(GameSettings.CameraShake);
        }

        [Test]
        public void Settings_AreWrittenToPlayerPrefs_AsTheSoundAndEffectsCodeReadThem()
        {
            WithTheRealStore(() =>
            {
                GameSettings.MusicVolume = 0.25f;
                GameSettings.CameraShake = false;
                Assert.AreEqual(0.25f, PlayerPrefs.GetFloat("Volume.Music"));
                Assert.AreEqual(0, PlayerPrefs.GetInt("Fx.CameraShake"));
            });
        }

        [Test]
        public void CameraShake_IsTheSettingsSwitch_ForTheKKeyAndForReset()
        {
            GameSettings.CameraShake = false;
            Assert.IsFalse(CameraShake.ShakeEnabled, "Off in Settings is off.");
            CameraShake.ShakeEnabled = true; // What the K key does.
            Assert.IsTrue(GameSettings.CameraShake, "Settings shows what K did.");
            CameraShake.ShakeEnabled = false;
            GameSettings.ResetToDefaults();
            Assert.IsTrue(CameraShake.ShakeEnabled, "Reset to defaults turns it back on.");
        }

        [Test]
        public void Volumes_StayBetween0And1()
        {
            GameSettings.MasterVolume = 3f;
            Assert.AreEqual(1f, GameSettings.MasterVolume);
            GameSettings.SfxVolume = -1f;
            Assert.AreEqual(0f, GameSettings.SfxVolume);
            WithTheRealStore(() =>
            {
                PlayerPrefs.SetFloat(GameSettings.UiVolumeKey, 9f);
                Assert.AreEqual(1f, GameSettings.UiVolume, "A bad stored value is still kept in range.");
            });
        }

        [Test]
        public void ResetToDefaults_PutsSoundAndEffectsBack()
        {
            GameSettings.MasterVolume = 0.1f;
            GameSettings.MusicVolume = 0.1f;
            GameSettings.CameraShake = false;
            GameSettings.UiScale = 1.25f;
            GameSettings.ResetToDefaults();
            Assert.AreEqual(1f, GameSettings.MasterVolume);
            Assert.AreEqual(0.6f, GameSettings.MusicVolume);
            Assert.IsTrue(GameSettings.CameraShake);
            Assert.AreEqual(1f, GameSettings.UiScale);
        }

        [Test]
        public void TheBattleLog_IsOnByDefault_AndCanBeTurnedOff()
        {
            Assert.IsTrue(GameSettings.BattleLog, "A game writes its log unless the player says not to (D69).");
            GameSettings.BattleLog = false;
            Assert.IsFalse(GameSettings.BattleLog);
            GameSettings.ResetToDefaults();
            Assert.IsTrue(GameSettings.BattleLog);
        }

        [Test]
        public void UiScale_StaysInRange_AndOnItsSteps()
        {
            Assert.AreEqual(GameSettings.SmallestUiScale, GameSettings.ClampUiScale(0.1f));
            Assert.AreEqual(GameSettings.LargestUiScale, GameSettings.ClampUiScale(9f));
            Assert.AreEqual(1.25f, GameSettings.ClampUiScale(1.26f), 0.001f);
            Assert.AreEqual(1f, GameSettings.ClampUiScale(float.NaN));
        }

        [TestCase(1280f, 1024f, 1.25f)]
        [TestCase(1024f, 819f, 1.25f)]
        [TestCase(1024f, 768f, 1.25f)]
        [TestCase(1280f, 1024f, 1.1f)]
        [TestCase(800f, 600f, 1f)]
        [TestCase(700f, 700f, 1f)]
        public void TheHud_KeepsItsWidestBarInsideTheWindow(float width, float height, float uiScale)
        {
            float units = HudScale.ScaledWidthFor(width, height, uiScale);
            Assert.GreaterOrEqual(units, 1120f + 2f * 10f - 0.01f);
        }

        [Test]
        public void TheHud_KeepsItsScaleWhereTheBarFitsAnyway()
        {
            Assert.AreEqual(1f, HudScale.ScaleFor(1920f, 1080f, 1f), 1e-5f);
            Assert.AreEqual(1.25f, HudScale.ScaleFor(1920f, 1080f, 1.25f), 1e-5f);
            Assert.AreEqual(1600f / 1080f, HudScale.ScaleFor(2560f, 1600f, 1f), 1e-5f);
            Assert.AreEqual(1024f / 1080f, HudScale.ScaleFor(1280f, 1024f, 1f), 1e-5f, "5:4 at 100% has room.");
            Assert.AreEqual(0.5f, HudScale.ScaleFor(800f, 500f, 1f), 1e-5f, "The floor for a tiny window.");
        }

        [TestCase(1140f)]
        [TestCase(1350f)]
        [TestCase(1440f)]
        [TestCase(1600f)]
        [TestCase(1920f)]
        [TestCase(2560f)]
        public void TheResultLine_ClearsTheSandboxBar(float width)
        {
            Rect line = BattleHud.ResultLine(width, SandboxHud.ResultLineTop);
            Assert.IsFalse(line.Overlaps(SandboxHud.TopBar(width)));
            Assert.GreaterOrEqual(line.x, 0f);
            Assert.LessOrEqual(line.xMax, width);
        }

        [TestCase(1140f)]
        [TestCase(1350f)]
        [TestCase(1600f)]
        [TestCase(1920f)]
        [TestCase(2560f)]
        public void TheSandboxMapButton_SitsUnderTheBar_ClearOfTheMessageAndTheUnitList(float width)
        {
            // D82: the bar is full, so the Map setting has a small panel of its own under the bar's right end.
            Rect bar = SandboxHud.TopBar(width), map = SandboxHud.MapButton(bar);
            var message = new Rect(width * 0.5f - 400f, SandboxHud.ResultLineTop, 800f, 30f);
            var list = new Rect(width - 410f, 204f, 400f, 600f);
            Assert.IsFalse(map.Overlaps(bar));
            Assert.IsFalse(map.Overlaps(message), "Clear of the message line.");
            Assert.IsFalse(map.Overlaps(list), "Clear of the unit list.");
            Assert.LessOrEqual(map.xMax, width);
        }

        [TestCase(1140f, 912f)]
        [TestCase(1152f, 864f)]
        [TestCase(1227f, 982f)]
        [TestCase(1450f, 864f)]
        [TestCase(1536f, 864f)]
        [TestCase(1920f, 1080f)]
        public void TheItemsPanel_KeepsClearOfATallSquadWindow(float width, float height)
        {
            const float panelHeight = 212f, windowHeight = 720f, windowWidth = 440f;
            var window = new Rect(10f, height - windowHeight - 10f, windowWidth, windowHeight);
            Rect panel = SandboxHud.ItemsPanel(width, panelHeight, window);
            Assert.IsFalse(panel.Overlaps(window), "It does not lie on the squad window.");
            Assert.IsFalse(panel.Overlaps(SandboxHud.TopBar(width)), "Nor on the bar.");
            Assert.LessOrEqual(panel.xMax, width);

            Rect alone = SandboxHud.ItemsPanel(width, panelHeight, null);
            Assert.AreEqual(10f, alone.x, "With no squad selected it stays in its place.");
            Assert.IsFalse(alone.Overlaps(SandboxHud.TopBar(width)));
            var small = new Rect(10f, alone.yMax + 10f, windowWidth, height - alone.yMax - 20f);
            Assert.AreEqual(alone, SandboxHud.ItemsPanel(width, panelHeight, small), "A squad window that leaves it room moves nothing.");
        }

        [TestCase(1140f)]
        [TestCase(1350f)]
        [TestCase(1440f)]
        public void TheSquadActions_StayClearOfTheSquadWindow_AndOnTheScreen(float width)
        {
            float left = SquadStatsWindow.ActionsPanelLeft(width, 520f);
            Assert.GreaterOrEqual(left, 450f);
            Assert.LessOrEqual(left + 520f, width);
        }

        [Test]
        public void TheSquadActions_AreCentred_WhereThereIsRoom()
        {
            Assert.AreEqual(700f, SquadStatsWindow.ActionsPanelLeft(1920f, 520f));
        }

        [Test]
        public void TheShop_IsInOrderOfTierThenPrice_WithoutTouchingTheRoster()
        {
            UnitStats One(string name, int tier, int cost) => new UnitStats { Name = name, Tier = tier, Cost = cost };
            UnitStats titan = One("titan", 3, 800), tank = One("tank", 2, 300), cheapTier2 = One("cheapTier2", 2, 150), marine = One("marine", 1, 100), dearTier1 = One("dearTier1", 1, 200), sameAsMarine = One("sameAsMarine", 1, 100);
            var roster = new List<UnitStats> { titan, tank, dearTier1, cheapTier2, marine, sameAsMarine };
            var rows = new List<UnitStats> { titan };
            MatchHud.FillShopRows(roster, rows);
            CollectionAssert.AreEqual(new[] { marine, sameAsMarine, dearTier1, cheapTier2, tank, titan }, rows, "A dear unit of a low tier comes before a cheap one of the next; equals keep their order.");
            CollectionAssert.AreEqual(new[] { titan, tank, dearTier1, cheapTier2, marine, sameAsMarine }, roster, "The roster keeps its order for the cards and the AI.");
        }

        [TestCase(1920f, 8)]
        [TestCase(1536f, 5)]
        public void TheUnitBar_FitsAsManyIconsAsTheScaleLeavesRoomFor(float width, int columns)
        {
            Assert.AreEqual(columns, MatchHud.UnitBarColumns(width));
            Rect bar = MatchHud.UnitBarRect(width, width / 16f * 9f, 3 * columns);
            Assert.AreEqual(width - 112f, bar.xMax, "It stops short of the Menu button.");
            Assert.GreaterOrEqual(bar.x, SquadStatsWindow.ActionsPanelLeft(width, 520f) + 520f, "It keeps clear of Move / Turn / Sell.");
            Assert.AreEqual(width / 16f * 9f - 10f, bar.yMax);
        }

        [Test]
        public void TheUnitBar_GrowsUpward_AndScrollsBeforeItReachesTheTopBar()
        {
            Rect one = MatchHud.UnitBarRect(1920f, 1080f, 2), three = MatchHud.UnitBarRect(1920f, 1080f, 24);
            Assert.AreEqual(578f, one.width);
            Assert.AreEqual(one.yMax, three.yMax);
            Assert.Less(three.y, one.y);
            Assert.GreaterOrEqual(MatchHud.UnitBarRect(1536f, 864f, 69).y, 110f, "Every unit unlocked, at the largest UI scale.");
        }

        [Test]
        public void TheUnitBar_StandsAboveTheSquadActions_InANarrowWindow()
        {
            Rect bar = MatchHud.UnitBarRect(1140f, 912f, 8);
            Assert.AreEqual(4, MatchHud.UnitBarColumns(1140f));
            Assert.LessOrEqual(bar.yMax, 912f - 70f);
        }

        [TestCase(1920f, 1080f, 1f)]
        [TestCase(1920f, 1080f, 1.05f)]
        [TestCase(1920f, 1080f, 1.25f)]
        [TestCase(1280f, 1024f, 1.25f)]
        [TestCase(1024f, 768f, 1.25f)]
        [TestCase(1000f, 1080f, 1.25f)]
        [TestCase(800f, 500f, 1f)]
        public void TheUnlockPagesList_StopsAboveTheMenuButton(float screenWidth, float screenHeight, float uiScale)
        {
            float scale = HudScale.ScaleFor(screenWidth, screenHeight, uiScale);
            Rect menu = PauseMenu.CornerButtonPixels(screenWidth, screenHeight, uiScale);
            Rect page = MatchHud.UnlockPageRect(screenWidth / scale, screenHeight / scale);
            Rect list = MatchHud.UnlockPageListRect(page, menu.y / scale);
            Assert.LessOrEqual(list.yMax * scale, menu.y + 0.01f, "A scroll bar or a cell under that button would lose its clicks to it.");
            Assert.LessOrEqual(list.yMax, page.yMax, "It stays on the page.");
            Assert.Greater(list.height, 300f);
        }

        [Test]
        public void TheMenuButton_IsWhereItWasDrawn_InTheMenusOwnScale()
        {
            Assert.AreEqual(new Rect(1814f, 1036f, 96f, 34f), PauseMenu.CornerButtonPixels(1920f, 1080f, 1f));
            // Asked for at 1.25, the menus fit the Settings panel's height here (D94 made it a row taller), and the button goes with them.
            Rect narrow = PauseMenu.CornerButtonPixels(1000f, 1080f, 1.25f);
            float fit = Mathf.Min(1.25f, 1080f / (SettingsScreen.PanelHeight + 20f));
            Assert.AreEqual(1080f - 44f * fit, narrow.y, 0.01f);
        }

        [Test]
        public void AHoveredUnitsWindow_GoesAboveTheBar_OrBesideItIfTooTall()
        {
            Rect above = SquadStatsWindow.HoverRect(440f, 300f, 1808f, 700f, 1224f, 1080f);
            Assert.AreEqual(new Rect(1368f, 400f, 440f, 300f), above);
            Rect beside = SquadStatsWindow.HoverRect(440f, 580f, 1424f, 532f, 1050f, 864f);
            Assert.AreEqual(new Rect(610f, 274f, 440f, 580f), beside, "At the screen's bottom, left of the bar.");
            Assert.AreEqual(0f, SquadStatsWindow.HoverRect(440f, 2000f, 1424f, 532f, 1050f, 864f).y, "Its top stays on the screen.");
        }

        [Test]
        public void TheShopsStatsWindow_ShowsLevel1_WithEveryTechLocked()
        {
            var tech = new TechUpgrade { Name = "Plates" };
            SquadProgressView view = SquadStatsWindow.ShopProgress(new UnitStats { Name = "tank", TechUpgrades = new[] { tech } });
            Assert.AreEqual(1, view.Level);
            Assert.IsFalse(view.ShowXp);
            Assert.IsNull(view.LevelUpLabel);
            Assert.AreEqual(1, view.Techs.Count);
            Assert.IsTrue(view.Techs[0].Locked);
            Assert.IsFalse(view.Techs[0].Owned);
            Assert.IsNull(view.Techs[0].ButtonLabel, "It can't be bought from here.");
            StringAssert.Contains("In the shop", view.SideLabel);
            StringAssert.DoesNotContain("shop", SquadStatsWindow.ShopProgress(new UnitStats { Name = "tank" }, hovered: true).SideLabel, "Unlock Skirmish has no shop.");
        }

        [Test]
        public void TheSelectionRing_IsOpenInTheMiddle_AndFacesUp()
        {
            Mesh ring = PlaceholderArt.Ring;
            Assert.Greater(ring.vertexCount, 0);
            foreach (Vector3 vertex in ring.vertices)
            {
                Assert.AreEqual(0f, vertex.y);
                Assert.GreaterOrEqual(vertex.magnitude, 0.5f * PlaceholderArt.RingHoleShare - 1e-4f, "Nothing covers the middle.");
                Assert.LessOrEqual(vertex.magnitude, 0.5f + 1e-4f);
            }
            Vector3[] v = ring.vertices;
            int[] t = ring.triangles;
            for (int i = 0; i < t.Length; i += 3)
                Assert.Greater(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).y, 0f, "Seen from above, or it would not be drawn.");
            foreach (Vector3 normal in ring.normals) Assert.AreEqual(Vector3.up, normal);
        }

        private static IEnumerable<(string name, Font font)> GameFonts()
        {
            foreach (string theme in new[] { "UNSC_Theme", "Covenant_Theme", "Flood_Theme" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<UiThemeDefinition>($"Assets/_Project/Data/UI/{theme}.asset");
                Assert.IsNotNull(definition, theme);
                yield return (theme, definition.fontAsset);
            }
            var look = Resources.Load<MenuLook>(MenuLook.ResourcePath);
            yield return ("menu text", look.textFont);
            yield return ("the default font", null);
        }

        [Test]
        public void TheGamesFonts_KeepANonBreakingSpaceTogether_AndGiveItAWidth()
        {
            foreach ((string name, Font font) in GameFonts())
            {
                var line = new GUIStyle { font = font, fontSize = 18, wordWrap = false };
                float plain = line.CalcSize(new GUIContent("in 8 m")).x;
                float joined = line.CalcSize(new GUIContent("in 8\u00A0m")).x;
                TestContext.WriteLine($"{name}: 'in 8 m' is {plain} wide with a space, {joined} with a non-breaking one.");
                Assert.Greater(plain, 0f, name);
                Assert.AreEqual(plain, joined, 1.5f, name + ": a non-breaking space looks like a space, not a missing letter.");

                // At a width that holds two of these words: plain spaces make 2 lines of 2. If the middle space holds, the two words it joins need a line of their own, which makes 3.
                var wrap = new GUIStyle(line) { wordWrap = true };
                float room = line.CalcSize(new GUIContent("aa aa a")).x; // With room for the space at the end of a line.
                float plainHeight = wrap.CalcHeight(new GUIContent("aa aa aa aa"), room);
                float joinedHeight = wrap.CalcHeight(new GUIContent("aa aa\u00A0aa aa"), room);
                TestContext.WriteLine($"{name}: wrapped at {room}, {plainHeight} high with plain spaces, {joinedHeight} with a non-breaking one.");
                Assert.AreEqual(wrap.CalcHeight(new GUIContent("aa"), room) * 2f, plainHeight, 3f, name + ": plain spaces make 2 lines, or this test says nothing.");
                Assert.Greater(joinedHeight, plainHeight + 4f, name + ": the line breaks at the plain spaces only.");
            }
        }

        [Test]
        public void TheBuild_StartsInTheMainMenu_WithEveryScene()
        {
            string[] enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.AreEqual(BattleSceneBuilder.MainMenuScenePath, enabled[0], "The first scene of the build.");
            StringAssert.EndsWith("/MainMenu.unity", BattleSceneBuilder.MainMenuScenePath);
            Assert.AreEqual(BattleSceneBuilder.MainMenuScenePath, BattleSceneBuilder.GameScenes[0], "The first scene of a command-line build.");
            foreach (string path in BattleSceneBuilder.GameScenes)
            {
                CollectionAssert.Contains(enabled, path);
                Assert.IsTrue(System.IO.File.Exists(path), path);
            }
            CollectionAssert.Contains(enabled, BattleSceneBuilder.ClassicMenuScenePath, "The classic menu is kept in the build (D94).");
        }

        [Test]
        public void TheMenuChosenInSettings_Opens_ButATestToolAlwaysHasTheCinematicOne()
        {
            Assert.AreEqual(GameFlow.MainMenuScene, GameFlow.MenuSceneFor(false, new string[0]));
            Assert.AreEqual(GameFlow.ClassicMainMenuScene, GameFlow.MenuSceneFor(true, new string[0]));
            Assert.AreEqual(GameFlow.MainMenuScene, GameFlow.MenuSceneFor(true, new[] { GameFlow.MenuDemoFlag }));
            Assert.IsTrue(GameFlow.IsMainMenu(GameFlow.MainMenuScene));
            Assert.IsTrue(GameFlow.IsMainMenu(GameFlow.ClassicMainMenuScene));
            Assert.IsFalse(GameFlow.IsMainMenu(GameFlow.MatchScene));

            Assert.IsFalse(GameSettings.ClassicMenu, "The cinematic menu unless the player chooses the classic.");
            GameSettings.ClassicMenu = true;
            Assert.IsTrue(GameSettings.ClassicMenu);
            GameSettings.ClassicMenu = false;
            Assert.IsFalse(GameSettings.ClassicMenu);
        }

        [Test]
        public void TheMenuLook_HasItsFonts()
        {
            var look = Resources.Load<MenuLook>(MenuLook.ResourcePath);
            Assert.IsNotNull(look, "Resources/MenuLook is made by MainMenuSceneBuilder.");
            Assert.IsNotNull(look.titleFont);
            Assert.IsNotNull(look.headingFont);
            Assert.IsNotNull(look.textFont);
        }
    }
}
