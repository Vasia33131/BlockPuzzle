using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Tetromino figures and small squares that drift slowly behind the menu. All of them are moved by
    /// one <see cref="Tick"/> call from the menu (sine offsets, no per-object Update, no allocations)
    /// and live on their own canvas, so their motion does not rebatch the menu's buttons.
    /// </summary>
    public sealed class MenuFloaters : MonoBehaviour
    {
        public const string ObjectName = "MenuFloaters";

        private const float BlockSize = 44f;
        private const float BlockGap = 5f;
        private const float FigureAlpha = 0.34f;
        private const float SquareAlpha = 0.24f;

        /// <summary>Cells of the seven tetrominoes (x to the right, y up).</summary>
        private static readonly Vector2Int[][] Figures =
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 0) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) },
            new[] { new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(2, 0) },
            new[] { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(0, 0) }
        };

        /// <summary>Anchor x, anchor y, figure index (-1 = single square), size or scale, colour index.</summary>
        private static readonly float[][] Layout =
        {
            new[] { 0.12f, 0.80f, 0f, 0.9f, 0f },
            new[] { 0.86f, 0.74f, 1f, 0.8f, 1f },
            new[] { 0.08f, 0.52f, 2f, 0.85f, 2f },
            new[] { 0.90f, 0.50f, 3f, 0.9f, 3f },
            new[] { 0.16f, 0.26f, 4f, 0.8f, 4f },
            new[] { 0.84f, 0.24f, 5f, 0.85f, 5f },
            new[] { 0.50f, 0.90f, 6f, 0.7f, 0f },
            new[] { 0.42f, 0.12f, 1f, 0.7f, 2f },
            new[] { 0.28f, 0.66f, -1f, 30f, 1f },
            new[] { 0.72f, 0.88f, -1f, 26f, 4f },
            new[] { 0.94f, 0.36f, -1f, 34f, 0f },
            new[] { 0.05f, 0.92f, -1f, 24f, 3f },
            new[] { 0.66f, 0.10f, -1f, 30f, 5f },
            new[] { 0.34f, 0.40f, -1f, 22f, 2f },
            new[] { 0.60f, 0.62f, -1f, 26f, 3f },
            new[] { 0.20f, 0.06f, -1f, 28f, 4f }
        };

        private RectTransform[] items;
        private float[] phase;
        private float[] speed;
        private float[] amplitude;
        private float[] spin;
        private float[] baseRotation;

        /// <summary>Builds the field filling <paramref name="parent"/>.</summary>
        public static MenuFloaters Create(RectTransform parent)
        {
            RectTransform root = UIFactory.CreateRect(ObjectName, parent);
            UIFactory.Stretch(root);
            root.gameObject.AddComponent<Canvas>();
            var floaters = root.gameObject.AddComponent<MenuFloaters>();
            floaters.Build(root);
            return floaters;
        }

        /// <summary>Moves every figure to its place at <paramref name="time"/>.</summary>
        public void Tick(float time)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Length; i++)
            {
                float t = time * speed[i] + phase[i];
                float dx = Mathf.Sin(t) * amplitude[i];
                float dy = Mathf.Cos(t * 0.83f) * amplitude[i] * 1.3f;
                items[i].anchoredPosition = new Vector2(dx, dy);
                items[i].localEulerAngles = new Vector3(0f, 0f, baseRotation[i] + Mathf.Sin(t * 0.6f) * spin[i]);
            }
        }

        private void Build(RectTransform root)
        {
            int count = Layout.Length;
            items = new RectTransform[count];
            phase = new float[count];
            speed = new float[count];
            amplitude = new float[count];
            spin = new float[count];
            baseRotation = new float[count];

            var random = new System.Random(8888);
            for (int i = 0; i < count; i++)
            {
                float[] entry = Layout[i];
                var anchor = new Vector2(entry[0], entry[1]);
                int figure = Mathf.RoundToInt(entry[2]);
                Color color = MenuArt.LogoColors[Mathf.RoundToInt(entry[4]) % MenuArt.LogoColors.Length];

                RectTransform holder = UIFactory.CreateRect(figure >= 0 ? "Figure" : "Square", root);
                UIFactory.Anchor(holder, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

                if (figure >= 0)
                {
                    BuildFigure(holder, Figures[figure % Figures.Length], entry[3], GameTheme.WithAlpha(color, FigureAlpha));
                }
                else
                {
                    Image square = UIFactory.CreateImage("Block", holder, GameTheme.WithAlpha(color, SquareAlpha));
                    square.raycastTarget = false;
                    UIFactory.Anchor(
                        square.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(entry[3], entry[3]));
                }

                items[i] = holder;
                phase[i] = (float)random.NextDouble() * Mathf.PI * 2f;
                speed[i] = 0.22f + (float)random.NextDouble() * 0.22f;
                amplitude[i] = 14f + (float)random.NextDouble() * 22f;
                spin[i] = 4f + (float)random.NextDouble() * 9f;
                baseRotation[i] = (float)(random.NextDouble() * 60.0 - 30.0);
            }

            Tick(0f);
        }

        private static void BuildFigure(RectTransform holder, Vector2Int[] cells, float scale, Color color)
        {
            float pitch = (BlockSize + BlockGap) * scale;
            float size = BlockSize * scale;

            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                maxX = Mathf.Max(maxX, cells[i].x);
                maxY = Mathf.Max(maxY, cells[i].y);
            }

            var centre = new Vector2(maxX * pitch * 0.5f, maxY * pitch * 0.5f);
            for (int i = 0; i < cells.Length; i++)
            {
                Image block = UIFactory.CreateImage("Block", holder, color);
                block.raycastTarget = false;
                UIFactory.Anchor(
                    block.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(cells[i].x * pitch, cells[i].y * pitch) - centre,
                    new Vector2(size, size));
            }
        }
    }
}
