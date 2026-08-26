using System.IO;
using UnityEditor;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Generates theme pattern sprites and refreshes ThemeConfig assets on editor load
    /// so palettes and overlays appear without a manual menu click.
    /// </summary>
    [InitializeOnLoad]
    public static class ThemeAssetRepair
    {
        private const string SessionKey = "BlockPuzzle.ThemeAssetRepair.ClassicBind.Ran";

        static ThemeAssetRepair()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (Application.isBatchMode ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            bool missingPatterns = !File.Exists(PatternSpriteGenerator.BlockClassicPath) ||
                                   !File.Exists(PatternSpriteGenerator.BlockOceanPath) ||
                                   !File.Exists(PatternSpriteGenerator.BlockCandyPath) ||
                                   !File.Exists(PatternSpriteGenerator.BgClassicPath) ||
                                   !File.Exists(PatternSpriteGenerator.BgOceanPath) ||
                                   !File.Exists(PatternSpriteGenerator.BgCandyPath);
            bool missingBindings = ThemeSpritesUnassigned();

            if (!missingPatterns && !missingBindings && SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            PatternSpriteGenerator.EnsureAssets();
            ThemeConfigGenerator.EnsureAssets();
            ShapeLibraryGenerator.EnsureAsset();
            TmpCyrillicFontGenerator.EnsureAsset();
            PrefabGenerator.EnsureGridCell();
            PrefabGenerator.EnsureBlockPiece();
            ConfigureBoosterIcons();
            if (missingPatterns || missingBindings)
            {
                Debug.Log("[Block Puzzle] Theme palettes, pattern sprites and cube/cell prefabs refreshed.");
            }
        }

        private static bool ThemeSpritesUnassigned()
        {
            return SpriteMissing(ThemeConfigGenerator.Folder + "/ThemeDefault.asset") ||
                   SpriteMissing(ThemeConfigGenerator.Folder + "/ThemeOcean.asset") ||
                   SpriteMissing(ThemeConfigGenerator.Folder + "/ThemeCandy.asset");
        }

        private static bool SpriteMissing(string assetPath)
        {
            ThemeConfig theme = AssetDatabase.LoadAssetAtPath<ThemeConfig>(assetPath);
            if (theme == null)
            {
                return true;
            }

            var serialized = new SerializedObject(theme);
            SerializedProperty block = serialized.FindProperty("blockPattern");
            SerializedProperty background = serialized.FindProperty("backgroundPattern");
            return block == null || block.objectReferenceValue == null ||
                   background == null || background.objectReferenceValue == null;
        }

        private static void ConfigureBoosterIcons()
        {
            ConfigureBoosterIcon("Assets/Resources/UI/Icons/IconUndo.png");
            ConfigureBoosterIcon("Assets/Resources/UI/Icons/IconExtra.png");
            ConfigureBoosterIcon("Assets/Resources/UI/Icons/IconClear.png");
        }

        private static void ConfigureBoosterIcon(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool alreadyConfigured =
                importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.alphaIsTransparency &&
                importer.filterMode == FilterMode.Bilinear &&
                importer.wrapMode == TextureWrapMode.Clamp &&
                settings.spriteMeshType == SpriteMeshType.FullRect;
            if (alreadyConfigured)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
