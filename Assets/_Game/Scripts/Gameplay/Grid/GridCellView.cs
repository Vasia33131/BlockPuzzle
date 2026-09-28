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
        [SerializeField] private Image pattern;

        private Image crystalIcon;
        private Image markFrame;
        private bool isFilled;
        private bool isMarked;

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
            view.EnsurePattern();

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
            EnsurePattern();
        }

        public void SetCoordinate(Vector2Int value) => coordinate = value;

        public void SetEmpty()
        {
            CancelAnimations();
            isFilled = false;
            ApplyVisual(false, Color.clear);
        }

        /// <summary>Paints the empty-cell layer from the active theme without touching a filled block.</summary>
        public void RefreshEmptyColor()
        {
            if (background != null)
            {
                background.color = GameTheme.EmptyCell;
            }
        }

        /// <summary>
        /// Fills the cell. A level block may carry a crystal icon or the frame of a marked
        /// goal cell on top; both go away with the block.
        /// </summary>
        public void SetFilled(Color color, bool crystal = false, bool marked = false)
        {
            CancelAnimations();
            isFilled = true;
            ApplyVisual(true, color, crystal, marked);
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
            EnsurePattern();

            // A collected crystal flies to the goal counter as its own object.
            ApplyExtras(false, false);

            fill.enabled = true;
            fill.color = Color.white;
            fill.rectTransform.localScale = Vector3.one;

            if (pattern != null)
            {
                pattern.enabled = pattern.sprite != null;
                GameTween.Tint(
                    pattern,
                    GameTheme.WithAlpha(ThemePattern.BlockOverlayTint, 0f),
                    ClearDuration,
                    TweenEase.InQuad);
            }

            GameTween.Tint(fill, GameTheme.WithAlpha(color, 0f), ClearDuration, TweenEase.InQuad, onComplete: SetEmpty);
            GameTween.Scale(fill.rectTransform, Vector3.one * 0.35f, ClearDuration, TweenEase.InQuad);
        }

        /// <summary>Stops any animation in flight so the cell can be reused immediately.</summary>
        public void CancelAnimations()
        {
            if (fill != null)
            {
                GameTween.Kill(fill);
                GameTween.Kill(fill.rectTransform);
            }

            if (pattern != null)
            {
                GameTween.Kill(pattern);
            }
        }

        private void ApplyVisual(bool showFill, Color color, bool crystal = false, bool marked = false)
        {
            if (background != null)
            {
                background.color = GameTheme.EmptyCell;
            }

            ApplyExtras(showFill && crystal, showFill && marked);

            if (fill == null)
            {
                return;
            }

            fill.enabled = showFill;
            fill.color = color;
            fill.rectTransform.localScale = Vector3.one;
            transform.localScale = Vector3.one;
            EnsurePattern();
            ThemePattern.ApplyBlockOverlay(pattern, showFill);
        }

        /// <summary>
        /// Shows or hides the crystal icon and the goal frame. The two objects are built the first
        /// time a level needs them, so the endless mode never pays for them.
        /// </summary>
        private void ApplyExtras(bool showCrystal, bool showMarked)
        {
            if (showCrystal)
            {
                if (crystalIcon == null)
                {
                    crystalIcon = CreateExtra("Crystal", LevelArt.CrystalSprite, Color.white, 0.2f);
                }

                crystalIcon.gameObject.SetActive(true);
                crystalIcon.transform.SetAsLastSibling();
            }
            else if (crystalIcon != null)
            {
                crystalIcon.gameObject.SetActive(false);
            }

            if (showMarked)
            {
                if (markFrame == null)
                {
                    markFrame = CreateExtra("Mark", LevelArt.FrameSprite, LevelArt.MarkColor, 0f);
                    markFrame.type = Image.Type.Sliced;
                }

                markFrame.gameObject.SetActive(true);
            }
            else if (markFrame != null)
            {
                markFrame.gameObject.SetActive(false);
            }

            // Only a marked cell needs the per-frame pulse.
            isMarked = showMarked;
            enabled = isMarked;
        }

        private Image CreateExtra(string objectName, Sprite sprite, Color color, float inset)
        {
            Image image = UIFactory.CreateImage(objectName, transform, color, rounded: false);
            image.sprite = sprite;
            image.preserveAspect = inset > 0f;
            image.raycastTarget = false;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(inset, inset);
            rect.anchorMax = new Vector2(1f - inset, 1f - inset);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        /// <summary>Breathing of the goal frame; the component is enabled only while the cell is marked.</summary>
        private void Update()
        {
            if (!isMarked || markFrame == null)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * 4f) * 0.5f + 0.5f;
            Color color = LevelArt.MarkColor;
            color.a = Mathf.Lerp(0.55f, 1f, wave);
            markFrame.color = color;
        }

        private void EnsurePattern()
        {
            if (fill == null)
            {
                Transform fillTransform = transform.Find("Fill");
                if (fillTransform != null)
                {
                    fill = fillTransform.GetComponent<Image>();
                }
            }

            if (fill == null)
            {
                return;
            }

            ApplyRoundedSprite(fill);
            ThemePattern.EnsureRoundedMask(fill.gameObject);
            pattern = ThemePattern.EnsureChild(fill.transform, ThemePattern.BlockChildName, 0);
        }

        private static void ApplyRoundedSprite(Image image)
        {
            if (image == null || UIFactory.RoundedSprite == null)
            {
                return;
            }

            image.sprite = UIFactory.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }
    }
}
