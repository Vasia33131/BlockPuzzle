using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Managers;
using BlockPuzzle.Pieces;
using BlockPuzzle.UI;

namespace BlockPuzzle.Bootstrap
{
    /// <summary>
    /// Builds the complete portrait game screen out of uGUI objects.
    /// The same code is used by the editor tool that bakes the scene asset and by
    /// <see cref="GameBootstrap"/> when the hierarchy has to be created at runtime.
    /// </summary>
    public static class GameSceneFactory
    {
        private const float TopPanelHeight = 84f;
        private const float ScreenSideMargin = 16f;
        private const float ScreenTopMargin = 12f;
        private const float PauseButtonSize = 56f;
        private const float HudButtonGap = 10f;
        private const float ScoreSectionWidth = 320f;
        private const float BestSectionWidth = 280f;
        private const float BoardVerticalOffset = 30f;
        private const float BoardPadding = 14f;
        private const float SpawnAreaHeight = 280f;
        private const float SpawnAreaBottomMargin = 16f;
        private const float SpawnAreaSideMargin = 30f;
        private const float BoosterBarSideMargin = 30f;

        public const string ThemeClassicCardName = "ThemeClassicCard";
        public const string ThemeDefaultCardName = "ThemeDefaultCard";
        public const string ThemeOceanCardName = "ThemeOceanCard";
        public const string ThemeCandyCardName = "ThemeCandyCard";

        private const float ShopCardWidth = 780f;
        private const float ShopCardHeight = 1280f;
        private const float ShopProductWidth = 680f;
        private const float NoAdsCardHeight = 250f;
        private const float NoAdsCardY = -148f;
        private const float ThemeCardWidth = 216f;
        private const float ThemeCardHeight = 360f;
        private const float ThemeCardY = -428f;
        private const float ThemeCardPitch = 232f;
        private const float ThemeIconSize = 72f;
        private const float PackCardHeight = 230f;
        private const float PackCardY = -838f;

        /// <summary>Optional authored prefabs used when baking or bootstrapping the scene.</summary>
        public sealed class PrefabSet
        {
            public GridCellView GridCell;
            public BlockPiece BlockPiece;
            public GameOverPanel GameOverPanel;
            public PausePanel PausePanel;
            public ShopPanel ShopPanel;
            public Image Spark;
        }

        /// <summary>References to everything the factory produced.</summary>
        public sealed class BuildResult
        {
            public Canvas Canvas;
            public GameManager GameManager;
            public GridManager GridManager;
            public ShapeSpawner ShapeSpawner;
            public ScoreManager ScoreManager;
            public GameOverHandler GameOverHandler;
            public AudioManager AudioManager;
            public UndoBuffer UndoBuffer;
            public BoosterController BoosterController;
            public HudController Hud;
            public BoosterBar BoosterBar;
            public GameOverPanel GameOverPanel;
            public PausePanel PausePanel;
            public ShopPanel ShopPanel;
            public Camera Camera;
            public EventSystem EventSystem;
        }

