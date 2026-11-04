// UiAudio.cs
// A small helper for IMGUI HUDs: plain clicks are found by themselves (AudioDirector watches IMGUI's hot control), but two things
// IMGUI can't tell from outside are a mouse going over a button and a click on a greyed-out one. Call Watch(rect, enabled) next to a
// button that should have those: a soft sound on hover, the "can't do that" sound for a click while it is greyed out.
// Call it from OnGUI, in the same coordinates as the button. A hover is the mouse moving onto a button: a screen that opens under a mouse
// standing still is silent, and so is a mouse resting on a button (see HoverWatch).
// Also here: the sounds of what is done to a squad every round (selected, moved, turned, a spell taken back), kept below the sounds of buying and selling.
// Depends on: GameAudio, UnityEngine.

using System.Collections.Generic;
using UnityEngine;

namespace Halobellum.Presentation
{
    /// <summary>
    /// Says when the pointer has newly moved onto a button. Plain C#, so it can be tested. Each button is told apart by an id, and is
    /// only new if it wasn't under the pointer the frame before, so that two buttons overlapping can't set each other off every frame.
    /// </summary>
    public sealed class HoverWatch
    {
        /// <summary>The least time between two hovers, for a mouse swept across a row of small buttons.</summary>
        public const float MinGapSeconds = 0.05f;

        private HashSet<int> _over = new HashSet<int>();
        private HashSet<int> _overBefore = new HashSet<int>();
        private int _overFrame = int.MinValue;
        private int _pointerFrame = int.MinValue;
        private float _x, _y;
        private bool _moved;
        private float _lastSound = float.NegativeInfinity;

        /// <summary>Where the pointer is this frame. Call each frame there are buttons to watch.</summary>
        public void NotePointer(float x, float y, int frame)
        {
            if (frame == _pointerFrame) return;
            _moved = frame == _pointerFrame + 1 && (x != _x || y != _y);
            _pointerFrame = frame;
            _x = x;
            _y = y;
        }

        /// <summary>This button is under the pointer this frame. True if that is a hover to be heard.</summary>
        public bool PointerOver(int id, int frame, float now)
        {
            if (frame != _overFrame)
            {
                HashSet<int> old = _overBefore;
                _overBefore = _over;
                _over = old;
                _over.Clear();
                if (frame != _overFrame + 1) _overBefore.Clear();
                _overFrame = frame;
            }
            if (!_over.Add(id) || _overBefore.Contains(id) || !_moved || now - _lastSound < MinGapSeconds) return false;
            _lastSound = now;
            return true;
        }

        /// <summary>Forgets where the pointer was (a new scene): whatever is under it now didn't come there by moving.</summary>
        public void ForgetPointer()
        {
            _pointerFrame = int.MinValue;
            _moved = false;
        }
    }

    public static class UiAudio
    {
        private const float SelectedVolume = 0.6f, TurnedVolume = 0.8f, MovedVolume = 0.6f, TakenBackVolume = 0.5f;
        private static readonly HoverWatch Hovers = new HoverWatch();

        /// <summary>Where a test tool says the pointer is, in pixels of IMGUI's screen (top left), in place of the real mouse; null for the real mouse.</summary>
        public static Vector2? PointerByTest { get; set; }

        /// <summary>Hover and greyed-out sounds of a button at this rectangle.</summary>
        public static void Watch(Rect rect, bool enabled = true)
        {
            Event current = Event.current;
            if (current == null) return;
            Matrix4x4 matrix = GUI.matrix;
            Vector2 pointer = PointerByTest.HasValue ? (Vector2)matrix.inverse.MultiplyPoint3x4(PointerByTest.Value) : current.mousePosition;
            bool repaint = current.type == EventType.Repaint;
            if (repaint)
            {
                // In pixels, because each HUD has its own scale.
                Vector2 pixels = PointerByTest ?? (Vector2)matrix.MultiplyPoint3x4(pointer);
                Hovers.NotePointer(pixels.x, pixels.y, Time.frameCount);
            }
            if (!rect.Contains(pointer)) return;
            if (!enabled && current.type == EventType.MouseDown && current.button == 0) GameAudio.Ui(UiSound.Error);
            if (repaint && Hovers.PointerOver(rect.GetHashCode(), Time.frameCount, Time.unscaledTime) && enabled) GameAudio.Ui(UiSound.Hover);
        }

        /// <summary>Whatever is under the mouse when a scene opens didn't get there by moving.</summary>
        public static void ForgetPointer() => Hovers.ForgetPointer();

        /// <summary>A plain click sound, for something that takes the mouse without being a button (an item being picked up).</summary>
        public static void Click() => GameAudio.Ui(UiSound.Click);

        /// <summary>The player clicked a squad on the field.</summary>
        public static void SquadSelected() => GameAudio.Ui(UiSound.Click, SelectedVolume);

        /// <summary>A squad turned, or its ghost while it is being moved. From the R key: the Turn button is drawn in OnGUI, and it already clicked as it went down (AudioDirector).</summary>
        public static void SquadTurned()
        {
            // GUIUtility.guiDepth, not Event.current: once a built game has drawn its GUI, Event.current stays set outside OnGUI too.
            if (GUIUtility.guiDepth == 0) GameAudio.Ui(UiSound.Click, TurnedVolume);
        }

        /// <summary>A squad set down in a new spot.</summary>
        public static void SquadMoved() => GameAudio.Ui(UiSound.Skip, MovedVolume);

        /// <summary>A placed spell taken back: the falling sound of a sale, well below one.</summary>
        public static void SpellTakenBack() => GameAudio.Ui(UiSound.Sell, TakenBackVolume);
    }
}
