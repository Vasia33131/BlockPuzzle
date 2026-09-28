using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Overlay shown when no move is left. Waits a moment so the player sees the jammed
    /// board, then pops a card that pushes toward one more run: the distance to the record
    /// with a progress bar, or a "new best" celebration with confetti. Buttons are ranked:
    /// the rewarded continue (once per run) is the brightest, "play again" is large, the
    /// optional Yandex sign-in prompt is secondary. Platform code toggles the sign-in prompt
    /// and listens to <see cref="ContinueRequested"/> without a YG dependency here.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        /// <summary>Pause before the card appears, so the filled board stays readable.</summary>
        private const float RevealDelay = 0.7f;
        private const float ShowDuration = 0.3f;
        private const float CountDuration = 0.8f;
        private const float RecordCountDuration = 1.2f;

        private const float CardWidth = 840f;
        /// <summary>Title, score, the record / progress slot and the coins line, measured from the card top.</summary>
        private const float TopBlockHeight = 556f;
        private const float BottomPadding = 32f;
        private const float TopPaddingBelowButtons = 28f;

        private static readonly Vector2 ContinueSize = new Vector2(640f, 140f);
        private static readonly Vector2 RestartSize = new Vector2(600f, 120f);
        private static readonly Vector2 HomeSize = new Vector2(440f, 84f);
        private static readonly Vector2 AuthButtonSize = new Vector2(440f, 80f);
        private static readonly Vector2 AuthHintSize = new Vector2(720f, 64f);
        private static readonly Vector2 ProgressTrackSize = new Vector2(600f, 26f);
        private static readonly Color DarkLabel = GameTheme.FromHex("#1a1a2e");

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text finalScoreValue;
        [SerializeField] private TMP_Text bestScoreValue;
        [SerializeField] private TMP_Text recordBadge;
        [SerializeField] private Button restartButton;

        private Button homeButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button authButton;
        [SerializeField] private TMP_Text authHint;

        private TMP_Text progressLabel;
        private TMP_Text coinsLabel;
        private Image progressTrack;
        private RectTransform progressFill;
        private Image continueGlow;
        private ConfettiBurst confetti;

        private readonly object revealKey = new object();
        private Coroutine countRoutine;
        private bool authPromptVisible;
        private bool continueButtonVisible;
        private bool revealed;
        private float cardFitScale = 1f;

        private int shownScore;
        private int shownBest;
        private bool shownRecord;
        private int displayedScore;

        /// <summary>Raised when the player taps the in-game authorization button.</summary>
        public event Action AuthRequested;

        /// <summary>Raised when the player taps continue. Platform code shows the rewarded ad.</summary>
        public event Action ContinueRequested;

        private void Awake()
        {
            EnsureElements();
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
            EnsureElements();
            GameLocalization.LanguageChanged += HandleLanguageChanged;
            RefreshLocalizedTexts();

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

            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(HandleHomeClicked);
                homeButton.onClick.AddListener(HandleHomeClicked);
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

            Hide();
            SetAuthPromptVisible(false);
            RefreshContinueButton();
        }

        /// <summary>
        /// Shows or hides the authorization button and its explanation.
        /// Platform code calls this when the player is not authorized on Yandex Games.
        /// </summary>
        public void SetAuthPromptVisible(bool visible)
        {
            EnsureElements();
            authPromptVisible = visible;

            if (authHint != null)
            {
                authHint.gameObject.SetActive(visible);
            }

            if (authButton != null)
            {
                authButton.gameObject.SetActive(visible);
            }

            ApplyLayout();
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

            if (homeButton != null)
            {
                homeButton.onClick.RemoveListener(HandleHomeClicked);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
            }

            if (authButton != null)
            {
                authButton.onClick.RemoveListener(HandleAuthClicked);
            }

            GameLocalization.LanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged() => RefreshLocalizedTexts();

        private void RefreshLocalizedTexts()
        {
            ResolveCard();
            if (card != null)
            {
                UIFactory.SetText(card.Find("Title")?.GetComponent<TMP_Text>(), GameLocalization.GameOverTitle);
                UIFactory.SetText(card.Find("ScoreCaption")?.GetComponent<TMP_Text>(), GameLocalization.ScoreCaption);
                UIFactory.SetText(card.Find("BestCaption")?.GetComponent<TMP_Text>(), GameLocalization.BestCaption);
            }

            if (recordBadge != null)
            {
                recordBadge.text = GameLocalization.NewBest;
            }

            if (authHint != null)
            {
                authHint.text = GameLocalization.AuthHint;
            }

            StyleButtons();
            UpdateProgress(displayedScore);
            if (revealed)
            {
                RefreshCoins();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    Hide();
                    SetAuthPromptVisible(false);
                    RefreshContinueButton();
                    break;

                case GameState.MainMenu:
                    Hide();
                    SetAuthPromptVisible(false);
                    break;

                case GameState.GameOver:
                    BeginReveal();
                    break;
            }
        }

        /// <summary>
        /// Keeps the card hidden for <see cref="RevealDelay"/> so the player can see the
        /// board that ran out of room. Input is already blocked by the invisible overlay.
        /// </summary>
        private void BeginReveal()
        {
            Hide();
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }

            GameTween.Delay(revealKey, RevealDelay, true, () =>
            {
                if (gameManager == null || gameManager.State == GameState.GameOver)
                {
                    Show();
                }
            });
        }

        private void Show()
        {
            EnsureElements();
            ScoreManager score = gameManager != null ? gameManager.Score : null;
            shownScore = score != null ? score.Score : 0;
            shownBest = score != null ? score.BestScore : 0;
            bool record = gameManager != null && gameManager.GameOver != null
                ? gameManager.GameOver.WasRecord
                : score != null && score.IsNewRecord;
            shownRecord = shownScore > 0 && record;
            displayedScore = 0;

            RefreshLocalizedTexts();

            if (finalScoreValue != null)
            {
                GameTween.Kill(finalScoreValue);
                GameTween.Kill(finalScoreValue.transform);
                finalScoreValue.transform.localScale = Vector3.one;
                finalScoreValue.color = GameTheme.TextPrimary;
                finalScoreValue.text = "0";
            }

            if (recordBadge != null)
            {
                GameTween.Kill(recordBadge.transform);
                recordBadge.color = GameTheme.Accent;
                recordBadge.transform.localScale = Vector3.zero;
                recordBadge.gameObject.SetActive(shownRecord);
            }

            bool showProgress = !shownRecord && shownBest > 0;
            SetActive(progressLabel, showProgress);
            SetActive(progressTrack, showProgress);
            SetActive(bestScoreValue, showProgress);
            SetActive(card != null ? card.Find("BestCaption") : null, false);
            if (bestScoreValue != null)
            {
                bestScoreValue.text = GameLocalization.BestPrefix + shownBest;
            }

            UpdateProgress(0);
            RefreshCoins();
            RefreshContinueButton();
            revealed = true;
            AnimateIn(StartCountUp);
        }

        /// <summary>Coins this run paid out (a continued run shows only the new part).</summary>
        private void RefreshCoins()
        {
            if (coinsLabel == null)
            {
                return;
            }

            int coins = MetaProgress.LastRunCoins;
            coinsLabel.gameObject.SetActive(coins > 0);
            coinsLabel.text = GameLocalization.CoinsEarned(coins);
            coinsLabel.color = MetaUi.CoinGold;
        }

        private void HandleRestartClicked()
        {
            gameManager?.RestartGame();
        }

        private void HandleHomeClicked() => gameManager?.OpenMainMenu();

        private void HandleContinueClicked()
        {
            ContinueRequested?.Invoke();
        }

        private void HandleAuthClicked()
        {
            AuthRequested?.Invoke();
        }

        private void Hide()
        {
            GameTween.Kill(revealKey);
            revealed = false;
            StopCountUp();
            if (confetti != null)
            {
                confetti.Stop();
            }
            SetVisible(false);
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
                card.localScale = visible ? Vector3.one * cardFitScale : Vector3.zero;
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
        private void AnimateIn(Action onComplete)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                SetVisible(true);
                onComplete?.Invoke();
                return;
            }

            GameTween.Kill(canvasGroup);
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            if (card == null)
            {
                onComplete?.Invoke();
                return;
            }

            GameTween.Kill(card);
            card.localScale = Vector3.zero;
            GameTween.Scale(
                card, Vector3.one * cardFitScale, ShowDuration, TweenEase.OutBack, unscaled: true, onComplete: onComplete);
        }

        private void StartCountUp()
        {
            StopCountUp();
            if (!revealed)
            {
                return;
            }

            if (!isActiveAndEnabled || !Application.isPlaying)
            {
                FinishCountUp();
                return;
            }

            countRoutine = StartCoroutine(CountUpRoutine());
        }

        private void StopCountUp()
        {
            if (countRoutine != null)
            {
                StopCoroutine(countRoutine);
                countRoutine = null;
            }
        }

        /// <summary>Rolls the score up from zero; the gap to the record shrinks along with it.</summary>
        private IEnumerator CountUpRoutine()
        {
            float duration = shownRecord ? RecordCountDuration : CountDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                int value = Mathf.RoundToInt(shownScore * eased);

                if (value != displayedScore)
                {
                    displayedScore = value;
                    if (finalScoreValue != null)
                    {
                        finalScoreValue.text = value.ToString();
                    }

                    UpdateProgress(value);
                }

                yield return null;
            }

            countRoutine = null;
            FinishCountUp();
        }

        private void FinishCountUp()
        {
            displayedScore = shownScore;
            if (finalScoreValue != null)
            {
                finalScoreValue.text = shownScore.ToString();
            }

            UpdateProgress(shownScore);

            if (shownRecord)
            {
                Celebrate();
            }
        }

        private void Celebrate()
        {
            if (recordBadge != null)
            {
                recordBadge.transform.localScale = Vector3.zero;
                GameTween.Scale(recordBadge.transform, Vector3.one, 0.35f, TweenEase.OutBack, unscaled: true);
            }

            if (finalScoreValue != null)
            {
                GameTween.Tint(finalScoreValue, GameTheme.Accent, 0.25f, unscaled: true);
                GameTween.Punch(finalScoreValue.transform, 0.25f, 0.45f, unscaled: true);
            }

            if (confetti != null && card != null)
            {
                RectTransform layer = (RectTransform)confetti.transform;
                Vector3 world = card.TransformPoint(new Vector3(0f, card.rect.height * 0.5f - 280f, 0f));
                Vector2 origin = layer.InverseTransformPoint(world);
                confetti.Play(origin);
            }
        }

        /// <summary>"To beat your best: N" and the bar, both following the rolling score.</summary>
        private void UpdateProgress(int value)
        {
            if (shownRecord || shownBest <= 0)
            {
                return;
            }

            int gap = Mathf.Max(0, shownBest - value);
            if (progressLabel != null)
            {
                progressLabel.text = gap == 0 && value == shownScore
                    ? GameLocalization.RecordTied
                    : GameLocalization.ToRecord(gap);
            }

            if (progressFill != null)
            {
                float ratio = Mathf.Clamp01((float)value / shownBest);
                progressFill.anchorMin = Vector2.zero;
                progressFill.anchorMax = new Vector2(ratio, 1f);
                progressFill.offsetMin = Vector2.zero;
                // A rounded fill narrower than it is tall looks broken, so it never gets thinner than a dot.
                float minWidth = ProgressTrackSize.y - ratio * ProgressTrackSize.x;
                progressFill.offsetMax = new Vector2(Mathf.Max(0f, minWidth), 0f);
                progressFill.gameObject.SetActive(ratio > 0f);
            }
        }

        /// <summary>Gentle breathing glow behind the continue button to draw the eye to it.</summary>
        private void Update()
        {
            if (!revealed || !continueButtonVisible || continueGlow == null)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * 4f) * 0.5f + 0.5f;
            Color color = GameTheme.ShopBuy;
            color.a = Mathf.Lerp(0.15f, 0.45f, wave);
            continueGlow.color = color;
            continueGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.05f, wave);
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
            EnsureElements();

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

            SetActive(continueGlow, show);
            ApplyLayout();
        }

        /// <summary>
        /// Finds or builds everything the card needs. Older baked prefabs miss the progress
        /// bar, the glow and the confetti layer, so those are added on the fly.
        /// </summary>
        private void EnsureElements()
        {
            ResolveCard();
            if (card == null)
            {
                return;
            }

            if (finalScoreValue == null)
            {
                finalScoreValue = card.Find("ScoreValue")?.GetComponent<TMP_Text>();
            }

            if (bestScoreValue == null)
            {
                bestScoreValue = card.Find("BestValue")?.GetComponent<TMP_Text>();
            }

            if (recordBadge == null)
            {
                recordBadge = card.Find("RecordBadge")?.GetComponent<TMP_Text>();
            }

            if (restartButton == null)
            {
                restartButton = card.Find("RestartButton")?.GetComponent<Button>();
            }

            if (homeButton == null)
            {
                homeButton = card.Find("HomeButton")?.GetComponent<Button>();
            }

            if (homeButton == null)
            {
                homeButton = UIFactory.CreateButton(
                    "HomeButton", card, GameLocalization.Home, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 32f);
            }

            if (authHint == null)
            {
                authHint = card.Find("AuthHint")?.GetComponent<TMP_Text>();
            }

            if (authHint == null)
            {
                TextMeshProUGUI hint = UIFactory.CreateText(
                    "AuthHint", card, GameLocalization.AuthHint, 26f, GameTheme.TextSecondary);
                hint.enableWordWrapping = true;
                authHint = hint;
            }

            if (authButton == null)
            {
                authButton = card.Find("AuthButton")?.GetComponent<Button>();
            }

            if (authButton == null)
            {
                authButton = UIFactory.CreateButton(
                    "AuthButton", card, GameLocalization.SignIn, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 28f);
            }

            if (continueButton == null)
            {
                continueButton = card.Find("ContinueButton")?.GetComponent<Button>();
            }

            if (continueButton == null)
            {
                continueButton = UIFactory.CreateButton(
                    "ContinueButton", card, GameLocalization.ContinueAd, GameTheme.ShopBuy, DarkLabel, 40f);
            }

            if (continueGlow == null)
            {
                continueGlow = card.Find("ContinueGlow")?.GetComponent<Image>();
            }

            if (continueGlow == null)
            {
                continueGlow = UIFactory.CreateImage("ContinueGlow", card, GameTheme.WithAlpha(GameTheme.ShopBuy, 0.3f));
                continueGlow.raycastTarget = false;
                continueGlow.gameObject.SetActive(false);
            }

            // The glow sits right behind the button it highlights.
            int buttonIndex = continueButton.transform.GetSiblingIndex();
            if (continueGlow.transform.GetSiblingIndex() > buttonIndex)
            {
                continueGlow.transform.SetSiblingIndex(buttonIndex);
            }

            if (progressLabel == null)
            {
                progressLabel = card.Find("ProgressLabel")?.GetComponent<TMP_Text>();
            }

            if (progressLabel == null)
            {
                progressLabel = UIFactory.CreateText(
                    "ProgressLabel", card, string.Empty, 40f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
                progressLabel.gameObject.SetActive(false);
            }

            if (coinsLabel == null)
            {
                coinsLabel = card.Find("CoinsLabel")?.GetComponent<TMP_Text>();
            }

            if (coinsLabel == null)
            {
                coinsLabel = UIFactory.CreateText(
                    "CoinsLabel", card, string.Empty, 38f, MetaUi.CoinGold, TextAlignmentOptions.Center, FontStyles.Bold);
                coinsLabel.gameObject.SetActive(false);
            }

            if (progressTrack == null)
            {
                progressTrack = card.Find("ProgressTrack")?.GetComponent<Image>();
            }

            if (progressTrack == null)
            {
                progressTrack = UIFactory.CreateImage("ProgressTrack", card, GameTheme.ButtonSecondary);
                progressTrack.raycastTarget = false;
                progressTrack.gameObject.SetActive(false);
            }

            if (progressFill == null)
            {
                progressFill = progressTrack.transform.Find("Fill") as RectTransform;
            }

            if (progressFill == null)
            {
                Image fill = UIFactory.CreateImage("Fill", progressTrack.transform, GameTheme.Accent);
                fill.raycastTarget = false;
                progressFill = fill.rectTransform;
            }

            if (confetti == null)
            {
                confetti = GetComponentInChildren<ConfettiBurst>(true);
            }

            if (confetti == null && Application.isPlaying)
            {
                confetti = ConfettiBurst.Create(transform);
            }

            ApplyLayout();
        }

        private void StyleButtons()
        {
            StyleButton(restartButton, GameLocalization.PlayAgain, GameTheme.Accent, DarkLabel, 46f);
            StyleButton(homeButton, GameLocalization.Home, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 32f);
            StyleButton(authButton, GameLocalization.SignIn, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 28f);
            StyleContinueButton();

            Image fill = progressFill != null ? progressFill.GetComponent<Image>() : null;
            if (fill != null)
            {
                fill.color = GameTheme.Accent;
            }
        }

        private static void StyleButton(Button button, string caption, Color background, Color labelColor, float fontSize)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = background;
            }

            TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                label = button.GetComponentInChildren<TMP_Text>(true);
            }

            if (label != null)
            {
                label.text = caption;
                label.color = labelColor;
                label.fontSize = fontSize;
                GameFonts.Apply(label, FontRole.Heading);
            }
        }

        /// <summary>Bright two-line button: the action on top, what the ad gives underneath.</summary>
        private void StyleContinueButton()
        {
            if (continueButton == null)
            {
                return;
            }

            StyleButton(continueButton, GameLocalization.ContinueAd, GameTheme.ShopBuy, DarkLabel, 40f);

            TMP_Text label = continueButton.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                label = continueButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (label != null)
            {
                UIFactory.Anchor(
                    label.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 20f),
                    new Vector2(ContinueSize.x - 20f, 52f));
            }

            Transform hintTransform = continueButton.transform.Find("Hint");
            TMP_Text hint = hintTransform != null ? hintTransform.GetComponent<TMP_Text>() : null;
            if (hint == null)
            {
                hint = UIFactory.CreateText("Hint", continueButton.transform, string.Empty, 26f, DarkLabel);
            }

            hint.text = GameLocalization.ContinueHint;
            hint.fontSize = 26f;
            hint.fontStyle = FontStyles.Normal;
            hint.color = GameTheme.WithAlpha(DarkLabel, 0.75f);
            UIFactory.Anchor(
                hint.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -28f),
                new Vector2(ContinueSize.x - 20f, 36f));
        }

        /// <summary>
        /// Positions every element. The top block is fixed; the button stack grows from the
        /// bottom (sign-in, play again, continue) and the card height follows it.
        /// </summary>
        private void ApplyLayout()
        {
            ResolveCard();
            if (card == null)
            {
                return;
            }

            PlaceTop(card.Find("Title"), -56f, new Vector2(800f, 100f), 76f);
            PlaceTop(card.Find("ScoreCaption"), -170f, new Vector2(800f, 44f), 34f);
            PlaceTop(finalScoreValue != null ? finalScoreValue.transform : null, -212f, new Vector2(800f, 120f), 112f);
            PlaceTop(recordBadge != null ? recordBadge.transform : null, -350f, new Vector2(800f, 76f), 60f);
            PlaceTop(progressLabel != null ? progressLabel.transform : null, -345f, new Vector2(800f, 52f), 40f);
            PlaceTop(progressTrack != null ? progressTrack.transform : null, -407f, ProgressTrackSize, 0f);
            PlaceTop(bestScoreValue != null ? bestScoreValue.transform : null, -443f, new Vector2(800f, 44f), 30f);
            PlaceTop(coinsLabel != null ? coinsLabel.transform : null, -494f, new Vector2(800f, 50f), 38f);
            if (bestScoreValue != null)
            {
                bestScoreValue.color = GameTheme.TextSecondary;
            }

            float y = BottomPadding;
            PlaceBottom(homeButton != null ? homeButton.transform : null, y, HomeSize);
            y += HomeSize.y + 14f;

            if (authPromptVisible)
            {
                PlaceBottom(authButton != null ? authButton.transform : null, y, AuthButtonSize);
                y += AuthButtonSize.y + 6f;
                PlaceBottom(authHint != null ? authHint.transform : null, y, AuthHintSize);
                y += AuthHintSize.y + 14f;
            }

            PlaceBottom(restartButton != null ? restartButton.transform : null, y, RestartSize);
            y += RestartSize.y;

            if (continueButtonVisible)
            {
                y += 26f;
                PlaceBottom(continueButton != null ? continueButton.transform : null, y, ContinueSize);
                if (continueGlow != null)
                {
                    RectTransform glow = continueGlow.rectTransform;
                    UIFactory.Anchor(
                        glow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, y + ContinueSize.y * 0.5f),
                        ContinueSize + new Vector2(28f, 28f));
                }

                y += ContinueSize.y;
            }

            float height = TopBlockHeight + y + TopPaddingBelowButtons;
            card.sizeDelta = new Vector2(CardWidth, height);
            UpdateFitScale(height);
        }

        /// <summary>Shrinks the card on short (landscape) screens so the buttons stay on screen.</summary>
        private void UpdateFitScale(float cardHeight)
        {
            Rect area = ((RectTransform)transform).rect;
            float fit = 1f;
            if (area.height > 1f && area.width > 1f)
            {
                fit = Mathf.Min(1f, (area.height - 60f) / cardHeight, (area.width - 40f) / CardWidth);
            }

            fit = Mathf.Max(0.5f, fit);
            if (Mathf.Approximately(fit, cardFitScale))
            {
                return;
            }

            cardFitScale = fit;
            if (revealed && card != null)
            {
                GameTween.Kill(card);
                card.localScale = Vector3.one * cardFitScale;
            }
        }

        private static void PlaceTop(Transform target, float y, Vector2 size, float fontSize)
        {
            if (target == null)
            {
                return;
            }

            UIFactory.Anchor((RectTransform)target, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), size);
            TMP_Text text = target.GetComponent<TMP_Text>();
            if (text != null && fontSize > 0f)
            {
                text.fontSize = fontSize;
            }
        }

        private static void PlaceBottom(Transform target, float y, Vector2 size)
        {
            if (target == null)
            {
                return;
            }

            UIFactory.Anchor((RectTransform)target, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y), size);
        }

        private static void SetActive(Component target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }
    }
}
