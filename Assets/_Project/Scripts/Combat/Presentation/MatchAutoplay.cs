// MatchAutoplay.cs
// Test tool for built games: plays the Match scene by itself so screens and performance can be
// checked without clicking. It does nothing unless the game is started with "-autoplay", e.g.
//   Halobellum.exe -autoplay -autoplayRounds 3 -autoplaySpeed 4 -autoplayShots C:\temp\shots
// It picks the first draft card, takes the first affordable card on each card screen (Milestone 7),
// buys up to 2 squads per round of the first affordable unlocked unit type, places every ready spell
// (offensive ones on the enemy's last-seen army, defensive ones on its own first squad), puts items
// on its squads, uses Intensive Training, presses Ready, saves screenshots of the draft, each card screen, the bought squads falling in, each
// deployment, battle (at 1.2 s of battle time, when spells go off, and again at 8, 16 and 26 s, for the effects) and result, then quits after the
// given number of rounds (or when the match ends).
// With "-sandboxDemo <folder>" it opens the Sandbox scene instead (see SandboxAutoplay).
// Depends on: MatchController, BattleController, SandboxAutoplay, Halobellum.Economy, UnityEngine.

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
        private int _card;
        private string _faction;
        private string _subFaction;
        private float _wait;
        private int _lastShotRound = -1;
        private MatchPhase _lastPhase = (MatchPhase)(-1);
        private bool _battleShotTaken;
        private bool _dropShotPending;

        private void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, SandboxAutoplay.Flag) >= 0)
            {
                // The sandbox demo runs in the Sandbox scene (see SandboxAutoplay).
                enabled = false;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Sandbox");
                return;
            }
            if (Array.IndexOf(args, "-autoplay") < 0)
            {
                enabled = false;
                return;
            }
            _shots = Argument(args, "-autoplayShots");
            if (int.TryParse(Argument(args, "-autoplayRounds"), out int rounds)) _rounds = rounds;
            if (float.TryParse(Argument(args, "-autoplaySpeed"), NumberStyles.Float, CultureInfo.InvariantCulture, out float speed)) _speed = speed;
            if (int.TryParse(Argument(args, "-autoplayCard"), out int card)) _card = card; // Which draft card to pick (default 0).
            _faction = Argument(args, "-autoplayFaction"); // Or the first card of this faction, e.g. UNSC.
            _subFaction = Argument(args, "-autoplaySubFaction"); // Or of this sub-faction, e.g. Noble. If the draw has none, a new match is drawn.
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
                _phaseShotTaken = false;
            }
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0f) return;

            // Each screen: first take its screenshot, then act a moment later (a screenshot is saved
            // at the end of the frame, so acting in the same frame would capture the next screen).
            switch (m.Phase)
            {
                case MatchPhase.Draft:
                    if (ShotFirst("01_draft")) return;
                    if (!string.IsNullOrEmpty(_subFaction) && !m.PlayerDraft.Exists(c => c.SubFaction.Name == _subFaction))
                    {
                        _match.NewMatch();
                        return;
                    }
                    int pick = Mathf.Clamp(_card, 0, m.PlayerDraft.Count - 1);
                    if (!string.IsNullOrEmpty(_subFaction)) _card = pick = m.PlayerDraft.FindIndex(c => c.SubFaction.Name == _subFaction);
                    if (!string.IsNullOrEmpty(_faction))
                    {
                        int byFaction = m.PlayerDraft.FindIndex(c => c.Faction.Name == _faction);
                        if (byFaction >= 0) pick = byFaction;
                    }
                    _match.ChooseDraft(pick);
                    break;
                case MatchPhase.Cards:
                    if (ShotFirst($"r{m.Round:00}_c_cards")) return;
                    TakeFirstAffordableCard(m);
                    break;
                case MatchPhase.Deployment:
                    if (_lastShotRound != m.Round)
                    {
                        BuySquads(m);
                        PlaceSpells(m);
                        _lastShotRound = m.Round;
                        _dropShotPending = true;
                        _wait = 0.25f; // Bought squads drop in: one shot mid-fall, then let them land.
                        return;
                    }
                    if (_dropShotPending)
                    {
                        Shot($"r{m.Round:00}_0_drop");
                        _dropShotPending = false;
                        _wait = 0.8f;
                        return;
                    }
                    // With an item in the inventory: one shot with the item selected (hexagons over the squads).
                    if (m.Player.Inventory.Count > 0 && _inventoryShotRound != m.Round)
                    {
                        if (_match.PlacingItem == null)
                        {
                            _match.BeginPlacingItem(m.Player.Inventory[0]);
                            _wait = 0.4f;
                            return;
                        }
                        Shot($"r{m.Round:00}_1a_inventory");
                        _inventoryShotRound = m.Round;
                        _wait = 0.4f;
                        return;
                    }
                    if (_inventoryShotRound == m.Round || m.Player.Inventory.Count == 0)
                    {
                        _match.CancelItemPlacement();
                        UseItemsAndTraining(m);
                    }
                    if (ShotFirst($"r{m.Round:00}_1_deployment")) return;
                    _battle.Speed = _speed;
                    _match.Ready();
                    break;
                case MatchPhase.Battle:
                    if (_match.Ending != null)
                    {
                        if (_endingShots < EndingShotSeconds.Length && _match.Ending.Clock >= EndingShotSeconds[_endingShots])
                        {
                            Shot($"r{m.Round:00}_2_ending_{_endingShots}");
                            _endingShots++;
                        }
                        break;
                    }
                    _endingShots = 0;
                    if (!_battleShotTaken && _battle.Simulation != null && _battle.Simulation.ElapsedSeconds >= 1.2f)
                    {
                        Shot($"r{m.Round:00}_2_battle");
                        _battleShotTaken = true;
                        _nextLaterShot = 0;
                    }
                    if (_battleShotTaken && _nextLaterShot < LaterShotSeconds.Length && _battle.Simulation != null && _battle.Simulation.ElapsedSeconds >= LaterShotSeconds[_nextLaterShot])
                    {
                        Shot($"r{m.Round:00}_2_battle_{LaterShotSeconds[_nextLaterShot]:00}s");
                        _nextLaterShot++;
                    }
                    break;
                case MatchPhase.Result:
                    if (ShotFirst($"r{m.Round:00}_3_result")) return;
                    if (m.Round >= _rounds) { Quit(); return; }
                    _match.Continue();
                    break;
                case MatchPhase.MatchOver:
                    if (ShotFirst("99_match_over")) return;
                    Quit();
                    break;
            }
        }

        private static readonly float[] LaterShotSeconds = { 8f, 16f, 26f };
        private int _nextLaterShot;
        private int _endingShots;
        /// <summary>The nodes on their way, the first hits, and the crowd's hits with their numbers.</summary>
        private static readonly float[] EndingShotSeconds = { 0.7f, 1.05f, 1.35f };
        private bool _phaseShotTaken;
        private int _inventoryShotRound = -1;

        private void TakeFirstAffordableCard(Match m)
        {
            CardOffer offer = m.Player.Offer;
            for (int i = 0; offer != null && i < offer.Count; i++)
            {
                if (offer.PriceOf(i) <= m.Player.Credits && _match.ChooseCard(i).Ok) return;
            }
            _match.SkipCards();
        }

        private void UseItemsAndTraining(Match m)
        {
            foreach (ItemStats item in new System.Collections.Generic.List<ItemStats>(m.Player.Inventory))
            {
                PlacedSquad free = m.Player.Squads.Find(s => s.Item == null);
                if (free == null) break;
                _match.BeginPlacingItem(item);
                _match.PlaceItemOn(free);
            }
            if (m.CanUseTraining(Team.A) && m.Player.Squads.Count > 0) _match.UseTraining(m.Player.Squads[0]);
        }

        private void PlaceSpells(Match m)
        {
            foreach (OwnedSpell spell in m.Player.Spells)
            {
                if (!spell.IsReady(m.Round)) continue;
                Vec2 target = new Vec2(0f, 30f);
                if (spell.Stats.Offensive && m.SeenArmy(Team.B).Count > 0)
                {
                    SeenSquad seen = m.SeenArmy(Team.B)[0];
                    target = DeploymentGrid.WorldCentre(seen.Type, seen.Column, seen.Row, seen.Rotated90, Team.B, m.CombatRules);
                }
                else if (!spell.Stats.Offensive && m.Player.Squads.Count > 0)
                {
                    PlacedSquad own = m.Player.Squads[0];
                    target = DeploymentGrid.WorldCentre(own.Type, own.Column, own.Row, own.Rotated90, Team.A, m.CombatRules);
                }
                _match.BeginPlacingSpell(spell);
                if (!_match.PlaceSpellAt(target).Ok) _match.CancelSpellPlacement();
            }
        }

        /// <summary>Takes this screen's screenshot once and returns true (wait); returns false once taken (act).</summary>
        private bool ShotFirst(string name)
        {
            if (_phaseShotTaken) return false;
            Shot(name);
            _phaseShotTaken = true;
            _wait = 0.3f;
            return true;
        }

        private void BuySquads(Match m)
        {
            // Asked for a sub-faction: show its exclusive units first.
            if (!string.IsNullOrEmpty(_subFaction))
            {
                foreach (UnitStats type in m.Player.SubFaction.ExclusiveUnits)
                {
                    if (m.Player.Unlocked.Contains(type)) continue;
                    if (m.Player.Credits >= type.UnlockCost + type.Cost && _match.Unlock(type).Ok) break;
                }
                for (int i = m.Player.SubFaction.ExclusiveUnits.Count - 1; i >= 0; i--)
                {
                    UnitStats type = m.Player.SubFaction.ExclusiveUnits[i];
                    if (m.Player.Unlocked.Contains(type) && m.Player.Credits >= type.Cost) _match.Buy(type);
                }
            }
            for (int i = 0; i < m.Rules.NewSquadsPerRound; i++)
            {
                foreach (UnitStats type in m.Player.Roster)
                {
                    if (!m.Player.Unlocked.Contains(type) || m.Player.Credits < type.Cost) continue;
                    if (_match.Buy(type).Ok) break;
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
