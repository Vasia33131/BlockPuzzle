using System;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.Managers
{
    public enum FreeBoosterType
    {
        Undo,
        Extra,
        Clear
    }

    /// <summary>
    /// Rewarded in-run boosters, the one-shot continue on Game Over, and the
    /// single free score-threshold charge for the current run.
    /// </summary>
    public class BoosterController : MonoBehaviour
    {
        public const int FreeBonusScoreStep = 3000;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private GridManager grid;
        [SerializeField] private ShapeSpawner spawner;
        [SerializeField] private UndoBuffer undoBuffer;
        [SerializeField] private GameOverHandler gameOverHandler;
        [SerializeField] private ScoreManager scoreManager;

        private bool continueUsed;
        private FreeBoosterType? freeCharge;
        private int lastGrantedThreshold;

        /// <summary>True until this run has already used the continue booster.</summary>
        public bool CanContinue => !continueUsed;

        /// <summary>True when the last successful drop can still be reversed.</summary>
        public bool CanUndo => undoBuffer != null && undoBuffer.CanUndo;

        /// <summary>True when the tray has a free slot for an extra figure.</summary>
        public bool CanExtraPiece => spawner != null && spawner.RemainingCount < ShapeSpawner.SlotCount;

        /// <summary>True when the board has at least one occupied cell to clear.</summary>
        public bool CanClearLine =>
            grid != null && grid.Model != null && grid.Model.OccupiedCount > 0;

        /// <summary>True once this run has spent its continue.</summary>
        public bool ContinueUsed => continueUsed;

        /// <summary>Unused free charge for this run, if any. Saved with the run.</summary>
        public FreeBoosterType? FreeCharge => freeCharge;

        /// <summary>Last <see cref="FreeBonusScoreStep"/> multiple that granted a charge.</summary>
        public int LastGrantedThreshold => lastGrantedThreshold;

        /// <summary>
        /// Raised after a booster changed the board, the tray or the run's booster state
        /// (continue spent, free charge granted or burnt), so the run can be saved.
        /// </summary>
        public event Action RunChanged;

        public void Configure(
            GameManager manager,
            GridManager gridManager,
            ShapeSpawner shapeSpawner,
            UndoBuffer undo,
            GameOverHandler gameOver)
        {
            gameManager = manager;
            grid = gridManager;
            spawner = shapeSpawner;
            undoBuffer = undo;
            gameOverHandler = gameOver;
            BindScore();
        }

        private void OnEnable() => BindScore();

        private void OnDisable() => UnbindScore();

        /// <summary>Allows continue again. Called at the start of every run.</summary>
        public void ResetContinue()
        {
            continueUsed = false;
        }

        /// <summary>
        /// Drops the free charge and score threshold. Called at the start of every
        /// run; Game Over continue must not call this.
        /// </summary>
        public void ResetRun()
        {
            freeCharge = null;
            lastGrantedThreshold = 0;
        }

        /// <summary>
        /// Brings back the booster state of a saved run. Call before the score is restored,
        /// so the restored score does not look like a threshold crossed just now.
        /// </summary>
        public void RestoreRun(bool usedContinue, FreeBoosterType? charge, int grantedThreshold)
        {
            continueUsed = usedContinue;
            freeCharge = charge.HasValue && Enum.IsDefined(typeof(FreeBoosterType), charge.Value) ? charge : null;
            lastGrantedThreshold = Mathf.Max(0, grantedThreshold);
        }

        public bool HasFree(FreeBoosterType type) => freeCharge == type;

        /// <summary>Boosters of this type the player keeps between runs (daily reward, tasks).</summary>
        public static int StockCount(FreeBoosterType type) => MetaProgress.BoosterCount(ToMeta(type));

        public static MetaBooster ToMeta(FreeBoosterType type)
        {
            switch (type)
            {
                case FreeBoosterType.Extra:
                    return MetaBooster.Extra;
                case FreeBoosterType.Clear:
                    return MetaBooster.Clear;
                default:
                    return MetaBooster.Undo;
            }
        }

        /// <summary>
        /// Applies the matching booster and burns the charge. False when this is
        /// not the charged type or <c>Can*</c> is false — the charge stays.
        /// </summary>
        public bool TryConsumeFree(FreeBoosterType type)
        {
            if (freeCharge != type || !TryApply(type))
            {
                return false;
            }

            freeCharge = null;
            int score = scoreManager != null ? scoreManager.Score : 0;
            lastGrantedThreshold = score / FreeBonusScoreStep;
            RunChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Applies one booster from the stock and takes it away. False when the stock is
        /// empty or <c>Can*</c> is false — the stock stays.
        /// </summary>
        public bool TryConsumeStock(FreeBoosterType type)
        {
            if (StockCount(type) <= 0 || !TryApply(type))
            {
                return false;
            }

            MetaProgress.TrySpendBooster(ToMeta(type));
            return true;
        }

        private bool TryApply(FreeBoosterType type)
        {
            switch (type)
            {
                case FreeBoosterType.Undo:
                    return CanUndo && TryUndo();
                case FreeBoosterType.Extra:
                    return CanExtraPiece && TryExtraPiece();
                case FreeBoosterType.Clear:
                    return CanClearLine && TryClearFullestLine();
                default:
                    return false;
            }
        }

        public bool TryUndo()
        {
            return Notify(undoBuffer != null && undoBuffer.TryUndo());
        }

        public bool TryExtraPiece()
        {
            return Notify(spawner != null && spawner.TryGrantExtraShape());
        }

        /// <summary>Clears the single fullest row or column. False when the board is empty.</summary>
        public bool TryClearFullestLine()
        {
            if (!TryGetFullestLine(out int index, out bool horizontal, out int fill) || fill <= 0)
            {
                return false;
            }

            grid.ClearLineAndRedraw(index, horizontal);
            undoBuffer?.RefreshSettled();
            spawner?.RefreshPlayability();
            RunChanged?.Invoke();
            gameOverHandler?.Evaluate();
            return true;
        }

        /// <summary>
        /// Clears the one or two fullest lines, re-arms game over and returns the run
        /// to playing. Once per run.
        /// </summary>
        public bool TryContinue()
        {
            if (continueUsed || gameManager == null || grid == null)
            {
                return false;
            }

            continueUsed = true;
            ClearTopLines(2);
            undoBuffer?.Clear();

            gameManager.ResumePlaying();
            gameOverHandler?.Arm();
            spawner?.SetInteractable(true);
            spawner?.RefreshPlayability();
            RunChanged?.Invoke();
            gameOverHandler?.Evaluate();
            return true;
        }

        private bool Notify(bool applied)
        {
            if (applied)
            {
                RunChanged?.Invoke();
            }

            return applied;
        }

        private void ClearTopLines(int maxLines)
        {
            if (grid == null || grid.Model == null || maxLines <= 0)
            {
                return;
            }

            int size = grid.Size;
            int total = size * 2;
            var lines = new LineFill[total];
            CollectLineFills(lines);

            for (int n = 0; n < maxLines; n++)
            {
                int best = -1;
                for (int i = 0; i < total; i++)
                {
                    if (lines[i].Cleared || lines[i].Fill <= 0)
                    {
                        continue;
                    }

                    if (best < 0 || lines[i].Fill > lines[best].Fill)
                    {
                        best = i;
                    }
                }

                if (best < 0)
                {
                    return;
                }

                grid.ClearLineAndRedraw(lines[best].Index, lines[best].Horizontal);
                lines[best].Cleared = true;
            }
        }

        private bool TryGetFullestLine(out int index, out bool horizontal, out int fill)
        {
            index = 0;
            horizontal = true;
            fill = 0;

            if (grid == null || grid.Model == null)
            {
                return false;
            }

            int size = grid.Size;
            var lines = new LineFill[size * 2];
            CollectLineFills(lines);

            int best = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (best < 0 || lines[i].Fill > lines[best].Fill)
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                return false;
            }

            index = lines[best].Index;
            horizontal = lines[best].Horizontal;
            fill = lines[best].Fill;
            return true;
        }

        private void CollectLineFills(LineFill[] lines)
        {
            GridModel model = grid.Model;
            int size = grid.Size;

            for (int row = 0; row < size; row++)
            {
                int fill = 0;
                for (int col = 0; col < size; col++)
                {
                    if (model.IsOccupied(row, col))
                    {
                        fill++;
                    }
                }

                lines[row] = new LineFill(row, true, fill);
            }

            for (int col = 0; col < size; col++)
            {
                int fill = 0;
                for (int row = 0; row < size; row++)
                {
                    if (model.IsOccupied(row, col))
                    {
                        fill++;
                    }
                }

                lines[size + col] = new LineFill(col, false, fill);
            }
        }

        private struct LineFill
        {
            public int Index;
            public bool Horizontal;
            public int Fill;
            public bool Cleared;

            public LineFill(int index, bool horizontal, int fill)
            {
                Index = index;
                Horizontal = horizontal;
                Fill = fill;
                Cleared = false;
            }
        }

        private void BindScore()
        {
            ScoreManager score = gameManager != null ? gameManager.Score : null;
            if (scoreManager != null && scoreManager != score)
            {
                scoreManager.ScoreChanged -= HandleScoreChanged;
            }

            scoreManager = score;
            if (scoreManager == null)
            {
                return;
            }

            scoreManager.ScoreChanged -= HandleScoreChanged;
            scoreManager.ScoreChanged += HandleScoreChanged;
        }

        private void UnbindScore()
        {
            if (scoreManager != null)
            {
                scoreManager.ScoreChanged -= HandleScoreChanged;
                scoreManager = null;
            }
        }

        private void HandleScoreChanged(int score)
        {
            TryGrantFreeBonus(score);
        }

        private void TryGrantFreeBonus(int score)
        {
            if (freeCharge.HasValue)
            {
                return;
            }

            int threshold = score / FreeBonusScoreStep;
            if (threshold <= lastGrantedThreshold)
            {
                return;
            }

            freeCharge = (FreeBoosterType)UnityEngine.Random.Range(0, 3);
            lastGrantedThreshold = threshold;
            RunChanged?.Invoke();
        }
    }
}
