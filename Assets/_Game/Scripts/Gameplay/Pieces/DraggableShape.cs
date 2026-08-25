using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;

namespace BlockPuzzle.Pieces
{
    /// <summary>
    /// A figure sitting in the spawn area that the player can drag onto the board.
    /// Builds itself from a <see cref="BlockShape"/> and reports back when it is consumed.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DraggableShape : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float PickUpDuration = 0.12f;
        private const float ReturnDuration = 0.16f;
        private const float SpawnDuration = 0.26f;

        [SerializeField] private BlockShape shape;
        [SerializeField] private float idleScale = 0.55f;

        [Tooltip("How far above the finger the figure is held so the hand does not cover it.")]
        [SerializeField] private float dragLiftPixels = 150f;

        private readonly List<BlockPiece> pieces = new List<BlockPiece>();
        private GridManager grid;
        private RectTransform dragLayer;
        private RectTransform slot;
        private RectTransform rect;
        private CanvasGroup canvasGroup;
        private Canvas canvas;
        private bool dragging;
        private bool consumed;

        /// <summary>Raised once the figure has been dropped on the board and used up.</summary>
        public event Action<DraggableShape> Consumed;

        public BlockShape Shape => shape;
        public bool IsConsumed => consumed;
        public bool Interactable { get; set; } = true;

        private BlockPiece piecePrefab;

        /// <summary>Creates the visual figure and hooks it to the board.</summary>
        public static DraggableShape Create(
            BlockShape shape,
            RectTransform slot,
            RectTransform dragLayer,
            GridManager grid,
            float idleScale,
            BlockPiece piecePrefab = null)
        {
            RectTransform rect = UIFactory.CreateRect($"Shape_{shape.DisplayName}", slot);
            var draggable = rect.gameObject.AddComponent<DraggableShape>();
            draggable.shape = shape;
            draggable.slot = slot;
            draggable.dragLayer = dragLayer;
            draggable.grid = grid;
            draggable.idleScale = idleScale;
            draggable.piecePrefab = piecePrefab;
            draggable.Build();
            return draggable;
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
        }

        private void Build()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();

            float cellSize = grid != null ? grid.CellSize : GameTheme.CellSize;
            float pitch = grid != null ? grid.Pitch : GameTheme.CellSize + GameTheme.CellSpacing;
            var bounds = new Vector2Int(shape.Width, shape.Height);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(
                bounds.x * pitch - (pitch - cellSize),
                bounds.y * pitch - (pitch - cellSize));
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one * idleScale;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Invisible pad that grows the touch area beyond the figure outline.
            Image hitArea = UIFactory.CreateImage("HitArea", rect, new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(hitArea.rectTransform, -40f);
            hitArea.raycastTarget = true;

            IReadOnlyList<Vector2Int> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                pieces.Add(BlockPiece.Create(rect, cells[i], bounds, cellSize, pitch, shape.Color, piecePrefab));
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Interactable || consumed || grid == null)
            {
                return;
            }

            dragging = true;
            GameTween.Kill(rect);

            if (dragLayer != null)
            {
                rect.SetParent(dragLayer, true);
                rect.SetAsLastSibling();
            }

            // Picking the figure up brings it to full size so it matches the board cells.
            GameTween.Scale(rect, Vector3.one, PickUpDuration, TweenEase.OutBack);
            canvasGroup.blocksRaycasts = false;
            MoveTo(eventData);
            RefreshHighlight();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            MoveTo(eventData);
            RefreshHighlight();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            canvasGroup.blocksRaycasts = true;
            grid.HideDropHighlight();

            // Only a drop the highlight showed in green commits; anything else, including a drop
            // away from the board, sends the figure back to its slot.
            if (TryGetDropOrigin(out Vector2Int origin) && grid.CanPlace(shape, origin))
            {
                PlacementResult result = grid.PlaceShape(shape, origin);
                if (result.Success)
                {
                    consumed = true;
                    Consumed?.Invoke(this);
                    Destroy(gameObject);
                    return;
                }
            }

