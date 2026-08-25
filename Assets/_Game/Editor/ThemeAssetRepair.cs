using System.IO;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Generates theme pattern sprites and refreshes ThemeConfig assets on editor load
    /// so palettes and overlays appear without a manual menu click.
    /// </summary>
    [InitializeOnLoad]
    public static class ThemeAssetRepair
    {
        private const string SessionKey = "BlockPuzzle.ThemeAssetRepair.Patterns.Ran";

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

            if (!missingPatterns && SessionState.GetBool(SessionKey, false))
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
            if (missingPatterns)
            {
                Debug.Log("[Block Puzzle] Theme palettes, pattern sprites and cube/cell prefabs refreshed.");
            }
        }
    }
}
