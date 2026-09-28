using System;
using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;

namespace BlockPuzzle.Pieces
{
    /// <summary>Tuning of <see cref="SmartShapeProvider"/>, exposed on the spawner in the inspector.</summary>
    [Serializable]
    public sealed class SmartShapeSettings
    {
        [Header("Move Guarantee")]
        [Tooltip("How many times a batch with no placeable figure is redrawn before the smallest fitting figure is forced in.")]
        [SerializeField, Min(0)] private int maxRerolls = 20;

        [Header("Line Assist")]
        [Tooltip("Board fill (0..1) above which the provider starts helping the player finish lines.")]
        [SerializeField, Range(0f, 1f)] private float assistFillThreshold = 0.55f;

        [Tooltip("Chance that a batch dealt on a crowded board contains a figure that completes a line.")]
        [SerializeField, Range(0f, 1f)] private float assistChance = 0.65f;

        [Header("Onboarding")]
        [Tooltip("Batches of the very first run that contain only easy figures.")]
        [SerializeField, Min(0)] private int onboardingBatches = 3;

        [Header("Difficulty Ramp")]
        [Tooltip("Extra weight of large figures gained every Score Step points (0.5 = +50%).")]
        [SerializeField, Min(0f)] private float largeWeightBonusPerStep = 0.5f;

        // Named for the x10 score economy so a value saved under the old 1000 step is not picked up.
        [SerializeField, Min(1)] private int scoreStepPoints = 10000;

        [Tooltip("Upper bound of the extra weight, so late runs do not turn into a wall of 3x3 squares.")]
        [SerializeField, Min(0f)] private float maxLargeWeightBonus = 1.5f;

        public int MaxRerolls => maxRerolls;
        public float AssistFillThreshold => assistFillThreshold;
        public float AssistChance => assistChance;
        public int OnboardingBatches => onboardingBatches;
        public float LargeWeightBonusPerStep => largeWeightBonusPerStep;
        public int ScoreStep => Mathf.Max(1, scoreStepPoints);
        public float MaxLargeWeightBonus => maxLargeWeightBonus;
    }

    /// <summary>
    /// Board-aware figure dealer. On top of the weighted draw of <see cref="WeightedShapeProvider"/>
    /// it guarantees that every batch has a legal move, sometimes hands out a line-finishing figure
    /// on a crowded board, keeps the first batches of a new player easy and lets large figures
    /// become more common as the score grows.
    ///
    /// The board is read as a 64-bit mask (bit = row * size + col), and every figure keeps the
    /// masks of all its placements on an empty board, so "does it fit" is one AND and "does it
    /// finish a line" is one AND + compare per row and column.
    /// </summary>
    public sealed class SmartShapeProvider : IShapeProvider
    {
        /// <summary>A bigger board no longer fits in a ulong; placement falls back to GridModel.</summary>
        private const int MaxBitboardSize = 8;

        private sealed class Entry
        {
            public BlockShape Shape;
            public bool IsEasy;
            public bool IsLarge;
            public ulong[] Placements;

            // Refreshed for every draw.
            public float Weight;
            public bool Fits;
            public int MaxLines;
        }

        private readonly IReadOnlyList<BlockShape> shapes;
        private readonly IReadOnlyList<BlockShape> pack1;
        private readonly Func<GridModel> boardSource;
        private readonly Func<int> scoreSource;
        private readonly SmartShapeSettings settings;
        private readonly System.Random random;

        private readonly Dictionary<BlockShape, Entry> entries = new Dictionary<BlockShape, Entry>();
        private readonly List<Entry> pool = new List<Entry>();
        private Entry[] drawn = new Entry[ShapeSpawner.SlotCount];
        private ulong[] lineMasks;
        private int maskSize = -1;
        private float fillRatio;

        private bool onboardingRun;
        private int batchesDealt;

