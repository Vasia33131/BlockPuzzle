using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Generates the tiled white-on-transparent overlays used by themes, so the project
    /// ships without any hand-authored pattern art.
    /// </summary>
    public static class PatternSpriteGenerator
    {
        public const string Folder = "Assets/Resources/UI/Patterns";

        public const string BlockClassicPath = Folder + "/BlockClassic.png";
        public const string BlockOceanPath = Folder + "/BlockOcean.png";
        public const string BlockCandyPath = Folder + "/BlockCandy.png";
        public const string BgClassicPath = Folder + "/BgClassic.png";
        public const string BgOceanPath = Folder + "/BgOcean.png";
        public const string BgCandyPath = Folder + "/BgCandy.png";

        public const string BlockClassicGuid = "c5f5765ac27fbf345a7daafe6017ece7";
        public const string BlockOceanGuid = "a18e73a8bf38d694b873e2e7cc79fea1";
        public const string BlockCandyGuid = "0f7bcc6c107a547489a59bb4c0acdad5";
        public const string BgClassicGuid = "bae99b6cbd283f443934486ac3c32095";
        public const string BgOceanGuid = "06a04715265810546992011341b40e77";
        public const string BgCandyGuid = "da749fda472104b419c7647b598a7057";

        private const int BlockSize = 32;
        private const int BackgroundSize = 64;

        [MenuItem("Tools/Block Puzzle/Regenerate Theme Patterns", priority = 26)]
        public static void Regenerate()
        {
            GenerateAll(force: true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Block Puzzle] Theme pattern sprites regenerated.");
        }

        public static void EnsureAssets()
        {
            GenerateAll(force: false);
        }

        private static void GenerateAll(bool force)
        {
            Directory.CreateDirectory(Folder);
            Write(BlockClassicPath, BlockClassicGuid, BlockSize, PaintClassicBlock, force);
            Write(BlockOceanPath, BlockOceanGuid, BlockSize, PaintOceanBlock, force);
            Write(BlockCandyPath, BlockCandyGuid, BlockSize, PaintCandyBlock, force);
            Write(BgClassicPath, BgClassicGuid, BackgroundSize, PaintClassicBackground, force);
            Write(BgOceanPath, BgOceanGuid, BackgroundSize, PaintOceanBackground, force);
            Write(BgCandyPath, BgCandyGuid, BackgroundSize, PaintCandyBackground, force);
        }

        private static Sprite Write(string path, string guid, int size, System.Action<Color32[], int> paint, bool force)
        {
            if (!force && File.Exists(path))
            {
                ConfigureImporter(path, size);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            paint(pixels, size);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            string metaPath = path + ".meta";
            string resolvedGuid = ReadExistingGuid(metaPath) ?? guid;
            if (!File.Exists(metaPath))
            {
                WriteMeta(path, resolvedGuid, size);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path, size);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string ReadExistingGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
            {
                return null;
            }

            foreach (string line in File.ReadLines(metaPath))
            {
                if (line.StartsWith("guid: "))
                {
                    string value = line.Substring(6).Trim();
                    return string.IsNullOrEmpty(value) ? null : value;
                }
            }

            return null;
        }

        private static void WriteMeta(string path, string guid, int size)
        {
            int maxSize = Mathf.NextPowerOfTwo(size);
            File.WriteAllText(
                path + ".meta",
                "fileFormatVersion: 2\n" +
                "guid: " + guid + "\n" +
                "TextureImporter:\n" +
                "  serializedVersion: 13\n" +
                "  spriteMode: 1\n" +
                "  spritePixelsToUnits: 100\n" +
                "  textureType: 8\n" +
                "  alphaIsTransparency: 1\n" +
                "  textureSettings:\n" +
                "    serializedVersion: 2\n" +
                "    filterMode: 1\n" +
                "    wrapU: 0\n" +
                "    wrapV: 0\n" +
                "    wrapW: 0\n" +
                "  spriteMeshType: 0\n" +
                "  maxTextureSize: " + maxSize + "\n");
        }

        private static void ConfigureImporter(string path, int size)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = 100f;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Repeat;
            importer.SetTextureSettings(settings);

            importer.spriteBorder = Vector4.zero;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.Max(2048, Mathf.NextPowerOfTwo(Mathf.Max(1, size)));
            importer.SaveAndReimport();
        }

        private static void PaintClassicBlock(Color32[] pixels, int size)
        {
            Clear(pixels);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if ((x + y) % 8 < 2)
                    {
                        pixels[y * size + x] = Ink(200);
                    }
                }
            }
        }

        private static void PaintOceanBlock(Color32[] pixels, int size)
        {
            Clear(pixels);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wave = y + 3f * Mathf.Sin(x * Mathf.PI * 2f / size);
                    float band = Mathf.Abs(Mathf.Repeat(wave, 8f) - 4f);
                    if (band < 1.15f)
                    {
                        pixels[y * size + x] = Ink(210);
                    }
                }
            }
        }

        private static void PaintCandyBlock(Color32[] pixels, int size)
        {
            Clear(pixels);
            const int cell = 8;
            float radius = 2.15f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x % cell) - cell * 0.5f + 0.5f;
                    float dy = (y % cell) - cell * 0.5f + 0.5f;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        pixels[y * size + x] = Ink(220);
                    }
                }
            }
        }

        private static void PaintClassicBackground(Color32[] pixels, int size)
        {
            Clear(pixels);
            const int step = 32;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int lx = x % step;
                    int ly = y % step;
                    bool plus = (lx == step / 2 && Mathf.Abs(ly - step / 2) <= 6)
                                || (ly == step / 2 && Mathf.Abs(lx - step / 2) <= 6);
                    if (plus)
                    {
                        pixels[y * size + x] = Ink(180);
                    }
                }
            }
        }

        private static void PaintOceanBackground(Color32[] pixels, int size)
        {
            Clear(pixels);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wave = y + 7f * Mathf.Sin(x * Mathf.PI * 2f / size);
                    float band = Mathf.Abs(Mathf.Repeat(wave, 16f) - 8f);
                    if (band < 1.1f)
                    {
                        pixels[y * size + x] = Ink(170);
                    }
                }
            }
        }

        private static void PaintCandyBackground(Color32[] pixels, int size)
        {
            Clear(pixels);
            const int cell = 16;
            float radius = 3.2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x % cell) - cell * 0.5f + 0.5f;
                    float dy = (y % cell) - cell * 0.5f + 0.5f;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        pixels[y * size + x] = Ink(190);
                    }
                }
            }
        }

        private static void Clear(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 0);
            }
        }

        private static Color32 Ink(byte alpha) => new Color32(255, 255, 255, alpha);
    }
}
