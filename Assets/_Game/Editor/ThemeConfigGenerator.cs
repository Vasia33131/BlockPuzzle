using System.IO;
using UnityEditor;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Writes the three theme palettes into Resources so <see cref="GameTheme"/> can
    /// load them at runtime. Missing files are created; existing generated assets are
    /// refreshed so new palette and pattern fields stay in sync with code.
    /// </summary>
    public static class ThemeConfigGenerator
    {
        public const string Folder = "Assets/_Game/Resources/Themes";

        [MenuItem("Tools/Block Puzzle/Regenerate Theme Configs", priority = 25)]
        public static void Regenerate()
        {
            PatternSpriteGenerator.EnsureAssets();
            DeleteIfExists(Folder + "/ThemeDefault.asset");
            DeleteIfExists(Folder + "/ThemeOcean.asset");
            DeleteIfExists(Folder + "/ThemeCandy.asset");
            EnsureAssets();
            Debug.Log("[Block Puzzle] Theme configs regenerated.");
        }

        public static void EnsureAssets()
        {
            PatternSpriteGenerator.EnsureAssets();
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Folder);
            Write("ThemeDefault.asset", ThemeConfig.CreateDefault,
                PatternSpriteGenerator.BlockClassicPath, PatternSpriteGenerator.BgClassicPath);
            Write("ThemeOcean.asset", ThemeConfig.CreateOcean,
                PatternSpriteGenerator.BlockOceanPath, PatternSpriteGenerator.BgOceanPath);
            Write("ThemeCandy.asset", ThemeConfig.CreateCandy,
                PatternSpriteGenerator.BlockCandyPath, PatternSpriteGenerator.BgCandyPath);
            AssetDatabase.SaveAssets();
        }

        private static void Write(
            string fileName,
            System.Func<ThemeConfig> factory,
            string blockPatternPath,
            string backgroundPatternPath)
        {
            string path = Folder + "/" + fileName;
            ThemeConfig created = factory();
            ThemeConfig existing = AssetDatabase.LoadAssetAtPath<ThemeConfig>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(created, path);
                existing = created;
            }
            else
            {
                EditorUtility.CopySerialized(created, existing);
                existing.name = Path.GetFileNameWithoutExtension(fileName);
                Object.DestroyImmediate(created);
            }

            BindPatterns(existing, blockPatternPath, backgroundPatternPath);
            EditorUtility.SetDirty(existing);
        }

        private static void BindPatterns(ThemeConfig config, string blockPath, string backgroundPath)
        {
            if (config == null)
            {
                return;
            }

            Sprite block = AssetDatabase.LoadAssetAtPath<Sprite>(blockPath);
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(backgroundPath);
            var serialized = new SerializedObject(config);
            SerializedProperty blockProperty = serialized.FindProperty("blockPattern");
            SerializedProperty backgroundProperty = serialized.FindProperty("backgroundPattern");
            if (block != null && blockProperty != null)
            {
                blockProperty.objectReferenceValue = block;
            }

            if (background != null && backgroundProperty != null)
            {
                backgroundProperty.objectReferenceValue = background;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
