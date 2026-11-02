// CardArtTests.cs
// The art of the cards (M10): every unit has a picture or a fallback, an unknown unit falls back without an error, every
// kind of card has its border and ribbon, the borders' edges repeat cleanly, every spell has an emblem, and the card
// row fits 1 to 5 cards at every screen shape.
// Depends on: Halobellum.UI (UnitIcons, CardArt, CardLayout), Halobellum.Units, Halobellum.Core, NUnit, UnityEditor, UnityEngine.

using System;
using System.Collections.Generic;
using System.IO;
using Halobellum.Core;
using Halobellum.UI;
using Halobellum.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Halobellum.Tests
{
    public class CardArtTests
    {
        private static IEnumerable<T> AllAssets<T>() where T : UnityEngine.Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
                yield return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        }

        // ------------------------------------------------------------------ unit icons

        [Test]
        public void EveryUnit_HasAPictureOrAFallback()
        {
            var noFile = new List<string>();
            foreach (UnitDefinition unit in AllAssets<UnitDefinition>())
            {
                UnitStats stats = unit.ToStats();
                Texture2D icon = UnitIcons.For(stats);
                Assert.IsNotNull(icon, unit.unitName);
                Assert.Greater(icon.width, 0, unit.unitName);
                if (!UnitIcons.HasFile(unit.unitName)) noFile.Add(unit.unitName);
            }
            if (noFile.Count > 0) TestContext.WriteLine("Using the run-time fallback, until Render Unit Icons runs again: " + string.Join(", ", noFile));
        }

        [Test]
        public void EveryFactionsRoster_HasAPictureOrAFallback()
        {
            foreach (FactionDefinition faction in AllAssets<FactionDefinition>())
            {
                foreach (UnitDefinition unit in faction.baseRoster) Assert.IsNotNull(UnitIcons.For(unit.ToStats(), faction.factionName), unit.unitName);
                foreach (FactionDefinition.SubFactionEntry sub in faction.subFactions)
                {
                    foreach (UnitDefinition unit in sub.exclusiveUnits) Assert.IsNotNull(UnitIcons.For(unit.ToStats(), faction.factionName), unit.unitName);
                }
            }
        }

        [Test]
        public void AUnitNobodyHasSeen_FallsBack_WithoutAnError()
        {
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var stats = new UnitStats { Name = "A unit added tomorrow " + role, Role = role, Tier = 2, SquadSize = 3, CollisionRadius = 0.6f };
                Assert.IsFalse(UnitIcons.HasFile(stats.Name));
                foreach (string faction in new[] { "UNSC", "Covenant", "Flood", "Someone new", null })
                {
                    Texture2D icon = UnitIcons.For(stats, faction);
                    Assert.IsNotNull(icon);
                    Assert.AreEqual(UnitIcons.FallbackSize, icon.width);
                }
            }
            Assert.IsNotNull(UnitIcons.For(null), "Even no unit at all has a picture.");
        }

        [Test]
        public void TheFallback_HasABodyShapeForEveryKindOfUnit_AndItsLayersAreReadable()
        {
            foreach (UnitDefinition unit in AllAssets<UnitDefinition>())
            {
                string shape = UnitIcons.BodyShapeFor(unit.ToStats());
                var layer = Resources.Load<Texture2D>("CardArt/Layers/Body_" + shape);
                Assert.IsNotNull(layer, shape);
                Assert.IsTrue(layer.isReadable, shape + ": CardArtImport keeps the layers readable, because UnitIcons paints with them.");
            }
            foreach (UnitRole role in Enum.GetValues(typeof(UnitRole)))
            {
                var layer = Resources.Load<Texture2D>("CardArt/Layers/Role_" + role);
                Assert.IsNotNull(layer, role.ToString());
                Assert.IsTrue(layer.isReadable, role.ToString());
            }
        }

        [TestCase("Hunter Captain", "Hulk")]
        [TestCase("Goliath", "Hulk")]
        [TestCase("Reaver", "Walker")]
        [TestCase("Grunt Goblin", "Walker")]
        [TestCase("Infested Ghost", "Speeder")]
        [TestCase("Skitterer", "FloodSwarm")]
        [TestCase("Methane Wagon", "Pod")]
        public void AUnitWhoseStatsHideItsShape_IsNamed(string unitName, string shape)
        {
            foreach (UnitDefinition unit in AllAssets<UnitDefinition>())
            {
                if (unit.unitName == unitName)
                {
                    Assert.AreEqual(shape, UnitIcons.BodyShapeFor(unit.ToStats()));
                    return;
                }
            }
            Assert.Fail("No unit is called " + unitName);
        }

        [Test]
        public void UnitsThatShareABody_ButNotATint_HaveDifferentPictures()
        {
            var units = new List<UnitDefinition>();
            foreach (UnitDefinition unit in AllAssets<UnitDefinition>())
            {
                if (unit.bakedBody != null && unit.bakedBody.IsReady) units.Add(unit);
            }
            foreach (UnitDefinition a in units)
            {
                foreach (UnitDefinition b in units)
                {
                    if (a.bakedBody != b.bakedBody || a.bakedTint == b.bakedTint) continue;
                    CollectionAssert.AreNotEqual(IconFile(a), IconFile(b), $"{a.unitName} and {b.unitName} look different in battle. Run Render Unit Icons.");
                }
            }
        }

        private static byte[] IconFile(UnitDefinition unit) =>
            File.ReadAllBytes($"Assets/_Project/Resources/UnitIcons/{UnitIcons.FileName(unit.unitName)}.png");

        [TestCase(1920f, 8f)]  // 16:9 at 100%.
        [TestCase(1536f, 8f)]  // 16:9 at 125%: the bar moves right instead.
        [TestCase(1440f, 164f)] // 4:3.
        [TestCase(1350f, 164f)] // 5:4.
        public void TheSandboxItemsPanel_GoesUnderTheTopBar_OnANarrowWindow(float screenWidth, float top)
        {
            Assert.AreEqual(top, SandboxHud.ItemsPanelTop(screenWidth));
        }

        [Test]
        public void TheFallback_IsClearAtItsCorners_AndHasSomethingInTheMiddle()
        {
            Texture2D icon = UnitIcons.Compose(UnitRole.Tank, "Tank", "UNSC");
            Assert.AreEqual(0f, icon.GetPixel(0, icon.height - 1).a, 0.02f, "A clear background.");
            Assert.Greater(icon.GetPixel(icon.width / 2, icon.height / 2).a, 0.5f);
            UnityEngine.Object.DestroyImmediate(icon);
        }

        [Test]
        public void FileName_KeepsAUnitsNameReadable_AndDropsWhatAFileCantHave()
        {
            Assert.AreEqual("Jackal (Plasma Rifle)", UnitIcons.FileName("Jackal (Plasma Rifle)"));
            Assert.AreEqual("Hunter Pair", UnitIcons.FileName("Hunter: Pair"));
            Assert.AreEqual("A or B", UnitIcons.FileName("A/ or *B?"));
        }

        // ------------------------------------------------------------------ frames and emblems

        private const string FrameFolder = "Assets/_Project/Resources/CardArt/Frames/";

        [Test]
        public void EveryKind_HasItsFrameAndRibbon()
        {
            var seen = new HashSet<Texture2D>();
            foreach (CardFrameKind kind in Enum.GetValues(typeof(CardFrameKind)))
            {
                var frame = Resources.Load<Texture2D>("CardArt/Frames/Frame_" + kind);
                Assert.IsNotNull(frame, kind.ToString());
                Assert.IsTrue(seen.Add(frame));
                Assert.AreEqual(512, frame.width, "CardArt draws frames with fixed corners of a 512 pixel texture.");
                Assert.AreEqual(512, frame.height);
                Assert.IsNotNull(Resources.Load<Texture2D>("CardArt/Frames/Ribbon_" + kind), kind.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(CardArt.Label(kind)));
            }
        }

        [Test]
        public void TheRepeatingPartOfEveryFrameEdge_JoinsUpWithItself()
        {
            const int border = 160, size = 512, band = 90;
            foreach (CardFrameKind kind in Enum.GetValues(typeof(CardFrameKind)))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes($"{FrameFolder}Frame_{kind}.png")), kind.ToString());
                Color32[] pixels = texture.GetPixels32();
                Color32 At(int x, int y) => pixels[y * size + x];
                float worst = 0f;
                float worstAt = 0f;
                for (int across = 0; across < band; across++)
                {
                    // The first and last columns of the repeating part of the top and bottom edges, and rows of the left and right edges.
                    Check(At(border, across), At(size - border, across), across, ref worst, ref worstAt);
                    Check(At(border, size - 1 - across), At(size - border, size - 1 - across), across, ref worst, ref worstAt);
                    Check(At(across, border), At(across, size - border), across, ref worst, ref worstAt);
                    Check(At(size - 1 - across, border), At(size - 1 - across, size - border), across, ref worst, ref worstAt);
                }
                Assert.Less(worst, 60f, $"{kind}: the edge doesn't repeat cleanly, {worstAt:0} pixels in from the outside.");
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void Check(Color32 a, Color32 b, int across, ref float worst, ref float worstAt)
        {
            float difference = Mathf.Abs(a.a - b.a);
            if (a.a > 40 && b.a > 40) difference = Mathf.Max(difference, Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
            if (difference > worst)
            {
                worst = difference;
                worstAt = across;
            }
        }

        [Test]
        public void EverySpell_HasAnEmblem_AndMostHaveTheirOwn()
        {
            int own = 0, all = 0;
            foreach (SpellDefinition spell in AllAssets<SpellDefinition>())
            {
                SpellStats stats = spell.ToStats();
                CardStats card = CardStats.FromSpell(stats);
                Assert.IsNotNull(CardArt.EmblemFor(card, null), stats.Name);
                all++;
                if (Resources.Load<Texture2D>("CardArt/Emblems/Spell_" + CardArt.Compact(stats.Name)) != null) own++;
                Assert.AreEqual(stats.Offensive ? CardFrameKind.SpellOffensive : CardFrameKind.SpellDefensive, CardArt.KindOf(card), stats.Name);
            }
            Assert.Greater(all, 0);
            Assert.GreaterOrEqual(own, all / 2);
        }

        [Test]
        public void EveryCard_HasAKindAndAnEmblem_AndALargeVersionOfItsIcon()
        {
            foreach (CardDefinition definition in AllAssets<CardDefinition>())
            {
                CardStats card = definition.ToStats();
                Assert.AreNotEqual(CardFrameKind.Neutral, CardArt.KindOf(card), card.Name);
                Texture2D emblem = CardArt.EmblemFor(card, definition.icon);
                Assert.IsNotNull(emblem, card.Name);
                if (definition.icon != null) Assert.Greater(emblem.width, definition.icon.width, card.Name + " is drawn large on its card.");
            }
        }

        [Test]
        public void EachFaction_HasItsOwnFrame_AndAnUnknownOneTheNeutralFrame()
        {
            Assert.AreEqual(CardFrameKind.UNSC, CardArt.FactionFrame("UNSC"));
            Assert.AreEqual(CardFrameKind.Covenant, CardArt.FactionFrame("Covenant"));
            Assert.AreEqual(CardFrameKind.Flood, CardArt.FactionFrame("Flood"));
            Assert.AreEqual(CardFrameKind.Neutral, CardArt.FactionFrame("Someone new"));
            Assert.AreEqual(CardFrameKind.Neutral, CardArt.FactionFrame(null));
        }

        // ------------------------------------------------------------------ layout

        [Test]
        public void TheCardRow_Fits1To5Cards_AtEveryScreenShape()
        {
            // HUD units: 1080 tall, as wide as the screen's shape makes it: 16:9, 4:3, 5:4, 21:9; and a tiny window (scale 0.5).
            (float width, float height)[] screens = { (1920f, 1080f), (1440f, 1080f), (1350f, 1080f), (2560f, 1080f), (1600f, 1000f) };
            foreach ((float width, float height) in screens)
            {
                const float top = 110f;
                float bottom = height - 80f;
                for (int count = 1; count <= 5; count++)
                {
                    CardRow row = CardLayout.Arrange(count, width, top, bottom);
                    string label = $"{count} cards at {width} x {height}";
                    Assert.GreaterOrEqual(row.CardWidth, 240f, label);
                    Assert.LessOrEqual(row.CardWidth, CardLayout.MostWidth, label);
                    Assert.GreaterOrEqual(row.CardHeight, 500f, label);
                    for (int i = 0; i < count; i++)
                    {
                        Rect card = row.CardAt(i);
                        Assert.GreaterOrEqual(card.xMin, CardLayout.SideMargin - 0.01f, label);
                        Assert.LessOrEqual(card.xMax, width - CardLayout.SideMargin + 0.01f, label);
                        Assert.GreaterOrEqual(card.yMin, top - 0.01f, label);
                        Assert.LessOrEqual(card.yMax, bottom + 0.01f, label);
                        if (i > 0) Assert.GreaterOrEqual(card.xMin, row.CardAt(i - 1).xMax, label);
                    }
                    Assert.AreEqual(width * 0.5f, (row.CardAt(0).xMin + row.CardAt(count - 1).xMax) * 0.5f, 0.1f, label + " is centred");
                }
            }
        }
    }
}
