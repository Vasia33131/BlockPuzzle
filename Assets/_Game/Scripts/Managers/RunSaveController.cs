using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Account copy of the unfinished run. The platform layer implements it on top of
    /// its cloud save and decides itself how often it really writes.
    /// </summary>
    public interface IRunCloudStore
    {
        /// <summary>Run held by the account save, or null while that save is not loaded yet.</summary>
        string ReadRun();

        /// <summary>Queues <paramref name="data"/> for the account save; null erases it. May be delayed.</summary>
        void WriteRun(string data);

        /// <summary>Writes whatever is queued right away: the tab is going away.</summary>
        void Flush();
    }

    /// <summary>
    /// Keeps the running game safe from a closed tab. After every change of the board,
    /// the tray, the score or the boosters the run is captured once at the end of the
    /// frame, written to PlayerPrefs immediately and handed to <see cref="CloudStore"/>,
    /// which throttles the account writes. Game Over erases the saved run.
    /// </summary>
    public sealed class RunSaveController : MonoBehaviour
    {
        public const string RunKey = "BlockPuzzle.CurrentRun";

        /// <summary>
        /// Save time of the last run that ended. An account copy that is not newer is a
        /// run that is already over and must not come back, even if erasing it in the
        /// cloud did not go through before the tab closed.
        /// </summary>
        public const string EndedStampKey = "BlockPuzzle.CurrentRunEndedAt";

        /// <summary>Set by the platform layer; null without a cloud save.</summary>
        public static IRunCloudStore CloudStore { get; set; }

        private GameManager gameManager;
        private GridManager grid;
        private ShapeSpawner spawner;
        private ScoreManager score;
        private BoosterController boosters;

        private bool dirty;
        private bool touchedThisSession;
        private long runStamp;
        private long lastSavedStamp;
        private string lastSavedBody;

        public void Configure(
            GameManager manager,
            GridManager gridManager,
            ShapeSpawner shapeSpawner,
            ScoreManager scoreManager,
            BoosterController boosterController)
        {
            Unsubscribe();
            gameManager = manager;
            grid = gridManager;
            spawner = shapeSpawner;
            score = scoreManager;
            boosters = boosterController;
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        /// <summary>
        /// A run has begun: <paramref name="resumedStamp"/> is the save time of the run that
        /// was brought back, or 0 for a fresh one. The run is saved at the end of this frame.
        /// </summary>
        public void BeginRun(long resumedStamp)
        {
            runStamp = resumedStamp;
            lastSavedBody = null;
            dirty = true;
        }

        /// <summary>
        /// The run is over: forget it locally and in the account right away. The Game Over
        /// screen is a likely moment to close the tab, so the erase is not throttled.
        /// </summary>
        public void ClearSavedRun()
        {
            dirty = false;
            lastSavedBody = null;

            long ended = Math.Max(lastSavedStamp, runStamp);
            if (ended > 0)
            {
                PlayerPrefs.SetString(EndedStampKey, ended.ToString(CultureInfo.InvariantCulture));
            }

            PlayerPrefs.DeleteKey(RunKey);
            PlayerPrefs.Save();

            CloudStore?.WriteRun(null);
            CloudStore?.Flush();
        }

        /// <summary>
        /// The newest readable unfinished run of the local and the account copy. A broken
        /// local copy is dropped so it cannot fail every launch.
        /// </summary>
        public bool TryLoadSavedRun(out RunSnapshot snapshot)
        {
            RunSnapshot local = null;
            string localText = PlayerPrefs.GetString(RunKey, string.Empty);
            if (!string.IsNullOrEmpty(localText) && !RunSnapshotCodec.TryDecode(localText, out local))
            {
                Debug.LogWarning("[RunSave] Local run is unreadable; starting a new game.");
                PlayerPrefs.DeleteKey(RunKey);
                PlayerPrefs.Save();
            }

            TryReadCloudRun(out RunSnapshot cloud);

            snapshot = local;
            if (cloud != null && (snapshot == null || cloud.Stamp > snapshot.Stamp))
            {
                snapshot = cloud;
            }

            if (snapshot != null && snapshot.Stamp <= ReadEndedStamp())
            {
                snapshot = null;
            }

            return snapshot != null;
        }

        /// <summary>
        /// The account save arrived after the game had already started (the SDK loads
        /// asynchronously). Its run replaces the current one only while the player has not
        /// made a move yet in this session and the account copy is the newer one. True
        /// when the account run was taken over.
        /// </summary>
        public bool OfferCloudRun()
        {
            if (gameManager == null || touchedThisSession || !gameManager.IsPlaying || TutorialProgress.IsActive
                || gameManager.Mode != GameMode.Endless)
            {
                return false;
            }

            if (!TryReadCloudRun(out RunSnapshot cloud) || cloud.Stamp <= runStamp || cloud.Stamp <= ReadEndedStamp())
            {
                return false;
            }

            if (gameManager.ResumeRun(cloud))
            {
                return true;
            }

            // A half-restored board is worse than the fresh run the player just got.
            gameManager.StartNewGame();
            return false;
        }

        /// <summary>Saves a pending change now and pushes the account copy out. Call when the tab goes away.</summary>
        public void FlushNow()
        {
            SaveIfDirty();
            CloudStore?.Flush();
        }

        private void LateUpdate() => SaveIfDirty();

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                FlushNow();
            }
        }

        private void OnApplicationQuit() => FlushNow();

        private void SaveIfDirty()
        {
            if (!dirty || !CanSave())
            {
                return;
            }

            // The first tray drops in a frame after the run starts; the empty moment
            // in between is not a state worth keeping.
            if (spawner.RemainingCount == 0)
            {
                return;
            }

            dirty = false;
            RunSnapshot snapshot = Capture();
            string body = RunSnapshotCodec.Encode(WithStamp(snapshot, 0));
            if (body == null || body == lastSavedBody)
            {
                return;
            }

            // Strictly increasing, so the newest copy always wins even within one millisecond.
            long stamp = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), lastSavedStamp + 1);
            string data = RunSnapshotCodec.Encode(WithStamp(snapshot, stamp));
            if (data == null)
            {
                return;
            }

            lastSavedBody = body;
            lastSavedStamp = stamp;
            runStamp = stamp;

            PlayerPrefs.SetString(RunKey, data);
            PlayerPrefs.Save();
            CloudStore?.WriteRun(data);
        }

        private bool CanSave()
        {
            // A level is never saved: its attempt burns, and the saved endless run must stay untouched.
            return gameManager != null
                && gameManager.Mode == GameMode.Endless
                && (gameManager.IsPlaying || gameManager.IsPaused)
                && grid != null && grid.Model != null
                && spawner != null
                && score != null
                && !TutorialProgress.IsActive;
        }

        private RunSnapshot Capture()
        {
            IReadOnlyList<BlockShape> shapes = spawner.PeekShapes();
            var tray = new string[shapes.Count];
            for (int i = 0; i < shapes.Count; i++)
            {
                tray[i] = shapes[i] != null ? shapes[i].DisplayName : null;
            }

            return new RunSnapshot
            {
                Board = grid.CaptureBoard(),
                Tray = tray,
                Score = score.Score,
                Combo = score.ComboStreak,
                DryMoves = score.MovesWithoutClear,
                RunStartRecord = score.RunStartRecord,
                ContinueUsed = boosters != null && boosters.ContinueUsed,
                FreeCharge = boosters != null ? boosters.FreeCharge : null,
                FreeThreshold = boosters != null ? boosters.LastGrantedThreshold : 0
            };
        }

        private static RunSnapshot WithStamp(RunSnapshot snapshot, long stamp)
        {
            snapshot.Stamp = stamp;
            return snapshot;
        }

        private static bool TryReadCloudRun(out RunSnapshot snapshot)
        {
            snapshot = null;
            string text = CloudStore?.ReadRun();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (RunSnapshotCodec.TryDecode(text, out snapshot))
            {
                return true;
            }

            Debug.LogWarning("[RunSave] Cloud run is unreadable; ignoring it.");
            return false;
        }

        private static long ReadEndedStamp()
        {
            string text = PlayerPrefs.GetString(EndedStampKey, string.Empty);
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long stamp) ? stamp : 0L;
        }

        private void MarkDirty() => dirty = true;

        private void HandleShapePlaced(PlacementResult result)
        {
            touchedThisSession = true;
            dirty = true;
        }

        private void HandleBoosterUsed()
        {
            touchedThisSession = true;
            dirty = true;
        }

        private void HandleScoreChanged(int value) => dirty = true;

        private void HandleStateChanged(GameState state) => dirty = true;

        private void Subscribe()
        {
            Unsubscribe();

            if (gameManager != null)
            {
                gameManager.StateChanged += HandleStateChanged;
            }

            if (grid != null)
            {
                grid.ShapePlaced += HandleShapePlaced;
            }

            if (spawner != null)
            {
                spawner.ShapesChanged += MarkDirty;
            }

            if (score != null)
            {
                score.ScoreChanged += HandleScoreChanged;
            }

            if (boosters != null)
            {
                boosters.RunChanged += HandleBoosterUsed;
            }
        }

        private void Unsubscribe()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
            }

            if (grid != null)
            {
                grid.ShapePlaced -= HandleShapePlaced;
            }

            if (spawner != null)
            {
                spawner.ShapesChanged -= MarkDirty;
            }

            if (score != null)
            {
                score.ScoreChanged -= HandleScoreChanged;
            }

            if (boosters != null)
            {
                boosters.RunChanged -= HandleBoosterUsed;
            }
        }
    }
}
