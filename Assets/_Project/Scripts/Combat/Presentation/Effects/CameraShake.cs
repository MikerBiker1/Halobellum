// CameraShake.cs
// A small camera shake on big blasts (M10) that can be turned off with the K key (remembered between runs). BattleCameraController
// sets the camera's position from nothing every frame, so this runs after it and adds a small offset that can't add up. It leaves the
// camera alone whenever BattleCameraController is off (test tools looking closely at something). It shakes by a share of the distance
// to the ground, so that it feels the same at every zoom, and less the further the blast is from the middle of the view.
// Depends on: BattleCameraController, Unity Input System, UnityEngine.

using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    [DefaultExecutionOrder(1000)]
    public sealed class CameraShake : MonoBehaviour
    {
        private const string SettingKey = "Halobellum.CameraShake";
        private const float DecayPerSecond = 1.6f;
        private const float MostShare = 0.012f;
        private const float MessageSeconds = 2f;

        private static CameraShake _instance;
        private static bool _loaded, _enabled;

        private BattleCameraController _control;
        private float _trauma;
        private float _messageUntil;

        /// <summary>Whether big blasts shake the camera. Kept between runs; a settings screen can set it too.</summary>
        public static bool ShakeEnabled
        {
            get
            {
                if (!_loaded)
                {
                    _enabled = PlayerPrefs.GetInt(SettingKey, 1) != 0;
                    _loaded = true;
                }
                return _enabled;
            }
            set
            {
                _enabled = value;
                _loaded = true;
                PlayerPrefs.SetInt(SettingKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>How strongly the camera is shaking right now, 0 to 1 (for tests).</summary>
        public static float CurrentTrauma => _instance != null ? _instance._trauma : 0f;

        /// <summary>Something big went off here. strength: 0.2 a light vehicle, 1 the biggest things in the game.</summary>
        public static void Kick(Vector3 where, float strength)
        {
            if (_instance == null || !ShakeEnabled) return;
            _instance._trauma = AddTrauma(_instance._trauma, strength * _instance.NearMiddle(where));
        }

        /// <summary>Adds a kick: several small ones add up, but never past the biggest shake.</summary>
        public static float AddTrauma(float trauma, float strength) => Mathf.Clamp01(Mathf.Max(trauma, strength) + Mathf.Min(trauma, strength) * 0.25f);

        /// <summary>A share of the camera's distance: shake grows with the square of the trauma, so small kicks stay small.</summary>
        public static float ShakeShare(float trauma) => trauma * trauma * MostShare;

        private void Awake()
        {
            _instance = this;
            _control = GetComponent<BattleCameraController>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private float NearMiddle(Vector3 where)
        {
            // 1 in the middle of the view, falling to 0.4 far away.
            Vector3 toThing = where - transform.position;
            float off = 1f - Vector3.Dot(toThing.normalized, transform.forward);
            return Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(off * 8f));
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
            {
                ShakeEnabled = !ShakeEnabled;
                _messageUntil = Time.unscaledTime + MessageSeconds;
            }
        }

        private void LateUpdate()
        {
            if (_trauma <= 0f) return;
            _trauma = Mathf.Max(0f, _trauma - DecayPerSecond * Time.unscaledDeltaTime);
            if (_control == null || !_control.isActiveAndEnabled || !ShakeEnabled) return;

            float amount = ShakeShare(_trauma) * Mathf.Max(20f, transform.position.y / 0.82f);
            float time = Time.unscaledTime * 21f;
            float x = (Mathf.PerlinNoise(time, 0.3f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.7f, time) - 0.5f) * 2f;
            transform.position += (transform.right * x + transform.up * y) * amount;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime >= _messageUntil) return;
            GUI.Label(new Rect(20f, Screen.height - 40f, 300f, 30f), ShakeEnabled ? "Camera shake: on (K)" : "Camera shake: off (K)");
        }
    }
}
