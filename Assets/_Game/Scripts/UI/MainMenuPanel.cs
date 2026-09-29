using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Levels;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Full-screen main menu: profile badge, coins, the logo, the best score, the Levels and Endless
    /// buttons, the daily reward gift and the settings button, over a dark blue gradient with drifting
    /// figures. The panel is visible exactly while the game is in <see cref="GameState.MainMenu"/>.
    /// The Endless button says "Continue" while an unfinished run is saved and calls
    /// <see cref="GameManager.StartEndless"/>; Levels opens the <see cref="LevelMapPanel"/> and carries the
    /// level the player is up to as a subtitle ("Level 13").
    ///
    /// Portrait stacks everything in one column; landscape uses two, logo and profile left, buttons
    /// right (re-laid out when <see cref="OrientationHandler"/> reports a new layout). The content sits
    /// inside a <see cref="SafeAreaFitter"/>. Idle motion is driven by one <see cref="Update"/> that
    /// ticks a handful of views, never one loop per particle.
    /// </summary>
    public class MainMenuPanel : MonoBehaviour
    {
        public const string ObjectName = "MainMenuPanel";

        /// <summary>How often the saved-run check, the best score and the gift state are re-read while the menu is up.</summary>
        private const float RefreshInterval = 0.5f;

        /// <summary>
        /// The saved-run check decodes the run text (local and account copy), so it is not part of the
        /// half-second refresh: it runs on open and at this slower pace for the late account save.
        /// </summary>
        private const float SavedRunInterval = 3f;

        private const float IntroDuration = 0.5f;
        private const float IntroStep = 0.07f;
        private const float IntroStartDelay = 0.05f;

        private const float PulseSpeed = 3.2f;
        private const float PulseAmount = 0.035f;
        private const float ShineCycle = 3.4f;
        private const float ShineTravel = 1.1f;

        private const float Margin = 36f;
        private const float SettingsSize = 120f;
        private const float BestOffset = 270f;
        private const float LandscapeLeftColumn = 0.27f;
        private const float LandscapeRightColumn = 0.73f;

        private static readonly Vector2 LogoSize = new Vector2(1000f, 440f);
        private static readonly Vector2 BestSize = new Vector2(500f, 84f);
        private static readonly Vector2 LevelsSize = new Vector2(840f, 200f);
        private static readonly Vector2 EndlessSize = new Vector2(840f, 180f);
        private static readonly Vector2 GlowSize = new Vector2(1500f, 1500f);
        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

        [SerializeField] private GameManager gameManager;
        [SerializeField] private CanvasGroup canvasGroup;

        private readonly List<RectTransform> introOrder = new List<RectTransform>(8);

        private RectTransform glow;
        private Image glowImage;
        private MenuFloaters floaters;
        private MenuLogo logo;
        private RectTransform coinSlot;
        private RectTransform avatarSlot;
        private RectTransform bestSlot;
        private TMP_Text bestLabel;
        private Outline bestOutline;
        private MenuButton settings;
        private MenuButton levels;
        private MenuButton endless;
        private MenuGiftButton gift;
        private DailyRewardPanel dailyPanel;

        private bool built;
        private bool visible;
        private float nextRefresh;
        private float nextSavedRunCheck;
        private float shineHalfTravel;

        private bool shownSaved;
        private int shownBest = -1;
        private bool shownGift;
        private int shownLevelKey = -2;

        /// <summary>True while the menu is on screen and accepts taps. Platform code reports Game Ready on it.</summary>
        public static bool IsInteractive { get; private set; }

        /// <summary>Raised by the settings button. Without a listener the button shows a "coming soon" hint.</summary>
        public static event Action SettingsRequested;

        /// <summary>Builds the menu under <paramref name="parent"/> and binds it to <paramref name="manager"/>.</summary>
        public static MainMenuPanel Create(RectTransform parent, GameManager manager)
        {
            RectTransform root = UIFactory.CreateRect(ObjectName, parent);
            UIFactory.Stretch(root);
            var panel = root.gameObject.AddComponent<MainMenuPanel>();
            panel.Build(root);
            panel.Bind(manager);
            return panel;
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            if (gameManager == null || !built)
            {
                return;
            }

            gameManager.StateChanged += HandleStateChanged;
            GameLocalization.LanguageChanged += HandleLanguageChanged;
            GameTheme.Changed += ApplyTheme;
            MetaProgress.Changed += HandleMetaChanged;
            OrientationHandler.LayoutApplied += HandleLayoutApplied;

            HandleStateChanged(gameManager.State);
        }

        private void OnDestroy()
        {
            Unbind();
            IsInteractive = false;
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            GameLocalization.LanguageChanged -= HandleLanguageChanged;
            GameTheme.Changed -= ApplyTheme;
            MetaProgress.Changed -= HandleMetaChanged;
            OrientationHandler.LayoutApplied -= HandleLayoutApplied;
        }

        private void HandleStateChanged(GameState state) => SetVisible(state == GameState.MainMenu);

        private void HandleLanguageChanged()
        {
            RefreshTexts();
            RefreshDynamic(true);
        }

        private void HandleMetaChanged()
        {
            if (visible)
            {
                RefreshDynamic(false);
            }
        }

        private void HandleLayoutApplied(bool portrait) => ApplyLayout(portrait);

        // ---------------------------------------------------------------- clicks

        private void HandlePlayClicked() => gameManager?.StartEndless();

        private void HandleLevelsClicked()
        {
            LevelMapPanel map = LevelMapPanel.Instance != null ? LevelMapPanel.Instance : FindObjectOfType<LevelMapPanel>(true);
            if (map != null)
            {
                map.Open();
            }
            else
            {
                MetaToast.ShowText(GameLocalization.ComingSoon);
            }
        }

        private void HandleSettingsClicked()
        {
            if (SettingsRequested != null)
            {
                SettingsRequested.Invoke();
            }
            else
            {
                MetaToast.ShowText(GameLocalization.ComingSoon);
            }
        }

        private void HandleGiftClicked()
        {
            if (dailyPanel == null)
            {
                dailyPanel = FindObjectOfType<DailyRewardPanel>(true);
            }

            dailyPanel?.Open();
        }

        // ---------------------------------------------------------------- visibility

        private void SetVisible(bool show)
        {
            bool wasVisible = visible;
            visible = show;
            IsInteractive = show;

            if (show)
            {
                ApplyLayout(Screen.width <= Screen.height);
                ApplyTheme();
                RefreshTexts();
                RefreshDynamic(true);
                nextRefresh = Time.unscaledTime + RefreshInterval;
                if (!wasVisible)
                {
                    PlayIntro();
                }
            }
            else
            {
                StopIntro();
            }

            canvasGroup.alpha = show ? 1f : 0f;
            canvasGroup.blocksRaycasts = show;
            canvasGroup.interactable = show;
        }

        /// <summary>Every element pops in from nothing, one after another (unscaled time, OutBack).</summary>
        private void PlayIntro()
        {
            for (int i = 0; i < introOrder.Count; i++)
            {
                RectTransform slot = introOrder[i];
                GameTween.Kill(slot);
                slot.localScale = Vector3.zero;
                GameTween.Scale(
                    slot,
                    Vector3.one,
                    IntroDuration,
                    TweenEase.OutBack,
                    IntroStartDelay + i * IntroStep,
                    unscaled: true);
            }
        }

        private void StopIntro()
        {
            for (int i = 0; i < introOrder.Count; i++)
            {
                GameTween.Kill(introOrder[i]);
                introOrder[i].localScale = Vector3.one;
            }
        }

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (!visible)
            {
                return;
            }

            float time = Time.unscaledTime;
            logo.Tick(time);
            floaters.Tick(time);
            gift.Tick(time);
            TickLevelsButton(time);

            if (time >= nextRefresh)
            {
                // The account save (run, best score, meta) can arrive after the menu is already shown.
                nextRefresh = time + RefreshInterval;
                RefreshDynamic(false);
            }
        }

        /// <summary>The main button breathes, and a highlight sweeps across it now and then.</summary>
        private void TickLevelsButton(float time)
        {
            float pulse = 1f + Mathf.Sin(time * PulseSpeed) * PulseAmount;
            levels.Visual.localScale = new Vector3(pulse, pulse, 1f);

            float progress = Mathf.Clamp01(Mathf.Repeat(time, ShineCycle) / ShineTravel);
            levels.Shine.anchoredPosition = new Vector2(Mathf.Lerp(-shineHalfTravel, shineHalfTravel, progress), 0f);
        }

        // ---------------------------------------------------------------- content

        private void RefreshTexts()
        {
            logo.Refresh();
            levels.SetText(GameLocalization.Levels);
            gift.RefreshText();
        }

        /// <summary>Re-reads what can change while the menu is open; texts are only rewritten when a value changed.</summary>
        private void RefreshDynamic(bool force)
        {
            if (gameManager == null)
            {
                return;
            }

            bool saved = shownSaved;
            if (force || Time.unscaledTime >= nextSavedRunCheck)
            {
                nextSavedRunCheck = Time.unscaledTime + SavedRunInterval;
                saved = gameManager.HasSavedRun;
            }

            if (force || saved != shownSaved)
            {
                shownSaved = saved;
                endless.SetText(saved ? GameLocalization.Resume : GameLocalization.MenuEndless);
            }

            int best = gameManager.Score != null ? gameManager.Score.BestScore : 0;
            if (force || best != shownBest)
            {
                shownBest = best;
                bestSlot.gameObject.SetActive(best > 0);
                bestLabel.text = GameLocalization.BestPrefix + best;
            }

            // -1: every level passed. The map opens on the same level the caption names.
            int levelKey = LevelProgress.IsCampaignComplete ? -1 : LevelProgress.Unlocked;
            if (force || levelKey != shownLevelKey)
            {
                shownLevelKey = levelKey;
                levels.SetSubtitle(levelKey < 0 ? GameLocalization.LevelsAllDone : GameLocalization.LevelTitle(levelKey));
            }

            bool giftReady = MetaProgress.CanClaimDaily;
            if (force || giftReady != shownGift)
            {
                shownGift = giftReady;
                gift.SetAvailable(giftReady);
            }
        }

        /// <summary>The menu stays bright in every theme; only the glow and the record plate borrow the accent.</summary>
        private void ApplyTheme()
        {
            glowImage.color = GameTheme.WithAlpha(Color.Lerp(MenuArt.GlowBase, GameTheme.Accent, 0.35f), 0.7f);
            bestOutline.effectColor = GameTheme.WithAlpha(GameTheme.Accent, 0.85f);
        }

        // ---------------------------------------------------------------- layout

        /// <summary>Portrait: one column. Landscape: logo and profile on the left, buttons on the right.</summary>
        private void ApplyLayout(bool portrait)
        {
            if (!built)
            {
                return;
            }

            float banner = GameTheme.ActiveBannerReserve;
            // The balance always sits top-left, the settings gear top-right.
            Place(coinSlot, 0f, 1f, 0f, 1f, Margin, -Margin - 12f, CoinCounterView.Size);
            Place(settings.Slot, 1f, 1f, 1f, 1f, -Margin, -Margin, new Vector2(SettingsSize, SettingsSize));

            if (portrait)
            {
                Place(avatarSlot, 0.5f, 1f, 0.5f, 1f, 0f, -Margin, AvatarBadgeView.Size);
                PlaceLogo(0.5f, 0.66f);
                Place(levels.Slot, 0.5f, 0f, 0.5f, 0f, 0f, 490f + banner, LevelsSize);
                Place(endless.Slot, 0.5f, 0f, 0.5f, 0f, 0f, 270f + banner, EndlessSize);
                Place(gift.Slot, 0.5f, 0f, 0.5f, 0f, 0f, 30f + banner, MenuGiftButton.Size);
            }
            else
            {
                Place(avatarSlot, LandscapeLeftColumn, 1f, 0.5f, 1f, 0f, -Margin, AvatarBadgeView.Size);
                PlaceLogo(LandscapeLeftColumn, 0.44f);
                Place(levels.Slot, LandscapeRightColumn, 0.5f, 0.5f, 0.5f, 0f, 150f, LevelsSize);
                Place(endless.Slot, LandscapeRightColumn, 0.5f, 0.5f, 0.5f, 0f, -100f, EndlessSize);
                Place(gift.Slot, LandscapeRightColumn, 0.5f, 0.5f, 0.5f, 0f, -370f, MenuGiftButton.Size);
            }
        }

        private void PlaceLogo(float anchorX, float anchorY)
        {
            var anchor = new Vector2(anchorX, anchorY);
            UIFactory.Anchor(glow, anchor, Half, Vector2.zero, GlowSize);
            UIFactory.Anchor(logoSlot, anchor, Half, Vector2.zero, LogoSize);
            UIFactory.Anchor(bestSlot, anchor, Half, new Vector2(0f, -BestOffset), BestSize);
        }

        private static void Place(
            RectTransform rect, float anchorX, float anchorY, float pivotX, float pivotY, float x, float y, Vector2 size)
        {
            UIFactory.Anchor(rect, new Vector2(anchorX, anchorY), new Vector2(pivotX, pivotY), new Vector2(x, y), size);
        }

        // ---------------------------------------------------------------- build

        private RectTransform logoSlot;

        /// <summary>
        /// The painted backdrop over the gradient, cropped to cover the screen in any orientation.
        /// Without the picture the gradient alone stays.
        /// </summary>
        private static void BuildBackgroundArt(RectTransform background)
        {
            Sprite sprite = GameArt.MenuBackground;
            if (sprite == null)
            {
                return;
            }

            // The mask keeps the cover-cropped picture inside the screen.
            background.gameObject.AddComponent<RectMask2D>();
            Image picture = UIFactory.CreateImage("BackgroundArt", background, Color.white, false);
            picture.sprite = sprite;
            picture.raycastTarget = false;
            RectTransform rect = picture.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        private void Build(RectTransform root)
        {
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Opaque and a raycast target, so neither the last board nor its tray can be touched through the menu.
            Image background = UIFactory.CreateImage("MenuBackground", root, Color.white, false);
            UIFactory.Stretch(background.rectTransform);
            background.gameObject.AddComponent<VerticalGradient>()
                .SetColors(MenuArt.BackgroundTop, MenuArt.BackgroundBottom);
            BuildBackgroundArt(background.rectTransform);

            glowImage = UIFactory.CreateImage("LogoGlow", root, Color.white, false);
            glowImage.sprite = MenuArt.GlowSprite;
            glowImage.raycastTarget = false;
            glow = glowImage.rectTransform;

            floaters = MenuFloaters.Create(root);

            RectTransform content = UIFactory.CreateRect("Content", root);
            UIFactory.Stretch(content);
            content.gameObject.AddComponent<SafeAreaFitter>();

            settings = MenuButtonFactory.CreateRound(
                "SettingsButton",
                content,
                SettingsSize,
                GameTheme.FromHex("#6D86FF"),
                GameTheme.FromHex("#3550D8"),
                GameTheme.FromHex("#1C2A82"),
                MenuArt.GearSprite);
            settings.Button.onClick.AddListener(HandleSettingsClicked);

            coinSlot = UIFactory.CreateRect("CoinSlot", content);
            CoinCounterView.Create(coinSlot);

            avatarSlot = UIFactory.CreateRect("AvatarSlot", content);
            AvatarBadgeView.Create(avatarSlot);

            logoSlot = UIFactory.CreateRect("LogoSlot", content);
            logo = MenuLogo.Create(logoSlot);

            BuildBestPlate(content);

            levels = MenuButtonFactory.CreateWide(
                "LevelsButton",
                content,
                LevelsSize,
                GameTheme.FromHex("#FFB43B"),
                GameTheme.FromHex("#FF6A13"),
                GameTheme.FromHex("#B83F00"),
                MenuArt.PlaySprite,
                new Vector2(64f, 64f),
                animated: true);
            levels.Button.onClick.AddListener(HandleLevelsClicked);
            MenuButtonFactory.AddSubtitle(levels, 38f, GameTheme.FromHex("#FFF3D6"));
            shineHalfTravel = LevelsSize.x * 0.5f + 80f;

            endless = MenuButtonFactory.CreateWide(
                "EndlessButton",
                content,
                EndlessSize,
                GameTheme.FromHex("#8B6DFF"),
                GameTheme.FromHex("#3B4FE0"),
                GameTheme.FromHex("#1F2A8C"),
                MenuArt.InfinitySprite,
                new Vector2(112f, 56f),
                animated: false);
            endless.Button.onClick.AddListener(HandlePlayClicked);

            gift = MenuGiftButton.Create(content);
            gift.Button.onClick.AddListener(HandleGiftClicked);
            gift.Slot.gameObject.SetActive(MetaClock.DailyFeaturesEnabled);

            introOrder.Add(settings.Slot);
            introOrder.Add(coinSlot);
            introOrder.Add(avatarSlot);
            introOrder.Add(logoSlot);
            introOrder.Add(bestSlot);
            introOrder.Add(levels.Slot);
            introOrder.Add(endless.Slot);
            introOrder.Add(gift.Slot);

            built = true;
            ApplyLayout(Screen.width <= Screen.height);
            ApplyTheme();
            SetVisible(false);
        }

        private void BuildBestPlate(RectTransform parent)
        {
            bestSlot = UIFactory.CreateRect("BestSlot", parent);

            Image plate = UIFactory.CreateImage("Plate", bestSlot, GameTheme.WithAlpha(MenuArt.Ink, 0.6f));
            plate.raycastTarget = false;
            UIFactory.Stretch(plate.rectTransform);
            bestOutline = plate.gameObject.AddComponent<Outline>();
            bestOutline.effectDistance = new Vector2(3f, -3f);

            TextMeshProUGUI label = UIFactory.CreateText(
                "Label", bestSlot, string.Empty, 44f, GameTheme.FromHex("#FFE066"), TextAlignmentOptions.Center,
                FontStyles.Bold);
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 44f;
            UIFactory.Stretch(label.rectTransform, 12f);
            bestLabel = label;
        }
    }
}
