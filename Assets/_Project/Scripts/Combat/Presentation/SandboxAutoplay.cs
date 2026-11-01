// SandboxAutoplay.cs
// Test tool for built games: shows the Sandbox scene working without clicking. It does nothing
// unless the game is started with "-sandboxDemo <screenshot folder>" (MatchAutoplay then opens the
// Sandbox scene). It places a mixed army on each side (including a Pelican, whose Marines drop at
// 10 s), saves a screenshot of the placement, runs the battle at 4x, saves screenshots at 3 s and
// 12 s of battle time and at the end, then quits.
// Depends on: SandboxController, BattleController, Halobellum.Core, UnityEngine.

using System;
using System.IO;
using Halobellum.Core;
using UnityEngine;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(SandboxController))]
    public sealed class SandboxAutoplay : MonoBehaviour
    {
        public const string Flag = "-sandboxDemo";

        private static readonly string[] Blue = { "Marine", "Marine", "Scorpion", "Pelican", "Warthog", "Spartan" };
        private static readonly string[] Red = { "Grunt", "Grunt", "Jackal (Shield)", "Elite", "Wraith", "Banshee" };

        private SandboxController _sandbox;
        private BattleController _battle;
        private string _shots;
        private int _step;
        private float _wait = 1f;

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
                    Shot("1_placement");
                    _step++;
                    _wait = 0.3f;
                    break;
                case 2:
                    _battle.Speed = 4f;
                    _sandbox.Run();
                    _step++;
                    break;
                case 3:
                    if (_battle.Simulation.ElapsedSeconds >= 3f) { Shot("2_battle_3s"); _step++; }
                    break;
                case 4:
                    if (_battle.Simulation.ElapsedSeconds >= 12f || _battle.Simulation.IsFinished) { Shot("3_battle_12s"); _step++; }
                    break;
                case 5:
                    if (_battle.Simulation.IsFinished) { _step++; _wait = 0.5f; }
                    break;
                case 6:
                    Shot("4_result");
                    _step++;
                    _wait = 0.5f;
                    break;
                default:
                    enabled = false;
                    Debug.Log("Sandbox demo finished: " + _battle.Simulation.Result.ToSummaryLine());
                    Application.Quit();
                    break;
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