        public SmartShapeProvider(
            IReadOnlyList<BlockShape> shapes,
            IReadOnlyList<BlockShape> pack1,
            Func<GridModel> boardSource,
            Func<int> scoreSource,
            SmartShapeSettings settings,
            int seed)
        {
            if (shapes == null || shapes.Count == 0)
            {
                throw new ArgumentException("Shape provider requires at least one shape.", nameof(shapes));
            }

            this.shapes = shapes;
            this.pack1 = pack1;
            this.boardSource = boardSource;
            this.scoreSource = scoreSource;
            this.settings = settings ?? new SmartShapeSettings();
            random = new System.Random(seed);
        }

        /// <summary>
        /// False in a level: the paid figure pack must not change what a level deals, or the same
        /// seed would give a different game to a player who owns the pack.
        /// </summary>
        public bool IncludePaidPack { get; set; } = true;

        /// <summary>True while the current batch is still one of the easy onboarding batches.</summary>
        public bool IsOnboarding => onboardingRun && batchesDealt < settings.OnboardingBatches;

        /// <summary>Starts counting batches of a new run. <paramref name="firstRun"/> enables onboarding.</summary>
        public void BeginRun(bool firstRun)
        {
            onboardingRun = firstRun;
            batchesDealt = 0;
        }

        /// <summary>Single weighted figure that ignores the board. Kept for <see cref="IShapeProvider"/>.</summary>
        public BlockShape Next()
        {
            RefreshPool(analyseBoard: false);
            Entry picked = PickWeighted(requireFit: false, requireClear: false);
            return picked != null ? picked.Shape : shapes[shapes.Count - 1];
        }

        /// <summary>
        /// Deals a full tray into <paramref name="output"/> for the board as it is right now.
        /// At least one figure fits whenever any figure of the pool fits at all.
        /// </summary>
        public void FillBatch(BlockShape[] output)
        {
            if (output == null || output.Length == 0)
            {
                return;
            }

            int count = output.Length;
            if (drawn.Length != count)
            {
                drawn = new Entry[count];
            }

            RefreshPool(analyseBoard: true);
            bool anyFits = AnyFits();

            // 1. Plain weighted draw, redrawn while nothing of it can be placed.
            DrawBatch(count);
            for (int attempt = 0; anyFits && !BatchFits(count) && attempt < settings.MaxRerolls; attempt++)
            {
                DrawBatch(count);
            }

            // 2. Still stuck: swap a random slot for the smallest figure that fits.
            if (anyFits && !BatchFits(count))
            {
                drawn[random.Next(count)] = SmallestFitting();
            }

            // 3. Crowded board: sometimes make sure one figure finishes a line.
            if (fillRatio > settings.AssistFillThreshold
                && !BatchClears(count)
                && random.NextDouble() < settings.AssistChance)
            {
                Entry clearer = PickWeighted(requireFit: true, requireClear: true);
                if (clearer != null)
                {
                    // The clearer fits by definition, so the move guarantee survives the swap.
                    drawn[random.Next(count)] = clearer;
                }
            }

            for (int i = 0; i < count; i++)
            {
                output[i] = drawn[i] != null ? drawn[i].Shape : shapes[0];
                drawn[i] = null;
            }

            batchesDealt++;
        }

        /// <summary>A figure that fits the current board (extra-piece booster). Null when nothing fits.</summary>
        public BlockShape NextFitting()
        {
            RefreshPool(analyseBoard: true);
            Entry picked = PickWeighted(requireFit: true, requireClear: false) ?? SmallestFitting();
            return picked?.Shape;
        }

        /// <summary>
        /// Rebuilds the list of drawable figures (the paid pack only while it is owned), applies the
        /// score ramp and onboarding to their weights and, when asked, measures each one against the board.
        /// </summary>
        private void RefreshPool(bool analyseBoard)
        {
            pool.Clear();
            AddToPool(shapes);
            if (IncludePaidPack && PlayerProgress.OwnsPack(PlayerProgress.ShapesPack1Id))
            {
                AddToPool(pack1);
            }

            float largeFactor = 1f + LargeWeightBonus();
            bool onboarding = IsOnboarding && PoolHasEasy();

            for (int i = 0; i < pool.Count; i++)
            {
                Entry entry = pool[i];
                entry.Weight = onboarding && !entry.IsEasy
                    ? 0f
                    : entry.Shape.Weight * (entry.IsLarge ? largeFactor : 1f);
                entry.Fits = true;
                entry.MaxLines = 0;
            }

            if (analyseBoard)
            {
                AnalyseBoard();
            }
        }

