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
        private const float ScoreSectionWidth = 320f;
        private const float BestSectionWidth = 280f;
        private const float BoardVerticalOffset = 30f;
        private const float BoardPadding = 14f;
        private const float SpawnAreaHeight = 280f;
        private const float SpawnAreaBottomMargin = 16f;
        private const float SpawnAreaSideMargin = 30f;

        /// <summary>Optional authored prefabs used when baking or bootstrapping the scene.</summary>
        public sealed class PrefabSet
        {
            public GridCellView GridCell;
            public BlockPiece BlockPiece;
            public GameOverPanel GameOverPanel;
            public PausePanel PausePanel;
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
            public HudController Hud;
            public GameOverPanel GameOverPanel;
            public PausePanel PausePanel;
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
            result.GameManager = managersGo.AddComponent<GameManager>();

            result.Hud = CreateTopPanel(safeArea, result.ScoreManager, out Button pauseButton);
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

            RectTransform topPanelRect = result.Hud != null ? (RectTransform)result.Hud.transform : null;
            RectTransform gridAreaRect = result.GridManager != null ? result.GridManager.BoardRoot : null;
            RectTransform spawnAreaRect = result.ShapeSpawner != null
                ? (RectTransform)result.ShapeSpawner.transform
                : null;
            RectTransform pauseButtonRect = pauseButton != null
                ? (RectTransform)pauseButton.transform
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
                result.ShapeSpawner);

            // Bake uses factory defaults; runtime layout adapts to the live aspect / orientation.
            if (Application.isPlaying)
            {
                orientationHandler.RefreshNow();
            }

            // The pause screen sits below the game over screen: losing overrides being paused.
            result.PausePanel = CreatePausePanel(
                canvasRect, result.GameManager, pauseButton, prefabs != null ? prefabs.PausePanel : null);
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

            return result;
        }

        /// <summary>Unbound pause overlay hierarchy, used when baking the PausePanel prefab.</summary>
        public static PausePanel BuildPausePanelHierarchy(RectTransform parent)
        {
            return CreatePausePanel(parent, null, null, null);
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
        }

        /// <summary>
        /// Single-row HUD: Score left, Best center, Pause right — no overlapping anchors.
        /// </summary>
        private static HudController CreateTopPanel(
            RectTransform parent,
            ScoreManager scoreManager,
            out Button pauseButton)
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
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
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

        private static void ApplyButtonColors(Button button)
        {
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.9f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
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
            area.anchoredPosition = new Vector2(0f, SpawnAreaBottomMargin);
            area.sizeDelta = new Vector2(-(SpawnAreaSideMargin * 2f), SpawnAreaHeight);

            var slots = new RectTransform[ShapeSpawner.SlotCount];
            for (int i = 0; i < ShapeSpawner.SlotCount; i++)
            {
                slots[i] = UIFactory.CreateRect($"Slot_{i}", area);
            }

            // The spawner owns the row layout, so slots stay evenly spread whatever the area size.
            var spawner = area.gameObject.AddComponent<ShapeSpawner>();
            spawner.Configure(grid, dragLayer, slots, library);
            spawner.SetPiecePrefab(piecePrefab);
            return spawner;
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
                panel.Bind(gameManager, group, card, scoreValue, bestValue, badge, button);
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
            UIFactory.Anchor(badgeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(800f, 60f));
            badgeText.gameObject.SetActive(false);
            badge = badgeText;

            TextMeshProUGUI scoreCaption = UIFactory.CreateText(
                "ScoreCaption", card, "СЧЁТ", 36f, GameTheme.TextSecondary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(scoreCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(800f, 50f));

            TextMeshProUGUI scoreValueText = UIFactory.CreateText(
                "ScoreValue", card, "0", 110f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(scoreValueText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(800f, 130f));
            scoreValue = scoreValueText;

            TextMeshProUGUI bestCaption = UIFactory.CreateText(
                "BestCaption", card, "РЕКОРД", 36f, GameTheme.TextSecondary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(bestCaption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(800f, 50f));

            TextMeshProUGUI bestValueText = UIFactory.CreateText(
                "BestValue", card, "0", 64f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(bestValueText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -520f), new Vector2(800f, 80f));
            bestValue = bestValueText;

            // Smaller and lower so the button stays clear of the score / record values above.
            button = UIFactory.CreateButton(
                "RestartButton", card, "НАЧАТЬ ЗАНОВО", GameTheme.Accent, GameTheme.FromHex("#1a1a2e"), 36f);
            UIFactory.Anchor(
                (RectTransform)button.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 28f),
                new Vector2(480f, 100f));

            panel = root.gameObject.AddComponent<GameOverPanel>();
            panel.Bind(gameManager, group, card, scoreValue, bestValue, badge, button);
            return panel;
        }
    }
}
