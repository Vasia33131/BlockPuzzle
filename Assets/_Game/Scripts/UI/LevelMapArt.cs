using System;
using UnityEngine;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Icons of the level map drawn in code, like <see cref="ProfileUi"/>: the padlock of a closed level,
    /// the trophy of the last node and the goal icons of the level card that <see cref="BlockPuzzle.Core.LevelArt"/>
    /// does not have (score target, cleared line). All white, tinted through the Image colour.
    /// </summary>
    public static class LevelMapArt
    {
        private const int Size = 128;
        private const int Samples = 3;

        private static Sprite lockSprite;
        private static Sprite trophySprite;
        private static Sprite targetSprite;
        private static Sprite linesSprite;

        public static Sprite LockSprite => lockSprite != null ? lockSprite : lockSprite = Bake("MapLock", InsideLock);
        public static Sprite TrophySprite => trophySprite != null ? trophySprite : trophySprite = Bake("MapTrophy", InsideTrophy);
        public static Sprite TargetSprite => targetSprite != null ? targetSprite : targetSprite = Bake("MapTarget", InsideTarget);
        public static Sprite LinesSprite => linesSprite != null ? linesSprite : linesSprite = Bake("MapLines", InsideLines);

        /// <summary>Rounded rectangle: the point is inside when it is within <paramref name="radius"/> of the shrunken box.</summary>
        private static bool Box(float u, float v, float x0, float x1, float y0, float y1, float radius)
        {
            float cx = Mathf.Clamp(u, x0 + radius, x1 - radius);
            float cy = Mathf.Clamp(v, y0 + radius, y1 - radius);
            float dx = u - cx;
            float dy = v - cy;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static bool InsideLock(float u, float v)
        {
            bool body = Box(u, v, 0.2f, 0.8f, 0.1f, 0.56f, 0.09f);

            float dx = u - 0.5f;
            float dy = v - 0.56f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            bool arc = v >= 0.56f && distance <= 0.27f && distance >= 0.16f;
            bool legs = v >= 0.5f && v < 0.56f && Mathf.Abs(dx) >= 0.16f && Mathf.Abs(dx) <= 0.27f;

            float kx = u - 0.5f;
            float ky = v - 0.37f;
            bool keyhole = kx * kx + ky * ky <= 0.075f * 0.075f
                || (Mathf.Abs(u - 0.5f) <= 0.03f && v >= 0.2f && v <= 0.37f);

            return (body || arc || legs) && !keyhole;
        }

        private static bool InsideTrophy(float u, float v)
        {
            float dx = Mathf.Abs(u - 0.5f);

            bool bowl = false;
            if (v >= 0.45f && v <= 0.9f)
            {
                float t = (v - 0.45f) / 0.45f;
                bowl = dx <= 0.12f + 0.16f * Mathf.Pow(t, 0.7f);
            }
            else if (v < 0.45f)
            {
                float ex = dx / 0.12f;
                float ey = (v - 0.45f) / 0.07f;
                bowl = ex * ex + ey * ey <= 1f;
            }

            bool stem = Box(u, v, 0.44f, 0.56f, 0.22f, 0.42f, 0.02f);
            bool foot = Box(u, v, 0.3f, 0.7f, 0.08f, 0.2f, 0.04f);

            float hx = dx - 0.3f;
            float hy = v - 0.72f;
            float handle = Mathf.Sqrt(hx * hx + hy * hy);
            bool handles = handle <= 0.14f && handle >= 0.07f;

            return bowl || stem || foot || handles;
        }

        /// <summary>A ring around a dot: the score target.</summary>
        private static bool InsideTarget(float u, float v)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return (distance <= 0.48f && distance >= 0.36f) || distance <= 0.2f;
        }

        /// <summary>A row of four blocks: a cleared line.</summary>
        private static bool InsideLines(float u, float v)
        {
            for (int i = 0; i < 4; i++)
            {
                float x0 = 0.06f + i * 0.24f;
                if (Box(u, v, x0, x0 + 0.2f, 0.36f, 0.64f, 0.04f))
                {
                    return true;
                }
            }

            return false;
        }

        private static Sprite Bake(string name, Func<float, float, bool> inside)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / Samples) / Size;
                            float v = (y + (sy + 0.5f) / Samples) / Size;
                            if (inside(u, v))
                            {
                                hits++;
                            }
                        }
                    }

                    byte alpha = (byte)Mathf.RoundToInt(255f * hits / (Samples * Samples));
                    pixels[y * Size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
