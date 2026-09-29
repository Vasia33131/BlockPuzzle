using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The settings popup, opened by the gear of the main menu (<see cref="MainMenuPanel.SettingsRequested"/>)
    /// and by the gear of the pause screen (<see cref="OpenIfAvailable"/>). It holds the music and sound effects switches,
    /// "How to play" (replays the tutorial in a fresh run), the "Our games" links and the game version.
    ///
    /// The links live here and nowhere else: never on the play field or in another popup, no calls
    /// to action in the text, each one opens only from the player's own tap (see <see cref="SocialLinks"/>).
    /// There is no vibration in the game, so there is no vibration switch. The popup animates on
    /// unscaled time because it also opens over the paused game.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public const string ObjectName = "SettingsPanel";

        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const float CardWidth = 780f;
        private const float SocialSize = 140f;
        private const float SocialGap = 44f;

        /// <summary>Every button is at least 140 tall with 28 between them; the closing (main) one is 180.</summary>
        private const float ButtonHeight = 140f;
        private const float CloseHeight = 180f;
        private const float ButtonFont = 52f;
        private const float SmallFont = 34f;

        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

        private static SettingsPanel instance;

        private GameManager gameManager;
        private CanvasGroup canvasGroup;
        private RectTransform card;
        private TMP_Text titleLabel;
        private Button musicButton;
        private Button soundButton;
        private Button howToButton;
        private Button closeButton;
        private RectTransform socialBlock;
        private TMP_Text socialCaption;
        private Button telegramButton;
        private Button youTubeButton;
        private TMP_Text versionLabel;

        private bool visible;
        private GameState stateAtOpen;
        private float cardFitScale = 1f;

        private sealed class DimTap : MonoBehaviour, IPointerClickHandler
        {
            public System.Action Clicked;

            public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
        }

        public bool IsOpen => visible;

        public static SettingsPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            SettingsPanel panel = FindObjectOfType<SettingsPanel>(true);
            if (panel == null)
            {
                if (canvasRect == null)
                {
                    return null;
                }

                RectTransform root = UIFactory.CreateRect(ObjectName, canvasRect);
                UIFactory.Stretch(root);
                panel = root.gameObject.AddComponent<SettingsPanel>();
                panel.Build();
            }

            panel.Bind(manager);
            return panel;
        }

        /// <summary>Opens the panel from anywhere (the pause screen). Does nothing before the panel is built.</summary>
        public static void OpenIfAvailable()
        {
            if (instance != null)
            {
                instance.Open();
            }
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            instance = this;
            gameManager = manager;
            if (gameManager != null)
            {
                gameManager.StateChanged += HandleStateChanged;
            }

            MainMenuPanel.SettingsRequested += Open;
            GameLocalization.LanguageChanged += RefreshTexts;
            SoundSettings.Changed += RefreshSoundLabels;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            Unbind();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            MainMenuPanel.SettingsRequested -= Open;
            GameLocalization.LanguageChanged -= RefreshTexts;
            SoundSettings.Changed -= RefreshSoundLabels;
        }

        /// <summary>A run starting or ending under the popup (How to play, an SDK pause) closes it.</summary>
        private void HandleStateChanged(GameState state)
        {
            if (visible && state != stateAtOpen)
            {
                Hide();
            }
        }

        public void Open()
        {
            if (visible || gameManager == null)
            {
                return;
            }

            stateAtOpen = gameManager.State;
            RefreshTexts();
            transform.SetAsLastSibling();
            visible = true;

            GameTween.Kill(canvasGroup);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            SfxHub.Play(SfxId.UiOpen);
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            UpdateFitScale();
            GameTween.Kill(card);
            card.localScale = Vector3.one * (cardFitScale * 0.85f);
            GameTween.Scale(card, Vector3.one * cardFitScale, ShowDuration, TweenEase.OutBack, unscaled: true);
        }

        public void Hide()
        {
            if (!visible)
            {
                return;
            }

            visible = false;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            GameTween.Kill(canvasGroup);
            SfxHub.Play(SfxId.UiClose);
            GameTween.Fade(canvasGroup, 0f, HideDuration, TweenEase.InQuad, unscaled: true);
            GameTween.Kill(card);
            GameTween.Scale(card, Vector3.one * (cardFitScale * 0.85f), HideDuration, TweenEase.InQuad, unscaled: true);
        }

        private void SetVisible(bool show)
        {
            visible = show;
            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;
            canvasGroup.interactable = show;
        }

        // ---------------------------------------------------------------- clicks

        private void HandleMusicClicked() => SoundSettings.SetMusicMuted(!SoundSettings.MusicMuted);

        private void HandleSoundClicked() => SoundSettings.SetSfxMuted(!SoundSettings.SfxMuted);

        /// <summary>Starts a fresh run with the tutorial staged again. From the menu this replaces a saved run.</summary>
        private void HandleHowToPlayClicked()
        {
            if (gameManager == null)
            {
                return;
            }

            TutorialProgress.RequestReplay();
            Hide();
            gameManager.StartNewGame();
        }

        private void HandleTelegramClicked() => SocialLinks.Open(SocialLinks.Telegram);

        private void HandleYouTubeClicked() => SocialLinks.Open(SocialLinks.YouTube);

        // ---------------------------------------------------------------- content

        private void RefreshTexts()
        {
            UIFactory.SetText(titleLabel, GameLocalization.SettingsTitle);
            UIFactory.SetButtonText(howToButton, GameLocalization.HowToPlay);
            UIFactory.SetButtonText(closeButton, GameLocalization.Close);
            UIFactory.SetText(socialCaption, GameLocalization.OurGames);
            UIFactory.SetText(versionLabel, GameLocalization.VersionLabel(Application.version));
            RefreshSoundLabels();

            // Links are read every time: an empty URL hides its button, no URLs hide the block.
            bool telegram = SocialLinks.TelegramUrl != null;
            bool youTube = SocialLinks.YouTubeUrl != null;
            telegramButton.gameObject.SetActive(telegram);
            youTubeButton.gameObject.SetActive(youTube);
            socialBlock.gameObject.SetActive(telegram || youTube);

            if (visible)
            {
                UpdateFitScale();
                card.localScale = Vector3.one * cardFitScale;
            }
        }

        private void RefreshSoundLabels()
        {
            UIFactory.SetButtonText(musicButton, SoundSettings.MusicMuted ? GameLocalization.MusicOff : GameLocalization.MusicOn);
            UIFactory.SetButtonText(soundButton, SoundSettings.SfxMuted ? GameLocalization.SoundOff : GameLocalization.SoundOn);
        }

        /// <summary>Shrinks the card on short (landscape) screens so it stays on screen.</summary>
        private void UpdateFitScale()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            Rect area = ((RectTransform)transform).rect;
            float fit = 1f;
            if (area.height > 1f && area.width > 1f)
            {
                fit = Mathf.Min(1f, (area.height - 60f) / card.rect.height, (area.width - 40f) / CardWidth);
            }

            cardFitScale = Mathf.Max(0.5f, fit);
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            Image dim = UIFactory.CreateImage("Dim", transform, new Color(0.03f, 0.03f, 0.08f, 0.78f), false);
            UIFactory.Stretch(dim.rectTransform);

            // A tap outside the card closes it. Not a Button on purpose: ButtonPressAnimator.AttachAll would squash it.
            dim.gameObject.AddComponent<DimTap>().Clicked = Hide;

            Image cardImage = UIFactory.CreateImage("Card", transform, GameTheme.CardBackground);
            card = cardImage.rectTransform;
            UIFactory.Anchor(card, Half, Half, Vector2.zero, new Vector2(CardWidth, 100f));

            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(80, 80, 44, 44);
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = UIFactory.CreateText(
                "Title", card, GameLocalization.SettingsTitle, 60f, GameTheme.TextPrimary, TextAlignmentOptions.Center,
                FontStyles.Bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 36f;
            title.fontSizeMax = 60f;
            SetHeight(title.rectTransform, 84f);
            titleLabel = title;

            musicButton = CreateButton("MusicButton", GameLocalization.MusicOn, GameTheme.ButtonSecondary, GameTheme.TextPrimary, ButtonFont, ButtonHeight);
            soundButton = CreateButton("SoundButton", GameLocalization.SoundOn, GameTheme.ButtonSecondary, GameTheme.TextPrimary, ButtonFont, ButtonHeight);
            howToButton = CreateButton("HowToPlayButton", GameLocalization.HowToPlay, GameTheme.ButtonSecondary, GameTheme.TextPrimary, ButtonFont, ButtonHeight);

            BuildSocialBlock();

            TextMeshProUGUI version = UIFactory.CreateText(
                "Version", card, string.Empty, SmallFont, GameTheme.TextSecondary, TextAlignmentOptions.Center);
            SetHeight(version.rectTransform, 44f);
            versionLabel = version;

            closeButton = CreateButton("CloseButton", GameLocalization.Close, GameTheme.ShopBuy, GameTheme.ShopBuyLabel, ButtonFont, CloseHeight);

            musicButton.onClick.AddListener(HandleMusicClicked);
            soundButton.onClick.AddListener(HandleSoundClicked);
            howToButton.onClick.AddListener(HandleHowToPlayClicked);
            closeButton.onClick.AddListener(Hide);

            SetVisible(false);
            card.localScale = Vector3.one * 0.85f;
        }

        private void BuildSocialBlock()
        {
            socialBlock = UIFactory.CreateRect("OurGames", card);
            var column = socialBlock.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 14f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            TextMeshProUGUI caption = UIFactory.CreateText(
                "Caption", socialBlock, GameLocalization.OurGames, SmallFont, GameTheme.TextSecondary,
                TextAlignmentOptions.Center, FontStyles.Bold);
            SetHeight(caption.rectTransform, 44f);
            socialCaption = caption;

            RectTransform row = UIFactory.CreateRect("Icons", socialBlock);
            var line = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            line.spacing = SocialGap;
            line.childAlignment = TextAnchor.MiddleCenter;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;
            SetHeight(row, SocialSize);

            telegramButton = SocialIcons.CreateTelegram(row, SocialSize);
            SetSize(telegramButton.transform, SocialSize, SocialSize);
            telegramButton.onClick.AddListener(HandleTelegramClicked);

            float youTubeHeight = SocialSize;
            youTubeButton = SocialIcons.CreateYouTube(row, youTubeHeight);
            SetSize(youTubeButton.transform, SocialSize * 1.25f, youTubeHeight);
            youTubeButton.onClick.AddListener(HandleYouTubeClicked);
        }

        private Button CreateButton(string name, string caption, Color background, Color label, float fontSize, float height)
        {
            Button button = UIFactory.CreateButton(name, card, caption, background, label, fontSize);
            SetHeight(button.transform, height);
            UIFactory.FitText(button.GetComponentInChildren<TMP_Text>(true));
            return button;
        }

        private static void SetHeight(Transform rect, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void SetSize(Transform rect, float width, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }
    }
}
