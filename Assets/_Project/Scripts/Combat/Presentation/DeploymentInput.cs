// DeploymentInput.cs
// Mouse and keyboard during deployment (Bible Section 5, Placement). While a squad is being placed
// or moved, a flat "ghost" shows its footprint snapped to the grid squares under the mouse: green
// where it fits, red where it doesn't.
//   Left-click: place / move there.   R: turn 90 degrees.   Right-click or Escape: cancel.
// When nothing is being placed: M moves the selected squad (if bought this round), R turns it,
// Delete sells it.
// Depends on: MatchController, BattleController (rules), UiHitAreas, PlaceholderArt,
//             Halobellum.Economy (DeploymentGrid, MatchPhase), Unity Input System.

using Halobellum.Core;
using Halobellum.Economy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Halobellum.Presentation
{
    [RequireComponent(typeof(MatchController))]
    public sealed class DeploymentInput : MonoBehaviour
    {
        [SerializeField] private Color validColor = new Color(0.3f, 0.95f, 0.4f);
        [SerializeField] private Color invalidColor = new Color(0.95f, 0.3f, 0.25f);

        private MatchController _match;
        private BattleController _battle;
        private Transform _ghost;
        private MeshRenderer _ghostRenderer;
        private Material _validMaterial, _invalidMaterial;

        private void Start()
        {
            _match = GetComponent<MatchController>();
            _battle = GetComponent<BattleController>();
            _validMaterial = PlaceholderArt.MaterialFor(validColor);
            _invalidMaterial = PlaceholderArt.MaterialFor(invalidColor);
            _ghost = PlaceholderArt.CreatePart("Placement Ghost", null, PlaceholderArt.Cube, _validMaterial);
            _ghostRenderer = _ghost.GetComponent<MeshRenderer>();
            _ghost.gameObject.SetActive(false);
        }

        private void Update()
        {
            Match match = _match.Match;
            bool deploying = match != null && match.Phase == MatchPhase.Deployment;
            if (!deploying)
            {
                if (_match.IsPlacing) _match.CancelPlacement();
                _ghost.gameObject.SetActive(false);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (!_match.IsPlacing)
            {
                _ghost.gameObject.SetActive(false);
                if (keyboard == null) return;
                PlacedSquad selected = _match.SelectedPlacedSquad;
                if (selected == null) return;
                if (keyboard.mKey.wasPressedThisFrame) _match.BeginMoving(selected);
                else if (keyboard.rKey.wasPressedThisFrame) _match.ToggleRotation();
                else if (keyboard.deleteKey.wasPressedThisFrame) _match.Sell(selected);
                return;
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) _match.ToggleRotation();
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                _match.CancelPlacement();
                return;
            }
            if (mouse == null) return;

            Vector2 pointer = mouse.position.ReadValue();
            if (UiHitAreas.Contains(pointer) || !PointerCell(pointer, out int column, out int row))
            {
                _ghost.gameObject.SetActive(false);
                return;
            }

            ShowGhost(column, row);
            if (mouse.leftButton.wasPressedThisFrame) _match.PlaceAt(column, row);
        }

        /// <summary>The grid spot (left column, front row) that centres the footprint under the mouse.</summary>
        private bool PointerCell(Vector2 pointer, out int column, out int row)
        {
            column = row = 0;
            Camera camera = Camera.main;
            if (camera == null || _match.PlacementType == null) return false;
            Ray ray = camera.ScreenPointToRay(pointer);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return false;
            float distance = -ray.origin.y / ray.direction.y;
            if (distance < 0f) return false;
            Vector3 hit = ray.origin + ray.direction * distance;

            CombatRules rules = _battle.Rules;
            DeploymentGrid.Footprint(_match.PlacementType, _match.PlacingRotated, rules, out int width, out int depth);
            float columnF = (hit.x + rules.ArenaWidth * 0.5f) / rules.SquareSize;
            float rowF = (-hit.z - rules.DeploymentZoneFront) / rules.SquareSize; // Rows count back from the zone's front.
            column = Mathf.RoundToInt(columnF - width * 0.5f);
            row = Mathf.RoundToInt(rowF - depth * 0.5f);
            return true;
        }

        private void ShowGhost(int column, int row)
        {
            CombatRules rules = _battle.Rules;
            DeploymentGrid.Footprint(_match.PlacementType, _match.PlacingRotated, rules, out int width, out int depth);
            float x = (column + width * 0.5f) * rules.SquareSize - rules.ArenaWidth * 0.5f;
            float z = -(rules.DeploymentZoneFront + (row + depth * 0.5f) * rules.SquareSize);
            _ghost.SetPositionAndRotation(new Vector3(x, 0.06f, z), Quaternion.identity);
            _ghost.localScale = new Vector3(width * rules.SquareSize - 0.1f, 0.1f, depth * rules.SquareSize - 0.1f);
            _ghostRenderer.sharedMaterial = _match.CanPlaceAt(column, row) ? _validMaterial : _invalidMaterial;
            _ghost.gameObject.SetActive(true);
        }
    }
}
