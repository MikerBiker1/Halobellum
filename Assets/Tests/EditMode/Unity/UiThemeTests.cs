// UiThemeTests.cs
// Checks the faction UI themes (requested after M6): the UNSC, Covenant and neutral theme assets
// exist and look different (UNSC: cut corners and capital titles; Covenant: rounded, glowing),
// each faction asset points to its theme, and HudTheme switches themes and titles correctly.
// Decision D28: the UNSC and Covenant themes are textured (Art/UI) and use font files with their
// licences; the neutral theme is the blueprint (D68).
// Needs the Unity editor (AssetDatabase), so it runs only in Unity's Test Runner.
// Depends on: Halobellum.Units, Halobellum.UI (HudTheme), NUnit, UnityEditor.

using Halobellum.UI;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        [TestCase("UNSC")]
        [TestCase("Covenant")]
        [TestCase("Flood")]
        public void FactionThemes_AreTextured_WithFontFiles(string name)
        {
            // Decision D28: textured, ship-inspired panels and real font files (free OFL fonts).
            UiThemeDefinition theme = Theme(name);
            foreach (Texture2D texture in new[] { theme.panelFrame, theme.panelDetail, theme.panelOrnament, theme.rowFrame, theme.barFill, theme.buttonNormal, theme.buttonHover, theme.buttonPressed })
            {
                Assert.IsNotNull(texture, $"{name}: every texture slot is filled.");
                Assert.AreEqual(1, texture.mipmapCount, $"{texture.name}: UI textures have no mipmaps (Art/UI import rule).");
            }
            Assert.AreEqual(TextureWrapMode.Repeat, theme.panelDetail.wrapMode, "The hull texture tiles.");
            Assert.AreEqual(TextureWrapMode.Clamp, theme.panelFrame.wrapMode, "Frames are 9-sliced, not tiled.");
            Assert.Less(2 * theme.panelFrameBorder, theme.panelFrame.width, "The frame's corners leave a middle to stretch.");
            Assert.Less(2 * theme.buttonBorder, theme.buttonNormal.height);
            Assert.IsNotNull(theme.fontAsset);
            Assert.IsNotNull(theme.titleFontAsset);
            foreach (Font font in new[] { theme.fontAsset, theme.titleFontAsset })
            {
                string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(font));
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(folder, "OFL.txt")), $"{font.name}: its licence ships with it.");
            }
        }

        [Test]
        public void FactionThemes_LookDifferent_AndNeutralIsTheBlueprint()
        {
            UiThemeDefinition unsc = Theme("UNSC"), covenant = Theme("Covenant"), neutral = Theme("Neutral");
            Assert.AreNotSame(unsc.panelFrame, covenant.panelFrame);
            Assert.AreNotSame(unsc.fontAsset, covenant.fontAsset);
            Assert.AreNotSame(unsc.titleFontAsset, covenant.titleFontAsset);
            Assert.AreEqual("Blueprint_PanelFrame", neutral.panelFrame.name, "Unlock Skirmish, the draft screen and the sandbox are drawn as a blueprint (D68).");
            Assert.AreEqual("Blueprint_Grid_Tile", neutral.panelDetail.name);
            Assert.AreEqual("Blueprint_Button", neutral.buttonNormal.name);
            Assert.AreNotSame(unsc.panelFrame, neutral.panelFrame);
        }

        [Test]
        public void EachFaction_UsesItsTheme()
        {
            var unsc = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/UNSC_Standard.asset");
            var covenant = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/Covenant_Standard.asset");
            Assert.AreSame(Theme("UNSC"), unsc.uiTheme);
            Assert.AreSame(Theme("Covenant"), covenant.uiTheme);
            var flood = AssetDatabase.LoadAssetAtPath<FactionDefinition>("Assets/_Project/Data/Factions/Flood_Standard.asset");
            Assert.AreSame(Theme("Flood"), flood.uiTheme);
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
