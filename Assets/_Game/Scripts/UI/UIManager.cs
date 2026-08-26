using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Keeps the HUD readable on every aspect ratio. On wide / landscape screens the
    /// fixed 8x8 board would otherwise collide with the bottom spawn tray; this component
    /// re-anchors the tray, shrinks the board into the free vertical band and tunes the
    /// canvas scaler so height is preserved. Orientation flips are driven by
    /// <see cref="OrientationHandler"/>; this class still reacts to window resizes.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        private const float MinCellSize = 40f;
        private const float MaxCellSize = 90f;
        private const float BoardPadding = 14f;
        private const float SectionGap = 20f;
        private const float SideMargin = 30f;
        private const float PortraitSpawnHeight = 280f;
        private const float CompactSpawnHeight = 150f;
        private const float LandscapeSpawnHeight = 130f;
        private const float SpawnBottomPadding = 16f;
        private const float WideAspectThreshold = 1.5f;
        private const float WideMatchWidthOrHeight = 0.85f;
        private const float DefaultMatchWidthOrHeight = 0.5f;
        private const float LayoutSettleDelay = 0.05f;

        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform topPanel;
        [SerializeField] private RectTransform boardPanel;
        [SerializeField] private RectTransform gridArea;
        [SerializeField] private RectTransform spawnArea;
        [SerializeField] private RectTransform boosterBar;
        [SerializeField] private GridManager gridManager;
        [SerializeField] private ShapeSpawner shapeSpawner;

        private Vector2Int lastResolution;
        private float lastSpawnHeight = -1f;
        private Coroutine layoutRoutine;
        private OrientationHandler orientationHandler;

        public static UIManager Ensure(Canvas canvas = null)
        {
            UIManager existing = FindObjectOfType<UIManager>();
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

            return canvas.gameObject.AddComponent<UIManager>();
        }

        public void Configure(
            CanvasScaler scaler,
            RectTransform safeAreaRect,
            RectTransform topPanelRect,
            RectTransform boardPanelRect,
            RectTransform gridAreaRect,
            RectTransform spawnAreaRect,
            RectTransform boosterBarRect,
            GridManager grid,
            ShapeSpawner spawner)
        {
            canvasScaler = scaler;
            safeArea = safeAreaRect;
            topPanel = topPanelRect;
            boardPanel = boardPanelRect;
            gridArea = gridAreaRect;
            spawnArea = spawnAreaRect;
            boosterBar = boosterBarRect;
            gridManager = grid;
            shapeSpawner = spawner;
        }

        private void Awake()
        {
            ResolveReferences();
            FixLayoutForPC();
        }

        private void Start()
        {
            // One more pass after the first CanvasScaler rebuild so rect sizes are final.
            FixLayoutForPC();
        }

        private void Update()
        {
            // OrientationHandler owns rotate-to-relayout; still cover editor/PC window resizes.
            if (orientationHandler != null)
            {
#if UNITY_EDITOR
                LogLayoutDebug();
#endif
                return;
            }

            if (Screen.width != lastResolution.x || Screen.height != lastResolution.y)
            {
                QueueLayoutRefresh();
            }

#if UNITY_EDITOR
            LogLayoutDebug();
#endif
        }

        private void QueueLayoutRefresh()
        {
            if (layoutRoutine != null)
            {
                StopCoroutine(layoutRoutine);
            }

            layoutRoutine = StartCoroutine(FixLayoutWithDelay());
        }

        private IEnumerator FixLayoutWithDelay()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(LayoutSettleDelay);
            layoutRoutine = null;
            FixLayoutForPC();
        }

        /// <summary>
        /// Forces SpawnArea to the bottom edge, sizes the board to the free band between
        /// TopPanel and SpawnArea, and biases the canvas scaler toward height on wide screens.
        /// </summary>
        public void FixLayoutForPC()
        {
            ResolveReferences();
            if (safeArea == null || spawnArea == null || boardPanel == null || gridArea == null)
            {
                return;
            }

            lastResolution = new Vector2Int(Screen.width, Screen.height);
            ApplyCanvasScalerForAspect();
            Canvas.ForceUpdateCanvases();

            float safeHeight = Mathf.Max(1f, safeArea.rect.height);
            float safeWidth = Mathf.Max(1f, safeArea.rect.width);
            bool isPortrait = Screen.width <= Screen.height;

            float spawnHeight = ResolveSpawnHeight(safeHeight, isPortrait);
            float bannerReserve = GameTheme.ActiveBannerReserve;
            ApplySpawnAreaLayout(spawnHeight, bannerReserve);

            float topReserved = ResolveTopReservedHeight();
            float boosterReserved = boosterBar != null
                ? BoosterBar.BarHeight + BoosterBar.TrayGap
                : 0f;
            float spawnReserved = SpawnBottomPadding + bannerReserve + spawnHeight;
            float availableHeight = safeHeight - topReserved - spawnReserved - boosterReserved - SectionGap * 2f;
            float availableWidth = safeWidth - SideMargin * 2f;
            float maxBoardOuter = Mathf.Min(availableWidth, availableHeight);
            maxBoardOuter = Mathf.Max(maxBoardOuter, MinCellSize * GameTheme.GridSize);

            float spacing = GameTheme.CellSpacing;
            float cellSize = (maxBoardOuter - BoardPadding * 2f - (GameTheme.GridSize - 1) * spacing)
                / GameTheme.GridSize;
            cellSize = Mathf.Clamp(cellSize, MinCellSize, MaxCellSize);

            float boardSize = GameTheme.GridSize * cellSize + (GameTheme.GridSize - 1) * spacing;
            float panelSize = boardSize + BoardPadding * 2f;

            // Desktop / landscape: pin the tray to the board column so figures are not
            // stretched across the whole monitor. Portrait keeps the full-width tray.
            if (!isPortrait)
            {
                ApplySpawnAreaLayout(spawnHeight, bannerReserve, panelSize);
            }

            float zoneMin = spawnReserved + boosterReserved + SectionGap;
            float zoneMax = safeHeight - topReserved - SectionGap;
            float zoneCenter = (zoneMin + zoneMax) * 0.5f;
            float boardAnchoredY = zoneCenter - safeHeight * 0.5f;

            boardPanel.anchorMin = new Vector2(0.5f, 0.5f);
            boardPanel.anchorMax = new Vector2(0.5f, 0.5f);
            boardPanel.pivot = new Vector2(0.5f, 0.5f);
            boardPanel.anchoredPosition = new Vector2(0f, boardAnchoredY);
            boardPanel.sizeDelta = new Vector2(panelSize, panelSize);

            gridArea.anchorMin = new Vector2(0.5f, 0.5f);
            gridArea.anchorMax = new Vector2(0.5f, 0.5f);
            gridArea.pivot = new Vector2(0.5f, 0.5f);
            gridArea.anchoredPosition = Vector2.zero;
            gridArea.sizeDelta = new Vector2(boardSize, boardSize);

            ApplyBoosterBarLayout(spawnHeight, bannerReserve, panelSize, boardAnchoredY, safeHeight);

            if (gridManager != null)
            {
                gridManager.ApplyCellSize(cellSize, spacing);
            }

            if (shapeSpawner != null)
            {
                // OrientationHandler may refine scale again; keep tray figures in sync here too.
                float shapeScale = isPortrait ? 1f : 0.85f;
                shapeSpawner.UpdateShapeSizes(shapeScale);
            }

            lastSpawnHeight = spawnHeight;
        }

        private void ApplyCanvasScalerForAspect()
        {
            if (canvasScaler == null)
            {
                return;
            }

            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(GameTheme.ReferenceWidth, GameTheme.ReferenceHeight);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.referencePixelsPerUnit = 100f;

            // Portrait UI on a wide / landscape screen needs height priority, otherwise the canvas
            // becomes too short and SpawnArea climbs into the board.
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            canvasScaler.matchWidthOrHeight = aspect > WideAspectThreshold
                ? WideMatchWidthOrHeight
                : DefaultMatchWidthOrHeight;
        }

        private void ApplySpawnAreaLayout(float height, float bannerReserve, float compactWidth = -1f)
        {
            spawnArea.pivot = new Vector2(0.5f, 0f);
            spawnArea.anchoredPosition = new Vector2(0f, SpawnBottomPadding + bannerReserve);

            if (compactWidth > 0f)
            {
                spawnArea.anchorMin = new Vector2(0.5f, 0f);
                spawnArea.anchorMax = new Vector2(0.5f, 0f);
                spawnArea.sizeDelta = new Vector2(compactWidth, height);
                return;
            }

            spawnArea.anchorMin = new Vector2(0f, 0f);
            spawnArea.anchorMax = new Vector2(1f, 0f);
            spawnArea.sizeDelta = new Vector2(-(SideMargin * 2f), height);
        }

        private void ApplyBoosterBarLayout(
            float spawnHeight,
            float bannerReserve,
            float panelSize,
            float boardAnchoredY,
            float safeHeight)
        {
            if (boosterBar == null)
            {
                return;
            }

            boosterBar.anchorMin = new Vector2(0f, 0f);
            boosterBar.anchorMax = new Vector2(1f, 0f);
            boosterBar.pivot = new Vector2(0.5f, 0f);

            float spawnTop = SpawnBottomPadding + bannerReserve + spawnHeight;
            float minY = spawnTop + BoosterBar.TrayGap;
            float boardBottom = safeHeight * 0.5f + boardAnchoredY - panelSize * 0.5f;
            float desiredY = boardBottom - BoosterBar.BoardGap - BoosterBar.BarHeight;

            boosterBar.anchoredPosition = new Vector2(0f, Mathf.Max(minY, desiredY));
            boosterBar.sizeDelta = new Vector2(-(SideMargin * 2f), BoosterBar.BarHeight);
        }

        private static float ResolveSpawnHeight(float safeHeight, bool isPortrait)
        {
            if (!isPortrait)
            {
                return Mathf.Clamp(safeHeight * 0.22f, LandscapeSpawnHeight, CompactSpawnHeight);
            }

            // Short canvases get a compact tray; tall phones keep the large one.
            if (safeHeight < 1200f)
            {
                return Mathf.Clamp(safeHeight * 0.2f, CompactSpawnHeight, PortraitSpawnHeight);
            }

            return PortraitSpawnHeight;
        }

        private float ResolveTopReservedHeight()
        {
            if (topPanel == null)
            {
                return 120f;
            }

            // Top-anchored panel: anchoredPosition / offsets place the top edge; rect extends down.
            float fromTop = -topPanel.offsetMax.y;
            if (fromTop < 1f)
            {
                fromTop = -topPanel.anchoredPosition.y;
            }

            fromTop += topPanel.rect.height > 1f
                ? topPanel.rect.height
                : Mathf.Abs(topPanel.offsetMin.y - topPanel.offsetMax.y);

            return fromTop > 60f ? fromTop : 120f;
        }

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

            if (orientationHandler == null)
            {
                orientationHandler = GetComponent<OrientationHandler>();
                if (orientationHandler == null)
                {
                    orientationHandler = FindObjectOfType<OrientationHandler>();
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
                if (topPanel == null)
                {
                    topPanel = safeArea.Find("TopPanel") as RectTransform;
                }

                if (boardPanel == null)
                {
                    boardPanel = safeArea.Find("BoardPanel") as RectTransform;
                }

                if (spawnArea == null)
                {
                    spawnArea = safeArea.Find("SpawnArea") as RectTransform;
                }

                if (boosterBar == null)
                {
                    boosterBar = safeArea.Find("BoosterBar") as RectTransform;
                }
            }

            if (gridArea == null && boardPanel != null)
            {
                gridArea = boardPanel.Find("GridArea") as RectTransform;
            }

            if (gridManager == null)
            {
                gridManager = FindObjectOfType<GridManager>();
            }

            if (shapeSpawner == null)
            {
                shapeSpawner = FindObjectOfType<ShapeSpawner>();
            }

            if (gridArea == null && gridManager != null)
            {
                gridArea = gridManager.BoardRoot;
            }

            if (spawnArea == null && shapeSpawner != null)
            {
                spawnArea = shapeSpawner.transform as RectTransform;
            }

            if (boosterBar == null)
            {
                BoosterBar bar = FindObjectOfType<BoosterBar>(true);
                if (bar != null)
                {
                    boosterBar = (RectTransform)bar.transform;
                }
            }
        }

