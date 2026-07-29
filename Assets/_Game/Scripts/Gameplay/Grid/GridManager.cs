using System;
using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Vfx;

namespace BlockPuzzle.Grid
{
    /// <summary>
    /// View layer of the 8x8 board. Owns a <see cref="GridModel"/>, keeps the cell views in
    /// sync with it and translates screen coordinates into board coordinates for dragging.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private RectTransform boardRoot;
        [SerializeField] private GridCellView cellPrefab;
        [SerializeField] private int size = GameTheme.GridSize;
        [SerializeField] private float cellSize = GameTheme.CellSize;
        [SerializeField] private float spacing = GameTheme.CellSpacing;

        [Header("Starting Layout")]
        [Tooltip("How many decorative figures the board is pre-filled with when a run begins.")]
        [SerializeField, Min(0)] private int startingShapeCount = 3;

        [Tooltip("Empty margin kept around every starting figure, picked at random per figure.")]
        [SerializeField, Min(0)] private int minStartingGap = 1;
        [SerializeField, Min(0)] private int maxStartingGap = 2;

        [Header("Drag Feedback")]
        [SerializeField] private HighlightGrid highlight;

        [Header("Effects")]
        [SerializeField] private SparkBurst sparks;

        private GridModel model;
        private GridCellView[,] cells;
        private readonly List<Vector2Int> highlightedCells = new List<Vector2Int>();
        private readonly List<Vector2Int> startingOrigins = new List<Vector2Int>();
        private readonly List<Color> clearedColors = new List<Color>();
        private List<BlockShape> startingShapes;
        private Canvas parentCanvas;

        /// <summary>Raised after a figure has been successfully dropped on the board.</summary>
        public event Action<PlacementResult> ShapePlaced;

        public GridModel Model => model;
        public SparkBurst Sparks => sparks;
        public int Size => size;
        public float CellSize => cellSize;
        public float Pitch => cellSize + spacing;
        public RectTransform BoardRoot => boardRoot;

        /// <summary>Optional authored cell. When null, cells are built in code.</summary>
        public void SetCellPrefab(GridCellView prefab) => cellPrefab = prefab;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (boardRoot == null)
            {
                boardRoot = (RectTransform)transform;
            }

