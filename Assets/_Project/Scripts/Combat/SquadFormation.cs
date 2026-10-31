// SquadFormation.cs
// Works out where each body of a squad starts, around the squad's centre. Bodies stand in rows,
// about twice as wide as deep (an 18-body squad is 6 wide and 3 deep), facing the enemy. Turning
// the squad 90 degrees swaps width and depth (Bible Section 5, Placement). Used when a squad is
// added to a battle; the deployment grid itself is built in Milestone 3.
// Depends on: Halobellum.Core (Vec2).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public static class SquadFormation
    {
        /// <summary>Number of bodies per row for a squad of this size.</summary>
        public static int Columns(int squadSize)
        {
            if (squadSize <= 1) return 1;
            return Math.Min(squadSize, (int)MathF.Ceiling(MathF.Sqrt(squadSize * 2f)));
        }

        /// <summary>
        /// Offsets from the squad centre for each body. X is across the battlefield, Y is toward the
        /// enemy for Team A. Bodies are spaced by their diameter plus the gap.
        /// </summary>
        public static List<Vec2> Offsets(int squadSize, float bodyRadius, float gap, bool rotated90)
        {
            var offsets = new List<Vec2>(squadSize);
            if (squadSize <= 0) return offsets;

            int columns = Columns(squadSize);
            int rows = (int)MathF.Ceiling(squadSize / (float)columns);
            float spacing = bodyRadius * 2f + gap;

            for (int i = 0; i < squadSize; i++)
            {
                int row = i / columns;
                int column = i % columns;
                // Centre a short last row.
                int inThisRow = row == rows - 1 ? squadSize - row * columns : columns;
                float x = (column - (inThisRow - 1) * 0.5f) * spacing;
                float y = ((rows - 1) * 0.5f - row) * spacing;
                offsets.Add(rotated90 ? new Vec2(y, x) : new Vec2(x, y));
            }
            return offsets;
        }
    }
}
