// SandboxController.cs
// Sandbox mode (Milestone 4, used for balancing): pick two armies from every faction's roster
// (plus the placeholder test units, which never appear in matches),
// place them freely in both deployment zones (no credits, no limits, every squad movable), run the
// battle, then reset to the same placement and try again. Both armies are shown while placing.
// Placement uses the same deployment grid as the match (snap to squares, turn 90 degrees, no
// overlap), so a sandbox battle plays exactly like a match battle with the same deployments.
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

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(BattleController))]
    public sealed class SandboxController : MonoBehaviour, IDeploymentPlacement
    {
        /// <summary>One unit type the sandbox offers, with the faction it comes from.</summary>
        public readonly struct SandboxUnit
        {
            public readonly string Faction;
            public readonly UnitStats Stats;

            public SandboxUnit(string faction, UnitStats stats)
            {
                Faction = faction;
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
        private readonly Dictionary<UnitStats, UnitDefinition> _sources = new Dictionary<UnitStats, UnitDefinition>();
        private readonly List<PlacedSquad> _previewOrder = new List<PlacedSquad>();
        private int _nextSquadId = 1;

        public PlayerState ArmyA { get; } = new PlayerState(Team.A);
        public PlayerState ArmyB { get; } = new PlayerState(Team.B);
        public List<SandboxUnit> Units { get; } = new List<SandboxUnit>();
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

            var cache = new Dictionary<UnitDefinition, UnitStats>();
            foreach (FactionDefinition faction in factions)
            {
                if (faction == null) continue;
                FactionInfo info = faction.ToInfo(cache);
                foreach (UnitStats unit in info.BaseRoster) Units.Add(new SandboxUnit(info.Name, unit));
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
            Refresh();
        }

        /// <summary>Copies one side's army to the other side (same spots in its own zone), replacing it.</summary>
        public void CopyToOtherSide(Team from)
        {
            if (IsBattle) return;
            CancelPlacement();
            PlayerState target = Army(from.Opponent());
            target.Squads.Clear();
            foreach (PlacedSquad squad in Army(from).Squads)
            {
                target.Squads.Add(new PlacedSquad { Id = _nextSquadId++, Type = squad.Type, Column = squad.Column, Row = squad.Row, Rotated90 = squad.Rotated90, PricePaid = squad.PricePaid });
            }
            Refresh();
        }

        /// <summary>Total cost of one side's squads (to compare armies of equal value).</summary>
        public int ArmyCost(Team team)
        {
            int total = 0;
            foreach (PlacedSquad squad in Army(team).Squads) total += squad.Type.Cost;
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
