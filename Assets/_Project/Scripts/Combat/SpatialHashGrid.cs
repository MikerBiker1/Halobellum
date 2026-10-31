// SpatialHashGrid.cs
// A uniform grid over the arena used to find nearby units quickly (Bible Section 6, Movement),
// so collision and separation stay fast with 300 vs 300 units. Each tick it is rebuilt from the
// unit list; Query returns every living unit whose centre is within a radius of a point.
// It allocates no memory per tick after the first build.
// Depends on: SimUnit, Halobellum.Core (Vec2).

using System;
using System.Collections.Generic;
using Halobellum.Core;

namespace Halobellum.Combat
{
    public sealed class SpatialHashGrid
    {
        private readonly float _cellSize;
        private readonly float _minX;
        private readonly float _minY;
        private readonly int _columns;
        private readonly int _rows;
        private readonly int[] _cellHead;
        private int[] _next = new int[0];
        private IReadOnlyList<SimUnit> _units;

        /// <summary>Largest collision radius among the units in the last build.</summary>
        public float MaxRadius { get; private set; }

        /// <param name="width">Arena width in metres (X from -width/2 to +width/2).</param>
        /// <param name="length">Arena length in metres (Y from -length/2 to +length/2).</param>
        public SpatialHashGrid(float width, float length, float cellSize)
        {
            if (cellSize <= 0f) throw new ArgumentException("Cell size must be above 0.", nameof(cellSize));
            _cellSize = cellSize;
            _minX = -width * 0.5f;
            _minY = -length * 0.5f;
            _columns = Math.Max(1, (int)MathF.Ceiling(width / cellSize));
            _rows = Math.Max(1, (int)MathF.Ceiling(length / cellSize));
            _cellHead = new int[_columns * _rows];
        }

        private int Column(float x) => Math.Clamp((int)MathF.Floor((x - _minX) / _cellSize), 0, _columns - 1);
        private int Row(float y) => Math.Clamp((int)MathF.Floor((y - _minY) / _cellSize), 0, _rows - 1);

        /// <summary>Sorts all living units into cells. Units outside the arena go into the nearest edge cell.</summary>
        public void Build(IReadOnlyList<SimUnit> units)
        {
            _units = units;
            Array.Fill(_cellHead, -1);
            if (_next.Length < units.Count) _next = new int[Math.Max(units.Count, _next.Length * 2)];
            MaxRadius = 0f;

            for (int i = 0; i < units.Count; i++)
            {
                SimUnit unit = units[i];
                if (!unit.IsAlive) continue;
                int cell = Row(unit.Position.Y) * _columns + Column(unit.Position.X);
                _next[i] = _cellHead[cell];
                _cellHead[cell] = i;
                if (unit.Radius > MaxRadius) MaxRadius = unit.Radius;
            }
        }

        /// <summary>Fills results with every living unit whose centre is within radius of center.</summary>
        public void Query(Vec2 center, float radius, List<SimUnit> results)
        {
            results.Clear();
            if (_units == null) return;

            float radiusSquared = radius * radius;
            int c0 = Column(center.X - radius), c1 = Column(center.X + radius);
            int r0 = Row(center.Y - radius), r1 = Row(center.Y + radius);

            for (int row = r0; row <= r1; row++)
            {
                for (int column = c0; column <= c1; column++)
                {
                    for (int i = _cellHead[row * _columns + column]; i >= 0; i = _next[i])
                    {
                        SimUnit unit = _units[i];
                        if (Vec2.DistanceSquared(unit.Position, center) <= radiusSquared) results.Add(unit);
                    }
                }
            }
        }
    }
}
