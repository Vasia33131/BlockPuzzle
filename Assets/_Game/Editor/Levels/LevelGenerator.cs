using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using BlockPuzzle.Levels;
using Random = System.Random;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Builds the 100 campaign levels from a difficulty curve and proves with <see cref="LevelBotSimulator"/>
    /// that each one can be won. Every level starts from a guess, is played 200 times by the bot and is then
    /// eased or tightened by one knob (<c>ease</c>: more moves and a lower target, or fewer and a higher one)
    /// until its win rate lands between the minimum for its number and a ceiling that falls as the level gets harder.
    /// </summary>
    public static class LevelGenerator
    {
        public const string LevelsFolder = "Assets/_Game/Resources/Levels";
        public const string DatabasePath = LevelsFolder + "/LevelDatabase.asset";

        private const string LogTag = "[LevelGen]";
        private const int Size = LevelDefinition.Size;

        /// <summary>Levels up to this number must be won by the bot at least <see cref="EarlyMinWinRate"/> of the time.</summary>
        private const int EarlyLevelCount = 20;

        private const float EarlyMinWinRate = 0.6f;
        private const float MinWinRate = 0.3f;

        private const int LayoutAttempts = 3;
        private const int MaxTuneSteps = 14;
        private const float EaseStepUp = 0.15f;
        private const float EaseStepDown = 0.1f;
        private const float MinEase = -0.7f;
        private const float MaxEase = 3f;
        private const int MaxMoveLimit = 90;
        private const int MaxBlocksPerLine = 5;

        private sealed class Plan
        {
            public int Number;
            public LevelGoalType Goal;
            public float Difficulty;
            public bool Special;
            public int Reward;
            public int Seed;
            public LevelCellType[] Cells;
            public int BaseMoves;
            public int BaseTarget;
            public int MinTarget;
        }

        /// <summary>Result of generating one level. The caller owns <see cref="Level"/> and must destroy it.</summary>
        public sealed class Result
        {
            public LevelDefinition Level;
            public BotReport Report;
            public bool Passed;
            public string Line;
        }

        [MenuItem("Tools/Block Puzzle/Generate 100 Levels")]
        private static void GenerateAllMenu()
        {
            if (EditorUtility.DisplayDialog(
                    "Generate 100 levels",
                    "Rebuilds every level asset in " + LevelsFolder + " and checks each one with the bot. Hand-made edits to those levels are lost. It takes a few minutes.",
                    "Generate",
                    "Cancel"))
            {
                GenerateAll();
            }
        }

        /// <summary>Command-line entry point: <c>-executeMethod BlockPuzzle.EditorTools.LevelGenerator.GenerateAllBatch</c>.</summary>
        public static void GenerateAllBatch()
        {
            GenerateAll();
        }

        /// <summary>Generates levels 1..100, writes them as assets and fills the <see cref="LevelDatabase"/>.</summary>
        public static LevelDatabase GenerateAll()
        {
            EnsureFolder();
            var assets = new List<LevelDefinition>();
            var weak = new List<string>();
            int passed = 0;
            bool cancelled = false;

            try
            {
                for (int number = 1; number <= LevelDatabase.LevelCount; number++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Generating levels", $"Level {number} / {LevelDatabase.LevelCount}", number / (float)LevelDatabase.LevelCount))
                    {
                        cancelled = true;
                        break;
                    }

                    Result result = Generate(number, 0);
                    LevelDefinition asset = LoadOrCreate(number);
                    asset.CopyFrom(result.Level);
                    EditorUtility.SetDirty(asset);
                    assets.Add(asset);
                    UnityEngine.Object.DestroyImmediate(result.Level);

                    Debug.Log(result.Line);
                    if (result.Passed)
                    {
                        passed++;
                    }
                    else
                    {
                        weak.Add(result.Line);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.SetLevels(assets);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = $"{LogTag} {assets.Count} levels written to {LevelsFolder}, {passed} within the win-rate limits, {weak.Count} below them.";
            if (cancelled)
            {
                summary += " Cancelled before the last level.";
            }

            if (weak.Count > 0)
            {
                summary += "\nBelow the limits:\n" + string.Join("\n", weak);
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }

            return database;
        }

        /// <summary>
        /// Rebuilds <paramref name="level"/> in place with a new layout and figure seed, keeping its number,
        /// goal type and place on the difficulty curve.
        /// </summary>
        public static Result Regenerate(LevelDefinition level)
        {
            int variation = UnityEngine.Random.Range(1, 100000);
            Result result = Generate(level.Number, variation);
            level.CopyFrom(result.Level);
            EditorUtility.SetDirty(level);
            UnityEngine.Object.DestroyImmediate(result.Level);
            Debug.Log(result.Line);
            return result;
        }

        /// <summary>Generates level <paramref name="number"/>. Variation 0 is the canonical level of the campaign.</summary>
        public static Result Generate(int number, int variation)
        {
            float minRate = number <= EarlyLevelCount ? EarlyMinWinRate : MinWinRate;
            Result best = null;
            int bestAttempt = 0;

            for (int attempt = 0; attempt < LayoutAttempts; attempt++)
            {
                Plan plan = CreatePlan(number, variation, attempt);
                Result result = Tune(plan, minRate, out float ease, out int steps);
                result.Line = Describe(plan, result, ease, steps, attempt, minRate);

                if (best == null || (result.Passed && !best.Passed) || (!best.Passed && result.Report.WinRate > best.Report.WinRate))
                {
                    if (best != null)
                    {
                        UnityEngine.Object.DestroyImmediate(best.Level);
                    }

                    best = result;
                    bestAttempt = attempt;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(result.Level);
                }

                if (best.Passed)
                {
                    break;
                }
            }

            if (bestAttempt > 0 || !best.Passed)
            {
                best.Line += $" (layout {bestAttempt + 1} of {LayoutAttempts})";
            }

            return best;
        }

        // ---- difficulty curve ----

        /// <summary>0..1 difficulty of a level: a slow climb to 50, then a sawtooth where every 10th level bites and the next one rests.</summary>
        private static float DifficultyOf(int number)
        {
            float d = number <= 50
                ? Mathf.Lerp(0.05f, 0.7f, (number - 1) / 49f)
                : Mathf.Lerp(0.6f, 0.9f, (number - 51) / 49f);

            if (number > 50)
            {
                if (number % 10 == 0)
                {
                    d += 0.12f;
                }
                else if (number % 10 == 1 && number > 51)
                {
                    d -= 0.15f;
                }
            }

            if (IsIntro(number))
            {
                d -= 0.15f;
            }

            return Mathf.Clamp(d, 0.02f, 1f);
        }

        /// <summary>The first level of each new goal type is a gentle lesson.</summary>
        private static bool IsIntro(int number) => number == 11 || number == 21 || number == 36;

        private static LevelGoalType GoalFor(int number)
        {
            LevelGoalType previous = LevelGoalType.Score;
            for (int k = 1; k <= number; k++)
            {
                previous = PickGoal(k, previous);
            }

            return previous;
        }

        private static LevelGoalType PickGoal(int number, LevelGoalType previous)
        {
            if (number <= 10)
            {
                return LevelGoalType.Score;
            }

            if (number == 11)
            {
                return LevelGoalType.Gems;
            }

            if (number == 21)
            {
                return LevelGoalType.ClearMarked;
            }

            if (number == 36)
            {
                return LevelGoalType.Lines;
            }

            var pool = new List<LevelGoalType> { LevelGoalType.Score, LevelGoalType.Gems };
            if (number >= 21)
            {
                pool.Add(LevelGoalType.ClearMarked);
            }

            if (number >= 36)
            {
                pool.Add(LevelGoalType.Lines);
            }

            var rng = new Random(number * 7919 + 3);
            if (rng.NextDouble() < 0.7)
            {
                pool.Remove(previous);
            }

            return pool[rng.Next(pool.Count)];
        }

        // ---- plans ----

        private static Plan CreatePlan(int number, int variation, int attempt)
        {
            var rng = new Random(unchecked(number * 7919 + variation * 104729 + attempt * 31 + 3));
            float d = DifficultyOf(number);
            bool intro = IsIntro(number);
            LevelGoalType goal = GoalFor(number);
            bool special = number % 5 == 0;

            var plan = new Plan
            {
                Number = number,
                Goal = goal,
                Difficulty = d,
                Special = special,
                Reward = RoundTo((10 + number / 10 * 5) * (special ? 2 : 1), 5),
                Seed = unchecked(number * 104729 + variation * 7907 + attempt * 13 + 17)
            };

            int blocks;
            int gems = 0;
            int marked = 0;

            switch (goal)
            {
                case LevelGoalType.Gems:
                    gems = intro ? 3 : Mathf.RoundToInt(Mathf.Lerp(3f, 10f, d));
                    blocks = gems + Mathf.RoundToInt(Mathf.Lerp(5f, 16f, d));
                    plan.BaseMoves = Mathf.RoundToInt(Mathf.Lerp(26f, 16f, d));
                    break;

                case LevelGoalType.ClearMarked:
                    marked = intro ? 3 : Mathf.RoundToInt(Mathf.Lerp(3f, 12f, d));
                    blocks = marked + Mathf.RoundToInt(Mathf.Lerp(6f, 14f, d));
                    plan.BaseMoves = Mathf.RoundToInt(Mathf.Lerp(26f, 16f, d));
                    break;

                case LevelGoalType.Lines:
                    plan.BaseTarget = intro ? 4 : Mathf.RoundToInt(Mathf.Lerp(4f, 14f, d));
                    plan.MinTarget = 3;
                    blocks = Mathf.RoundToInt(Mathf.Lerp(0f, 12f, d));
                    plan.BaseMoves = Mathf.RoundToInt(plan.BaseTarget * Mathf.Lerp(3.2f, 2.3f, d));
                    break;

                default:
                    blocks = number == 1 ? 0 : Mathf.RoundToInt(Mathf.Lerp(0f, 14f, d));
                    if (number <= 3)
                    {
                        // The first three levels have no move limit: a plain score to reach.
                        plan.BaseMoves = 0;
                        plan.BaseTarget = 300 * number + 200;
                    }
                    else
                    {
                        plan.BaseMoves = Mathf.RoundToInt(Mathf.Lerp(30f, 20f, d));
                        plan.BaseTarget = RoundTo(plan.BaseMoves * Mathf.Lerp(40f, 85f, d), 50);
                    }

                    plan.MinTarget = 200;
                    break;
            }

            plan.Cells = BuildLayout(rng, blocks, gems, marked, out int crystals);
            if (goal == LevelGoalType.Gems)
            {
                plan.BaseTarget = crystals;
                plan.MinTarget = crystals;
            }

            return plan;
        }

        /// <summary>Writes the plan into <paramref name="level"/> with the given ease (0 = as planned).</summary>
        private static void Apply(LevelDefinition level, Plan plan, float ease)
        {
            int moves = plan.BaseMoves > 0
                ? Mathf.Clamp(Mathf.RoundToInt(plan.BaseMoves * (1f + ease)), 4, MaxMoveLimit)
                : 0;

            int target = plan.BaseTarget;
            if (plan.Goal == LevelGoalType.Score || plan.Goal == LevelGoalType.Lines)
            {
                target = Mathf.Max(plan.MinTarget, Mathf.RoundToInt(plan.BaseTarget / (1f + 0.5f * ease)));
                if (plan.Goal == LevelGoalType.Score)
                {
                    target = Mathf.Max(plan.MinTarget, RoundTo(target, 50));
                }
            }

            level.Configure(plan.Number, plan.Special, plan.Goal, target, moves, plan.Seed, plan.Difficulty, plan.Reward);
            level.SetCells(plan.Cells);
        }

        // ---- bot-driven tuning ----

        private static Result Tune(Plan plan, float minRate, out float finalEase, out int steps)
        {
            float ceiling = Mathf.Max(minRate + 0.15f, Mathf.Lerp(0.95f, 0.5f, plan.Difficulty));
            var candidate = ScriptableObject.CreateInstance<LevelDefinition>();
            var good = ScriptableObject.CreateInstance<LevelDefinition>();
            BotReport goodReport = null;
            BotReport lastReport = null;
            float goodEase = 0f;
            float ease = 0f;
            steps = 0;

            while (steps < MaxTuneSteps)
            {
                steps++;
                Apply(candidate, plan, ease);
                lastReport = LevelBotSimulator.Evaluate(candidate, LevelBotSimulator.DefaultRuns, minRate);

                if (!lastReport.Aborted && lastReport.WinRate >= minRate)
                {
                    good.CopyFrom(candidate);
                    goodReport = lastReport;
                    goodEase = ease;

                    if (lastReport.WinRate > ceiling && ease - EaseStepDown >= MinEase - 0.001f)
                    {
                        ease -= EaseStepDown;
                        continue;
                    }

                    break;
                }

                // Too hard. If an earlier, easier step already passed, that one is the answer.
                if (goodReport != null || ease + EaseStepUp > MaxEase)
                {
                    break;
                }

                ease += EaseStepUp;
            }

            var result = new Result();
            if (goodReport != null)
            {
                candidate.CopyFrom(good);
                result.Report = goodReport;
                result.Passed = true;
                finalEase = goodEase;
            }
            else
            {
                Apply(candidate, plan, ease);
                result.Report = lastReport;
                result.Passed = false;
                finalEase = ease;
            }

            UnityEngine.Object.DestroyImmediate(good);
            AssignStars(candidate, result.Report);
            result.Level = candidate;
            return result;
        }

        private static void AssignStars(LevelDefinition level, BotReport report)
        {
            List<int> metrics = report != null ? report.WinMetrics : null;
            if (level.HasMoveLimit)
            {
                int two = metrics != null && metrics.Count > 0 ? Percentile(metrics, 0.4f) : Mathf.RoundToInt(level.MoveLimit * 0.3f);
                int three = metrics != null && metrics.Count > 0 ? Percentile(metrics, 0.8f) : Mathf.RoundToInt(level.MoveLimit * 0.6f);
                two = Mathf.Max(1, two);
                level.SetStarThresholds(two, Mathf.Max(two + 1, three));
            }
            else
            {
                int target = level.GoalTarget;
                int two = metrics != null && metrics.Count > 0 ? Percentile(metrics, 0.4f) : Mathf.RoundToInt(target * 1.3f);
                int three = metrics != null && metrics.Count > 0 ? Percentile(metrics, 0.8f) : Mathf.RoundToInt(target * 1.7f);
                two = RoundTo(Mathf.Max(target, two), 50);
                level.SetStarThresholds(two, Mathf.Max(two + 100, RoundTo(three, 50)));
            }
        }

        private static int Percentile(List<int> sorted, float fraction)
        {
            return sorted[Mathf.Clamp(Mathf.FloorToInt((sorted.Count - 1) * fraction), 0, sorted.Count - 1)];
        }

        // ---- board layouts ----

        /// <summary>
        /// Prefilled board of <paramref name="blocks"/> occupied cells, at most a few per row and column so that
        /// no line starts complete. <paramref name="gems"/> and <paramref name="marked"/> of them become crystals
        /// and marked cells.
        /// </summary>
        private static LevelCellType[] BuildLayout(Random rng, int blocks, int gems, int marked, out int crystals)
        {
            var filled = new bool[Size * Size];
            var rows = new int[Size];
            var cols = new int[Size];
            int count = 0;

            if (blocks >= 8 && rng.NextDouble() < 0.45)
            {
                // Bands: short horizontal runs, mostly in the lower half.
                for (int attempt = 0; attempt < 30 && count < blocks; attempt++)
                {
                    int row = rng.Next(2, Size);
                    int length = rng.Next(3, 5);
                    int start = rng.Next(0, Size - length + 1);
                    for (int i = 0; i < length && count < blocks; i++)
                    {
                        TryAdd(filled, rows, cols, row, start + i, ref count);
                    }
                }
            }

            // Mirrored scatter for the rest, which reads as authored rather than random.
            for (int attempt = 0; attempt < 800 && count < blocks; attempt++)
            {
                int row = rng.Next(Size);
                int col = rng.Next(Size / 2);
                TryAdd(filled, rows, cols, row, col, ref count);
                if (count < blocks)
                {
                    TryAdd(filled, rows, cols, row, Size - 1 - col, ref count);
                }
            }

            var indices = new List<int>();
            for (int i = 0; i < filled.Length; i++)
            {
                if (filled[i])
                {
                    indices.Add(i);
                }
            }

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var cells = new LevelCellType[Size * Size];
            crystals = Mathf.Min(gems, indices.Count);
            int marks = Mathf.Min(marked, indices.Count);

            for (int i = 0; i < indices.Count; i++)
            {
                cells[indices[i]] = i < crystals
                    ? LevelCellType.Crystal
                    : i < marks ? LevelCellType.Marked : LevelCellType.Block;
            }

            return cells;
        }

        private static void TryAdd(bool[] filled, int[] rows, int[] cols, int row, int col, ref int count)
        {
            if (filled[row * Size + col] || rows[row] >= MaxBlocksPerLine || cols[col] >= MaxBlocksPerLine)
            {
                return;
            }

            filled[row * Size + col] = true;
            rows[row]++;
            cols[col]++;
            count++;
        }

        // ---- helpers ----

        private static string Describe(Plan plan, Result result, float ease, int steps, int attempt, float minRate)
        {
            LevelDefinition level = result.Level;
            string verdict = result.Passed ? "ok" : $"BELOW {minRate * 100f:0}%";
            return $"{LogTag} #{level.Number:000} {level.GoalType,-11} target={level.GoalTarget} moves={(level.HasMoveLimit ? level.MoveLimit.ToString() : "-")} "
                + $"blocks={level.CountOccupied()} d={level.Difficulty:0.00} bot={result.Report.WinRate * 100f:0}% ease={ease:0.00} steps={steps} [{verdict}]"
                + (level.IsSpecial ? " special" : string.Empty);
        }

        private static int RoundTo(float value, int step)
        {
            return Mathf.RoundToInt(value / step) * step;
        }

        private static LevelDefinition LoadOrCreate(int number)
        {
            string path = $"{LevelsFolder}/Level_{number:000}.asset";
            var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                AssetDatabase.CreateAsset(level, path);
            }

            return level;
        }

        private static void EnsureFolder()
        {
            string full = Path.GetFullPath(LevelsFolder);
            if (!Directory.Exists(full))
            {
                Directory.CreateDirectory(full);
                AssetDatabase.Refresh();
            }
        }
    }
}
