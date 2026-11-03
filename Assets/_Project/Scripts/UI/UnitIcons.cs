// UnitIcons.cs
// A picture of every unit for the card screens, the shop and the squad window (M10). UnitIcons.For(unit) never fails:
//   1. Resources/UnitIcons/<unit name>.png, if there is one. The Unit Icon Renderer (Halobellum > Art > Render Unit Icons,
//      an editor tool) renders those from real models, and writes a designed fallback for units that are still placeholders.
//   2. Otherwise (a unit added since the tool last ran) the same fallback is put together here, once, and kept.
// The fallback is a dark shape of the unit's kind of body, edged and backed with its faction's colour, with its role's
// emblem in a badge. The layers are in Resources/CardArt/Layers, drawn by Tools/UI/make_card_art.py. Units are looked
// up by name, so new units need nothing here. Which body shape a unit gets is decided from its stats (BodyShapeFor).
// Depends on: Halobellum.Core (UnitStats), UnityEngine.

using System.Collections.Generic;
using System.Text;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.UI
{
    public static class UnitIcons
    {
        public const string ResourceFolder = "UnitIcons";
        public const int FallbackSize = 256;

        private const string LayerFolder = "CardArt/Layers/";
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The picture of a unit. The faction ("UNSC", "Covenant", "Flood" or null) only matters to a unit with no picture
        /// of its own yet: it colours the fallback.
        /// </summary>
        public static Texture2D For(UnitStats unit, string faction = null)
        {
            if (unit == null) return For("", UnitRole.Chaff, "Trooper", faction);
            return For(unit.Name, unit.Role, BodyShapeFor(unit), faction ?? (unit.IsFlood ? "Flood" : null));
        }

        private static Texture2D For(string unitName, UnitRole role, string shape, string faction)
        {
            string key = unitName + "|" + faction;
            if (Cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            Texture2D texture = Rendered(unitName);
            if (texture == null) texture = Compose(role, shape, faction);
            Cache[key] = texture;
            return texture;
        }

        /// <summary>True if the unit has its own file in Resources/UnitIcons (from a real model, or a fallback the tool wrote).</summary>
        public static bool HasFile(string unitName) => Rendered(unitName) != null;

        private static Texture2D Rendered(string unitName) =>
            string.IsNullOrEmpty(unitName) ? null : Resources.Load<Texture2D>(ResourceFolder + "/" + FileName(unitName));

        /// <summary>The file name for a unit: its name, without signs that a file name can't have.</summary>
        public static string FileName(string unitName)
        {
            var builder = new StringBuilder(unitName.Length);
            foreach (char c in unitName)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '(' || c == ')' || c == '-' || c == '_' || c == '\'') builder.Append(c);
            }
            return builder.ToString().Trim();
        }

        /// <summary>The accent colour of a faction: its fallback icons, card tints and sub-faction banners.</summary>
        public static Color FactionColor(string faction)
        {
            switch (faction)
            {
                case "UNSC": return new Color(0.9f, 0.68f, 0.16f);
                case "Covenant": return new Color(0.62f, 0.42f, 1f);
                case "Flood": return new Color(0.72f, 0.8f, 0.24f);
                default: return new Color(0.6f, 0.68f, 0.76f);
            }
        }

        // ------------------------------------------------------------------ the fallback

        /// <summary>
        /// The kind of body to show a unit with no model as (a Body_ layer): from what the stats say it is.
        /// A few units whose stats don't show their shape are named.
        /// </summary>
        public static string BodyShapeFor(UnitStats unit)
        {
            switch (unit.Name)
            {
                case "Mantis": return "Walker";
                case "Archer Missile Pod": return "Pod";
                case "Hunter Pair": return "Hulk";
                case "Hunter Captain": return "Hulk";
                case "Goliath": return "Hulk"; // Worm colonies in a suit, not a vehicle.
                case "Reaver": return "Walker";
                case "Grunt Goblin": return "Walker"; // A Grunt in a mech.
                case "Skitterer": return "FloodSwarm"; // Small spider-like machines: the closest shape is the cluster of little crawling bodies.
                case "Infested Ghost": return "Speeder"; // A vehicle, though it is Flood.
            }
            if (unit.IsFlood)
            {
                if (unit.FormsPerBody > 1) return "FloodSwarm";
                return unit.Tier >= 3 || unit.CollisionRadius >= 1.5f ? "FloodBeast" : "FloodForm";
            }
            if (unit.IsAir) return "Flyer";
            if (unit.CollisionRadius <= 0.7f)
            {
                if (unit.HasDefenseArc) return "ShieldTrooper";
                return unit.Tier >= 2 && unit.SquadSize <= 4 ? "HeavyTrooper" : "Trooper";
            }
            return unit.CollisionRadius < 1.3f ? "Speeder" : "Tank";
        }

        /// <summary>Puts a fallback together: a glow in the faction's colour, the dark body with a lit edge, and a badge with the role's emblem.</summary>
        public static Texture2D Compose(UnitRole role, string bodyShape, string faction)
        {
            int size = FallbackSize;
            var pixels = new Color32[size * size];
            Color accent = FactionColor(faction);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                    float glow = Mathf.Clamp01(1f - r);
                    pixels[y * size + x] = new Color(accent.r * 0.7f, accent.g * 0.7f, accent.b * 0.7f, Mathf.Pow(glow, 1.4f) * 0.9f);
                }
            }

            Color32[] body = Layer("Body_" + bodyShape, out int bodySize) ?? Layer("Body_Trooper", out bodySize);
            if (body != null && bodySize == size)
            {
                var dark = new Color(0.05f, 0.06f, 0.07f);
                for (int i = 0; i < pixels.Length; i++)
                {
                    float alpha = body[i].a / 255f;
                    if (alpha <= 0f) continue;
                    Color surface = Color.Lerp(dark, Color.Lerp(accent, Color.white, 0.25f), body[i].r / 255f);
                    pixels[i] = Over(surface, alpha, pixels[i]);
                }
            }

            // The badge: a dark disc with a coloured rim, bottom right; the role's emblem on it.
            const int badge = 72;
            int left = size - badge - 4, bottom = 4; // Rows count from the bottom.
            float badgeHalf = badge * 0.5f;
            for (int y = 0; y < badge; y++)
            {
                for (int x = 0; x < badge; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - badgeHalf) * (x + 0.5f - badgeHalf) + (y + 0.5f - badgeHalf) * (y + 0.5f - badgeHalf));
                    float cover = Mathf.Clamp01(badgeHalf - d);
                    if (cover <= 0f) continue;
                    int index = (bottom + y) * size + left + x;
                    Color surface = d > badgeHalf - 5f ? accent : new Color(0.07f, 0.08f, 0.1f);
                    pixels[index] = Over(surface, cover, pixels[index]);
                }
            }
            Color32[] emblem = Layer("Role_" + role, out int emblemSize);
            if (emblem != null)
            {
                const int inset = 14;
                int drawn = badge - 2 * inset;
                for (int y = 0; y < drawn; y++)
                {
                    for (int x = 0; x < drawn; x++)
                    {
                        // A plain average of the source pixels this one covers.
                        int fromX = x * emblemSize / drawn, toX = Mathf.Max(fromX + 1, (x + 1) * emblemSize / drawn);
                        int fromY = y * emblemSize / drawn, toY = Mathf.Max(fromY + 1, (y + 1) * emblemSize / drawn);
                        float alpha = 0f;
                        for (int sy = fromY; sy < toY; sy++)
                            for (int sx = fromX; sx < toX; sx++)
                                alpha += emblem[sy * emblemSize + sx].a / 255f;
                        alpha /= (toX - fromX) * (toY - fromY);
                        if (alpha <= 0f) continue;
                        int index = (bottom + inset + y) * size + left + inset + x;
                        pixels[index] = Over(Color.Lerp(accent, Color.white, 0.55f), alpha, pixels[index]);
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Unit fallback icon",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>Paints a colour over a pixel, keeping straight (not premultiplied) alpha.</summary>
        private static Color32 Over(Color color, float alpha, Color32 under)
        {
            float underAlpha = under.a / 255f;
            float outAlpha = alpha + underAlpha * (1f - alpha);
            if (outAlpha <= 0f) return new Color32(0, 0, 0, 0);
            float keep = underAlpha * (1f - alpha);
            return new Color(
                (color.r * alpha + under.r / 255f * keep) / outAlpha,
                (color.g * alpha + under.g / 255f * keep) / outAlpha,
                (color.b * alpha + under.b / 255f * keep) / outAlpha,
                outAlpha);
        }

        private static Color32[] Layer(string name, out int size)
        {
            size = 0;
            var texture = Resources.Load<Texture2D>(LayerFolder + name);
            if (texture == null || !texture.isReadable) return null;
            size = texture.width;
            return texture.GetPixels32();
        }
    }
}
