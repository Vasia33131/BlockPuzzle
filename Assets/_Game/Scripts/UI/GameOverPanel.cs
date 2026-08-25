using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Overlay shown when no move is left. Dims the board, displays the final and the
    /// best score and offers a restart. Optionally shows a Yandex auth prompt that
    /// Platform code can toggle without taking a YG dependency in this assembly.
    /// A rewarded continue button is offered once per run.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        private const float ShowDuration = 0.3f;
        private const string AuthHintText =
            "Авторизуйтесь, чтобы сохранить результат в таблице лидеров";
        private const string AuthButtonText = "АВТОРИЗОВАТЬСЯ";
        private const string ContinueButtonText = "Продолжить — реклама";
        private const string ContinueHintText = "Уберём 1–2 линии";

        private static readonly Vector2 CardSizeDefault = new Vector2(840f, 780f);
        private static readonly Vector2 CardSizeWithAuth = new Vector2(840f, 960f);
        private static readonly Vector2 CardSizeContinue = new Vector2(840f, 920f);
        private static readonly Vector2 CardSizeContinueWithAuth = new Vector2(840f, 1120f);

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text finalScoreValue;
        [SerializeField] private TMP_Text bestScoreValue;
        [SerializeField] private TMP_Text recordBadge;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button authButton;
        [SerializeField] private TMP_Text authHint;

        private bool authPromptVisible;
        private bool continueButtonVisible;

        /// <summary>Raised when the player taps the in-game authorization button.</summary>
        public event Action AuthRequested;

        /// <summary>Raised when the player taps continue. Platform code shows the rewarded ad.</summary>
        public event Action ContinueRequested;

        private void Awake()
        {
            ResolveCard();
            EnsureAuthPrompt();
            EnsureContinueButton();
            SetAuthPromptVisible(false);
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

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
            Button restart,
            Button auth = null,
            TMP_Text hint = null,
            Button continueBtn = null)
        {
            canvasGroup = group;
            card = cardRect;
            finalScoreValue = finalScore;
            bestScoreValue = bestScore;
            recordBadge = badge;
            restartButton = restart;
            if (auth != null)
            {
                authButton = auth;
            }

            if (hint != null)
            {
                authHint = hint;
            }

            if (continueBtn != null)
            {
                continueButton = continueBtn;
            }

            Bind(manager);
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            EnsureAuthPrompt();
            EnsureContinueButton();

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

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
                continueButton.onClick.AddListener(HandleContinueClicked);
            }

            if (authButton != null)
            {
                authButton.onClick.RemoveListener(HandleAuthClicked);
                authButton.onClick.AddListener(HandleAuthClicked);
            }

            SetVisible(false);
            SetAuthPromptVisible(false);
            RefreshContinueButton();
        }

        /// <summary>
        /// Shows or hides the authorization button and its explanation.
        /// Platform code calls this when the player is not authorized on Yandex Games.
        /// </summary>
        public void SetAuthPromptVisible(bool visible)
        {
            EnsureAuthPrompt();
            authPromptVisible = visible;

            if (authHint != null)
            {
                authHint.gameObject.SetActive(visible);
            }

            if (authButton != null)
            {
                authButton.gameObject.SetActive(visible);
            }

            ApplyCardSize();
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

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
            }

            if (authButton != null)
            {
                authButton.onClick.RemoveListener(HandleAuthClicked);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    SetVisible(false);
                    SetAuthPromptVisible(false);
                    RefreshContinueButton();
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

            RefreshContinueButton();
            AnimateIn();
        }

        private void HandleRestartClicked()
        {
            gameManager?.RestartGame();
        }

        private void HandleContinueClicked()
        {
            ContinueRequested?.Invoke();
        }

        private void HandleAuthClicked()
        {
            AuthRequested?.Invoke();
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

        private void RefreshContinueButton()
        {
            EnsureContinueButton();

            BoosterController boosters = gameManager != null ? gameManager.Boosters : null;
            bool show = gameManager != null
                && gameManager.State == GameState.GameOver
                && boosters != null
                && boosters.CanContinue;

            continueButtonVisible = show;
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(show);
            }

            ApplyCardSize();
        }

        private void EnsureAuthPrompt()
        {
            ResolveCard();
            if (card == null)
            {
                return;
            }

            if (authHint == null)
            {
                authHint = card.Find("AuthHint")?.GetComponent<TMP_Text>();
            }

            if (authButton == null)
            {
                authButton = card.Find("AuthButton")?.GetComponent<Button>();
            }

            if (authHint == null)
            {
                TextMeshProUGUI hint = UIFactory.CreateText(
                    "AuthHint",
                    card,
                    AuthHintText,
                    28f,
                    GameTheme.TextSecondary,
                    TextAlignmentOptions.Center,
                    FontStyles.Normal);
                hint.enableWordWrapping = true;
                authHint = hint;
            }
            else
            {
                authHint.text = AuthHintText;
            }

            if (authButton == null)
            {
                authButton = UIFactory.CreateButton(
                    "AuthButton",
                    card,
                    AuthButtonText,
                    GameTheme.ButtonSecondary,
                    GameTheme.TextPrimary,
                    32f);
            }
            else
            {
                TMP_Text label = authButton.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = AuthButtonText;
                }
            }

            if (restartButton == null)
            {
                restartButton = card.Find("RestartButton")?.GetComponent<Button>();
            }

            if (restartButton != null)
            {
                UIFactory.Anchor(
                    (RectTransform)restartButton.transform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 28f),
                    new Vector2(480f, 100f));
            }

            LayoutAuthPrompt();
        }

        private void EnsureContinueButton()
        {
            ResolveCard();
            if (card == null)
            {
                return;
            }

            if (continueButton == null)
            {
                continueButton = card.Find("ContinueButton")?.GetComponent<Button>();
            }

            if (continueButton == null)
            {
                continueButton = UIFactory.CreateButton(
                    "ContinueButton",
                    card,
                    ContinueButtonText,
                    GameTheme.ButtonSecondary,
                    GameTheme.TextPrimary,
                    32f);
            }

            UIFactory.Anchor(
                (RectTransform)continueButton.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 144f),
                new Vector2(480f, 120f));

            StyleContinueButton();
        }

        private void StyleContinueButton()
        {
            if (continueButton == null)
            {
                return;
            }

            TMP_Text label = continueButton.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                label = continueButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (label != null)
            {
                label.text = ContinueButtonText;
                label.fontSize = 32f;
                label.fontStyle = FontStyles.Bold;
                UIFactory.Anchor(
                    label.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 16f),
                    new Vector2(460f, 44f));
            }

            Transform hintTransform = continueButton.transform.Find("Hint");
            TMP_Text hint = hintTransform != null ? hintTransform.GetComponent<TMP_Text>() : null;
            if (hint == null)
            {
                TextMeshProUGUI created = UIFactory.CreateText(
                    "Hint",
                    continueButton.transform,
                    ContinueHintText,
                    22f,
                    GameTheme.TextSecondary,
                    TextAlignmentOptions.Center,
                    FontStyles.Normal);
                hint = created;
            }
            else
            {
                hint.text = ContinueHintText;
                hint.fontSize = 22f;
                hint.color = GameTheme.TextSecondary;
            }

            UIFactory.Anchor(
                hint.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -22f),
                new Vector2(460f, 32f));
        }

        private void LayoutAuthPrompt()
        {
            float authButtonY = continueButtonVisible ? 280f : 145f;
            float authHintY = continueButtonVisible ? 386f : 250f;

            if (authButton != null)
            {
                UIFactory.Anchor(
                    (RectTransform)authButton.transform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, authButtonY),
                    new Vector2(480f, 90f));
            }

            if (authHint != null)
            {
                UIFactory.Anchor(
                    authHint.rectTransform,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, authHintY),
                    new Vector2(720f, 90f));
            }
        }

        private void ApplyCardSize()
        {
            ResolveCard();
            if (card == null)
            {
                return;
            }

            if (continueButtonVisible && authPromptVisible)
            {
                card.sizeDelta = CardSizeContinueWithAuth;
            }
            else if (continueButtonVisible)
            {
                card.sizeDelta = CardSizeContinue;
            }
            else if (authPromptVisible)
            {
                card.sizeDelta = CardSizeWithAuth;
            }
            else
            {
                card.sizeDelta = CardSizeDefault;
            }

            LayoutAuthPrompt();
        }
    }
}
