// IDeploymentPlacement.cs
// What DeploymentInput needs from a scene that lets the player place squads on the deployment grid:
// the Match (MatchController: the player's own side) and the Sandbox (SandboxController: either side).
// Depends on: Halobellum.Economy (PlacedSquad, ActionResult), Halobellum.Core.

using Halobellum.Core;
using Halobellum.Economy;

namespace Halobellum.Presentation
{
    public interface IDeploymentPlacement
    {
        /// <summary>True while squads can be placed (deployment phase, sandbox editing).</summary>
        bool IsDeploying { get; }
        /// <summary>True while a squad is being placed or moved (the ghost follows the mouse).</summary>
        bool IsPlacing { get; }
        /// <summary>The unit type being placed or moved, or null.</summary>
        UnitStats PlacementType { get; }
        bool PlacingRotated { get; }
        /// <summary>True if the placing side's flank zones should show (D61).</summary>
        bool ShowsFlankZones { get; }
        /// <summary>True if they glow on and off (the tutorial's flank zone step).</summary>
        bool PulsesFlankZones { get; }
        /// <summary>Whose zone the squad goes in (Team B's grid is the mirror image of Team A's).</summary>
        Team PlacingTeam { get; }
        /// <summary>The placed squad currently selected, or null.</summary>
        PlacedSquad SelectedPlacedSquad { get; }

        void CancelPlacement();
        /// <summary>Turns the squad being placed or moved, or the selected squad where it stands.</summary>
        void ToggleRotation();
        void BeginMoving(PlacedSquad squad);
        /// <summary>Takes the squad off the field (sold in a match, removed in the sandbox).</summary>
        ActionResult Remove(PlacedSquad squad);
        bool CanPlaceAt(int column, int row);
        ActionResult PlaceAt(int column, int row);
    }
}