        public static BuildResult Build(ShapeLibrary library = null, PrefabSet prefabs = null)
        {
            var result = new BuildResult
            {
                Camera = EnsureCamera(),
                EventSystem = EnsureEventSystem()
            };

            Canvas canvas = CreateCanvas();
            result.Canvas = canvas;
            var canvasRect = (RectTransform)canvas.transform;

            CreateBackground(canvasRect);

            RectTransform safeArea = UIFactory.CreateRect("SafeArea", canvasRect);
            UIFactory.Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaHandler>();

            var managersGo = new GameObject("Managers");
            result.ScoreManager = managersGo.AddComponent<ScoreManager>();
            result.GameOverHandler = managersGo.AddComponent<GameOverHandler>();
            result.AudioManager = managersGo.AddComponent<AudioManager>();
            result.UndoBuffer = managersGo.AddComponent<UndoBuffer>();
            result.BoosterController = managersGo.AddComponent<BoosterController>();
            result.GameManager = managersGo.AddComponent<GameManager>();

            result.Hud = CreateTopPanel(safeArea, result.ScoreManager, out Button pauseButton, out Button shopButton);
            result.GridManager = CreateBoard(
                safeArea, prefabs != null ? prefabs.GridCell : null, out RectTransform boardPanel);

            RectTransform dragLayer = UIFactory.CreateRect("DragLayer", canvasRect);
            UIFactory.Stretch(dragLayer);
            dragLayer.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            result.ShapeSpawner = CreateSpawnArea(
                safeArea,
                result.GridManager,
                dragLayer,
                library,
                prefabs != null ? prefabs.BlockPiece : null);

            result.BoosterBar = CreateBoosterBar(safeArea);
            if (result.BoosterBar != null && result.ShapeSpawner != null)
            {
                result.BoosterBar.transform.SetSiblingIndex(result.ShapeSpawner.transform.GetSiblingIndex());
            }

            RectTransform topPanelRect = result.Hud != null ? (RectTransform)result.Hud.transform : null;
            RectTransform gridAreaRect = result.GridManager != null ? result.GridManager.BoardRoot : null;
            RectTransform spawnAreaRect = result.ShapeSpawner != null
                ? (RectTransform)result.ShapeSpawner.transform
                : null;
            RectTransform boosterBarRect = result.BoosterBar != null
                ? (RectTransform)result.BoosterBar.transform
                : null;
            RectTransform pauseButtonRect = pauseButton != null
                ? (RectTransform)pauseButton.transform
                : null;
            RectTransform shopButtonRect = shopButton != null
                ? (RectTransform)shopButton.transform
                : null;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

            var uiManager = canvas.gameObject.AddComponent<UIManager>();
            uiManager.Configure(
                scaler,
                safeArea,
                topPanelRect,
                boardPanel,
                gridAreaRect,
                spawnAreaRect,
                boosterBarRect,
                result.GridManager,
                result.ShapeSpawner);

            var orientationHandler = canvas.gameObject.AddComponent<OrientationHandler>();
            orientationHandler.Configure(
                scaler,
                safeArea,
                topPanelRect,
                gridAreaRect,
                spawnAreaRect,
                pauseButtonRect,
                result.GridManager,
                result.ShapeSpawner,
                shopButtonRect);

            // Bake uses factory defaults; runtime layout adapts to the live aspect / orientation.
            if (Application.isPlaying)
            {
                orientationHandler.RefreshNow();
            }

            // Pause below shop, shop below game over: losing still overrides everything.
            result.PausePanel = CreatePausePanel(
                canvasRect, result.GameManager, pauseButton, prefabs != null ? prefabs.PausePanel : null);
            result.ShopPanel = CreateShopPanel(
                canvasRect, result.GameManager, shopButton, prefabs != null ? prefabs.ShopPanel : null);
            result.GameOverPanel = CreateGameOverPanel(
                canvasRect, result.GameManager, prefabs != null ? prefabs.GameOverPanel : null);

            if (prefabs != null && prefabs.Spark != null)
            {
                result.GridManager?.Sparks?.SetPrefab(prefabs.Spark);
            }

            result.GameOverHandler.Configure(result.GridManager, result.ShapeSpawner, result.ScoreManager);
            result.GameManager.Configure(
                result.GridManager,
                result.ShapeSpawner,
                result.ScoreManager,
                result.GameOverHandler,
                result.AudioManager);
            result.BoosterBar?.Bind(result.GameManager);

            return result;
        }

        /// <summary>
        /// Adds the booster row to an already-baked scene that predates it, then binds
        /// it to the live <see cref="GameManager"/>.
        /// </summary>
        public static BoosterBar EnsureBoosterBar(RectTransform safeArea, GameManager gameManager)
        {
            BoosterBar existing = Object.FindObjectOfType<BoosterBar>(true);
            if (existing != null)
            {
                existing.Bind(gameManager);
                return existing;
            }

            if (safeArea == null)
            {
                return null;
            }

            BoosterBar bar = CreateBoosterBar(safeArea);
            ShapeSpawner spawner = Object.FindObjectOfType<ShapeSpawner>(true);
            if (bar != null && spawner != null)
            {
                bar.transform.SetSiblingIndex(spawner.transform.GetSiblingIndex());
            }

            bar?.Bind(gameManager);
            return bar;
        }

        /// <summary>Unbound pause overlay hierarchy, used when baking the PausePanel prefab.</summary>
        public static PausePanel BuildPausePanelHierarchy(RectTransform parent)
        {
            return CreatePausePanel(parent, null, null, null);
        }

        /// <summary>Unbound shop overlay hierarchy, used when baking the ShopPanel prefab.</summary>
        public static ShopPanel BuildShopPanelHierarchy(RectTransform parent)
        {
            return CreateShopPanel(parent, null, null, null);
        }

