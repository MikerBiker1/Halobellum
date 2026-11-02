// BattleCameraController.cs
// The fixed, angled top-down battle camera seen from a distance (Bible Section 3): pan and zoom only.
//   Pan:  W A S D or the arrow keys, or hold the middle mouse button and drag.
//   Zoom: mouse scroll wheel, unless the pointer is over a HUD window (it scrolls the window's list instead: UiHitAreas).
// The camera never rotates. Its focus point stays inside the arena.
// Put it on the Main Camera. Values are camera feel settings, adjustable in the Inspector.
// Depends on: BattleController (arena size), UiHitAreas, Unity Input System.

using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    public sealed class BattleCameraController : MonoBehaviour
    {
        [SerializeField] private BattleController battle;
        [Tooltip("Downward tilt in degrees (90 = straight down).")]
        [SerializeField] private float pitch = 55f;
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

        private void Start()
        {
            _focus = startFocus;
            _distance = Mathf.Clamp(startDistance, minDistance, maxDistance);
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
            }
            _focus += move.normalized * (panSpeed * speedScale * Time.unscaledDeltaTime);

            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                {
                    // Drag the ground: moving the mouse right moves the view left, like grabbing the map.
                    Vector2 delta = mouse.delta.ReadValue();
                    float metresPerPixel = 2f * _distance * Mathf.Tan(GetFieldOfView() * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, Screen.height);
                    _focus -= delta * metresPerPixel;
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !UiHitAreas.Contains(mouse.position.ReadValue()))
                {
                    _distance *= scroll > 0f ? 1f - zoomStep : 1f + zoomStep;
                    _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
                }
            }

            ClampFocus();
            Apply();
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
            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
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
