// TechIconsTests.cs
// The small tech icons drawn in code (D145): every kind has its own 64 x 64 picture, a white sign on a dark disc, see-through outside
// the disc. With HB_TECH_ICON_SHEET set to a file path, the test also saves a sheet of them, large and at their size in the game, to look at.

using System;
using System.Collections.Generic;
using System.IO;
using Halobellum.Core;
using Halobellum.UI;
using NUnit.Framework;
using UnityEngine;

namespace Halobellum.Tests
{
    public class TechIconsTests
    {
        [Test]
        public void EveryKind_HasItsOwnPicture_ASignOnADisc()
        {
            var seen = new HashSet<string>();
            foreach (TechIconKind kind in Enum.GetValues(typeof(TechIconKind)))
            {
                Texture2D icon = TechIcons.Icon(kind);
                Assert.AreEqual(64, icon.width, kind.ToString());
                Assert.AreSame(icon, TechIcons.Icon(kind), "Drawn once and kept.");
                Color32[] pixels = icon.GetPixels32();
                Assert.AreEqual(0, pixels[0].a, $"{kind}: clear outside the disc.");
                int white = 0;
                foreach (Color32 p in pixels) if (p.a > 200 && p.r > 200) white++;
                Assert.Greater(white, 200, $"{kind}: a white sign on it.");
                Assert.IsTrue(seen.Add(Convert.ToBase64String(icon.GetRawTextureData())), $"{kind}: a picture of its own.");
            }
            string sheet = Environment.GetEnvironmentVariable("HB_TECH_ICON_SHEET");
            if (!string.IsNullOrEmpty(sheet)) SaveSheet(sheet);
        }

        /// <summary>Each icon at 128 px and at 20 px (its size over a squad), on a grassy colour.</summary>
        private static void SaveSheet(string path)
        {
            var kinds = (TechIconKind[])Enum.GetValues(typeof(TechIconKind));
            const int cell = 140;
            var sheet = new Texture2D(kinds.Length * cell, 200, TextureFormat.RGBA32, false);
            var ground = new Color(0.36f, 0.42f, 0.28f, 1f);
            var fill = new Color[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = ground;
            sheet.SetPixels(fill);
            for (int k = 0; k < kinds.Length; k++)
            {
                Texture2D icon = TechIcons.Icon(kinds[k]);
                Blit(sheet, icon, k * cell + 6, 60, 128);
                Blit(sheet, icon, k * cell + 60, 14, 20);
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
        }

        private static void Blit(Texture2D into, Texture2D icon, int x0, int y0, int size)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = icon.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size, Mathf.Max(0, Mathf.RoundToInt(Mathf.Log(64f / size, 2f))));
                    Color under = into.GetPixel(x0 + x, y0 + y);
                    into.SetPixel(x0 + x, y0 + y, Color.Lerp(under, new Color(c.r, c.g, c.b, 1f), c.a));
                }
            }
        }
    }
}
