using UnityEngine;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Rewarded in-run boosters and the one-shot continue on Game Over.
    /// </summary>
    public class BoosterController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private GridManager grid;
        [SerializeField] private ShapeSpawner spawner;
        [SerializeField] private UndoBuffer undoBuffer;
        [SerializeField] private GameOverHandler gameOverHandler;

        private bool continueUsed;

        /// <summary>True until this run has already used the continue booster.</summary>
        public bool CanContinue => !continueUsed;

        /// <summary>True when the last successful drop can still be reversed.</summary>
        public bool CanUndo => undoBuffer != null && undoBuffer.CanUndo;

        /// <summary>True when the tray has a free slot for an extra figure.</summary>
        public bool CanExtraPiece => spawner != null && spawner.RemainingCount < ShapeSpawner.SlotCount;

        /// <summary>True when the board has at least one occupied cell to clear.</summary>
        public bool CanClearLine =>
            grid != null && grid.Model != null && grid.Model.OccupiedCount > 0;

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
        }

        /// <summary>Allows continue again. Called at the start of every run.</summary>
        public void ResetContinue()
        {
            continueUsed = false;
        }

        public bool TryUndo()
        {
            return undoBuffer != null && undoBuffer.TryUndo();
        }

        public bool TryExtraPiece()
        {
            return spawner != null && spawner.TryGrantExtraShape();
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
            gameOverHandler?.Evaluate();
            return true;
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
    }
}
