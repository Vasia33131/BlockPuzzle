using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.Grid
{
    /// <summary>
    /// Visual representation of one cell of the board. Knows nothing about the rules;
    /// it only reflects the state <see cref="GridManager"/> pushes into it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GridCellView : MonoBehaviour
    {
        /// <summary>Length of the flash-and-shrink a cell plays while its line is cleared.</summary>
        public const float ClearDuration = 0.26f;

        private const float PlaceDuration = 0.16f;

        [SerializeField] private Vector2Int coordinate;
        [SerializeField] private Image border;
        [SerializeField] private Image background;
        [SerializeField] private Image fill;

        private bool isFilled;

        public Vector2Int Coordinate => coordinate;
        public bool IsFilled => isFilled;
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>Builds the cell hierarchy: border frame, empty background and a fill layer.</summary>
        public static GridCellView Create(
            Transform parent,
            Vector2Int coordinate,
            float size,
            float pitch,
            GridCellView prefab = null)
        {
            if (prefab != null)
            {
                GridCellView instance = Object.Instantiate(prefab, parent);
                instance.gameObject.name = $"Cell_{coordinate.y}_{coordinate.x}";
                instance.Configure(coordinate, size, pitch);
                instance.SetEmpty();
                return instance;
            }

            RectTransform rect = UIFactory.CreateRect($"Cell_{coordinate.y}_{coordinate.x}", parent);
            var view = rect.gameObject.AddComponent<GridCellView>();

            view.border = rect.gameObject.AddComponent<Image>();
            view.border.color = GameTheme.CellBorder;
            view.border.raycastTarget = false;
            ApplyRoundedSprite(view.border);

            view.background = UIFactory.CreateImage("Background", rect, GameTheme.EmptyCell);
            view.background.raycastTarget = false;
            UIFactory.Stretch(view.background.rectTransform, 2f);

            view.fill = UIFactory.CreateImage("Fill", rect, Color.clear);
            view.fill.raycastTarget = false;
            UIFactory.Stretch(view.fill.rectTransform, 3f);
            view.fill.enabled = false;

            view.Configure(coordinate, size, pitch);
            view.SetEmpty();
            return view;
        }

        /// <summary>Places the cell on the board grid and stores its coordinate.</summary>
        public void Configure(Vector2Int value, float size, float pitch)
        {
            coordinate = value;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(
                value.x * pitch + size * 0.5f,
                -(value.y * pitch + size * 0.5f));
            rect.localScale = Vector3.one;
        }

        public void SetCoordinate(Vector2Int value) => coordinate = value;

        public void SetEmpty()
        {
            CancelAnimations();
            isFilled = false;
            ApplyVisual(false, Color.clear);
        }

        public void SetFilled(Color color)
        {
            CancelAnimations();
            isFilled = true;
            ApplyVisual(true, color);
        }

        /// <summary>Fills the cell and gives it a short pop, used when the player drops a figure.</summary>
        public void SetFilledAnimated(Color color)
        {
            SetFilled(color);

            if (fill == null)
            {
                return;
            }

            fill.rectTransform.localScale = Vector3.one * 0.72f;
            GameTween.Scale(fill.rectTransform, Vector3.one, PlaceDuration, TweenEase.OutBack);
        }

        /// <summary>
        /// Plays the disappearance of a cell whose line was completed: a white flash that fades
        /// out through <paramref name="color"/> while the block shrinks away. The model has
        /// already been cleared by the time this runs, so it is pure decoration.
        /// </summary>
        public void PlayClear(Color color)
        {
            isFilled = false;

            if (fill == null)
            {
                SetEmpty();
                return;
            }

            CancelAnimations();

            fill.enabled = true;
            fill.color = Color.white;
            fill.rectTransform.localScale = Vector3.one;

            GameTween.Tint(fill, GameTheme.WithAlpha(color, 0f), ClearDuration, TweenEase.InQuad, onComplete: SetEmpty);
            GameTween.Scale(fill.rectTransform, Vector3.one * 0.35f, ClearDuration, TweenEase.InQuad);
        }

        /// <summary>Stops any animation in flight so the cell can be reused immediately.</summary>
        public void CancelAnimations()
        {
            if (fill == null)
            {
                return;
            }

            GameTween.Kill(fill);
            GameTween.Kill(fill.rectTransform);
        }

        private void ApplyVisual(bool showFill, Color color)
        {
            if (fill == null)
            {
                return;
            }

            fill.enabled = showFill;
            fill.color = color;
            fill.rectTransform.localScale = Vector3.one;
            transform.localScale = Vector3.one;
        }

        private static void ApplyRoundedSprite(Image image)
        {
            if (UIFactory.RoundedSprite == null)
            {
                return;
            }

            image.sprite = UIFactory.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }
    }
}
