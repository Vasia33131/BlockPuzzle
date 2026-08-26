using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;

namespace BlockPuzzle.Pieces
{
    /// <summary>
    /// Owns the spawn area: it lays the three slots out in a row, offers a batch of
    /// figures and refills the area the moment the last one has been placed.
    /// </summary>
    public class ShapeSpawner : MonoBehaviour
    {
        public const int SlotCount = 3;

        /// <summary>Slot size assumed while the canvas layout has not been resolved yet.</summary>
        private const float FallbackSlotSize = 300f;

        /// <summary>Landscape / desktop tray: keep slots roughly this wide so they sit as a group.</summary>
        private const float CompactSlotMinWidth = 180f;

        /// <summary>Upper bound so a tall landscape tray does not stretch the group back out.</summary>
        private const float CompactSlotMaxWidth = 230f;

        /// <summary>Slot width as a multiple of tray height on desktop (slightly wider than tall).</summary>
        private const float CompactSlotAspect = 1.5f;

        /// <summary>Delay between the pop-in of two neighbouring figures of a batch.</summary>
        private const float SpawnStagger = 0.06f;

        [Header("References")]
        [SerializeField] private GridManager grid;
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private RectTransform[] slots = new RectTransform[SlotCount];
        [SerializeField] private BlockPiece piecePrefab;

        [Header("Content")]
        [SerializeField] private ShapeLibrary library;
        [SerializeField, Range(0.2f, 1f)] private float slotScale = 0.55f;

        [Header("Layout")]
        [Tooltip("Horizontal gap kept between two neighbouring slots.")]
        [SerializeField, Min(0f)] private float slotSpacing = 24f;

        [Tooltip("Empty border kept inside a slot so a figure never touches its edges.")]
        [SerializeField, Min(0f)] private float slotPadding = 10f;

        [Tooltip("Lower bound of the shrink applied to a figure that does not fit its slot.")]
        [SerializeField, Range(0.5f, 1f)] private float minFitScale = 0.7f;

        private readonly DraggableShape[] active = new DraggableShape[SlotCount];
        private IShapeProvider provider;
        private bool interactable = true;

        /// <summary>Raised whenever the set of currently offered figures changes.</summary>
        public event Action ShapesChanged;

        /// <summary>Raised right after a brand new batch of three figures appeared.</summary>
        public event Action BatchSpawned;

        public IReadOnlyList<RectTransform> Slots => slots;

