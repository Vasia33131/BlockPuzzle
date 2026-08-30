using UnityEngine;

namespace BlockPuzzle.Grid
{
    /// <summary>
    /// Independent copy of a <see cref="GridModel"/> board: occupancy and cell colours.
    /// Safe to keep across later mutations of the live model.
    /// </summary>
    public sealed class BoardSnapshot
    {
        public BoardSnapshot(int size, bool[,] occupied, Color[,] colors)
        {
            Size = Mathf.Max(1, size);
            Occupied = new bool[Size, Size];
            Colors = new Color[Size, Size];
            CopyFrom(occupied, colors);
        }

        public int Size { get; }

        public bool[,] Occupied { get; }

        public Color[,] Colors { get; }

        /// <summary>
        /// Maps every occupied cell through <paramref name="remap"/> so an undo step
        /// stays in the colours of the theme that is now active.
        /// </summary>
        public void RemapColors(System.Func<Color, Color> remap)
        {
            if (remap == null)
            {
                return;
            }

            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    if (Occupied[row, col])
                    {
                        Colors[row, col] = remap(Colors[row, col]);
                    }
                }
            }
        }

        /// <summary>Writes this snapshot into the destination arrays, which must match <see cref="Size"/>.</summary>
        public void CopyTo(bool[,] occupied, Color[,] colors)
        {
            if (occupied == null || colors == null)
            {
                return;
            }

            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                {
                    occupied[row, col] = Occupied[row, col];
                    colors[row, col] = Colors[row, col];
                }
            }
        }

        private void CopyFrom(bool[,] occupied, Color[,] colors)
        {
            if (occupied == null || colors == null)
            {
                return;
            }

            int rows = Mathf.Min(Size, occupied.GetLength(0));
            int cols = Mathf.Min(Size, occupied.GetLength(1));

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    Occupied[row, col] = occupied[row, col];
                    Colors[row, col] = colors[row, col];
                }
            }
        }
    }
}
