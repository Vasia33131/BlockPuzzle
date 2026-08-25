using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Built-in set of figures. Used whenever no authored <see cref="ShapeLibrary"/>
    /// asset is assigned, so the game is playable straight out of the box.
    /// Every figure is painted from <see cref="GameTheme.Pastel"/> so a theme change
    /// recolors newly dealt pieces without baking a palette into the catalog.
    /// </summary>
    public static class ShapeCatalog
    {
        /// <summary>Bumped whenever the figures or their colours change, so baked assets refresh.</summary>
        public const int Version = 3;

        private const int Blue = 0;
        private const int Sky = 1;
        private const int Aqua = 2;
        private const int Mint = 3;
        private const int Butter = 4;
        private const int Peach = 5;
        private const int Rose = 6;
        private const int Lavender = 7;

        public static List<BlockShape> CreateDefaultShapes()
        {
            var shapes = new List<BlockShape>
            {
                BlockShape.Create("Single", Aqua, 1.1f, C(0, 0)),

                BlockShape.Create("Domino H", Mint, 1.2f, C(0, 0), C(1, 0)),
                BlockShape.Create("Domino V", Mint, 1.2f, C(0, 0), C(0, 1)),

                BlockShape.Create("Line3 H", Blue, 1.1f, C(0, 0), C(1, 0), C(2, 0)),
                BlockShape.Create("Line3 V", Blue, 1.1f, C(0, 0), C(0, 1), C(0, 2)),

                BlockShape.Create("Line4 H", Lavender, 0.85f, C(0, 0), C(1, 0), C(2, 0), C(3, 0)),
                BlockShape.Create("Line4 V", Lavender, 0.85f, C(0, 0), C(0, 1), C(0, 2), C(0, 3)),

                BlockShape.Create("Line5 H", Sky, 0.5f, C(0, 0), C(1, 0), C(2, 0), C(3, 0), C(4, 0)),
                BlockShape.Create("Line5 V", Sky, 0.5f, C(0, 0), C(0, 1), C(0, 2), C(0, 3), C(0, 4)),

                BlockShape.Create("Square 2x2", Butter, 1f, C(0, 0), C(1, 0), C(0, 1), C(1, 1)),
                BlockShape.Create("Square 3x3", Peach, 0.35f,
                    C(0, 0), C(1, 0), C(2, 0),
                    C(0, 1), C(1, 1), C(2, 1),
                    C(0, 2), C(1, 2), C(2, 2)),

                BlockShape.Create("Corner TL", Rose, 1f, C(0, 0), C(1, 0), C(0, 1)),
                BlockShape.Create("Corner TR", Rose, 1f, C(0, 0), C(1, 0), C(1, 1)),
                BlockShape.Create("Corner BR", Rose, 1f, C(1, 0), C(0, 1), C(1, 1)),
                BlockShape.Create("Corner BL", Rose, 1f, C(0, 0), C(0, 1), C(1, 1)),

                BlockShape.Create("L Up", Peach, 0.7f, C(0, 0), C(0, 1), C(0, 2), C(1, 2), C(2, 2)),
                BlockShape.Create("L Right", Peach, 0.7f, C(0, 0), C(1, 0), C(2, 0), C(2, 1), C(2, 2)),
                BlockShape.Create("L Down", Peach, 0.7f, C(0, 0), C(1, 0), C(2, 0), C(0, 1), C(0, 2)),
                BlockShape.Create("L Left", Peach, 0.7f, C(0, 0), C(0, 1), C(1, 2), C(2, 2), C(0, 2)),

                BlockShape.Create("T Down", Lavender, 0.7f, C(0, 0), C(1, 0), C(2, 0), C(1, 1)),
                BlockShape.Create("T Up", Lavender, 0.7f, C(1, 0), C(0, 1), C(1, 1), C(2, 1)),

                BlockShape.Create("S", Aqua, 0.6f, C(1, 0), C(2, 0), C(0, 1), C(1, 1)),
                BlockShape.Create("Z", Aqua, 0.6f, C(0, 0), C(1, 0), C(1, 1), C(2, 1))
            };

            return shapes;
        }

        /// <summary>
        /// Extra figures for <see cref="PlayerProgress.ShapesPack1Id"/>. Slight variations of
        /// catalog pieces so the paid tray feels new without changing the free set.
        /// </summary>
        public static List<BlockShape> CreatePack1Shapes()
        {
            return new List<BlockShape>
            {
                BlockShape.Create("Plus", Sky, 0.55f,
                    C(1, 0),
                    C(0, 1), C(1, 1), C(2, 1),
                    C(1, 2)),

                BlockShape.Create("U", Butter, 0.65f,
                    C(0, 0), C(2, 0),
                    C(0, 1), C(1, 1), C(2, 1)),

                BlockShape.Create("T Right", Lavender, 0.7f,
                    C(0, 0),
                    C(0, 1), C(1, 1),
                    C(0, 2)),

                BlockShape.Create("Rect 2x3", Peach, 0.5f,
                    C(0, 0), C(1, 0),
                    C(0, 1), C(1, 1),
                    C(0, 2), C(1, 2))
            };
        }

        private static Vector2Int C(int x, int y) => new Vector2Int(x, y);
    }
}
