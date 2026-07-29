using System;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Central coordinator of the session. It is the only object that knows about all
    /// subsystems: it wires them together, owns the game state and drives restarts.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager instance;

        [Header("Systems")]
        [SerializeField] private GridManager gridManager;
        [SerializeField] private ShapeSpawner shapeSpawner;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private GameOverHandler gameOverHandler;
        [SerializeField] private AudioManager audioManager;

        [Header("Options")]
        [SerializeField] private bool startAutomatically = true;

        [Tooltip("Freeze the simulation while paused. UI animations keep running on unscaled time.")]
        [SerializeField] private bool freezeTimeWhilePaused = true;

        public static GameManager Instance => instance;

        public GridManager Grid => gridManager;
        public ShapeSpawner Spawner => shapeSpawner;
        public ScoreManager Score => scoreManager;
        public GameOverHandler GameOver => gameOverHandler;
        public AudioManager Audio => audioManager;

        public GameState State { get; private set; } = GameState.Boot;
        public bool IsPlaying => State == GameState.Playing;
        public bool IsPaused => State == GameState.Paused;

        /// <summary>True while the player is allowed to open the pause screen.</summary>
        public bool CanPause => State == GameState.Playing || State == GameState.Paused;

        public event Action<GameState> StateChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ResolveMissingReferences();
        }

        private void OnEnable() => SubscribeSystems();

        private void OnDisable() => UnsubscribeSystems();

        private void Start()
        {
            if (startAutomatically)
            {
                StartNewGame();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            // Leaving a frozen time scale behind would stall whatever loads next.
            ResumeTime();
        }

        /// <summary>Sending the app to the background opens the pause screen, as players expect.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused && IsPlaying)
            {
                SetPaused(true);
            }
        }

        /// <summary>
        /// Starts a run from scratch: the board is wiped and pre-filled with a freshly drawn
        /// starting layout, the score goes back to zero and the spawn area is dealt a new batch.
        /// Nothing survives from the previous run, including a pause that was still open.
        /// </summary>
        public void StartNewGame()
        {
            gameOverHandler?.Configure(gridManager, shapeSpawner, scoreManager);
            SubscribeSystems();

            ResumeTime();

            gridManager?.StartGame();
            scoreManager?.ResetScore();
            shapeSpawner?.Restart();
            gameOverHandler?.Arm();

            SetState(GameState.Playing);
            shapeSpawner?.SetInteractable(true);
        }

        public void RestartGame() => StartNewGame();

        /// <summary>Opens or closes the pause screen. Ignored once the run is over.</summary>
        public void SetPaused(bool paused)
        {
            if (!CanPause || IsPaused == paused)
            {
                return;
            }

            if (paused)
            {
                FreezeTime();
            }
            else
            {
                ResumeTime();
            }

            SetState(paused ? GameState.Paused : GameState.Playing);
            shapeSpawner?.SetInteractable(!paused);
        }

        public void TogglePause() => SetPaused(!IsPaused);

        private void FreezeTime()
        {
            if (freezeTimeWhilePaused)
            {
                Time.timeScale = 0f;
            }
        }

        private void ResumeTime()
        {
            if (freezeTimeWhilePaused)
            {
                Time.timeScale = 1f;
            }
        }

        private void HandleShapePlaced(PlacementResult result)
        {
            scoreManager?.RegisterPlacement(result);

            if (audioManager == null)
            {
                return;
            }

            audioManager.PlayPlacement();
            audioManager.PlayLineClear(result.LinesCleared);
        }

        private void HandleGameOver()
        {
            if (State == GameState.GameOver)
            {
                return;
            }

            ResumeTime();
            shapeSpawner?.SetInteractable(false);
            scoreManager?.Save();
            SetState(GameState.GameOver);
        }

        private void SetState(GameState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(State);
        }

        private void ResolveMissingReferences()
        {
            gridManager = gridManager != null ? gridManager : FindObjectOfType<GridManager>(true);
            shapeSpawner = shapeSpawner != null ? shapeSpawner : FindObjectOfType<ShapeSpawner>(true);
            scoreManager = scoreManager != null ? scoreManager : GetComponent<ScoreManager>();
            scoreManager = scoreManager != null ? scoreManager : FindObjectOfType<ScoreManager>(true);
            gameOverHandler = gameOverHandler != null ? gameOverHandler : GetComponent<GameOverHandler>();
            gameOverHandler = gameOverHandler != null ? gameOverHandler : FindObjectOfType<GameOverHandler>(true);
            audioManager = audioManager != null ? audioManager : GetComponent<AudioManager>();
            audioManager = audioManager != null ? audioManager : FindObjectOfType<AudioManager>(true);
        }

        /// <summary>Injection entry point used by the scene factory when building at runtime.</summary>
        public void Configure(
            GridManager grid,
            ShapeSpawner spawner,
            ScoreManager score,
            GameOverHandler gameOver,
            AudioManager audio = null)
        {
            UnsubscribeSystems();

            gridManager = grid;
            shapeSpawner = spawner;
            scoreManager = score;
            gameOverHandler = gameOver;
            audioManager = audio != null ? audio : audioManager;

            SubscribeSystems();
        }

        private void SubscribeSystems()
        {
            if (gridManager != null)
            {
                gridManager.ShapePlaced -= HandleShapePlaced;
                gridManager.ShapePlaced += HandleShapePlaced;
            }

            if (gameOverHandler != null)
            {
                gameOverHandler.GameOver -= HandleGameOver;
                gameOverHandler.GameOver += HandleGameOver;
            }
        }

        private void UnsubscribeSystems()
        {
            if (gridManager != null)
            {
                gridManager.ShapePlaced -= HandleShapePlaced;
            }

            if (gameOverHandler != null)
            {
                gameOverHandler.GameOver -= HandleGameOver;
            }
        }
    }
}
