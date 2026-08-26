using TMPro;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Top panel of the screen: score on the left, best in the center, and a short
    /// combo popup. Reacts to score events with a small pop animation.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private TMP_Text scoreValue;
        [SerializeField] private TMP_Text bestValue;
        [SerializeField] private TMP_Text comboLabel;

        /// <summary>How long the combo banner stays at full opacity before it fades.</summary>
        private const float ComboHold = 0.55f;

        private const float ComboFade = 0.45f;
        private const float PopDuration = 0.18f;
        private const string ScorePrefix = "СЧЁТ: ";
        private const string BestPrefix = "РЕКОРД: ";

        private void Awake()
        {
            if (scoreManager != null)
            {
                Bind(scoreManager);
            }
        }

        public void Bind(ScoreManager manager, TMP_Text score, TMP_Text best, TMP_Text combo)
        {
            scoreValue = score;
            bestValue = best;
            comboLabel = combo;
            Bind(manager);
        }

        public void Bind(ScoreManager manager)
        {
            Unbind();
            scoreManager = manager;

            if (scoreManager == null)
            {
                return;
            }

            scoreManager.ScoreChanged += HandleScoreChanged;
            scoreManager.BestScoreChanged += HandleBestScoreChanged;
            scoreManager.LinesCleared += HandleLinesCleared;

            FitHudNumber(scoreValue);
            FitHudNumber(bestValue);

            HandleScoreChanged(scoreManager.Score);
            HandleBestScoreChanged(scoreManager.BestScore);
        }

        private static void FitHudNumber(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = Mathf.Max(36f, text.fontSize);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (scoreManager == null)
            {
                return;
            }

            scoreManager.ScoreChanged -= HandleScoreChanged;
            scoreManager.BestScoreChanged -= HandleBestScoreChanged;
            scoreManager.LinesCleared -= HandleLinesCleared;
            scoreManager = null;
        }

        private void HandleScoreChanged(int value)
        {
            if (scoreValue == null)
            {
                return;
            }

            scoreValue.text = ScorePrefix + value;
            GameTween.Punch(scoreValue.rectTransform, 0.18f, PopDuration);
        }

        private void HandleBestScoreChanged(int value)
        {
            if (bestValue != null)
            {
                bestValue.text = BestPrefix + value;
            }
        }

        private void HandleLinesCleared(int lines, int points, int combo)
        {
            if (comboLabel == null || !isActiveAndEnabled)
            {
                return;
            }

            comboLabel.text = combo > 1 ? $"КОМБО x{combo}  +{points}" : $"+{points}";

            Color color = combo > 1 ? GameTheme.Accent : GameTheme.TextPrimary;
            color.a = 1f;

            GameTween.Kill(comboLabel);
            GameTween.Kill(comboLabel.rectTransform);
            comboLabel.color = color;

            GameTween.Fade(comboLabel, 0f, ComboFade, TweenEase.InQuad, ComboHold);
            GameTween.Punch(comboLabel.rectTransform, 0.22f, PopDuration);
        }
    }
}