        /// <summary>
        /// Adds the shop overlay to an already-baked scene that predates it, then binds
        /// it to the HUD shop button.
        /// </summary>
        public static ShopPanel EnsureShopPanel(RectTransform canvasRect, Button hudShopButton)
        {
            ShopPanel existing = Object.FindObjectOfType<ShopPanel>(true);
            if (existing != null)
            {
                RectTransform existingCard = existing.transform.Find("Card") as RectTransform;
                EnsureThemeProductCards(existingCard);
                EnsureShapesPackCard(existingCard);
                existing.Bind(Object.FindObjectOfType<GameManager>(true), hudShopButton);
                PlaceShopBelowGameOver(existing);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            ShopPanel panel = CreateShopPanel(
                canvasRect, Object.FindObjectOfType<GameManager>(true), hudShopButton, null);
            PlaceShopBelowGameOver(panel);
            return panel;
        }

        /// <summary>HUD shop control on TopPanel, left of pause. Used for baked scenes that predate it.</summary>
        public static Button EnsureHudShopButton(RectTransform topPanel)
        {
            if (topPanel == null)
            {
                return null;
            }

            Button existing = topPanel.Find("ShopButton")?.GetComponent<Button>();
            if (existing != null)
            {
                return existing;
            }

            return CreateHudShopButton(topPanel);
        }

        private static void PlaceShopBelowGameOver(ShopPanel shop)
        {
            if (shop == null)
            {
                return;
            }

            GameOverPanel gameOver = Object.FindObjectOfType<GameOverPanel>(true);
            if (gameOver == null)
            {
                return;
            }

            int gameOverIndex = gameOver.transform.GetSiblingIndex();
            shop.transform.SetSiblingIndex(gameOverIndex);
        }

        /// <summary>Unbound game-over overlay hierarchy, used when baking the GameOverPanel prefab.</summary>
        public static GameOverPanel BuildGameOverPanelHierarchy(RectTransform parent)
        {
            return CreateGameOverPanel(parent, null, null);
        }

        private static Camera EnsureCamera()
        {
            Camera existing = Camera.main;
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GameTheme.BackgroundBottom;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
            return camera;
        }

        private static EventSystem EnsureEventSystem()
        {
            EventSystem existing = Object.FindObjectOfType<EventSystem>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("EventSystem");
            EventSystem eventSystem = go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return eventSystem;
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("GameCanvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(GameTheme.ReferenceWidth, GameTheme.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateBackground(RectTransform parent)
        {
            Image background = UIFactory.CreateImage("Background", parent, Color.white, false);
            UIFactory.Stretch(background.rectTransform);
            background.raycastTarget = false;
            background.gameObject.AddComponent<VerticalGradient>()
                .SetColors(GameTheme.BackgroundTop, GameTheme.BackgroundBottom);
            if (background.GetComponent<ThemeBinder>() == null)
            {
                background.gameObject.AddComponent<ThemeBinder>();
            }

            ThemeBinder.EnsureBackgroundPattern(background.rectTransform);
        }

        /// <summary>
        /// Single-row HUD: Score left, Best center, Shop then Pause on the right.
        /// </summary>
        private static HudController CreateTopPanel(
            RectTransform parent,
            ScoreManager scoreManager,
            out Button pauseButton,
            out Button shopButton)
        {
            RectTransform panel = UIFactory.CreateRect("TopPanel", parent);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.offsetMin = new Vector2(ScreenSideMargin, -ScreenTopMargin - TopPanelHeight);
            panel.offsetMax = new Vector2(-ScreenSideMargin, -ScreenTopMargin);

            TMP_Text scoreValue = CreateHudStat(
                panel,
                "ScoreSection",
                "ScoreText",
                "СЧЁТ: 0",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(20f, 0f),
                new Vector2(ScoreSectionWidth, TopPanelHeight),
                TextAlignmentOptions.MidlineLeft);

            TMP_Text bestValue = CreateHudStat(
                panel,
                "BestSection",
                "BestText",
                "РЕКОРД: 0",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(BestSectionWidth, TopPanelHeight),
                TextAlignmentOptions.Center);

            shopButton = CreateHudShopButton(panel);
            pauseButton = CreatePauseButton(panel);

            TextMeshProUGUI combo = UIFactory.CreateText(
                "ComboLabel", panel, string.Empty, 36f, GameTheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(
                combo.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -8f),
                new Vector2(600f, 48f));
            combo.color = new Color(GameTheme.Accent.r, GameTheme.Accent.g, GameTheme.Accent.b, 0f);

            var hud = panel.gameObject.AddComponent<HudController>();
            hud.Bind(scoreManager, scoreValue, bestValue, combo);
            return hud;
        }

        private static TMP_Text CreateHudStat(
            RectTransform parent,
            string sectionName,
            string textName,
            string initialText,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            TextAlignmentOptions alignment)
        {
            RectTransform section = UIFactory.CreateRect(sectionName, parent);
            UIFactory.Anchor(section, anchor, pivot, position, size);

            TextMeshProUGUI text = UIFactory.CreateText(
                textName, section, initialText, 36f, GameTheme.TextPrimary, alignment, FontStyles.Bold);
            UIFactory.Stretch(text.rectTransform);
            FitHudNumber(text);
            return text;
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
            text.fontSizeMax = 36f;
        }

        /// <summary>
        /// Pause control on the right edge of TopPanel. Behaviour is owned by <see cref="PausePanel"/>.
        /// </summary>
        private static Button CreatePauseButton(RectTransform parent)
        {
            Image background = UIFactory.CreateImage(
                "PauseButton", parent, GameTheme.WithAlpha(GameTheme.CardBackground, 0.9f));

            UIFactory.Anchor(
                background.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-15f, 0f),
                new Vector2(PauseButtonSize, PauseButtonSize));

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ApplyButtonColors(button);

            // Two bars drawn from plain rects, so the icon needs no texture of its own.
            for (int i = 0; i < 2; i++)
            {
                Image bar = UIFactory.CreateImage($"Bar_{i}", background.rectTransform, GameTheme.TextPrimary);
                bar.raycastTarget = false;
                UIFactory.Anchor(
                    bar.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(i == 0 ? -7f : 7f, 0f),
                    new Vector2(7f, 24f));
            }

            return button;
        }

        /// <summary>
        /// Shop control left of pause on TopPanel. Behaviour is owned by <see cref="ShopPanel"/>.
        /// </summary>
        private static Button CreateHudShopButton(RectTransform parent)
        {
            Image background = UIFactory.CreateImage(
                "ShopButton", parent, GameTheme.WithAlpha(GameTheme.CardBackground, 0.9f));

            float x = -(15f + PauseButtonSize + HudButtonGap);
            UIFactory.Anchor(
                background.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(x, 0f),
                new Vector2(PauseButtonSize, PauseButtonSize));

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ApplyButtonColors(button);
            CreateShopCartIcon(background.rectTransform);
            return button;
        }

        private static void CreateShopCartIcon(RectTransform parent)
        {
            Image body = UIFactory.CreateImage("CartBody", parent, GameTheme.TextPrimary);
            body.raycastTarget = false;
            UIFactory.Anchor(
                body.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-2f, -1f),
                new Vector2(26f, 16f));

            for (int i = 0; i < 2; i++)
            {
                Image wheel = UIFactory.CreateImage($"Wheel_{i}", parent, GameTheme.TextPrimary);
                wheel.raycastTarget = false;
                UIFactory.Anchor(
                    wheel.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(i == 0 ? -8f : 6f, -14f),
                    new Vector2(8f, 8f));
            }

            Image handle = UIFactory.CreateImage("CartHandle", parent, GameTheme.TextPrimary);
            handle.raycastTarget = false;
            UIFactory.Anchor(
                handle.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(12f, 6f),
                new Vector2(4f, 14f));
        }

        private static void ApplyButtonColors(Button button)
        {
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            ButtonPressAnimator.Attach(button);
        }

        private static PausePanel CreatePausePanel(
            RectTransform parent,
            GameManager gameManager,
            Button pauseButton,
            PausePanel prefab)
        {
            PausePanel panel;
            CanvasGroup group;
            RectTransform card;
            Button resume;
            Button restart;
            Button sound;

            if (prefab != null)
            {
                panel = Object.Instantiate(prefab, parent);
                panel.gameObject.name = "PausePanel";
                UIFactory.Stretch((RectTransform)panel.transform);
                group = panel.GetComponent<CanvasGroup>();
                card = panel.transform.Find("Card") as RectTransform;
                resume = panel.transform.Find("Card/ResumeButton")?.GetComponent<Button>();
                restart = panel.transform.Find("Card/RestartButton")?.GetComponent<Button>();
                sound = panel.transform.Find("Card/SoundButton")?.GetComponent<Button>();
                panel.Bind(gameManager, group, card, pauseButton, resume, restart, sound);
                return panel;
            }

            RectTransform root = UIFactory.CreateRect("PausePanel", parent);
            UIFactory.Stretch(root);

            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image dim = UIFactory.CreateImage("Dim", root, new Color(0.03f, 0.03f, 0.08f, 0.78f), false);
            UIFactory.Stretch(dim.rectTransform);

            Image cardImage = UIFactory.CreateImage("Card", root, GameTheme.CardBackground);
            card = cardImage.rectTransform;
            UIFactory.Anchor(
                card,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(780f, 700f));

            TextMeshProUGUI title = UIFactory.CreateText(
                "Title", card, "ПАУЗА", 80f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -50f),
                new Vector2(700f, 90f));
            title.characterSpacing = 6f;

            sound = UIFactory.CreateButton(
                "SoundButton", card, "ЗВУК: ВКЛ", GameTheme.ButtonSecondary, GameTheme.TextPrimary, 34f);
            UIFactory.Anchor(
                (RectTransform)sound.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 360f),
                new Vector2(620f, 100f));

