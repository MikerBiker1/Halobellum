// MatchController.cs
// Runs a match in the Match scene (Bible Section 5). It owns the plain C# Match and connects it to
// the scene: during deployment it shows the player's squads as a preview battle (the AI's squads
// stay hidden), forwards the player's actions (unlock, buy, move, turn, sell), and on Ready hands
// the round's battle to the BattleController to play. When the battle ends it applies the result
// (base damage, income) and the HUD shows the result screen. A bought squad is placed in the middle
// of the zone, drops in from high above and is selected when it lands, ready to be moved.
// The HUD (Scripts/UI/MatchHud) and DeploymentInput call the public methods here; tests do too.
// Depends on: BattleController, SquadSelection, UnitViewManager,
//             Halobellum.Economy (Match, DoNothingAi, DeploymentGrid),
//             Halobellum.Units (MatchRulesDefinition, FactionDefinition, UnitDefinition), UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Units;
using UnityEngine;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(BattleController))]
    public sealed class MatchController : MonoBehaviour, IDeploymentPlacement
    {
        [SerializeField] private MatchRulesDefinition matchRules;
        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();
        [Tooltip("Match seed. 0 = a new random match every time.")]
        [SerializeField] private int seed;

        private BattleController _battle;
        private SquadSelection _selection;
        private UnitViewManager _views;
        /// <summary>Squads still dropping in: placed squad id -> Time.unscaledTime the drop began.</summary>
        private readonly Dictionary<int, float> _drops = new Dictionary<int, float>();
        private readonly Dictionary<UnitStats, UnitDefinition> _sources = new Dictionary<UnitStats, UnitDefinition>();
        private readonly List<PlacedSquad> _previewOrder = new List<PlacedSquad>();

        public Match Match { get; private set; }
        /// <summary>Feedback from the last action, for the HUD (empty when nothing to say).</summary>
        public string Message { get; private set; } = "";
        /// <summary>Raised after every phase change (draft chosen, battle started or finished, next round).</summary>
        public event Action PhaseChanged;

        /// <summary>The squad being moved, or -1.</summary>
        public int MovingSquadId { get; private set; } = -1;
        /// <summary>Whether the squad being moved is turned 90 degrees.</summary>
        public bool PlacingRotated { get; private set; }
        public bool IsPlacing => MovingSquadId >= 0;
        public bool IsDeploying => Match != null && Match.Phase == MatchPhase.Deployment;
        /// <summary>The player always deploys in their own zone.</summary>
        public Team PlacingTeam => Team.A;

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _selection = GetComponent<SquadSelection>();
            _views = GetComponent<UnitViewManager>();
            _battle.BattleFinished += OnBattleFinished;
            NewMatch();
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.BattleFinished -= OnBattleFinished;
        }

        /// <summary>Starts a fresh match at the draft. fixedSeed overrides the Inspector seed (tests).</summary>
        public void NewMatch(int? fixedSeed = null)
        {
            var cache = new Dictionary<UnitDefinition, UnitStats>();
            var infos = new List<FactionInfo>();
            foreach (FactionDefinition faction in factions)
            {
                if (faction != null) infos.Add(faction.ToInfo(cache));
            }
            _sources.Clear();
            foreach (var pair in cache) _sources[pair.Value] = pair.Key;

            int matchSeed = fixedSeed ?? (seed != 0 ? seed : Environment.TickCount & 0x7FFFFFFF);
            Match = new Match(matchRules.ToRules(), _battle.Rules, infos, matchSeed, new DoNothingAi());
            CancelPlacement();
            _drops.Clear();
            Message = "";
            _battle.ShowBattle(null, null, play: false);
            PhaseChanged?.Invoke();
        }

        // ------------------------------------------------------------------ draft

        public ActionResult ChooseDraft(int cardIndex)
        {
            ActionResult result = Match.ChooseDraft(cardIndex);
            Report(result);
            if (result.Ok)
            {
                RefreshPreview();
                PhaseChanged?.Invoke();
            }
            return result;
        }

        // ------------------------------------------------------------------ deployment

        public ActionResult Unlock(UnitStats type) => Report(Match.Unlock(Team.A, type));

        /// <summary>
        /// Buys a squad of this type: it is placed at the free spot nearest the middle of the zone,
        /// drops in from high above and is selected when it lands.
        /// </summary>
        public ActionResult Buy(UnitStats type)
        {
            if (Match.Phase != MatchPhase.Deployment) return ActionResult.Fail("Squads can only be bought during deployment.");
            CancelPlacement();
            ActionResult result = Report(Match.Buy(Team.A, type));
            if (result.Ok)
            {
                _drops[Match.Player.Squads[Match.Player.Squads.Count - 1].Id] = Time.unscaledTime;
                RefreshPreview();
            }
            return result;
        }

        /// <summary>True while a bought squad is still falling into place.</summary>
        public bool IsDropping => _drops.Count > 0;

        private void Update()
        {
            if (_drops.Count == 0) return;
            // Select each squad as it lands (the last one to land stays selected).
            float duration = _views.DropDuration;
            int landed = -1;
            foreach (var pair in _drops)
            {
                if (Time.unscaledTime - pair.Value >= duration) landed = pair.Key;
            }
            if (landed < 0) return;
            _drops.Remove(landed);
            if (Match.Phase == MatchPhase.Deployment && !IsPlacing) SelectPlaced(landed);
        }

        /// <summary>Starts moving one of this round's squads; the next PlaceAt moves it there.</summary>
        public void BeginMoving(PlacedSquad squad)
        {
            if (Match.Phase != MatchPhase.Deployment || squad == null) return;
            if (!Match.CanMove(squad))
            {
                Message = "Only squads bought this round can be moved or turned.";
                return;
            }
            CancelPlacement();
            MovingSquadId = squad.Id;
            PlacingRotated = squad.Rotated90;
            _selection.InputEnabled = false;
            Message = "Left-click the new spot, R to turn, right-click to cancel.";
        }

        public void CancelPlacement()
        {
            MovingSquadId = -1;
            PlacingRotated = false;
            if (_selection != null) _selection.InputEnabled = true;
        }

        /// <summary>Turns the squad being moved, or turns the selected squad where it stands.</summary>
        public void ToggleRotation()
        {
            if (IsPlacing)
            {
                PlacingRotated = !PlacingRotated;
                return;
            }
            PlacedSquad squad = SelectedPlacedSquad;
            if (squad == null) return;
            if (Report(Match.Move(Team.A, squad.Id, squad.Column, squad.Row, !squad.Rotated90)).Ok) RefreshPreview(squad.Id);
        }

        /// <summary>The type of the squad being moved (for the ghost), or null.</summary>
        public UnitStats PlacementType => Match.Player.FindSquad(MovingSquadId)?.Type;

        /// <summary>Moves the squad being moved to this spot (column and row in the player's grid).</summary>
        public ActionResult PlaceAt(int column, int row)
        {
            if (MovingSquadId < 0) return ActionResult.Fail("Nothing to place.");
            int placedId = MovingSquadId;
            ActionResult result = Report(Match.Move(Team.A, placedId, column, row, PlacingRotated));
            if (result.Ok)
            {
                CancelPlacement();
                _drops.Remove(placedId);
                RefreshPreview(placedId);
            }
            return result;
        }

        /// <summary>True if the squad being moved could go here (for the ghost's color).</summary>
        public bool CanPlaceAt(int column, int row)
        {
            UnitStats type = PlacementType;
            return type != null && DeploymentGrid.CanPlace(Match.Player, type, column, row, PlacingRotated, Match.CombatRules, MovingSquadId);
        }

        /// <summary>In a match, taking a squad off the field means selling it.</summary>
        public ActionResult Remove(PlacedSquad squad) => Sell(squad);

        public ActionResult Sell(PlacedSquad squad)
        {
            if (squad == null) return ActionResult.Fail("Select a squad first.");
            ActionResult result = Report(Match.Sell(Team.A, squad.Id));
            if (result.Ok)
            {
                _drops.Remove(squad.Id);
                RefreshPreview();
            }
            return result;
        }

        /// <summary>The player's squad currently selected in the deployment preview, or null.</summary>
        public PlacedSquad SelectedPlacedSquad
        {
            get
            {
                if (Match.Phase != MatchPhase.Deployment || _selection == null || _battle.Simulation == null) return null;
                IReadOnlyList<SimSquad> squads = _battle.Simulation.Squads;
                for (int i = 0; i < squads.Count && i < _previewOrder.Count; i++)
                {
                    if (squads[i].Id == _selection.SelectedSquadId) return _previewOrder[i];
                }
                return null;
            }
        }

        // ------------------------------------------------------------------ battle and result

        /// <summary>The player is ready: the round's battle starts (the AI's army is revealed).</summary>
        public void Ready()
        {
            if (Match.Phase != MatchPhase.Deployment) return;
            CancelPlacement();
            _drops.Clear();
            Message = "";
            _battle.ShowBattle(Match.StartBattle(), _sources, play: true);
            PhaseChanged?.Invoke();
        }

        private void OnBattleFinished(BattleResult result)
        {
            if (Match == null || Match.Phase != MatchPhase.Battle || _battle.Simulation != Match.CurrentBattle) return;
            Match.FinishBattle();
            PhaseChanged?.Invoke();
        }

        /// <summary>Leaves the result screen for the next round's deployment.</summary>
        public void Continue()
        {
            if (Match.Phase != MatchPhase.Result) return;
            Match.ContinueAfterResult();
            RefreshPreview();
            PhaseChanged?.Invoke();
        }

        /// <summary>Shows the player's current deployment (not the AI's) as a battle that isn't running.</summary>
        private void RefreshPreview(int selectPlacedSquadId = -1)
        {
            if (Match.Phase != MatchPhase.Deployment) return;
            BattleSimulation preview = Match.BuildBattle(includeEnemy: false);
            _previewOrder.Clear();
            _previewOrder.AddRange(Match.Player.Squads);
            _battle.ShowBattle(preview, _sources, play: false);

            // Squads still dropping in keep falling from where they were.
            foreach (var pair in _drops)
            {
                int dropIndex = _previewOrder.FindIndex(s => s.Id == pair.Key);
                if (dropIndex >= 0 && dropIndex < preview.Squads.Count) _views.DropSquad(preview.Squads[dropIndex].Id, pair.Value);
            }
            SelectPlaced(selectPlacedSquadId);
        }

        /// <summary>Selects the preview squad showing this placed squad (nothing if it is not shown).</summary>
        private void SelectPlaced(int placedSquadId)
        {
            int index = _previewOrder.FindIndex(s => s.Id == placedSquadId);
            if (_battle.Simulation != null && index >= 0 && index < _battle.Simulation.Squads.Count)
                _selection.Select(_battle.Simulation.Squads[index].Id);
        }

        private ActionResult Report(ActionResult result)
        {
            Message = result.Message ?? "";
            return result;
        }
    }
}
