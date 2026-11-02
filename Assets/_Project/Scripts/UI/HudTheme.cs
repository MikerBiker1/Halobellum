// HudTheme.cs
// Applies the current UI theme (UiThemeDefinition: UNSC, Covenant or neutral) to every HUD drawn
// with Unity's immediate-mode GUI. Each HUD calls HudTheme.Apply() at the start of OnGUI; that sets
// GUI.skin to a skin built from the theme: its fonts, text colours and button images.
// Themes with textures (decision D28: UNSC hull plating, Covenant violet hull with glowing lines)
// draw panels as fill colour + repeating hull texture + frame on top (9-slice, drawn at the theme's
// texture scale and shrunk to fit small panels) + an ornament on the top edge; list rows (RowBox)
// and bars (Bar) have their own textures. Themes without textures get plain shapes drawn in code
// (cut corners, rounded glowing corners or square).
// MatchHud picks the theme: neutral on the draft screen, then the player's faction's theme.
// Styles a HUD builds from GUI.skin should be rebuilt when Version changes; HUDs size their text
// with Size and TitleSize and use TitleFont for titles.
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
        /// <summary>The font for titles and headers (null = the skin's font).</summary>
        public static Font TitleFont => Current != null ? Current.titleFontAsset : null;
        /// <summary>Bold for the default fonts; a title font file is used as it is (it is already bold).</summary>
        public static FontStyle TitleFontStyle => TitleFont != null ? FontStyle.Normal : FontStyle.Bold;

        /// <summary>A text size scaled for the theme's font.</summary>
        public static int Size(int size) => Current != null ? Mathf.Max(1, Mathf.RoundToInt(size * Current.fontScale)) : size;
        /// <summary>A title or header size scaled for the theme's title font.</summary>
        public static int TitleSize(int size) => Current != null ? Mathf.Max(1, Mathf.RoundToInt(size * Current.titleFontScale)) : size;

        /// <summary>
        /// Moves a style's text up so the theme's font sits where the layout expects (the HUDs were laid
        /// out with Arial; some fonts sit lower and would be clipped at the bottom). Call on every style a
        /// HUD builds, after setting its font and size.
        /// </summary>
        public static void Fit(params GUIStyle[] styles)
        {
            if (Current == null) return;
            foreach (GUIStyle style in styles)
            {
                if (style == null) continue;
                float shift = style.font != null && style.font == Current.titleFontAsset ? Current.titleTextShift : Current.textShift;
                style.contentOffset = new Vector2(style.contentOffset.x, -Mathf.Round(shift * style.fontSize));
            }
        }

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
            UiThemeDefinition theme = Current;
            if (theme == null || theme.panelFrame == null)
            {
                GUI.Box(rect, GUIContent.none);
                if (theme == null) GUI.Box(rect, GUIContent.none);
                return;
            }

            // Fill and hull texture first, inside the frame's outer edge; the frame covers their edges.
            float scale = FitScale(theme.textureScale, theme.panelFrameBorder, rect);
            float inset = theme.panelFillInset * scale;
            var inner = new Rect(rect.x + inset, rect.y + inset, rect.width - 2f * inset, rect.height - 2f * inset);
            Color old = GUI.color;
            GUI.color = theme.panelColor;
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            if (theme.panelDetail != null)
            {
                GUI.color = theme.panelDetailTint;
                float tileWidth = theme.panelDetail.width * theme.textureScale, tileHeight = theme.panelDetail.height * theme.textureScale;
                GUI.DrawTextureWithTexCoords(inner, theme.panelDetail, new Rect(0f, 0f, inner.width / tileWidth, inner.height / tileHeight));
            }
            GUI.color = old;
            DrawNineSlice(rect, theme.panelFrame, theme.panelFrameBorder, scale, drawMiddle: false);

            if (theme.panelOrnament != null)
            {
                float width = theme.panelOrnament.width * theme.textureScale, height = theme.panelOrnament.height * theme.textureScale;
                if (rect.width >= width * 2.5f)
                    GUI.DrawTexture(new Rect(rect.center.x - width * 0.5f, rect.y + theme.ornamentOffset, width, height), theme.panelOrnament);
            }
        }

        /// <summary>Draws the background of one row of a list inside a panel (e.g. a shop entry).</summary>
        public static void RowBox(Rect rect)
        {
            UiThemeDefinition theme = Current;
            if (theme == null || theme.rowFrame == null)
            {
                GUI.Box(rect, GUIContent.none);
                return;
            }
            DrawNineSlice(rect, theme.rowFrame, theme.rowFrameBorder, FitScale(theme.textureScale, theme.rowFrameBorder, rect), drawMiddle: true);
        }

        /// <summary>Draws a bar (base health, XP) filled to this fraction in this colour.</summary>
        public static void Bar(Rect rect, float fraction, Color color)
        {
            fraction = Mathf.Clamp01(fraction);
            UiThemeDefinition theme = Current;
            Color old = GUI.color;
            if (theme == null || theme.barFill == null)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = color;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), Texture2D.whiteTexture);
                GUI.color = old;
                return;
            }
            RowBox(rect);
            float pad = Mathf.Min(3f, rect.height * 0.15f);
            var fill = new Rect(rect.x + pad, rect.y + pad, (rect.width - 2f * pad) * fraction, rect.height - 2f * pad);
            if (fill.width <= 0f) return;
            GUI.color = color;
            if (theme.barSegmentWidth > 0f) GUI.DrawTextureWithTexCoords(fill, theme.barFill, new Rect(0f, 0f, fill.width / theme.barSegmentWidth, 1f));
            else GUI.DrawTexture(fill, theme.barFill, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        /// <summary>A title in the theme's style (capitals for UNSC).</summary>
        public static string Title(string text) => Current != null && Current.uppercaseTitles ? text.ToUpperInvariant() : text;

        // ------------------------------------------------------------------ textured drawing

        /// <summary>
        /// Draws a texture stretched to rect with fixed corners (9-slice): the outer `border` texture
        /// pixels on each side are drawn at `scale`, the edges stretch along their length and the
        /// middle stretches both ways (or is skipped).
        /// </summary>
        private static readonly float[] SliceXs = new float[4], SliceYs = new float[4], SliceUs = new float[4], SliceVs = new float[4];

        private static void DrawNineSlice(Rect rect, Texture2D texture, int border, float scale, bool drawMiddle)
        {
            float b = border * scale;
            float u = border / (float)texture.width, v = border / (float)texture.height;
            // Kept, not made anew: the battle's HUD draws panels and bars like this every frame.
            float[] xs = SliceXs, ys = SliceYs, us = SliceUs, vs = SliceVs;
            xs[0] = rect.xMin; xs[1] = rect.xMin + b; xs[2] = rect.xMax - b; xs[3] = rect.xMax;
            ys[0] = rect.yMin; ys[1] = rect.yMin + b; ys[2] = rect.yMax - b; ys[3] = rect.yMax;
            us[0] = 0f; us[1] = u; us[2] = 1f - u; us[3] = 1f;
            vs[0] = 1f; vs[1] = 1f - v; vs[2] = v; vs[3] = 0f; // Texture coordinates start at the bottom, screen rows at the top.
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    if (!drawMiddle && row == 1 && column == 1) continue;
                    Rect destination = Rect.MinMaxRect(xs[column], ys[row], xs[column + 1], ys[row + 1]);
                    if (destination.width <= 0f || destination.height <= 0f) continue;
                    GUI.DrawTextureWithTexCoords(destination, texture, Rect.MinMaxRect(us[column], vs[row + 1], us[column + 1], vs[row]));
                }
            }
        }

        /// <summary>The texture scale, reduced if needed so a 9-slice's corners fit inside the rect.</summary>
        private static float FitScale(float scale, int border, Rect rect)
        {
            if (border <= 0) return scale;
            return Mathf.Max(0.01f, Mathf.Min(scale, Mathf.Min(rect.width, rect.height) / (2f * border)));
        }

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
            int panelBorder = corner + glow + theme.borderThickness + 2;
            skin.box = new GUIStyle(_defaultSkin.box)
            {
                border = new RectOffset(panelBorder, panelBorder, panelBorder, panelBorder),
                normal = { background = panel, textColor = theme.textColor },
            };

            Texture2D button, hover, pressed;
            int buttonBorder;
            if (theme.buttonNormal != null)
            {
                button = theme.buttonNormal;
                hover = theme.buttonHover != null ? theme.buttonHover : button;
                pressed = theme.buttonPressed != null ? theme.buttonPressed : button;
                buttonBorder = theme.buttonBorder;
            }
            else
            {
                button = MakeShape(theme, theme.buttonColor, theme.borderColor, 0);
                hover = MakeShape(theme, theme.buttonHoverColor, theme.accentColor, 0);
                pressed = MakeShape(theme, theme.buttonPressedColor, theme.accentColor, 0);
                buttonBorder = corner + theme.borderThickness + 2;
            }
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

            // Scroll bars: a dark track and a thumb in the button colours with an accent edge.
            Texture2D track = MakeBox(theme.panelColor * new Color(0.6f, 0.6f, 0.6f, 1f), theme.borderColor);
            Texture2D thumb = MakeBox(theme.buttonHoverColor, theme.accentColor);
            skin.verticalScrollbar = new GUIStyle(_defaultSkin.verticalScrollbar) { border = new RectOffset(2, 2, 2, 2), normal = { background = track } };
            skin.verticalScrollbarThumb = new GUIStyle(_defaultSkin.verticalScrollbarThumb) { border = new RectOffset(2, 2, 2, 2), normal = { background = thumb } };

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

        /// <summary>A tiny square image with a one-pixel edge (scroll bars).</summary>
        private static Texture2D MakeBox(Color fill, Color edge)
        {
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HUD box",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? edge : fill;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
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
