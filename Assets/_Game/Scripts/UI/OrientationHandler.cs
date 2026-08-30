using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Reacts to device orientation changes in real time: enables auto-rotation,
    /// recomputes TopPanel / SpawnArea / board chrome for portrait vs landscape, then
    /// asks <see cref="UIManager"/> to refit the 8x8 grid and spawn figures without
    /// resetting score or board state.
    /// </summary>
    [DefaultExecutionOrder(-190)]
    [DisallowMultipleComponent]
    public class OrientationHandler : MonoBehaviour
    {
        private const float LayoutSettleDelay = 0.1f;

        private const float PortraitTopMargin = 12f;
        private const float PortraitTopHeight = 164f;
        private const float PortraitSideMargin = 16f;
        private const float PortraitPauseSize = 148f;

        private const float LandscapeTopMargin = 8f;
        private const float LandscapeTopHeight = 132f;
        private const float LandscapeSideMargin = 12f;
        private const float LandscapePauseSize = 124f;

        private const float ScoreSectionWidth = 320f;
        private const float BestSectionWidth = 280f;
        private const float SectionSidePadding = 20f;
        private const float PauseRightPadding = 15f;
        private const float HudButtonGap = 12f;

        [Header("UI Панели")]
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform gridArea;
        [SerializeField] private RectTransform spawnArea;
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform pauseButton;
        [SerializeField] private RectTransform shopButton;

        [Header("Настройки размеров")]
        [SerializeField] private float portraitCellSize = 65f;
        [SerializeField] private float landscapeCellSize = 50f;
        [SerializeField] private float spawnAreaHeightPortrait = 140f;
        [SerializeField] private float spawnAreaHeightLandscape = 120f;
        [SerializeField] private bool useDynamicBoardFit = true;

        private ScreenOrientation lastOrientation;
        private Vector2Int lastResolution;
        private bool lastPortrait;
        private CanvasScaler canvasScaler;
        private UIManager uiManager;
        private SafeAreaHandler safeAreaHandler;
        private GridManager gridManager;
        private ShapeSpawner shapeSpawner;
        private Coroutine layoutRoutine;
        private bool layoutQueued;
        private bool topPanelMigrated;

        public static OrientationHandler Ensure(Canvas canvas = null)
        {
            OrientationHandler existing = FindObjectOfType<OrientationHandler>();
            if (existing != null)
            {
                return existing;
            }

            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas == null)
            {
                return null;
            }

            return canvas.gameObject.AddComponent<OrientationHandler>();
        }

        public void Configure(
            CanvasScaler scaler,
            RectTransform safeAreaRect,
            RectTransform topPanelRect,
            RectTransform gridAreaRect,
            RectTransform spawnAreaRect,
            RectTransform pauseButtonRect,
            GridManager grid,
            ShapeSpawner spawner,
            RectTransform shopButtonRect = null)
        {
            canvasScaler = scaler;
            safeArea = safeAreaRect;
            topPanel = topPanelRect;
            gridArea = gridAreaRect;
            spawnArea = spawnAreaRect;
            pauseButton = pauseButtonRect;
            shopButton = shopButtonRect;
            gridManager = grid;
            shapeSpawner = spawner;
        }

        private void Awake()
        {
            EnableAutorotation();
            ResolveReferences();
            lastOrientation = Screen.orientation;
            lastResolution = new Vector2Int(Screen.width, Screen.height);
            lastPortrait = IsPortrait();
        }

        private void Start()
        {
            UpdateLayout();
        }

        private void Update()
        {
            bool portrait = IsPortrait();
            bool orientationChanged = Screen.orientation != lastOrientation || portrait != lastPortrait;
            bool resolutionChanged = Screen.width != lastResolution.x || Screen.height != lastResolution.y;

            if (!orientationChanged && !resolutionChanged)
            {
                return;
            }

            lastOrientation = Screen.orientation;
            lastResolution = new Vector2Int(Screen.width, Screen.height);
            lastPortrait = portrait;
            QueueLayoutRefresh();
        }

        /// <summary>Public entry point used after a manual resize or scene rebuild.</summary>
        public void RefreshNow()
        {
            if (layoutRoutine != null)
            {
                StopCoroutine(layoutRoutine);
                layoutRoutine = null;
            }

            layoutQueued = false;
            UpdateLayout();
        }

        private void QueueLayoutRefresh()
        {
            if (layoutQueued)
            {
                return;
            }

            layoutQueued = true;
            if (layoutRoutine != null)
            {
                StopCoroutine(layoutRoutine);
            }

            layoutRoutine = StartCoroutine(UpdateLayoutWithDelay());
        }

        private IEnumerator UpdateLayoutWithDelay()
        {
            // Wait until Unity finishes swapping the backbuffer / safe-area after a rotate.
            yield return null;
            yield return new WaitForSecondsRealtime(LayoutSettleDelay);
            layoutQueued = false;
            layoutRoutine = null;
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            ResolveReferences();
            bool isPortrait = IsPortrait();

            safeAreaHandler?.Apply();
            ApplyCanvasScaler(isPortrait);
            MigrateLegacyTopPanelIfNeeded();
            ApplyTopPanel(isPortrait);
            Canvas.ForceUpdateCanvases();
            ApplyPauseButton(isPortrait);
            ApplyShopButton(isPortrait);
            ApplyHudSections(isPortrait);

            Canvas.ForceUpdateCanvases();

            if (useDynamicBoardFit && uiManager != null)
            {
                // UIManager sizes the board into the free band between TopPanel and SpawnArea.
                uiManager.FixLayoutForPC();
            }
            else
            {
                ApplyFixedFallbackLayout(isPortrait);
            }

            float shapeScale = isPortrait ? 1f : 0.85f;
            if (shapeSpawner != null)
            {
                shapeSpawner.UpdateShapeSizes(shapeScale);
            }

            lastOrientation = Screen.orientation;
            lastResolution = new Vector2Int(Screen.width, Screen.height);
            lastPortrait = isPortrait;
        }

        private void ApplyCanvasScaler(bool isPortrait)
        {
            if (canvasScaler == null)
            {
                return;
            }

            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Landscape needs height priority so the portrait-authored HUD still has vertical room.
            canvasScaler.matchWidthOrHeight = isPortrait ? 0.5f : 0.85f;
        }

        private void ApplyTopPanel(bool isPortrait)
        {
            if (topPanel == null)
            {
                return;
            }

            float topMargin = isPortrait ? PortraitTopMargin : LandscapeTopMargin;
            float height = isPortrait ? PortraitTopHeight : LandscapeTopHeight;
            float side = isPortrait ? PortraitSideMargin : LandscapeSideMargin;

            topPanel.anchorMin = new Vector2(0f, 1f);
            topPanel.anchorMax = new Vector2(1f, 1f);
            topPanel.pivot = new Vector2(0.5f, 1f);
            topPanel.offsetMin = new Vector2(side, -topMargin - height);
            topPanel.offsetMax = new Vector2(-side, -topMargin);
        }

        private void ApplyPauseButton(bool isPortrait)
        {
            if (pauseButton == null)
            {
                return;
            }

            float size = isPortrait ? PortraitPauseSize : LandscapePauseSize;

            // Pause lives inside TopPanel (right-center). Falls back cleanly if still a sibling.
            Transform parent = pauseButton.parent;
            bool insideTopPanel = topPanel != null && parent == topPanel;

            if (insideTopPanel)
            {
                pauseButton.anchorMin = new Vector2(1f, 0.5f);
                pauseButton.anchorMax = new Vector2(1f, 0.5f);
                pauseButton.pivot = new Vector2(1f, 0.5f);
                pauseButton.anchoredPosition = new Vector2(-PauseRightPadding, 0f);
            }
            else
            {
                float top = isPortrait ? PortraitTopMargin : LandscapeTopMargin;
                float side = isPortrait ? PortraitSideMargin : LandscapeSideMargin;
                pauseButton.anchorMin = new Vector2(1f, 1f);
                pauseButton.anchorMax = new Vector2(1f, 1f);
                pauseButton.pivot = new Vector2(1f, 1f);
                pauseButton.anchoredPosition = new Vector2(-side, -top);
            }

            pauseButton.sizeDelta = new Vector2(size, size);
            ScalePauseBars(pauseButton, size);
        }

        private void ApplyShopButton(bool isPortrait)
        {
            if (shopButton == null)
            {
                return;
            }

            float size = isPortrait ? PortraitPauseSize : LandscapePauseSize;
            Transform parent = shopButton.parent;
            bool insideTopPanel = topPanel != null && parent == topPanel;
            float pauseSize = pauseButton != null ? pauseButton.sizeDelta.x : size;

            if (insideTopPanel)
            {
                shopButton.anchorMin = new Vector2(1f, 0.5f);
                shopButton.anchorMax = new Vector2(1f, 0.5f);
                shopButton.pivot = new Vector2(1f, 0.5f);
                shopButton.anchoredPosition = new Vector2(-(PauseRightPadding + pauseSize + HudButtonGap), 0f);
            }
            else
            {
                float top = isPortrait ? PortraitTopMargin : LandscapeTopMargin;
                float side = isPortrait ? PortraitSideMargin : LandscapeSideMargin;
                shopButton.anchorMin = new Vector2(1f, 1f);
                shopButton.anchorMax = new Vector2(1f, 1f);
                shopButton.pivot = new Vector2(1f, 1f);
                shopButton.anchoredPosition = new Vector2(-(side + pauseSize + HudButtonGap), -top);
            }

            shopButton.sizeDelta = new Vector2(size, size);
            ScaleShopCart(shopButton, size);
        }

        private void ApplyHudSections(bool isPortrait)
        {
            if (topPanel == null)
            {
                return;
            }

            float height = isPortrait ? PortraitTopHeight : LandscapeTopHeight;
            float pauseSize = isPortrait ? PortraitPauseSize : LandscapePauseSize;
            float shopSize = shopButton != null ? pauseSize : 0f;
            float shopGap = shopButton != null ? HudButtonGap : 0f;
            float rightReserved = PauseRightPadding + pauseSize + shopGap + shopSize;

            // Remove a leftover HUD mute control if an older bake still has it.
            Transform leftoverSound = topPanel.Find("SoundToggle");
            if (leftoverSound != null)
            {
                leftoverSound.gameObject.SetActive(false);
            }

            RectTransform score = FindHudSection(topPanel, "ScoreSection", "ScoreGroup");
            RectTransform best = FindHudSection(topPanel, "BestSection", "BestGroup");

            if (score != null)
            {
                UIFactory.Anchor(
                    score,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(SectionSidePadding, 0f),
                    new Vector2(ScoreSectionWidth, height));
                CompactLegacyScoreBlock(score, TextAlignmentOptions.MidlineLeft);
            }

            if (best != null)
            {
                // Keep Best clear of the right controls on narrow canvases.
                float bestWidth = Mathf.Min(BestSectionWidth, Mathf.Max(160f, topPanel.rect.width - rightReserved - ScoreSectionWidth));
                UIFactory.Anchor(
                    best,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(bestWidth, height));
                CompactLegacyScoreBlock(best, TextAlignmentOptions.Center);
            }
        }

        /// <summary>
        /// Older baked scenes kept PauseButton as a SafeArea sibling above a tall TopPanel
        /// with Best anchored top-right — the overlap reported on Yandex WebGL. Reparent and
        /// flatten once so runtime layout matches the new left / center / right strip.
        /// </summary>
        private void MigrateLegacyTopPanelIfNeeded()
        {
            if (topPanelMigrated || topPanel == null || safeArea == null)
            {
                return;
            }

            topPanelMigrated = true;

            if (pauseButton != null && pauseButton.parent == safeArea)
            {
                pauseButton.SetParent(topPanel, false);
            }

            if (shopButton != null && shopButton.parent == safeArea)
            {
                shopButton.SetParent(topPanel, false);
            }

            if (pauseButton == null)
            {
                pauseButton = topPanel.Find("PauseButton") as RectTransform;
            }

            if (shopButton == null)
            {
                shopButton = topPanel.Find("ShopButton") as RectTransform;
            }

            // Hide obsolete caption rows when we still have Label + Value groups.
            CollapseLegacyLabels(FindHudSection(topPanel, "ScoreSection", "ScoreGroup"));
            CollapseLegacyLabels(FindHudSection(topPanel, "BestSection", "BestGroup"));
        }

        private static RectTransform FindHudSection(Transform panel, string primary, string legacy)
        {
            return (panel.Find(primary) as RectTransform) ?? (panel.Find(legacy) as RectTransform);
        }

        private static void CollapseLegacyLabels(RectTransform section)
        {
            if (section == null)
            {
                return;
            }

            for (int i = 0; i < section.childCount; i++)
            {
                Transform child = section.GetChild(i);
                if (child.name.EndsWith("Label"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private static void CompactLegacyScoreBlock(RectTransform section, TextAlignmentOptions alignment)
        {
            if (section == null)
            {
                return;
            }

            for (int i = 0; i < section.childCount; i++)
            {
                var child = section.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                if (child.name.EndsWith("Label"))
                {
                    continue;
                }

                UIFactory.Stretch(child);
                var text = child.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.alignment = alignment;
                    text.enableWordWrapping = false;
                    text.overflowMode = TextOverflowModes.Overflow;
                    text.enableAutoSizing = true;
                    text.fontSizeMin = 16f;
                    text.fontSizeMax = 36f;
                }
            }
        }

        private static void ScalePauseBars(RectTransform pause, float size)
        {
            float barWidth = Mathf.Max(8f, size * 0.14f);
            float barHeight = Mathf.Max(22f, size * 0.50f);
            float offset = Mathf.Max(8f, size * 0.15f);

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

        private static void ScaleShopCart(RectTransform shop, float size)
        {
            float bodyW = Mathf.Max(18f, size * 0.52f);
            float bodyH = Mathf.Max(12f, size * 0.34f);
            float wheel = Mathf.Max(7f, size * 0.16f);
            float handleW = Mathf.Max(4f, size * 0.08f);
            float handleH = Mathf.Max(10f, size * 0.28f);

            var body = shop.Find("CartBody") as RectTransform;
            if (body != null)
            {
                UIFactory.Anchor(
                    body,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-size * 0.04f, -size * 0.02f),
                    new Vector2(bodyW, bodyH));
            }

            for (int i = 0; i < 2; i++)
            {
                var wheelRect = shop.Find($"Wheel_{i}") as RectTransform;
                if (wheelRect == null)
                {
                    continue;
                }

                UIFactory.Anchor(
                    wheelRect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(i == 0 ? -size * 0.16f : size * 0.13f, -size * 0.28f),
                    new Vector2(wheel, wheel));
            }

            var handle = shop.Find("CartHandle") as RectTransform;
            if (handle != null)
            {
                UIFactory.Anchor(
                    handle,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(size * 0.24f, size * 0.12f),
                    new Vector2(handleW, handleH));
            }
        }

        /// <summary>
        /// Fallback used when UIManager is missing: fixed cell sizes from the inspector,
        /// matching the orientation brief.
        /// </summary>
        private void ApplyFixedFallbackLayout(bool isPortrait)
        {
            float cellSize = isPortrait ? portraitCellSize : landscapeCellSize;
            float spawnHeight = isPortrait ? spawnAreaHeightPortrait : spawnAreaHeightLandscape;

            float bannerReserve = GameTheme.ActiveBannerReserve;

            if (spawnArea != null)
            {
                spawnArea.pivot = new Vector2(0.5f, 0f);
                spawnArea.anchoredPosition = new Vector2(0f, (isPortrait ? 20f : 10f) + bannerReserve);

                if (isPortrait)
                {
                    spawnArea.anchorMin = new Vector2(0f, 0f);
                    spawnArea.anchorMax = new Vector2(1f, 0f);
                    spawnArea.sizeDelta = new Vector2(0f, spawnHeight);
                }
                else
                {
                    float trayWidth = cellSize * 8f;
                    spawnArea.anchorMin = new Vector2(0.5f, 0f);
                    spawnArea.anchorMax = new Vector2(0.5f, 0f);
                    spawnArea.sizeDelta = new Vector2(trayWidth, spawnHeight);
                }
            }

            RectTransform booster = safeArea != null ? safeArea.Find("BoosterBar") as RectTransform : null;
            float boosterReserved = 0f;
            if (booster != null)
            {
                boosterReserved = BoosterBar.BarHeight + BoosterBar.TrayGap;
            }

            if (gridArea != null)
            {
                gridArea.anchorMin = new Vector2(0.5f, 0.5f);
                gridArea.anchorMax = new Vector2(0.5f, 0.5f);
                gridArea.pivot = new Vector2(0.5f, 0.5f);

                float gridPixelSize = cellSize * 8f;
                gridArea.sizeDelta = new Vector2(gridPixelSize, gridPixelSize);

                float topHeight = isPortrait ? 90f : 65f;
                float bottomHeight = (isPortrait ? 160f : 130f) + bannerReserve + boosterReserved;
                float offsetY = (bottomHeight - topHeight) / 2f;
                gridArea.anchoredPosition = new Vector2(0f, offsetY);

                if (booster != null)
                {
                    booster.anchorMin = new Vector2(0f, 0f);
                    booster.anchorMax = new Vector2(1f, 0f);
                    booster.pivot = new Vector2(0.5f, 0f);
                    float spawnBottom = (isPortrait ? 20f : 10f) + bannerReserve;
                    float minY = spawnBottom + spawnHeight + BoosterBar.TrayGap;
                    float safeHeight = safeArea != null ? Mathf.Max(1f, safeArea.rect.height) : GameTheme.ReferenceHeight;
                    float gridBottom = safeHeight * 0.5f + offsetY - gridPixelSize * 0.5f;
                    float desiredY = gridBottom - BoosterBar.BoardGap - BoosterBar.BarHeight;
                    booster.anchoredPosition = new Vector2(0f, Mathf.Max(minY, desiredY));
                    booster.sizeDelta = new Vector2(0f, BoosterBar.BarHeight);
                }
            }

            if (gridManager != null)
            {
                gridManager.UpdateCellSize(cellSize);
            }
        }

        private static void EnableAutorotation()
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;

            if (Screen.orientation != ScreenOrientation.AutoRotation)
            {
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }

        private static bool IsPortrait() => Screen.width <= Screen.height;

        private void ResolveReferences()
        {
            if (canvasScaler == null)
            {
                canvasScaler = GetComponent<CanvasScaler>();
                if (canvasScaler == null)
                {
                    canvasScaler = FindObjectOfType<CanvasScaler>();
                }
            }

            if (uiManager == null)
            {
                uiManager = GetComponent<UIManager>();
                if (uiManager == null)
                {
                    uiManager = FindObjectOfType<UIManager>();
                }
            }

            if (safeArea == null)
            {
                Transform found = transform.Find("SafeArea");
                if (found == null)
                {
                    GameObject go = GameObject.Find("SafeArea");
                    found = go != null ? go.transform : null;
                }

                safeArea = found as RectTransform;
            }

            if (safeArea != null)
            {
                safeAreaHandler = safeArea.GetComponent<SafeAreaHandler>();

                if (topPanel == null)
                {
                    topPanel = safeArea.Find("TopPanel") as RectTransform;
                }

                if (spawnArea == null)
                {
                    spawnArea = safeArea.Find("SpawnArea") as RectTransform;
                }

                if (pauseButton == null && topPanel != null)
                {
                    pauseButton = topPanel.Find("PauseButton") as RectTransform;
                }

                if (pauseButton == null)
                {
                    pauseButton = safeArea.Find("PauseButton") as RectTransform;
                }

                if (shopButton == null && topPanel != null)
                {
                    shopButton = topPanel.Find("ShopButton") as RectTransform;
                }

                if (shopButton == null)
                {
                    shopButton = safeArea.Find("ShopButton") as RectTransform;
                }

                if (gridArea == null)
                {
                    Transform board = safeArea.Find("BoardPanel/GridArea");
                    gridArea = board as RectTransform;
                }
            }

            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
            }

            if (gridArea == null && gridManager != null)
            {
                gridArea = gridManager.BoardRoot;
            }

            if (shapeSpawner == null)
            {
                shapeSpawner = FindObjectOfType<ShapeSpawner>();
            }

            if (spawnArea == null && shapeSpawner != null)
            {
                spawnArea = shapeSpawner.transform as RectTransform;
            }
        }
    }
}
