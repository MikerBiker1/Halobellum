// UiHitAreas.cs
// A tiny shared list of screen rectangles covered by HUD windows this frame, so a click on a
// window does not also select a unit behind it. HUD scripts call Add() while drawing; the list is
// cleared at the start of each frame. Rectangles use GUI coordinates (origin at the top-left).
// Depends on: UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class UiHitAreas
    {
        private static readonly List<Rect> Areas = new List<Rect>();
        private static int _frame = -1;

        /// <summary>Registers a screen rectangle (GUI coordinates, already scaled to screen pixels).</summary>
        public static void Add(Rect screenRect)
        {
            if (_frame != Time.frameCount)
            {
                Areas.Clear();
                _frame = Time.frameCount;
            }
            Areas.Add(screenRect);
        }

        /// <summary>True if a pointer position (Input System coordinates, origin at the bottom-left) is over a HUD window.</summary>
        public static bool Contains(Vector2 pointer)
        {
            // Areas are drawn every frame; anything older than last frame is stale.
            if (_frame < Time.frameCount - 1) return false;
            var guiPoint = new Vector2(pointer.x, Screen.height - pointer.y);
            foreach (Rect area in Areas)
            {
                if (area.Contains(guiPoint)) return true;
            }
            return false;
        }
    }
}
