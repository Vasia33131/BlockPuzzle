using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Sprites of the levels mode, drawn in code so they need no art files and no emoji in the
    /// font: the crystal on a board block, the frame of a marked cell and the result star.
    /// Each texture is built once on first use.
    /// </summary>
    public static class LevelArt
    {
        public static readonly Color CrystalTint = GameTheme.FromHex("#55E6FF");
        public static readonly Color MarkColor = GameTheme.FromHex("#FFD54A");
        public static readonly Color StarGold = GameTheme.FromHex("#FFC83D");

        private const int Size = 64;

        private static Sprite crystalSprite;
        private static Sprite frameSprite;
        private static Sprite starSprite;

        /// <summary>Cut gem: a rhombus with a lit top-left facet and a shaded bottom-right one.</summary>
        public static Sprite CrystalSprite => crystalSprite != null ? crystalSprite : (crystalSprite = BuildCrystal());

        /// <summary>Hollow rounded square, nine-sliced, for the frame of a marked cell.</summary>
        public static Sprite FrameSprite => frameSprite != null ? frameSprite : (frameSprite = BuildFrame());

        /// <summary>Solid five-point star, white so it can be tinted.</summary>
        public static Sprite StarSprite => starSprite != null ? starSprite : (starSprite = BuildStar());

        private static Sprite BuildCrystal()
        {
            const float half = Size * 0.5f;
            Color light = Color.white;
            Color mid = new Color(0.78f, 0.97f, 1f, 1f);
            Color dark = new Color(0.36f, 0.72f, 0.92f, 1f);
            Color rim = new Color(0.13f, 0.42f, 0.70f, 1f);

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f - half) / (half - 2f);
                    float dy = (y + 0.5f - half) / (half - 2f);

                    // Rhombus, a little taller than wide.
                    float d = Mathf.Abs(dx) / 0.86f + Mathf.Abs(dy);
                    float coverage = Mathf.Clamp01((1f - d) * (half - 2f) * 0.9f + 0.5f);

                    Color color;
                    if (d > 0.86f)
                    {
                        color = rim;
                    }
                    else if (dx - dy < -0.05f && dy > -0.15f)
                    {
                        color = light;
                    }
                    else if (dx + dy > 0.1f)
                    {
                        color = dark;
                    }
                    else
                    {
                        color = mid;
                    }

                    color.a = coverage;
                    pixels[y * Size + x] = color;
                }
            }

            return CreateSprite("LevelCrystal", pixels, Vector4.zero);
        }

        private static Sprite BuildFrame()
        {
            const float thickness = 7f;
            const float radius = 16f;
            const float half = Size * 0.5f;

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                    float distance = outside + inside - radius;

                    // Ring: inside the outer edge and outside the inner one, both antialiased.
                    float coverage = Mathf.Clamp01(0.5f - distance) * Mathf.Clamp01(distance + thickness + 0.5f);
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, coverage);
                }
            }

            return CreateSprite("LevelMarkFrame", pixels, new Vector4(24f, 24f, 24f, 24f));
        }

        private static Sprite BuildStar()
        {
            const int samples = 3;
            const float outer = Size * 0.5f - 2f;
            const float inner = outer * 0.42f;
            const float half = Size * 0.5f;

            var polygon = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outer : inner;
                polygon[i] = new Vector2(half + Mathf.Cos(angle) * radius, half + 2f + Mathf.Sin(angle) * radius - 1f);
            }

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            var point = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                            if (Contains(polygon, point))
                            {
                                hits++;
                            }
                        }
                    }

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, hits / (float)(samples * samples));
                }
            }

            return CreateSprite("LevelStar", pixels, Vector4.zero);
        }

        /// <summary>Even-odd point in polygon test.</summary>
        private static bool Contains(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Sprite CreateSprite(string name, Color[] pixels, Vector4 border)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, Size, Size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
        }
    }
}
