// UiThemeDefinition.cs
// The look of the HUD for one faction (requested after M6): colours, fonts and panel shape. The
// player's faction picks the theme (UNSC or Covenant); the draft screen and the sandbox use the
// neutral one. Assets live in Data/UI/. The UI code (Scripts/UI/HudTheme) turns this into a GUI skin.
// Fonts are looked up on the computer by name, first match wins (Windows ships Franklin Gothic
// Medium and Candara); to use your own font file, put it in Art/Imported and assign it to Font Asset.
// Avoid variable fonts such as Bahnschrift: Unity draws the wrong text with them.
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
        [Tooltip("Fonts installed on the computer, tried in order.")]
        public string[] osFontNames = { "Segoe UI", "Arial" };
        [Tooltip("Write titles in capitals.")]
        public bool uppercaseTitles;

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
