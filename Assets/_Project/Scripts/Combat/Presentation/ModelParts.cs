// ModelParts.cs
// What a real model's own nodes do in battle (M9, decision D45). Prepared models name their nodes (Tools/Art/model_tools.py):
//   Muzzle_<weapon>[letter]  where that weapon's shots start; several letters take turns
//   Turret_<weapon>[letter]  turns toward that weapon's target, around its own origin
//   Wheel_<n>                spins with the distance the unit moves
//   Swing_<n>_<phase %>      swings to and fro while the unit moves, like a leg, around its own origin
//   Motion_Hover             the whole body bobs a little, as a hover vehicle does
//   Motion_Wobble            the whole body sways and squashes, as a Flood form does
// Air units always bob, and bank into their turns. All of this is looks only: it reads the simulation and changes nothing.
// It runs on battle time, so it pauses with the battle and speeds up with it.
// Depends on: Halobellum.Combat, UnityEngine.

using System.Collections.Generic;
using Halobellum.Combat;
using UnityEngine;

namespace Halobellum.Presentation
{
    public sealed class ModelParts
    {
        private const float TurretDegreesPerSecond = 120f;
        // A big walker's legs are long: much more than this and its feet go through the ground.
        private const float SwingAngle = 4f;
        private const float SwingCyclesPerSecond = 0.5f;
        private const float AirBobHeight = 0.3f;
        private const float HoverBobHeight = 0.07f;
        private const float BobSpeed = 1.3f;
        private const float BankPerDegreePerSecond = 0.25f;
        private const float MostBank = 22f;
        private const float MovingPitch = 5f;

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
        }

        private readonly Transform _root, _body;
        private readonly Dictionary<int, List<Transform>> _muzzles = new Dictionary<int, List<Transform>>();
        private readonly List<Turret> _turrets = new List<Turret>();
        private readonly List<Wheel> _wheels = new List<Wheel>();
        private readonly List<Swing> _swings = new List<Swing>();
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
                    int split = name.LastIndexOf('_');
                    int.TryParse(name.Substring(split + 1), out int percent);
                    parts._swings.Add(new Swing { Node = node, Rest = inverseRoot * node.rotation, Phase = percent / 100f });
                }
                else if (name == "Motion_Hover") parts._hover = true;
                else if (name == "Motion_Wobble") parts._wobble = true;
            }
            foreach (List<Transform> list in parts._muzzles.Values) list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            bool any = parts._air || parts._hover || parts._wobble || parts._muzzles.Count > 0 || parts._turrets.Count > 0 || parts._wheels.Count > 0 || parts._swings.Count > 0;
            return any ? parts : null;
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
                float angle = Mathf.Sin((time * SwingCyclesPerSecond + swing.Phase) * Mathf.PI * 2f) * SwingAngle * _moving;
                swing.Node.rotation = Quaternion.AngleAxis(angle, _root.right) * _root.rotation * swing.Rest;
            }

            if (_air) Fly(time, turned / delta, delta);
            else if (_wobble) Wobble(time, unit.State == UnitState.Attack);
            else if (_hover) _body.localPosition = _restPosition + Vector3.up * (Mathf.Sin((time + _phase) * BobSpeed * 2f) * HoverBobHeight);
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
