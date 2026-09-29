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
        private const float TopPanelHeight = 180f;
        private const float ScreenSideMargin = 16f;
        private const float ScreenTopMargin = 12f;
        private const float PauseButtonSize = 140f;
        private const float PauseRightPadding = 15f;
        private const float HudButtonGap = 40f;
        private const float ScoreSectionWidth = 400f;
        private const float ScoreSectionHeight = 92f;
        private const float BestSectionHeight = 60f;
        private const float ScoreFont = 76f;
        private const float BestFont = 44f;
        private const float BoardVerticalOffset = 30f;
        private const float BoardPadding = 14f;
        private const float SpawnAreaHeight = 280f;
        private const float SpawnAreaBottomMargin = 16f;
        private const float SpawnAreaSideMargin = 30f;
        private const float BoosterBarSideMargin = 30f;

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
            public BoosterConfirmPanel BoosterConfirmPanel;
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

            // Confirm below pause, pause below shop, shop below game over.
            result.BoosterConfirmPanel = CreateBoosterConfirmPanel(canvasRect, result.GameManager);
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
            result.BoosterBar?.Bind(result.GameManager, result.BoosterConfirmPanel);

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

        /// <summary>
        /// Adds the rewarded-booster confirm overlay to a baked scene that predates it,
        /// then wires it to the live <see cref="BoosterBar"/>.
        /// </summary>
        public static BoosterConfirmPanel EnsureBoosterConfirmPanel(RectTransform canvasRect, GameManager gameManager)
        {
            BoosterConfirmPanel existing = Object.FindObjectOfType<BoosterConfirmPanel>(true);
            BoosterBar bar = Object.FindObjectOfType<BoosterBar>(true);
            if (existing != null)
            {
                existing.Bind(gameManager);
                PlaceConfirmBelowPause(existing);
                bar?.Bind(gameManager, existing);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            BoosterConfirmPanel panel = CreateBoosterConfirmPanel(canvasRect, gameManager);
            PlaceConfirmBelowPause(panel);
            bar?.Bind(gameManager, panel);
            return panel;
        }

        /// <summary>
        /// Builds the full-screen main menu, or rebinds the one already in the scene.
        /// It is created last so it covers the HUD, the board and every other overlay.
        /// </summary>
        public static MainMenuPanel EnsureMainMenuPanel(RectTransform canvasRect, GameManager gameManager)
        {
            MainMenuPanel existing = Object.FindObjectOfType<MainMenuPanel>(true);
            if (existing != null)
            {
                existing.Bind(gameManager);
                return existing;
            }

            return canvasRect != null ? CreateMainMenuPanel(canvasRect, gameManager) : null;
        }

        private static MainMenuPanel CreateMainMenuPanel(RectTransform parent, GameManager gameManager)
        {
            // The menu builds itself (background, logo, buttons, profile widgets); see MainMenuPanel.
            return MainMenuPanel.Create(parent, gameManager);
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
                // Baked scenes still hold the old square cart button: restyle it in place.
                HudShopButton.Setup(existing);
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

        private static void PlaceConfirmBelowPause(BoosterConfirmPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            PausePanel pause = Object.FindObjectOfType<PausePanel>(true);
            if (pause != null)
            {
                panel.transform.SetSiblingIndex(pause.transform.GetSiblingIndex());
                return;
            }

            ShopPanel shop = Object.FindObjectOfType<ShopPanel>(true);
            if (shop != null)
            {
                panel.transform.SetSiblingIndex(shop.transform.GetSiblingIndex());
                return;
            }

            GameOverPanel gameOver = Object.FindObjectOfType<GameOverPanel>(true);
            if (gameOver != null)
            {
                panel.transform.SetSiblingIndex(gameOver.transform.GetSiblingIndex());
            }
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

            // Left column: big score with the record (crown) under it. Positions are refined by OrientationHandler.
            TMP_Text scoreValue = CreateHudStat(
                panel,
                "ScoreSection",
                "ScoreText",
                "0",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(12f, -4f),
                new Vector2(ScoreSectionWidth, ScoreSectionHeight),
                TextAlignmentOptions.MidlineLeft,
                ScoreFont,
                44f);

            TMP_Text bestValue = CreateHudStat(
                panel,
                "BestSection",
                "BestText",
                "0",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(12f, -4f - ScoreSectionHeight - 8f),
                new Vector2(ScoreSectionWidth, BestSectionHeight),
                TextAlignmentOptions.MidlineLeft,
                BestFont,
                30f);
            HudController.EnsureCrown(bestValue.transform.parent);

            shopButton = CreateHudShopButton(panel);
            pauseButton = CreatePauseButton(panel);

            // Hidden until the run beats the record; the scene may still call it ComboLabel.
            TextMeshProUGUI record = UIFactory.CreateText(
                "RecordLabel", panel, string.Empty, 44f, GameTheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(
                record.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -8f),
                new Vector2(700f, 60f));
            record.color = new Color(GameTheme.Accent.r, GameTheme.Accent.g, GameTheme.Accent.b, 0f);

            var hud = panel.gameObject.AddComponent<HudController>();
            hud.Bind(scoreManager, scoreValue, bestValue, record);
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
            TextAlignmentOptions alignment,
            float fontSize,
            float minFontSize)
        {
            RectTransform section = UIFactory.CreateRect(sectionName, parent);
            UIFactory.Anchor(section, anchor, pivot, position, size);

            TextMeshProUGUI text = UIFactory.CreateText(
                textName, section, initialText, fontSize, GameTheme.TextPrimary, alignment, FontStyles.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = minFontSize;
            text.fontSizeMax = fontSize;
            return text;
        }

        /// <summary>
        /// Pause control on the right edge of TopPanel. Behaviour is owned by <see cref="PausePanel"/>.
        /// </summary>
        private static Button CreatePauseButton(RectTransform parent)
        {
            Image background = UIFactory.CreateImage(
                "PauseButton", parent, GameTheme.HudButton);

            UIFactory.Anchor(
                background.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-PauseRightPadding, 0f),
                new Vector2(PauseButtonSize, PauseButtonSize));

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ApplyButtonColors(button);

            // Two bars drawn from plain rects, so the icon needs no texture of its own.
            for (int i = 0; i < 2; i++)
            {
                Image bar = UIFactory.CreateImage($"Bar_{i}", background.rectTransform, GameTheme.HudButtonIcon);
                bar.raycastTarget = false;
            }

            LayoutPauseBars(background.rectTransform, PauseButtonSize);
            return button;
        }

        /// <summary>
        /// Green "SHOP" pill left of pause on TopPanel (see <see cref="HudShopButton"/> for the look).
        /// Behaviour is owned by <see cref="ShopPanel"/>.
        /// </summary>
        private static Button CreateHudShopButton(RectTransform parent)
        {
            Image root = UIFactory.CreateImage("ShopButton", parent, Color.clear, false);
            UIFactory.Anchor(
                root.rectTransform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-(PauseRightPadding + PauseButtonSize + HudButtonGap), 0f),
                new Vector2(HudShopButton.MinWidth, HudShopButton.Height));

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root;
            HudShopButton.Setup(button);
            return button;
        }

        private static void LayoutPauseBars(RectTransform pause, float size)
        {
            if (pause == null)
            {
                return;
            }

            float barWidth = Mathf.Max(12f, size * 0.17f);
            float barHeight = Mathf.Max(30f, size * 0.56f);
            float offset = Mathf.Max(12f, size * 0.19f);

            for (int i = 0; i < 2; i++)
            {
                var bar = pause.Find($"Bar_{i}") as RectTransform;
                if (bar == null)
                {
                    continue;
                }

                UIFactory.Anchor(
                    bar,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(i == 0 ? -offset : offset, 0f),
                    new Vector2(barWidth, barHeight));
            }
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

        private static BoosterConfirmPanel CreateBoosterConfirmPanel(RectTransform parent, GameManager gameManager)
        {
            RectTransform root = UIFactory.CreateRect(BoosterConfirmPanel.ObjectName, parent);
            UIFactory.Stretch(root);

            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image dim = UIFactory.CreateImage("Dim", root, new Color(0.03f, 0.03f, 0.08f, 0.78f), false);
            UIFactory.Stretch(dim.rectTransform);

            Image cardImage = UIFactory.CreateImage("Card", root, GameTheme.CardBackground);
            RectTransform card = cardImage.rectTransform;
            UIFactory.Anchor(
                card,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(780f, 1080f));

            Image icon = UIFactory.CreateImage("Icon", card, Color.white, rounded: false);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIFactory.Anchor(
                icon.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -40f),
                new Vector2(152f, 152f));

            TextMeshProUGUI title = UIFactory.CreateText(
                "Title",
                card,
                GameLocalization.UndoTitle,
                48f,
                GameTheme.TextPrimary,
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            UIFactory.Anchor(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -210f),
                new Vector2(700f, 70f));

            TextMeshProUGUI body = UIFactory.CreateText(
                "Body",
                card,
                GameLocalization.UndoBody,
                42f,
                GameTheme.TextPrimary,
                TextAlignmentOptions.Center,
                FontStyles.Normal);
            body.enableWordWrapping = true;
            UIFactory.Anchor(
                body.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -290f),
                new Vector2(680f, 190f));

            TextMeshProUGUI warning = UIFactory.CreateText(
                "Warning",
                card,
                GameLocalization.AdBonusWarning,
                34f,
                GameTheme.TextSecondary,
                TextAlignmentOptions.Center,
                FontStyles.Normal);
            warning.enableWordWrapping = true;
            UIFactory.Anchor(
                warning.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -490f),
                new Vector2(680f, 110f));

            Button watch = UIFactory.CreateButton(
                "WatchButton",
                card,
                GameLocalization.WatchAd,
                GameTheme.Accent,
                GameTheme.FromHex("#1a1a2e"),
                52f);
            // The main button is 180 tall, the one under it 140, with 28 between them.
            UIFactory.Anchor(
                (RectTransform)watch.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 212f),
                new Vector2(620f, 180f));

            Button cancel = UIFactory.CreateButton(
                "CancelButton",
                card,
                GameLocalization.Cancel,
                GameTheme.ButtonSecondary,
                GameTheme.TextPrimary,
                52f);
            UIFactory.Anchor(
                (RectTransform)cancel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 44f),
                new Vector2(620f, 140f));

            BoosterConfirmPanel panel = root.gameObject.AddComponent<BoosterConfirmPanel>();
            panel.Bind(gameManager, group, card, icon, title, body, warning, watch, cancel);
            return panel;
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
                "Title", card, GameLocalization.PauseTitle, 80f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -50f),
                new Vector2(700f, 90f));
            title.characterSpacing = 6f;

            sound = UIFactory.CreateButton(
                "SoundButton", card, GameLocalization.SoundOn, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 34f);
            UIFactory.Anchor(
                (RectTransform)sound.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 360f),
                new Vector2(620f, 100f));

            resume = UIFactory.CreateButton(
                "ResumeButton", card, GameLocalization.Resume, GameTheme.Accent, GameTheme.FromHex("#1a1a2e"), 44f);
            UIFactory.Anchor(
                (RectTransform)resume.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 200f),
                new Vector2(620f, 130f));

            restart = UIFactory.CreateButton(
                "RestartButton", card, GameLocalization.Restart, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 38f);
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

            if (prefab != null)
            {
                panel = Object.Instantiate(prefab, parent);
                panel.gameObject.name = "ShopPanel";
                UIFactory.Stretch((RectTransform)panel.transform);
                group = panel.GetComponent<CanvasGroup>();
                card = panel.transform.Find("Card") as RectTransform;
                panel.Bind(gameManager, hudShopButton, group, card);
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

            // The shop fills the whole screen. ShopPanel builds the scrolling content inside the card at
            // runtime, so an older baked prefab ends up with the same layout as a fresh one.
            Image cardImage = UIFactory.CreateImage("Card", root, GameTheme.CardBackground, false);
            card = cardImage.rectTransform;
            UIFactory.Stretch(card);

            panel = root.gameObject.AddComponent<ShopPanel>();
            panel.Bind(gameManager, hudShopButton, group, card);
            return panel;
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
                "Title", card, GameLocalization.GameOverTitle, 84f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(800f, 100f));

            TextMeshProUGUI badgeText = UIFactory.CreateText(
                "RecordBadge", card, GameLocalization.NewBest, 44f, GameTheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(badgeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(800f, 60f));
            badgeText.gameObject.SetActive(false);
            badge = badgeText;

            TextMeshProUGUI scoreCaption = UIFactory.CreateText(
                "ScoreCaption", card, GameLocalization.ScoreCaption, 36f, GameTheme.TextSecondary, TextAlignmentOptions.Center, FontStyles.Normal, FontRole.Body);
            UIFactory.Anchor(scoreCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(800f, 50f));

            TextMeshProUGUI scoreValueText = UIFactory.CreateText(
                "ScoreValue", card, "0", 100f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(scoreValueText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -285f), new Vector2(800f, 120f));
            scoreValue = scoreValueText;

            TextMeshProUGUI bestCaption = UIFactory.CreateText(
                "BestCaption", card, GameLocalization.BestCaption, 36f, GameTheme.TextSecondary, TextAlignmentOptions.Center, FontStyles.Normal, FontRole.Body);
            UIFactory.Anchor(bestCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(800f, 50f));

            TextMeshProUGUI bestValueText = UIFactory.CreateText(
                "BestValue", card, "0", 64f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(bestValueText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -465f), new Vector2(800f, 80f));
            bestValue = bestValueText;

            TextMeshProUGUI authHintText = UIFactory.CreateText(
                "AuthHint",
                card,
                GameLocalization.AuthHint,
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
                GameLocalization.SignIn,
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
                GameLocalization.ContinueAd,
                GameTheme.ShopBuy,
                GameTheme.ShopBuyLabel,
                40f);
            UIFactory.Anchor(
                (RectTransform)continueButton.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 144f),
                new Vector2(480f, 120f));
            continueButton.gameObject.SetActive(false);

            button = UIFactory.CreateButton(
                "RestartButton", card, GameLocalization.PlayAgain, GameTheme.Accent, GameTheme.FromHex("#1a1a2e"), 46f);
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
