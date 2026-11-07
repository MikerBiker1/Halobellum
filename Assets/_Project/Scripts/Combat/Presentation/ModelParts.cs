// ModelParts.cs
// What a real model's own nodes do in battle (M9, decision D45). Prepared models name their nodes (Tools/Art/model_tools.py):
//   Muzzle_<weapon>[letter]  where that weapon's shots start; several letters take turns
//   Turret_<weapon>[letter]  turns toward that weapon's target, around its own origin
//   Wheel_<n>                spins with the distance the unit moves
//   Swing_<n>_<phase %>[_<degrees>[_<cycles per second>]]  swings to and fro while the unit moves, like a leg, around its own origin
//   Flap_L, Flap_R           beats up and down all the time, like a wing, around its own origin
//   Motion_Hover             the whole body bobs a little, as a hover vehicle does
//   Motion_Wobble            the whole body sways and squashes, as a Flood form does
//   Thruster_<size>          a glow this many metres wide that lights up while the unit moves and goes out when it stops (the Banshee's wing tips,
//                            the Ghost's fins: the owner, 2026-11-07, D82). It always faces the camera.
// Air units always bob, and bank into their turns. All of this is looks only: it reads the simulation and changes nothing.
// It runs on battle time, so it pauses with the battle and speeds up with it.
// Depends on: Halobellum.Combat, UnityEngine.

