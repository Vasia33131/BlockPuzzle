using System;
using TMPro;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Colours, sprites and the text material of the main menu. Everything is drawn in code
    /// (no art assets), like <see cref="ProfileUi"/>: a soft radial glow, the settings gear, the
    /// play triangle and the infinity sign, all white so they can be tinted through the Image colour.
    /// Glyphs such as ▶ and ∞ are not in the TMP atlas, which is why the button icons are sprites.
    /// </summary>
    public static class MenuArt
    {
        /// <summary>Resources path of the outlined text material baked by the editor tool.</summary>
        public const string TextMaterialResource = "Fonts/MenuTextMaterial";

        public static readonly Color BackgroundTop = GameTheme.FromHex("#233A9A");
        public static readonly Color BackgroundBottom = GameTheme.FromHex("#0B1236");
        public static readonly Color GlowBase = GameTheme.FromHex("#5B8CFF");

        /// <summary>Dark navy used for text outlines and drop shadows.</summary>
        public static readonly Color Ink = GameTheme.FromHex("#141845");

        /// <summary>Yellow, blue, red, purple, green, cyan: the logo letters cycle through these.</summary>
        public static readonly Color[] LogoColors =
        {
            GameTheme.FromHex("#FFD23F"),
            GameTheme.FromHex("#3D8BFF"),
            GameTheme.FromHex("#FF4757"),
            GameTheme.FromHex("#A55EEA"),
            GameTheme.FromHex("#2ED573"),
            GameTheme.FromHex("#18DCFF")
        };

        private const int GlowSize = 128;
        private const int GearSize = 128;
        private const int PlaySize = 96;
        private const int InfinityWidth = 128;
        private const int InfinityHeight = 64;
        private const int InfinitySegments = 48;

        /// <summary>Half of the infinity stroke, in texels.</summary>
        private const float InfinityHalfStroke = 4.8f;

        private static Sprite glowSprite;
        private static Sprite gearSprite;
        private static Sprite playSprite;
        private static Sprite infinitySprite;
        private static Vector2[] lemniscate;
        private static Material textMaterial;
        private static bool textMaterialLoaded;

        public static Sprite GlowSprite =>
            glowSprite != null ? glowSprite : glowSprite = Bake("MenuGlow", GlowSize, GlowSize, GlowAlpha, 1);

        public static Sprite GearSprite =>
            gearSprite != null ? gearSprite : gearSprite = Bake("MenuGear", GearSize, GearSize, GearCoverage, 3);

        public static Sprite PlaySprite =>
            playSprite != null ? playSprite : playSprite = Bake("MenuPlay", PlaySize, PlaySize, PlayCoverage, 3);

        public static Sprite InfinitySprite =>
            infinitySprite != null
                ? infinitySprite
                : infinitySprite = Bake("MenuInfinity", InfinityWidth, InfinityHeight, InfinityCoverage, 2);

        /// <summary>
        /// Gives <paramref name="text"/> the shared outline + underlay material, so white or coloured
        /// letters get a thick dark outline and a drop shadow. Without the baked asset a material
        /// instance is configured at run time instead (fine in the Editor; a build can strip the
        /// shader variants, so run the Menu Text Material tool before building).
        /// </summary>
        public static void ApplyOutlinedMaterial(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            Material shared = LoadTextMaterial();
            // The baked material carries the atlas of the heading font; on any other font it would draw garbage.
            if (shared != null && text.font != null && shared.mainTexture == text.font.atlasTexture)
            {
                text.fontSharedMaterial = shared;
                return;
            }

            // No baked menu material for this font: use its baked outline preset, not a material instance.
            FontRole role = text.font == GameFonts.Get(FontRole.Body) ? FontRole.Body : FontRole.Heading;
            GameFonts.Apply(text, role, FontPreset.Outline);
        }

        /// <summary>Outline and underlay values shared by the baked asset and the run-time fallback.</summary>
        public static void ConfigureTextMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetColor("_OutlineColor", Ink);
            material.SetFloat("_OutlineWidth", 0.2f);
            material.SetFloat("_OutlineSoftness", 0f);
            material.SetColor("_UnderlayColor", new Color(0.02f, 0.03f, 0.12f, 0.85f));
            material.SetFloat("_UnderlayOffsetX", 0.6f);
            material.SetFloat("_UnderlayOffsetY", -0.6f);
            material.SetFloat("_UnderlayDilate", 0.35f);
            material.SetFloat("_UnderlaySoftness", 0.1f);
        }

        private static Material LoadTextMaterial()
        {
            if (!textMaterialLoaded)
            {
                textMaterial = Resources.Load<Material>(TextMaterialResource);
                textMaterialLoaded = true;
            }

            return textMaterial;
        }

        // ---------------------------------------------------------------- shapes

        private static float GlowAlpha(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
            return falloff * falloff * (2f - falloff);
        }

        /// <summary>Eight-toothed gear with a round hole.</summary>
        private static float GearCoverage(float u, float v)
        {
            float x = u * 2f - 1f;
            float y = v * 2f - 1f;
            float distance = Mathf.Sqrt(x * x + y * y);
            float angle = Mathf.Atan2(y, x);
            float t = Mathf.Repeat(angle / (2f * Mathf.PI) * 8f, 1f);
            float tooth = 1f - Mathf.Abs(t * 2f - 1f);
            float outer = 0.72f + 0.26f * Mathf.Clamp01((tooth - 0.25f) * 4f);
            return distance <= outer && distance >= 0.3f ? 1f : 0f;
        }

        private static float PlayCoverage(float u, float v)
        {
            var a = new Vector2(0.24f, 0.08f);
            var b = new Vector2(0.24f, 0.92f);
            var c = new Vector2(0.94f, 0.5f);
            var p = new Vector2(u, v);
            float d1 = Cross(a, b, p);
            float d2 = Cross(b, c, p);
            float d3 = Cross(c, a, p);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return hasNegative && hasPositive ? 0f : 1f;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }

        /// <summary>A lemniscate stroke: the distance to a 48-segment polyline decides the coverage.</summary>
        private static float InfinityCoverage(float u, float v)
        {
            EnsureLemniscate();
            var p = new Vector2((u - 0.5f) * InfinityWidth, (v - 0.5f) * InfinityHeight);
            float best = float.MaxValue;
            for (int i = 0; i < InfinitySegments; i++)
            {
                best = Mathf.Min(best, SegmentDistanceSqr(p, lemniscate[i], lemniscate[i + 1]));
            }

            return best <= InfinityHalfStroke * InfinityHalfStroke ? 1f : 0f;
        }

        private static void EnsureLemniscate()
        {
            if (lemniscate != null)
            {
                return;
            }

            lemniscate = new Vector2[InfinitySegments + 1];
            for (int i = 0; i <= InfinitySegments; i++)
            {
                float t = i / (float)InfinitySegments * 2f * Mathf.PI;
                float s = Mathf.Sin(t);
                float denominator = 1f + s * s;
                lemniscate[i] = new Vector2(
                    56f * Mathf.Cos(t) / denominator,
                    56f * s * Mathf.Cos(t) / denominator);
            }
        }

        private static float SegmentDistanceSqr(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            float t = lengthSqr > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSqr) : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }

        internal static Sprite Bake(string name, int width, int height, Func<float, float, float> coverage, int samples)
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
                    float sum = 0f;
                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            sum += coverage(
                                (x + (sx + 0.5f) / samples) / width,
                                (y + (sy + 0.5f) / samples) / height);
                        }
                    }

                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(sum / (samples * samples)));
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
