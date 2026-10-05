using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The pause screen and the HUD button that opens it. Both live here because they are
    /// two halves of the same interaction, and keeping them together means the whole
    /// feature subscribes and unsubscribes in one place.
    ///
    /// Pausing freezes the time scale, so every animation on this panel runs on unscaled
    /// time — otherwise the screen would appear without ever fading in.
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const float PauseCardHeight = 700f;
        private const float PauseCardWidth = 780f;

        private const float ButtonFontSize = 52f;
        private const float ToggleFontSize = ButtonFontSize;
        private const float ButtonHeight = 140f;
        private const float ToggleHeight = ButtonHeight;

        /// <summary>The main button of the screen is taller than the rest.</summary>
        private const float ResumeHeight = 180f;

        private const float ButtonGap = 28f;
        private const float StackBottom = 50f;
        private const float SettingsSize = 140f;

        /// <summary>Top of the daily tasks block, under the title and the settings gear.</summary>
        private const float QuestsTop = -190f;

        /// <summary>Height of the music / sound / resume / restart / home stack at the bottom, plus a gap.</summary>
        private const float ButtonStackHeight =
            StackBottom + ButtonHeight * 4f + ResumeHeight + ButtonGap * 4f + 32f;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button soundButton;

        private Button homeButton;
        private Button settingsButton;
        private Button musicButton;

        private TMP_Text soundLabel;
        private TMP_Text musicLabel;
        private DailyQuestsView questsView;
        private float cardFitScale = 1f;

        private void Awake()
        {
            ResolveCard();
            ResolveSoundButton();
            ResolveMusicButton();
            ResolveHomeButton();
            ResolveSettingsButton();
            HideLegacyShopButton();

            if (gameManager != null)
            {
                Bind(gameManager);
            }
        }

        public void Bind(
            GameManager manager,
            CanvasGroup group,
            RectTransform cardRect,
            Button pause,
            Button resume,
            Button restart,
            Button sound = null)
        {
            canvasGroup = group;
            card = cardRect;
            pauseButton = pause;
            resumeButton = resume;
            restartButton = restart;
            soundButton = sound != null ? sound : soundButton;
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

            ResolveSoundButton();
            ResolveMusicButton();
            ResolveCard();
            ResolveHomeButton();
            ResolveSettingsButton();
            questsView = MetaClock.DailyFeaturesEnabled ? DailyQuestsView.Ensure(card) : HideQuests(card);
            HideLegacyShopButton();
            gameManager.StateChanged += HandleStateChanged;

            Listen(pauseButton, HandlePauseClicked);
            Listen(resumeButton, HandleResumeClicked);
            Listen(restartButton, HandleRestartClicked);
            Listen(homeButton, HandleHomeClicked);
            Listen(settingsButton, HandleSettingsClicked);
            Listen(soundButton, HandleSoundClicked);
            Listen(musicButton, HandleMusicClicked);
            GameLocalization.LanguageChanged += HandleLanguageChanged;
            SoundSettings.Changed += RefreshSoundLabels;

            RefreshLocalizedTexts();
            SetVisible(false);
            HandleStateChanged(gameManager.State);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            pauseButton?.onClick.RemoveListener(HandlePauseClicked);
            resumeButton?.onClick.RemoveListener(HandleResumeClicked);
            restartButton?.onClick.RemoveListener(HandleRestartClicked);
            homeButton?.onClick.RemoveListener(HandleHomeClicked);
            settingsButton?.onClick.RemoveListener(HandleSettingsClicked);
            soundButton?.onClick.RemoveListener(HandleSoundClicked);
            musicButton?.onClick.RemoveListener(HandleMusicClicked);
            GameLocalization.LanguageChanged -= HandleLanguageChanged;
            SoundSettings.Changed -= RefreshSoundLabels;
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void HandlePauseClicked() => gameManager?.SetPaused(true);

        private void HandleResumeClicked() => gameManager?.SetPaused(false);

        /// <summary>Restarting throws the run away, so the player always confirms it first.</summary>
        private void HandleRestartClicked()
        {
            if (gameManager == null)
            {
                return;
            }

            RestartConfirmPanel confirm = FindObjectOfType<RestartConfirmPanel>(true);
            if (confirm != null)
            {
                confirm.Show(gameManager.RestartGame);
                return;
            }

            gameManager.RestartGame();
        }

        /// <summary>
        /// Leaves for the main menu; the unfinished endless run is saved there and can be continued.
        /// A level is not saved, so once it has a move the player is asked before the attempt burns.
        /// </summary>
        private void HandleHomeClicked()
        {
            if (gameManager == null)
            {
                return;
            }

            LevelRunController levelRun = gameManager.LevelRun;
            if (gameManager.Mode == GameMode.Level && levelRun != null && levelRun.HasProgress)
            {
                LevelExitConfirmPanel confirm = FindObjectOfType<LevelExitConfirmPanel>(true);
                if (confirm != null)
                {
                    confirm.Show(gameManager.OpenMainMenu);
                    return;
                }
            }

            gameManager.OpenMainMenu();
        }

        private void HandleSettingsClicked() => SettingsPanel.OpenIfAvailable();

        private void HandleSoundClicked() => SoundSettings.SetSfxMuted(!SoundSettings.SfxMuted);

        private void HandleMusicClicked() => SoundSettings.SetMusicMuted(!SoundSettings.MusicMuted);

        private void RefreshSoundLabels()
        {
            if (soundLabel == null && soundButton != null)
            {
                soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (musicLabel == null && musicButton != null)
            {
                musicLabel = musicButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (soundLabel != null)
            {
                soundLabel.text = SoundSettings.SfxMuted ? GameLocalization.SoundOff : GameLocalization.SoundOn;
            }

            if (musicLabel != null)
            {
                musicLabel.text = SoundSettings.MusicMuted ? GameLocalization.MusicOff : GameLocalization.MusicOn;
            }
        }

        private void HandleLanguageChanged() => RefreshLocalizedTexts();

        private void RefreshLocalizedTexts()
        {
            ResolveCard();
            if (card != null)
            {
                UIFactory.SetText(card.Find("Title")?.GetComponent<TMP_Text>(), GameLocalization.PauseTitle);
            }

            UIFactory.SetButtonText(resumeButton, GameLocalization.Resume);
            UIFactory.SetButtonText(restartButton, GameLocalization.Restart);
            UIFactory.SetButtonText(homeButton, GameLocalization.Home);
            StyleToggleLabel(FindLabel(resumeButton));
            StyleToggleLabel(FindLabel(restartButton));
            StyleToggleLabel(FindLabel(homeButton));
            RefreshSoundLabels();
        }

        private void HandleStateChanged(GameState state)
        {
            if (pauseButton != null)
            {
                pauseButton.interactable = state == GameState.Playing;
            }

            if (state == GameState.Paused)
            {
                RefreshLocalizedTexts();
                RefreshSoundLabels();
                questsView?.Refresh();
                Show();
            }
            else
            {
                Hide();
            }
        }

        private void Show()
        {
            ResolveCard();
            EnsureCanvasGroup();

            if (canvasGroup == null)
            {
                gameObject.SetActive(true);
                return;
            }

            GameTween.Kill(canvasGroup);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            SfxHub.Play(SfxId.UiOpen);
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            if (card != null)
            {
                UpdateFitScale();
                GameTween.Kill(card);
                card.localScale = Vector3.one * (cardFitScale * 0.85f);
                GameTween.Scale(card, Vector3.one * cardFitScale, ShowDuration, TweenEase.OutBack, unscaled: true);
            }
        }

        /// <summary>The tasks make the card tall; shrink it on short (landscape) screens.</summary>
        private void UpdateFitScale()
        {
            Rect area = ((RectTransform)transform).rect;
            float height = card != null ? card.sizeDelta.y : PauseCardHeight;
            float fit = 1f;
            if (area.height > 1f && area.width > 1f)
            {
                fit = Mathf.Min(1f, (area.height - 60f) / height, (area.width - 40f) / PauseCardWidth);
            }

            cardFitScale = Mathf.Max(0.3f, fit);
        }

        private void Hide()
        {
            ResolveCard();
            EnsureCanvasGroup();

            if (canvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

            // Input is blocked immediately so a click cannot slip through the fade-out.
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (canvasGroup.alpha <= 0f)
            {
                return;
            }

            GameTween.Kill(canvasGroup);
            SfxHub.Play(SfxId.UiClose);
            GameTween.Fade(canvasGroup, 0f, HideDuration, TweenEase.InQuad, unscaled: true);

            if (card != null)
            {
                GameTween.Kill(card);
                GameTween.Scale(card, Vector3.one * (cardFitScale * 0.85f), HideDuration, TweenEase.InQuad, unscaled: true);
            }
        }

        private void SetVisible(bool visible)
        {
            EnsureCanvasGroup();
            ResolveCard();

            if (card != null)
            {
                card.localScale = Vector3.one * (visible ? cardFitScale : cardFitScale * 0.85f);
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

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void ResolveCard()
        {
            if (card == null)
            {
                card = transform.Find("Card") as RectTransform;
            }
        }

        private void ResolveSoundButton()
        {
            if (soundButton == null)
            {
                soundButton = transform.Find("Card/SoundButton")?.GetComponent<Button>();
            }

            if (soundButton != null)
            {
                soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
                StyleToggleLabel(soundLabel);
                return;
            }

            if (card == null)
            {
                return;
            }

            // Older baked prefabs lack the control; build it so mute still works.
            soundButton = UIFactory.CreateButton(
                "SoundButton",
                card,
                GameLocalization.SoundOn,
                GameTheme.ButtonSecondary,
                GameTheme.TextPrimary,
                ToggleFontSize);
            soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
            StyleToggleLabel(soundLabel);
            LayoutPauseButtons();
        }

        /// <summary>The music switch is not in the baked prefab, so it is built when missing.</summary>
        private void ResolveMusicButton()
        {
            if (musicButton == null)
            {
                musicButton = transform.Find("Card/MusicButton")?.GetComponent<Button>();
            }

            if (musicButton == null && card != null)
            {
                musicButton = UIFactory.CreateButton(
                    "MusicButton",
                    card,
                    GameLocalization.MusicOn,
                    GameTheme.ButtonSecondary,
                    GameTheme.TextPrimary,
                    ToggleFontSize);
                LayoutPauseButtons();
            }

            if (musicButton != null)
            {
                musicLabel = musicButton.GetComponentInChildren<TMP_Text>(true);
                StyleToggleLabel(musicLabel);
            }
        }

        private static TMP_Text FindLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        /// <summary>Button captions of the stack: 52 units, shrinking a little rather than running out of the button.</summary>
        private static void StyleToggleLabel(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            label.fontSize = ToggleFontSize;
            UIFactory.FitText(label);
        }

        /// <summary>The home button is not in older baked prefabs, so it is built when missing.</summary>
        private void ResolveHomeButton()
        {
            if (homeButton == null)
            {
                homeButton = transform.Find("Card/HomeButton")?.GetComponent<Button>();
            }

            if (homeButton != null || card == null)
            {
                return;
            }

            homeButton = UIFactory.CreateButton(
                "HomeButton",
                card,
                GameLocalization.Home,
                GameTheme.ButtonSecondary,
                GameTheme.TextPrimary,
                ButtonFontSize);
            LayoutPauseButtons();
        }

        /// <summary>A small gear in the top-right corner of the card that opens the settings panel.</summary>
        private void ResolveSettingsButton()
        {
            if (settingsButton == null)
            {
                settingsButton = transform.Find("Card/SettingsButton")?.GetComponent<Button>();
            }

            if (settingsButton != null || card == null)
            {
                return;
            }

            settingsButton = UIFactory.CreateButton(
                "SettingsButton", card, string.Empty, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 30f);
            UIFactory.Anchor(
                (RectTransform)settingsButton.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(SettingsSize, SettingsSize));

            Image gear = UIFactory.CreateImage("Gear", settingsButton.transform, GameTheme.TextPrimary, false);
            gear.sprite = MenuArt.GearSprite;
            gear.preserveAspect = true;
            gear.raycastTarget = false;
            UIFactory.Anchor(
                gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
        }

        /// <summary>No trusted clock in this build: no daily tasks block on the card (a baked one is hidden).</summary>
        private static DailyQuestsView HideQuests(RectTransform host)
        {
            Transform existing = host != null ? host.Find(DailyQuestsView.ObjectName) : null;
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }

            return null;
        }

        private void HideLegacyShopButton()
        {
            Transform leftover = transform.Find("Card/ShopButton");
            if (leftover != null)
            {
                leftover.gameObject.SetActive(false);
            }

            LayoutPauseButtons();
        }

        private void LayoutPauseButtons()
        {
            if (card != null)
            {
                float height = PauseCardHeight;
                if (questsView != null)
                {
                    // Title, then the daily tasks, then the button stack at the bottom.
                    height = -QuestsTop + DailyQuestsView.BlockHeight + ButtonStackHeight;
                    UIFactory.Anchor(
                        (RectTransform)questsView.transform,
                        new Vector2(0.5f, 1f),
                        new Vector2(0.5f, 1f),
                        new Vector2(0f, QuestsTop),
                        new Vector2(DailyQuestsView.Width, DailyQuestsView.BlockHeight));
                }

                card.sizeDelta = new Vector2(card.sizeDelta.x, height);
            }

            // Bottom-up: home, restart, resume (the main one), sound, music. Every button is at least 140
            // tall and 28 apart from the next one.
            float y = StackBottom;
            PlaceStackButton(homeButton, y, ButtonHeight);
            y += ButtonHeight + ButtonGap;
            PlaceStackButton(restartButton, y, ButtonHeight);
            y += ButtonHeight + ButtonGap;
            PlaceStackButton(resumeButton, y, ResumeHeight);
            y += ResumeHeight + ButtonGap;
            PlaceStackButton(soundButton, y, ToggleHeight);
            y += ToggleHeight + ButtonGap;
            PlaceStackButton(musicButton, y, ToggleHeight);
        }

        private static void PlaceStackButton(Button button, float y, float height)
        {
            if (button == null)
            {
                return;
            }

            UIFactory.Anchor(
                (RectTransform)button.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, y),
                new Vector2(620f, height));
        }
    }
}
