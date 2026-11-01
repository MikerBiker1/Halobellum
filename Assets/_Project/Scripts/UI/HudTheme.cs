// HudTheme.cs
// Applies the current UI theme (UiThemeDefinition: UNSC, Covenant or neutral) to every HUD drawn
// with Unity's immediate-mode GUI. Each HUD calls HudTheme.Apply() at the start of OnGUI; that sets
// GUI.skin to a skin built from the theme: its font, text colours, and panel and button images drawn
// in code (cut corners with metal edges for UNSC, rounded glowing corners for Covenant).
// MatchHud picks the theme: neutral on the draft screen, then the player's faction's theme.
// Styles a HUD builds from GUI.skin should be rebuilt when Version changes.
// Depends on: Halobellum.Units (UiThemeDefinition, PanelShape), UnityEngine.

using System.Collections.Generic;
using Halobellum.Units;
using UnityEngine;

namespace Halobellum.UI
{
    public static class HudTheme
    {
        private static GUISkin _defaultSkin;
        private static readonly Dictionary<UiThemeDefinition, GUISkin> Skins = new Dictionary<UiThemeDefinition, GUISkin>();

        /// <summary>The theme in use (null = Unity's default look).</summary>
        public static UiThemeDefinition Current { get; private set; }
        /// <summary>Changes whenever the theme changes, so HUDs know to rebuild their styles.</summary>
        public static int Version { get; private set; }

        public static Color Text => Current != null ? Current.textColor : Color.white;
        public static Color DimText => Current != null ? Current.dimTextColor : new Color(0.75f, 0.75f, 0.75f);
        public static Color TitleColor => Current != null ? Current.titleColor : Color.white;
        public static Color Accent => Current != null ? Current.accentColor : new Color(0.45f, 0.75f, 1f);

        /// <summary>Switches the theme (takes effect from the next Apply).</summary>
        public static void Use(UiThemeDefinition theme)
        {
            if (theme == Current) return;
            Current = theme;
            Version++;
        }

        /// <summary>Sets GUI.skin for the current theme. Call at the start of OnGUI.</summary>
        public static void Apply()
        {
            if (_defaultSkin == null) _defaultSkin = GUI.skin;
            if (Current == null)
            {
                GUI.skin = _defaultSkin;
                return;
            }
            if (!Skins.TryGetValue(Current, out GUISkin skin) || skin == null)
            {
                skin = BuildSkin(Current);
                Skins[Current] = skin;
            }
            GUI.skin = skin;
        }

        /// <summary>Draws a panel background (twice with Unity's default look, which is quite transparent).</summary>
        public static void PanelBox(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            if (Current == null) GUI.Box(rect, GUIContent.none);
        }

        /// <summary>A title in the theme's style (capitals for UNSC).</summary>
        public static string Title(string text) => Current != null && Current.uppercaseTitles ? text.ToUpperInvariant() : text;

        // ------------------------------------------------------------------ building the skin

        private static GUISkin BuildSkin(UiThemeDefinition theme)
        {
            GUISkin skin = Object.Instantiate(_defaultSkin);
            skin.name = "HUD skin: " + theme.themeName;
            Font font = theme.fontAsset;
            if (font == null && theme.osFontNames != null && theme.osFontNames.Length > 0)
            {
                font = Font.CreateDynamicFontFromOSFont(theme.osFontNames, 16);
            }
            if (font != null) skin.font = font;

            int corner = Mathf.Max(0, theme.cornerSize), glow = Mathf.Max(0, theme.glowSize);
            Texture2D panel = MakeShape(theme, theme.panelColor, theme.borderColor, glow);
            Texture2D button = MakeShape(theme, theme.buttonColor, theme.borderColor, 0);
            Texture2D hover = MakeShape(theme, theme.buttonHoverColor, theme.accentColor, 0);
            Texture2D pressed = MakeShape(theme, theme.buttonPressedColor, theme.accentColor, 0);
            int panelBorder = corner + glow + theme.borderThickness + 2, buttonBorder = corner + theme.borderThickness + 2;

            skin.box = new GUIStyle(_defaultSkin.box)
            {
                border = new RectOffset(panelBorder, panelBorder, panelBorder, panelBorder),
                normal = { background = panel, textColor = theme.textColor },
            };
            skin.button = new GUIStyle(_defaultSkin.button)
            {
                border = new RectOffset(buttonBorder, buttonBorder, buttonBorder, buttonBorder),
                normal = { background = button, textColor = theme.buttonTextColor },
                hover = { background = hover, textColor = theme.buttonTextColor },
                active = { background = pressed, textColor = theme.accentColor },
                focused = { background = button, textColor = theme.buttonTextColor },
                onNormal = { background = pressed, textColor = theme.accentColor },
                onHover = { background = hover, textColor = theme.accentColor },
                onActive = { background = pressed, textColor = theme.accentColor },
            };
            // Note: keep the default text clipping. TextClipping.Overflow made Unity reuse the wrong text
            // for labels with OS fonts (every label showed the same string).
            skin.label = new GUIStyle(_defaultSkin.label) { normal = { textColor = theme.textColor } };
            if (font != null)
            {
                skin.box.font = font;
                skin.button.font = font;
                skin.label.font = font;
            }
            return skin;
        }

        /// <summary>
        /// A small image of a panel or button in the theme's shape, drawn pixel by pixel: the fill, an
        /// edge line, optional cut or rounded corners, and an optional soft glow outside the edge.
        /// It is stretched with fixed corners (9-slice), so one small image fits any size.
        /// </summary>
        private static Texture2D MakeShape(UiThemeDefinition theme, Color fill, Color edge, int glow)
        {
            int corner = Mathf.Max(0, theme.cornerSize), border = theme.borderThickness;
            int size = 2 * (corner + glow + border + 4) + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HUD shape",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            float inner = size - 2f * glow; // The panel itself, inside the glow margin.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance outside the panel's shape (negative = inside).
                    float px = x + 0.5f - glow, py = y + 0.5f - glow;
                    float d = ShapeDistance(theme.panelShape, px, py, inner, corner);
                    Color c;
                    if (d > 0f)
                    {
                        // Outside: transparent, or the glow fading out.
                        c = glow > 0 && d < glow ? new Color(theme.glowColor.r, theme.glowColor.g, theme.glowColor.b, theme.glowColor.a * (1f - d / glow) * (1f - d / glow)) : Color.clear;
                    }
                    else if (d > -border) c = edge;
                    else c = fill;
                    // Soften the outline by one pixel.
                    if (d > -1f && d <= 0f && border == 0) c.a *= 0.6f;
                    pixels[y * size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Signed distance from (x, y) to the edge of a size x size shape (negative inside).</summary>
        private static float ShapeDistance(PanelShape shape, float x, float y, float size, int corner)
        {
            float dx = Mathf.Max(-x, x - size), dy = Mathf.Max(-y, y - size);
            float box = Mathf.Max(dx, dy); // Square: distance to the nearest side.
            if (corner <= 0 || shape == PanelShape.Square) return box;

            // Position measured from the nearest corner's inner point.
            float cx = x < corner ? corner - x : x > size - corner ? x - (size - corner) : 0f;
            float cy = y < corner ? corner - y : y > size - corner ? y - (size - corner) : 0f;
            if (cx <= 0f || cy <= 0f) return box;
            if (shape == PanelShape.Rounded) return Mathf.Max(box, Mathf.Sqrt(cx * cx + cy * cy) - corner);
            // Chamfered: a 45-degree cut across the corner.
            return Mathf.Max(box, (cx + cy - corner) * 0.7071f);
        }
    }
}
