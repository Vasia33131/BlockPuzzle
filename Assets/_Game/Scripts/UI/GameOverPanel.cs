using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Overlay shown when no move is left. Dims the board, displays the final and the
    /// best score and offers a restart.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        /// <summary>Length of the pop-in of the card.</summary>
        private const float ShowDuration = 0.3f;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text finalScoreValue;
        [SerializeField] private TMP_Text bestScoreValue;
        [SerializeField] private TMP_Text recordBadge;
        [SerializeField] private Button restartButton;

        private void Awake()
        {
            ResolveCard();

            if (gameManager != null)
            {
                Bind(gameManager);
            }
        }

        public void Bind(
            GameManager manager,
            CanvasGroup group,
            RectTransform cardRect,
            TMP_Text finalScore,
            TMP_Text bestScore,
            TMP_Text badge,
            Button restart)
        {
            canvasGroup = group;
            card = cardRect;
            finalScoreValue = finalScore;
            bestScoreValue = bestScore;
            recordBadge = badge;
            restartButton = restart;
            Bind(manager);
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;

            if (gameManager == null)
            {
                return;
            }

            gameManager.StateChanged += HandleStateChanged;

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(HandleRestartClicked);
                restartButton.onClick.AddListener(HandleRestartClicked);
            }

            SetVisible(false);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(HandleRestartClicked);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    SetVisible(false);
                    break;

                case GameState.GameOver:
                    Show();
                    break;
            }
        }

        private void Show()
        {
            ScoreManager score = gameManager != null ? gameManager.Score : null;
            int finalScore = score != null ? score.Score : 0;
            int best = score != null ? score.BestScore : 0;

            if (finalScoreValue != null)
            {
                finalScoreValue.text = finalScore.ToString();
            }

            if (bestScoreValue != null)
            {
                bestScoreValue.text = best.ToString();
            }

            if (recordBadge != null)
            {
                bool record = gameManager != null && gameManager.GameOver != null
                    ? gameManager.GameOver.WasRecord
                    : score != null && score.IsNewRecord;

                recordBadge.gameObject.SetActive(finalScore > 0 && record);
            }

            AnimateIn();
        }

        private void HandleRestartClicked()
        {
            gameManager?.RestartGame();
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            ResolveCard();
            GameTween.Kill(canvasGroup);
            GameTween.Kill(card);

            if (card != null)
            {
                card.localScale = visible ? Vector3.one : Vector3.zero;
            }

            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                return;
            }

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        /// <summary>Fades the dimmed background in while the card grows from nothing.</summary>
        private void AnimateIn()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                SetVisible(true);
                return;
            }

            GameTween.Kill(canvasGroup);
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            if (card == null)
            {
                return;
            }

            GameTween.Kill(card);
            card.localScale = Vector3.zero;
            GameTween.Scale(card, Vector3.one, ShowDuration, TweenEase.OutBack, unscaled: true);
        }

        private void ResolveCard()
        {
            if (card == null)
            {
                card = transform.Find("Card") as RectTransform;
            }
        }
    }
}
