// SandboxController.cs
// Sandbox mode (Milestone 4, used for balancing): pick two armies from every faction's and sub-faction's roster
// (plus the placeholder test units, which never appear in matches),
// place them freely in both deployment zones (no credits, no limits, every squad movable), run the
// battle, then reset to the same placement and try again. Both armies are shown while placing.
// Placement uses the same deployment grid as the match (snap to squares, turn 90 degrees, no
// overlap), so a sandbox battle plays exactly like a match battle with the same deployments.
// Every item is free to use (decision D34): pick one from Items, then a squad on either side; one
// item per squad, and the stats window can take it off again. Spells are not in the sandbox.
// A switch (off at first, so that a fight measures its units alone) puts each side's two towers on the field, as in a match, at level 1.
// Squads standing where a tower goes step aside, and switching off puts them back, so that the same layout and seed fight the same fight again.
// The HUD (Scripts/UI/SandboxHud) and DeploymentInput call the public methods here; tests do too.
// Depends on: BattleController, SquadSelection, IDeploymentPlacement, RightClick, Halobellum.Economy
//             (PlayerState, DeploymentGrid, TowerBuilder), Halobellum.Units (FactionDefinition, UnitDefinition, MatchRulesDefinition), BattleFactions, UnityEngine.

using System;
using System.Collections.Generic;
using Halobellum.Combat;
using Halobellum.Core;
using Halobellum.Economy;
using Halobellum.Units;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(BattleController))]
    public sealed class SandboxController : MonoBehaviour, IDeploymentPlacement, ISquadProgression
    {
        /// <summary>Where the towers moved a squad from and to, so that switching them off can put it back.</summary>
        private struct MovedOffTower
        {
            public int FromColumn, FromRow, ToColumn, ToRow;
            public bool Rotated90;
        }

        /// <summary>One unit type the sandbox offers, with the faction it comes from.</summary>
        public readonly struct SandboxUnit
        {
            /// <summary>The group it is listed under: a faction, "Faction: Sub-faction", or the placeholders' label.</summary>
            public readonly string Faction;
            /// <summary>The faction its sound and looks come from (BattleFactions), or null for a placeholder unit.</summary>
            public readonly string BaseFaction;
            public readonly UnitStats Stats;

            public SandboxUnit(string faction, string baseFaction, UnitStats stats)
            {
                Faction = faction;
                BaseFaction = baseFaction;
                Stats = stats;
            }
        }

        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();
        [Tooltip("Extra units offered only in the sandbox (the placeholder test units), listed as 'Placeholder'.")]
        [SerializeField] private List<UnitDefinition> extraUnits = new List<UnitDefinition>();
        [Tooltip("Where the towers stand and how strong they are, as in a match.")]
        [SerializeField] private MatchRulesDefinition matchRules;
        [Tooltip("Seed of the next battle (shield block rolls).")]
        [SerializeField] private int seed = 1000;

        private BattleController _battle;
        private SquadSelection _selection;
        private UnitViewManager _views;
        private readonly Dictionary<ItemStats, CardStats> _itemCards = new Dictionary<ItemStats, CardStats>();
        private readonly Dictionary<CardStats, CardDefinition> _cardSources = new Dictionary<CardStats, CardDefinition>();
        private readonly Dictionary<UnitStats, UnitDefinition> _sources = new Dictionary<UnitStats, UnitDefinition>();
        private readonly List<PlacedSquad> _previewOrder = new List<PlacedSquad>();
        private readonly Dictionary<string, string> _factionOfUnit = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _counting = new Dictionary<string, int>();
        private readonly Dictionary<TowerKind, UnitStats> _towerStats = new Dictionary<TowerKind, UnitStats>();
        private readonly Dictionary<PlacedSquad, MovedOffTower> _movedOffTowers = new Dictionary<PlacedSquad, MovedOffTower>();
        private MatchRules _towerRules;
        private int _nextSquadId = 1;

        /// <summary>A squad on the field was set down in a new spot. For sound.</summary>
        public event Action SquadMoved;
        /// <summary>A squad on the field, or the ghost of one being placed, was turned. For sound.</summary>
        public event Action SquadTurned;
        /// <summary>Something asked for couldn't be done. For sound.</summary>
        public event Action ActionRefused;

        public PlayerState ArmyA { get; } = new PlayerState(Team.A);
        public PlayerState ArmyB { get; } = new PlayerState(Team.B);
        public List<SandboxUnit> Units { get; } = new List<SandboxUnit>();
        /// <summary>The units that only land from a spell, such as the ODST: not on the list to place, but the gallery shows them.</summary>
        public List<SandboxUnit> DropUnits { get; } = new List<SandboxUnit>();
        /// <summary>Every item, once each: pick one, then a squad.</summary>
        public List<ItemStats> Items { get; } = new List<ItemStats>();
        /// <summary>The item picked from the list, waiting for a squad (hexagons show), or null.</summary>
        public ItemStats PlacingItem { get; private set; }
        public int Seed { get => seed; set => seed = value; }
        /// <summary>Which side new squads go to.</summary>
        public Team EditingTeam { get; set; } = Team.A;
        /// <summary>False while placing; true once Run was pressed (until Reset).</summary>
        public bool IsBattle { get; private set; }
        /// <summary>True if both sides have their towers: see SetTowers.</summary>
        public bool TowersOn { get; private set; }
        public string Message { get; private set; } = "";

        /// <summary>The unit type being placed, or null.</summary>
        public UnitStats PlacingType { get; private set; }
        public int MovingSquadId { get; private set; } = -1;
        private Team _movingTeam;

        // IDeploymentPlacement
        public bool IsDeploying => !IsBattle;
        public bool IsPlacing => PlacingType != null || MovingSquadId >= 0;
        public bool ShowsFlankZones => PlacementType != null && PlacementType.CanFlank;
        public bool PulsesFlankZones => false;
        public bool PlacingRotated { get; private set; }
        public Team PlacingTeam => MovingSquadId >= 0 ? _movingTeam : EditingTeam;
        public UnitStats PlacementType => PlacingType ?? Army(_movingTeam).FindSquad(MovingSquadId)?.Type;

        public PlayerState Army(Team team) => team == Team.A ? ArmyA : ArmyB;

        /// <summary>The group name of the sandbox-only units in the unit list.</summary>
        public const string ExtraUnitsLabel = "Placeholder";

        private void Awake()
        {
            _battle = GetComponent<BattleController>();
            _selection = GetComponent<SquadSelection>();
            _selection.TowersSelectable = false;
            _views = GetComponent<UnitViewManager>();
            _towerRules = matchRules.ToRules();

            var cache = new Dictionary<UnitDefinition, UnitStats>();
            foreach (FactionDefinition faction in factions)
            {
                if (faction == null) continue;
                FactionInfo info = faction.ToInfo(cache, _cardSources);
                foreach (UnitStats unit in info.BaseRoster) AddUnit(info.Name, info.Name, unit);
                foreach (SubFactionInfo sub in info.SubFactions)
                {
                    foreach (UnitStats unit in sub.ExclusiveUnits) AddUnit($"{info.Name}: {sub.Name}", info.Name, unit);
                }
                foreach (CardStats card in info.Cards)
                {
                    UnitStats drop = card.Spell?.DropUnit;
                    if (drop != null && !DropUnits.Exists(u => u.Stats == drop)) DropUnits.Add(new SandboxUnit(info.Name, info.Name, drop));
                    // Factions share their items: list each one once.
                    if (card.Item == null || Items.Exists(i => i.Name == card.Item.Name)) continue;
                    Items.Add(card.Item);
                    _itemCards[card.Item] = card;
                }
            }
            foreach (UnitDefinition extra in extraUnits)
            {
                if (extra == null) continue;
                UnitStats stats = extra.ToStats();
                cache[extra] = stats;
                AddUnit(ExtraUnitsLabel, null, stats);
            }
            foreach (var pair in cache) _sources[pair.Value] = pair.Key;
            ShowFactions(); // Before anything reads them: a match played earlier must not leak in here.
        }

        private void AddUnit(string label, string baseFaction, UnitStats stats)
        {
            Units.Add(new SandboxUnit(label, baseFaction, stats));
            if (baseFaction != null) _factionOfUnit[stats.Name] = baseFaction;
        }

        private void Start() => Refresh();

        private void Update()
        {
            if (PlacingItem == null) return;
            Keyboard keyboard = Keyboard.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || RightClick.WasClicked) CancelPlacement();
        }

        // ------------------------------------------------------------------ placing

        /// <summary>Starts placing a squad of this type in the editing side's zone; the next PlaceAt puts it there.</summary>
        public void BeginPlacing(UnitStats type)
        {
            if (IsBattle || type == null) return;
            CancelPlacement();
            PlacingType = type;
            _selection.InputEnabled = false;
            Message = $"Left-click in the {SideName(EditingTeam)} zone to place, R to turn, right-click to cancel.";
        }

        public void BeginMoving(PlacedSquad squad)
        {
            if (IsBattle || squad == null) return;
            CancelPlacement();
            MovingSquadId = squad.Id;
            _movingTeam = ArmyA.Squads.Contains(squad) ? Team.A : Team.B;
            PlacingRotated = squad.Rotated90;
            _selection.InputEnabled = false;
            Message = "Left-click the new spot, R to turn, right-click to cancel.";
        }

        public void CancelPlacement()
        {
            PlacingType = null;
            PlacingItem = null;
            MovingSquadId = -1;
            PlacingRotated = false;
            if (_selection != null) _selection.InputEnabled = true;
        }

        public void ToggleRotation()
        {
            if (IsPlacing)
            {
                PlacingRotated = !PlacingRotated;
                SquadTurned?.Invoke();
                return;
            }
            PlacedSquad squad = SelectedPlacedSquad;
            if (squad == null) return;
            PlayerState army = OwnerOf(squad);
            if (DeploymentGrid.CanPlace(army, squad.Type, squad.Column, squad.Row, !squad.Rotated90, _battle.Rules, squad.Id))
            {
                squad.Rotated90 = !squad.Rotated90;
                Refresh(squad.Id);
                SquadTurned?.Invoke();
            }
            else
            {
                Message = "No room to turn it there.";
                ActionRefused?.Invoke();
            }
        }

        public bool CanPlaceAt(int column, int row)
        {
            UnitStats type = PlacementType;
            return type != null && DeploymentGrid.CanPlace(Army(PlacingTeam), type, column, row, PlacingRotated, _battle.Rules, MovingSquadId);
        }

        /// <summary>Places the new squad, or moves the squad being moved, at this spot of its side's grid.</summary>
        public ActionResult PlaceAt(int column, int row)
        {
            if (!IsPlacing) return ActionResult.Fail("Nothing to place.");
            if (!CanPlaceAt(column, row)) return Report(ActionResult.Fail("That spot is blocked or outside the zone."));

            PlayerState army = Army(PlacingTeam);
            int placedId;
            if (PlacingType != null)
            {
                placedId = _nextSquadId++;
                army.Squads.Add(new PlacedSquad { Id = placedId, Type = PlacingType, Column = column, Row = row, Rotated90 = PlacingRotated, PricePaid = PlacingType.Cost });
            }
            else
            {
                PlacedSquad squad = army.FindSquad(MovingSquadId);
                squad.Column = column;
                squad.Row = row;
                squad.Rotated90 = PlacingRotated;
                placedId = squad.Id;
                SquadMoved?.Invoke();
            }
            // Keep placing the same type (handy for building an army); moving ends after one click.
            bool keepPlacing = PlacingType != null;
            UnitStats type = PlacingType;
            CancelPlacement();
            Refresh(placedId);
            if (keepPlacing) BeginPlacing(type);
            return Report(ActionResult.Success());
        }

        /// <summary>Places a squad at the free spot nearest the middle of a side's zone (tests, quick setups).</summary>
        public ActionResult AddSquad(Team team, UnitStats type)
        {
            if (IsBattle) return ActionResult.Fail("Reset the battle first.");
            PlayerState army = Army(team);
            if (!DeploymentGrid.FindFreeSpot(army, type, false, _battle.Rules, out int column, out int row)) return Report(ActionResult.Fail("That zone is full."));
            army.Squads.Add(new PlacedSquad { Id = _nextSquadId++, Type = type, Column = column, Row = row, PricePaid = type.Cost });
            Refresh();
            return ActionResult.Success();
        }

        public ActionResult Remove(PlacedSquad squad)
        {
            if (IsBattle || squad == null) return ActionResult.Fail("Nothing to remove.");
            OwnerOf(squad)?.Squads.Remove(squad);
            Refresh();
            return ActionResult.Success();
        }

        /// <summary>Removes every squad of one side.</summary>
        public void Clear(Team team)
        {
            if (IsBattle) return;
            CancelPlacement();
            Army(team).Squads.Clear();
            Army(team).Techs.Clear();
            Refresh();
        }

        /// <summary>Copies one side's army to the other side (same spots in its own zone), replacing it.</summary>
        public void CopyToOtherSide(Team from)
        {
            if (IsBattle) return;
            CancelPlacement();
            PlayerState target = Army(from.Opponent());
            target.Squads.Clear();
            target.Techs.Clear();
            foreach (PlacedSquad squad in Army(from).Squads)
            {
                var copy = new PlacedSquad { Id = _nextSquadId++, Type = squad.Type, Column = squad.Column, Row = squad.Row, Rotated90 = squad.Rotated90, PricePaid = squad.PricePaid, Level = squad.Level, Item = squad.Item };
                target.Squads.Add(copy);
                if (_movedOffTowers.TryGetValue(squad, out MovedOffTower moved)) _movedOffTowers[copy] = moved; // It stands aside like its original, and goes back with it.
            }
            foreach (var pair in Army(from).Techs) target.Techs[pair.Key] = new List<TechUpgrade>(pair.Value);
            Refresh();
        }

        /// <summary>
        /// What one side's army is worth (to compare armies of equal value): each squad's price plus
        /// half its price per level above 1 and the price of its item, plus the techs it owns.
        /// </summary>
        public int ArmyCost(Team team)
        {
            int total = 0;
            PlayerState army = Army(team);
            foreach (PlacedSquad squad in army.Squads)
            {
                total += squad.Type.Cost + squad.Type.Cost * (squad.Level - 1) / 2;
                if (squad.Item != null && _itemCards.TryGetValue(squad.Item, out CardStats card)) total += card.Cost;
            }
            foreach (var pair in army.Techs) foreach (TechUpgrade tech in pair.Value) total += tech.Cost;
            return total;
        }

        // ------------------------------------------------------------------ towers

        /// <summary>Puts both sides' towers on the field or takes them away. Squads standing where a tower goes move to the nearest free spot, and go back when it goes again.</summary>
        public void SetTowers(bool on)
        {
            if (IsBattle || on == TowersOn) return;
            CancelPlacement();
            TowersOn = on;
            Message = "";
            ArmyA.Towers.Clear();
            ArmyB.Towers.Clear();
            if (on)
            {
                TowerBuilder.AddTo(ArmyA, _towerRules, _battle.Rules);
                TowerBuilder.AddTo(ArmyB, _towerRules, _battle.Rules);
                MoveSquadsOffTowers(ArmyA);
                MoveSquadsOffTowers(ArmyB);
                if (_movedOffTowers.Count > 0 && Message == "") Message = "Squads on the towers' squares moved aside: Towers off puts them back.";
            }
            else
            {
                PutBackSquadsMovedOffTowers(ArmyA);
                PutBackSquadsMovedOffTowers(ArmyB);
                _movedOffTowers.Clear();
            }
            Refresh();
        }

        private void MoveSquadsOffTowers(PlayerState army)
        {
            for (int i = 0; i < army.Squads.Count; i++)
            {
                PlacedSquad squad = army.Squads[i];
                if (DeploymentGrid.CanPlace(army, squad.Type, squad.Column, squad.Row, squad.Rotated90, _battle.Rules, squad.Id)) continue;
                // Out of the list while looking, or it would block the spots around itself. It goes back to the same place in the list, which is its number in the battle.
                army.Squads.RemoveAt(i);
                if (DeploymentGrid.FindFreeSpotNear(army, squad.Type, squad.Rotated90, _battle.Rules, squad.Column, squad.Row, out int column, out int row))
                {
                    _movedOffTowers[squad] = new MovedOffTower { FromColumn = squad.Column, FromRow = squad.Row, ToColumn = column, ToRow = row, Rotated90 = squad.Rotated90 };
                    squad.Column = column;
                    squad.Row = row;
                }
                else Report(ActionResult.Fail($"No room to move {squad.Type.Name} off the tower: it stays where it is."));
                army.Squads.Insert(i, squad);
            }
        }

        private void PutBackSquadsMovedOffTowers(PlayerState army)
        {
            // The last moved goes back first: that undoes the moves exactly, even if one squad took a square another had left.
            for (int i = army.Squads.Count - 1; i >= 0; i--)
            {
                PlacedSquad squad = army.Squads[i];
                if (!_movedOffTowers.TryGetValue(squad, out MovedOffTower moved)) continue;
                // Moved or turned by hand since then: it stands where the player wants it.
                if (squad.Column != moved.ToColumn || squad.Row != moved.ToRow || squad.Rotated90 != moved.Rotated90) continue;
                if (DeploymentGrid.CanPlace(army, squad.Type, moved.FromColumn, moved.FromRow, squad.Rotated90, _battle.Rules, squad.Id))
                {
                    squad.Column = moved.FromColumn;
                    squad.Row = moved.FromRow;
                }
                else Report(ActionResult.Fail($"No room to put {squad.Type.Name} back where it was: it stays where it is."));
            }
        }

        /// <summary>A battle (not started) from both armies as placed. The towers come after the squads, so that squad and unit numbering stay as they are without them.</summary>
        private BattleSimulation BuildBattle()
        {
            BattleSimulation battle = DeploymentGrid.CreateBattle(_battle.Rules, ArmyA, ArmyB, seed);
            foreach (PlayerState army in new[] { ArmyA, ArmyB })
            {
                foreach (PlacedTower tower in army.Towers)
                {
                    battle.AddUnit(TowerStatsFor(tower), army.Team, DeploymentGrid.TowerCentre(tower, army.Team, _battle.Rules));
                }
            }
            return battle;
        }

        private UnitStats TowerStatsFor(PlacedTower tower)
        {
            if (_towerStats.TryGetValue(tower.Kind, out UnitStats stats)) return stats;
            stats = TowerBuilder.MakeStats(_towerRules, tower.Kind, tower.Level);
            _towerStats[tower.Kind] = stats;
            return stats;
        }

        // ------------------------------------------------------------------ factions

        /// <summary>The faction most of this side's squads belong to; with none (or only placeholders) UNSC for blue and Covenant for red.</summary>
        public string FactionOf(Team team)
        {
            _counting.Clear();
            string best = null;
            int bestCount = 0;
            foreach (PlacedSquad squad in Army(team).Squads)
            {
                if (!_factionOfUnit.TryGetValue(squad.Type.Name, out string faction)) continue;
                _counting.TryGetValue(faction, out int count);
                count++;
                _counting[faction] = count;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = faction;
                }
            }
            return best ?? (team == Team.A ? BattleFactions.Unsc : BattleFactions.Covenant);
        }

        /// <summary>Tells the map, sound and effects whose armies these are. Only a match's draft sets this anywhere else.</summary>
        private void ShowFactions() => BattleFactions.Set(FactionOf(Team.A), FactionOf(Team.B));

        // ------------------------------------------------------------------ battle

        /// <summary>Runs the battle with both armies as placed.</summary>
        public void Run()
        {
            if (IsBattle) return;
            if (ArmyA.Squads.Count == 0 || ArmyB.Squads.Count == 0)
            {
                Message = "Place at least one squad on each side.";
                return;
            }
            CancelPlacement();
            IsBattle = true;
            Message = "";
            _battle.ShowBattle(BuildBattle(), _sources, play: true);
        }

        /// <summary>Back to placing, with both armies where they were placed.</summary>
        public void ResetBattle()
        {
            IsBattle = false;
            Message = "";
            Refresh();
        }

        // ------------------------------------------------------------------ levels and techs (free in the sandbox)

        /// <summary>Sets a squad's level (1 to 5).</summary>
        public void SetLevel(PlacedSquad squad, int level)
        {
            if (IsBattle || squad == null) return;
            squad.Level = Math.Max(1, Math.Min(UnitUpgrades.MaxLevel, level));
            Refresh(squad.Id);
        }

        /// <summary>Gives or takes away a tech for every squad of that type on one side.</summary>
        public void ToggleTech(Team team, UnitStats type, TechUpgrade tech, int reselectSquadId = -1)
        {
            if (IsBattle) return;
            PlayerState army = Army(team);
            if (army.HasTech(type, tech)) army.RemoveTech(type, tech);
            else army.AddTech(type, tech);
            Refresh(reselectSquadId);
        }

        // ------------------------------------------------------------------ items (free in the sandbox)

        /// <summary>Picks an item; the next PlaceItemOn puts it on a squad of either side.</summary>
        public void BeginPlacingItem(ItemStats item)
        {
            if (IsBattle || item == null || !Items.Contains(item)) return;
            CancelPlacement();
            PlacingItem = item;
            _selection.InputEnabled = false;
            Message = $"{item.Name}: click the hexagon over a squad, right-click to cancel.";
        }

        public ActionResult PlaceItemOn(PlacedSquad squad)
        {
            if (IsBattle || PlacingItem == null || squad == null) return ActionResult.Fail("Pick an item first.");
            if (squad.Item != null) return Report(ActionResult.Fail($"{squad.Type.Name} already carries {squad.Item.Name}."));
            squad.Item = PlacingItem;
            Message = $"{squad.Item.Name} placed on {squad.Type.Name}.";
            CancelPlacement();
            Refresh(squad.Id);
            return ActionResult.Success(Message);
        }

        /// <summary>Takes the item off a squad.</summary>
        public void RemoveItem(PlacedSquad squad)
        {
            if (IsBattle || squad == null) return;
            squad.Item = null;
            Refresh(squad.Id);
        }

        /// <summary>The icon of an item (Art/UI/Items), or null.</summary>
        public Texture2D IconFor(ItemStats item) =>
            item != null && _itemCards.TryGetValue(item, out CardStats card) && _cardSources.TryGetValue(card, out CardDefinition definition) ? definition.icon : null;

        /// <summary>Screen position (pixels, origin bottom-left) just above a squad; false if it isn't on screen.</summary>
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

        public SquadProgressView DescribeProgress(int simSquadId)
        {
            BattleSimulation sim = _battle.Simulation;
            if (sim == null) return null;
            int index = -1;
            for (int i = 0; i < sim.Squads.Count; i++) if (sim.Squads[i].Id == simSquadId) { index = i; break; }
            if (index < 0) return null;
            var all = new List<PlacedSquad>(ArmyA.Squads);
            all.AddRange(ArmyB.Squads);
            if (index >= all.Count) return null;
            PlacedSquad squad = all[index];
            Team team = index < ArmyA.Squads.Count ? Team.A : Team.B;
            PlayerState army = Army(team);

            var view = new SquadProgressView { Level = squad.Level, Note = IsBattle ? null : "Sandbox: set the level and techs freely." };
            if (squad.Item != null)
            {
                view.ItemText = $"Item: {squad.Item.Name} ({squad.Item.EffectsText()})";
                if (!IsBattle) view.OnRemoveItem = () => RemoveItem(squad);
            }
            if (!IsBattle)
            {
                if (squad.Level < UnitUpgrades.MaxLevel)
                {
                    view.LevelUpLabel = "Level +";
                    view.LevelUpEnabled = true;
                    view.OnLevelUp = () => SetLevel(squad, squad.Level + 1);
                }
                if (squad.Level > 1) view.OnLevelDown = () => SetLevel(squad, squad.Level - 1);
            }
            foreach (TechUpgrade tech in squad.Type.TechUpgrades ?? Array.Empty<TechUpgrade>())
            {
                if (tech == null) continue;
                bool owned = army.HasTech(squad.Type, tech);
                view.Techs.Add(new TechLine
                {
                    Tech = tech,
                    Owned = owned,
                    ButtonLabel = IsBattle ? null : owned ? "Remove" : "Add",
                    ButtonEnabled = !IsBattle,
                    OnButton = () => ToggleTech(team, squad.Type, tech, squad.Id),
                });
            }
            return view;
        }

        // ------------------------------------------------------------------ selection and preview

        /// <summary>The placed squad selected in the preview (either side), or null.</summary>
        public PlacedSquad SelectedPlacedSquad
        {
            get
            {
                if (IsBattle || _selection == null || _battle.Simulation == null) return null;
                IReadOnlyList<SimSquad> squads = _battle.Simulation.Squads;
                for (int i = 0; i < squads.Count && i < _previewOrder.Count; i++)
                {
                    if (squads[i].Id == _selection.SelectedSquadId) return _previewOrder[i];
                }
                return null;
            }
        }

        /// <summary>Shows both armies as a battle that isn't running, optionally selecting one placed squad.</summary>
        private void Refresh(int selectPlacedSquadId = -1)
        {
            if (IsBattle || _battle == null) return;
            ShowFactions(); // Before the battle is shown: the effects read them when it changes.
            BattleSimulation preview = BuildBattle();
            _previewOrder.Clear();
            _previewOrder.AddRange(ArmyA.Squads); // Team A's squads come first in the battle, then Team B's.
            _previewOrder.AddRange(ArmyB.Squads);
            _battle.ShowBattle(preview, _sources, play: false);

            int index = _previewOrder.FindIndex(s => s.Id == selectPlacedSquadId);
            if (index >= 0 && index < preview.Squads.Count) _selection.Select(preview.Squads[index].Id);
        }

        private PlayerState OwnerOf(PlacedSquad squad) => ArmyA.Squads.Contains(squad) ? ArmyA : ArmyB.Squads.Contains(squad) ? ArmyB : null;

        public static string SideName(Team team) => team == Team.A ? "blue" : "red";

        private ActionResult Report(ActionResult result)
        {
            Message = result.Message ?? "";
            if (!result.Ok) ActionRefused?.Invoke();
            return result;
        }
    }
}
