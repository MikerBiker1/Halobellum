// HudScale.cs
// Shared helper for the placeholder HUD: lays the GUI out on a 1080-pixel-tall virtual screen and
// scales it to the real screen, so the HUD looks the same size at any resolution.
// Depends on: UnityEngine.

using UnityEngine;

namespace Halobellum.UI
{
    public static class HudScale
    {
        private const float ReferenceHeight = 1080f;

        /// <summary>Sets GUI.matrix for the current screen and returns the scale factor.</summary>
        public static float Apply()
        {
            float scale = Mathf.Max(0.5f, Screen.height / ReferenceHeight);
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
