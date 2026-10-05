using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.Levels
{
    /// <summary>
    /// Authored data of one campaign level: the goal, the move limit, the prefilled board and the
    /// figure seed. Pure data — playing a level is not part of this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Block Puzzle/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public const int Size = GameTheme.GridSize;
        public const int CellCount = Size * Size;

        [SerializeField, Min(1)] private int number = 1;

        [Tooltip("Every 5th level: a bigger coin reward.")]
        [SerializeField] private bool isSpecial;

        [SerializeField] private LevelGoalType goalType = LevelGoalType.Score;

        [Tooltip("Points, crystals or lines to collect. Ignored for ClearMarked: the marked cells are the target.")]
        [SerializeField, Min(0)] private int goalTarget = 500;

        [Tooltip("Figures the player may place. 0 means no limit.")]
        [SerializeField, Min(0)] private int moveLimit;

        [Tooltip("Prefilled board, row-major (index = row * 8 + column), row 0 at the top.")]
        [SerializeField] private LevelCellType[] cells = new LevelCellType[CellCount];

        [Tooltip("Seed of the figure dealer, so a level always deals the same figures.")]
        [SerializeField] private int shapeSeed = 1;

        [SerializeField, Range(0f, 1f)] private float difficulty;

        [Tooltip("Stars beyond the first: moves left (level with a move limit) or points (level without one).")]
        [SerializeField, Min(0)] private int twoStarThreshold;

        [SerializeField, Min(0)] private int threeStarThreshold;

        [SerializeField, Min(0)] private int coinReward = 10;

        public int Number => number;
        public bool IsSpecial => isSpecial;
        public LevelGoalType GoalType => goalType;
        public int MoveLimit => moveLimit;
        public bool HasMoveLimit => moveLimit > 0;
        public int ShapeSeed => shapeSeed;
        public float Difficulty => difficulty;
        public int CoinReward => coinReward;
        public int TwoStarThreshold => twoStarThreshold;
        public int ThreeStarThreshold => threeStarThreshold;

        /// <summary>True when stars are decided by the moves left; false when by the final score.</summary>
        public bool StarsUseMovesLeft => moveLimit > 0;

        /// <summary>The number to reach. For ClearMarked this is the count of marked cells.</summary>
        public int GoalTarget => goalType == LevelGoalType.ClearMarked ? CountCells(LevelCellType.Marked) : goalTarget;

        public LevelCellType GetCell(int row, int col)
        {
            EnsureCells();
            return row >= 0 && row < Size && col >= 0 && col < Size ? cells[row * Size + col] : LevelCellType.Empty;
        }

        public void SetCell(int row, int col, LevelCellType type)
        {
            EnsureCells();
            if (row >= 0 && row < Size && col >= 0 && col < Size)
            {
                cells[row * Size + col] = type;
            }
        }

        /// <summary>Number of cells of exactly this type.</summary>
        public int CountCells(LevelCellType type)
        {
            EnsureCells();
            int count = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == type)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Number of occupied cells (everything except Empty).</summary>
        public int CountOccupied() => CellCount - CountCells(LevelCellType.Empty);

        /// <summary>
        /// Stars (2..3) for a finished level. A win always gives at least two stars. Levels with a
        /// move limit give three when at least one move is left; levels without one give three for
        /// any win, since the level ends as soon as the goal is reached and the score cannot run
        /// far past it. The authored thresholds only make it easier, never harder.
        /// </summary>
        public int GetStars(int movesLeft, int score)
        {
            if (!StarsUseMovesLeft)
            {
                return 3;
            }

            int three = threeStarThreshold > 0 ? Mathf.Min(threeStarThreshold, 1) : 0;
            return movesLeft >= three ? 3 : 2;
        }

        /// <summary>Human-readable problems of this level; empty when it is consistent.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            EnsureCells();

            if (goalType == LevelGoalType.ClearMarked && CountCells(LevelCellType.Marked) == 0)
            {
                problems.Add("ClearMarked needs at least one marked cell.");
            }

            if (goalType != LevelGoalType.ClearMarked && goalTarget <= 0)
            {
                problems.Add("Goal target must be above zero.");
            }

            if (goalType == LevelGoalType.Gems && CountCells(LevelCellType.Crystal) < goalTarget)
            {
                problems.Add("Fewer crystals on the board than the goal asks for.");
            }

            if (goalType != LevelGoalType.Gems && CountCells(LevelCellType.Crystal) > 0)
            {
                problems.Add("Crystals are only used by Gems levels.");
            }

            if (goalType != LevelGoalType.ClearMarked && CountCells(LevelCellType.Marked) > 0)
            {
                problems.Add("Marked cells are only used by ClearMarked levels.");
            }

            if (HasCompleteLine())
            {
                problems.Add("The prefilled board already contains a complete row or column.");
            }

            if (threeStarThreshold > 0 && threeStarThreshold <= twoStarThreshold)
            {
                problems.Add("Three-star threshold must be above the two-star threshold.");
            }

            return problems;
        }

        /// <summary>Overwrites every field with the values of <paramref name="source"/>, keeping this asset's identity.</summary>
        public void CopyFrom(LevelDefinition source)
        {
            if (source == null)
            {
                return;
            }

            source.EnsureCells();
            number = source.number;
            isSpecial = source.isSpecial;
            goalType = source.goalType;
            goalTarget = source.goalTarget;
            moveLimit = source.moveLimit;
            cells = (LevelCellType[])source.cells.Clone();
            shapeSeed = source.shapeSeed;
            difficulty = source.difficulty;
            twoStarThreshold = source.twoStarThreshold;
            threeStarThreshold = source.threeStarThreshold;
            coinReward = source.coinReward;
        }

        /// <summary>Authoring entry point for the level generator and the level editor.</summary>
        public void Configure(
            int levelNumber,
            bool special,
            LevelGoalType goal,
            int target,
            int moves,
            int seed,
            float levelDifficulty,
            int reward)
        {
            number = Mathf.Max(1, levelNumber);
            isSpecial = special;
            goalType = goal;
            goalTarget = Mathf.Max(0, target);
            moveLimit = Mathf.Max(0, moves);
            shapeSeed = seed;
            difficulty = Mathf.Clamp01(levelDifficulty);
            coinReward = Mathf.Max(0, reward);
        }

        public void SetStarThresholds(int two, int three)
        {
            twoStarThreshold = Mathf.Max(0, two);
            threeStarThreshold = Mathf.Max(0, three);
        }

        public void SetCells(IReadOnlyList<LevelCellType> source)
        {
            EnsureCells();
            for (int i = 0; i < CellCount; i++)
            {
                cells[i] = source != null && i < source.Count ? source[i] : LevelCellType.Empty;
            }
        }

        private bool HasCompleteLine()
        {
            for (int line = 0; line < Size; line++)
            {
                bool row = true;
                bool column = true;
                for (int i = 0; i < Size; i++)
                {
                    row &= cells[line * Size + i] != LevelCellType.Empty;
                    column &= cells[i * Size + line] != LevelCellType.Empty;
                }

                if (row || column)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureCells()
        {
            if (cells != null && cells.Length == CellCount)
            {
                return;
            }

            var resized = new LevelCellType[CellCount];
            if (cells != null)
            {
                System.Array.Copy(cells, resized, Mathf.Min(cells.Length, CellCount));
            }

            cells = resized;
        }

        private void OnValidate()
        {
            EnsureCells();
        }
    }
}
