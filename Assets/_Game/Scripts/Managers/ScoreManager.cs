using System;
using UnityEngine;
using BlockPuzzle.Grid;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Keeps the current run's score and the all-time best, which is persisted in PlayerPrefs.
    /// The record must also survive a change of device, so the platform layer mirrors it
    /// into the Yandex save on <see cref="BestScoreSaved"/> and feeds the cloud copy back
    /// through <see cref="RestoreBestScore"/>. The running score is saved with the board
    /// by <see cref="RunSaveController"/> and comes back through <see cref="RestoreRun"/>.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public const string BestScoreKey = "BlockPuzzle.BestScore";

        [Header("Scoring rules")]
        // Renamed with the x10 economy on purpose: the scene still carries the old 1/10/5,
        // and those must not override the new defaults.
        [SerializeField] private int blockPoints = 10;
        [SerializeField] private int linePoints = 100;
        [SerializeField] private int comboStepBonus = 50;

        [Tooltip("Moves in a row without a clear that end the combo. The streak survives the ones before.")]
        [SerializeField, Min(1)] private int comboGraceMoves = 3;

        [Tooltip("Bonus for a placement that leaves the whole board empty.")]
        [SerializeField, Min(0)] private int boardClearBonus = 3000;

        private int comboStreak;
        private int movesWithoutClear;
        private int recordAtRunStart;
        private bool recordAnnounced;

        /// <summary>
        /// False while a level is played: its points count towards the level goal only and never
        /// touch the endless record, the "new best" banner or the leaderboard.
        /// </summary>
        public bool TracksRecord { get; set; } = true;

        /// <summary>Current score of the running game.</summary>
        public int Score { get; private set; }

        /// <summary>Highest score ever reached on this device.</summary>
        public int BestScore { get; private set; }

        public int ComboStreak => comboStreak;

        /// <summary>Dry moves made since the last clear; the combo breaks at <see cref="comboGraceMoves"/>.</summary>
        public int MovesWithoutClear => movesWithoutClear;

        /// <summary>True while the running game is ahead of the record it started with.</summary>
        public bool IsNewRecord => Score > recordAtRunStart;

        /// <summary>The record this run has to beat.</summary>
        public int RunStartRecord => recordAtRunStart;

        public event Action<int> ScoreChanged;
        public event Action<int> BestScoreChanged;

        /// <summary>Raised when the record was flushed to storage, so it can be mirrored to the cloud.</summary>
        public event Action<int> BestScoreSaved;

        /// <summary>Raised when lines are cleared: (lines, points awarded, combo streak).</summary>
        public event Action<int, int, int> LinesCleared;

        /// <summary>Raised after <see cref="LinesCleared"/> when a placement emptied the board: (bonus).</summary>
        public event Action<int> BoardCleared;

        /// <summary>
        /// Raised once per run, the moment the score first passes the record the run started
        /// with. A first ever run has no record to beat and stays quiet.
        /// </summary>
        public event Action<int> RecordBroken;

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
            movesWithoutClear = 0;
            Score = 0;
            recordAtRunStart = BestScore;
            recordAnnounced = false;
            ScoreChanged?.Invoke(Score);
            BestScoreChanged?.Invoke(BestScore);
        }

        /// <summary>
        /// Continues a saved run: its score, combo and the record it started with. The
        /// record banner is not replayed if the run had already passed that record.
        /// </summary>
        public void RestoreRun(int score, int combo, int dryMoves, int runStartRecord)
        {
            Score = Mathf.Max(0, score);
            comboStreak = Mathf.Max(0, combo);
            movesWithoutClear = Mathf.Clamp(dryMoves, 0, comboGraceMoves - 1);
            recordAtRunStart = Mathf.Clamp(runStartRecord, 0, BestScore);
            recordAnnounced = recordAtRunStart > 0 && IsNewRecord;
            TryUpdateBestScore();
            ScoreChanged?.Invoke(Score);
            BestScoreChanged?.Invoke(BestScore);
        }

        /// <summary>
        /// Awards points for a placement. Every block is worth <see cref="blockPoints"/>;
        /// complete lines scale quadratically and consecutive clears add a combo bonus. The
        /// combo forgives a few dry moves: it only breaks after <see cref="comboGraceMoves"/>
        /// placements in a row cleared nothing, so a tray spent setting up the next clear
        /// does not cost the streak.
        /// </summary>
        public void RegisterPlacement(PlacementResult result)
        {
            if (!result.Success)
            {
                return;
            }

            int gained = result.BlocksPlaced * blockPoints;

            if (result.LinesCleared > 0)
            {
                comboStreak++;
                movesWithoutClear = 0;
                int lineScore = result.LinesCleared * result.LinesCleared * linePoints;
                int combo = (comboStreak - 1) * comboStepBonus * result.LinesCleared;
                gained += lineScore + combo;
                LinesCleared?.Invoke(result.LinesCleared, lineScore + combo, comboStreak);

                if (result.BoardCleared && boardClearBonus > 0)
                {
                    gained += boardClearBonus;
                    BoardCleared?.Invoke(boardClearBonus);
                }
            }
            else if (++movesWithoutClear >= comboGraceMoves)
            {
                comboStreak = 0;
                movesWithoutClear = 0;
            }

            Add(gained);
        }

        /// <summary>Puts the score and the combo back to an earlier state of a level (undo). The record is not touched.</summary>
        public void RestoreLevelState(int score, int combo, int dryMoves)
        {
            Score = Mathf.Max(0, score);
            comboStreak = Mathf.Max(0, combo);
            movesWithoutClear = Mathf.Clamp(dryMoves, 0, comboGraceMoves - 1);
            ScoreChanged?.Invoke(Score);
        }

        public void Add(int points)
        {
            if (points == 0)
            {
                return;
            }

            Score += points;
            ScoreChanged?.Invoke(Score);

            if (!TracksRecord)
            {
                return;
            }

            TryUpdateBestScore();

            if (!recordAnnounced && recordAtRunStart > 0 && IsNewRecord)
            {
                recordAnnounced = true;
                RecordBroken?.Invoke(Score);
            }
        }

        /// <summary>
        /// Closes the run: if it scored higher than the record it started with, the new
        /// record is written to PlayerPrefs. Returns true when a record was set.
        /// </summary>
        public bool CommitBestScore()
        {
            if (!TracksRecord)
            {
                return false;
            }

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