            parentCanvas = GetComponentInParent<Canvas>();
            model = new GridModel(size);
            BindOrBuildCells();
            BindOrBuildHighlight();
            BindOrBuildSparks();
            RedrawAll();
        }

        /// <summary>Alias used by orientation / layout handlers.</summary>
        public void UpdateCellSize(float newCellSize) => ApplyCellSize(newCellSize);

        /// <summary>
        /// Resizes every cell and the board root so the 8x8 grid fits the free vertical band
        /// between TopPanel and SpawnArea on short (wide) screens.
        /// </summary>
        public void ApplyCellSize(float newCellSize, float newSpacing = -1f)
        {
            if (boardRoot == null)
            {
                boardRoot = (RectTransform)transform;
            }

            cellSize = Mathf.Max(1f, newCellSize);
            if (newSpacing >= 0f)
            {
                spacing = newSpacing;
            }

            float boardSize = size * cellSize + (size - 1) * spacing;
            boardRoot.sizeDelta = new Vector2(boardSize, boardSize);

            if (cells == null)
            {
                return;
            }

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    GridCellView view = cells[row, col];
                    if (view != null)
                    {
                        view.Configure(new Vector2Int(col, row), cellSize, Pitch);
                    }
                }
            }

            highlight?.Configure(cellSize, Pitch);
        }

        /// <summary>
        /// Grid position a figure would snap to while it is being dragged, derived from the point
        /// its top-left cell currently covers. The result is the nearest cell rather than the one
        /// strictly under the point, so the figure locks onto the grid as soon as it is roughly in
        /// place, and it may fall outside the board so a figure hanging over an edge can still be
        /// highlighted as invalid. False means the figure does not touch the board at all.
        /// </summary>
        public bool TryGetDropOrigin(Vector2 screenPoint, BlockShape shape, out Vector2Int origin)
        {
            origin = default;

            if (shape == null || !TryGetNearestCell(screenPoint, out origin))
            {
                return false;
            }

            return OverlapsBoard(shape, origin);
        }

        /// <summary>World position of the centre of a cell, used to snap dragged figures.</summary>
        public Vector3 GetCellWorldPosition(Vector2Int coordinate)
        {
            Rect rect = boardRoot.rect;
            var local = new Vector2(
                rect.xMin + coordinate.x * Pitch + cellSize * 0.5f,
                rect.yMax - (coordinate.y * Pitch + cellSize * 0.5f));

            return boardRoot.TransformPoint(local);
        }

        public bool CanPlace(BlockShape shape, Vector2Int origin) => model != null && model.CanPlace(shape, origin);

        public bool HasPlacementFor(BlockShape shape) => model != null && model.HasPlacementFor(shape);

        public bool HasPlacementForAny(IEnumerable<BlockShape> shapes) => model != null && model.HasPlacementForAny(shapes);

        /// <summary>
        /// Marks the cells the figure would occupy on the <see cref="HighlightGrid"/>: green when
        /// the whole figure fits on free ground, red when a cell is taken or the figure sticks out
        /// over an edge. Cells outside the board cannot be drawn, so only the part that is over
        /// the board is marked.
        /// </summary>
        public void ShowDropHighlight(BlockShape shape, Vector2Int origin)
        {
            if (shape == null || highlight == null)
            {
                return;
            }

            highlightedCells.Clear();
            IReadOnlyList<Vector2Int> shapeCells = shape.Cells;

            for (int i = 0; i < shapeCells.Count; i++)
            {
                Vector2Int target = origin + shapeCells[i];
                if (model.IsInside(target))
                {
                    highlightedCells.Add(target);
                }
            }

            highlight.Show(highlightedCells, model.CanPlace(shape, origin));
        }

        public void HideDropHighlight()
        {
            highlightedCells.Clear();

            if (highlight != null)
            {
                highlight.Hide();
            }
        }

        /// <summary>Commits the figure to the board and resolves completed lines.</summary>
        public PlacementResult PlaceShape(BlockShape shape, Vector2Int origin)
        {
            if (shape == null || !model.CanPlace(shape, origin))
            {
                return PlacementResult.Failed;
            }

            HideDropHighlight();

            List<Vector2Int> placed = model.Place(shape, origin);
            for (int i = 0; i < placed.Count; i++)
            {
                cells[placed[i].y, placed[i].x].SetFilledAnimated(shape.Color);
            }

            LineClearResult lines = model.FindCompletedLines();
            if (lines.HasLines)
            {
                // The colours have to be read before the model forgets them: the sparks and the
                // fade-out are painted in the shade of the blocks that are being removed.
                CaptureClearedColors(lines);
                model.ApplyClear(lines);
                PlayClearFeedback(lines);
            }

            var result = new PlacementResult(true, shape.BlockCount, lines.LineCount, lines.Cells.Count);
            ShapePlaced?.Invoke(result);
            return result;
        }

        public void ResetBoard()
        {
            HideDropHighlight();
            sparks?.Clear();
            CancelCellAnimations();
            model.Reset();
            RedrawAll();
        }

        /// <summary>
        /// Prepares the board for a new run: wipes it clean and then scatters the dim starting
        /// figures over it. None of them may finish a row or a column, so the player never starts
        /// with a free line clear.
        /// </summary>
        public void StartGame()
        {
            ResetBoard();
            ScatterStartingShapes();
        }

        private void ScatterStartingShapes()
        {
            List<BlockShape> palette = GetStartingShapes();
            if (palette.Count == 0)
            {
                return;
            }

            int target = Mathf.Max(0, startingShapeCount);

            // A figure can be rejected by the gap rule or by the no-line rule, so a miss is retried
            // with a freshly drawn figure instead of costing us one of the requested figures.
            int placed = 0;
            int attemptsLeft = target * 8;

            while (placed < target && attemptsLeft > 0)
            {
                attemptsLeft--;
                BlockShape shape = palette[UnityEngine.Random.Range(0, palette.Count)];
                if (TryPlaceStartingShape(shape))
                {
                    placed++;
                }
            }
        }

        private bool TryPlaceStartingShape(BlockShape shape)
        {
            int lowGap = Mathf.Min(minStartingGap, maxStartingGap);
            int gap = UnityEngine.Random.Range(lowGap, Mathf.Max(minStartingGap, maxStartingGap) + 1);

            // Shrink the requested margin rather than dropping the figure when the board gets busy.
            for (; gap >= 0; gap--)
            {
                CollectStartingOrigins(shape, gap);
                if (startingOrigins.Count == 0)
                {
                    continue;
                }

                Vector2Int origin = startingOrigins[UnityEngine.Random.Range(0, startingOrigins.Count)];
                CommitStartingShape(shape, origin);
                return true;
            }

            return false;
        }

        private void CollectStartingOrigins(BlockShape shape, int gap)
        {
            startingOrigins.Clear();

            for (int row = 0; row <= size - shape.Height; row++)
            {
                for (int col = 0; col <= size - shape.Width; col++)
                {
                    var origin = new Vector2Int(col, row);
                    if (HasClearance(shape, origin, gap) && !model.WouldCompleteLine(shape, origin))
                    {
                        startingOrigins.Add(origin);
                    }
                }
            }
        }

        /// <summary>True when the figure and the <paramref name="gap"/> ring around it are empty.</summary>
        private bool HasClearance(BlockShape shape, Vector2Int origin, int gap)
        {
            IReadOnlyList<Vector2Int> shapeCells = shape.Cells;

            for (int i = 0; i < shapeCells.Count; i++)
            {
                Vector2Int target = origin + shapeCells[i];
                for (int row = target.y - gap; row <= target.y + gap; row++)
                {
                    for (int col = target.x - gap; col <= target.x + gap; col++)
                    {
                        if (model.IsOccupied(row, col))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private void CommitStartingShape(BlockShape shape, Vector2Int origin)
        {
            List<Vector2Int> placed = model.Place(shape, origin);
            for (int i = 0; i < placed.Count; i++)
            {
                cells[placed[i].y, placed[i].x]?.SetFilled(shape.Color);
            }
        }

        private List<BlockShape> GetStartingShapes()
        {
            return startingShapes ??= StartingShapeCatalog.CreateStartingShapes();
        }

        /// <summary>
        /// Grid coordinate whose centre is closest to <paramref name="screenPoint"/>. The result is
        /// never clamped, so a point past an edge maps to a coordinate outside the board.
        /// </summary>
        private bool TryGetNearestCell(Vector2 screenPoint, out Vector2Int coordinate)
        {
            coordinate = default;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    boardRoot, screenPoint, EventCamera, out Vector2 local))
            {
                return false;
            }

            Rect rect = boardRoot.rect;
            float fromLeft = local.x - rect.xMin - cellSize * 0.5f;
            float fromTop = rect.yMax - local.y - cellSize * 0.5f;

            coordinate = new Vector2Int(
                Mathf.RoundToInt(fromLeft / Pitch),
                Mathf.RoundToInt(fromTop / Pitch));

            return true;
        }

        /// <summary>True when at least one cell of the figure lands on the board.</summary>
        private bool OverlapsBoard(BlockShape shape, Vector2Int origin)
        {
            IReadOnlyList<Vector2Int> shapeCells = shape.Cells;

            for (int i = 0; i < shapeCells.Count; i++)
            {
                if (model.IsInside(origin + shapeCells[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private Camera EventCamera
        {
            get
            {
                if (parentCanvas == null)
                {
                    parentCanvas = GetComponentInParent<Canvas>();
                }

                if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return null;
                }

                return parentCanvas.worldCamera;
            }
        }

        private void BindOrBuildCells()
        {
            cells = new GridCellView[size, size];

            GridCellView[] existing = boardRoot.GetComponentsInChildren<GridCellView>(true);
            if (existing.Length == size * size)
            {
                foreach (GridCellView view in existing)
                {
                    Vector2Int coordinate = view.Coordinate;
                    if (model.IsInside(coordinate))
                    {
                        cells[coordinate.y, coordinate.x] = view;
                    }
                }

                if (AllCellsBound())
                {
                    return;
                }
            }

            for (int i = existing.Length - 1; i >= 0; i--)
            {
                DestroyImmediate(existing[i].gameObject);
            }

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    cells[row, col] = GridCellView.Create(
                        boardRoot, new Vector2Int(col, row), cellSize, Pitch, cellPrefab);
                }
            }
        }

        /// <summary>
        /// Picks up the overlay baked into the scene or creates it on the spot, always as the last
        /// child of the board so the drop hint is drawn over the cells.
        /// </summary>
        private void BindOrBuildHighlight()
        {
            if (highlight == null)
            {
                highlight = boardRoot.GetComponentInChildren<HighlightGrid>(true);
            }

            if (highlight == null)
            {
                highlight = HighlightGrid.Create(boardRoot, cellSize, Pitch);
            }
            else
            {
                highlight.Configure(cellSize, Pitch);
            }

            highlight.transform.SetAsLastSibling();
        }

        private bool AllCellsBound()
        {
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (cells[row, col] == null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void RedrawAll()
        {
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    RedrawCell(new Vector2Int(col, row));
                }
            }
        }

        private void RedrawCell(Vector2Int coordinate)
        {
            GridCellView view = cells[coordinate.y, coordinate.x];
            if (view == null)
            {
                return;
            }

            if (model.IsOccupied(coordinate))
            {
                view.SetFilled(model.GetColor(coordinate.y, coordinate.x));
            }
            else
            {
                view.SetEmpty();
            }
        }

        /// <summary>Reads the colour of every cell about to be wiped, in the order of the clear.</summary>
        private void CaptureClearedColors(LineClearResult lines)
        {
            clearedColors.Clear();
            IReadOnlyList<Vector2Int> cleared = lines.Cells;

            for (int i = 0; i < cleared.Count; i++)
            {
                Vector2Int cell = cleared[i];
                clearedColors.Add(model.GetColor(cell.y, cell.x));
            }
        }

        /// <summary>Fades the completed cells out and throws sparks of their own colour.</summary>
        private void PlayClearFeedback(LineClearResult lines)
        {
            IReadOnlyList<Vector2Int> cleared = lines.Cells;

            for (int i = 0; i < cleared.Count; i++)
            {
                Vector2Int cell = cleared[i];
                Color color = i < clearedColors.Count ? clearedColors[i] : GameTheme.ShapePrimary;

                GridCellView view = cells[cell.y, cell.x];
                if (view == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    view.PlayClear(color);
                    sparks?.Emit(AnchoredPosition(cell), color);
                }
                else
                {
                    view.SetEmpty();
                }
            }
        }

        /// <summary>Centre of a cell in the board's own anchored space, measured from the top left.</summary>
        private Vector2 AnchoredPosition(Vector2Int coordinate)
        {
            return new Vector2(
                coordinate.x * Pitch + cellSize * 0.5f,
                -(coordinate.y * Pitch + cellSize * 0.5f));
        }

        private void CancelCellAnimations()
        {
            if (cells == null)
            {
                return;
            }

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    cells[row, col]?.CancelAnimations();
                }
            }
        }

        /// <summary>
        /// Picks up the spark layer baked into the scene or creates it, always above the
        /// highlight so nothing draws over the effect.
        /// </summary>
        private void BindOrBuildSparks()
        {
            if (sparks == null)
            {
                sparks = boardRoot.GetComponentInChildren<SparkBurst>(true);
            }

            if (sparks == null)
            {
                sparks = SparkBurst.Create(boardRoot);
            }

            sparks.transform.SetAsLastSibling();
        }
    }
}