#if UNITY_EDITOR
        private float debugTimer;

        private void LogLayoutDebug()
        {
            debugTimer -= Time.unscaledDeltaTime;
            if (debugTimer > 0f || spawnArea == null || gridArea == null)
            {
                return;
            }

            debugTimer = 1f;
            Vector3[] spawnCorners = new Vector3[4];
            Vector3[] gridCorners = new Vector3[4];
            spawnArea.GetWorldCorners(spawnCorners);
            gridArea.GetWorldCorners(gridCorners);

            float spawnYMax = spawnCorners[1].y;
            float gridYMin = gridCorners[0].y;
            if (spawnYMax > gridYMin + 0.5f)
            {
                Debug.LogWarning(
                    $"[UIManager] SpawnArea overlaps GridArea (spawnYMax={spawnYMax:F1} > gridYMin={gridYMin:F1}). " +
                    $"spawnH={lastSpawnHeight:F0} screen={Screen.width}x{Screen.height}");
            }
        }

        private void OnDrawGizmos()
        {
            if (spawnArea == null || gridArea == null)
            {
                return;
            }

            DrawRectGizmo(spawnArea, new Color(0.2f, 0.9f, 0.4f, 0.35f));
            DrawRectGizmo(gridArea, new Color(0.2f, 0.5f, 1f, 0.35f));
        }

        private static void DrawRectGizmo(RectTransform rect, Color color)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Gizmos.color = color;
            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }
        }
#endif
    }
}
