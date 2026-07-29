using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.Pieces
{
    /// <summary>
    /// One square of a figure. Stores its offset inside the figure and owns the two
    /// images (body plus highlight) that give the block a bit of depth.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BlockPiece : MonoBehaviour
    {
        [SerializeField] private Vector2Int offset;
        [SerializeField] private Image body;
        [SerializeField] private Image highlight;

        public Vector2Int Offset => offset;
        public RectTransform Rect => (RectTransform)transform;
        public Color Color => body != null ? body.color : Color.white;

        /// <summary>
        /// Creates a block positioned relative to the centre of its parent figure.
        /// </summary>
        public static BlockPiece Create(
            Transform parent,
            Vector2Int offset,
            Vector2Int shapeBounds,
            float cellSize,
            float pitch,
            Color color,
            BlockPiece prefab = null)
        {
            if (prefab != null)
            {
                BlockPiece instance = Object.Instantiate(prefab, parent);
                instance.gameObject.name = $"Block_{offset.y}_{offset.x}";
                instance.Configure(offset, shapeBounds, cellSize, pitch, color);
                return instance;
            }

            RectTransform rect = UIFactory.CreateRect($"Block_{offset.y}_{offset.x}", parent);
            var piece = rect.gameObject.AddComponent<BlockPiece>();

            piece.body = rect.gameObject.AddComponent<Image>();
            piece.body.raycastTarget = false;
            if (UIFactory.RoundedSprite != null)
            {
                piece.body.sprite = UIFactory.RoundedSprite;
                piece.body.type = Image.Type.Sliced;
                piece.body.pixelsPerUnitMultiplier = 1f;
            }

            piece.highlight = UIFactory.CreateImage("Highlight", rect, Color.white);
            piece.highlight.raycastTarget = false;

            piece.Configure(offset, shapeBounds, cellSize, pitch, color);
            return piece;
        }

        /// <summary>Applies the cell offset inside its figure and paints the block.</summary>
        public void Configure(Vector2Int cellOffset, Vector2Int shapeBounds, float cellSize, float pitch, Color color)
        {
            offset = cellOffset;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cellSize, cellSize);
            rect.anchoredPosition = new Vector2(
                (cellOffset.x - (shapeBounds.x - 1) * 0.5f) * pitch,
                -(cellOffset.y - (shapeBounds.y - 1) * 0.5f) * pitch);
            rect.localScale = Vector3.one;

            if (highlight != null)
            {
                RectTransform highlightRect = highlight.rectTransform;
                highlightRect.anchorMin = new Vector2(0f, 1f);
                highlightRect.anchorMax = new Vector2(1f, 1f);
                highlightRect.pivot = new Vector2(0.5f, 1f);
                highlightRect.offsetMin = new Vector2(8f, 0f);
                highlightRect.offsetMax = new Vector2(-8f, -8f);
                highlightRect.sizeDelta = new Vector2(highlightRect.sizeDelta.x, cellSize * 0.22f);
            }

            SetColor(color);
        }

        public void SetColor(Color color)
        {
            if (body != null)
            {
                body.color = color;
            }

            if (highlight != null)
            {
                Color light = GameTheme.Lighten(color, 0.45f);
                light.a = 0.55f;
                highlight.color = light;
            }
        }

        public void SetAlpha(float alpha)
        {
            if (body != null)
            {
                Color color = body.color;
                color.a = alpha;
                body.color = color;
            }

            if (highlight != null)
            {
                Color color = highlight.color;
                color.a = 0.55f * alpha;
                highlight.color = color;
            }
        }
    }
}
