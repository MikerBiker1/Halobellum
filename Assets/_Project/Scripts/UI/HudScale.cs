// HudScale.cs
// Shared helper for the placeholder HUD: lays the GUI out on a 1080-pixel-tall virtual screen and
// scales it to the real screen, so the HUD looks the same size at any resolution.
// The player's UI scale (Settings) multiplies it, as far as the widest bar still fits across the window. Menus use ApplyFit, which also keeps a whole layout inside a narrow or tiny window.
// Depends on: GameSettings (the player's UI scale), UnityEngine.

using Halobellum.Presentation;
using UnityEngine;

namespace Halobellum.UI
{
    public static class HudScale
    {
        private const float ReferenceHeight = 1080f;

        /// <summary>The HUD is never wider than a window that shows this many units: the 1120-unit bars of the Match and the Sandbox, and a margin.</summary>
        public const float LeastWidth = 1140f;

        /// <summary>The HUD's scale for a window of this size. Every HUD of a scene has the same one, so that they stay in line with each other.</summary>
        public static float ScaleFor(float screenWidth, float screenHeight, float uiScale)
        {
            return Mathf.Min(Mathf.Max(0.5f, screenHeight / ReferenceHeight) * uiScale, screenWidth / LeastWidth);
        }

        /// <summary>How many units fit across a window of this size.</summary>
        public static float ScaledWidthFor(float screenWidth, float screenHeight, float uiScale) => screenWidth / ScaleFor(screenWidth, screenHeight, uiScale);

        /// <summary>Sets GUI.matrix for the current screen and returns the scale factor.</summary>
        public static float Apply()
        {
            float scale = ScaleFor(Screen.width, Screen.height, GameSettings.UiScale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            return scale;
        }

        /// <summary>
        /// Like Apply, for a menu: makes sure at least this many virtual units fit across and down, however small or narrow
        /// the window is, and sets GUI.matrix. Returns the scale factor.
        /// </summary>
        public static float ApplyFit(float leastWidth, float leastHeight)
        {
            float wanted = Screen.height / ReferenceHeight * GameSettings.UiScale;
            float scale = Mathf.Min(wanted, Mathf.Min(Screen.width / leastWidth, Screen.height / leastHeight));
            scale = Mathf.Max(0.2f, scale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            return scale;
        }

        /// <summary>Screen width in virtual (unscaled) GUI units.</summary>
        public static float ScaledWidth(float scale) => Screen.width / scale;

        /// <summary>Screen height in virtual (unscaled) GUI units.</summary>
        public static float ScaledHeight(float scale) => Screen.height / scale;

        /// <summary>Converts a rectangle in virtual GUI units to real screen pixels.</summary>
        public static Rect ToScreen(Rect rect, float scale) => new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);
    }
}
