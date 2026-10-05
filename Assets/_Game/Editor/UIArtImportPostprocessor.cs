using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Keeps the import settings of the painted UI art in <c>Resources/UI/Art</c> fixed, so a reimport or a
    /// fresh checkout never resets them: single sprites without mipmaps, compressed (DXT on WebGL), with the
    /// size limit at the picture's own size, so nothing is scaled down. The menu background is opaque (no
    /// alpha channel, DXT1) and crunched. Compression needs both sides to be a multiple of 4; a picture that
    /// is not gets a warning, because Unity would silently keep it as uncompressed RGBA32.
    /// </summary>
    public sealed class UIArtImportPostprocessor : AssetPostprocessor
    {
        public const string Folder = "Assets/_Game/Resources/UI/Art/";

        private const string OpaqueName = "MenuBackground";
        private const int MaxSize = 1024;
        private const int CrunchQuality = 70;

        /// <summary>Written to the importer once the settings are applied; a file without it is reimported.</summary>
        private const string Stamp = "BlockPuzzle.UIArtImport.v1";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            bool opaque = Path.GetFileNameWithoutExtension(assetPath) == OpaqueName;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaSource = opaque ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !opaque;

            int width;
            int height;
            ReadPngSize(assetPath, out width, out height);
            if (width % 4 != 0 || height % 4 != 0)
            {
                Debug.LogWarning("[Block Puzzle] " + assetPath + " is " + width + "x" + height
                                 + ": both sides must be a multiple of 4, or it cannot be compressed (pad it with transparent pixels).");
            }

            TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
            settings.maxTextureSize = SizeLimit(Mathf.Max(width, height));
            settings.format = TextureImporterFormat.Automatic;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.crunchedCompression = opaque;
            settings.compressionQuality = opaque ? CrunchQuality : 50;
            importer.SetPlatformTextureSettings(settings);

            // A platform override would silently win over the defaults above.
            importer.ClearPlatformTextureSettings("WebGL");
            importer.userData = Stamp;
        }

        /// <summary>The smallest power of two that holds <paramref name="size"/>, so the picture is never scaled down.</summary>
        private static int SizeLimit(int size)
        {
            int limit = 32;
            while (limit < size && limit < MaxSize)
            {
                limit *= 2;
            }

            return limit;
        }

        /// <summary>Width and height from the PNG header; zero when the file is not a PNG.</summary>
        private static void ReadPngSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            byte[] header = new byte[24];
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Read(header, 0, header.Length) < header.Length || header[1] != 'P' || header[2] != 'N' || header[3] != 'G')
                {
                    return;
                }
            }

            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        }

        /// <summary>Pictures imported before this script existed get the settings on the next editor load.</summary>
        [InitializeOnLoadMethod]
        private static void ReimportUnstamped()
        {
            EditorApplication.delayCall += () =>
            {
                var stale = new List<string>();
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.userData != Stamp)
                    {
                        stale.Add(path);
                    }
                }

                foreach (string path in stale)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            };
        }
    }
}
