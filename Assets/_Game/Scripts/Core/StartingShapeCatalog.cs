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
            return new List<BlockShape>
            {
                BlockShape.CreateStartingFromMatrix("Start I 4x1", 1f, new[,]
                {
                    { X, X, X, X }
                }),

                BlockShape.CreateStartingFromMatrix("Start I 1x4", 1f, new[,]
                {
                    { X },
                    { X },
                    { X },
                    { X }
                }),

                BlockShape.CreateStartingFromMatrix("Start O 2x2", 1f, new[,]
                {
                    { X, X },
                    { X, X }
                }),

                BlockShape.CreateStartingFromMatrix("Start T", 1f, new[,]
                {
                    { X, X, X },
                    { _, X, _ }
                }),

                BlockShape.CreateStartingFromMatrix("Start L", 1f, new[,]
                {
                    { X, _, _ },
                    { X, X, X }
                }),

                BlockShape.CreateStartingFromMatrix("Start J", 1f, new[,]
                {
                    { _, _, X },
                    { X, X, X }
                }),

                BlockShape.CreateStartingFromMatrix("Start S", 1f, new[,]
                {
                    { _, X, X },
                    { X, X, _ }
                }),

                BlockShape.CreateStartingFromMatrix("Start Z", 1f, new[,]
                {
                    { X, X, _ },
                    { _, X, X }
                }),

                BlockShape.CreateStartingFromMatrix("Start Single", 1f, new[,]
                {
                    { X }
                }),

                BlockShape.CreateStartingFromMatrix("Start Bar 2x1", 1f, new[,]
                {
                    { X, X }
                }),

                BlockShape.CreateStartingFromMatrix("Start Bar 1x2", 1f, new[,]
                {
                    { X },
                    { X }
                }),

                BlockShape.CreateStartingFromMatrix("Start Bar 3x1", 1f, new[,]
                {
                    { X, X, X }
                }),

                BlockShape.CreateStartingFromMatrix("Start Bar 1x3", 1f, new[,]
                {
                    { X },
                    { X },
                    { X }
                })
            };
        }
    }
}
