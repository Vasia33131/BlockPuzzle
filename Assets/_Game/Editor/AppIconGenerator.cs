using System.IO;
using UnityEditor;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Generates a simple block-puzzle app icon and assigns it to Android player settings.
    /// </summary>
    public static class AppIconGenerator
    {
        public const string IconPath = "Assets/_Game/Art/AppIcon.png";

        private const int Size = 512;
        private const int Grid = 3;
        private const float Margin = 56f;
        private const float Gap = 18f;
        private const float Corner = 28f;

        [MenuItem("Tools/Block Puzzle/Regenerate App Icon", priority = 40)]
        public static void Regenerate()
        {
            Texture2D icon = EnsureIcon(force: true);
            AssignAndroidIcon(icon);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Block Puzzle] App icon written to {IconPath} and assigned for Android.");
        }

        public static Texture2D EnsureIcon(bool force = false)
        {
            if (!force && File.Exists(IconPath))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));

            Texture2D texture = PaintIcon();
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        public static void AssignAndroidIcon(Texture2D icon)
        {
            if (icon == null)
            {
                return;
            }

            var target = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetIcons(target, new[] { icon }, IconKind.Application);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });

            AssignPlatformKind(target, UnityEditor.Android.AndroidPlatformIconKind.Adaptive, icon);
            AssignPlatformKind(target, UnityEditor.Android.AndroidPlatformIconKind.Round, icon);
            AssignPlatformKind(target, UnityEditor.Android.AndroidPlatformIconKind.Legacy, icon);
        }

        private static void AssignPlatformKind(
            UnityEditor.Build.NamedBuildTarget target,
            PlatformIconKind kind,
            Texture2D icon)
        {
            PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
            if (icons == null || icons.Length == 0)
            {
                return;
            }

            for (int i = 0; i < icons.Length; i++)
            {
                // Adaptive icons want foreground (+ optional background). One texture is enough
                // for Round/Legacy and still produces a usable Adaptive foreground.
                icons[i].SetTexture(icon);
            }

            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        private static void ConfigureImporter()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 512;
            importer.SaveAndReimport();
        }

        private static Texture2D PaintIcon()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            Color32 bgTop = To32(GameTheme.BackgroundTop);
            Color32 bgBottom = To32(GameTheme.BackgroundBottom);

            // Soft vertical backdrop.
            for (int y = 0; y < Size; y++)
            {
                float t = y / (float)(Size - 1);
                Color32 bg = Lerp(bgBottom, bgTop, t);
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = bg;
                }
            }

            float cell = (Size - Margin * 2f - Gap * (Grid - 1)) / Grid;
            // 3x3 with the centre empty — reads as a "missing block" puzzle piece.
            bool[,] filled =
            {
                { true, true, true },
                { true, false, true },
                { true, true, false }
            };

            Color[] palette =
            {
                GameTheme.Pastel(0),
                GameTheme.Pastel(2),
                GameTheme.Pastel(4),
                GameTheme.Pastel(6),
                GameTheme.Pastel(1),
                GameTheme.Pastel(3),
                GameTheme.Pastel(5),
                GameTheme.Pastel(7)
            };

            int colorIndex = 0;
            for (int row = 0; row < Grid; row++)
            {
                for (int col = 0; col < Grid; col++)
                {
                    if (!filled[row, col])
                    {
                        continue;
                    }

                    float x0 = Margin + col * (cell + Gap);
                    // Texture y grows upward; icon rows read top-to-bottom.
                    float y0 = Size - Margin - (row + 1) * cell - row * Gap;
                    FillRoundedRect(pixels, x0, y0, cell, cell, Corner, To32(palette[colorIndex % palette.Length]));
                    colorIndex++;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void FillRoundedRect(Color32[] pixels, float x, float y, float w, float h, float radius, Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(x));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(x + w));
            int minY = Mathf.Max(0, Mathf.FloorToInt(y));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(y + h));

            var half = new Vector2(w * 0.5f, h * 0.5f);
            var center = new Vector2(x + half.x, y + half.y);
            float r = Mathf.Min(radius, Mathf.Min(half.x, half.y));

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    var point = new Vector2(px + 0.5f, py + 0.5f) - center;
                    Vector2 inner = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - (half - Vector2.one * r);
                    float distance = Vector2.Max(inner, Vector2.zero).magnitude
                                     + Mathf.Min(Mathf.Max(inner.x, inner.y), 0f)
                                     - r;

                    float cover = Mathf.Clamp01(0.5f - distance);
                    if (cover <= 0f)
                    {
                        continue;
                    }

                    int index = py * Size + px;
                    pixels[index] = Blend(pixels[index], color, cover);
                }
            }
        }

        private static Color32 Blend(Color32 dst, Color32 src, float t)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(dst.r + (src.r - dst.r) * t),
                (byte)Mathf.RoundToInt(dst.g + (src.g - dst.g) * t),
                (byte)Mathf.RoundToInt(dst.b + (src.b - dst.b) * t),
                (byte)Mathf.RoundToInt(dst.a + (src.a - dst.a) * t));
        }

        private static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(a.r + (b.r - a.r) * t),
                (byte)Mathf.RoundToInt(a.g + (b.g - a.g) * t),
                (byte)Mathf.RoundToInt(a.b + (b.b - a.b) * t),
                (byte)Mathf.RoundToInt(a.a + (b.a - a.a) * t));
        }

        private static Color32 To32(Color color)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(color.r * 255f),
                (byte)Mathf.RoundToInt(color.g * 255f),
                (byte)Mathf.RoundToInt(color.b * 255f),
                (byte)Mathf.RoundToInt(color.a * 255f));
        }
    }
}