        public int RemainingCount
        {
            get
            {
                int count = 0;
                foreach (DraggableShape shape in active)
                {
                    if (shape != null && !shape.IsConsumed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public IEnumerable<BlockShape> AvailableShapes
        {
            get
            {
                foreach (DraggableShape draggable in active)
                {
                    if (draggable != null && !draggable.IsConsumed)
                    {
                        yield return draggable.Shape;
                    }
                }
            }
        }

        /// <summary>
        /// Current tray, one entry per slot. Empty or already placed slots are <c>null</c>.
        /// </summary>
        public IReadOnlyList<BlockShape> PeekShapes()
        {
            var shapes = new BlockShape[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                DraggableShape draggable = active[i];
                shapes[i] = draggable != null && !draggable.IsConsumed ? draggable.Shape : null;
            }

            return shapes;
        }

        /// <summary>Replaces the tray with <paramref name="shapes"/>, mapped onto slots by index.</summary>
        public void RestoreShapes(IReadOnlyList<BlockShape> shapes)
        {
            ClearAll();
            LayoutSlots();

            if (shapes != null)
            {
                int count = Mathf.Min(SlotCount, shapes.Count);
                for (int i = 0; i < count; i++)
                {
                    if (shapes[i] != null)
                    {
                        SpawnAt(i, shapes[i]);
                    }
                }
            }

            ShapesChanged?.Invoke();
        }

        /// <summary>
        /// Fills one empty slot with a random figure from the library. False when the tray is full.
        /// </summary>
        public bool TryGrantExtraShape()
        {
            int slot = FindEmptySlot();
            if (slot < 0)
            {
                return false;
            }

            BlockShape shape = DrawLibraryShape();
            if (shape == null)
            {
                return false;
            }

            SpawnAt(slot, shape);
            ShapesChanged?.Invoke();
            return true;
        }

        public void Configure(GridManager gridManager, RectTransform layer, RectTransform[] slotRects, ShapeLibrary shapeLibrary)
        {
            grid = gridManager;
            dragLayer = layer;
            slots = slotRects;
            library = shapeLibrary;
            EnsureBottomDocked();
            LayoutSlots();
        }

        private void Awake()
        {
            EnsureBottomDocked();
            LayoutSlots();
        }

        private void OnEnable()
        {
            GameTheme.Changed += ApplyThemeColors;
            ApplyThemeColors();
        }

        private void OnDisable()
        {
            GameTheme.Changed -= ApplyThemeColors;
        }

        /// <summary>Repaints tray cubes from the active theme without dealing a new batch.</summary>
        public void ApplyThemeColors()
        {
            for (int i = 0; i < active.Length; i++)
            {
                DraggableShape draggable = active[i];
                if (draggable != null && !draggable.IsConsumed)
                {
                    draggable.ApplyTheme();
                }
            }
        }

        /// <summary>
        /// Keeps the tray pinned to the bottom edge of its parent. UIManager may refine
        /// height and margins for the current aspect ratio afterwards.
        /// </summary>
        public void EnsureBottomDocked()
        {
            var spawnRect = (RectTransform)transform;
            spawnRect.anchorMin = new Vector2(0f, 0f);
            spawnRect.anchorMax = new Vector2(1f, 0f);
            spawnRect.pivot = new Vector2(0.5f, 0f);
        }

        /// <summary>Optional authored block square. When null, pieces are built in code.</summary>
        public void SetPiecePrefab(BlockPiece prefab) => piecePrefab = prefab;

        /// <summary>
        /// Empties the spawn area for a new run. The area stays visibly empty until the
        /// run is actually under way: the first batch drops in on the next frame, once the
        /// canvas layout knows how wide a slot is.
        /// </summary>
        public void Restart()
        {
            provider = WeightedShapeProvider.FromLibrary(library);
            ClearAll();
            LayoutSlots();

            if (isActiveAndEnabled)
            {
                StartCoroutine(SpawnFirstBatch());
            }
            else
            {
                SpawnBatch();
            }
        }

        public void SetInteractable(bool value)
        {
            interactable = value;

            foreach (DraggableShape draggable in active)
            {
                if (draggable == null)
                {
                    continue;
                }

                draggable.Interactable = value;

                if (!value)
                {
                    draggable.CancelDrag();
                }
            }
        }

        /// <summary>Fades out the figures that no longer fit anywhere on the board.</summary>
        public void RefreshPlayability()
        {
            if (grid == null)
            {
                return;
            }

            foreach (DraggableShape draggable in active)
            {
                if (draggable != null && !draggable.IsConsumed)
                {
                    draggable.SetDimmed(!grid.HasPlacementFor(draggable.Shape));
                }
            }
        }

        /// <summary>
        /// Lays the slots out in a single row. Portrait / mobile keeps an even spread across
        /// the tray; desktop landscape clusters them in the centre so they are not scattered
        /// across a wide screen.
        /// </summary>
        public void LayoutSlots()
        {
            if (slots == null)
            {
                return;
            }

            if (UseCompactSlotRow())
            {
                LayoutSlotsCompact();
                return;
            }

            LayoutSlotsSpread();
        }

        /// <summary>True on landscape (desktop / wide) screens where a full-width tray looks sparse.</summary>
        private static bool UseCompactSlotRow()
        {
            return Screen.width > Screen.height;
        }

        private void LayoutSlotsSpread()
        {
            float half = slotSpacing * 0.5f;
            int count = slots.Length;

            for (int i = 0; i < count; i++)
            {
                RectTransform slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.anchorMin = new Vector2(i / (float)count, 0f);
                slot.anchorMax = new Vector2((i + 1) / (float)count, 1f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.anchoredPosition = Vector2.zero;
                slot.sizeDelta = Vector2.zero;
                slot.offsetMin = new Vector2(half, 0f);
                slot.offsetMax = new Vector2(-half, 0f);
            }
        }

        private void LayoutSlotsCompact()
        {
            var area = (RectTransform)transform;
            float height = area.rect.height > 1f ? area.rect.height : CompactSlotMinWidth;
            float areaWidth = area.rect.width;
            float slotWidth = ResolveCompactSlotWidth(height);
            int count = slots.Length;
            float total = count * slotWidth + (count - 1) * slotSpacing;

            if (areaWidth > 1f && total >= areaWidth - 8f)
            {
                LayoutSlotsSpread();
                return;
            }

            float origin = -total * 0.5f + slotWidth * 0.5f;
            for (int i = 0; i < count; i++)
            {
                RectTransform slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.anchorMin = new Vector2(0.5f, 0f);
                slot.anchorMax = new Vector2(0.5f, 1f);
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(slotWidth, 0f);
                slot.anchoredPosition = new Vector2(origin + i * (slotWidth + slotSpacing), 0f);
            }
        }

        private static float ResolveCompactSlotWidth(float trayHeight)
        {
            return Mathf.Clamp(trayHeight * CompactSlotAspect, CompactSlotMinWidth, CompactSlotMaxWidth);
        }

        /// <summary>
        /// Rescales figures already sitting in the tray after the board cell size or spawn
        /// tray height changed (orientation flip). Does not respawn or clear the batch.
        /// </summary>
        public void UpdateShapeSizes(float scaleMultiplier = 1f)
        {
            LayoutSlots();
            Canvas.ForceUpdateCanvases();

            for (int i = 0; i < SlotCount; i++)
            {
                DraggableShape draggable = active[i];
                if (draggable == null || draggable.IsConsumed || slots == null || i >= slots.Length || slots[i] == null)
                {
                    continue;
                }

                float scale = ResolveScale(draggable.Shape, slots[i]) * Mathf.Max(0.1f, scaleMultiplier);
                draggable.RefreshGeometry(scale);
            }
        }

        private IEnumerator SpawnFirstBatch()
        {
            yield return null;
            SpawnBatch();
        }

        private void SpawnBatch()
        {
            provider ??= WeightedShapeProvider.FromLibrary(library);
            LayoutSlots();

            for (int i = 0; i < SlotCount; i++)
            {
                SpawnAt(i, provider.Next());
            }

            BatchSpawned?.Invoke();
            ShapesChanged?.Invoke();
        }

        private void SpawnAt(int index, BlockShape shape)
        {
            if (shape == null || slots == null || index >= slots.Length || slots[index] == null)
            {
                return;
            }

            RectTransform slot = slots[index];
            DraggableShape draggable = DraggableShape.Create(
                shape, slot, dragLayer, grid, ResolveScale(shape, slot), piecePrefab);
            draggable.Interactable = interactable;
            draggable.Consumed += HandleShapeConsumed;
            draggable.PlaySpawnAnimation(index * SpawnStagger);
            active[index] = draggable;
        }

        /// <summary>
        /// Scale a figure is shown with in its slot. Figures too big for the slot shrink to
        /// fit, but never below <see cref="minFitScale"/> of the normal size so that they
        /// stay readable, and figures that already fit are never blown up.
        /// </summary>
        private float ResolveScale(BlockShape shape, RectTransform slot)
        {
            float cellSize = grid != null ? grid.CellSize : GameTheme.CellSize;
            float pitch = grid != null ? grid.Pitch : GameTheme.CellSize + GameTheme.CellSpacing;

            Vector2 figure = new Vector2(
                shape.Width * pitch - (pitch - cellSize),
                shape.Height * pitch - (pitch - cellSize)) * slotScale;

            Vector2 available = ResolveSlotSize(slot) - Vector2.one * (slotPadding * 2f);
            if (figure.x <= 0f || figure.y <= 0f || available.x <= 0f || available.y <= 0f)
            {
                return slotScale;
            }

            float fit = Mathf.Min(available.x / figure.x, available.y / figure.y);
            return slotScale * Mathf.Clamp(fit, minFitScale, 1f);
        }

        private Vector2 ResolveSlotSize(RectTransform slot)
        {
            Vector2 size = slot.rect.size;
            if (size.x > 1f && size.y > 1f)
            {
                return size;
            }

            Rect area = ((RectTransform)transform).rect;
            float height = area.height > 1f ? area.height : FallbackSlotSize;
            float width;
            if (UseCompactSlotRow())
            {
                width = ResolveCompactSlotWidth(height);
            }
            else
            {
                width = area.width > 1f
                    ? area.width / SlotCount - slotSpacing
                    : FallbackSlotSize;
            }

            return new Vector2(width, height);
        }

        private void HandleShapeConsumed(DraggableShape draggable)
        {
            draggable.Consumed -= HandleShapeConsumed;

            for (int i = 0; i < SlotCount; i++)
            {
                if (active[i] == draggable)
                {
                    active[i] = null;
                }
            }

            // The rule of the genre: a new batch appears only when the area is empty.
            if (RemainingCount == 0)
            {
                SpawnBatch();
            }
            else
            {
                ShapesChanged?.Invoke();
            }
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (active[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private BlockShape DrawLibraryShape()
        {
            provider ??= WeightedShapeProvider.FromLibrary(library);
            return provider.Next();
        }

        private void ClearAll()
        {
            StopAllCoroutines();

            for (int i = 0; i < SlotCount; i++)
            {
                if (active[i] != null)
                {
                    active[i].Consumed -= HandleShapeConsumed;
                    Destroy(active[i].gameObject);
                    active[i] = null;
                }
            }

            if (slots == null)
            {
                return;
            }

            foreach (RectTransform slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                for (int i = slot.childCount - 1; i >= 0; i--)
                {
                    Transform child = slot.GetChild(i);
                    if (child.GetComponent<DraggableShape>() != null)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }
    }
}
