using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;
using BlockPuzzle.Core;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Bakes the outline and shadow material presets next to the Montserrat font assets
    /// ("Montserrat-ExtraBold SDF Outline.mat" and so on). <see cref="GameFonts.Apply(TMPro.TMP_Text, FontRole, FontPreset)"/>
    /// assigns them as shared materials, so no text ever needs a material instance or outlineWidth at run
    /// time. Tweak thickness and colour on the .mat assets; saved keywords keep the shader variants in a WebGL build.
    /// </summary>
    [InitializeOnLoad]
    public static class FontMaterialPresetGenerator
    {
        private const string Folder = "Assets/_Game/Resources/Fonts/";

        private static readonly Color OutlineColor = new Color(0.08f, 0.06f, 0.18f, 0.9f);
        private static readonly Color ShadowColor = new Color(0.02f, 0.03f, 0.12f, 0.75f);

        static FontMaterialPresetGenerator()
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

            EnsureAll(false);
        }

        [MenuItem("Tools/Block Puzzle/Rebuild Font Material Presets", priority = 45)]
        public static void RebuildFromMenu()
        {
            EnsureAll(true);
            Debug.Log("[Block Puzzle] Font material presets are ready in " + Folder);
        }

        /// <summary>Creates every missing preset (or resets all of them when <paramref name="reset"/>).</summary>
        public static void EnsureAll(bool reset)
        {
            bool changed = false;
            changed |= EnsureFor(MontserratFontGenerator.ExtraBoldAssetPath, reset);
            changed |= EnsureFor(MontserratFontGenerator.SemiBoldAssetPath, reset);
            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }

        private static bool EnsureFor(string fontPath, bool reset)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null || font.material == null || font.atlasTexture == null)
            {
                return false;
            }

            bool changed = false;
            changed |= Ensure(font, FontPreset.Outline, reset);
            changed |= Ensure(font, FontPreset.Shadow, reset);
            return changed;
        }

        private static bool Ensure(TMP_FontAsset font, FontPreset preset, bool reset)
        {
            string path = Folder + font.name + " " + preset + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            // A preset made from an older atlas is rebuilt; a tweaked one is left alone.
            if (existing != null && !reset && existing.mainTexture == font.atlasTexture)
            {
                return false;
            }

            Directory.CreateDirectory(Folder);

            Material material = existing;
            if (material == null)
            {
                material = new Material(font.material) { name = font.name + " " + preset };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(font.material);
            }

            if (preset == FontPreset.Outline)
            {
                material.EnableKeyword("OUTLINE_ON");
                material.SetColor("_OutlineColor", OutlineColor);
                material.SetFloat("_OutlineWidth", 0.22f);
                material.SetFloat("_OutlineSoftness", 0f);
            }
            else
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", ShadowColor);
                material.SetFloat("_UnderlayOffsetX", 0.6f);
                material.SetFloat("_UnderlayOffsetY", -0.6f);
                material.SetFloat("_UnderlayDilate", 0.2f);
                material.SetFloat("_UnderlaySoftness", 0.3f);
            }

            EditorUtility.SetDirty(material);
            return true;
        }
    }
}
