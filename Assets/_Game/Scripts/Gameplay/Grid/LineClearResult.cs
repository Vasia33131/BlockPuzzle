using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Grid
{
    /// <summary>
    /// Description of the rows and columns that became complete after a placement.
    /// </summary>
    public sealed class LineClearResult
    {
        public static readonly LineClearResult Empty = new LineClearResult(
            new List<int>(), new List<int>(), new List<Vector2Int>());

        public LineClearResult(IReadOnlyList<int> rows, IReadOnlyList<int> columns, IReadOnlyList<Vector2Int> cells)
        {
            Rows = rows;
            Columns = columns;
            Cells = cells;
        }

        public IReadOnlyList<int> Rows { get; }
        public IReadOnlyList<int> Columns { get; }

        /// <summary>Every distinct cell covered by the completed lines.</summary>
        public IReadOnlyList<Vector2Int> Cells { get; }

        public int LineCount => Rows.Count + Columns.Count;
        public bool HasLines => LineCount > 0;
    }
}
