// SquadSelection.cs
// Lets the player select a squad by left-clicking any of its bodies (Bible Section 13: selecting a
// squad opens the stats window). Right-click or Escape clears the selection. Picking uses the
// simulation's positions and collision sizes, so no physics colliders are needed. Clicks on HUD
// windows are ignored (see UiHitAreas).
// Depends on: BattleController, UnitViewManager, UiHitAreas, Halobellum.Combat, Unity Input System.

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

        private void Start()
        {
            _battle = GetComponent<BattleController>();
            _units = GetComponent<UnitViewManager>();
        }

        private void Update()
        {
            if (_battle.Simulation == null) return;
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Select(-1);
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) Select(-1);
            if (!mouse.leftButton.wasPressedThisFrame) return;

            Vector2 pointer = mouse.position.ReadValue();
            if (UiHitAreas.Contains(pointer)) return;

            Camera camera = Camera.main;
            if (camera == null) return;
            SimUnit picked = Pick(camera.ScreenPointToRay(pointer));
            Select(picked != null ? picked.SquadId : -1);
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
