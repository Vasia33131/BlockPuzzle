using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Immutable description of a single puzzle figure: which cells it occupies and
    /// which colour it is painted with. Authored as an asset or created at runtime
    /// from <see cref="ShapeCatalog"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BlockShape", menuName = "Block Puzzle/Block Shape")]
    public class BlockShape : ScriptableObject
    {
        [SerializeField] private string displayName = "Shape";
        [SerializeField] private Color color = Color.cyan;

        [Tooltip("Index into the active theme block palette. Negative means use the stored colour.")]
        [SerializeField] private int paletteIndex = -1;

        [Tooltip("When set, the figure always uses the active theme's starting-block colour.")]
        [SerializeField] private bool usesStartingColor;

        [Tooltip("Cell offsets of the figure. X grows to the right, Y grows downwards.")]
        [SerializeField] private Vector2Int[] cells = { Vector2Int.zero };

        [Tooltip("Relative chance of this figure being drawn from the bag.")]
        [SerializeField, Min(0.01f)] private float weight = 1f;

        private int cachedWidth = -1;
        private int cachedHeight = -1;

        public string DisplayName => displayName;
        public int PaletteIndex => paletteIndex;
        public bool UsesStartingColor => usesStartingColor;

        /// <summary>
        /// Live colour of the figure: starting blocks follow the theme, catalog pieces
        /// follow <see cref="GameTheme.Pastel"/>, authored colours are remapped when they
        /// match a known palette entry.
        /// </summary>
        public Color Color
        {
            get
            {
                if (usesStartingColor)
                {
                    return GameTheme.StartingBlock;
                }

                if (paletteIndex >= 0)
                {
                    return GameTheme.Pastel(paletteIndex);
                }

                return GameTheme.ResolvePlayableColor(color);
            }
        }

        public float Weight => Mathf.Max(0.01f, weight);
        public IReadOnlyList<Vector2Int> Cells => cells;
        public int BlockCount => cells.Length;

        public int Width
        {
            get
            {
                EnsureBoundsCached();
                return cachedWidth;
            }
        }

        public int Height
        {
            get
            {
                EnsureBoundsCached();
                return cachedHeight;
            }
        }

        /// <summary>Creates a shape instance in memory, used by the built-in catalog.</summary>
        public static BlockShape Create(string displayName, Color color, float weight, params Vector2Int[] cells)
        {
            BlockShape shape = CreateInstance<BlockShape>();
            shape.name = displayName;
            shape.displayName = displayName;
            shape.color = color;
            shape.paletteIndex = -1;
            shape.usesStartingColor = false;
            shape.weight = weight;
            shape.cells = cells != null && cells.Length > 0 ? cells : new[] { Vector2Int.zero };
            shape.Normalize();
            return shape;
        }

        /// <summary>Catalog figure whose colour tracks palette slot <paramref name="paletteIndex"/>.</summary>
        public static BlockShape Create(string displayName, int paletteIndex, float weight, params Vector2Int[] cells)
        {
            BlockShape shape = Create(displayName, GameTheme.Pastel(paletteIndex), weight, cells);
            shape.paletteIndex = paletteIndex;
            return shape;
        }

        /// <summary>
        /// Creates a shape from an occupancy matrix indexed as <c>[row, column]</c>, where every
        /// <c>true</c> entry is a filled cell. Rows grow downwards, like board coordinates.
        /// </summary>
        public static BlockShape CreateFromMatrix(string displayName, Color color, float weight, bool[,] matrix)
        {
            return Create(displayName, color, weight, MatrixToCells(matrix));
        }

        /// <summary>Starting-layout figure that always uses the active theme's starting-block colour.</summary>
        public static BlockShape CreateStartingFromMatrix(string displayName, float weight, bool[,] matrix)
        {
            BlockShape shape = Create(displayName, GameTheme.StartingBlock, weight, MatrixToCells(matrix));
            shape.usesStartingColor = true;
            return shape;
        }

        /// <summary>Converts an occupancy matrix into the cell offsets of a figure.</summary>
        public static Vector2Int[] MatrixToCells(bool[,] matrix)
        {
            if (matrix == null)
            {
                return new[] { Vector2Int.zero };
            }

            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            var result = new List<Vector2Int>(rows * columns);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    if (matrix[row, col])
                    {
                        result.Add(new Vector2Int(col, row));
                    }
                }
            }

            return result.Count > 0 ? result.ToArray() : new[] { Vector2Int.zero };
        }

        /// <summary>Occupancy matrix of the bounding box, indexed as <c>[row, column]</c>.</summary>
        public bool[,] ToMatrix()
        {
            var matrix = new bool[Height, Width];
            foreach (Vector2Int cell in cells)
            {
                matrix[cell.y, cell.x] = true;
            }

            return matrix;
        }

        /// <summary>Shifts the cells so the top-left corner of the bounding box sits at (0, 0).</summary>
        public void Normalize()
        {
            if (cells == null || cells.Length == 0)
            {
                cells = new[] { Vector2Int.zero };
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (Vector2Int cell in cells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
            }

            if (minX != 0 || minY != 0)
            {
                var offset = new Vector2Int(minX, minY);
                for (int i = 0; i < cells.Length; i++)
                {
                    cells[i] -= offset;
                }
            }

            InvalidateBounds();
        }

        /// <summary>Geometric centre of the bounding box, used to centre the figure inside its slot.</summary>
        public Vector2 BoundsCenter => new Vector2((Width - 1) * 0.5f, (Height - 1) * 0.5f);

        private void OnValidate()
        {
            InvalidateBounds();
        }

        private void InvalidateBounds()
        {
            cachedWidth = -1;
            cachedHeight = -1;
        }

        private void EnsureBoundsCached()
        {
            if (cachedWidth > 0 && cachedHeight > 0)
            {
                return;
            }

            int maxX = 0;
            int maxY = 0;
            foreach (Vector2Int cell in cells)
            {
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            cachedWidth = maxX + 1;
            cachedHeight = maxY + 1;
        }
    }
}
