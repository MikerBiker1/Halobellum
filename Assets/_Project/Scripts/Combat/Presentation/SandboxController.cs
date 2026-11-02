// SandboxController.cs
// Sandbox mode (Milestone 4, used for balancing): pick two armies from every faction's and sub-faction's roster
// (plus the placeholder test units, which never appear in matches),
// place them freely in both deployment zones (no credits, no limits, every squad movable), run the
// battle, then reset to the same placement and try again. Both armies are shown while placing.
// Placement uses the same deployment grid as the match (snap to squares, turn 90 degrees, no
// overlap), so a sandbox battle plays exactly like a match battle with the same deployments.
// Every item is free to use (decision D34): pick one from Items, then a squad on either side; one
// item per squad, and the stats window can take it off again. Spells are not in the sandbox.
// The HUD (Scripts/UI/SandboxHud) and DeploymentInput call the public methods here; tests do too.
// Depends on: BattleController, SquadSelection, IDeploymentPlacement, Halobellum.Economy
//             (PlayerState, DeploymentGrid), Halobellum.Units (FactionDefinition, UnitDefinition), UnityEngine.

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
        /// <summary>One unit type the sandbox offers, with the faction it comes from.</summary>
        public readonly struct SandboxUnit
        {
            /// <summary>The heading it is listed under: a faction, or a faction and sub-faction such as "UNSC: Noble".</summary>
            public readonly string Faction;
            /// <summary>The faction alone, such as "UNSC": what its land, sky and colours follow.</summary>
            public readonly string BaseFaction;
            public readonly UnitStats Stats;

            public SandboxUnit(string faction, UnitStats stats, string baseFaction = null)
            {
                Faction = faction;
                BaseFaction = baseFaction ?? faction;
                Stats = stats;
            }
        }

        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();
        [Tooltip("Extra units offered only in the sandbox (the placeholder test units), listed as 'Placeholder'.")]
        [SerializeField] private List<UnitDefinition> extraUnits = new List<UnitDefinition>();
        [Tooltip("Seed of the next battle (shield block rolls).")]
        [SerializeField] private int seed = 1000;

        private BattleController _battle;
        private SquadSelection _selection;
        private UnitViewManager _views;
        private readonly Dictionary<ItemStats, CardStats> _itemCards = new Dictionary<ItemStats, CardStats>();
        private readonly Dictionary<CardStats, CardDefinition> _cardSources = new Dictionary<CardStats, CardDefinition>();
        private readonly Dictionary<UnitStats, UnitDefinition> _sources = new Dictionary<UnitStats, UnitDefinition>();
        private readonly List<PlacedSquad> _previewOrder = new List<PlacedSquad>();
        private int _nextSquadId = 1;

        public PlayerState ArmyA { get; } = new PlayerState(Team.A);
        public PlayerState ArmyB { get; } = new PlayerState(Team.B);
        public List<SandboxUnit> Units { get; } = new List<SandboxUnit>();
        /// <summary>Every item, once each: pick one, then a squad.</summary>
        public List<ItemStats> Items { get; } = new List<ItemStats>();
        /// <summary>The item picked from the list, waiting for a squad (hexagons show), or null.</summary>
        public ItemStats PlacingItem { get; private set; }
        public int Seed { get => seed; set => seed = value; }
        /// <summary>Which side new squads go to.</summary>
        public Team EditingTeam { get; set; } = Team.A;
        /// <summary>False while placing; true once Run was pressed (until Reset).</summary>
        public bool IsBattle { get; private set; }
        public string Message { get; private set; } = "";

        /// <summary>The unit type being placed, or null.</summary>
        public UnitStats PlacingType { get; private set; }
        public int MovingSquadId { get; private set; } = -1;
        private Team _movingTeam;

        // IDeploymentPlacement
        public bool IsDeploying => !IsBattle;
        public bool IsPlacing => PlacingType != null || MovingSquadId >= 0;
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
            _views = GetComponent<UnitViewManager>();

            var cache = new Dictionary<UnitDefinition, UnitStats>();
            foreach (FactionDefinition faction in factions)
            {
                if (faction == null) continue;
                FactionInfo info = faction.ToInfo(cache, _cardSources);
                foreach (UnitStats unit in info.BaseRoster) Units.Add(new SandboxUnit(info.Name, unit));
                foreach (SubFactionInfo sub in info.SubFactions)
                {
                    foreach (UnitStats unit in sub.ExclusiveUnits) Units.Add(new SandboxUnit($"{info.Name}: {sub.Name}", unit, info.Name));
                }
                foreach (CardStats card in info.Cards)
                {
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
                Units.Add(new SandboxUnit(ExtraUnitsLabel, stats));
            }
            foreach (var pair in cache) _sources[pair.Value] = pair.Key;
        }

        private void Start() => Refresh();

        private void Update()
        {
            if (PlacingItem == null) return;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame)) CancelPlacement();
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
                return;
            }
            PlacedSquad squad = SelectedPlacedSquad;
            if (squad == null) return;
            PlayerState army = OwnerOf(squad);
            if (DeploymentGrid.CanPlace(army, squad.Type, squad.Column, squad.Row, !squad.Rotated90, _battle.Rules, squad.Id))
            {
                squad.Rotated90 = !squad.Rotated90;
                Refresh(squad.Id);
            }
            else Message = "No room to turn it there.";
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
                target.Squads.Add(new PlacedSquad { Id = _nextSquadId++, Type = squad.Type, Column = squad.Column, Row = squad.Row, Rotated90 = squad.Rotated90, PricePaid = squad.PricePaid, Level = squad.Level, Item = squad.Item });
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
            _battle.ShowBattle(DeploymentGrid.CreateBattle(_battle.Rules, ArmyA, ArmyB, seed), _sources, play: true);
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
            BattleSimulation preview = DeploymentGrid.CreateBattle(_battle.Rules, ArmyA, ArmyB, seed);
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
            return result;
        }
    }
}
