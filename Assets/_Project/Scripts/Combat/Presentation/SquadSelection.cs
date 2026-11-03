// SquadSelection.cs
// Lets the player select a squad by left-clicking any of its bodies (Bible Section 13: selecting a
// squad opens the stats window). A right-click (not a drag, which turns the camera: RightClick) or Escape clears the selection. Picking uses the
// simulation's positions and collision sizes, so no physics colliders are needed. Clicks on HUD
// windows are ignored (see UiHitAreas).
// Depends on: BattleController, UnitViewManager, RightClick, UiHitAreas, Halobellum.Combat, Unity Input System.

using System;
using Halobellum.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    public sealed class SquadSelection : MonoBehaviour
    {
        [Tooltip("Extra metres around a body that still count as clicking it.")]
        [SerializeField] private float pickTolerance = 0.6f;

        private BattleController _battle;
        private UnitViewManager _units;

        /// <summary>Selected squad id, or -1 when nothing is selected.</summary>
        public int SelectedSquadId { get; private set; } = -1;

        /// <summary>Raised with the new squad id (-1 for none) whenever the selection changes.</summary>
        public event Action<int> SelectionChanged;

        /// <summary>When false, mouse and keyboard don't change the selection (for example while placing a squad).</summary>
        public bool InputEnabled { get; set; } = true;

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _units = GetComponent<UnitViewManager>();
            _battle.BattleChanged += _ => Select(-1); // Squad ids belong to one battle.
        }

        private void Update()
        {
            if (_battle.Simulation == null || !InputEnabled) return;
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || RightClick.WasClicked) Select(-1);
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            Vector2 pointer = mouse.position.ReadValue();
            if (UiHitAreas.Contains(pointer)) return;

            SimUnit picked = BodyAt(pointer);
            SelectByPlayer(picked != null ? picked.SquadId : -1);
        }

        /// <summary>The living body under the pointer, seen from wherever the camera is now, or null.</summary>
        public SimUnit BodyAt(Vector2 pointer)
        {
            Camera camera = Camera.main;
            return camera != null ? Pick(camera.ScreenPointToRay(pointer)) : null;
        }

        /// <summary>The player's own click on a squad: like Select, and heard. Select alone stays silent, because it also runs each time a preview is rebuilt and when a bought squad lands.</summary>
        public void SelectByPlayer(int squadId)
        {
            if (squadId >= 0 && squadId != SelectedSquadId) UiAudio.SquadSelected();
            Select(squadId);
        }

        /// <summary>Selects a squad by id (-1 clears the selection).</summary>
        public void Select(int squadId)
        {
            if (squadId == SelectedSquadId) return;
            SelectedSquadId = squadId;
            SelectionChanged?.Invoke(squadId);
        }

        /// <summary>A living body of the selected squad, or null if none is left.</summary>
        public SimUnit FirstAliveOfSelected()
        {
            if (SelectedSquadId < 0 || _battle.Simulation == null) return null;
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (unit.SquadId == SelectedSquadId && unit.IsAlive) return unit;
            }
            return null;
        }

        /// <summary>The living body closest to the mouse ray, within its radius plus the tolerance.</summary>
        private SimUnit Pick(Ray ray)
        {
            SimUnit best = null;
            float bestDistance = float.MaxValue;
            foreach (SimUnit unit in _battle.Simulation.Units)
            {
                if (!unit.IsAlive) continue;
                Vector3 centre = _units.WorldPosition(unit);
                centre.y += unit.IsAir ? 0f : unit.Radius;
                float distance = Vector3.Cross(ray.direction, centre - ray.origin).magnitude; // Distance from the ray.
                if (distance <= unit.Radius + pickTolerance && distance < bestDistance)
                {
                    best = unit;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }
}
