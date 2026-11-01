// SandboxAutoplay.cs
// Test tool for built games: shows the Sandbox scene working without clicking. It does nothing
// unless the game is started with "-sandboxDemo <screenshot folder>" (MatchAutoplay then opens the
// Sandbox scene). It places a mixed army on each side (ground and air units), saves a screenshot of
// the placement, runs the battle at 4x, saves screenshots at 3 s and 12 s of battle time and at the
// end, then quits. It also saves one screenshot of a lobbed shot at the top of its arc, one of a beam
// charging, one of a beam firing, one of the first infected body and one of a unit leaping, the first time each is
// seen, and close-ups of a baked Marine: held still, in the middle of a cross-fade, running, shooting and falling.
// Depends on: SandboxController, BattleController, Halobellum.Combat, Halobellum.Core, UnityEngine.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Units;
using UnityEngine;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(SandboxController))]
    public sealed class SandboxAutoplay : MonoBehaviour
    {
        public const string Flag = "-sandboxDemo";

        private static readonly string[] Blue = { "Marine", "Marine", "Marine", "Marine", "Scorpion", "Spartan" };
        private static readonly string[] Red = { "Infection Forms", "Infection Forms", "Infection Forms", "Infection Forms", "Carrier Form", "Flood Abomination", "Scarab", "Wraith" };

        private SandboxController _sandbox;
        private BattleController _battle;
        private string _shots;
        private int _step;
        private float _wait = 1f;
        private bool _shotArc, _shotCharge, _shotBeam, _shotInfected, _shotLeap;
        private readonly List<ShotInFlight> _inFlight = new List<ShotInFlight>();
        private readonly List<BeamShot> _beams = new List<BeamShot>();

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index < 0)
            {
                enabled = false;
                return;
            }
            _shots = index + 1 < args.Length ? args[index + 1] : "SandboxShots";
            Directory.CreateDirectory(_shots);
        }

        private void Start()
        {
            _sandbox = GetComponent<SandboxController>();
            _battle = GetComponent<BattleController>();
        }

        private bool _closeUp, _shotFade;
        private readonly HashSet<BakedAnimation> _shotRoles = new HashSet<BakedAnimation>();

        /// <summary>Freezes the battle, looks closely at a body, saves a screenshot, and carries on.</summary>
        private IEnumerator CloseUp(SimUnit unit, string name)
        {
            _closeUp = true;
            float speed = _battle.Speed;
            _battle.Speed = 0f;
            Camera view = Camera.main;
            var control = view.GetComponent<BattleCameraController>();
            if (control != null) control.enabled = false;
            view.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
            var body = new Vector3(unit.Position.X, 0.8f, unit.Position.Y);
            var facing = new Vector3(unit.Facing.X, 0f, unit.Facing.Y);
            view.transform.position = body + facing * 3.4f + Vector3.Cross(facing, Vector3.up) * 1.8f + Vector3.up * 0.9f;
            view.transform.LookAt(body);
            yield return null;
            Shot(name);
            yield return null;
            yield return null;
            view.transform.SetPositionAndRotation(position, rotation);
            if (control != null) control.enabled = true;
            _battle.Speed = speed;
            _closeUp = false;
        }

        private void ShootMarine()
        {
            BattleSimulation simulation = _battle.Simulation;
            if (simulation == null || _closeUp) return;
            var views = GetComponent<UnitViewManager>();
            foreach (SimUnit unit in simulation.Units)
            {
                if (unit.Stats.Name != "Marine") continue;
                BakedAnimation? playing = views.PlayingAnimation(unit);
                if (playing == null) return;
                // The fade takes 0.2 battle seconds and starts with the battle.
                if (_step >= 5 && !_shotFade && simulation.ElapsedSeconds >= 0.1f)
                {
                    _shotFade = true;
                    StartCoroutine(CloseUp(unit, "marine_fade"));
                    return;
                }
                bool settled = simulation.ElapsedSeconds >= 1f;
                if (playing == BakedAnimation.Still && _step == 4 && _shotRoles.Add(BakedAnimation.Still))
                {
                    StartCoroutine(CloseUp(unit, "marine_still"));
                    return;
                }
                if (settled && playing == BakedAnimation.Moving && unit.IsAlive && _shotRoles.Add(BakedAnimation.Moving))
                {
                    StartCoroutine(CloseUp(unit, "marine_moving"));
                    return;
                }
                if (settled && playing == BakedAnimation.Shooting && unit.IsAlive && unit.Weapons[0].Target != null && _shotRoles.Add(BakedAnimation.Shooting))
                {
                    StartCoroutine(CloseUp(unit, "marine_shooting"));
                    return;
                }
                // A corpse that rises as infected is gone before it has finished falling: try the next one.
                if (unit == _deadSeen && !views.IsShown(unit))
                {
                    _deadSeen = null;
                    _deadShotAt = 0f;
                }
                if (playing == BakedAnimation.Dead && !unit.IsAlive && views.IsShown(unit) && !_shotRoles.Contains(BakedAnimation.Dead))
                {
                    if (_deadSeen == null) _deadSeen = unit;
                    if (_deadSeen == unit && Time.unscaledTime >= _deadShotAt && _deadShotAt > 0f)
                    {
                        _shotRoles.Add(BakedAnimation.Dead);
                        StartCoroutine(CloseUp(unit, "marine_dead"));
                        return;
                    }
                    if (_deadSeen == unit && _deadShotAt <= 0f) _deadShotAt = Time.unscaledTime + 0.6f; // At 4x: well into the fall.
                }
            }
        }

        private SimUnit _deadSeen;
        private float _deadShotAt;

        private void Update()
        {
            if (_step >= 4) ShootMarine();
            if (_closeUp) return;
            if (_step >= 5) ShootWeaponMechanics();
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0f) return;
            switch (_step)
            {
                case 0:
                    foreach (string name in Blue) _sandbox.AddSquad(Team.A, Find(name));
                    foreach (string name in Red) _sandbox.AddSquad(Team.B, Find(name));
                    _step++;
                    _wait = 0.5f;
                    break;
                case 1:
                    // An item selected (hexagons over the squads) and one already on a squad.
                    if (_sandbox.Items.Count > 1)
                    {
                        _sandbox.BeginPlacingItem(_sandbox.Items[0]);
                        _sandbox.PlaceItemOn(_sandbox.ArmyA.Squads[0]);
                        _sandbox.BeginPlacingItem(_sandbox.Items[1]);
                    }
                    _step++;
                    _wait = 0.4f;
                    break;
                case 2:
                    Shot("0_items");
                    _step++;
                    _wait = 0.3f;
                    break;
                case 3:
                    _sandbox.CancelPlacement();
                    Shot("1_placement");
                    _step++;
                    _wait = 0.3f;
                    break;
                case 4:
                    _battle.Speed = 4f;
                    _sandbox.Run();
                    _step++;
                    break;
                case 5:
                    if (_battle.Simulation.ElapsedSeconds >= 3f) { Shot("2_battle_3s"); _step++; }
                    break;
                case 6:
                    if (_battle.Simulation.ElapsedSeconds >= 12f || _battle.Simulation.IsFinished) { Shot("3_battle_12s"); _step++; }
                    break;
                case 7:
                    if (_battle.Simulation.IsFinished) { _step++; _wait = 0.5f; }
                    break;
                case 8:
                    Shot("4_result");
                    _step++;
                    _wait = 0.5f;
                    break;
                default:
                    enabled = false;
                    int infected = 0;
                    foreach (SimUnit unit in _battle.Simulation.Units) if (unit.Stats.InfectedFrom != null) infected++;
                    Debug.Log("Sandbox demo finished: " + _battle.Simulation.Result.ToSummaryLine() + $"; {infected} rose infected");
                    Application.Quit();
                    break;
            }
        }

        private void ShootWeaponMechanics()
        {
            BattleSimulation simulation = _battle.Simulation;
            if (simulation == null) return;
            if (!_shotArc)
            {
                simulation.GetProjectiles(_inFlight);
                foreach (ShotInFlight shot in _inFlight)
                {
                    if (shot.ArcLength <= 0f || shot.Progress < 0.4f || shot.Progress > 0.6f) continue;
                    Shot("arc");
                    _shotArc = true;
                    break;
                }
            }
            if (!_shotLeap)
            {
                int leaping = 0;
                foreach (SimUnit unit in simulation.Units) if (unit.IsAlive && unit.IsLeaping && unit.LeapProgress > 0.3f) leaping++;
                if (leaping >= 5)
                {
                    Shot("leap");
                    _shotLeap = true;
                }
            }
            if (!_shotInfected)
            {
                foreach (SimUnit unit in simulation.Units)
                {
                    if (unit.Stats.InfectedFrom == null) continue;
                    Shot("infected");
                    _shotInfected = true;
                    break;
                }
            }
            simulation.GetBeams(_beams);
            foreach (BeamShot beam in _beams)
            {
                if (beam.Firing && !_shotBeam)
                {
                    Shot("beam_firing");
                    _shotBeam = true;
                }
                else if (!beam.Firing && beam.Charge > 0.6f && !_shotCharge)
                {
                    Shot("beam_charging");
                    _shotCharge = true;
                }
            }
        }

        private UnitStats Find(string name)
        {
            foreach (SandboxController.SandboxUnit unit in _sandbox.Units)
            {
                if (unit.Stats.Name == name) return unit.Stats;
            }
            throw new InvalidOperationException("No sandbox unit named " + name);
        }

        private void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_shots, name + ".png"));
    }
}
