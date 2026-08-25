using System;
using UnityEngine;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Watches the board and the spawn area: every time a fresh batch of figures appears
    /// it checks whether at least one of them still fits. If none of the three can be
    /// placed anywhere, the run is over and the score is committed to disk.
    /// </summary>
    public class GameOverHandler : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private ShapeSpawner spawner;
        [SerializeField] private ScoreManager score;

        private bool armed;

        /// <summary>Raised once per run when no move is left.</summary>
        public event Action GameOver;

        /// <summary>True when the run that just ended beat the previously stored record.</summary>
        public bool WasRecord { get; private set; }

        public void Configure(GridManager gridManager, ShapeSpawner shapeSpawner, ScoreManager scoreManager = null)
        {
            Unsubscribe();
            grid = gridManager;
            spawner = shapeSpawner;

            if (scoreManager != null)
            {
                score = scoreManager;
            }

            ResolveScore();
            Subscribe();
        }

        /// <summary>Starts watching for a dead end. Called at the beginning of every run.</summary>
        public void Arm()
        {
            armed = true;
            WasRecord = false;
            Subscribe();
        }

        public void Disarm()
        {
            armed = false;
        }

        /// <summary>True when at least one offered figure still fits on the board.</summary>
        public bool HasAnyMove()
        {
            if (grid == null || spawner == null)
            {
                return true;
            }

            return grid.HasPlacementForAny(spawner.AvailableShapes);
        }

        /// <summary>Evaluates the board immediately and fires the event if the run is dead.</summary>
        public bool Evaluate()
        {
            if (!armed)
            {
                return false;
            }

            // An empty spawn area never means a dead end: the next batch is on its way.
            if (spawner != null && spawner.RemainingCount == 0)
            {
                return false;
            }

            spawner?.RefreshPlayability();

            if (HasAnyMove())
            {
                return false;
            }

            armed = false;
            WasRecord = score != null && score.CommitBestScore();
            GameOver?.Invoke();
            return true;
        }

        private void Awake() => ResolveScore();

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void ResolveScore()
        {
            if (score != null)
            {
                return;
            }

            score = GetComponent<ScoreManager>();
            score = score != null ? score : FindObjectOfType<ScoreManager>(true);
        }

        private void Subscribe()
        {
            if (spawner == null)
            {
                return;
            }

            spawner.BatchSpawned -= HandleBatchSpawned;
            spawner.BatchSpawned += HandleBatchSpawned;
            spawner.ShapesChanged -= HandleShapesChanged;
            spawner.ShapesChanged += HandleShapesChanged;
        }

        private void Unsubscribe()
        {
            if (spawner == null)
            {
                return;
            }

            spawner.BatchSpawned -= HandleBatchSpawned;
            spawner.ShapesChanged -= HandleShapesChanged;
        }

        /// <summary>A brand new batch of three figures has to offer at least one legal move.</summary>
        private void HandleBatchSpawned() => Evaluate();

        private void HandleShapesChanged() => Evaluate();
    }
}