            resume = UIFactory.CreateButton(
                "ResumeButton", card, "ПРОДОЛЖИТЬ", GameTheme.Accent, GameTheme.FromHex("#1a1a2e"), 44f);
            UIFactory.Anchor(
                (RectTransform)resume.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 200f),
                new Vector2(620f, 130f));

            restart = UIFactory.CreateButton(
                "RestartButton", card, "НАЧАТЬ ЗАНОВО", GameTheme.ButtonSecondary, GameTheme.TextPrimary, 38f);
            UIFactory.Anchor(
                (RectTransform)restart.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 50f),
                new Vector2(620f, 120f));

            panel = root.gameObject.AddComponent<PausePanel>();
            panel.Bind(gameManager, group, card, pauseButton, resume, restart, sound);
            return panel;
        }

        private static ShopPanel CreateShopPanel(
            RectTransform parent,
            GameManager gameManager,
            Button hudShopButton,
            ShopPanel prefab)
        {
            ShopPanel panel;
            CanvasGroup group;
            RectTransform card;
            TMP_Text price;
            Button buy;
            Button back;

            if (prefab != null)
            {
                panel = Object.Instantiate(prefab, parent);
                panel.gameObject.name = "ShopPanel";
                UIFactory.Stretch((RectTransform)panel.transform);
                group = panel.GetComponent<CanvasGroup>();
                card = panel.transform.Find("Card") as RectTransform;
                EnsureThemeProductCards(card);
                EnsureShapesPackCard(card);
                price = panel.transform.Find("Card/NoAdsCard/Price")?.GetComponent<TMP_Text>();
                buy = panel.transform.Find("Card/NoAdsCard/BuyButton")?.GetComponent<Button>();
                back = panel.transform.Find("Card/BackButton")?.GetComponent<Button>();
                panel.Bind(gameManager, hudShopButton, group, card, price, buy, back);
                return panel;
            }

            RectTransform root = UIFactory.CreateRect("ShopPanel", parent);
            UIFactory.Stretch(root);

            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image dim = UIFactory.CreateImage("Dim", root, new Color(0.03f, 0.03f, 0.08f, 0.78f), false);
            UIFactory.Stretch(dim.rectTransform);

            Image cardImage = UIFactory.CreateImage("Card", root, GameTheme.CardBackground);
            card = cardImage.rectTransform;
            UIFactory.Anchor(
                card,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(ShopCardWidth, ShopCardHeight));

            TextMeshProUGUI title = UIFactory.CreateText(
                "Title", card, "МАГАЗИН", 72f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -46f),
                new Vector2(700f, 84f));
            title.characterSpacing = 6f;

            Image product = UIFactory.CreateImage("NoAdsCard", card, GameTheme.EmptyCell);
            RectTransform productRect = product.rectTransform;
            UIFactory.Anchor(
                productRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -148f),
                new Vector2(680f, 250f));

            TextMeshProUGUI productTitle = UIFactory.CreateText(
                "Title",
                productRect,
                "Без рекламы",
                40f,
                GameTheme.TextPrimary,
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            UIFactory.Anchor(
                productTitle.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(620f, 52f));

            TextMeshProUGUI priceText = UIFactory.CreateText(
                "Price",
                productRect,
                "Покупка",
                30f,
                GameTheme.TextSecondary,
                TextAlignmentOptions.Center,
                FontStyles.Normal);
            UIFactory.Anchor(
                priceText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -70f),
                new Vector2(620f, 40f));
            price = priceText;

            buy = UIFactory.CreateButton(
                "BuyButton",
                productRect,
                "Купить",
                GameTheme.Accent,
                GameTheme.FromHex("#1a1a2e"),
                36f);
            UIFactory.Anchor(
                (RectTransform)buy.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 20f),
                new Vector2(560f, 90f));

            CreateThemeProductCards(card);
            CreateShapesPackCard(card);

            back = UIFactory.CreateButton(
                "BackButton", card, "НАЗАД", GameTheme.ButtonSecondary, GameTheme.TextPrimary, 36f);
            UIFactory.Anchor(
                (RectTransform)back.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 36f),
                new Vector2(620f, 100f));

            panel = root.gameObject.AddComponent<ShopPanel>();
            panel.Bind(gameManager, hudShopButton, group, card, price, buy, back);
            return panel;
        }

        /// <summary>
        /// Adds the free classic card plus the two paid theme cards to a shop
        /// that was baked before they existed, then packs them into one row.
        /// </summary>
        public static void EnsureThemeProductCards(RectTransform shopCard)
        {
            if (shopCard == null)
            {
                return;
            }

            shopCard.sizeDelta = new Vector2(ShopCardWidth, ShopCardHeight);
            CompactNoAdsCard(shopCard.Find("NoAdsCard") as RectTransform);
            CreateThemeProductCards(shopCard);
            LayoutThemeProductCards(shopCard);
            LayoutShapesPackCard(shopCard.Find("ShapesPack1Card") as RectTransform);
        }

        /// <summary>
        /// Adds the paid figure-pack card to a shop that was baked before it existed.
        /// </summary>
        public static void EnsureShapesPackCard(RectTransform shopCard)
        {
            if (shopCard == null)
            {
                return;
            }

            shopCard.sizeDelta = new Vector2(ShopCardWidth, ShopCardHeight);
            CreateShapesPackCard(shopCard);
            LayoutShapesPackCard(shopCard.Find("ShapesPack1Card") as RectTransform);
        }

        private static void CompactNoAdsCard(RectTransform productRect)
        {
            if (productRect == null)
            {
                return;
            }

            UIFactory.Anchor(
                productRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, NoAdsCardY),
                new Vector2(ShopProductWidth, NoAdsCardHeight));

            RectTransform buy = productRect.Find("BuyButton") as RectTransform;
            if (buy != null)
            {
                UIFactory.Anchor(
                    buy,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 20f),
                    new Vector2(560f, 90f));
            }
        }

        private static void CreateThemeProductCards(RectTransform shopCard)
        {
            if (shopCard == null)
            {
                return;
            }

            if (FindThemeCard(shopCard, ThemeClassicCardName, ThemeDefaultCardName) == null)
            {
                CreateThemeProductCard(
                    shopCard,
                    ThemeClassicCardName,
                    GameTheme.Get(ThemeConfig.DefaultId),
                    ClassicThemePosition);
            }

            if (shopCard.Find(ThemeOceanCardName) == null)
            {
                CreateThemeProductCard(
                    shopCard,
                    ThemeOceanCardName,
                    GameTheme.Get(ThemeConfig.OceanId),
                    OceanThemePosition);
            }

            if (shopCard.Find(ThemeCandyCardName) == null)
            {
                CreateThemeProductCard(
                    shopCard,
                    ThemeCandyCardName,
                    GameTheme.Get(ThemeConfig.CandyId),
                    CandyThemePosition);
            }
        }

        private static Vector2 ClassicThemePosition => new Vector2(-ThemeCardPitch, ThemeCardY);
        private static Vector2 OceanThemePosition => new Vector2(0f, ThemeCardY);
        private static Vector2 CandyThemePosition => new Vector2(ThemeCardPitch, ThemeCardY);

        private static Transform FindThemeCard(RectTransform shopCard, string primaryName, string aliasName)
        {
            Transform card = shopCard.Find(primaryName);
            return card != null ? card : shopCard.Find(aliasName);
        }

        private static void LayoutThemeProductCards(RectTransform shopCard)
        {
            if (shopCard == null)
            {
                return;
            }

            LayoutThemeProductCard(
                FindThemeCard(shopCard, ThemeClassicCardName, ThemeDefaultCardName) as RectTransform,
                ClassicThemePosition);
            LayoutThemeProductCard(shopCard.Find(ThemeOceanCardName) as RectTransform, OceanThemePosition);
            LayoutThemeProductCard(shopCard.Find(ThemeCandyCardName) as RectTransform, CandyThemePosition);
        }

        private static void LayoutThemeProductCard(RectTransform productRect, Vector2 position)
        {
            if (productRect == null)
            {
                return;
            }

            UIFactory.Anchor(
                productRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                position,
                new Vector2(ThemeCardWidth, ThemeCardHeight));

            LayoutThemeSwatch(productRect.Find("Icon") as RectTransform);

            RectTransform title = productRect.Find("Title") as RectTransform;
            if (title != null)
            {
                UIFactory.Anchor(
                    title,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -96f),
                    new Vector2(196f, 40f));
                TMP_Text titleLabel = title.GetComponent<TMP_Text>();
                if (titleLabel != null)
                {
                    titleLabel.fontSize = 26f;
                    titleLabel.enableWordWrapping = false;
                    titleLabel.overflowMode = TextOverflowModes.Ellipsis;
                }
            }

            RectTransform price = productRect.Find("Price") as RectTransform;
            if (price != null)
            {
                UIFactory.Anchor(
                    price,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -136f),
                    new Vector2(196f, 28f));
                TMP_Text priceLabel = price.GetComponent<TMP_Text>();
                if (priceLabel != null)
                {
                    priceLabel.fontSize = 20f;
                }
            }

            RectTransform buy = productRect.Find("BuyButton") as RectTransform;
            if (buy != null)
            {
                UIFactory.Anchor(
                    buy,
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 14f),
                    new Vector2(188f, 72f));
                TMP_Text buyLabel = buy.GetComponentInChildren<TMP_Text>(true);
                if (buyLabel != null)
                {
                    buyLabel.fontSize = 24f;
                }
            }
        }

        private static void LayoutShapesPackCard(RectTransform productRect)
        {
            if (productRect == null)
            {
                return;
            }

            UIFactory.Anchor(
                productRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, PackCardY),
                new Vector2(ShopProductWidth, PackCardHeight));
        }

        private static void CreateShapesPackCard(RectTransform shopCard)
        {
            if (shopCard == null || shopCard.Find("ShapesPack1Card") != null)
            {
                return;
            }

            Image product = UIFactory.CreateImage("ShapesPack1Card", shopCard, GameTheme.EmptyCell);
            RectTransform productRect = product.rectTransform;
            LayoutShapesPackCard(productRect);

            TextMeshProUGUI productTitle = UIFactory.CreateText(
                "Title",
                productRect,
                "Набор фигурок",
                40f,
                GameTheme.TextPrimary,
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            UIFactory.Anchor(
                productTitle.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -16f),
                new Vector2(620f, 48f));

            TextMeshProUGUI priceText = UIFactory.CreateText(
                "Price",
                productRect,
                "Покупка",
                28f,
                GameTheme.TextSecondary,
                TextAlignmentOptions.Center,
                FontStyles.Normal);
            UIFactory.Anchor(
                priceText.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -62f),
                new Vector2(620f, 36f));

            Button buy = UIFactory.CreateButton(
                "BuyButton",
                productRect,
                "Купить",
                GameTheme.Accent,
                GameTheme.FromHex("#1a1a2e"),
                32f);
            UIFactory.Anchor(
                (RectTransform)buy.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(560f, 82f));
        }

        private static void CreateThemeProductCard(
            RectTransform parent,
            string objectName,
            ThemeConfig theme,
            Vector2 position)
        {
            if (theme == null)
            {
                return;
            }

            bool free = theme.Id == ThemeConfig.DefaultId;
            Image product = UIFactory.CreateImage(objectName, parent, GameTheme.EmptyCell);
            RectTransform productRect = product.rectTransform;

            CreateThemeSwatch(productRect, theme);

            UIFactory.CreateText(
                "Title",
                productRect,
                theme.DisplayName,
                26f,
                GameTheme.TextPrimary,
                TextAlignmentOptions.Center,
                FontStyles.Bold);

            TextMeshProUGUI priceText = UIFactory.CreateText(
                "Price",
                productRect,
                "Покупка",
                20f,
                GameTheme.TextSecondary,
                TextAlignmentOptions.Center,
                FontStyles.Normal);
            priceText.gameObject.SetActive(!free);

            UIFactory.CreateButton(
                "BuyButton",
                productRect,
                free ? "Выбрать" : "Купить",
                theme.Accent,
                GameTheme.FromHex("#1a1a2e"),
                24f);

            LayoutThemeProductCard(productRect, position);
        }

        private static void CreateThemeSwatch(RectTransform parent, ThemeConfig theme)
        {
            Image icon = UIFactory.CreateImage("Icon", parent, theme.BackgroundBottom);
            float square = 28f;
            float gap = 4f;
            float startX = -(square + gap) * 0.5f;
            Color[] swatches = { theme.BackgroundTop, theme.EmptyCell, theme.Accent };
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    int index = i * 2 + j;
                    Color color = index < swatches.Length ? swatches[index] : theme.StartingBlock;
                    UIFactory.CreateImage($"Swatch_{i}_{j}", icon.rectTransform, color);
                }
            }

            LayoutThemeSwatch(icon.rectTransform);
        }

        private static void LayoutThemeSwatch(RectTransform icon)
        {
            if (icon == null)
            {
                return;
            }

            UIFactory.Anchor(
                icon,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -14f),
                new Vector2(ThemeIconSize, ThemeIconSize));

            float square = 28f;
            float gap = 4f;
            float startX = -(square + gap) * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    RectTransform swatch = icon.Find($"Swatch_{i}_{j}") as RectTransform;
                    if (swatch == null)
                    {
                        continue;
                    }

                    UIFactory.Anchor(
                        swatch,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(startX + j * (square + gap), (square + gap) * 0.5f - i * (square + gap)),
                        new Vector2(square, square));
                }
            }
        }

        private static GridManager CreateBoard(
            RectTransform parent,
            GridCellView cellPrefab,
            out RectTransform boardPanel)
        {
            float board = GameTheme.BoardSize;

            Image panel = UIFactory.CreateImage("BoardPanel", parent, new Color(0f, 0f, 0f, 0.22f));
            boardPanel = panel.rectTransform;
            panel.raycastTarget = false;
            UIFactory.Anchor(
                boardPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, BoardVerticalOffset),
                new Vector2(board + BoardPadding * 2f, board + BoardPadding * 2f));

            RectTransform gridArea = UIFactory.CreateRect("GridArea", boardPanel);
            UIFactory.Anchor(
                gridArea,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(board, board));

            var gridManager = gridArea.gameObject.AddComponent<GridManager>();
            gridManager.SetCellPrefab(cellPrefab);
            gridManager.Initialize();
            return gridManager;
        }

        private static ShapeSpawner CreateSpawnArea(
            RectTransform parent,
            GridManager grid,
            RectTransform dragLayer,
            ShapeLibrary library,
            BlockPiece piecePrefab)
        {
            RectTransform area = UIFactory.CreateRect("SpawnArea", parent);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 0f);
            area.pivot = new Vector2(0.5f, 0f);
            area.anchoredPosition = new Vector2(0f, SpawnAreaBottomMargin + GameTheme.ActiveBannerReserve);
            area.sizeDelta = new Vector2(-(SpawnAreaSideMargin * 2f), SpawnAreaHeight);

            var slots = new RectTransform[ShapeSpawner.SlotCount];
            for (int i = 0; i < ShapeSpawner.SlotCount; i++)
            {
                slots[i] = UIFactory.CreateRect($"Slot_{i}", area);
            }

            // The spawner owns the row layout: even spread on portrait, a centred cluster on desktop.
            var spawner = area.gameObject.AddComponent<ShapeSpawner>();
            spawner.Configure(grid, dragLayer, slots, library);
            spawner.SetPiecePrefab(piecePrefab);
            return spawner;
        }

        private static BoosterBar CreateBoosterBar(RectTransform parent)
        {
            BoosterBar bar = BoosterBar.Build(parent, null);
            var rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(
                0f,
                SpawnAreaBottomMargin + GameTheme.ActiveBannerReserve + SpawnAreaHeight + BoosterBar.TrayGap);
            rect.sizeDelta = new Vector2(-(BoosterBarSideMargin * 2f), BoosterBar.BarHeight);
            return bar;
        }

        private static GameOverPanel CreateGameOverPanel(
            RectTransform parent,
            GameManager gameManager,
            GameOverPanel prefab)
        {
            GameOverPanel panel;
            CanvasGroup group;
            RectTransform card;
            TMP_Text scoreValue;
            TMP_Text bestValue;
            TMP_Text badge;
            Button button;
            Button continueButton;
            Button authButton;
            TMP_Text authHint;

            if (prefab != null)
            {
                panel = Object.Instantiate(prefab, parent);
                panel.gameObject.name = "GameOverPanel";
                UIFactory.Stretch((RectTransform)panel.transform);
                group = panel.GetComponent<CanvasGroup>();
                card = panel.transform.Find("Card") as RectTransform;
                scoreValue = panel.transform.Find("Card/ScoreValue")?.GetComponent<TMP_Text>();
                bestValue = panel.transform.Find("Card/BestValue")?.GetComponent<TMP_Text>();
                badge = panel.transform.Find("Card/RecordBadge")?.GetComponent<TMP_Text>();
                button = panel.transform.Find("Card/RestartButton")?.GetComponent<Button>();
                continueButton = panel.transform.Find("Card/ContinueButton")?.GetComponent<Button>();
                authButton = panel.transform.Find("Card/AuthButton")?.GetComponent<Button>();
                authHint = panel.transform.Find("Card/AuthHint")?.GetComponent<TMP_Text>();
                panel.Bind(
                    gameManager, group, card, scoreValue, bestValue, badge, button, authButton, authHint, continueButton);
                return panel;
            }

            RectTransform root = UIFactory.CreateRect("GameOverPanel", parent);
            UIFactory.Stretch(root);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image dim = UIFactory.CreateImage("Dim", root, new Color(0.03f, 0.03f, 0.08f, 0.82f), false);
            UIFactory.Stretch(dim.rectTransform);

            Image cardImage = UIFactory.CreateImage("Card", root, GameTheme.CardBackground);
            card = cardImage.rectTransform;
            UIFactory.Anchor(
                card,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(840f, 780f));

            TextMeshProUGUI title = UIFactory.CreateText(
                "Title", card, "ПОРАЖЕНИЕ", 84f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 100f));

            TextMeshProUGUI badgeText = UIFactory.CreateText(
                "RecordBadge", card, "НОВЫЙ РЕКОРД!", 44f, GameTheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(badgeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(800f, 60f));
            badgeText.gameObject.SetActive(false);
            badge = badgeText;

            TextMeshProUGUI scoreCaption = UIFactory.CreateText(
                "ScoreCaption", card, "СЧЁТ", 36f, GameTheme.TextSecondary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(scoreCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(800f, 50f));

            TextMeshProUGUI scoreValueText = UIFactory.CreateText(
                "ScoreValue", card, "0", 100f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(scoreValueText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -285f), new Vector2(800f, 120f));
            scoreValue = scoreValueText;

            TextMeshProUGUI bestCaption = UIFactory.CreateText(
                "BestCaption", card, "РЕКОРД", 36f, GameTheme.TextSecondary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(bestCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(800f, 50f));

            TextMeshProUGUI bestValueText = UIFactory.CreateText(
                "BestValue", card, "0", 64f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(bestValueText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -465f), new Vector2(800f, 80f));
            bestValue = bestValueText;

            TextMeshProUGUI authHintText = UIFactory.CreateText(
                "AuthHint",
                card,
                "Авторизуйтесь, чтобы сохранить результат в таблице лидеров",
                28f,
                GameTheme.TextSecondary,
                TextAlignmentOptions.Center,
                FontStyles.Normal);
            authHintText.enableWordWrapping = true;
            UIFactory.Anchor(
                authHintText.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 250f),
                new Vector2(720f, 90f));
            authHintText.gameObject.SetActive(false);
            authHint = authHintText;

            authButton = UIFactory.CreateButton(
                "AuthButton",
                card,
                "АВТОРИЗОВАТЬСЯ",
                GameTheme.ButtonSecondary,
                GameTheme.TextPrimary,
                32f);
            UIFactory.Anchor(
                (RectTransform)authButton.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 145f),
                new Vector2(480f, 90f));
            authButton.gameObject.SetActive(false);

            continueButton = UIFactory.CreateButton(
                "ContinueButton",
                card,
                "Продолжить — реклама",
                GameTheme.ButtonSecondary,
                GameTheme.TextPrimary,
                32f);
            UIFactory.Anchor(
                (RectTransform)continueButton.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 144f),
                new Vector2(480f, 120f));
            continueButton.gameObject.SetActive(false);

            button = UIFactory.CreateButton(
                "RestartButton", card, "НАЧАТЬ ЗАНОВО", GameTheme.Accent, GameTheme.FromHex("#1a1a2e"), 36f);
            UIFactory.Anchor(
                (RectTransform)button.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 28f),
                new Vector2(480f, 100f));

            panel = root.gameObject.AddComponent<GameOverPanel>();
            panel.Bind(
                gameManager, group, card, scoreValue, bestValue, badge, button, authButton, authHint, continueButton);
            return panel;
        }
    }
}
