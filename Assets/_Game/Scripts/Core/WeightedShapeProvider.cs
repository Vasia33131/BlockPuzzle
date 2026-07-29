using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Picks figures at random using <see cref="BlockShape.Weight"/>, so large or awkward
    /// figures appear less often than small ones.
    /// </summary>
    public sealed class WeightedShapeProvider : IShapeProvider
    {
        private readonly IReadOnlyList<BlockShape> shapes;
        private readonly System.Random random;
        private readonly float totalWeight;

        public WeightedShapeProvider(IReadOnlyList<BlockShape> shapes, int seed)
        {
            if (shapes == null || shapes.Count == 0)
            {
                throw new ArgumentException("Shape provider requires at least one shape.", nameof(shapes));
            }

            this.shapes = shapes;
            random = new System.Random(seed);

            foreach (BlockShape shape in shapes)
            {
                totalWeight += shape.Weight;
            }
        }

        public BlockShape Next()
        {
            double roll = random.NextDouble() * totalWeight;
            foreach (BlockShape shape in shapes)
            {
                roll -= shape.Weight;
                if (roll <= 0d)
                {
                    return shape;
                }
            }

            return shapes[shapes.Count - 1];
        }

        /// <summary>Convenience factory that seeds the generator from the current time.</summary>
        public static WeightedShapeProvider FromLibrary(ShapeLibrary library)
        {
            IReadOnlyList<BlockShape> source = library != null ? library.Shapes : ShapeCatalog.CreateDefaultShapes();
            return new WeightedShapeProvider(source, Environment.TickCount ^ UnityEngine.Random.Range(0, int.MaxValue));
        }
    }
}
