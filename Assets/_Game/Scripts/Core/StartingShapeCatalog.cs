using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Figures used to pre-fill the board at the beginning of a run. They are described as
    /// occupancy matrices so the silhouette is readable in code, and they are painted in the
    /// dim board colour because they are part of the playfield, not draggable pieces.
    /// </summary>
    public static class StartingShapeCatalog
    {
        private const bool X = true;
        private const bool _ = false;

        public static List<BlockShape> CreateStartingShapes()
        {
            Color color = GameTheme.StartingBlock;

            return new List<BlockShape>
            {
                BlockShape.CreateFromMatrix("Start I 4x1", color, 1f, new[,]
                {
                    { X, X, X, X }
                }),

                BlockShape.CreateFromMatrix("Start I 1x4", color, 1f, new[,]
                {
                    { X },
                    { X },
                    { X },
                    { X }
                }),

                BlockShape.CreateFromMatrix("Start O 2x2", color, 1f, new[,]
                {
                    { X, X },
                    { X, X }
                }),

                BlockShape.CreateFromMatrix("Start T", color, 1f, new[,]
                {
                    { X, X, X },
                    { _, X, _ }
                }),

                BlockShape.CreateFromMatrix("Start L", color, 1f, new[,]
                {
                    { X, _, _ },
                    { X, X, X }
                }),

                BlockShape.CreateFromMatrix("Start J", color, 1f, new[,]
                {
                    { _, _, X },
                    { X, X, X }
                }),

                BlockShape.CreateFromMatrix("Start S", color, 1f, new[,]
                {
                    { _, X, X },
                    { X, X, _ }
                }),

                BlockShape.CreateFromMatrix("Start Z", color, 1f, new[,]
                {
                    { X, X, _ },
                    { _, X, X }
                }),

                BlockShape.CreateFromMatrix("Start Single", color, 1f, new[,]
                {
                    { X }
                }),

                BlockShape.CreateFromMatrix("Start Bar 2x1", color, 1f, new[,]
                {
                    { X, X }
                }),

                BlockShape.CreateFromMatrix("Start Bar 1x2", color, 1f, new[,]
                {
                    { X },
                    { X }
                }),

                BlockShape.CreateFromMatrix("Start Bar 3x1", color, 1f, new[,]
                {
                    { X, X, X }
                }),

                BlockShape.CreateFromMatrix("Start Bar 1x3", color, 1f, new[,]
                {
                    { X },
                    { X },
                    { X }
                })
            };
        }
    }
}
