using System;
using UnityEngine;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// White glyph sprites drawn in code (crown, bag, play, dot, pill) so the HUD needs no
    /// texture assets. Tint them through <c>Image.color</c>. They are created at runtime and
    /// hidden from scene serialisation, so views assign them in Awake instead of at bake time.
    /// </summary>
    public static class HudIcons
    {
        /// <summary>Height of the <see cref="Pill"/> sprite; its caps are half circles of this height.</summary>
        public const int PillHeight = 140;

        private const int IconPx = 128;
        private const int Samples = 3;
        private const int PillWidth = PillHeight + 1;

        private static Sprite crown;
        private static Sprite storefront;
        private static Sprite play;
        private static Sprite dot;
        private static Sprite pill;

        /// <summary>Three-spike crown for the record line.</summary>
        public static Sprite Crown => Cached(ref crown, "HudCrown", IconPx, IconPx, InsideCrown, Vector4.zero);

        /// <summary>
        /// Shop front for the shop button: a scalloped awning over a building with a door and two
        /// windows. It replaced a bag whose arched handle read as a padlock ("the shop is locked").
        /// </summary>
        public static Sprite Storefront =>
            Cached(ref storefront, "HudStorefront", IconPx, IconPx, InsideStorefront, Vector4.zero);

        /// <summary>Right-pointing triangle used on the "watch an ad" chips.</summary>
        public static Sprite Play => Cached(ref play, "HudPlay", IconPx, IconPx, InsidePlay, Vector4.zero);

        /// <summary>Filled circle for notification dots.</summary>
        public static Sprite Dot => Cached(ref dot, "HudDot", IconPx, IconPx, InsideDot, Vector4.zero);

        /// <summary>
        /// Nine-sliced stadium. Only the horizontal borders are set, so a <see cref="PillHeight"/> tall
        /// Image gets exact half-circle caps at any width.
        /// </summary>
        public static Sprite Pill =>
            Cached(ref pill, "HudPill", PillWidth, PillHeight, InsidePill, new Vector4(PillHeight / 2f, 0f, PillHeight / 2f, 0f));

        private static Sprite Cached(
            ref Sprite slot, string name, int width, int height, Func<float, float, bool> inside, Vector4 border)
        {
            if (slot == null)
            {
                slot = Bake(name, width, height, inside, border);
            }

            return slot;
        }

        private static Sprite Bake(string name, int width, int height, Func<float, float, bool> inside, Vector4 border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[width * height];
            int total = Samples * Samples;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / Samples) / width;
                            float v = (y + (sy + 0.5f) / Samples) / height;
                            if (inside(u, v))
                            {
                                hits++;
                            }
                        }
                    }

                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(255 * hits / total));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool InsidePill(float u, float v)
        {
            float x = u * PillWidth;
            float y = v * PillHeight;
            float dx = Mathf.Max(0f, Mathf.Abs(x - PillWidth * 0.5f) - 0.5f);
            float dy = y - PillHeight * 0.5f;
            float r = PillHeight * 0.5f;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool InsideDot(float u, float v)
        {
            float dx = u - 0.5f;
            float dy = v - 0.5f;
            return dx * dx + dy * dy <= 0.25f;
        }

        private static bool InsidePlay(float u, float v)
        {
            return InsidePolygon(u, v, PlayPoints);
        }

        private static bool InsideCrown(float u, float v)
        {
            if (u >= 0.14f && u <= 0.86f && v >= 0.14f && v <= 0.32f)
            {
                return true;
            }

            return InsidePolygon(u, v, CrownPoints)
                || InsideCircle(u, v, 0.10f, 0.80f, 0.065f)
                || InsideCircle(u, v, 0.50f, 0.88f, 0.065f)
                || InsideCircle(u, v, 0.90f, 0.80f, 0.065f);
        }

        private static bool InsideStorefront(float u, float v)
        {
            // Awning: a band across the top with five half-round scallops hanging from it.
            if (InsideRoundRect(u, v, 0.06f, 0.62f, 0.94f, 0.86f, 0.05f))
            {
                return true;
            }

            const float ScallopRadius = 0.088f;
            for (int i = 0; i < 5; i++)
            {
                float cx = 0.06f + ScallopRadius + i * ScallopRadius * 2f;
                if (v <= 0.62f && InsideCircle(u, v, cx, 0.62f, ScallopRadius))
                {
                    return true;
                }
            }

            // Building under the awning, with the door and two windows cut out of it.
            if (!InsideRoundRect(u, v, 0.14f, 0.08f, 0.86f, 0.48f, 0.04f))
            {
                return false;
            }

            bool door = u >= 0.42f && u <= 0.58f && v <= 0.34f;
            bool leftWindow = u >= 0.21f && u <= 0.35f && v >= 0.22f && v <= 0.36f;
            bool rightWindow = u >= 0.65f && u <= 0.79f && v >= 0.22f && v <= 0.36f;
            return !(door || leftWindow || rightWindow);
        }

        private static readonly Vector2[] PlayPoints =
        {
            new Vector2(0.28f, 0.12f), new Vector2(0.28f, 0.88f), new Vector2(0.88f, 0.50f)
        };

        private static readonly Vector2[] CrownPoints =
        {
            new Vector2(0.14f, 0.30f), new Vector2(0.10f, 0.80f), new Vector2(0.32f, 0.54f),
            new Vector2(0.50f, 0.88f), new Vector2(0.68f, 0.54f), new Vector2(0.90f, 0.80f),
            new Vector2(0.86f, 0.30f)
        };

        private static bool InsideCircle(float u, float v, float cx, float cy, float r)
        {
            float dx = u - cx;
            float dy = v - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool InsideRoundRect(float u, float v, float x0, float y0, float x1, float y1, float r)
        {
            if (u < x0 || u > x1 || v < y0 || v > y1)
            {
                return false;
            }

            float cx = Mathf.Clamp(u, x0 + r, x1 - r);
            float cy = Mathf.Clamp(v, y0 + r, y1 - r);
            float dx = u - cx;
            float dy = v - cy;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>Even-odd ray cast.</summary>
        private static bool InsidePolygon(float u, float v, Vector2[] points)
        {
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[j];
                if ((a.y > v) != (b.y > v) && u < (b.x - a.x) * (v - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
