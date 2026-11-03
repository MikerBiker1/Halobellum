// BattleCameraController.cs
// The angled battle camera seen from a distance (Bible Section 3). It always looks down at a point on the ground, and turns around that point.
//   Pan:   W A S D or the arrow keys, or hold the middle mouse button and drag. Both go along the ground relative to where the camera faces.
//   Turn:  hold the right mouse button and drag. Sideways goes around the point, a full circle if wanted; up and down tilts it between minPitch and maxPitch. The point looked at stays
//          where it is. A drag that starts over a HUD window does nothing, and a short right-click keeps its old meanings: RightClick tells them apart.
//   Zoom:  mouse scroll wheel, unless the pointer is over a HUD window (it scrolls the window's list instead: UiHitAreas).
//   C:     back to the view the scene started with.
// Its focus point stays inside the arena. It runs before other scripts' LateUpdate, so that health bars and whatever else faces the camera turn with it in the same frame.
// Test tools get the same default view unless the command line says otherwise: -cameraYaw <degrees> and -cameraPitch <degrees>.
// Put it on the Main Camera. Values are camera feel settings, adjustable in the Inspector.
// Depends on: BattleController (arena size), RightClick, UiHitAreas, Unity Input System.

using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    [DefaultExecutionOrder(-50)]
    public sealed class BattleCameraController : MonoBehaviour
    {
        /// <summary>The downward tilt the game starts with.</summary>
        public const float DefaultPitch = 55f;
        private const string YawArgument = "-cameraYaw";
        private const string PitchArgument = "-cameraPitch";

        [SerializeField] private BattleController battle;
        [Tooltip("Downward tilt at the start, in degrees (90 = straight down).")]
        [SerializeField] private float pitch = DefaultPitch;
        [Tooltip("The lowest tilt the right mouse button can reach. At 25 degrees the top of the view would reach the horizon.")]
        [SerializeField] private float minPitch = 35f;
        [Tooltip("The steepest tilt the right mouse button can reach.")]
        [SerializeField] private float maxPitch = 75f;
        [Tooltip("Degrees turned or tilted per pixel the mouse moves with the right button held.")]
        [SerializeField] private float orbitDegreesPerPixel = 0.2f;
        [Tooltip("Point on the ground the camera looks at when the battle starts (x, z).")]
        [SerializeField] private Vector2 startFocus = new Vector2(0f, -5f);
        [SerializeField] private float startDistance = 150f;
        [SerializeField] private float minDistance = 20f;
        [SerializeField] private float maxDistance = 240f;
        [Tooltip("Pan speed at a distance of 100 m, in metres per second. Scales with zoom.")]
        [SerializeField] private float panSpeed = 60f;
        [Tooltip("Fraction of the distance moved per scroll notch.")]
        [SerializeField] private float zoomStep = 0.12f;

        private Vector2 _focus;
        private float _distance;
        private float _yaw;
        private float _pitch;

        private void Start()
        {
            ResetView();
            string[] args = Environment.GetCommandLineArgs();
            _yaw = Number(args, YawArgument, _yaw);
            _pitch = Number(args, PitchArgument, _pitch);
            Apply();
        }

        private void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            float speedScale = _distance / 100f;

            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
                if (keyboard.cKey.wasPressedThisFrame) ResetView();
            }
            Pan(move.normalized * (panSpeed * speedScale * Time.unscaledDeltaTime));

            if (RightClick.IsRotating) Orbit(RightClick.Delta);

            if (mouse != null)
            {
                if (mouse.middleButton.isPressed) DragGround(mouse.delta.ReadValue());
                Wheel(mouse.scroll.ReadValue().y, mouse.position.ReadValue());
            }

            ClampFocus();
            Apply();
        }

        /// <summary>How far the camera is from its focus point.</summary>
        public float Distance => _distance;

        /// <summary>The point on the ground the camera looks at (x, z).</summary>
        public Vector2 Focus => _focus;

        /// <summary>Which way the camera faces, in degrees: 0 is north, from Team A's side, and 90 is east.</summary>
        public float Yaw => _yaw;

        /// <summary>How far the camera looks down, in degrees.</summary>
        public float Pitch => _pitch;

        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float MaxDistance => maxDistance;

        /// <summary>
        /// One reading of the mouse with the right button held, in pixels: sideways turns around the point looked at, up and down tilts. It turns like grabbing the ground: moving
        /// the mouse right brings the near ground to the right, moving it up lays the view flatter.
        /// </summary>
        public void Orbit(Vector2 mouseDelta)
        {
            _yaw = Mathf.Repeat(_yaw + mouseDelta.x * orbitDegreesPerPixel, 360f);
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y * orbitDegreesPerPixel, minPitch, maxPitch);
            Apply();
        }

        /// <summary>Moves the point looked at, in metres to the right of the view (x) and away from the camera along the ground (y).</summary>
        public void Pan(Vector2 metres)
        {
            float yaw = _yaw * Mathf.Deg2Rad;
            float sin = Mathf.Sin(yaw), cos = Mathf.Cos(yaw);
            _focus += new Vector2(metres.x * cos + metres.y * sin, -metres.x * sin + metres.y * cos);
            ClampFocus();
            Apply();
        }

        /// <summary>One reading of the mouse with the middle button held, in pixels. Drags the ground: moving the mouse right moves the view left, like grabbing the map.</summary>
        public void DragGround(Vector2 mouseDelta)
        {
            float metresPerPixel = 2f * _distance * Mathf.Tan(GetFieldOfView() * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
            Pan(-mouseDelta * metresPerPixel);
        }

        /// <summary>One reading of the mouse wheel, with the pointer here (Input System coordinates): zooms, unless a HUD window is under the pointer.</summary>
        public void Wheel(float scroll, Vector2 pointer)
        {
            if (Mathf.Abs(scroll) <= 0.01f || UiHitAreas.Contains(pointer)) return;
            _distance *= scroll > 0f ? 1f - zoomStep : 1f + zoomStep;
            _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
        }

        /// <summary>Back to the view the scene started with: the C key.</summary>
        public void ResetView()
        {
            _focus = startFocus;
            _distance = Mathf.Clamp(startDistance, minDistance, maxDistance);
            _yaw = 0f;
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            ClampFocus();
            Apply();
        }

        /// <summary>Looks at this view at once, within the same limits as the mouse has. Test tools use it to take pictures.</summary>
        public void SetView(Vector2 focus, float yaw, float tilt, float distance)
        {
            _focus = focus;
            _yaw = Mathf.Repeat(yaw, 360f);
            _pitch = Mathf.Clamp(tilt, minPitch, maxPitch);
            _distance = Mathf.Clamp(distance, minDistance, maxDistance);
            ClampFocus();
            Apply();
        }

        /// <summary>The view a test tool's command line asks for: the default one, unless it says -cameraYaw or -cameraPitch.</summary>
        public static void ToolView(string[] args, out float yaw, out float tilt)
        {
            yaw = Number(args, YawArgument, 0f);
            tilt = Number(args, PitchArgument, DefaultPitch);
        }

        private static float Number(string[] args, string name, float otherwise)
        {
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length && float.TryParse(args[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : otherwise;
        }

        private void ClampFocus()
        {
            if (battle == null || battle.Rules == null) return;
            float halfWidth = battle.Rules.ArenaWidth * 0.5f;
            float halfLength = battle.Rules.ArenaLength * 0.5f;
            _focus = new Vector2(Mathf.Clamp(_focus.x, -halfWidth, halfWidth), Mathf.Clamp(_focus.y, -halfLength, halfLength));
        }

        private void Apply()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 focus = new Vector3(_focus.x, 0f, _focus.y);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * _distance, rotation);
        }

        private float GetFieldOfView()
        {
            var camera = GetComponent<Camera>();
            return camera != null ? camera.fieldOfView : 60f;
        }
    }
}
