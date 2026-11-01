// SandboxAutoplay.cs
// Test tool for built games: shows the Sandbox scene working without clicking. It does nothing
// unless the game is started with "-sandboxDemo <screenshot folder>" (MatchAutoplay then opens the
// Sandbox scene). It places a mixed army on each side (ground and air units), saves a screenshot of
// the placement, runs the battle at 4x, saves screenshots at 3 s and 12 s of battle time and at the
// end, then quits. It also saves one screenshot of a lobbed shot at the top of its arc, one of a beam
// charging, one of a beam firing, one of the first infected body and one of a unit leaping, the first time each is seen.
// Depends on: SandboxController, BattleController, Halobellum.Combat, Halobellum.Core, UnityEngine.

using System;
using System.Collections.Generic;
using System.IO;
using Halobellum.Combat;
using Halobellum.Core;
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

        private void Update()
        {
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
