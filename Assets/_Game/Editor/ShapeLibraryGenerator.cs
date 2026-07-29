using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Materialises the built-in <see cref="ShapeCatalog"/> into a real asset so the
    /// figures, their colours and their drop rates can be tuned in the Inspector.
    /// </summary>
    public static class ShapeLibraryGenerator
    {
        public const string AssetPath = "Assets/_Game/ScriptableObjects/ShapeLibrary.asset";

        [MenuItem("Tools/Block Puzzle/Regenerate Shape Library")]
        public static void Regenerate()
        {
            AssetDatabase.DeleteAsset(AssetPath);
            EnsureAsset();
        }

        /// <summary>
        /// Returns the library asset, rebuilding it when it is missing or when it was baked
        /// from an older revision of <see cref="ShapeCatalog"/>.
        /// </summary>
        public static ShapeLibrary EnsureAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShapeLibrary>(AssetPath);
            if (existing != null && existing.CatalogVersion == ShapeCatalog.Version)
            {
                return existing;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(AssetPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            ShapeLibrary library = ScriptableObject.CreateInstance<ShapeLibrary>();
            AssetDatabase.CreateAsset(library, AssetPath);

            List<BlockShape> shapes = ShapeCatalog.CreateDefaultShapes();
            foreach (BlockShape shape in shapes)
            {
                AssetDatabase.AddObjectToAsset(shape, library);
            }

            var serialized = new SerializedObject(library);
            SerializedProperty list = serialized.FindProperty("shapes");
            list.arraySize = shapes.Count;
            for (int i = 0; i < shapes.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = shapes[i];
            }

            serialized.FindProperty("catalogVersion").intValue = ShapeCatalog.Version;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<ShapeLibrary>(AssetPath);
        }
    }
}
