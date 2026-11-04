// SkirmishSettingsTests.cs
// The settings of the Play page and the frame-rate counter (GameSettings): their defaults, the base health held inside its limits on a whole step, the slider's mapping, that
// the default is the Match Rules asset's own value, that the counter's cached value follows what is stored, and that Reset to defaults covers both. All in a hold, so that nothing reaches PlayerPrefs.
// Depends on: GameSettings, MainMenuHud, MatchRulesDefinition, Halobellum.Economy, NUnit, UnityEditor.

using Halobellum.Economy;
using Halobellum.Presentation;
using Halobellum.UI;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class SkirmishSettingsTests
    {
        [SetUp]
        public void HoldTheSettings() => GameSettings.HoldInMemory(startEmpty: true);

        [TearDown]
        public void LetGo() => GameSettings.ReleaseHold();

        [Test]
        public void TheDefaults_Are2000Health_AndNoCounter()
        {
            Assert.AreEqual(2000, GameSettings.SkirmishBaseHealth);
            Assert.IsFalse(GameSettings.ShowFrameRate, "The counter is behind a setting, and off until asked for.");
        }

        [Test]
        public void TheDefaultHealth_IsTheMatchRulesAssetsOwn()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MatchRulesDefinition>(MatchRulesDefinition.DefaultAssetPath);
            Assert.IsNotNull(asset, "Missing " + MatchRulesDefinition.DefaultAssetPath);
            Assert.AreEqual(asset.ToRules().BaseHealth, GameSettings.DefaultSkirmishBaseHealth);
        }

        [TestCase(-5, 100)]
        [TestCase(0, 100)]
        [TestCase(100, 100)]
        [TestCase(149, 100)]
        [TestCase(150, 200)]
        [TestCase(1549, 1500)]
        [TestCase(1550, 1600)]
        [TestCase(2000, 2000)]
        [TestCase(10000, 10000)]
        [TestCase(10001, 10000)]
        [TestCase(int.MaxValue, 10000)]
        public void TheBaseHealth_StaysInsideItsLimits_OnAWholeStep(int set, int expected)
        {
            GameSettings.SkirmishBaseHealth = set;
            Assert.AreEqual(expected, GameSettings.SkirmishBaseHealth);
        }

        [TestCase(0f, 100)]
        [TestCase(1f, 10000)]
        [TestCase(0.5f, 5100)] // 5050 is halfway between two steps.
        [TestCase(-1f, 100)]
        [TestCase(2f, 10000)]
        public void TheSlider_MapsToTheBaseHealth_SnappedToAStep(float position, int expected)
        {
            Assert.AreEqual(expected, MainMenuHud.BaseHealthFromSlider(position));
        }

        [Test]
        public void TheSlider_ReachesEveryStep_AndReadsItBack()
        {
            for (int health = MatchSetup.LeastBaseHealth; health <= MatchSetup.MostBaseHealth; health += MatchSetup.BaseHealthStep)
            {
                Assert.AreEqual(health, MainMenuHud.BaseHealthFromSlider(MainMenuHud.SliderFromBaseHealth(health)));
            }
        }

        [Test]
        public void TheBaseHealth_LastsAsLongAsItsHold()
        {
            GameSettings.SkirmishBaseHealth = 700;
            Assert.AreEqual(700, GameSettings.SkirmishBaseHealth);
            GameSettings.HoldInMemory(startEmpty: true);
            Assert.AreEqual(2000, GameSettings.SkirmishBaseHealth, "A new hold starts again.");
        }

        [Test]
        public void TheFrameRateSetting_RoundTrips()
        {
            GameSettings.ShowFrameRate = true;
            Assert.IsTrue(GameSettings.ShowFrameRate);
            GameSettings.ShowFrameRate = false;
            Assert.IsFalse(GameSettings.ShowFrameRate);
        }

        [Test]
        public void TheFrameRateCache_DoesNotOutliveItsHold()
        {
            GameSettings.ShowFrameRate = true;
            Assert.IsTrue(GameSettings.ShowFrameRate);
            GameSettings.HoldInMemory(startEmpty: true);
            Assert.IsFalse(GameSettings.ShowFrameRate, "Read again from what a new hold sees.");
        }

        [Test]
        public void NothingHereReachesTheSavedSettings_InAHold()
        {
            bool hadHealth = PlayerPrefs.HasKey(GameSettings.SkirmishBaseHealthKey), hadCounter = PlayerPrefs.HasKey(GameSettings.ShowFrameRateKey);
            int health = PlayerPrefs.GetInt(GameSettings.SkirmishBaseHealthKey, -1), counter = PlayerPrefs.GetInt(GameSettings.ShowFrameRateKey, -1);

            GameSettings.SkirmishBaseHealth = 700;
            GameSettings.ShowFrameRate = true;
            GameSettings.Save();

            Assert.AreEqual(hadHealth, PlayerPrefs.HasKey(GameSettings.SkirmishBaseHealthKey));
            Assert.AreEqual(hadCounter, PlayerPrefs.HasKey(GameSettings.ShowFrameRateKey));
            Assert.AreEqual(health, PlayerPrefs.GetInt(GameSettings.SkirmishBaseHealthKey, -1));
            Assert.AreEqual(counter, PlayerPrefs.GetInt(GameSettings.ShowFrameRateKey, -1));
        }

        [Test]
        public void ResetToDefaults_CoversBoth()
        {
            GameSettings.SkirmishBaseHealth = 700;
            GameSettings.ShowFrameRate = true;
            GameSettings.ResetToDefaults();
            Assert.AreEqual(2000, GameSettings.SkirmishBaseHealth);
            Assert.IsFalse(GameSettings.ShowFrameRate);
        }
    }
}
