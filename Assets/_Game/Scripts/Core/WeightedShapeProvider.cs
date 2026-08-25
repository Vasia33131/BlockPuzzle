using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Picks figures at random using <see cref="BlockShape.Weight"/>, so large or awkward
    /// figures appear less often than small ones. The paid pack is mixed in only while
    /// <see cref="PlayerProgress"/> owns <see cref="PlayerProgress.ShapesPack1Id"/>.
    /// </summary>
    public sealed class WeightedShapeProvider : IShapeProvider
    {
        private readonly IReadOnlyList<BlockShape> shapes;
        private readonly IReadOnlyList<BlockShape> pack1;
        private readonly System.Random random;
        private readonly float shapesWeight;
        private readonly float pack1Weight;

        public WeightedShapeProvider(IReadOnlyList<BlockShape> shapes, int seed)
            : this(shapes, null, seed)
        {
        }

        public WeightedShapeProvider(IReadOnlyList<BlockShape> shapes, IReadOnlyList<BlockShape> pack1, int seed)
        {
            if (shapes == null || shapes.Count == 0)
            {
                throw new ArgumentException("Shape provider requires at least one shape.", nameof(shapes));
            }

            this.shapes = shapes;
            this.pack1 = pack1;
            random = new System.Random(seed);
            shapesWeight = SumWeight(shapes);
            pack1Weight = SumWeight(pack1);
        }

        public BlockShape Next()
        {
            bool includePack = pack1 != null
                && pack1.Count > 0
                && pack1Weight > 0f
                && PlayerProgress.OwnsPack(PlayerProgress.ShapesPack1Id);

            float total = shapesWeight + (includePack ? pack1Weight : 0f);
            if (total <= 0f)
            {
                return shapes[shapes.Count - 1];
            }

            double roll = random.NextDouble() * total;
            BlockShape picked = Pick(shapes, ref roll);
            if (picked != null)
            {
                return picked;
            }

            if (includePack)
            {
                picked = Pick(pack1, ref roll);
                if (picked != null)
                {
                    return picked;
                }
            }

            return shapes[shapes.Count - 1];
        }

        /// <summary>Convenience factory that seeds the generator from the current time.</summary>
        public static WeightedShapeProvider FromLibrary(ShapeLibrary library)
        {
            IReadOnlyList<BlockShape> source = library != null ? library.Shapes : ShapeCatalog.CreateDefaultShapes();
            IReadOnlyList<BlockShape> extra = library != null ? library.Pack1 : ShapeCatalog.CreatePack1Shapes();
            return new WeightedShapeProvider(
                source,
                extra,
                Environment.TickCount ^ UnityEngine.Random.Range(0, int.MaxValue));
        }

        private static float SumWeight(IReadOnlyList<BlockShape> list)
        {
            if (list == null || list.Count == 0)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    sum += list[i].Weight;
                }
            }

            return sum;
        }

        private static BlockShape Pick(IReadOnlyList<BlockShape> list, ref double roll)
        {
            if (list == null)
            {
                return null;
            }

            for (int i = 0; i < list.Count; i++)
            {
                BlockShape shape = list[i];
                if (shape == null)
                {
                    continue;
                }

                roll -= shape.Weight;
                if (roll <= 0d)
                {
                    return shape;
                }
            }

            return null;
        }
    }
}
