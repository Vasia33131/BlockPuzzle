using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// One-step undo for a successful drop: the board and tray as they were
    /// immediately before that placement.
    /// </summary>
    public class UndoBuffer : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private ShapeSpawner spawner;

        private BoardSnapshot settledBoard;
        private List<BlockShape> settledShapes;
        private BoardSnapshot undoBoard;
        private List<BlockShape> undoShapes;
        private bool hasUndo;

        /// <summary>True when a successful drop can still be reversed.</summary>
        public bool CanUndo => hasUndo && undoBoard != null;

        public void Configure(GridManager gridManager, ShapeSpawner shapeSpawner)
        {
            Unsubscribe();
            grid = gridManager;
            spawner = shapeSpawner;
            Subscribe();
        }

        /// <summary>Drops the stored step. Called at the start of every run.</summary>
        public void Clear()
        {
            hasUndo = false;
            undoBoard = null;
            undoShapes = null;
            CaptureSettled();
        }

        /// <summary>
        /// Re-reads the live board and tray without granting an undo step.
        /// Call after boosters that change the board outside of a normal drop.
        /// </summary>
        public void RefreshSettled() => CaptureSettled();

        /// <summary>
        /// Restores the board and tray from the last successful drop.
        /// False when nothing has been stored yet.
        /// </summary>
        public bool TryUndo()
        {
            if (!hasUndo || undoBoard == null)
            {
                return false;
            }

            BoardSnapshot board = undoBoard;
            List<BlockShape> shapes = undoShapes;
            hasUndo = false;
            undoBoard = null;
            undoShapes = null;

            grid?.RestoreBoard(board);
            spawner?.RestoreShapes(shapes);
            CaptureSettled();
            return true;
        }

        private void Awake()
        {
            if (grid == null)
            {
                grid = FindObjectOfType<GridManager>(true);
            }

            if (spawner == null)
            {
                spawner = FindObjectOfType<ShapeSpawner>(true);
            }
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            GameTheme.Changed -= RemapStoredColors;
            GameTheme.Changed += RemapStoredColors;

            if (grid != null)
            {
                grid.ShapePlaced -= HandleShapePlaced;
                grid.ShapePlaced += HandleShapePlaced;
            }

            if (spawner != null)
            {
                spawner.ShapesChanged -= CaptureSettled;
                spawner.ShapesChanged += CaptureSettled;
                spawner.BatchSpawned -= CaptureSettled;
                spawner.BatchSpawned += CaptureSettled;
            }
        }

        private void Unsubscribe()
        {
            GameTheme.Changed -= RemapStoredColors;

            if (grid != null)
            {
                grid.ShapePlaced -= HandleShapePlaced;
            }

            if (spawner != null)
            {
                spawner.ShapesChanged -= CaptureSettled;
                spawner.BatchSpawned -= CaptureSettled;
            }
        }

        private void RemapStoredColors()
        {
            settledBoard?.RemapColors(GameTheme.RemapPlacedColor);
            undoBoard?.RemapColors(GameTheme.RemapPlacedColor);
        }

        /// <summary>
        /// <see cref="GridManager.ShapePlaced"/> fires after the board has already
        /// changed, so the undo step is the last settled snapshot, taken before this drop.
        /// </summary>
        private void HandleShapePlaced(PlacementResult result)
        {
            if (!result.Success || settledBoard == null)
            {
                return;
            }

            undoBoard = settledBoard;
            undoShapes = settledShapes;
            hasUndo = true;
        }

        private void CaptureSettled()
        {
            settledBoard = grid != null ? grid.CaptureBoard() : null;
            settledShapes = CopyShapes(spawner != null ? spawner.PeekShapes() : null);
        }

        private static List<BlockShape> CopyShapes(IReadOnlyList<BlockShape> source)
        {
            var copy = new List<BlockShape>(ShapeSpawner.SlotCount);
            if (source == null)
            {
                return copy;
            }

            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(source[i]);
            }

            return copy;
        }
    }
}
