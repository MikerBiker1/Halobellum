// DeploymentGrid.cs
// The deployment grid (Bible Section 5, Placement): each player's zone is a grid of squares; a squad
// covers a rectangle of squares (its footprint) worked out from its formation size, snapped to whole
// squares. Squads must stay inside the zone and cannot overlap; air squads only collide with air
// squads. Turning a squad 90 degrees swaps its width and depth.
// Coordinates are in the owner's frame: column 0 is the owner's left edge, row 0 is the front edge
// of the owner's zone. Team B's grid is the mirror image of Team A's.
// CreateBattle turns two deployments into a battle (used by the Match and the Sandbox).
// Depends on: Halobellum.Core (UnitStats, CombatRules, Vec2), Halobellum.Combat (SquadFormation), PlayerState.

using System;
using System.Collections.Generic;
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
        /// A player's deployment as an army for the battle, squads in deployment order. Each squad fights
        /// with its level and the player's tech upgrades applied (PlayerState.EffectiveStats).
        /// </summary>
        public static ArmySetup BuildArmy(PlayerState player, CombatRules rules)
        {
            var army = new ArmySetup(player.Label);
            foreach (PlacedSquad squad in player.Squads)
            {
                army.AddAt(player.EffectiveStats(squad), new List<Vec2> { CentreInSquares(squad, rules) }, new List<bool> { squad.Rotated90 });
            }
            return army;
        }

        /// <summary>
        /// A battle (not started) from two deployments: squads in deployment order, Team A first, so
        /// simulation squad i is placed squad i. playerB may be null (an empty side, e.g. a preview).
        /// </summary>
        public static BattleSimulation CreateBattle(CombatRules rules, PlayerState playerA, PlayerState playerB, int seed)
        {
            ArmySetup armyA = BuildArmy(playerA, rules);
            ArmySetup armyB = playerB != null ? BuildArmy(playerB, rules) : new ArmySetup("Team B");
            var layout = new RunnerSettings { Battles = 1, BaseSeed = seed, PositionJitter = 0f, FrontLineDistance = 0f, SquadGap = 0f };
            return HeadlessBattleRunner.CreateBattle(rules, armyA, armyB, layout, seed);
        }

        /// <summary>
        /// The free spot closest to the middle of the zone (where draft and newly bought squads
        /// appear). On equal distance the spot nearer the front wins, then the one further left.
        /// Returns false if the zone is full.
        /// </summary>
        public static bool FindFreeSpot(PlayerState player, UnitStats type, bool rotated90, CombatRules rules, out int column, out int row)
        {
            Footprint(type, rotated90, rules, out int width, out int depth);
            return FindFreeSpotNear(player, type, rotated90, rules, (rules.ArenaWidthSquares - width) / 2, (rules.DeploymentDepthSquares - depth) / 2, out column, out row);
        }

        /// <summary>
        /// The free spot whose top-left square is closest to (targetColumn, targetRow), for example to
        /// put tanks in front and artillery at the back. Same tie rules as FindFreeSpot.
        /// </summary>
        public static bool FindFreeSpotNear(PlayerState player, UnitStats type, bool rotated90, CombatRules rules,
            int targetColumn, int targetRow, out int column, out int row)
        {
            Footprint(type, rotated90, rules, out int width, out int depth);
            int bestDistance = int.MaxValue;
            column = row = -1;
            for (int r = 0; r + depth <= rules.DeploymentDepthSquares; r++)
            {
                for (int c = 0; c + width <= rules.ArenaWidthSquares; c++)
                {
                    int distance = (c - targetColumn) * (c - targetColumn) + (r - targetRow) * (r - targetRow);
                    if (distance >= bestDistance || !CanPlace(player, type, c, r, rotated90, rules)) continue;
                    bestDistance = distance;
                    column = c;
                    row = r;
                }
            }
            return column >= 0;
        }
    }
}
