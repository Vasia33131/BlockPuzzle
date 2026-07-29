using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Generates the single nine-sliced rounded rectangle the whole interface is built
    /// from, so the project ships without any binary art dependency.
    /// </summary>
    public static class RoundedSpriteGenerator
    {
        public const string AssetPath = "Assets/Resources/UI/RoundedRect.png";

        private const int TextureSize = 48;
        private const float CornerRadius = 12f;
        private const float SliceBorder = 14f;

        [MenuItem("Tools/Block Puzzle/Regenerate Rounded Sprite")]
        public static void Regenerate()
        {
            Generate(force: true);
            AssetDatabase.SaveAssets();
        }

        public static Sprite EnsureAsset()
        {
            return Generate(force: false);
        }

        private static Sprite Generate(bool force)
        {
            if (!force && File.Exists(AssetPath))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            Texture2D texture = CreateRoundedTexture();
            File.WriteAllBytes(AssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter();

            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
        }

        private static void ConfigureImporter()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(SliceBorder, SliceBorder, SliceBorder, SliceBorder);
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 64;
            importer.SaveAndReimport();
        }

        /// <summary>Signed-distance rounded box rasterised with one pixel of anti-aliasing.</summary>
        private static Texture2D CreateRoundedTexture()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureSize * TextureSize];
            var half = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f) - half;
                    Vector2 inner = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y))
                                    - (half - Vector2.one * CornerRadius);

                    float distance = Vector2.Max(inner, Vector2.zero).magnitude
                                     + Mathf.Min(Mathf.Max(inner.x, inner.y), 0f)
                                     - CornerRadius;

                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
