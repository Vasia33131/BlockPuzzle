using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;
using BlockPuzzle.UI;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Bakes the outline + underlay text material of the main menu logo and buttons into Resources.
    /// A material saved with OUTLINE_ON / UNDERLAY_ON keeps those shader variants in a WebGL build;
    /// enabling the keywords only at run time would let the build strip them. It is a copy of the
    /// default TMP font material, so it shares that font's atlas (the Cyrillic fallback is picked up
    /// through the font's fallback table). Tweak the outline and shadow on the asset itself.
    /// </summary>
    [InitializeOnLoad]
    public static class MenuTextMaterialGenerator
    {
        public const string AssetPath = "Assets/_Game/Resources/Fonts/MenuTextMaterial.mat";

        static MenuTextMaterialGenerator()
        {
            EditorApplication.delayCall += RunOnLoad;
        }

        private static void RunOnLoad()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunOnLoad;
                return;
            }

            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(AssetPath))
            {
                Ensure(false);
            }
        }

        [MenuItem("Tools/Block Puzzle/Rebuild Menu Text Material", priority = 42)]
        public static void RebuildFromMenu()
        {
            if (Ensure(true) != null)
            {
                Debug.Log("[Block Puzzle] Menu text material is ready: " + AssetPath);
            }
        }

        /// <summary>Creates the material when it is missing, or resets it to the defaults when <paramref name="reset"/>.</summary>
        public static Material Ensure(bool reset)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(AssetPath);
            // The menu text is set in the heading face, so the material must carry its atlas.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MontserratFontGenerator.ExtraBoldAssetPath);
            if (font == null || font.material == null)
            {
                if (existing == null)
                {
                    Debug.LogError("[Block Puzzle] Heading TMP font is missing at " + MontserratFontGenerator.ExtraBoldAssetPath);
                }

                return existing;
            }

            // An older material still points at the previous font's atlas: rebuild it.
            if (existing != null && !reset && existing.mainTexture == font.atlasTexture)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            Material material = existing;
            if (material == null)
            {
                material = new Material(font.material) { name = "MenuTextMaterial" };
                AssetDatabase.CreateAsset(material, AssetPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(font.material);
            }

            material.EnableKeyword("OUTLINE_ON");
            material.EnableKeyword("UNDERLAY_ON");
            MenuArt.ConfigureTextMaterial(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
