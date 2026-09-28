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

        /// <summary>Top of the daily tasks block, right under the title.</summary>
        private const float QuestsTop = -150f;

        /// <summary>Height of the sound / resume / restart / home stack at the bottom, plus a gap.</summary>
        private const float ButtonStackHeight = 610f;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button soundButton;

        private Button homeButton;
        private Button settingsButton;

        private TMP_Text soundLabel;
        private AudioManager audioManager;
        private DailyQuestsView questsView;
        private float cardFitScale = 1f;

        private void Awake()
        {
            ResolveCard();
            ResolveSoundButton();
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
            audioManager = gameManager != null ? gameManager.Audio : null;
            if (audioManager == null)
            {
                audioManager = FindObjectOfType<AudioManager>(true);
            }

            if (gameManager == null)
            {
                return;
            }

            ResolveSoundButton();
            ResolveCard();
            ResolveHomeButton();
            ResolveSettingsButton();
            questsView = DailyQuestsView.Ensure(card);
            HideLegacyShopButton();
            gameManager.StateChanged += HandleStateChanged;

            Listen(pauseButton, HandlePauseClicked);
            Listen(resumeButton, HandleResumeClicked);
            Listen(restartButton, HandleRestartClicked);
            Listen(homeButton, HandleHomeClicked);
            Listen(settingsButton, HandleSettingsClicked);
            Listen(soundButton, HandleSoundClicked);
            GameLocalization.LanguageChanged += HandleLanguageChanged;

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
            GameLocalization.LanguageChanged -= HandleLanguageChanged;
            audioManager = null;
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

        private void HandleRestartClicked() => gameManager?.RestartGame();

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

        private void HandleSoundClicked()
        {
            if (audioManager == null)
            {
                audioManager = FindObjectOfType<AudioManager>(true);
            }

            if (audioManager == null)
            {
                return;
            }

            audioManager.SetMuted(!audioManager.IsMuted);
            RefreshSoundLabel();
        }

        private void RefreshSoundLabel()
        {
            if (soundLabel == null && soundButton != null)
            {
                soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (soundLabel == null)
            {
                return;
            }

            bool muted = audioManager != null && audioManager.IsMuted;
            soundLabel.text = muted ? GameLocalization.SoundOff : GameLocalization.SoundOn;
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
            RefreshSoundLabel();
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
                RefreshSoundLabel();
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

            cardFitScale = Mathf.Max(0.5f, fit);
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
                34f);
            soundLabel = soundButton.GetComponentInChildren<TMP_Text>(true);
            LayoutPauseButtons();
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
                38f);
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
                new Vector2(96f, 96f));

            Image gear = UIFactory.CreateImage("Gear", settingsButton.transform, GameTheme.TextPrimary, false);
            gear.sprite = MenuArt.GearSprite;
            gear.preserveAspect = true;
            gear.raycastTarget = false;
            UIFactory.Anchor(
                gear.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
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

            // Bottom-up: home, restart, resume, sound.
            PlaceStackButton(homeButton, 50f, 100f);
            PlaceStackButton(restartButton, 170f, 120f);
            PlaceStackButton(resumeButton, 310f, 130f);
            PlaceStackButton(soundButton, 460f, 100f);
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
