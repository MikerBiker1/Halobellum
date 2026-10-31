// DeploymentGrid.cs
// The deployment grid (Bible Section 5, Placement): each player's zone is a grid of squares; a squad
// covers a rectangle of squares (its footprint) worked out from its formation size, snapped to whole
// squares. Squads must stay inside the zone and cannot overlap; air squads only collide with air
// squads. Turning a squad 90 degrees swaps its width and depth.
// Coordinates are in the owner's frame: column 0 is the owner's left edge, row 0 is the front edge
// of the owner's zone. Team B's grid is the mirror image of Team A's.
// Depends on: Halobellum.Core (UnitStats, CombatRules, Vec2), Halobellum.Combat (SquadFormation), PlayerState.

using System;
using Halobellum.Combat;
using Halobellum.Core;

namespace Halobellum.Economy
{
    public static class DeploymentGrid
    {
        /// <summary>Width (across) and depth of a squad's footprint in squares.</summary>
        public static void Footprint(UnitStats stats, bool rotated90, CombatRules rules, out int width, out int depth)
        {
            int columns = SquadFormation.Columns(stats.SquadSize);
            int rows = (int)MathF.Ceiling(stats.SquadSize / (float)columns);
            float spacing = stats.CollisionRadius * 2f + rules.FormationGap;
            float widthMetres = (columns - 1) * spacing + stats.CollisionRadius * 2f;
            float depthMetres = (rows - 1) * spacing + stats.CollisionRadius * 2f;
            width = Math.Max(1, (int)MathF.Ceiling(widthMetres / rules.SquareSize - 1e-3f));
            depth = Math.Max(1, (int)MathF.Ceiling(depthMetres / rules.SquareSize - 1e-3f));
            if (rotated90) (width, depth) = (depth, width);
        }

        /// <summary>True if the footprint lies completely inside the owner's deployment zone.</summary>
        public static bool IsInsideZone(int column, int row, int width, int depth, CombatRules rules)
        {
            return column >= 0 && row >= 0 && column + width <= rules.ArenaWidthSquares && row + depth <= rules.DeploymentDepthSquares;
        }

        /// <summary>
        /// True if a squad of this type could stand at this spot: inside the zone and not overlapping
        /// another squad on the same layer (ground or air). ignoreSquadId skips one squad (the one being moved).
        /// </summary>
        public static bool CanPlace(PlayerState player, UnitStats type, int column, int row, bool rotated90, CombatRules rules, int ignoreSquadId = -1)
        {
            Footprint(type, rotated90, rules, out int width, out int depth);
            if (!IsInsideZone(column, row, width, depth, rules)) return false;

            foreach (PlacedSquad other in player.Squads)
            {
                if (other.Id == ignoreSquadId || other.Type.IsAir != type.IsAir) continue;
                Footprint(other.Type, other.Rotated90, rules, out int otherWidth, out int otherDepth);
                bool overlap = column < other.Column + otherWidth && other.Column < column + width
                               && row < other.Row + otherDepth && other.Row < row + depth;
                if (overlap) return false;
            }
            return true;
        }

        /// <summary>
        /// The squad's centre in squares, in the form battle setups use: X across the field from the
        /// centre line, Y depth behind the zone's front edge.
        /// </summary>
        public static Vec2 CentreInSquares(PlacedSquad squad, CombatRules rules)
        {
            Footprint(squad.Type, squad.Rotated90, rules, out int width, out int depth);
            return new Vec2(squad.Column + width * 0.5f - rules.ArenaWidthSquares * 0.5f, squad.Row + depth * 0.5f);
        }

        /// <summary>
        /// The free spot closest to the middle of the front of the zone, searching row by row from the
        /// front and outward from the centre. Returns false if the zone is full.
        /// </summary>
        public static bool FindFreeSpot(PlayerState player, UnitStats type, bool rotated90, CombatRules rules, out int column, out int row)
        {
            Footprint(type, rotated90, rules, out int width, out _);
            int centreColumn = (rules.ArenaWidthSquares - width) / 2;
            for (row = 0; row < rules.DeploymentDepthSquares; row++)
            {
                for (int step = 0; step <= rules.ArenaWidthSquares; step++)
                {
                    // Try centre, then alternately left and right of it.
                    int offset = (step + 1) / 2 * (step % 2 == 1 ? -1 : 1);
                    column = centreColumn + offset;
                    if (CanPlace(player, type, column, row, rotated90, rules)) return true;
                }
            }
            column = row = -1;
            return false;
        }
    }
}
