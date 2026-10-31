// MatchAutoplay.cs
// Test tool for built games: plays the Match scene by itself so screens and performance can be
// checked without clicking. It does nothing unless the game is started with "-autoplay", e.g.
//   Halobellum.exe -autoplay -autoplayRounds 3 -autoplaySpeed 4 -autoplayShots C:\temp\shots
// It picks the first draft card, buys up to 2 squads per round of the first affordable unlocked
// unit type, presses Ready, saves screenshots of the draft, each deployment, battle (at 8 s of
// battle time) and result, then quits after the given number of rounds (or when the match ends).
// Depends on: MatchController, BattleController, Halobellum.Economy, UnityEngine.

using System;
using System.Globalization;
using System.IO;
using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(MatchController))]
    public sealed class MatchAutoplay : MonoBehaviour
    {
        private MatchController _match;
        private BattleController _battle;
        private string _shots;
        private int _rounds = 3;
        private float _speed = 4f;
        private float _wait;
        private int _lastShotRound = -1;
        private MatchPhase _lastPhase = (MatchPhase)(-1);
        private bool _battleShotTaken;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-autoplay") < 0)
            {
                enabled = false;
                return;
            }
            _shots = Argument(args, "-autoplayShots");
            if (int.TryParse(Argument(args, "-autoplayRounds"), out int rounds)) _rounds = rounds;
            if (float.TryParse(Argument(args, "-autoplaySpeed"), NumberStyles.Float, CultureInfo.InvariantCulture, out float speed)) _speed = speed;
            if (_shots != null) Directory.CreateDirectory(_shots);
        }

        private void Start()
        {
            _match = GetComponent<MatchController>();
            _battle = GetComponent<BattleController>();
        }

        private void Update()
        {
            Match m = _match.Match;
            if (m == null) return;
            if (m.Phase != _lastPhase)
            {
                _lastPhase = m.Phase;
                _wait = 0.6f; // Let the screen draw before acting or taking a screenshot.
                _battleShotTaken = false;
            }
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0f) return;

            switch (m.Phase)
            {
                case MatchPhase.Draft:
                    Shot("01_draft");
                    _match.ChooseDraft(0);
                    break;
                case MatchPhase.Deployment:
                    if (_lastShotRound != m.Round)
                    {
                        BuySquads(m);
                        _lastShotRound = m.Round;
                        _wait = 0.4f;
                        return;
                    }
                    Shot($"r{m.Round:00}_1_deployment");
                    _battle.Speed = _speed;
                    _match.Ready();
                    break;
                case MatchPhase.Battle:
                    if (!_battleShotTaken && _battle.Simulation != null && _battle.Simulation.ElapsedSeconds >= 8f)
                    {
                        Shot($"r{m.Round:00}_2_battle");
                        _battleShotTaken = true;
                    }
                    break;
                case MatchPhase.Result:
                    Shot($"r{m.Round:00}_3_result");
                    if (m.Round >= _rounds) { Quit(); return; }
                    _match.Continue();
                    break;
                case MatchPhase.MatchOver:
                    Shot("99_match_over");
                    Quit();
                    break;
            }
        }

        private void BuySquads(Match m)
        {
            for (int i = 0; i < m.Rules.NewSquadsPerRound; i++)
            {
                foreach (UnitStats type in m.Player.Roster)
                {
                    if (!m.Player.Unlocked.Contains(type) || m.Player.Credits < type.Cost) continue;
                    if (!DeploymentGrid.FindFreeSpot(m.Player, type, false, m.CombatRules, out int column, out int row)) continue;
                    _match.BeginPlacing(type);
                    _match.PlaceAt(column, row);
                    break;
                }
            }
        }

        private void Shot(string name)
        {
            if (_shots != null) ScreenCapture.CaptureScreenshot(Path.Combine(_shots, name + ".png"));
        }

        private void Quit()
        {
            enabled = false;
            Debug.Log("Autoplay finished.");
            Application.Quit();
        }

        private static string Argument(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
