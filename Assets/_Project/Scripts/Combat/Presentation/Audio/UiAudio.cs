// UiAudio.cs
// A small helper for IMGUI HUDs: plain clicks are found by themselves (AudioDirector watches IMGUI's hot control), but two things
// IMGUI can't tell from outside are a mouse going over a button and a click on a greyed-out one. Call Watch(rect, enabled) next to a
// button that should have those: a soft sound on hover, the "can't do that" sound for a click while it is greyed out.
// Call it from OnGUI, in the same coordinates as the button. Depends on: GameAudio, UnityEngine.

using UnityEngine;

namespace Halobellum.Presentation
{
    public static class UiAudio
    {
        private static int _hovered;
        private static int _hoverFrame;

        /// <summary>Hover and greyed-out sounds of a button at this rectangle.</summary>
        public static void Watch(Rect rect, bool enabled = true)
        {
            Event current = Event.current;
            if (current == null || !rect.Contains(current.mousePosition))
            {
                if (Time.frameCount > _hoverFrame + 1) _hovered = 0;
                return;
            }
            if (!enabled && current.type == EventType.MouseDown && current.button == 0) GameAudio.Ui(UiSound.Error);
            if (current.type != EventType.Repaint) return;
            int id = rect.GetHashCode();
            _hoverFrame = Time.frameCount;
            if (id == _hovered) return;
            _hovered = id;
            if (enabled) GameAudio.Ui(UiSound.Hover);
        }

        /// <summary>A plain click sound, for something that takes the mouse without being a button (an item being picked up).</summary>
        public static void Click() => GameAudio.Ui(UiSound.Click);
    }
}
