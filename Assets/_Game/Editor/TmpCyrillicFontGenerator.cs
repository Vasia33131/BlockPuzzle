using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// LiberationSans SDF ships without Cyrillic. This bakes a static fallback atlas
    /// with the Russian alphabet (including «ф» in «Конфеты» and «Ё» in «СЧЁТ») and wires it
    /// into TMP Settings plus the default font's fallback table.
    /// </summary>
    [InitializeOnLoad]
    public static class TmpCyrillicFontGenerator
    {
        public const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        public const string AssetPath = "Assets/_Game/Resources/Fonts/LiberationSans-Cyrillic SDF.asset";
        public const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        public const string DefaultFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private const string SessionKey = "BlockPuzzle.TmpCyrillicFont.Static512";

        static TmpCyrillicFontGenerator()
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

            bool missing = !File.Exists(AssetPath);
            if (!missing && SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EnsureAsset();
        }

        [MenuItem("Tools/Block Puzzle/Ensure Cyrillic TMP Font", priority = 41)]
        public static void EnsureFromMenu()
        {
            TMP_FontAsset font = EnsureAsset();
            if (font != null)
            {
                Debug.Log("[Block Puzzle] Cyrillic TMP fallback is ready: " + AssetPath);
            }
        }

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

            WireFallbacks(fontAsset);
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
                   || !fontAsset.HasCharacter('\u0401', false, false);
        }

        private static void WireFallbacks(TMP_FontAsset cyrillic)
        {
            if (cyrillic == null)
            {
                return;
            }

            AppendFallback(
                AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath),
                "m_fallbackFontAssets",
                cyrillic);

            AppendFallback(
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath),
                "m_FallbackFontAssetTable",
                cyrillic);
        }

        private static void AppendFallback(Object host, string propertyName, TMP_FontAsset fallback)
        {
            if (host == null || fallback == null)
            {
                return;
            }

            var serialized = new SerializedObject(host);
            SerializedProperty list = serialized.FindProperty(propertyName);
            if (list == null || !list.isArray)
            {
                return;
            }

            bool alreadyPresent = false;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                Object value = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (value == null)
                {
                    list.DeleteArrayElementAtIndex(i);
                    continue;
                }

                if (value == fallback)
                {
                    alreadyPresent = true;
                }
            }

            if (!alreadyPresent)
            {
                int index = list.arraySize;
                list.arraySize = index + 1;
                list.GetArrayElementAtIndex(index).objectReferenceValue = fallback;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(host);
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
            return text.ToString();
        }
    }
}
