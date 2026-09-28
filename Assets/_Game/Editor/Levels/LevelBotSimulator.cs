using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Levels;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.EditorTools
{
    /// <summary>Outcome of <see cref="LevelBotSimulator.Evaluate"/>.</summary>
    public sealed class BotReport
    {
        public int Runs;
        public int Wins;

        /// <summary>True when the simulation stopped early because the required win rate was already out of reach.</summary>
        public bool Aborted;

        public float AverageMovesToWin;

        /// <summary>Moves left (limited level) or final score (unlimited level) of every winning run, ascending.</summary>
        public List<int> WinMetrics = new List<int>();

        public float WinRate => Runs > 0 ? Wins / (float)Runs : 0f;

        public override string ToString()
        {
            return $"{Wins}/{Runs} wins ({WinRate * 100f:0}%), avg {AverageMovesToWin:0.#} moves to win";
        }
    }

    /// <summary>
    /// Plays a level with a greedy bot. The board is kept as a 64-bit mask for speed and mirrored
    /// into a <see cref="GridModel"/> only when the real <see cref="SmartShapeProvider"/> has to
    /// deal a new tray, so the figures are the ones the game itself would hand out for the level's seed.
    /// </summary>
    public static class LevelBotSimulator
    {
        public const int DefaultRuns = 200;

        private const string ShapeLibraryPath = "Assets/_Game/ScriptableObjects/ShapeLibrary.asset";
        private const int SeedStride = 7919;
        private const int MoveCap = 300;

        /// <summary>A level without a move limit keeps being played a while after the goal, to measure the score for stars.</summary>
        private const int ExtraMovesWithoutLimit = 40;

        // Mirrors the ScoreManager defaults, so Score goals mean the same in the simulation and in the game.
        private const int BlockPoints = 10;
        private const int LinePoints = 100;
        private const int ComboStepBonus = 50;
        private const int ComboGraceMoves = 3;
        private const int BoardClearBonus = 3000;

        private const int Size = LevelDefinition.Size;
        private const ulong Col0 = 0x0101010101010101UL;
        private const ulong Col7 = Col0 << 7;
        private const ulong Row0 = 0xFFUL;
        private const ulong Row7 = Row0 << 56;

        private static readonly ulong[] RowMasks = new ulong[Size];
        private static readonly ulong[] ColMasks = new ulong[Size];
        private static readonly Dictionary<BlockShape, ulong[]> PlacementCache = new Dictionary<BlockShape, ulong[]>();

        private static IReadOnlyList<BlockShape> cachedShapes;
        private static BlockShape dot;

        static LevelBotSimulator()
        {
            for (int i = 0; i < Size; i++)
            {
                RowMasks[i] = Row0 << (i * Size);
                ColMasks[i] = Col0 << i;
            }
        }

        /// <summary>
        /// Plays <paramref name="runs"/> games; run i deals figures from <c>shapeSeed + i * stride</c>, so run 0
        /// is exactly what the game deals. When <paramref name="requiredWinRate"/> is positive the simulation
        /// stops as soon as that rate can no longer be reached.
        /// </summary>
        public static BotReport Evaluate(LevelDefinition level, int runs = DefaultRuns, float requiredWinRate = 0f)
        {
            var report = new BotReport { Runs = 0 };
            if (level == null || runs <= 0)
            {
                return report;
            }

            IReadOnlyList<BlockShape> shapes = LoadShapes();
            LevelState start = LevelState.From(level);
            int neededWins = Mathf.CeilToInt(requiredWinRate * runs);
            int losses = 0;
            long movesToWin = 0;

            for (int run = 0; run < runs; run++)
            {
                RunResult result = Play(level, start, shapes, unchecked(level.ShapeSeed + run * SeedStride));
                report.Runs++;

                if (result.Won)
                {
                    report.Wins++;
                    movesToWin += result.MovesToWin;
                    report.WinMetrics.Add(level.HasMoveLimit ? level.MoveLimit - result.MovesToWin : result.FinalScore);
                }
                else
                {
                    losses++;
                    if (requiredWinRate > 0f && runs - losses < neededWins)
                    {
                        report.Aborted = true;
                        break;
                    }
                }
            }

            report.WinMetrics.Sort();
            report.AverageMovesToWin = report.Wins > 0 ? movesToWin / (float)report.Wins : 0f;
            return report;
        }

        private struct LevelState
        {
            public ulong Board;
            public ulong Gems;
            public ulong Marked;

            public static LevelState From(LevelDefinition level)
            {
                var state = new LevelState();
                for (int row = 0; row < Size; row++)
                {
                    for (int col = 0; col < Size; col++)
                    {
                        LevelCellType type = level.GetCell(row, col);
                        if (type == LevelCellType.Empty)
                        {
                            continue;
                        }

                        ulong bit = 1UL << (row * Size + col);
                        state.Board |= bit;
                        if (type == LevelCellType.Crystal)
                        {
                            state.Gems |= bit;
                        }
                        else if (type == LevelCellType.Marked)
                        {
                            state.Marked |= bit;
                        }
                    }
                }

                return state;
            }
        }

        private struct RunResult
        {
            public bool Won;
            public int MovesToWin;
            public int FinalScore;
        }

        private static RunResult Play(LevelDefinition level, LevelState start, IReadOnlyList<BlockShape> shapes, int seed)
        {
            ulong board = start.Board;
            ulong gems = start.Gems;
            ulong marked = start.Marked;

            LevelGoalType goal = level.GoalType;
            int target = level.GoalTarget;
            int moveLimit = level.MoveLimit;

            int score = 0;
            int moves = 0;
            int linesTotal = 0;
            int gemsTotal = 0;
            int combo = 0;
            int dryMoves = 0;
            bool won = false;
            int movesToWin = 0;
            int extraLeft = ExtraMovesWithoutLimit;

            var model = new GridModel(Size);
            var provider = new SmartShapeProvider(shapes, null, () => model, () => score, new SmartShapeSettings(), seed);
            provider.BeginRun(false);

            var tray = new BlockShape[ShapeSpawner.SlotCount];
            var used = new bool[tray.Length];
            Deal(provider, model, board, tray, used);

            while (moves < MoveCap)
            {
                if (moveLimit > 0 && moves >= moveLimit)
                {
                    break;
                }

                if (!FindBestMove(board, gems, marked, goal, tray, used, out int pieceIndex, out ulong placement))
                {
                    break;
                }

                used[pieceIndex] = true;
                moves++;

                int blocks = tray[pieceIndex].BlockCount;
                board |= placement;
                ulong cleared = FindClearedCells(board, out int lines);
                int gained = blocks * BlockPoints;

                if (lines > 0)
                {
                    combo++;
                    dryMoves = 0;
                    gained += lines * lines * LinePoints + (combo - 1) * ComboStepBonus * lines;
                    board &= ~cleared;
                    if (board == 0UL)
                    {
                        gained += BoardClearBonus;
                    }

                    linesTotal += lines;
                    gemsTotal += PopCount(cleared & gems);
                    gems &= ~cleared;
                    marked &= ~cleared;
                }
                else if (++dryMoves >= ComboGraceMoves)
                {
                    combo = 0;
                    dryMoves = 0;
                }

                score += gained;

                if (!won && GoalReached(goal, target, score, gemsTotal, linesTotal, marked))
                {
                    won = true;
                    movesToWin = moves;
                    if (moveLimit > 0)
                    {
                        break;
                    }
                }

                if (won && --extraLeft <= 0)
                {
                    break;
                }

                if (AllUsed(used))
                {
                    Deal(provider, model, board, tray, used);
                }
            }

            return new RunResult { Won = won, MovesToWin = movesToWin, FinalScore = score };
        }

        private static bool GoalReached(LevelGoalType goal, int target, int score, int gemsTotal, int linesTotal, ulong marked)
        {
            switch (goal)
            {
                case LevelGoalType.Gems:
                    return gemsTotal >= target;
                case LevelGoalType.ClearMarked:
                    return marked == 0UL;
                case LevelGoalType.Lines:
                    return linesTotal >= target;
                default:
                    return score >= target;
            }
        }

        private static bool AllUsed(bool[] used)
        {
            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Mirrors the mask into the model the provider reads, then deals a fresh tray.</summary>
        private static void Deal(SmartShapeProvider provider, GridModel model, ulong board, BlockShape[] tray, bool[] used)
        {
            model.Reset();
            BlockShape cell = DotShape();
            for (int bit = 0; bit < Size * Size; bit++)
            {
                if ((board & (1UL << bit)) != 0UL)
                {
                    model.Place(cell, new Vector2Int(bit % Size, bit / Size));
                }
            }

            provider.FillBatch(tray);
            Array.Clear(used, 0, used.Length);
        }

        /// <summary>Greedy choice over every unused figure and every legal spot.</summary>
        private static bool FindBestMove(
            ulong board,
            ulong gems,
            ulong marked,
            LevelGoalType goal,
            BlockShape[] tray,
            bool[] used,
            out int bestPiece,
            out ulong bestPlacement)
        {
            bestPiece = -1;
            bestPlacement = 0UL;
            float bestValue = float.NegativeInfinity;

            for (int i = 0; i < tray.Length; i++)
            {
                if (used[i] || tray[i] == null)
                {
                    continue;
                }

                ulong[] placements = PlacementsOf(tray[i]);
                for (int p = 0; p < placements.Length; p++)
                {
                    ulong placement = placements[p];
                    if ((board & placement) != 0UL)
                    {
                        continue;
                    }

                    float value = ScoreBoard(board | placement, gems, marked, goal);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestPiece = i;
                        bestPlacement = placement;
                    }
                }
            }

            return bestPiece >= 0;
        }

        /// <summary>
        /// Value of a board after a placement: cleared lines and goal progress are rewarded, fill,
        /// single-cell pits are punished, and rows or columns that are nearly full are favoured.
        /// </summary>
        private static float ScoreBoard(ulong board, ulong gems, ulong marked, LevelGoalType goal)
        {
            ulong cleared = FindClearedCells(board, out int lines);
            ulong after = board & ~cleared;
            float value = lines * 25f + lines * lines * 10f;
            ulong targets = 0UL;

            switch (goal)
            {
                case LevelGoalType.Gems:
                    value += PopCount(cleared & gems) * 90f;
                    targets = gems & ~cleared;
                    break;
                case LevelGoalType.ClearMarked:
                    value += PopCount(cleared & marked) * 90f;
                    targets = marked & ~cleared;
                    break;
                case LevelGoalType.Lines:
                    value += lines * 35f;
                    break;
                default:
                    value += lines * 15f;
                    break;
            }

            // Push the rows and columns that still hide a goal cell towards completion.
            while (targets != 0UL)
            {
                ulong bit = targets & (~targets + 1UL);
                targets &= targets - 1UL;
                int index = BitIndex(bit);
                int row = index / Size;
                int col = index % Size;
                int fill = Math.Max(PopCount(after & RowMasks[row]), PopCount(after & ColMasks[col]));
                value += fill * fill * 0.35f;
            }

            for (int line = 0; line < Size; line++)
            {
                int rowFill = PopCount(after & RowMasks[line]);
                int colFill = PopCount(after & ColMasks[line]);
                value += (rowFill * rowFill + colFill * colFill) * 0.3f;
            }

            value -= PopCount(Pits(after)) * 14f;
            value -= PopCount(after) * 0.6f;

            if (after == 0UL)
            {
                value += 200f;
            }

            return value;
        }

        /// <summary>Empty cells whose four neighbours (walls included) are all blocked.</summary>
        private static ulong Pits(ulong board)
        {
            ulong left = ((board << 1) & ~Col0) | Col0;
            ulong right = ((board >> 1) & ~Col7) | Col7;
            ulong up = (board << Size) | Row0;
            ulong down = (board >> Size) | Row7;
            return ~board & left & right & up & down;
        }

        /// <summary>Union of every complete row and column of <paramref name="board"/>.</summary>
        private static ulong FindClearedCells(ulong board, out int lines)
        {
            ulong cleared = 0UL;
            lines = 0;
            for (int i = 0; i < Size; i++)
            {
                if ((board & RowMasks[i]) == RowMasks[i])
                {
                    cleared |= RowMasks[i];
                    lines++;
                }

                if ((board & ColMasks[i]) == ColMasks[i])
                {
                    cleared |= ColMasks[i];
                    lines++;
                }
            }

            return cleared;
        }

        private static ulong[] PlacementsOf(BlockShape shape)
        {
            if (PlacementCache.TryGetValue(shape, out ulong[] cached) && cached != null)
            {
                return cached;
            }

            int maxRow = Size - shape.Height;
            int maxCol = Size - shape.Width;
            var list = new List<ulong>();
            IReadOnlyList<Vector2Int> cells = shape.Cells;

            for (int row = 0; row <= maxRow; row++)
            {
                for (int col = 0; col <= maxCol; col++)
                {
                    ulong mask = 0UL;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        mask |= 1UL << ((row + cells[c].y) * Size + col + cells[c].x);
                    }

                    list.Add(mask);
                }
            }

            ulong[] result = list.ToArray();
            PlacementCache[shape] = result;
            return result;
        }

        private static IReadOnlyList<BlockShape> LoadShapes()
        {
            if (cachedShapes != null && cachedShapes.Count > 0 && cachedShapes[0] != null)
            {
                return cachedShapes;
            }

            PlacementCache.Clear();
            var library = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(ShapeLibraryPath);
            cachedShapes = library != null ? library.Shapes : ShapeCatalog.CreateDefaultShapes();
            return cachedShapes;
        }

        private static BlockShape DotShape()
        {
            if (dot == null)
            {
                dot = BlockShape.Create("Dot", Color.white, 1f, Vector2Int.zero);
                dot.hideFlags = HideFlags.HideAndDontSave;
            }

            return dot;
        }

        private static int BitIndex(ulong singleBit)
        {
            int index = 0;
            while ((singleBit >>= 1) != 0UL)
            {
                index++;
            }

            return index;
        }

        private static int PopCount(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }
    }
}
