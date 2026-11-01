// UiThemeTests.cs
// Checks the faction UI themes (requested after M6): the UNSC, Covenant and neutral theme assets
// exist and look different (UNSC: cut corners and capital titles; Covenant: rounded, glowing),
// each faction asset points to its theme, and HudTheme switches themes and titles correctly.
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.UI (HudTheme), NUnit, UnityEditor.

using Halobellum.UI;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;

namespace Halobellum.Tests
{
    public class UiThemeTests
    {
        private static UiThemeDefinition Theme(string name) => AssetDatabase.LoadAssetAtPath<UiThemeDefinition>($"Assets/_Project/Data/UI/{name}_Theme.asset");

        [Test]
        public void ThreeThemes_WithDistinctLooks()
        {
            UiThemeDefinition unsc = Theme("UNSC"), covenant = Theme("Covenant"), neutral = Theme("Neutral");
            Assert.IsNotNull(unsc);
            Assert.IsNotNull(covenant);
            Assert.IsNotNull(neutral);
            Assert.AreEqual(PanelShape.Chamfered, unsc.panelShape);
            Assert.IsTrue(unsc.uppercaseTitles, "UNSC titles are in capitals.");
            Assert.AreEqual(PanelShape.Rounded, covenant.panelShape);
            Assert.Greater(covenant.glowSize, 0, "Covenant panels glow.");
            Assert.AreNotEqual(unsc.accentColor, covenant.accentColor);
            Assert.AreNotEqual(unsc.panelColor, covenant.panelColor);
            Assert.IsNotEmpty(unsc.osFontNames);
            Assert.IsNotEmpty(covenant.osFontNames);
            Assert.AreNotEqual(unsc.osFontNames[0], covenant.osFontNames[0], "Different fonts.");
        }

        [Test]
        public void EachFaction_UsesItsTheme()
        {
            var unsc = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/UNSC_Standard.asset");
            var covenant = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/Covenant_Standard.asset");
            Assert.AreSame(Theme("UNSC"), unsc.uiTheme);
            Assert.AreSame(Theme("Covenant"), covenant.uiTheme);
        }

        [Test]
        public void HudTheme_SwitchesThemes_AndStylesTitles()
        {
            UiThemeDefinition before = HudTheme.Current;
            try
            {
                HudTheme.Use(Theme("UNSC"));
                int version = HudTheme.Version;
                Assert.AreSame(Theme("UNSC"), HudTheme.Current);
                Assert.AreEqual("ROUND 3 RESULT", HudTheme.Title("Round 3 result"));
                HudTheme.Use(Theme("UNSC"));
                Assert.AreEqual(version, HudTheme.Version, "Using the same theme again changes nothing.");
                HudTheme.Use(Theme("Covenant"));
                Assert.Greater(HudTheme.Version, version);
                Assert.AreEqual("Round 3 result", HudTheme.Title("Round 3 result"));
                Assert.AreEqual(Theme("Covenant").accentColor, HudTheme.Accent);
            }
            finally
            {
                HudTheme.Use(before);
            }
        }
    }
}
