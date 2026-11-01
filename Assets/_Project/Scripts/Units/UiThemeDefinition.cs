// UiThemeDefinition.cs
// The look of the HUD for one faction (requested after M6): colours, fonts and panel shape. The
// player's faction picks the theme (UNSC or Covenant); the draft screen and the sandbox use the
// neutral one. Assets live in Data/UI/. The UI code (Scripts/UI/HudTheme) turns this into a GUI skin.
// Fonts: the font files assigned here (Art/Imported/Fonts, free OFL fonts) win; otherwise fonts on
// the computer are looked up by name, first match wins. Avoid variable fonts such as Bahnschrift:
// Unity draws the wrong text with them.
// Textures (decision D28, drawn by Tools/UiArt/make_ui_textures.py into Art/UI): when set, panels
// are drawn as fill colour + repeating hull texture + frame on top (9-slice) + an ornament on the
// top edge; list rows, bars and buttons use their own textures. Without textures, the HUD draws
// plain shapes from the colours and Panel Shape below (the neutral theme does this).
// Depends on: UnityEngine.

using UnityEngine;

namespace Halobellum.Units
{
    public enum PanelShape
    {
        /// <summary>Square corners.</summary>
        Square = 0,
        /// <summary>Cut corners and thin metal edges (UNSC).</summary>
        Chamfered = 1,
        /// <summary>Rounded corners with a soft glow (Covenant).</summary>
        Rounded = 2,
    }

    [CreateAssetMenu(menuName = "Halobellum/UI Theme", fileName = "NewUiTheme")]
    public sealed class UiThemeDefinition : ScriptableObject
    {
        public string themeName;

        [Header("Fonts")]
        [Tooltip("Optional font file (from Art/Imported). If empty, the OS fonts below are used.")]
        public Font fontAsset;
        [Tooltip("Optional font file for titles and headers. If empty, the main font is used.")]
        public Font titleFontAsset;
        [Tooltip("Fonts installed on the computer, tried in order.")]
        public string[] osFontNames = { "Segoe UI", "Arial" };
        [Tooltip("Write titles in capitals.")]
        public bool uppercaseTitles;
        [Tooltip("Scales every text size (fonts differ in how big they look at the same size).")]
        [Range(0.6f, 1.5f)] public float fontScale = 1f;
        [Tooltip("Scales title and header sizes.")]
        [Range(0.6f, 1.5f)] public float titleFontScale = 1f;
        [Tooltip("Moves text up by this share of its size: fonts that sit lower than Arial (the layouts were made with it) get clipped at the bottom otherwise.")]
        [Range(-0.3f, 0.5f)] public float textShift;
        [Tooltip("The same for the title font.")]
        [Range(-0.3f, 0.5f)] public float titleTextShift;

        [Header("Textures (optional)")]
        [Tooltip("Panel, row and ornament textures are drawn at this scale (0.5 = made at double resolution).")]
        [Range(0.25f, 1f)] public float textureScale = 0.5f;
        [Tooltip("Panel frame drawn on top of the fill (9-slice; its middle is not drawn).")]
        public Texture2D panelFrame;
        [Tooltip("Frame corner size in texture pixels (the part that is never stretched).")]
        public int panelFrameBorder = 48;
        [Tooltip("How far inside the frame's outer edge the fill and hull texture start, in texture pixels.")]
        public int panelFillInset = 15;
        [Tooltip("Repeating texture drawn over the panel fill (hull plating).")]
        public Texture2D panelDetail;
        [Tooltip("Tint and strength (alpha) of the hull texture.")]
        public Color panelDetailTint = Color.white;
        [Tooltip("Decoration on the middle of a panel's top edge (only on panels wide enough).")]
        public Texture2D panelOrnament;
        [Tooltip("Vertical position of the ornament relative to the panel's top edge, in HUD pixels.")]
        public float ornamentOffset = -3f;
        [Tooltip("List rows inside panels (9-slice, middle stretched).")]
        public Texture2D rowFrame;
        public int rowFrameBorder = 20;
        [Tooltip("Bar fill (white, tinted by the bar's colour). Repeats every Bar Segment Width HUD pixels; 0 = stretched.")]
        public Texture2D barFill;
        public float barSegmentWidth;
        [Tooltip("Button images (9-slice at HUD size). If empty, plain shapes are drawn.")]
        public Texture2D buttonNormal;
        public Texture2D buttonHover;
        public Texture2D buttonPressed;
        public int buttonBorder = 12;

        [Header("Panels")]
        public PanelShape panelShape = PanelShape.Square;
        [Tooltip("Size of the cut or rounded corners, in pixels of the 1080p layout.")]
        [Range(0, 24)] public int cornerSize = 6;
        [Range(0, 4)] public int borderThickness = 1;
        [Tooltip("Soft outer glow around panels (0 = none).")]
        [Range(0, 8)] public int glowSize;
        public Color panelColor = new Color(0.08f, 0.08f, 0.09f, 0.88f);
        public Color borderColor = new Color(0.5f, 0.5f, 0.55f, 1f);
        public Color glowColor = new Color(0.6f, 0.3f, 1f, 0.35f);

        [Header("Text")]
        public Color textColor = new Color(0.93f, 0.93f, 0.93f);
        public Color dimTextColor = new Color(0.7f, 0.72f, 0.75f);
        public Color titleColor = Color.white;
        [Tooltip("Highlights: selected buttons, XP bar, hover edges.")]
        public Color accentColor = new Color(0.45f, 0.75f, 1f);

        [Header("Buttons")]
        public Color buttonColor = new Color(0.22f, 0.23f, 0.26f, 0.95f);
        public Color buttonHoverColor = new Color(0.3f, 0.32f, 0.36f, 0.98f);
        public Color buttonPressedColor = new Color(0.16f, 0.17f, 0.2f, 1f);
        public Color buttonTextColor = Color.white;
        public Color disabledTextColor = new Color(0.55f, 0.55f, 0.55f);
    }
}
