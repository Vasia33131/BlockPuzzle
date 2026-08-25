using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.Grid
{
    /// <summary>
    /// Overlay drawn on top of the board that marks the cells a dragged figure would land on:
    /// a translucent tint plus a denser contour, green when the drop is allowed and red when it
    /// is not. Deliberately separate from <see cref="GridCellView"/> so the transient hint and
    /// the committed board state never fight over the same visuals. Keeps a pool of markers so
    /// dragging allocates nothing.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HighlightGrid : MonoBehaviour
    {
        [SerializeField] private float cellSize = GameTheme.CellSize;
        [SerializeField] private float pitch = GameTheme.CellSize + GameTheme.CellSpacing;

        private readonly List<Marker> markers = new List<Marker>();
        private RectTransform rect;

        /// <summary>One pooled cell marker: translucent tint with a contour frame on top.</summary>
        private sealed class Marker
        {
            public RectTransform Rect;
            public Image Tint;
            public Image Contour;
        }

        /// <summary>Creates the overlay as the topmost child of the board so it covers the cells.</summary>
        public static HighlightGrid Create(RectTransform boardRoot, float cellSize, float pitch)
        {
            RectTransform root = UIFactory.CreateRect("HighlightGrid", boardRoot);
            UIFactory.Stretch(root);

            var highlight = root.gameObject.AddComponent<HighlightGrid>();
            highlight.Configure(cellSize, pitch);
            return highlight;
        }

        /// <summary>Keeps the overlay in step with the board metrics it draws over.</summary>
        public void Configure(float cellSize, float pitch)
        {
            this.cellSize = cellSize;
            this.pitch = pitch;
            Hide();
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        /// <summary>
        /// Marks <paramref name="coordinates"/> in the colour that tells the player whether
        /// releasing the figure right now would place it.
        /// </summary>
        public void Show(IReadOnlyList<Vector2Int> coordinates, bool valid)
        {
            if (coordinates == null || coordinates.Count == 0)
            {
                Hide();
                return;
            }

            Color tint = valid ? GameTheme.HighlightValid : GameTheme.HighlightInvalid;
            Color contour = GameTheme.WithAlpha(tint, GameTheme.HighlightOutlineAlpha);

            for (int i = 0; i < coordinates.Count; i++)
            {
                Marker marker = GetMarker(i);
                marker.Rect.sizeDelta = new Vector2(cellSize, cellSize);
                marker.Rect.anchoredPosition = AnchoredPosition(coordinates[i]);
                marker.Tint.color = tint;

                if (marker.Contour != null)
                {
                    marker.Contour.color = contour;
                }

                marker.Rect.gameObject.SetActive(true);
            }

            for (int i = coordinates.Count; i < markers.Count; i++)
            {
                markers[i].Rect.gameObject.SetActive(false);
            }
        }

        public void Hide()
        {
            for (int i = 0; i < markers.Count; i++)
            {
                markers[i].Rect.gameObject.SetActive(false);
            }
        }

        private Vector2 AnchoredPosition(Vector2Int coordinate)
        {
            return new Vector2(
                coordinate.x * pitch + cellSize * 0.5f,
                -(coordinate.y * pitch + cellSize * 0.5f));
        }

        private Marker GetMarker(int index)
        {
            while (markers.Count <= index)
            {
                markers.Add(CreateMarker(markers.Count));
            }

            return markers[index];
        }

        private Marker CreateMarker(int index)
        {
            if (rect == null)
            {
                rect = (RectTransform)transform;
            }

            Image tint = UIFactory.CreateImage($"Highlight_{index}", rect, Color.clear);
            tint.raycastTarget = false;

            RectTransform markerRect = tint.rectTransform;
            markerRect.anchorMin = new Vector2(0f, 1f);
            markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(cellSize, cellSize);

            var marker = new Marker { Rect = markerRect, Tint = tint };

            // The contour is the nine-slice frame of the rounded sprite with its centre skipped,
            // so it outlines the cell without darkening the tint underneath it.
            if (UIFactory.RoundedSprite != null)
            {
                Image contour = UIFactory.CreateImage("Contour", markerRect, Color.clear);
                contour.raycastTarget = false;
                contour.fillCenter = false;
                UIFactory.Stretch(contour.rectTransform);
                marker.Contour = contour;
            }

            tint.gameObject.SetActive(false);
            return marker;
        }
    }
}
