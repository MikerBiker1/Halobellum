// MenuFlowTests.cs
// The main menu's rules that need no running game (M10): where each test tool goes, what the settings default to and how they are kept
// in range, how the HUD fits a window, and that the build starts in the MainMenu scene with every scene it can reach.
// Settings tests put back whatever was stored, since PlayerPrefs outlive a test.
// Depends on: Halobellum.Presentation, Halobellum.UI, Halobellum.Editor (scene paths), NUnit.

using System.Linq;
using Halobellum.EditorTools;
using Halobellum.Presentation;
using Halobellum.UI;
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

        private float[] _floats;
        private int[] _ints;
        private bool[] _had;

        [SetUp]
        public void Remember()
        {
            _had = Keys.Select(PlayerPrefs.HasKey).ToArray();
            _floats = Keys.Select(k => PlayerPrefs.GetFloat(k, 0f)).ToArray();
            _ints = Keys.Select(k => PlayerPrefs.GetInt(k, 0)).ToArray();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
        }

        [TearDown]
        public void PutBack()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                PlayerPrefs.DeleteKey(Keys[i]);
                if (!_had[i]) continue;
                if (Keys[i] == GameSettings.CameraShakeKey) PlayerPrefs.SetInt(Keys[i], _ints[i]);
                else PlayerPrefs.SetFloat(Keys[i], _floats[i]);
            }
            GameSettings.UiScale = _had[5] ? _floats[5] : 1f;
            if (!_had[5]) PlayerPrefs.DeleteKey(GameSettings.UiScaleKey);
        }

        [TestCase("-autoplay", GameFlow.MatchScene)]
        [TestCase("-crowdBench", GameFlow.MatchScene)]
        [TestCase("-sandboxDemo", GameFlow.SandboxScene)]
        [TestCase("-gallery", GameFlow.SandboxScene)]
        [TestCase("-benchmark", GameFlow.BattleScene)]
        public void TestTools_SkipTheMenu(string flag, string scene)
        {
            Assert.AreEqual(scene, GameFlow.DirectSceneFor(new[] { "Halobellum.exe", flag, "somewhere" }));
            Assert.IsTrue(GameFlow.IsTestRun(new[] { flag }));
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
            GameSettings.MusicVolume = 0.25f;
            GameSettings.CameraShake = false;
            Assert.AreEqual(0.25f, PlayerPrefs.GetFloat("Volume.Music"));
            Assert.AreEqual(0, PlayerPrefs.GetInt("Fx.CameraShake"));
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
            PlayerPrefs.SetFloat(GameSettings.UiVolumeKey, 9f);
            Assert.AreEqual(1f, GameSettings.UiVolume, "A bad stored value is still kept in range.");
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
        public void UiScale_StaysInRange_AndOnItsSteps()
        {
            Assert.AreEqual(GameSettings.SmallestUiScale, GameSettings.ClampUiScale(0.1f));
            Assert.AreEqual(GameSettings.LargestUiScale, GameSettings.ClampUiScale(9f));
            Assert.AreEqual(1.25f, GameSettings.ClampUiScale(1.26f), 0.001f);
            Assert.AreEqual(1f, GameSettings.ClampUiScale(float.NaN));
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
