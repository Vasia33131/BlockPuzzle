using UnityEngine;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Shapes of the profile widgets drawn in code, so they need no art: an anti-aliased disc
    /// (avatar mask and ring) and the level shield. Tinted through the Image colour.
    /// </summary>
    public static class ProfileUi
    {
        private const int CircleSize = 128;
        private const int ShieldWidth = 112;
        private const int ShieldHeight = 128;
        private const int Samples = 3;

        /// <summary>Height of the shield's flat upper part, as a share of its height (the rest tapers to a point).</summary>
        private const float ShieldShoulder = 0.42f;

        private static Sprite circleSprite;
        private static Sprite shieldSprite;

        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null)
                {
                    circleSprite = Bake("ProfileCircle", CircleSize, CircleSize, InsideCircle);
                }

                return circleSprite;
            }
        }

        public static Sprite ShieldSprite
        {
            get
            {
                if (shieldSprite == null)
                {
                    shieldSprite = Bake("ProfileShield", ShieldWidth, ShieldHeight, InsideShield);
                }

                return shieldSprite;
            }
        }

        private static bool InsideCircle(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            return x * x + y * y <= 1f;
        }

        /// <summary>Flat top with softly rounded corners, sides straight down to the shoulder, then a rounded taper to the point.</summary>
        private static bool InsideShield(float u, float v)
        {
            float x = Mathf.Abs(u * 2f - 1f);
            float fromTop = 1f - v;

            // Rounded top corners.
            const float corner = 0.16f;
            if (fromTop < corner && x > 1f - corner)
            {
                float dx = x - (1f - corner);
                float dy = corner - fromTop;
                return dx * dx + dy * dy <= corner * corner;
            }

            if (v >= ShieldShoulder)
            {
                return true;
            }

            // Below the shoulder the half-width follows a quarter ellipse down to a point.
            float t = (ShieldShoulder - v) / ShieldShoulder;
            float halfWidth = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * t));
            return x <= halfWidth;
        }

        private static Sprite Bake(string name, int width, int height, System.Func<float, float, bool> inside)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
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

                    byte alpha = (byte)Mathf.RoundToInt(255f * hits / (Samples * Samples));
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(
                texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
