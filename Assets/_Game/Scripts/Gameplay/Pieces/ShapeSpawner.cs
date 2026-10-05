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

        /// <summary>Set once the very first run of this install has started; that run gets easy onboarding batches.</summary>
        private const string OnboardingDoneKey = "BlockPuzzle.OnboardingDone";

        [Header("Content")]
        [SerializeField] private ShapeLibrary library;
        [SerializeField, Range(0.2f, 1f)] private float slotScale = 0.55f;

        [Header("Generation")]
        [SerializeField] private SmartShapeSettings generation = new SmartShapeSettings();

        [Header("Layout")]
        [Tooltip("Horizontal gap kept between two neighbouring slots.")]
        [SerializeField, Min(0f)] private float slotSpacing = 24f;

        [Tooltip("Empty border kept inside a slot so a figure never touches its edges.")]
        [SerializeField, Min(0f)] private float slotPadding = 10f;

        [Tooltip("Lower bound of the shrink applied to a figure that does not fit its slot.")]
        [SerializeField, Range(0.5f, 1f)] private float minFitScale = 0.7f;

        private readonly DraggableShape[] active = new DraggableShape[SlotCount];
        private readonly BlockShape[] batchBuffer = new BlockShape[SlotCount];
        private SmartShapeProvider provider;
        private IShapeProvider scriptedProvider;
        private Func<int> scoreSource;
        private Dictionary<string, BlockShape> shapesByName;
        private bool interactable = true;

        /// <summary>Raised whenever the set of currently offered figures changes.</summary>
        public event Action ShapesChanged;

        /// <summary>Raised right after a brand new batch of three figures appeared.</summary>
        public event Action BatchSpawned;

        public IReadOnlyList<RectTransform> Slots => slots;

        /// <summary>Layer a figure is moved to while it is being dragged.</summary>
        public RectTransform DragLayer => dragLayer;

        /// <summary>Figure currently waiting in <paramref name="slot"/>, or null.</summary>
        public DraggableShape GetShape(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return null;
            }

            DraggableShape draggable = active[slot];
            return draggable != null && !draggable.IsConsumed ? draggable : null;
        }

        /// <summary>
        /// Scripted dealer for the first batch of the next <see cref="Restart"/>: one
        /// <see cref="IShapeProvider.Next"/> per slot, a null leaves the slot empty. Later
        /// batches come from the regular dealer again.
        /// </summary>
        public void SetNextBatch(IShapeProvider scripted) => scriptedProvider = scripted;

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
        /// Continues a saved run: a fresh dealer (without the onboarding batches) and the
        /// saved tray. Like <see cref="Restart"/>, the figures drop in on the next frame,
        /// once the canvas layout knows how wide a slot is. An empty tray is dealt anew.
        /// </summary>
        public void ResumeRun(IReadOnlyList<BlockShape> shapes)
        {
            provider = CreateProvider();
            provider.BeginRun(false);
            scriptedProvider = null;
            ClearAll();
            LayoutSlots();

            if (isActiveAndEnabled)
            {
                StartCoroutine(ResumeTrayNextFrame(shapes));
            }
            else
            {
                ResumeTray(shapes);
            }
        }

        /// <summary>
        /// Figure of the catalog (free set or a pack) with this <see cref="BlockShape.DisplayName"/>,
        /// or null. Used to bring a saved tray back.
        /// </summary>
        public BlockShape FindShape(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return null;
            }

            if (shapesByName == null)
            {
                shapesByName = new Dictionary<string, BlockShape>(StringComparer.Ordinal);
                IReadOnlyList<BlockShape> source = library != null ? library.Shapes : ShapeCatalog.CreateDefaultShapes();
                IReadOnlyList<BlockShape> extra = library != null ? library.Pack1 : ShapeCatalog.CreatePack1Shapes();
                IndexShapes(source);
                IndexShapes(extra);
            }

            return shapesByName.TryGetValue(displayName, out BlockShape shape) ? shape : null;
        }

        private void IndexShapes(IReadOnlyList<BlockShape> shapes)
        {
            if (shapes == null)
            {
                return;
            }

            foreach (BlockShape shape in shapes)
            {
                if (shape != null && !string.IsNullOrEmpty(shape.DisplayName) && !shapesByName.ContainsKey(shape.DisplayName))
                {
                    shapesByName.Add(shape.DisplayName, shape);
                }
            }
        }

        private IEnumerator ResumeTrayNextFrame(IReadOnlyList<BlockShape> shapes)
        {
            yield return null;
            ResumeTray(shapes);
        }

        private void ResumeTray(IReadOnlyList<BlockShape> shapes)
        {
            RestoreShapes(shapes);
            if (RemainingCount == 0)
            {
                SpawnBatch();
            }
        }

        /// <summary>
        /// Fills one empty slot with a figure that fits the board right now. False when the tray
        /// is full or no figure of the library fits anywhere.
        /// </summary>
        public bool TryGrantExtraShape()
        {
            int slot = FindEmptySlot();
            if (slot < 0)
            {
                return false;
            }

            BlockShape shape = DrawFittingShape();
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
            shapesByName = null;
            EnsureBottomDocked();
            LayoutSlots();
        }

        /// <summary>Current run score, read by the generator to ramp up large figures.</summary>
        public void SetScoreSource(Func<int> source) => scoreSource = source;

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
            provider = CreateProvider();
            provider.BeginRun(ConsumeFirstRun());
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

        /// <summary>
        /// Empties the spawn area for a level. The dealer is seeded with the level's seed, so a level
        /// always starts with the same figures; it keeps the line-finishing assist but has no
        /// onboarding batches and never draws from the paid figure pack.
        /// </summary>
        public void RestartLevel(int shapeSeed)
        {
            provider = CreateProvider(shapeSeed);
            provider.IncludePaidPack = false;
            provider.BeginRun(false);
            scriptedProvider = null;
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
            provider ??= CreateProvider();
            LayoutSlots();

            if (scriptedProvider != null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    batchBuffer[i] = scriptedProvider.Next();
                }

                scriptedProvider = null;
            }
            else
            {
                provider.FillBatch(batchBuffer);
            }

            for (int i = 0; i < SlotCount; i++)
            {
                SpawnAt(i, batchBuffer[i]);
                batchBuffer[i] = null;
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

        private BlockShape DrawFittingShape()
        {
            provider ??= CreateProvider();
            return provider.NextFitting();
        }

        /// <summary>A dealer with a random seed, or with <paramref name="seed"/> when a level asks for a fixed one.</summary>
        private SmartShapeProvider CreateProvider(int? seed = null)
        {
            IReadOnlyList<BlockShape> source = library != null ? library.Shapes : ShapeCatalog.CreateDefaultShapes();
            IReadOnlyList<BlockShape> extra = library != null ? library.Pack1 : ShapeCatalog.CreatePack1Shapes();
            return new SmartShapeProvider(
                source,
                extra,
                () => grid != null ? grid.Model : null,
                () => scoreSource != null ? scoreSource() : 0,
                generation,
                seed ?? (Environment.TickCount ^ UnityEngine.Random.Range(0, int.MaxValue)));
        }

        /// <summary>True exactly once per install: for the run that starts first.</summary>
        private static bool ConsumeFirstRun()
        {
            if (PlayerPrefs.GetInt(OnboardingDoneKey, 0) != 0)
            {
                return false;
            }

            PlayerPrefs.SetInt(OnboardingDoneKey, 1);
            PlayerPrefs.Save();
            return true;
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
