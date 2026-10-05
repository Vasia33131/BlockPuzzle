using System;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Levels;
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
        [SerializeField] private UndoBuffer undoBuffer;
        [SerializeField] private BoosterController boosterController;
        [SerializeField] private RunSaveController runSaves;
        [SerializeField] private MetaRunTracker metaTracker;
        [SerializeField] private LevelRunController levelRun;

        [Header("Options")]
        [Tooltip("Open the main menu at launch. Off starts an endless run right away.")]
        [SerializeField] private bool openMenuOnStart = true;

        [Tooltip("Freeze the simulation while paused. UI animations keep running on unscaled time.")]
        [SerializeField] private bool freezeTimeWhilePaused = true;

        public static GameManager Instance => instance;

        public GridManager Grid => gridManager;
        public ShapeSpawner Spawner => shapeSpawner;
        public ScoreManager Score => scoreManager;
        public GameOverHandler GameOver => gameOverHandler;
        public AudioManager Audio => audioManager;
        public UndoBuffer Undo => undoBuffer;
        public BoosterController Boosters => boosterController;
        public RunSaveController RunSaves => runSaves;

        /// <summary>
        /// The level attempt counters. Created on first access: the bootstrap binds the level screens
        /// before a baked scene has woken this manager up.
        /// </summary>
        public LevelRunController LevelRun
        {
            get
            {
                if (levelRun == null && Application.isPlaying)
                {
                    EnsureLevelRun();
                }

                return levelRun;
            }
        }


        public GameState State { get; private set; } = GameState.Boot;

        /// <summary>Kind of the current (or last) run: <see cref="GameMode.Level"/> from <see cref="StartLevel"/> until an endless run begins.</summary>
        public GameMode Mode { get; private set; } = GameMode.Endless;
        public bool IsPlaying => State == GameState.Playing;
        public bool IsPaused => State == GameState.Paused;

        /// <summary>True while the player is allowed to open the pause screen.</summary>
        public bool CanPause => State == GameState.Playing || State == GameState.Paused;

        public event Action<GameState> StateChanged;

        /// <summary>
        /// Raised at the start of <see cref="RestartGame"/>, before the board is wiped.
        /// Platform ads hook this so a fullscreen block can open on the same tap
        /// (Yandex allows at most 0.33s between the tap and the ad).
        /// </summary>
        public event Action RestartRequested;

        /// <summary>
        /// Raised at the start of <see cref="StartNewGame"/>, before the board and the tray
        /// are dealt, so a scripted run (the tutorial) can set its layout and first batch.
        /// </summary>
        public event Action RunStarting;

        /// <summary>
        /// Raised at the start of <see cref="StartEndless"/>, before anything is dealt. The
        /// Play tap in the menu is a player action, so platform ads open from here.
        /// </summary>
        public event Action EndlessRequested;

        /// <summary>
        /// Raised by <see cref="NextLevel"/>, <see cref="RestartLevel"/> and <see cref="StartLevelFromMap"/>
        /// before the level is set up, with the number of the level about to start. The tap on "Next" /
        /// "Retry" / "Play" is a player action, so platform ads open from here.
        /// </summary>
        public event Action<int> LevelAdBreakRequested;

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
            if (openMenuOnStart)
            {
                OpenMainMenu();
            }
            else
            {
                StartEndless();
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

        /// <summary>True when an unfinished endless run is saved and <see cref="StartEndless"/> would continue it.</summary>
        public bool HasSavedRun => runSaves != null && runSaves.TryLoadSavedRun(out _);

        /// <summary>
        /// Leaves the current screen for the main menu. An unfinished endless run is written
        /// to the save first (the saver stops working outside Playing/Paused), so "Continue"
        /// brings the same run back. Never shows an ad.
        /// </summary>
        public void OpenMainMenu()
        {
            if (State == GameState.MainMenu)
            {
                return;
            }

            if (Mode == GameMode.Level)
            {
                // An unfinished level is never saved: the attempt burns. The saved endless run stays as it is.
                levelRun?.Abandon();
            }
            else if (State == GameState.Playing || State == GameState.Paused)
            {
                runSaves?.FlushNow();
            }

            ResumeTime();
            shapeSpawner?.SetInteractable(false);
            SetState(GameState.MainMenu);
        }

        /// <summary>Enters the endless mode: continues the saved run if there is one, else starts a new game.</summary>
        public void StartEndless()
        {
            Mode = GameMode.Endless;
            EndlessRequested?.Invoke();

            if (!TryResumeSavedRun())
            {
                StartNewGame();
            }
        }

        /// <summary>
        /// Starts a fresh attempt of a campaign level: the board gets the level's authored blocks, the
        /// tray is dealt by a dealer seeded with the level's seed, and the goal card runs before the
        /// first move. Nothing of an earlier attempt survives, and an unfinished attempt is never saved.
        /// Does not check whether the level is open: that is up to the caller (the level map).
        /// </summary>
        public void StartLevel(int levelNumber)
        {
            LevelDefinition level = LevelDatabase.Load()?.GetByNumber(levelNumber);
            if (level == null || levelRun == null)
            {
                Debug.LogWarning($"[GameManager] Level {levelNumber} is not available.");
                return;
            }

            // Restarting from pause or from a result screen: what is left of the old attempt burns.
            levelRun.Abandon();

            Mode = GameMode.Level;
            if (scoreManager != null)
            {
                scoreManager.TracksRecord = false;
            }

            gameOverHandler?.Configure(gridManager, shapeSpawner, scoreManager);
            SubscribeSystems();

            ResumeTime();
            boosterController?.ResetContinue();
            boosterController?.ResetRun();
            gridManager?.StartLevel(level);
            scoreManager?.ResetScore();
            shapeSpawner?.RestartLevel(level.ShapeSeed);
            gameOverHandler?.Arm();
            undoBuffer?.Clear();

            // The goal card keeps the tray locked until it is gone; the controller unlocks it.
            levelRun.BeginLevel(level);
            SetState(GameState.Playing);
        }

        /// <summary>
        /// The "Play" tap of the level card on the map: the chosen level behind the same level ad break as
        /// "Next" / "Retry" (the platform layer keeps the first levels ad-free). A level that is still
        /// locked or missing from the database is ignored, so no ad opens for a level that cannot start.
        /// </summary>
        public void StartLevelFromMap(int levelNumber)
        {
            if (!LevelProgress.IsUnlocked(levelNumber) || LevelDatabase.Load()?.GetByNumber(levelNumber) == null)
            {
                return;
            }

            LevelAdBreakRequested?.Invoke(levelNumber);
            StartLevel(levelNumber);
        }

        /// <summary>The "Next" tap of the result screen: the level after the one just passed, behind the level ad break.</summary>
        public void NextLevel()
        {
            int next = (levelRun != null ? levelRun.LevelNumber : 0) + 1;
            if (next > LevelDatabase.LevelCount || LevelDatabase.Load()?.GetByNumber(next) == null)
            {
                OpenMainMenu();
                return;
            }

            LevelAdBreakRequested?.Invoke(next);
            StartLevel(next);
        }

        /// <summary>The "Retry" tap (result screen or pause): the same level again, behind the level ad break.</summary>
        public void RestartLevel()
        {
            int number = levelRun != null ? levelRun.LevelNumber : 0;
            if (number <= 0)
            {
                return;
            }

            LevelAdBreakRequested?.Invoke(number);
            StartLevel(number);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Start Level 1")]
        private void DebugStartLevel1() => StartLevel(1);

        [ContextMenu("Debug/Start Level 5")]
        private void DebugStartLevel5() => StartLevel(5);
#endif

        /// <summary>
        /// Starts a run from scratch: the board is wiped and pre-filled with a freshly drawn
        /// starting layout, the score goes back to zero and the spawn area is dealt a new batch.
        /// Nothing survives from the previous run, including a pause that was still open.
        /// </summary>
        public void StartNewGame()
        {
            // "How to play" from the pause screen of a level lands here: the attempt burns and the
            // goal card stops, otherwise a stale intro would keep the platform gameplay flag off.
            if (Mode == GameMode.Level)
            {
                levelRun?.Abandon();
            }

            EnterEndlessMode();
            gameOverHandler?.Configure(gridManager, shapeSpawner, scoreManager);
            SubscribeSystems();

            ResumeTime();
            RunStarting?.Invoke();

            boosterController?.ResetContinue();
            boosterController?.ResetRun();
            gridManager?.StartGame();
            scoreManager?.ResetScore();
            shapeSpawner?.Restart();
            gameOverHandler?.Arm();
            undoBuffer?.Clear();
            runSaves?.BeginRun(0L);

            SetState(GameState.Playing);
            shapeSpawner?.SetInteractable(true);
        }

        /// <summary>
        /// Continues a run saved by <see cref="RunSaveController"/> in place of a fresh one.
        /// False when the save does not fit this build (board size, a figure that is gone)
        /// or restoring it failed; the caller then starts a new game.
        /// </summary>
        public bool ResumeRun(RunSnapshot snapshot)
        {
            if (!TryResolveTray(snapshot, out BlockShape[] tray))
            {
                return false;
            }

            EnterEndlessMode();
            gameOverHandler?.Configure(gridManager, shapeSpawner, scoreManager);
            SubscribeSystems();
            ResumeTime();

            try
            {
                // The run may have been saved under another palette.
                snapshot.Board.RemapColors(GameTheme.RemapPlacedColor);
                gridManager.ResetBoard();
                gridManager.RestoreBoard(snapshot.Board);

                // Boosters first: with the threshold back, the restored score grants nothing new.
                boosterController?.RestoreRun(snapshot.ContinueUsed, snapshot.FreeCharge, snapshot.FreeThreshold);
                scoreManager.RestoreRun(snapshot.Score, snapshot.Combo, snapshot.DryMoves, snapshot.RunStartRecord);

                gameOverHandler?.Arm();
                undoBuffer?.Clear();
                shapeSpawner.ResumeRun(tray);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunSave] Could not restore the saved run: {e.Message}");
                return false;
            }

            runSaves?.BeginRun(snapshot.Stamp);
            SetState(GameState.Playing);
            shapeSpawner.SetInteractable(true);
            return true;
        }

        private bool TryResumeSavedRun()
        {
            return runSaves != null
                && runSaves.TryLoadSavedRun(out RunSnapshot snapshot)
                && ResumeRun(snapshot);
        }

        private bool TryResolveTray(RunSnapshot snapshot, out BlockShape[] tray)
        {
            tray = null;
            if (snapshot?.Board == null || snapshot.Tray == null
                || gridManager == null || scoreManager == null || shapeSpawner == null
                || snapshot.Board.Size != gridManager.Size
                || snapshot.Tray.Length > ShapeSpawner.SlotCount)
            {
                return false;
            }

            tray = new BlockShape[ShapeSpawner.SlotCount];
            for (int i = 0; i < snapshot.Tray.Length; i++)
            {
                string name = snapshot.Tray[i];
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                tray[i] = shapeSpawner.FindShape(name);
                if (tray[i] == null)
                {
                    Debug.LogWarning($"[RunSave] Saved figure '{name}' is not in the catalog; starting a new game.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>Returns the run to playing after a continue booster, hiding game-over UI.</summary>
        public void ResumePlaying()
        {
            ResumeTime();
            SetState(GameState.Playing);
            shapeSpawner?.SetInteractable(true);
        }

        public void RestartGame()
        {
            if (Mode == GameMode.Level)
            {
                RestartLevel();
                return;
            }

            RestartRequested?.Invoke();
            StartNewGame();
        }

        /// <summary>Back to the rules of the endless mode: the score counts towards the record again.</summary>
        private void EnterEndlessMode()
        {
            Mode = GameMode.Endless;
            if (scoreManager != null)
            {
                scoreManager.TracksRecord = true;
            }
        }

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

            // After the score: the level reads it to decide the win.
            if (Mode == GameMode.Level)
            {
                levelRun?.HandlePlacement(result);
            }

            if (audioManager == null)
            {
                return;
            }

            audioManager.PlayPlacement();
            audioManager.PlayLineClear(result.LinesCleared, scoreManager != null ? scoreManager.ComboStreak : 1);

            if (result.BoardCleared)
            {
                audioManager.PlayBoardClear();
            }
        }

        /// <summary>The board only knows which cells went; the praise and the points come from the score.</summary>
        private void HandleLinesCleared(int lines, int points, int combo)
        {
            gridManager?.Feedback?.ShowClearScore(lines, points, combo);
        }

        private void HandleRecordBroken(int newBest) => SfxHub.Play(SfxId.JingleNewRecord);

        private void HandleBoardCleared(int bonus)
        {
            gridManager?.Feedback?.ShowBoardCleared(bonus);
        }

        /// <summary>The goal was reached: freeze the board and show the result. Nothing is saved as a run.</summary>
        private void HandleLevelWon(LevelResult result)
        {
            SfxHub.Play(SfxId.JingleWin);
            EndLevelAttempt(GameState.LevelWon);
        }

        private void HandleLevelFailed(LevelFailReason reason)
        {
            EndLevelAttempt(GameState.LevelFailed);
        }

        private void EndLevelAttempt(GameState outcome)
        {
            ResumeTime();
            shapeSpawner?.SetInteractable(false);
            SetState(outcome);
        }

        private void HandleGameOver()
        {
            // In a level "no room" is a loss of the level, not a Game Over: no record, no run save to erase.
            if (Mode == GameMode.Level)
            {
                levelRun?.HandleNoSpace();
                return;
            }

            if (State == GameState.GameOver)
            {
                return;
            }

            ResumeTime();
            shapeSpawner?.SetInteractable(false);
            scoreManager?.Save();
            SfxHub.Play(SfxId.JingleGameOver);
            SetState(GameState.GameOver);
            runSaves?.ClearSavedRun();
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
            undoBuffer = undoBuffer != null ? undoBuffer : GetComponent<UndoBuffer>();
            if (undoBuffer == null)
            {
                undoBuffer = gameObject.AddComponent<UndoBuffer>();
            }

            boosterController = boosterController != null ? boosterController : GetComponent<BoosterController>();
            if (boosterController == null)
            {
                boosterController = gameObject.AddComponent<BoosterController>();
            }

            runSaves = runSaves != null ? runSaves : GetComponent<RunSaveController>();
            if (runSaves == null)
            {
                runSaves = gameObject.AddComponent<RunSaveController>();
            }

            metaTracker = metaTracker != null ? metaTracker : GetComponent<MetaRunTracker>();
            if (metaTracker == null)
            {
                metaTracker = gameObject.AddComponent<MetaRunTracker>();
            }

            EnsureLevelRun();
            WireBoosters();
        }

        private void EnsureLevelRun()
        {
            levelRun = levelRun != null ? levelRun : GetComponent<LevelRunController>();
            if (levelRun == null)
            {
                levelRun = gameObject.AddComponent<LevelRunController>();
            }
        }

        private void WireBoosters()
        {
            undoBuffer?.Configure(gridManager, shapeSpawner);

            // A level deals with the score at zero, so the same seed gives the same figures to everyone.
            shapeSpawner?.SetScoreSource(() => scoreManager != null && Mode == GameMode.Endless ? scoreManager.Score : 0);
            boosterController?.Configure(this, gridManager, shapeSpawner, undoBuffer, gameOverHandler);
            runSaves?.Configure(this, gridManager, shapeSpawner, scoreManager, boosterController);
            metaTracker?.Configure(this);

            if (levelRun != null)
            {
                levelRun.Configure(this, gridManager, shapeSpawner, scoreManager, boosterController, gameOverHandler, undoBuffer);
                levelRun.LevelWon -= HandleLevelWon;
                levelRun.LevelWon += HandleLevelWon;
                levelRun.LevelFailed -= HandleLevelFailed;
                levelRun.LevelFailed += HandleLevelFailed;
            }
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

            undoBuffer = undoBuffer != null ? undoBuffer : GetComponent<UndoBuffer>();
            boosterController = boosterController != null ? boosterController : GetComponent<BoosterController>();
            runSaves = runSaves != null ? runSaves : GetComponent<RunSaveController>();
            metaTracker = metaTracker != null ? metaTracker : GetComponent<MetaRunTracker>();
            EnsureLevelRun();
            WireBoosters();

            SubscribeSystems();
        }

        private void SubscribeSystems()
        {
            if (gridManager != null)
            {
                gridManager.ShapePlaced -= HandleShapePlaced;
                gridManager.ShapePlaced += HandleShapePlaced;
            }

            if (scoreManager != null)
            {
                scoreManager.LinesCleared -= HandleLinesCleared;
                scoreManager.LinesCleared += HandleLinesCleared;
                scoreManager.BoardCleared -= HandleBoardCleared;
                scoreManager.BoardCleared += HandleBoardCleared;
                scoreManager.RecordBroken -= HandleRecordBroken;
                scoreManager.RecordBroken += HandleRecordBroken;
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

            if (scoreManager != null)
            {
                scoreManager.LinesCleared -= HandleLinesCleared;
                scoreManager.BoardCleared -= HandleBoardCleared;
                scoreManager.RecordBroken -= HandleRecordBroken;
            }

            if (gameOverHandler != null)
            {
                gameOverHandler.GameOver -= HandleGameOver;
            }
        }
    }
}
