using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// Feeds the run into <see cref="MetaProgress"/>: daily task progress from the score
    /// events, and the coin and <see cref="PlayerLevel"/> experience payout when a run ends
    /// (Game Over, or a restart from pause).
    /// The tutorial run counts for neither.
    /// </summary>
    public class MetaRunTracker : MonoBehaviour
    {
        private GameManager gameManager;
        private ScoreManager scoreManager;

        public void Configure(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            scoreManager = manager != null ? manager.Score : null;
            if (isActiveAndEnabled)
            {
                Bind();
            }
        }

        private void OnEnable() => Bind();

        private void OnDisable() => Unbind();

        private void Bind()
        {
            Unbind();
            if (gameManager == null)
            {
                return;
            }

            gameManager.StateChanged += HandleStateChanged;
            gameManager.RestartRequested += HandleRestartRequested;
            gameManager.RunStarting += HandleRunStarting;

            if (scoreManager != null)
            {
                scoreManager.ScoreChanged += HandleScoreChanged;
                scoreManager.LinesCleared += HandleLinesCleared;
                scoreManager.BoardCleared += HandleBoardCleared;
            }
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager.RestartRequested -= HandleRestartRequested;
                gameManager.RunStarting -= HandleRunStarting;
            }

            if (scoreManager != null)
            {
                scoreManager.ScoreChanged -= HandleScoreChanged;
                scoreManager.LinesCleared -= HandleLinesCleared;
                scoreManager.BoardCleared -= HandleBoardCleared;
            }
        }

        private static bool Counts => !TutorialProgress.IsActive;

        private void HandleRunStarting()
        {
            MetaProgress.BeginRun();
            PlayerLevel.BeginRun();
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                PayOut();
            }
            else if (state == GameState.MainMenu)
            {
                // A saved run resumed from the menu is not the run that was paid out before.
                PlayerLevel.BeginRun();
            }
        }

        /// <summary>Restart from pause ends the run too; runs before the board is wiped.</summary>
        private void HandleRestartRequested() => PayOut();

        private void PayOut()
        {
            if (!Counts || scoreManager == null)
            {
                return;
            }

            MetaProgress.AwardRunCoins(scoreManager.Score);
            PlayerLevel.AwardRunXp(scoreManager.Score);
        }

        private void HandleScoreChanged(int score)
        {
            // "Score N in one game" is an endless-mode task; a level's points are not a run score.
            if (Counts && gameManager != null && gameManager.IsPlaying && gameManager.Mode == GameMode.Endless)
            {
                MetaProgress.ReportRunScore(score);
            }
        }

        private void HandleLinesCleared(int lines, int points, int combo)
        {
            if (!Counts)
            {
                return;
            }

            MetaProgress.ReportLinesCleared(lines);
            MetaProgress.ReportCombo(combo);
        }

        private void HandleBoardCleared(int bonus)
        {
            if (Counts)
            {
                MetaProgress.ReportBoardCleared();
            }
        }
    }
}