        /// <summary>An authored library without easy figures simply skips onboarding.</summary>
        private bool PoolHasEasy()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].IsEasy)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddToPool(IReadOnlyList<BlockShape> list)
        {
            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                BlockShape shape = list[i];
                if (shape == null)
                {
                    continue;
                }

                if (!entries.TryGetValue(shape, out Entry entry))
                {
                    entry = new Entry
                    {
                        Shape = shape,
                        IsEasy = IsEasyShape(shape),
                        IsLarge = IsLargeShape(shape)
                    };
                    entries.Add(shape, entry);
                }

                pool.Add(entry);
            }
        }

        /// <summary>
        /// Linear in the score and capped: with the defaults a large figure is 1.5x as likely at
        /// 1000 points, 2x at 2000 and never more than 2.5x.
        /// </summary>
        private float LargeWeightBonus()
        {
            int score = scoreSource != null ? Mathf.Max(0, scoreSource()) : 0;
            float bonus = settings.LargeWeightBonusPerStep * score / settings.ScoreStep;
            return Mathf.Min(bonus, settings.MaxLargeWeightBonus);
        }

        private void AnalyseBoard()
        {
            GridModel model = boardSource?.Invoke();
            fillRatio = 0f;
            if (model == null)
            {
                return;
            }

            int size = model.Size;
            if (size > MaxBitboardSize)
            {
                AnalyseWithoutBitboard(model);
                return;
            }

            EnsureMasks(size);
            ulong occupied = ReadBoard(model, size, out int filled);
            fillRatio = filled / (float)(size * size);

            // Counting lines is only needed when the assist can kick in.
            bool countLines = fillRatio > settings.AssistFillThreshold;

            for (int i = 0; i < pool.Count; i++)
            {
                Entry entry = pool[i];
                ulong[] placements = entry.Placements;
                entry.Fits = false;

                for (int p = 0; p < placements.Length; p++)
                {
                    ulong placement = placements[p];
                    if ((placement & occupied) != 0UL)
                    {
                        continue;
                    }

                    entry.Fits = true;
                    if (!countLines)
                    {
                        break;
                    }

                    int lines = CountCompleteLines(occupied | placement);
                    if (lines > entry.MaxLines)
                    {
                        entry.MaxLines = lines;
                    }
                }
            }
        }

        private void AnalyseWithoutBitboard(GridModel model)
        {
            fillRatio = model.OccupiedCount / (float)(model.Size * model.Size);
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].Fits = model.HasPlacementFor(pool[i].Shape);
            }
        }

        private static ulong ReadBoard(GridModel model, int size, out int filled)
        {
            ulong occupied = 0UL;
            filled = 0;

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (model.IsOccupied(row, col))
                    {
                        occupied |= 1UL << (row * size + col);
                        filled++;
                    }
                }
            }

            return occupied;
        }

        private int CountCompleteLines(ulong board)
        {
            int lines = 0;
            for (int i = 0; i < lineMasks.Length; i++)
            {
                ulong mask = lineMasks[i];
                if ((board & mask) == mask)
                {
                    lines++;
                }
            }

            return lines;
        }

        /// <summary>Row and column masks plus every figure's placement masks, rebuilt only when the board size changes.</summary>
        private void EnsureMasks(int size)
        {
            if (maskSize != size)
            {
                maskSize = size;
                lineMasks = new ulong[size * 2];
                ulong rowBits = (1UL << size) - 1UL;

                for (int i = 0; i < size; i++)
                {
                    lineMasks[i] = rowBits << (i * size);

                    ulong column = 0UL;
                    for (int row = 0; row < size; row++)
                    {
                        column |= 1UL << (row * size + i);
                    }

                    lineMasks[size + i] = column;
                }

                foreach (Entry entry in entries.Values)
                {
                    entry.Placements = null;
                }
            }

            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].Placements ??= BuildPlacements(pool[i].Shape, size);
            }
        }

        private static ulong[] BuildPlacements(BlockShape shape, int size)
        {
            int maxRow = size - shape.Height;
            int maxCol = size - shape.Width;
            if (maxRow < 0 || maxCol < 0)
            {
                return Array.Empty<ulong>();
            }

            IReadOnlyList<Vector2Int> cells = shape.Cells;
            var placements = new ulong[(maxRow + 1) * (maxCol + 1)];
            int index = 0;

            for (int row = 0; row <= maxRow; row++)
            {
                for (int col = 0; col <= maxCol; col++)
                {
                    ulong mask = 0UL;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        mask |= 1UL << ((row + cells[c].y) * size + col + cells[c].x);
                    }

                    placements[index++] = mask;
                }
            }

            return placements;
        }

        private void DrawBatch(int count)
        {
            for (int i = 0; i < count; i++)
            {
                drawn[i] = PickWeighted(requireFit: false, requireClear: false) ?? pool[pool.Count - 1];
            }
        }

        private bool AnyFits()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Fits)
                {
                    return true;
                }
            }

            return false;
        }

        private bool BatchFits(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (drawn[i] != null && drawn[i].Fits)
                {
                    return true;
                }
            }

            return false;
        }

        private bool BatchClears(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (drawn[i] != null && drawn[i].MaxLines > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Weighted roulette over the pool. A line-clearing pick is additionally weighted by how
        /// many lines the figure can finish, so a double clear is favoured over a single one.
        /// </summary>
        private Entry PickWeighted(bool requireFit, bool requireClear)
        {
            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                total += PickWeight(pool[i], requireFit, requireClear);
            }

            if (total <= 0f)
            {
                return null;
            }

            double roll = random.NextDouble() * total;
            Entry last = null;
            for (int i = 0; i < pool.Count; i++)
            {
                float weight = PickWeight(pool[i], requireFit, requireClear);
                if (weight <= 0f)
                {
                    continue;
                }

                last = pool[i];
                roll -= weight;
                if (roll <= 0d)
                {
                    return last;
                }
            }

            return last;
        }

        private static float PickWeight(Entry entry, bool requireFit, bool requireClear)
        {
            if ((requireFit && !entry.Fits) || (requireClear && entry.MaxLines <= 0))
            {
                return 0f;
            }

            return requireClear ? entry.Weight * entry.MaxLines : entry.Weight;
        }

        /// <summary>
        /// Fewest blocks among the figures that fit; ties go to the more common figure. Onboarding
        /// is ignored here on purpose: a legal move matters more than the easy-only rule.
        /// </summary>
        private Entry SmallestFitting()
        {
            Entry best = null;
            for (int i = 0; i < pool.Count; i++)
            {
                Entry entry = pool[i];
                if (!entry.Fits)
                {
                    continue;
                }

                if (best == null
                    || entry.Shape.BlockCount < best.Shape.BlockCount
                    || (entry.Shape.BlockCount == best.Shape.BlockCount && entry.Shape.Weight > best.Shape.Weight))
                {
                    best = entry;
                }
            }

            return best;
        }

        /// <summary>Single, domino, corner, 2x2 square and straight line of three.</summary>
        private static bool IsEasyShape(BlockShape shape)
        {
            if (shape.Width <= 2 && shape.Height <= 2)
            {
                return true;
            }

            return (shape.Width == 1 || shape.Height == 1) && shape.BlockCount <= 3;
        }

        /// <summary>Five blocks or more (Line5, L, 3x3 square, pack figures) and the S/Z skews.</summary>
        private static bool IsLargeShape(BlockShape shape)
        {
            return shape.BlockCount >= 5 || IsSkew(shape);
        }

        /// <summary>Four blocks in a 3x2 box split two and two along the long side: S or Z.</summary>
        private static bool IsSkew(BlockShape shape)
        {
            if (shape.BlockCount != 4)
            {
                return false;
            }

            bool wide = shape.Width == 3 && shape.Height == 2;
            bool tall = shape.Width == 2 && shape.Height == 3;
            if (!wide && !tall)
            {
                return false;
            }

            int first = 0;
            IReadOnlyList<Vector2Int> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                if ((wide ? cells[i].y : cells[i].x) == 0)
                {
                    first++;
                }
            }

            return first == 2;
        }
    }
}
