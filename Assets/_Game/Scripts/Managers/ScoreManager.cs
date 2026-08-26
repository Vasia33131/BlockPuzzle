using System;
using UnityEngine;
using BlockPuzzle.Grid;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Keeps the current run's score and the all-time best, which is persisted in PlayerPrefs.
    /// The record must also survive a change of device, so the platform layer mirrors it
    /// into the Yandex save on <see cref="BestScoreSaved"/> and feeds the cloud copy back
    /// through <see cref="RestoreBestScore"/>. The running board is never saved.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public const string BestScoreKey = "BlockPuzzle.BestScore";

        [Header("Scoring rules")]
        [SerializeField] private int pointsPerBlock = 1;
        [SerializeField] private int pointsPerLine = 10;
        [SerializeField] private int comboBonus = 5;

        private int comboStreak;
        private int recordAtRunStart;

        /// <summary>Current score of the running game.</summary>
        public int Score { get; private set; }

        /// <summary>Highest score ever reached on this device.</summary>
        public int BestScore { get; private set; }

        public int ComboStreak => comboStreak;

        /// <summary>True while the running game is ahead of the record it started with.</summary>
        public bool IsNewRecord => Score > recordAtRunStart;

        public event Action<int> ScoreChanged;
        public event Action<int> BestScoreChanged;

        /// <summary>Raised when the record was flushed to storage, so it can be mirrored to the cloud.</summary>
        public event Action<int> BestScoreSaved;

        /// <summary>Raised when lines are cleared: (lines, points awarded, combo streak).</summary>
        public event Action<int, int, int> LinesCleared;

        private void Awake()
        {
            LoadBestScore();
        }

        public void LoadBestScore()
        {
            BestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            BestScoreChanged?.Invoke(BestScore);
        }

        public void ResetScore()
        {
            comboStreak = 0;
            Score = 0;
            recordAtRunStart = BestScore;
            ScoreChanged?.Invoke(Score);
            BestScoreChanged?.Invoke(BestScore);
        }

        /// <summary>
        /// Awards points for a placement. Every block is worth a point; complete lines
        /// scale quadratically and consecutive clears add a combo bonus.
        /// </summary>
        public void RegisterPlacement(PlacementResult result)
        {
            if (!result.Success)
            {
                return;
            }

            int gained = result.BlocksPlaced * pointsPerBlock;

            if (result.LinesCleared > 0)
            {
                comboStreak++;
                int lineScore = result.LinesCleared * result.LinesCleared * pointsPerLine;
                int combo = (comboStreak - 1) * comboBonus * result.LinesCleared;
                gained += lineScore + combo;
                LinesCleared?.Invoke(result.LinesCleared, lineScore + combo, comboStreak);
            }
            else
            {
                comboStreak = 0;
            }

            Add(gained);
        }

        public void Add(int points)
        {
            if (points == 0)
            {
                return;
            }

            Score += points;
            ScoreChanged?.Invoke(Score);
            TryUpdateBestScore();
        }

        /// <summary>
        /// Closes the run: if it scored higher than the record it started with, the new
        /// record is written to PlayerPrefs. Returns true when a record was set.
        /// </summary>
        public bool CommitBestScore()
        {
            bool record = IsNewRecord;
            TryUpdateBestScore();
            Save();
            return record;
        }

        /// <summary>Writes the best score to disk. Called on game over and when the app pauses.</summary>
        public void Save()
        {
            PlayerPrefs.SetInt(BestScoreKey, BestScore);
            PlayerPrefs.Save();
            BestScoreSaved?.Invoke(BestScore);
        }

        /// <summary>
        /// Raises the record to a value that came from the platform save — the same
        /// account opened on another device. A lower cloud value is ignored.
        /// </summary>
        public void RestoreBestScore(int best)
        {
            if (best <= BestScore)
            {
                return;
            }

            BestScore = best;
            recordAtRunStart = Mathf.Max(recordAtRunStart, best);
            BestScoreChanged?.Invoke(BestScore);
            Save();
        }

        private void TryUpdateBestScore()
        {
            if (Score <= BestScore)
            {
                return;
            }

            BestScore = Score;
            BestScoreChanged?.Invoke(BestScore);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }
    }
}
