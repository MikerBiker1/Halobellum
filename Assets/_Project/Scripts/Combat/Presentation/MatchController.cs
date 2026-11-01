// MatchController.cs
// Runs a match in the Match scene (Bible Section 5). It owns the plain C# Match and connects it to
// the scene: during deployment it shows the player's squads as a preview battle (the AI's current
// squads stay hidden; its army as it was at the last battle start shows as grey ghosts the player
// can select, decision D25), forwards the player's actions (unlock, buy, move, turn, sell), and on Ready hands
// the round's battle to the BattleController to play. When the battle ends it applies the result
// (base damage, income) and the HUD shows the result screen. A bought squad is placed in the middle
// of the zone, drops in from high above and is selected when it lands, ready to be moved. Squads
// ready to level up show a pulsing arrow (decision D26). From round 2 each round opens with the card
// screen (Milestone 7): take a spell or reinforcement card, or skip. Owned spells are placed during
// deployment (SpellView draws the blue / red outlines and handles the mouse) and go off in battle.
// The HUD (Scripts/UI/MatchHud) and DeploymentInput call the public methods here; tests do too.
// Depends on: BattleController, SquadSelection, UnitViewManager,
//             Halobellum.Economy (Match, RuleBasedAi, DeploymentGrid),
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
    public sealed class MatchController : MonoBehaviour, IDeploymentPlacement, ISquadProgression
    {
        [SerializeField] private MatchRulesDefinition matchRules;
        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();
        [Tooltip("Measured unit-vs-unit win rates (Data/AI/MatchupTable.asset) that the Hard AI uses to pick counters.")]
        [SerializeField] private MatchupTableDefinition matchupTable;
        [Tooltip("Match seed. 0 = a new random match every time.")]
        [SerializeField] private int seed;

        private BattleController _battle;
        private SquadSelection _selection;
        private UnitViewManager _views;
        private SpellView _spellView;
        /// <summary>Squads still dropping in: placed squad id -> Time.unscaledTime the drop began.</summary>
        private readonly Dictionary<int, float> _drops = new Dictionary<int, float>();
        private readonly Dictionary<UnitStats, UnitDefinition> _sources = new Dictionary<UnitStats, UnitDefinition>();
        private readonly List<PlacedSquad> _previewOrder = new List<PlacedSquad>();
        /// <summary>The enemy's ghost squads in the preview, after the player's (decision D25).</summary>
        private readonly List<SeenSquad> _ghostOrder = new List<SeenSquad>();

        public Match Match { get; private set; }
        /// <summary>Feedback from the last action, for the HUD (empty when nothing to say).</summary>
        public string Message { get; private set; } = "";
        /// <summary>Raised after every phase change (draft chosen, battle started or finished, next round).</summary>
        public event Action PhaseChanged;

        private const string DifficultyKey = "Halobellum.AiDifficulty";
        private MatchupTable _matchups;

        /// <summary>The AI's difficulty (chosen on the draft screen, remembered between matches).</summary>
        public AiDifficulty Difficulty { get; private set; } = AiDifficulty.Normal;

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
            _views.LevelUpReady = ShowsLevelUpArrow;
            _spellView = GetComponent<SpellView>();
            if (_spellView == null) _spellView = gameObject.AddComponent<SpellView>();
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
            Difficulty = (AiDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, (int)AiDifficulty.Normal), 0, 2);
            _matchups = matchupTable != null ? matchupTable.ToTable() : null;
            Match = new Match(matchRules.ToRules(), _battle.Rules, infos, matchSeed, new RuleBasedAi(Difficulty, matchSeed, _matchups));
            CancelPlacement();
            CancelSpellPlacement();
            _drops.Clear();
            Message = "";
            _views.GhostTeam = null;
            _battle.ShowBattle(null, null, play: false);
            PhaseChanged?.Invoke();
        }

        // ------------------------------------------------------------------ draft

        /// <summary>The UI theme of the player's faction (null before the draft or if the faction has none).</summary>
        public UiThemeDefinition PlayerTheme
        {
            get
            {
                if (Match?.Player.Faction == null) return null;
                foreach (FactionDefinition faction in factions)
                {
                    if (faction != null && (faction.factionName == Match.Player.Faction.Name || faction.name == Match.Player.Faction.Name)) return faction.uiTheme;
                }
                return null;
            }
        }

        /// <summary>Sets the AI's difficulty for this match (only before a draft card is chosen).</summary>
        public void SetDifficulty(AiDifficulty difficulty)
        {
            if (Match == null || Match.Phase != MatchPhase.Draft) return;
            if (!Match.SetEnemyAi(new RuleBasedAi(difficulty, Match.Seed, _matchups))) return;
            Difficulty = difficulty;
            PlayerPrefs.SetInt(DifficultyKey, (int)difficulty);
        }

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
            CancelSpellPlacement();
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

        // ------------------------------------------------------------------ leveling and techs (M6)

        public ActionResult LevelUp(PlacedSquad squad)
        {
            if (squad == null) return ActionResult.Fail("Select a squad first.");
            ActionResult result = Report(Match.LevelUp(Team.A, squad.Id));
            if (result.Ok) RefreshPreview(squad.Id);
            return result;
        }

        public ActionResult BuyTech(UnitStats type, TechUpgrade tech, int reselectSquadId = -1)
        {
            ActionResult result = Report(Match.BuyTech(Team.A, type, tech));
            if (result.Ok) RefreshPreview(reselectSquadId);
            return result;
        }

        /// <summary>The placed squad behind a simulation squad (preview: the player's; battle: Team A then Team B), or null.</summary>
        public PlacedSquad PlacedFor(int simSquadId, out Team team)
        {
            team = Team.A;
            BattleSimulation sim = _battle.Simulation;
            if (Match == null || sim == null) return null;
            int index = -1;
            for (int i = 0; i < sim.Squads.Count; i++) if (sim.Squads[i].Id == simSquadId) { index = i; break; }
            if (index < 0) return null;
            if (Match.Phase == MatchPhase.Deployment) return index < _previewOrder.Count ? _previewOrder[index] : null;
            if (sim != Match.CurrentBattle) return null;
            if (index < Match.Player.Squads.Count) return Match.Player.Squads[index];
            team = Team.B;
            index -= Match.Player.Squads.Count;
            return index < Match.Enemy.Squads.Count ? Match.Enemy.Squads[index] : null;
        }

        /// <summary>The enemy ghost behind a preview squad (deployment only), or null.</summary>
        public SeenSquad GhostFor(int simSquadId)
        {
            BattleSimulation sim = _battle.Simulation;
            if (Match == null || sim == null || Match.Phase != MatchPhase.Deployment) return null;
            for (int i = 0; i < sim.Squads.Count; i++)
            {
                if (sim.Squads[i].Id != simSquadId) continue;
                int ghost = i - _previewOrder.Count;
                return ghost >= 0 && ghost < _ghostOrder.Count ? _ghostOrder[ghost] : null;
            }
            return null;
        }

        /// <summary>Decision D26: the player's squads that are ready to level up show an arrow (deployment and result).</summary>
        private bool ShowsLevelUpArrow(int simSquadId)
        {
            if (Match == null || (Match.Phase != MatchPhase.Deployment && Match.Phase != MatchPhase.Result)) return false;
            PlacedSquad squad = PlacedFor(simSquadId, out Team team);
            return team == Team.A && Match.CanLevelUp(squad);
        }

        public SquadProgressView DescribeProgress(int simSquadId)
        {
            SeenSquad ghost = GhostFor(simSquadId);
            if (ghost != null)
            {
                // What the player saw of this enemy squad when the last battle started (D25).
                var seen = new SquadProgressView { SideLabel = "enemy, last battle", Level = ghost.Level, Note = "As seen at the start of the last battle." };
                foreach (TechUpgrade tech in ghost.Techs) seen.Techs.Add(new TechLine { Tech = tech, Owned = true });
                return seen;
            }

            PlacedSquad squad = PlacedFor(simSquadId, out Team team);
            if (squad == null) return null;
            var view = new SquadProgressView { Level = squad.Level };
            if (team == Team.B)
            {
                // The enemy in battle: its level and the techs it fights with, nothing else.
                foreach (TechUpgrade tech in Match.Enemy.TechsFor(squad.Type)) view.Techs.Add(new TechLine { Tech = tech, Owned = true });
                return view;
            }

            bool deploying = Match.Phase == MatchPhase.Deployment;
            PlayerState player = Match.Player;
            view.ShowXp = squad.Level < UnitUpgrades.MaxLevel;
            view.Xp = squad.Xp;
            view.XpNeeded = Match.XpNeeded(squad);
            if (squad.Level < UnitUpgrades.MaxLevel)
            {
                view.LevelUpLabel = $"Level up ({Match.LevelUpCost(squad)})";
                view.LevelUpEnabled = deploying && Match.CanLevelUp(squad) && player.Credits >= Match.LevelUpCost(squad);
                view.OnLevelUp = () => LevelUp(squad);
                view.Note = Match.CanLevelUp(squad) ? "XP bar full: ready to level up." : "Earns XP from kills.";
            }
            else view.Note = "Maximum level.";

            foreach (TechUpgrade tech in squad.Type.TechUpgrades ?? Array.Empty<TechUpgrade>())
            {
                if (tech == null) continue;
                bool owned = player.HasTech(squad.Type, tech);
                view.Techs.Add(new TechLine
                {
                    Tech = tech,
                    Owned = owned,
                    ButtonLabel = owned ? null : $"Buy {tech.Cost}",
                    ButtonEnabled = deploying && player.Credits >= tech.Cost && player.Unlocked.Contains(squad.Type),
                    OnButton = () => BuyTech(squad.Type, tech, squad.Id),
                });
            }
            return view;
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

        // ------------------------------------------------------------------ cards and spells (Milestone 7)

        /// <summary>True while the card screen is up (start of a round from round 2).</summary>
        public bool IsChoosingCards => Match != null && Match.Phase == MatchPhase.Cards;

        public ActionResult ChooseCard(int index)
        {
            ActionResult result = Report(Match.ChooseCard(Team.A, index));
            if (result.Ok)
            {
                RefreshPreview();
                PhaseChanged?.Invoke();
            }
            return result;
        }

        public ActionResult SkipCards()
        {
            ActionResult result = Report(Match.SkipCards(Team.A));
            if (result.Ok)
            {
                RefreshPreview();
                PhaseChanged?.Invoke();
            }
            return result;
        }

        /// <summary>The spell being placed (its outline follows the mouse), or -1.</summary>
        public int PlacingSpellId { get; private set; } = -1;
        public OwnedSpell PlacingSpell => Match?.Player.FindSpell(PlacingSpellId);

        /// <summary>Starts placing (or moving) one of the player's ready spells; the next PlaceSpellAt puts it there.</summary>
        public void BeginPlacingSpell(OwnedSpell spell)
        {
            if (Match.Phase != MatchPhase.Deployment || spell == null) return;
            if (!spell.IsReady(Match.Round))
            {
                Message = $"{spell.Stats.Name} is recharging ({spell.RoundsLeft(Match.Round)} more round(s)).";
                return;
            }
            CancelPlacement();
            PlacingSpellId = spell.Id;
            _selection.InputEnabled = false;
            Message = spell.Stats.Offensive
                ? $"{spell.Stats.Name}: left-click anywhere on the field (red), right-click to cancel."
                : $"{spell.Stats.Name}: left-click on your side (blue), right-click to cancel.";
        }

        public void CancelSpellPlacement()
        {
            if (PlacingSpellId < 0) return;
            PlacingSpellId = -1;
            if (_selection != null && !IsPlacing) _selection.InputEnabled = true;
        }

        /// <summary>True if the spell being placed could go at this battlefield point (metres).</summary>
        public bool CanPlaceSpellAt(Vec2 position) => PlacingSpell != null && Match.CanPlaceSpell(Team.A, PlacingSpell, position);

        public ActionResult PlaceSpellAt(Vec2 position)
        {
            if (PlacingSpellId < 0) return ActionResult.Fail("No spell to place.");
            ActionResult result = Report(Match.PlaceSpell(Team.A, PlacingSpellId, position));
            if (result.Ok) CancelSpellPlacement();
            return result;
        }

        /// <summary>Takes a placed spell back; it stays ready.</summary>
        public ActionResult UnplaceSpell(OwnedSpell spell) => spell == null ? ActionResult.Fail("No spell.") : Report(Match.UnplaceSpell(Team.A, spell.Id));

        // ------------------------------------------------------------------ battle and result

        /// <summary>The player is ready: the round's battle starts (the AI's army is revealed).</summary>
        public void Ready()
        {
            if (Match.Phase != MatchPhase.Deployment) return;
            CancelPlacement();
            CancelSpellPlacement();
            _drops.Clear();
            Message = "";
            _views.GhostTeam = null;
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

        /// <summary>
        /// Shows the player's current deployment as a battle that isn't running, with the enemy's army
        /// as it was at the last battle start as grey ghosts (D25; never the AI's current deployment).
        /// </summary>
        private void RefreshPreview(int selectPlacedSquadId = -1)
        {
            if (Match.Phase != MatchPhase.Deployment && Match.Phase != MatchPhase.Cards) return;
            BattleSimulation preview = Match.BuildPreview();
            _previewOrder.Clear();
            _previewOrder.AddRange(Match.Player.Squads);
            _ghostOrder.Clear();
            _ghostOrder.AddRange(Match.SeenArmy(Team.B));
            _views.GhostTeam = Team.B;
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
