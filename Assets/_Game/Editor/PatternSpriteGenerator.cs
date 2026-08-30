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

        // Paint at the original authoring resolution, then downsample so the WebGL
        // data file stays small. ThemePattern keeps on-screen tile size unchanged.
        public const int BlockSourceSize = 256;
        public const int BlockExportSize = 64;
        public const int BackgroundSourceSize = 1024;
        public const int BackgroundExportSize = 256;

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
            Write(BlockClassicPath, BlockClassicGuid, BlockSourceSize, BlockExportSize, PaintClassicBlock, force);
            Write(BlockOceanPath, BlockOceanGuid, BlockSourceSize, BlockExportSize, PaintOceanBlock, force);
            Write(BlockCandyPath, BlockCandyGuid, BlockSourceSize, BlockExportSize, PaintCandyBlock, force);
            Write(BgClassicPath, BgClassicGuid, BackgroundSourceSize, BackgroundExportSize, PaintClassicBackground, force);
            Write(BgOceanPath, BgOceanGuid, BackgroundSourceSize, BackgroundExportSize, PaintOceanBackground, force);
            Write(BgCandyPath, BgCandyGuid, BackgroundSourceSize, BackgroundExportSize, PaintCandyBackground, force);
        }

        private static Sprite Write(
            string path,
            string guid,
            int sourceSize,
            int exportSize,
            System.Action<Color32[], int> paint,
            bool force)
        {
            if (!force && File.Exists(path) && PngSizeEquals(path, exportSize))
            {
                ConfigureImporter(path, exportSize);
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            var sourcePixels = new Color32[sourceSize * sourceSize];
            paint(sourcePixels, sourceSize);
            Color32[] exportPixels = sourceSize == exportSize
                ? sourcePixels
                : Downsample(sourcePixels, sourceSize, exportSize);

            var texture = new Texture2D(exportSize, exportSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(exportPixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            string metaPath = path + ".meta";
            string resolvedGuid = ReadExistingGuid(metaPath) ?? guid;
            if (!File.Exists(metaPath))
            {
                WriteMeta(path, resolvedGuid, exportSize);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(path, exportSize);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool PngSizeEquals(string path, int size)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length < 24)
                    {
                        return false;
                    }

                    byte[] header = new byte[24];
                    if (stream.Read(header, 0, 24) < 24)
                    {
                        return false;
                    }

                    int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                    int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                    return width == size && height == size;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static Color32[] Downsample(Color32[] source, int sourceSize, int exportSize)
        {
            var dest = new Color32[exportSize * exportSize];
            float scale = sourceSize / (float)exportSize;
            for (int y = 0; y < exportSize; y++)
            {
                for (int x = 0; x < exportSize; x++)
                {
                    float fx = x * scale;
                    float fy = y * scale;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, sourceSize - 1);
                    int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, sourceSize - 1);
                    int x1 = Mathf.Min(x0 + 1, sourceSize - 1);
                    int y1 = Mathf.Min(y0 + 1, sourceSize - 1);
                    float tx = fx - x0;
                    float ty = fy - y0;
                    Color a = source[y0 * sourceSize + x0];
                    Color b = source[y0 * sourceSize + x1];
                    Color c = source[y1 * sourceSize + x0];
                    Color d = source[y1 * sourceSize + x1];
                    dest[y * exportSize + x] = Color.Lerp(Color.Lerp(a, b, tx), Color.Lerp(c, d, tx), ty);
                }
            }

            return dest;
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

            int maxSize = Mathf.NextPowerOfTwo(Mathf.Max(1, size));
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool alreadyConfigured =
                importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.alphaIsTransparency &&
                !importer.mipmapEnabled &&
                importer.filterMode == FilterMode.Bilinear &&
                importer.wrapMode == TextureWrapMode.Repeat &&
                importer.textureCompression == TextureImporterCompression.Compressed &&
                importer.maxTextureSize == maxSize &&
                settings.spriteMeshType == SpriteMeshType.FullRect;
            if (alreadyConfigured)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
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
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = maxSize;
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