            ReturnToSlot();
        }

        /// <summary>
        /// Drops a drag that is still in progress and sends the figure home. Pausing or losing
        /// mid-drag would otherwise leave the figure stuck to a finger that no longer controls it.
        /// </summary>
        public void CancelDrag()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
            }

            grid?.HideDropHighlight();
            ReturnToSlot();
        }

        /// <summary>Pops the figure into its slot when a fresh batch is dealt.</summary>
        public void PlaySpawnAnimation(float delay)
        {
            rect.localScale = Vector3.zero;
            GameTween.Scale(rect, Vector3.one * idleScale, SpawnDuration, TweenEase.OutBack, delay);
        }

        /// <summary>Repaints every cube from the live shape colour and the active theme pattern.</summary>
        public void ApplyTheme()
        {
            if (consumed || shape == null)
            {
                return;
            }

            Color color = shape.Color;
            for (int i = 0; i < pieces.Count; i++)
            {
                pieces[i]?.SetColor(color);
            }
        }

        /// <summary>Greys out the figure when it can no longer be placed anywhere.</summary>
        public void SetDimmed(bool dimmed)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup != null)
            {
                GameTween.Kill(canvasGroup);
                GameTween.Fade(canvasGroup, dimmed ? 0.35f : 1f, 0.18f);
            }
        }

        /// <summary>
        /// Rebuilds block sizes from the current <see cref="GridManager"/> cell metrics after
        /// an orientation / board resize. Keeps the same figure instance so gameplay continues.
        /// </summary>
        public void RefreshGeometry(float newIdleScale)
        {
            if (consumed || shape == null)
            {
                return;
            }

            if (dragging)
            {
                CancelDrag();
            }

            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            idleScale = newIdleScale;
            float cellSize = grid != null ? grid.CellSize : GameTheme.CellSize;
            float pitch = grid != null ? grid.Pitch : GameTheme.CellSize + GameTheme.CellSpacing;
            var bounds = new Vector2Int(shape.Width, shape.Height);

            GameTween.Kill(rect);
            rect.sizeDelta = new Vector2(
                bounds.x * pitch - (pitch - cellSize),
                bounds.y * pitch - (pitch - cellSize));

            for (int i = 0; i < pieces.Count; i++)
            {
                BlockPiece piece = pieces[i];
                if (piece != null)
                {
                    piece.Configure(piece.Offset, bounds, cellSize, pitch, shape.Color);
                }
            }

            if (slot != null && rect.parent == slot)
            {
                rect.anchoredPosition = Vector2.zero;
            }

            rect.localScale = Vector3.one * idleScale;
        }

        /// <summary>Follows the pointer, held a little above it so the finger never covers the figure.</summary>
        private void MoveTo(PointerEventData eventData)
        {
            Camera cam = EventCamera(eventData);
            RectTransform parent = (RectTransform)rect.parent;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, cam, out Vector2 local))
            {
                rect.localPosition = new Vector3(local.x, local.y + dragLiftPixels, 0f);
            }
        }

        /// <summary>Green or red marks under the figure, or nothing while it is away from the board.</summary>
        private void RefreshHighlight()
        {
            if (TryGetDropOrigin(out Vector2Int origin))
            {
                grid.ShowDropHighlight(shape, origin);
            }
            else
            {
                grid.HideDropHighlight();
            }
        }

        /// <summary>
        /// Grid position the figure would snap to, taken from where its top-left cell sits rather
        /// than from the finger, so the figure lands exactly where the player sees it.
        /// </summary>
        private bool TryGetDropOrigin(out Vector2Int origin)
        {
            origin = default;
            if (grid == null)
            {
                return false;
            }

            float pitch = grid.Pitch;
            var localAnchor = new Vector2(
                -(shape.Width - 1) * 0.5f * pitch,
                (shape.Height - 1) * 0.5f * pitch);

            Vector3 worldAnchor = rect.TransformPoint(localAnchor);
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldAnchor);
            return grid.TryGetDropOrigin(screenPoint, shape, out origin);
        }

        private Camera EventCamera(PointerEventData eventData)
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return eventData.pressEventCamera != null ? eventData.pressEventCamera : canvas?.worldCamera;
        }

        private void ReturnToSlot()
        {
            if (slot == null)
            {
                return;
            }

            rect.SetParent(slot, true);
            GameTween.Kill(rect);
            GameTween.MoveAnchored(rect, Vector2.zero, ReturnDuration, TweenEase.OutQuad);
            GameTween.Scale(rect, Vector3.one * idleScale, ReturnDuration, TweenEase.OutQuad);
        }

        private void OnDestroy() => GameTween.Kill(rect);
    }
}
