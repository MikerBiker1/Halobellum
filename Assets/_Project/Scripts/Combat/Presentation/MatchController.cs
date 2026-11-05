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
// Items wait in the Inventory until the player selects one and picks a squad; Intensive Training fills
// a selected squad's XP bar (decision D33). The HUD draws both and calls the functions here.
// A click on a tower selects it (SquadSelection), and DescribeTower says what its window shows and sells (D55).
// A match can start with a MatchSetup (mode, base health, card filter), which goes on that match's own copy of the rules and
// is remembered, so that "New match" plays the same again. In Unlock Skirmish both sides draft from one made-up faction
// (SkirmishFaction), the seed picks one land for the whole field, and each unit keeps the looks of its real faction.
// The HUD (Scripts/UI/MatchHud) and DeploymentInput call the public methods here; tests do too.
// Depends on: BattleController, SquadSelection, UnitViewManager,
//             Halobellum.Economy (Match, RuleBasedAi, DeploymentGrid),
//             Halobellum.Units (MatchRulesDefinition, FactionDefinition, UnitDefinition), UnityEngine.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private readonly Dictionary<CardStats, CardDefinition> _cardSources = new Dictionary<CardStats, CardDefinition>();
        /// <summary>Squads still dropping in: placed squad id -> Time.unscaledTime the drop began.</summary>
        private readonly Dictionary<int, float> _drops = new Dictionary<int, float>();
        private readonly Dictionary<UnitStats, UnitDefinition> _sources = new Dictionary<UnitStats, UnitDefinition>();
        private readonly List<PlacedSquad> _previewOrder = new List<PlacedSquad>();
        /// <summary>The enemy's ghost squads in the preview, after the player's (decision D25).</summary>
        private readonly List<SeenSquad> _ghostOrder = new List<SeenSquad>();
        /// <summary>What the match was started with, or null for Faction mode at the asset's values.</summary>
        private MatchSetup _setup;
        /// <summary>The real factions of this match, whatever the mode offers.</summary>
        private List<FactionInfo> _realFactions = new List<FactionInfo>();
        private static readonly string[] SkirmishLands = { BattleFactions.Unsc, BattleFactions.Covenant, BattleFactions.Flood };

        public Match Match { get; private set; }
        /// <summary>Feedback from the last action, for the HUD (empty when nothing to say).</summary>
        public string Message { get; private set; } = "";
        /// <summary>Raised after every phase change (draft chosen, battle started or finished, next round).</summary>
        public event Action PhaseChanged;
        /// <summary>Raised after each action the player takes, with the name of its method and how it went (for sound).</summary>
        public event Action<string, ActionResult> ActionTaken;
        /// <summary>The end of a round, between the last shot landing and its window; null the rest of the time.</summary>
        public RoundEnding Ending { get; private set; }
        /// <summary>Raised as a node of light hits a base (for sound).</summary>
        public event Action<RoundEnding.Node> EndingHit;
        private const float FastestPlayerSpeed = 4f;

        public const string DifficultyKey = "Halobellum.AiDifficulty";
        private MatchupTable _matchups;

        /// <summary>The AI's difficulty (chosen on the draft screen, remembered between matches).</summary>
        public AiDifficulty Difficulty { get; private set; } = AiDifficulty.Normal;

        /// <summary>The squad being moved, or -1.</summary>
        public int MovingSquadId { get; private set; } = -1;
        /// <summary>Whether the squad being moved is turned 90 degrees.</summary>
        public bool PlacingRotated { get; private set; }
        public bool IsPlacing => MovingSquadId >= 0;
        /// <summary>A side that has units that can flank sees where they could go, throughout deployment.</summary>
        public bool ShowsFlankZones => IsDeploying && Match.Player.Faction != null && HasUnitsThatCanFlank();
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
            NewMatch(setup: GameFlow.TakeMatchSetup());
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.BattleFinished -= OnBattleFinished;
        }

        /// <summary>In Unlock Skirmish every unit is in the roster, so only those unlocked say anything about this side.</summary>
        private bool HasUnitsThatCanFlank()
        {
            if (Match.Rules.Mode != MatchMode.UnlockSkirmish) return Match.Player.Roster.Exists(unit => unit.CanFlank);
            foreach (UnitStats unit in Match.Player.Unlocked)
            {
                if (unit.CanFlank) return true;
            }
            return false;
        }

        /// <summary>
        /// Starts a fresh match at the draft. fixedSeed overrides the Inspector seed (tests). A setup is remembered for the
        /// matches after this one; without one, the last one is played again.
        /// </summary>
        public void NewMatch(int? fixedSeed = null, MatchSetup setup = null)
        {
            Ending = null;
            if (setup != null) _setup = setup;
            var cache = new Dictionary<UnitDefinition, UnitStats>();
            var infos = new List<FactionInfo>();
            _cardSources.Clear();
            foreach (FactionDefinition faction in factions)
            {
                if (faction != null) infos.Add(faction.ToInfo(cache, _cardSources));
            }
            _sources.Clear();
            foreach (var pair in cache) _sources[pair.Value] = pair.Key;

            int matchSeed = fixedSeed ?? (seed != 0 ? seed : Environment.TickCount & 0x7FFFFFFF);
            Difficulty = GameSettings.Difficulty;
            _matchups = matchupTable != null ? matchupTable.ToTable() : null;
            MatchRules rules = matchRules.ToRules();
            _setup?.ApplyTo(rules);
            _realFactions = infos;
            IReadOnlyList<FactionInfo> offered = infos;
            if (rules.Mode == MatchMode.UnlockSkirmish) offered = new[] { SkirmishFaction.Build(infos) };
            Match = new Match(rules, _battle.Rules, offered, matchSeed, new RuleBasedAi(Difficulty, matchSeed, _matchups));
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            _selection?.ClearTower();
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
            GameSettings.Difficulty = difficulty;
        }

        public ActionResult ChooseDraft(int cardIndex)
        {
            ActionResult result = Match.ChooseDraft(cardIndex);
            Report(result);
            if (result.Ok)
            {
                if (Match.Rules.Mode == MatchMode.UnlockSkirmish) BattleFactions.SetSkirmish(SkirmishLand(Match.Seed), RealFactionOfEachUnit());
                else BattleFactions.Set(Match.Player.Faction?.Name, Match.Enemy.Faction?.Name);
                RefreshPreview();
                PhaseChanged?.Invoke();
            }
            return result;
        }

        private static string SkirmishLand(int matchSeed)
        {
            int count = SkirmishLands.Length;
            return SkirmishLands[(matchSeed % count + count) % count];
        }

        /// <summary>Each unit's real faction by name: everything in a roster, and what a faction's spells drop (ODST, which is in no roster).</summary>
        private Dictionary<string, string> RealFactionOfEachUnit()
        {
            var map = new Dictionary<string, string>();
            foreach (FactionInfo faction in _realFactions)
            {
                foreach (UnitStats unit in faction.BaseRoster) map.TryAdd(unit.Name, faction.Name);
                foreach (SubFactionInfo subFaction in faction.SubFactions)
                {
                    foreach (UnitStats unit in subFaction.ExclusiveUnits) map.TryAdd(unit.Name, faction.Name);
                }
            }
            foreach (FactionInfo faction in _realFactions)
            {
                foreach (CardStats card in faction.Cards)
                {
                    if (card?.Spell?.DropUnit != null) map.TryAdd(card.Spell.DropUnit.Name, faction.Name);
                }
            }
            return map;
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
            if (Ending != null) TickEnding();
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
            CancelItemPlacement();
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
                ActionTaken?.Invoke(nameof(ToggleRotation), ActionResult.Success()); // Not through Report: the placing message stays.
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

        /// <summary>Levels up every ready squad of this type at once (Match.LevelUpAll).</summary>
        public ActionResult LevelUpAll(UnitStats type, int reselectSquadId = -1)
        {
            ActionResult result = Report(Match.LevelUpAll(Team.A, type));
            if (result.Ok) RefreshPreview(reselectSquadId);
            return result;
        }

        public ActionResult BuyTech(UnitStats type, TechUpgrade tech, int reselectSquadId = -1)
        {
            ActionResult result = Report(Match.BuyTech(Team.A, type, tech));
            if (result.Ok) RefreshPreview(reselectSquadId);
            return result;
        }

        // ------------------------------------------------------------------ towers (D55)

        public ActionResult LevelUpTower(TowerKind kind)
        {
            ActionResult result = Report(Match.LevelUpTower(Team.A, kind));
            if (result.Ok) RefreshPreview();
            return result;
        }

        public ActionResult BuyExtraSlot() => Report(Match.BuyExtraSlot(Team.A));
        public ActionResult BuyOilZone() => Report(Match.BuyOilZone(Team.A));
        public ActionResult RapidResupply() => Report(Match.RapidResupply(Team.A));
        public ActionResult BuyEliteRecruitment() => Report(Match.BuyEliteRecruitment(Team.A));

        public ActionResult BuyResearch(bool health)
        {
            ActionResult result = Report(Match.BuyResearch(Team.A, health));
            if (result.Ok) RefreshPreview();
            return result;
        }

        /// <summary>What the window of a tower says. The enemy's show only what the player has seen; only the player's own sell anything, and only during deployment.</summary>
        public TowerPanel DescribeTower(Team team, TowerKind kind)
        {
            PlayerState owner = Match.Get(team);
            PlacedTower tower = owner.FindTower(kind);
            if (tower == null) return null;
            MatchRules rules = Match.Rules;
            bool own = team == Team.A;
            int level = own ? tower.Level : Match.SeenEnemyTowerLevel(kind);
            UnitStats stats = Match.TowerStats(kind, level);
            var panel = new TowerPanel { Title = Match.TowerName(kind), Side = own ? "Your tower" : "Enemy tower, as last seen" };
            panel.Lines.Add($"Level {level} of {rules.TowerMaxLevel}    Health {stats.MaxHealth:0}");
            panel.Lines.Add($"If it falls, {(own ? "your" : "their")} whole army loses {(1f - rules.ShockwaveSpeedMultiplier) * 100f:0}% speed, {(1f - rules.ShockwaveDamageMultiplier) * 100f:0}% damage and {(1f - rules.ShockwaveFireRateMultiplier) * 100f:0}% rate of fire, and takes {(rules.ShockwaveDamageTakenMultiplier - 1f) * 100f:0}% more damage, for {stats.Shockwave.Seconds:0} s. Two falling add up their time, not their severity.");
            panel.Lines.Add($"Whoever destroys it earns {rules.TowerXpByLevel[level - 1]:0} XP.");
            if (!own) return panel;

            bool deploying = Match.Phase == MatchPhase.Deployment;
            int credits = owner.Credits;
            if (level < rules.TowerMaxLevel)
            {
                UnitStats next = Match.TowerStats(kind, level + 1);
                panel.Buttons.Add(new TowerButton
                {
                    Label = $"Level up ({rules.TowerLevelUpCost})",
                    Note = $"Health {next.MaxHealth:0}, shockwave {next.Shockwave.Seconds:0} s",
                    Enabled = deploying && credits >= rules.TowerLevelUpCost,
                    OnPress = () => LevelUpTower(kind),
                });
            }
            if (kind == TowerKind.CommandCenter)
            {
                panel.Buttons.Add(new TowerButton
                {
                    Label = $"Extra slot ({rules.ExtraSlotCost})", Note = owner.BoughtExtraSlot ? "Bought this round" : "One more squad this round",
                    Enabled = deploying && !owner.BoughtExtraSlot && credits >= rules.ExtraSlotCost, OnPress = () => BuyExtraSlot(),
                });
                if (rules.OilZone != null)
                {
                    panel.Buttons.Add(new TowerButton
                    {
                        Label = $"Oil zone ({rules.OilZone.Cost})", Note = owner.BoughtOilZone ? "Bought this round" : $"{rules.OilZone.AreaDepth:0} x {rules.OilZone.AreaWidth:0} m, one use: it goes in your spells",
                        Enabled = deploying && !owner.BoughtOilZone && credits >= rules.OilZone.Cost, OnPress = () => BuyOilZone(),
                    });
                }
                panel.Buttons.Add(new TowerButton
                {
                    Label = "Rapid Resupply (free)", Note = owner.UsedRapidResupply ? "Used this round" : $"+{rules.RapidResupplyCredits} now, {rules.RapidResupplyPenalty} less next round",
                    Enabled = deploying && !owner.UsedRapidResupply, OnPress = () => RapidResupply(),
                });
                panel.Buttons.Add(new TowerButton
                {
                    Label = $"Elite Recruitment ({rules.EliteRecruitmentCost})", Note = owner.EliteRecruitment ? "On: new squads are level 2" : "New squads this round are level 2, for half their price more",
                    Enabled = deploying && !owner.EliteRecruitment && credits >= rules.EliteRecruitmentCost, OnPress = () => BuyEliteRecruitment(),
                });
                return panel;
            }

            AddResearch(panel, "Health", rules.ResearchHealthShare, owner.ResearchHealthSteps, true, deploying, credits);
            AddResearch(panel, "Damage", rules.ResearchDamageShare, owner.ResearchDamageSteps, false, deploying, credits);
            return panel;
        }

        private void AddResearch(TowerPanel panel, string name, float share, int steps, bool health, bool deploying, int credits)
        {
            int[] costs = Match.Rules.ResearchCosts;
            string now = steps > 0 ? $"Now +{share * steps * 100f:0}%" : "Your whole army, for the rest of the match";
            if (steps >= costs.Length)
            {
                panel.Buttons.Add(new TowerButton { Label = $"{name}: complete", Note = now, Enabled = false });
                return;
            }
            int cost = costs[steps];
            panel.Buttons.Add(new TowerButton
            {
                Label = $"{name} +{share * 100f:0}% ({cost})", Note = now,
                Enabled = deploying && credits >= cost, OnPress = () => BuyResearch(health),
            });
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

        private static string ItemText(ItemStats item) => item == null ? null : $"Item: {item.Name} ({item.EffectsText()})";

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
                // What the player saw of this enemy squad when the last battle started (D25), or before the first, of its starting squads (D58).
                bool first = Match.NoBattleYet;
                var seen = new SquadProgressView
                {
                    SideLabel = first ? "enemy, starting squad" : "enemy, last battle", Level = ghost.Level,
                    Note = first ? "Where it will stand is hidden." : "As seen at the start of the last battle.", ItemText = ItemText(ghost.Item),
                };
                foreach (TechUpgrade tech in ghost.Techs) seen.Techs.Add(new TechLine { Tech = tech, Owned = true });
                return seen;
            }

            PlacedSquad squad = PlacedFor(simSquadId, out Team team);
            if (squad == null) return null;
            var view = new SquadProgressView { Level = squad.Level, ItemText = ItemText(squad.Item) };
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

            // Whether or not this squad is one of them: any squad of the type reaches the others.
            int ready = Match.SquadsReadyToLevel(Team.A, squad.Type, out int readyPrice);
            if (ready >= 2)
            {
                view.LevelAllLabel = $"Level all {ready} ({readyPrice})";
                view.LevelAllEnabled = deploying && player.Credits >= readyPrice;
                view.OnLevelAll = () => LevelUpAll(squad.Type, squad.Id);
            }

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
            CancelItemPlacement();
            PlacingSpellId = spell.Id;
            PlacingLineStart = null;
            _selection.Select(-1);
            _selection.InputEnabled = false;
            Message = spell.Stats.Offensive
                ? $"{spell.Stats.Name}: left-click anywhere on the field (red), right-click to cancel."
                : $"{spell.Stats.Name}: left-click on your side (blue), right-click to cancel.";
            if (spell.Stats.IsRectangle) Message = $"{spell.Stats.Name}: left-click where one end goes, right-click to cancel.";
        }

        /// <summary>
        /// A line-shaped spell being placed (D35): where its first end was clicked, or null. The other end
        /// then follows the mouse, and a second click confirms.
        /// </summary>
        public Vec2? PlacingLineStart { get; private set; }

        /// <summary>The first click of a line-shaped spell.</summary>
        public void SetLineStart(Vec2 position)
        {
            if (PlacingSpell == null || !PlacingSpell.Stats.IsRectangle) return;
            PlacingLineStart = position;
            Message = $"{PlacingSpell.Stats.Name}: move the mouse to aim the other end, left-click to confirm, right-click to go back.";
        }

        /// <summary>Forgets the first click; the spell is still being placed.</summary>
        public void ClearLineStart()
        {
            if (PlacingSpell == null || PlacingLineStart == null) return;
            PlacingLineStart = null;
            Message = $"{PlacingSpell.Stats.Name}: left-click where one end goes, right-click to cancel.";
        }

        /// <summary>The second click of a line-shaped spell: it runs from the first click toward this point.</summary>
        public ActionResult PlaceLineTowards(Vec2 towards)
        {
            if (PlacingSpell == null || PlacingLineStart == null) return ActionResult.Fail("Click where one end goes first.");
            Vec2 centre = PlacingSpell.Stats.LineFrom(PlacingLineStart.Value, towards, out float angle);
            return PlaceSpellAt(centre, angle);
        }

        public void CancelSpellPlacement()
        {
            if (PlacingSpellId < 0) return;
            PlacingSpellId = -1;
            PlacingLineStart = null;
            if (_selection != null && !IsPlacing) _selection.InputEnabled = true;
        }

        /// <summary>True if the spell being placed could go at this battlefield point (metres).</summary>
        public bool CanPlaceSpellAt(Vec2 position) => PlacingSpell != null && Match.CanPlaceSpell(Team.A, PlacingSpell, position);

        public ActionResult PlaceSpellAt(Vec2 position, float angleDegrees = 0f)
        {
            if (PlacingSpellId < 0) return ActionResult.Fail("No spell to place.");
            ActionResult result = Report(Match.PlaceSpell(Team.A, PlacingSpellId, position, angleDegrees));
            if (result.Ok) CancelSpellPlacement();
            return result;
        }

        /// <summary>Takes a placed spell back; it stays ready.</summary>
        public ActionResult UnplaceSpell(OwnedSpell spell) => spell == null ? ActionResult.Fail("No spell.") : Report(Match.UnplaceSpell(Team.A, spell.Id));

        // ------------------------------------------------------------------ items and training (D33)

        /// <summary>The item the player picked from the Inventory, waiting for a squad (hexagons show), or null.</summary>
        public ItemStats PlacingItem { get; private set; }

        public void BeginPlacingItem(ItemStats item)
        {
            if (Match.Phase != MatchPhase.Deployment || item == null || !Match.Player.Inventory.Contains(item)) return;
            CancelPlacement();
            CancelSpellPlacement();
            PlacingItem = item;
            _selection.InputEnabled = false;
            Message = $"{item.Name}: click the hexagon over a squad, right-click to cancel.";
        }

        public void CancelItemPlacement()
        {
            if (PlacingItem == null) return;
            PlacingItem = null;
            if (_selection != null && !IsPlacing) _selection.InputEnabled = true;
        }

        public ActionResult PlaceItemOn(PlacedSquad squad)
        {
            if (PlacingItem == null || squad == null) return ActionResult.Fail("Pick an item first.");
            ActionResult result = Report(Match.PlaceItem(Team.A, PlacingItem, squad.Id));
            if (result.Ok)
            {
                int squadId = squad.Id;
                CancelItemPlacement();
                RefreshPreview(squadId); // Its stats changed.
            }
            return result;
        }

        /// <summary>Intensive Training on this squad: its XP bar fills up, and it can then be leveled up.</summary>
        public ActionResult UseTraining(PlacedSquad squad)
        {
            if (squad == null) return Report(ActionResult.Fail("Select a squad first."));
            ActionResult result = Report(Match.UseTraining(Team.A, squad.Id));
            if (result.Ok) RefreshPreview(squad.Id);
            return result;
        }

        /// <summary>The icon of an item or card (Art/UI/Items), or null.</summary>
        public Texture2D IconFor(ItemStats item)
        {
            foreach (var pair in _cardSources)
            {
                if (pair.Key.Item == item) return pair.Value.icon;
            }
            return null;
        }

        public Texture2D IconFor(CardStats card) => card != null && _cardSources.TryGetValue(card, out CardDefinition definition) ? definition.icon : null;

        /// <summary>Screen position (pixels, origin bottom-left) just above one of the player's squads; false if it isn't on screen.</summary>
        public bool SquadOnScreen(PlacedSquad squad, out Vector3 screen)
        {
            screen = Vector3.zero;
            int index = _previewOrder.IndexOf(squad);
            BattleSimulation sim = _battle.Simulation;
            Camera camera = Camera.main;
            if (index < 0 || sim == null || index >= sim.Squads.Count || camera == null) return false;
            if (!_views.SquadTop(sim.Squads[index].Id, out Vector3 world)) return false;
            screen = camera.WorldToScreenPoint(world);
            return screen.z > 0f;
        }

        // ------------------------------------------------------------------ battle and result

        /// <summary>The player is ready: the round's battle starts (the AI's army is revealed).</summary>
        public void Ready()
        {
            if (Match.Phase != MatchPhase.Deployment) return;
            CancelPlacement();
            CancelSpellPlacement();
            CancelItemPlacement();
            _drops.Clear();
            Message = "";
            _views.GhostTeam = null;
            _selection.ClearTower();
            _battle.ShowBattle(Match.StartBattle(), _sources, play: true);
            PhaseChanged?.Invoke();
        }

        private void OnBattleFinished(BattleResult result)
        {
            if (Match == null || Match.Phase != MatchPhase.Battle || _battle.Simulation != Match.CurrentBattle) return;
            Ending = new RoundEnding(Match.CurrentBattle, _views.WorldPosition, Match.BattleSeed);
            Ending.Hit += OnEndingHit;
        }

        private void OnEndingHit(RoundEnding.Node node)
        {
            CameraShake.KickEverywhere(0.12f + 0.75f * node.Size * node.Size);
            EndingHit?.Invoke(node);
        }

        /// <summary>The nodes of light have all landed: only now does the round's result come, with its window and its sound.</summary>
        private void TickEnding()
        {
            if (_battle.Clock != null && _battle.Clock.IsPaused) return;
            // At the speeds a player can pick it takes its time, however fast the battle ran. Test runs go faster than those.
            Ending.Tick(Time.deltaTime * (_battle.Speed > FastestPlayerSpeed ? _battle.Speed : 1f));
            if (!Ending.IsDone) return;
            Ending = null;
            Match.FinishBattle();
            _selection.ClearTower();
            PhaseChanged?.Invoke();
        }

        /// <summary>A team's base health as the player sees it: while the nodes land, what is left after the hits so far.</summary>
        public int ShownBaseHealth(Team team)
        {
            PlayerState player = team == Team.A ? Match.Player : Match.Enemy;
            return Ending != null ? player.BaseHealth - Ending.HitsTaken(team) : player.BaseHealth;
        }

        /// <summary>Leaves the result screen for the next round's deployment.</summary>
        public void Continue()
        {
            if (Match.Phase != MatchPhase.Result) return;
            Match.ContinueAfterResult();
            _selection.ClearTower();
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

        private ActionResult Report(ActionResult result, [CallerMemberName] string action = "")
        {
            Message = result.Message ?? "";
            ActionTaken?.Invoke(action, result);
            return result;
        }
    }
}
