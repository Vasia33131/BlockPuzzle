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
    /// the rewarded continue (once per run) is one big pulsing green button that names the
    /// ad outright, "start over" is an outlined button that fades in a moment later, "menu"
    /// is plain text and the optional Yandex sign-in prompt is a single compact row. Once the
    /// continue is gone, "start over" takes over the big green look. Platform code toggles the
    /// sign-in prompt and listens to <see cref="ContinueRequested"/> and
    /// <see cref="RestartClicked"/> without a YG dependency here.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        /// <summary>Pause before the card appears, so the filled board stays readable.</summary>
        private const float RevealDelay = 0.7f;
        private const float ShowDuration = 0.3f;
        private const float CountDuration = 0.8f;
        private const float RecordCountDuration = 1.2f;
        /// <summary>The secondary "start over" shows up this long after the card, so the eye lands on continue first.</summary>
        private const float RestartRevealDelay = 0.6f;
        private const float RestartFadeDuration = 0.25f;

        /// <summary>Portrait card takes this share of the panel width; landscape is capped at <see cref="LandscapeCardMaxWidth"/>.</summary>
        private const float PortraitCardShare = 0.9f;
        private const float LandscapeCardMaxWidth = 900f;
        private const float FallbackCardWidth = 900f;
        private const float SidePadding = 40f;
        /// <summary>Title, score, the record / progress slot and the coins line, measured from the card top.</summary>
        private const float TopBlockHeight = 590f;
        private const float BottomPadding = 32f;
        private const float TopPaddingBelowButtons = 28f;
        private const float ButtonGap = 28f;

        private const float PrimaryHeight = 200f;
        private const float RestartOutlineHeight = 150f;
        private const float HomeHeight = 140f;
        private const float HomeWidth = 520f;
        private const float AuthRowHeight = 140f;
        private const float AuthButtonWidth = 300f;
        private const float AuthHintGap = 24f;
        private const float OutlineThickness = 5f;
        private const float IconDiameter = 112f;
        private const float IconLeft = 40f;
        private const float IconTextGap = 32f;
        private const float ContentRightPadding = 36f;
        private const float GlowMargin = 28f;

        private const float PulseSpeed = 2.6f;
        private const float PulseScale = 1.04f;
        private const float SheenPeriod = 3.4f;
        private const float SheenSweepShare = 0.3f;
        private const float SheenBandWidth = 170f;
        /// <summary>Keeps the clipped shine inside the rounded corners of the gradient sprite.</summary>
        private const float SheenInset = 12f;

        private const float ProgressTrackHeight = 26f;
        private const float ProgressTrackMaxWidth = 600f;
        private static readonly Color DarkLabel = GameTheme.FromHex("#1a1a2e");

        private static Sprite gradientSprite;
        private static Sprite sheenSprite;

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
        private RectTransform continueSheen;
        private RectTransform restartSheen;
        private ButtonPressAnimator continuePress;
        private ButtonPressAnimator restartPress;
        private CanvasGroup restartGroup;

        private readonly object revealKey = new object();
        private readonly object restartKey = new object();
        private Coroutine countRoutine;
        private bool authPromptVisible;
        private bool continueButtonVisible;
        private bool highlightVisible;
        private bool revealed;
        private float cardFitScale = 1f;
        private float cardWidth = FallbackCardWidth;
        private float progressTrackWidth = ProgressTrackMaxWidth;
        private Vector2 laidOutArea;

        private int shownScore;
        private int shownBest;
        private bool shownRecord;
        private int displayedScore;

        /// <summary>Raised when the player taps the in-game authorization button.</summary>
        public event Action AuthRequested;

        /// <summary>Raised when the player taps continue. Platform code shows the rewarded ad.</summary>
        public event Action ContinueRequested;

        /// <summary>Raised when the player taps start over; the argument says whether continue was on offer.</summary>
        public event Action<bool> RestartClicked;

        /// <summary>The button that carries the screen: continue while it is on offer, start over after that.</summary>
        private Button PrimaryButton => continueButtonVisible ? continueButton : restartButton;

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
        /// Shows or hides the authorization row (hint and sign-in button).
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
            RevealRestart();
            revealed = true;
            AnimateIn(StartCountUp);
        }

        /// <summary>
        /// With the continue on offer, "start over" fades in after <see cref="RestartRevealDelay"/> and cannot be
        /// tapped before that; without it, "start over" is the main button and is there at once.
        /// </summary>
        private void RevealRestart()
        {
            if (restartButton == null)
            {
                return;
            }

            GameTween.Kill(restartKey);
            if (restartGroup == null)
            {
                restartGroup = restartButton.GetComponent<CanvasGroup>();
                if (restartGroup == null)
                {
                    restartGroup = restartButton.gameObject.AddComponent<CanvasGroup>();
                }
            }

            GameTween.Kill(restartGroup);
            if (!continueButtonVisible)
            {
                SetRestartInteractable(1f, true);
                return;
            }

            SetRestartInteractable(0f, false);
            GameTween.Delay(restartKey, RestartRevealDelay, true, () =>
            {
                if (restartGroup == null)
                {
                    return;
                }

                restartGroup.blocksRaycasts = true;
                restartGroup.interactable = true;
                GameTween.Fade(restartGroup, 1f, RestartFadeDuration, TweenEase.OutQuad, unscaled: true);
            });
        }

        private void SetRestartInteractable(float alpha, bool interactable)
        {
            restartGroup.alpha = alpha;
            restartGroup.blocksRaycasts = interactable;
            restartGroup.interactable = interactable;
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
            RestartClicked?.Invoke(continueButtonVisible);
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
            GameTween.Kill(restartKey);
            if (restartGroup != null)
            {
                GameTween.Kill(restartGroup);
                SetRestartInteractable(1f, true);
            }

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
                float minWidth = ProgressTrackHeight - ratio * progressTrackWidth;
                progressFill.offsetMax = new Vector2(Mathf.Max(0f, minWidth), 0f);
                progressFill.gameObject.SetActive(ratio > 0f);
            }
        }

        /// <summary>
        /// Keeps the layout in step with the screen (rotation), and animates the main button:
        /// a soft scale pulse, a breathing glow behind it and a shine that sweeps across.
        /// </summary>
        private void Update()
        {
            if (!revealed)
            {
                return;
            }

            Vector2 areaSize = ((RectTransform)transform).rect.size;
            if ((areaSize - laidOutArea).sqrMagnitude > 0.25f)
            {
                ApplyLayout();
            }

            Button primary = PrimaryButton;
            if (primary == null)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * PulseSpeed) * 0.5f + 0.5f;
            ButtonPressAnimator press = primary == continueButton ? continuePress : restartPress;
            if (press != null)
            {
                press.SetRestScale(Vector3.one * Mathf.Lerp(1f, PulseScale, wave));
            }

            if (continueGlow != null && highlightVisible)
            {
                Color color = GameTheme.ShopBuy;
                color.a = Mathf.Lerp(0.15f, 0.45f, wave);
                continueGlow.color = color;
                continueGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, PulseScale, wave);
            }

            RectTransform band = primary == continueButton ? continueSheen : restartSheen;
            if (band != null)
            {
                AnimateSheen(band, ((RectTransform)primary.transform).rect.width);
            }
        }

        /// <summary>One diagonal shine crosses the button, then it rests until the next period.</summary>
        private static void AnimateSheen(RectTransform band, float buttonWidth)
        {
            float span = buttonWidth * 0.5f + SheenBandWidth;
            float t = Mathf.Repeat(Time.unscaledTime, SheenPeriod) / SheenPeriod;
            float x = span * 2f;
            if (t < SheenSweepShare)
            {
                x = Mathf.Lerp(-span, span, Mathf.SmoothStep(0f, 1f, t / SheenSweepShare));
            }

            band.anchoredPosition = new Vector2(x, 0f);
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
            highlightVisible = gameManager != null && gameManager.State == GameState.GameOver;
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(show);
            }

            SetActive(continueGlow, highlightVisible);
            StyleButtons();
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
                    "HomeButton", card, GameLocalization.GameOverMenu, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 52f);
            }

            if (authHint == null)
            {
                authHint = card.Find("AuthHint")?.GetComponent<TMP_Text>();
            }

            if (authHint == null)
            {
                TextMeshProUGUI hint = UIFactory.CreateText(
                    "AuthHint", card, GameLocalization.GameOverAuthHint, 34f, GameTheme.TextSecondary);
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
                    "AuthButton", card, GameLocalization.SignInShort, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 52f);
            }

            if (continueButton == null)
            {
                continueButton = card.Find("ContinueButton")?.GetComponent<Button>();
            }

            if (continueButton == null)
            {
                continueButton = UIFactory.CreateButton(
                    "ContinueButton", card, GameLocalization.GameOverContinue, GameTheme.ShopBuy, DarkLabel, 60f);
            }

            if (restartButton != null && restartPress == null)
            {
                restartPress = ButtonPressAnimator.Attach(restartButton);
            }

            if (continuePress == null)
            {
                continuePress = ButtonPressAnimator.Attach(continueButton);
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

            if (progressLabel == null)
            {
                progressLabel = card.Find("ProgressLabel")?.GetComponent<TMP_Text>();
            }

            if (progressLabel == null)
            {
                progressLabel = UIFactory.CreateText(
                    "ProgressLabel", card, string.Empty, 42f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
                progressLabel.gameObject.SetActive(false);
            }

            if (coinsLabel == null)
            {
                coinsLabel = card.Find("CoinsLabel")?.GetComponent<TMP_Text>();
            }

            if (coinsLabel == null)
            {
                coinsLabel = UIFactory.CreateText(
                    "CoinsLabel", card, string.Empty, 42f, MetaUi.CoinGold, TextAlignmentOptions.Center, FontStyles.Bold);
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

        // ---------------------------------------------------------------- styling

        private void StyleButtons()
        {
            if (continuePress != null)
            {
                continuePress.SetRestScale(Vector3.one);
            }

            if (restartPress != null)
            {
                restartPress.SetRestScale(Vector3.one);
            }

            StyleRestartButton();
            StyleHomeButton();
            StyleAuthRow();
            StyleContinueButton();

            Image fill = progressFill != null ? progressFill.GetComponent<Image>() : null;
            if (fill != null)
            {
                fill.color = GameTheme.Accent;
            }
        }

        /// <summary>Big green button while it is the only way forward, outlined and quieter next to the continue.</summary>
        private void StyleRestartButton()
        {
            if (restartButton == null)
            {
                return;
            }

            TMP_Text label = FindLabel(restartButton);
            if (continueButtonVisible)
            {
                ApplyOutlineLook(restartButton);
                SetLabel(label, GameLocalization.GameOverRestart, GameTheme.TextPrimary, 52f, FontRole.Heading);
            }
            else
            {
                restartSheen = ApplyPrimaryLook(restartButton);
                SetLabel(label, GameLocalization.GameOverRestart, DarkLabel, 60f, FontRole.Heading);
            }
        }

        /// <summary>A text-only button: invisible background, a full 140-unit tap zone.</summary>
        private void StyleHomeButton()
        {
            if (homeButton == null)
            {
                return;
            }

            if (homeButton.targetGraphic != null)
            {
                homeButton.targetGraphic.color = new Color(0f, 0f, 0f, 0f);
            }

            SetLabel(
                FindLabel(homeButton),
                GameLocalization.GameOverMenu,
                GameTheme.WithAlpha(GameTheme.TextPrimary, 0.85f),
                52f,
                FontRole.Heading);
        }

        private void StyleAuthRow()
        {
            if (authButton != null)
            {
                if (authButton.targetGraphic != null)
                {
                    authButton.targetGraphic.color = GameTheme.ButtonSecondary;
                }

                SetLabel(
                    FindLabel(authButton), GameLocalization.SignInShort, GameTheme.TextPrimary, 52f, FontRole.Heading);
            }

            if (authHint != null)
            {
                authHint.text = GameLocalization.GameOverAuthHint;
                authHint.color = GameTheme.TextSecondary;
                authHint.fontSize = 34f;
                authHint.enableAutoSizing = false;
                authHint.enableWordWrapping = true;
                authHint.alignment = TextAlignmentOptions.MidlineLeft;
                GameFonts.Apply(authHint, FontRole.Body);
            }
        }

        /// <summary>The main button: play icon, the action, and a caption that says an ad is coming.</summary>
        private void StyleContinueButton()
        {
            if (continueButton == null)
            {
                return;
            }

            continueSheen = ApplyPrimaryLook(continueButton);

            TMP_Text label = FindLabel(continueButton);
            SetLabel(label, GameLocalization.GameOverContinue, DarkLabel, 60f, FontRole.Heading);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
            }

            Transform hintTransform = continueButton.transform.Find("Hint");
            TMP_Text hint = hintTransform != null ? hintTransform.GetComponent<TMP_Text>() : null;
            if (hint == null)
            {
                hint = UIFactory.CreateText("Hint", continueButton.transform, string.Empty, 36f, DarkLabel);
            }

            hint.text = GameLocalization.GameOverContinueCaption;
            hint.enableAutoSizing = false;
            hint.enableWordWrapping = true;
            hint.fontSize = 36f;
            hint.fontStyle = FontStyles.Normal;
            hint.color = GameTheme.WithAlpha(DarkLabel, 0.9f);
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            hint.raycastTarget = false;
            GameFonts.Apply(hint, FontRole.Body);

            EnsureContinueIcon();
        }

        /// <summary>Gradient sprite, shine and no outline. Returns the shine band, or null outside play mode.</summary>
        private static RectTransform ApplyPrimaryLook(Button button)
        {
            Image image = button.targetGraphic as Image;
            if (image == null)
            {
                return null;
            }

            SetOutlineFillActive(button, false);
            if (!Application.isPlaying)
            {
                // The baked sprites are runtime-only, so a prefab bake just gets the flat colour.
                image.color = GameTheme.ShopBuy;
                return null;
            }

            image.sprite = GradientSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;
            return EnsureSheen(button);
        }

        /// <summary>Muted ring around a card-coloured fill: the "not now" look.</summary>
        private static void ApplyOutlineLook(Button button)
        {
            Image image = button.targetGraphic as Image;
            if (image == null)
            {
                return;
            }

            if (UIFactory.RoundedSprite != null)
            {
                image.sprite = UIFactory.RoundedSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            image.color = Color.Lerp(GameTheme.TextSecondary, Color.white, 0.15f);
            Transform sheen = button.transform.Find("Sheen");
            if (sheen != null)
            {
                sheen.gameObject.SetActive(false);
            }

            Transform existing = button.transform.Find("Fill");
            Image fill = existing != null ? existing.GetComponent<Image>() : null;
            if (fill == null)
            {
                fill = UIFactory.CreateImage("Fill", button.transform, GameTheme.CardBackground);
                fill.raycastTarget = false;
                fill.transform.SetAsFirstSibling();
            }

            fill.color = GameTheme.CardBackground;
            UIFactory.Stretch(fill.rectTransform, OutlineThickness);
            fill.gameObject.SetActive(true);
        }

        private static void SetOutlineFillActive(Button button, bool active)
        {
            Transform fill = button.transform.Find("Fill");
            if (fill != null)
            {
                fill.gameObject.SetActive(active);
            }
        }

        /// <summary>Clipped container with a slanted, soft-edged band that <see cref="AnimateSheen"/> slides across.</summary>
        private static RectTransform EnsureSheen(Button button)
        {
            Transform container = button.transform.Find("Sheen");
            if (container == null)
            {
                RectTransform rect = UIFactory.CreateRect("Sheen", button.transform);
                UIFactory.Stretch(rect, SheenInset);
                rect.gameObject.AddComponent<RectMask2D>();
                rect.SetAsFirstSibling();
                container = rect;
            }

            container.gameObject.SetActive(true);

            Transform existing = container.Find("Band");
            Image band = existing != null ? existing.GetComponent<Image>() : null;
            if (band == null)
            {
                band = UIFactory.CreateImage("Band", container, Color.white, false);
                band.raycastTarget = false;
                UIFactory.Anchor(
                    band.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 0f),
                    new Vector2(SheenBandWidth, 640f));
                band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            }

            band.sprite = SheenSprite;
            return band.rectTransform;
        }

        /// <summary>Dark disc with a play triangle on the left of the continue button.</summary>
        private void EnsureContinueIcon()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Transform existing = continueButton.transform.Find("Icon");
            Image disc = existing != null ? existing.GetComponent<Image>() : null;
            if (disc == null)
            {
                disc = UIFactory.CreateImage("Icon", continueButton.transform, DarkLabel, false);
                disc.raycastTarget = false;

                Image play = UIFactory.CreateImage("Play", disc.transform, Color.white, false);
                play.raycastTarget = false;
                UIFactory.Anchor(
                    play.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(4f, 0f),
                    new Vector2(IconDiameter * 0.5f, IconDiameter * 0.5f));
            }

            disc.sprite = HudIcons.Dot;
            disc.color = DarkLabel;
            Transform playTransform = disc.transform.Find("Play");
            Image playImage = playTransform != null ? playTransform.GetComponent<Image>() : null;
            if (playImage != null)
            {
                playImage.sprite = HudIcons.Play;
                playImage.color = Color.white;
            }
        }

        private static TMP_Text FindLabel(Button button)
        {
            TMP_Text label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            return label != null ? label : button.GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>A caption at <paramref name="size"/>; a long translation may shrink to 80% instead of spilling out.</summary>
        private static void SetLabel(TMP_Text label, string text, Color color, float size, FontRole role)
        {
            if (label == null)
            {
                return;
            }

            label.text = text;
            label.color = color;
            GameFonts.Apply(label, role);
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = size * 0.8f;
            label.fontSize = size;
        }

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Positions every element. The top block is fixed; the button stack grows from the
        /// bottom (sign-in row, menu, start over, continue) and the card height follows it.
        /// </summary>
        private void ApplyLayout()
        {
            ResolveCard();
            if (card == null)
            {
                return;
            }

            Rect area = ((RectTransform)transform).rect;
            laidOutArea = area.size;
            cardWidth = ResolveCardWidth(area);
            float inner = cardWidth - SidePadding * 2f;
            progressTrackWidth = Mathf.Min(ProgressTrackMaxWidth, inner);

            Transform title = card.Find("Title");
            PlaceTop(title, -48f, new Vector2(inner, 110f), 96f);
            if (title != null)
            {
                UIFactory.FitText(title.GetComponent<TMP_Text>(), 0.6f);
            }

            PlaceTop(card.Find("ScoreCaption"), -166f, new Vector2(inner, 44f), 34f);
            PlaceTop(finalScoreValue != null ? finalScoreValue.transform : null, -206f, new Vector2(inner, 160f), 140f);
            PlaceTop(recordBadge != null ? recordBadge.transform : null, -372f, new Vector2(inner, 76f), 60f);
            PlaceTop(progressLabel != null ? progressLabel.transform : null, -374f, new Vector2(inner, 56f), 42f);
            PlaceTop(
                progressTrack != null ? progressTrack.transform : null,
                -440f,
                new Vector2(progressTrackWidth, ProgressTrackHeight),
                0f);
            PlaceTop(bestScoreValue != null ? bestScoreValue.transform : null, -478f, new Vector2(inner, 44f), 34f);
            PlaceTop(coinsLabel != null ? coinsLabel.transform : null, -530f, new Vector2(inner, 54f), 42f);
            if (bestScoreValue != null)
            {
                bestScoreValue.color = GameTheme.TextSecondary;
            }

            float y = BottomPadding;
            if (authPromptVisible)
            {
                PlaceAuthRow(y, inner);
                y += AuthRowHeight + ButtonGap;
            }

            PlaceBottom(homeButton != null ? homeButton.transform : null, y, new Vector2(HomeWidth, HomeHeight));
            y += HomeHeight + ButtonGap;

            float restartHeight = continueButtonVisible ? RestartOutlineHeight : PrimaryHeight;
            PlaceBottom(restartButton != null ? restartButton.transform : null, y, new Vector2(inner, restartHeight));
            float primaryY = y;
            float primaryHeight = restartHeight;
            y += restartHeight;

            if (continueButtonVisible)
            {
                y += ButtonGap;
                PlaceBottom(continueButton != null ? continueButton.transform : null, y, new Vector2(inner, PrimaryHeight));
                LayoutContinueContents(inner);
                primaryY = y;
                primaryHeight = PrimaryHeight;
                y += PrimaryHeight;
            }

            PlaceGlow(primaryY, primaryHeight, inner);

            float height = TopBlockHeight + y + TopPaddingBelowButtons;
            card.sizeDelta = new Vector2(cardWidth, height);
            UpdateFitScale(height);
        }

        /// <summary>Portrait: 90% of the panel. Landscape: the same share, but never wider than 900.</summary>
        private static float ResolveCardWidth(Rect area)
        {
            if (area.width < 2f || area.height < 2f)
            {
                return FallbackCardWidth;
            }

            float width = area.width * PortraitCardShare;
            return area.width > area.height ? Mathf.Min(LandscapeCardMaxWidth, width) : width;
        }

        /// <summary>Hint on the left, sign-in button on the right, both as tall as the row.</summary>
        private void PlaceAuthRow(float y, float inner)
        {
            if (authButton != null)
            {
                UIFactory.Anchor(
                    (RectTransform)authButton.transform,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(-SidePadding, y),
                    new Vector2(AuthButtonWidth, AuthRowHeight));
            }

            if (authHint != null)
            {
                UIFactory.Anchor(
                    authHint.rectTransform,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(SidePadding, y),
                    new Vector2(inner - AuthButtonWidth - AuthHintGap, AuthRowHeight));
            }
        }

        /// <summary>Icon on the left, the action above the caption on the right.</summary>
        private void LayoutContinueContents(float width)
        {
            if (continueButton == null)
            {
                return;
            }

            float textLeft = IconLeft + IconDiameter + IconTextGap;
            float textWidth = width - textLeft - ContentRightPadding;
            Vector2 leftMiddle = new Vector2(0f, 0.5f);

            TMP_Text label = FindLabel(continueButton);
            if (label != null)
            {
                UIFactory.Anchor(label.rectTransform, leftMiddle, leftMiddle, new Vector2(textLeft, 34f), new Vector2(textWidth, 72f));
            }

            Transform hint = continueButton.transform.Find("Hint");
            if (hint != null)
            {
                UIFactory.Anchor((RectTransform)hint, leftMiddle, leftMiddle, new Vector2(textLeft, -44f), new Vector2(textWidth, 84f));
            }

            Transform icon = continueButton.transform.Find("Icon");
            if (icon != null)
            {
                UIFactory.Anchor(
                    (RectTransform)icon, leftMiddle, leftMiddle, new Vector2(IconLeft, 0f), new Vector2(IconDiameter, IconDiameter));
            }
        }

        /// <summary>The glow sits right behind whichever button currently carries the screen.</summary>
        private void PlaceGlow(float primaryY, float primaryHeight, float primaryWidth)
        {
            Button primary = PrimaryButton;
            if (continueGlow == null || primary == null)
            {
                return;
            }

            UIFactory.Anchor(
                continueGlow.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, primaryY + primaryHeight * 0.5f),
                new Vector2(primaryWidth + GlowMargin, primaryHeight + GlowMargin));

            if (continueGlow.transform.GetSiblingIndex() > primary.transform.GetSiblingIndex())
            {
                continueGlow.transform.SetSiblingIndex(primary.transform.GetSiblingIndex());
            }
        }

        /// <summary>Shrinks the card on short (landscape) screens so the buttons stay on screen.</summary>
        private void UpdateFitScale(float cardHeight)
        {
            Rect area = ((RectTransform)transform).rect;
            float fit = 1f;
            if (area.height > 1f && area.width > 1f)
            {
                fit = Mathf.Min(1f, (area.height - 60f) / cardHeight, (area.width - 40f) / cardWidth);
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

        // ---------------------------------------------------------------- baked sprites

        /// <summary>Green rounded rect, lighter on top; nine-sliced so the corners stay round at any size.</summary>
        private static Sprite GradientSprite
        {
            get
            {
                if (gradientSprite == null)
                {
                    gradientSprite = BakeGradientSprite();
                }

                return gradientSprite;
            }
        }

        /// <summary>Soft white vertical stripe (transparent at both edges) for the shine.</summary>
        private static Sprite SheenSprite
        {
            get
            {
                if (sheenSprite == null)
                {
                    sheenSprite = BakeSheenSprite();
                }

                return sheenSprite;
            }
        }

        private static Sprite BakeGradientSprite()
        {
            const int size = 96;
            const int radius = 40;
            Color top = Color.Lerp(GameTheme.ShopBuy, Color.white, 0.3f);
            Color bottom = Color.Lerp(GameTheme.ShopBuy, Color.black, 0.2f);

            var texture = NewTexture("GameOverGradient", size, size);
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            {
                // Only the stretched middle rows blend; the corner rows are flat so the ends of a tall button match.
                float blend = Mathf.Clamp01((py + 0.5f - radius) / (size - radius * 2f));
                Color rowColor = Color.Lerp(bottom, top, blend);

                for (int px = 0; px < size; px++)
                {
                    float dx = Mathf.Max(Mathf.Abs(px + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(py + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    Color pixel = rowColor;
                    pixel.a = Mathf.Clamp01(0.5f - distance);
                    pixels[py * size + px] = pixel;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "GameOverGradient";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite BakeSheenSprite()
        {
            const int width = 64;
            var texture = NewTexture("GameOverSheen", width, 4);
            var pixels = new Color[width * 4];
            for (int px = 0; px < width; px++)
            {
                float edge = 1f - Mathf.Abs((px + 0.5f) / width * 2f - 1f);
                float alpha = Mathf.Pow(edge, 1.5f) * 0.45f;
                for (int py = 0; py < 4; py++)
                {
                    pixels[py * width + px] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, 4f), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "GameOverSheen";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Texture2D NewTexture(string name, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }
}
