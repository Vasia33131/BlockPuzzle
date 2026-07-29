using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.Grid
{
    /// <summary>
    /// Plain C# model of the playfield. Holds no Unity objects, which keeps the rules
    /// testable and completely separated from how the board is drawn.
    /// Coordinates use <c>x = column</c> and <c>y = row</c>, with row 0 at the top.
    /// </summary>
    public sealed class GridModel
    {
        private readonly bool[,] occupied;
        private readonly Color[,] colors;

        public GridModel(int size = GameTheme.GridSize)
        {
            Size = Mathf.Max(1, size);
            occupied = new bool[Size, Size];
            colors = new Color[Size, Size];
        }

        public int Size { get; }

        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int row = 0; row < Size; row++)
                {
                    for (int col = 0; col < Size; col++)
                    {
                        if (occupied[row, col])
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
        }

        public bool IsInside(Vector2Int cell) => IsInside(cell.y, cell.x);

        public bool IsInside(int row, int col) => row >= 0 && row < Size && col >= 0 && col < Size;

        public bool IsOccupied(Vector2Int cell) => IsOccupied(cell.y, cell.x);

        public bool IsOccupied(int row, int col) => IsInside(row, col) && occupied[row, col];

        public Color GetColor(int row, int col) => colors[row, col];

        /// <summary>True when every cell of <paramref name="shape"/> fits on empty ground.</summary>
        public bool CanPlace(BlockShape shape, Vector2Int origin)
        {
            if (shape == null)
            {
                return false;
            }

            IReadOnlyList<Vector2Int> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int target = origin + cells[i];
                if (!IsInside(target) || occupied[target.y, target.x])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Writes the shape into the board and returns the cells it now owns.</summary>
        public List<Vector2Int> Place(BlockShape shape, Vector2Int origin)
        {
            var placed = new List<Vector2Int>(shape.BlockCount);
            IReadOnlyList<Vector2Int> cells = shape.Cells;

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int target = origin + cells[i];
                occupied[target.y, target.x] = true;
                colors[target.y, target.x] = shape.Color;
                placed.Add(target);
            }

            return placed;
        }

        /// <summary>True when the shape fits anywhere on the current board.</summary>
        public bool HasPlacementFor(BlockShape shape)
        {
            if (shape == null)
            {
                return false;
            }

            int maxRow = Size - shape.Height;
            int maxCol = Size - shape.Width;

            for (int row = 0; row <= maxRow; row++)
            {
                for (int col = 0; col <= maxCol; col++)
                {
                    if (CanPlace(shape, new Vector2Int(col, row)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool HasPlacementForAny(IEnumerable<BlockShape> shapes)
        {
            if (shapes == null)
            {
                return false;
            }

            foreach (BlockShape shape in shapes)
            {
                if (shape != null && HasPlacementFor(shape))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when dropping <paramref name="shape"/> at <paramref name="origin"/> would finish at
        /// least one row or column. Answers the question without touching the board.
        /// </summary>
        public bool WouldCompleteLine(BlockShape shape, Vector2Int origin)
        {
            if (shape == null)
            {
                return false;
            }

            IReadOnlyList<Vector2Int> cells = shape.Cells;
            var footprint = new HashSet<Vector2Int>();
            for (int i = 0; i < cells.Count; i++)
            {
                footprint.Add(origin + cells[i]);
            }

            foreach (Vector2Int cell in footprint)
            {
                if (IsRowComplete(cell.y, footprint) || IsColumnComplete(cell.x, footprint))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Collects every fully filled row and column without modifying the board.</summary>
        public LineClearResult FindCompletedLines()
        {
            var rows = new List<int>();
            var columns = new List<int>();

            for (int row = 0; row < Size; row++)
            {
                if (IsRowComplete(row))
                {
                    rows.Add(row);
                }
            }

            for (int col = 0; col < Size; col++)
            {
                if (IsColumnComplete(col))
                {
                    columns.Add(col);
                }
            }

            if (rows.Count == 0 && columns.Count == 0)
            {
                return LineClearResult.Empty;
            }

            var unique = new HashSet<Vector2Int>();
            foreach (int row in rows)
            {
                for (int col = 0; col < Size; col++)
                {
                    unique.Add(new Vector2Int(col, row));
                }
            }

            foreach (int col in columns)
            {
                for (int row = 0; row < Size; row++)
                {
                    unique.Add(new Vector2Int(col, row));
                }
            }

            return new LineClearResult(rows, columns, new List<Vector2Int>(unique));
        }

        /// <summary>Empties every cell listed in <paramref name="result"/>.</summary>
        public void ApplyClear(LineClearResult result)
        {
            if (result == null || !result.HasLines)
            {
                return;
            }

            foreach (Vector2Int cell in result.Cells)
            {
                occupied[cell.y, cell.x] = false;
                colors[cell.y, cell.x] = default;
            }
        }

        public void Reset()
        {
            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    occupied[row, col] = false;
                    colors[row, col] = default;
                }
            }
        }

        private bool IsRowComplete(int row) => IsRowComplete(row, null);

        private bool IsColumnComplete(int col) => IsColumnComplete(col, null);

        /// <summary>Cells listed in <paramref name="pending"/> count as filled.</summary>
        private bool IsRowComplete(int row, HashSet<Vector2Int> pending)
        {
            for (int col = 0; col < Size; col++)
            {
                if (!occupied[row, col] && (pending == null || !pending.Contains(new Vector2Int(col, row))))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Cells listed in <paramref name="pending"/> count as filled.</summary>
        private bool IsColumnComplete(int col, HashSet<Vector2Int> pending)
        {
            for (int row = 0; row < Size; row++)
            {
                if (!occupied[row, col] && (pending == null || !pending.Contains(new Vector2Int(col, row))))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
