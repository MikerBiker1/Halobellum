// RightClick.cs
// What the right mouse button means, now that holding it and moving the mouse turns the camera (BattleCameraController). A click is pressed and released with hardly any
// movement, and short, and it is acted on at the release: that is what cancels a placement or clears a selection. Moving further than that is a drag, which turns the camera
// and cancels nothing. A drag that starts over a HUD window (UiHitAreas) turns nothing either. What the mouse moved in the frame of the press happened around the press and not
// during it, so it doesn't count: a right-click with the mouse still on its way somewhere stays a click, and turns nothing.
// RightClickTracker is the logic, with no mouse in it, so that tests can drive it. RightClick is the one tracker every script reads: the first to ask in a frame moves it on, so all
// of them get the same answer that frame whatever order they run in. The camera asks every frame, so that no press goes unseen by scripts that only ask while they are placing.
// Depends on: UiHitAreas, Unity Input System, UnityEngine.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    /// <summary>One frame of the right mouse button.</summary>
    public struct RightButtonReading
    {
        public bool Pressed, Released, Held;
        /// <summary>Where the pointer is (Input System coordinates), and how far it moved this frame, in pixels.</summary>
        public Vector2 Position, Delta;
    }

    public sealed class RightClickTracker
    {
        /// <summary>A press that moves this far is a drag.</summary>
        public const float DragPixels = 6f;
        /// <summary>A press held this long is no longer a click, even if it never moved.</summary>
        public const float MostClickSeconds = 0.5f;

        private Vector2 _pressedAt, _travel;
        private float _pressedTime;

        public bool IsDown { get; private set; }
        /// <summary>True for the one step in which a click ended.</summary>
        public bool Clicked { get; private set; }
        /// <summary>The press now going on has moved far enough to be a drag.</summary>
        public bool IsDrag { get; private set; }
        public bool StartedOverWindow { get; private set; }
        /// <summary>True while the press now going on should turn the camera.</summary>
        public bool IsRotating => IsDown && IsDrag && !StartedOverWindow;

        /// <param name="overWindow">Whether the pointer is over a HUD window. Only read when a press starts.</param>
        public void Step(RightButtonReading reading, float now, bool overWindow)
        {
            Clicked = false;
            // A press nobody saw starting (nothing asked for a while) counts from now, so that a late release can't look like a click of long ago.
            if (reading.Pressed || (reading.Held && !IsDown))
            {
                IsDown = true;
                IsDrag = false;
                StartedOverWindow = overWindow;
                _pressedAt = reading.Position;
                _pressedTime = now;
                _travel = Vector2.zero;
            }
            else if (IsDown)
            {
                _travel += reading.Delta;
            }
            if (!IsDown) return;

            if (Mathf.Max((reading.Position - _pressedAt).sqrMagnitude, _travel.sqrMagnitude) >= DragPixels * DragPixels) IsDrag = true;

            if (reading.Released) Clicked = !IsDrag && now - _pressedTime < MostClickSeconds;
            if (reading.Released || !reading.Held) IsDown = false;
        }
    }

    public static class RightClick
    {
        private static readonly RightClickTracker Tracker = new RightClickTracker();
        private static RightButtonReading _reading;
        private static int _frame = -1;

        /// <summary>Tests read this instead of the mouse, since a faked mouse doesn't register in a batch run. Null is the real mouse.</summary>
        public static Func<RightButtonReading> ReadForTests;

        /// <summary>True for one frame when a right click has just ended.</summary>
        public static bool WasClicked
        {
            get
            {
                MoveOn();
                return Tracker.Clicked;
            }
        }

        /// <summary>True while a right-button drag is turning the camera.</summary>
        public static bool IsRotating
        {
            get
            {
                MoveOn();
                return Tracker.IsRotating;
            }
        }

        /// <summary>How far the pointer moved this frame, in pixels.</summary>
        public static Vector2 Delta
        {
            get
            {
                MoveOn();
                return _reading.Delta;
            }
        }

        private static void MoveOn()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _reading = Read();
            bool starting = !Tracker.IsDown && (_reading.Pressed || _reading.Held);
            Tracker.Step(_reading, Time.unscaledTime, starting && UiHitAreas.Contains(_reading.Position));
        }

        private static RightButtonReading Read()
        {
            if (ReadForTests != null) return ReadForTests();
            Mouse mouse = Mouse.current;
            if (mouse == null) return default;
            return new RightButtonReading
            {
                Pressed = mouse.rightButton.wasPressedThisFrame,
                Released = mouse.rightButton.wasReleasedThisFrame,
                Held = mouse.rightButton.isPressed,
                Position = mouse.position.ReadValue(),
                Delta = mouse.delta.ReadValue(),
            };
        }
    }
}