using System.Collections.Generic;
using System.Globalization;
using Halobellum.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace Halobellum.Presentation
{
    public sealed class ModelParts
    {
        private const float TurretDegreesPerSecond = 120f;
        // A big walker's legs are long: much more than this and its feet go through the ground.
        private const float SwingAngle = 4f;
        private const float SwingCyclesPerSecond = 0.5f;
        private const float FlapAngle = 30f;
        private const float FlapCyclesPerSecond = 2.2f;
        private const float AirBobHeight = 0.3f;
        private const float HoverBobHeight = 0.07f;
        private const float BobSpeed = 1.3f;
        private const float BankPerDegreePerSecond = 0.25f;
        private const float MostBank = 22f;
        private const float MovingPitch = 5f;
        /// <summary>A Covenant engine's blue-violet, and how far above 1 it goes for the bloom to catch it.</summary>
        private static readonly Color ThrusterColour = new Color(0.3f, 0.5f, 1f);
        private const float ThrusterBoost = 6f;
        private static Material _thrusterMaterial;
        private static Mesh _thrusterQuad;

        private sealed class Turret
        {
            public Transform Node;
            public int Weapon;
            public Quaternion Rest;
            public float Yaw;
        }

        private sealed class Wheel
        {
            public Transform Node;
            public float Radius;
        }

        private sealed class Swing
        {
            public Transform Node;
            public Quaternion Rest;
            public float Phase;
            public float Angle, Cycles;
        }

        private sealed class Thruster
        {
            public Transform Glow;
            public Renderer Renderer;
            public float Size;
        }

        private sealed class Flap
        {
            public Transform Node;
            public Quaternion Rest;
            /// <summary>The line from nose to tail, in the wing's own axes, and which way round it beats.</summary>
            public Vector3 Axis;
        }

        private readonly Transform _root, _body;
        private readonly Dictionary<int, List<Transform>> _muzzles = new Dictionary<int, List<Transform>>();
        private readonly List<Turret> _turrets = new List<Turret>();
        private readonly List<Wheel> _wheels = new List<Wheel>();
        private readonly List<Swing> _swings = new List<Swing>();
        private readonly List<Flap> _flaps = new List<Flap>();
        private readonly List<Thruster> _thrusters = new List<Thruster>();
        private readonly Vector3 _restPosition, _restScale;
        private readonly Quaternion _restRotation;
        private readonly float _phase;
        private bool _air, _hover, _wobble;
        private bool _started;
        private Vector3 _lastPosition;
        private float _lastYaw, _bank, _moving;

        private ModelParts(Transform root, Transform body, float phase)
        {
            _root = root;
            _body = body;
            _phase = phase;
            _restPosition = body.localPosition;
            _restRotation = body.localRotation;
            _restScale = body.localScale;
        }

        /// <summary>Reads a body's nodes. Returns null if nothing about it ever moves, so that it costs nothing.</summary>
        public static ModelParts Find(Transform root, Transform body, bool isAir, float phase)
        {
            var parts = new ModelParts(root, body, phase) { _air = isAir };
            Quaternion inverseRoot = Quaternion.Inverse(root.rotation);
            foreach (Transform node in body.GetComponentsInChildren<Transform>(true))
            {
                string name = node.name;
                if (name.StartsWith("Muzzle_"))
                {
                    int weapon = WeaponOf(name, "Muzzle_".Length);
                    if (!parts._muzzles.TryGetValue(weapon, out List<Transform> list)) parts._muzzles[weapon] = list = new List<Transform>();
                    list.Add(node);
                }
                else if (name.StartsWith("Turret_"))
                {
                    parts._turrets.Add(new Turret { Node = node, Weapon = WeaponOf(name, "Turret_".Length), Rest = inverseRoot * node.rotation });
                }
                else if (name.StartsWith("Wheel_"))
                {
                    Renderer renderer = node.GetComponent<Renderer>();
                    parts._wheels.Add(new Wheel { Node = node, Radius = renderer != null ? Mathf.Max(0.05f, renderer.bounds.extents.y) : 0.5f });
                }
                else if (name.StartsWith("Swing_"))
                {
                    string[] words = name.Split('_');
                    int.TryParse(words[2], out int percent);
                    float angle = SwingAngle, cycles = SwingCyclesPerSecond;
                    if (words.Length > 3) float.TryParse(words[3], NumberStyles.Float, CultureInfo.InvariantCulture, out angle);
                    if (words.Length > 4) float.TryParse(words[4], NumberStyles.Float, CultureInfo.InvariantCulture, out cycles);
                    parts._swings.Add(new Swing { Node = node, Rest = inverseRoot * node.rotation, Phase = percent / 100f, Angle = angle, Cycles = cycles });
                }
                else if (name.StartsWith("Flap_"))
                {
                    float sign = name.StartsWith("Flap_L") ? 1f : -1f;
                    parts._flaps.Add(new Flap { Node = node, Rest = node.localRotation, Axis = Quaternion.Inverse(node.rotation) * root.forward * sign });
                }
                else if (name.StartsWith("Thruster_"))
                {
                    float.TryParse(name.Substring("Thruster_".Length), NumberStyles.Float, CultureInfo.InvariantCulture, out float size);
                    Thruster thruster = MakeThruster(node, size > 0f ? size : 0.6f);
                    if (thruster != null) parts._thrusters.Add(thruster);
                }
                else if (name == "Motion_Hover") parts._hover = true;
                else if (name == "Motion_Wobble") parts._wobble = true;
            }
            foreach (List<Transform> list in parts._muzzles.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            bool any = parts._air || parts._hover || parts._wobble || parts._muzzles.Count > 0 || parts._turrets.Count > 0 || parts._wheels.Count > 0 || parts._swings.Count > 0 || parts._flaps.Count > 0 || parts._thrusters.Count > 0;
            return any ? parts : null;
        }

        /// <summary>A glow on a Thruster_ node, unlit until the unit moves. Null without a graphics device, where nothing is drawn anyway.</summary>
        private static Thruster MakeThruster(Transform node, float size)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;
            if (_thrusterMaterial == null) _thrusterMaterial = EffectAssets.ParticleMaterial("Thruster Glow", EffectAssets.Soft, ThrusterBoost, 0f, 0f, 3050);
            if (_thrusterQuad == null)
            {
                _thrusterQuad = new Mesh { name = "Thruster Glow", hideFlags = HideFlags.DontSave };
                _thrusterQuad.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
                _thrusterQuad.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
                _thrusterQuad.SetColors(new[] { ThrusterColour, ThrusterColour, ThrusterColour, ThrusterColour });
                _thrusterQuad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                _thrusterQuad.RecalculateBounds();
            }
            var glow = new GameObject("Glow");
            glow.transform.SetParent(node, false);
            glow.AddComponent<MeshFilter>().sharedMesh = _thrusterQuad;
            var renderer = glow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _thrusterMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return new Thruster { Glow = glow.transform, Renderer = renderer, Size = size };
        }

        private static int WeaponOf(string name, int at)
        {
            return at < name.Length && char.IsDigit(name[at]) ? name[at] - '0' : 0;
        }

        /// <summary>Where a weapon's given shot starts. False if the model doesn't say.</summary>
        public bool TryMuzzle(int weapon, int shot, out Vector3 position)
        {
            position = default;
            if (!_muzzles.TryGetValue(weapon, out List<Transform> list))
            {
                // A model with one muzzle fires everything from it.
                if (_muzzles.Count != 1) return false;
                foreach (List<Transform> only in _muzzles.Values) list = only;
            }
            position = list[(shot & int.MaxValue) % list.Count].position;
            return true;
        }

        /// <summary>One frame of movement. Delta is battle seconds, so nothing moves while the battle stands still.</summary>
        public void Animate(SimUnit unit, float delta, float time)
        {
            if (delta <= 0f) return;
            Vector3 position = _root.position;
            float yaw = _root.eulerAngles.y;
            float moved = _started ? new Vector2(position.x - _lastPosition.x, position.z - _lastPosition.z).magnitude : 0f;
            float turned = _started ? Mathf.DeltaAngle(_lastYaw, yaw) : 0f;
            _started = true;
            _lastPosition = position;
            _lastYaw = yaw;
            _moving = Mathf.MoveTowards(_moving, moved > 1e-4f ? 1f : 0f, delta * 3f);

            foreach (Turret turret in _turrets) Aim(turret, unit, delta);
            foreach (Wheel wheel in _wheels) wheel.Node.Rotate(_root.right, moved / wheel.Radius * Mathf.Rad2Deg, Space.World);
            foreach (Swing swing in _swings)
            {
                float angle = Mathf.Sin((time * swing.Cycles + swing.Phase) * Mathf.PI * 2f) * swing.Angle * _moving;
                swing.Node.rotation = Quaternion.AngleAxis(angle, _root.right) * _root.rotation * swing.Rest;
            }
            foreach (Flap flap in _flaps)
            {
                float beat = Mathf.Sin((time + _phase) * FlapCyclesPerSecond * Mathf.PI * 2f) * FlapAngle;
                flap.Node.localRotation = flap.Rest * Quaternion.AngleAxis(beat, flap.Axis);
            }

            if (_thrusters.Count > 0) Light(time);

            if (_air) Fly(time, turned / delta, delta);
            else if (_wobble) Wobble(time, unit.State == UnitState.Attack);
            else if (_hover) _body.localPosition = _restPosition + Vector3.up * (Mathf.Sin((time + _phase) * BobSpeed * 2f) * HoverBobHeight);
        }

        /// <summary>The thrusters burn while the unit moves, flickering a little, and go out as it stops.</summary>
        private void Light(float time)
        {
            Camera camera = Camera.main;
            foreach (Thruster thruster in _thrusters)
            {
                bool lit = _moving > 0.02f;
                if (thruster.Renderer.enabled != lit) thruster.Renderer.enabled = lit;
                if (!lit) continue;
                float flicker = 1f + 0.08f * Mathf.Sin((time + _phase) * 31f) + 0.05f * Mathf.Sin((time + _phase) * 53f);
                float size = thruster.Size * _moving * flicker;
                Vector3 parentScale = thruster.Glow.parent.lossyScale;
                thruster.Glow.localScale = new Vector3(size / Mathf.Max(1e-4f, parentScale.x), size / Mathf.Max(1e-4f, parentScale.y), 1f / Mathf.Max(1e-4f, parentScale.z));
                if (camera != null) thruster.Glow.rotation = camera.transform.rotation;
            }
        }

        private void Aim(Turret turret, SimUnit unit, float delta)
        {
            float wanted = 0f;
            int index = Mathf.Min(turret.Weapon, unit.Weapons.Length - 1);
            if (index >= 0)
            {
                SimWeapon weapon = unit.Weapons[index];
                SimUnit target = weapon.BeamTarget ?? weapon.Target;
                if (target != null && target.IsAlive)
                {
                    var towards = new Vector3(target.Position.X - _root.position.x, 0f, target.Position.Y - _root.position.z);
                    if (towards.sqrMagnitude > 1e-4f) wanted = Vector3.SignedAngle(_root.forward, towards, Vector3.up);
                }
            }
            turret.Yaw = Mathf.MoveTowardsAngle(turret.Yaw, wanted, TurretDegreesPerSecond * delta);
            turret.Node.rotation = Quaternion.AngleAxis(turret.Yaw, _root.up) * _root.rotation * turret.Rest;
        }

        private void Fly(float time, float degreesPerSecond, float delta)
        {
            float wantedBank = Mathf.Clamp(-degreesPerSecond * BankPerDegreePerSecond, -MostBank, MostBank);
            _bank = Mathf.MoveTowards(_bank, wantedBank, 40f * delta);
            _body.localRotation = Quaternion.Euler(MovingPitch * _moving, 0f, _bank) * _restRotation;
            _body.localPosition = _restPosition + Vector3.up * (Mathf.Sin((time + _phase) * BobSpeed) * AirBobHeight);
        }

        private void Wobble(float time, bool attacking)
        {
            float speed = attacking ? 9f : Mathf.Lerp(2.5f, 7f, _moving);
            float beat = (time + _phase) * speed;
            float sway = Mathf.Sin(beat);
            float reach = attacking ? 1f : Mathf.Lerp(0.4f, 1f, _moving);
            _body.localRotation = Quaternion.Euler((attacking ? 8f : 3f) * Mathf.Cos(beat * 0.5f) * reach, 0f, 7f * sway * reach) * _restRotation;
            float squash = Mathf.Sin(beat * 2f) * 0.04f * reach;
            _body.localScale = new Vector3(_restScale.x * (1f - squash * 0.5f), _restScale.y * (1f + squash), _restScale.z * (1f - squash * 0.5f));
        }
    }
}
