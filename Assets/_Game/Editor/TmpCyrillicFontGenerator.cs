using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// A static LiberationSans atlas with the Russian alphabet (including «ф» in «Конфеты» and «Ё» in «СЧЁТ»).
    /// It was the Cyrillic fallback before the game moved to Montserrat, which covers every character the UI
    /// prints. It is no longer wired in automatically and lives outside Resources, so it stays out of the
    /// build; BlockPuzzle/Build Size/Check Fonts adds it back as a fallback only when Montserrat misses a
    /// character the game uses.
    /// </summary>
    public static class TmpCyrillicFontGenerator
    {
        public const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        public const string AssetPath = "Assets/_Game/Fonts/LiberationSans-Cyrillic SDF.asset";
        public const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        /// <summary>Where the stock LiberationSans assets go when they leave the Resources folder.</summary>
        public const string DefaultFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans SDF.asset";

        /// <summary>Builds the asset when it is missing (or stale) and returns it.</summary>
        public static TMP_FontAsset EnsureAsset()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                Debug.LogError("[Block Puzzle] LiberationSans.ttf is missing at " + SourceFontPath);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (fontAsset == null || NeedsRebuild(fontAsset))
            {
                if (fontAsset != null)
                {
                    AssetDatabase.DeleteAsset(AssetPath);
                }

                fontAsset = CreateAsset(source);
            }

            return fontAsset;
        }

        private static TMP_FontAsset CreateAsset(Font source)
        {
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                42,
                5,
                GlyphRenderMode.SDFAA_HINTED,
                512,
                512,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                return null;
            }

            fontAsset.name = "LiberationSans-Cyrillic SDF";
            AssetDatabase.CreateAsset(fontAsset, AssetPath);

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            Texture2D[] atlases = fontAsset.atlasTextures;
            if (atlases != null)
            {
                for (int i = 0; i < atlases.Length; i++)
                {
                    if (atlases[i] == null)
                    {
                        continue;
                    }

                    atlases[i].name = fontAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlases[i], fontAsset);
                }
            }

            fontAsset.TryAddCharacters(CyrillicCharacters(), true);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
        }

        private static bool NeedsRebuild(TMP_FontAsset fontAsset)
        {
            return fontAsset.atlasPopulationMode != AtlasPopulationMode.Static
                   || fontAsset.atlasWidth > 512
                   || fontAsset.atlasHeight > 512
                   || !fontAsset.HasCharacter('\u0444', false, false)
                   || !fontAsset.HasCharacter('\u0401', false, false)
                   || !fontAsset.HasCharacter('\u00d7', false, false);
        }

        /// <summary>Russian alphabet plus punctuation used in the UI (СЧЁТ, Конфеты, 1–2, «—»).</summary>
        private static string CyrillicCharacters()
        {
            var text = new StringBuilder(80);
            for (int code = 0x0410; code <= 0x044F; code++)
            {
                text.Append((char)code);
            }

            text.Append('\u0401');
            text.Append('\u0451');
            text.Append('\u2013');
            text.Append('\u2014');
            text.Append('\u2026');
            text.Append('\u2116');
            text.Append('\u00AB');
            text.Append('\u00BB');
            text.Append('\u00D7');
            return text.ToString();
        }
    }
}
