using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using TMPro;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Bakes the two static Montserrat TMP font assets (ExtraBold for headings, SemiBold for body text),
    /// makes SemiBold the TMP default, keeps LiberationSans only as a fallback, and moves the text of the
    /// existing prefabs and the game scene onto the new fonts. Safe to run repeatedly.
    /// </summary>
    [InitializeOnLoad]
    public static class MontserratFontGenerator
    {
        public const string ExtraBoldSourcePath = "Assets/_Game/Fonts/Montserrat-ExtraBold.ttf";
        public const string SemiBoldSourcePath = "Assets/_Game/Fonts/Montserrat-SemiBold.ttf";
        public const string ExtraBoldAssetPath = "Assets/_Game/Resources/Fonts/Montserrat-ExtraBold SDF.asset";
        public const string SemiBoldAssetPath = "Assets/_Game/Resources/Fonts/Montserrat-SemiBold SDF.asset";
        public const string LiberationAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private const string PrefabFolder = "Assets/_Game/Prefabs";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const int AtlasSize = 2048;
        private const int Padding = 9;
        private const int SamplingSize = 84;
        private const string SessionKey = "BlockPuzzle.MontserratFonts.Applied";

        static MontserratFontGenerator()
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

            bool missing = !File.Exists(ExtraBoldAssetPath) || !File.Exists(SemiBoldAssetPath);
            if (!missing || SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EnsureAndApply();
        }

        [MenuItem("Tools/Block Puzzle/Ensure Montserrat TMP Fonts", priority = 43)]
        public static void EnsureFromMenu()
        {
            EnsureAndApply();
        }

        /// <summary>Builds the fonts, TMP Settings and the menu material, without touching prefabs or scenes.</summary>
        public static bool EnsureFonts()
        {
            TMP_FontAsset heading;
            TMP_FontAsset body;
            if (!Ensure(out heading, out body))
            {
                return false;
            }

            WireSettings(heading, body);
            MenuTextMaterialGenerator.Ensure(false);
            return true;
        }

        /// <summary>Builds the fonts, wires TMP Settings and migrates prefabs and the game scene.</summary>
        public static void EnsureAndApply()
        {
            TMP_FontAsset heading;
            TMP_FontAsset body;
            if (!Ensure(out heading, out body))
            {
                return;
            }

            WireSettings(heading, body);
            MenuTextMaterialGenerator.Ensure(true);
            int changed = MigratePrefabs(heading, body) + MigrateGameScene(heading, body);
            AssetDatabase.SaveAssets();
            Debug.Log("[Block Puzzle] Montserrat fonts are ready. Texts moved to Montserrat: " + changed);
        }

        /// <summary>Creates (or rebuilds) both font assets. Returns false when a source ttf is missing.</summary>
        public static bool Ensure(out TMP_FontAsset heading, out TMP_FontAsset body)
        {
            TmpCyrillicFontGenerator.EnsureAsset();
            heading = EnsureFont(ExtraBoldSourcePath, ExtraBoldAssetPath, "Montserrat-ExtraBold SDF");
            body = EnsureFont(SemiBoldSourcePath, SemiBoldAssetPath, "Montserrat-SemiBold SDF");
            return heading != null && body != null;
        }

        private static TMP_FontAsset EnsureFont(string sourcePath, string assetPath, string assetName)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogError("[Block Puzzle] Font file is missing: " + sourcePath);
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset != null && !NeedsRebuild(fontAsset))
            {
                return fontAsset;
            }

            if (fontAsset != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            fontAsset = TMP_FontAsset.CreateFontAsset(
                source,
                SamplingSize,
                Padding,
                GlyphRenderMode.SDFAA,
                AtlasSize,
                AtlasSize,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                Debug.LogError("[Block Puzzle] Could not bake " + assetName);
                return null;
            }

            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            if (fontAsset.material != null)
            {
                fontAsset.material.name = assetName + " Material";
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

                    atlases[i].name = assetName + " Atlas" + (i == 0 ? string.Empty : " " + i);
                    AssetDatabase.AddObjectToAsset(atlases[i], fontAsset);
                }
            }

            string missing;
            fontAsset.TryAddCharacters(Characters(), out missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning("[Block Puzzle] " + assetName + " has no glyphs for: " + Describe(missing)
                                 + " (they fall back to LiberationSans or draw nothing).");
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        }

        private static bool NeedsRebuild(TMP_FontAsset fontAsset)
        {
            return fontAsset.atlasPopulationMode != AtlasPopulationMode.Static
                   || fontAsset.atlasWidth != AtlasSize
                   || !fontAsset.HasCharacter('ф', false, false)
                   || !fontAsset.HasCharacter('Ё', false, false)
                   || !fontAsset.HasCharacter('×', false, false);
        }

        /// <summary>Latin, Cyrillic (with Ё/ё), digits, punctuation and the symbols the UI prints.</summary>
        private static string Characters()
        {
            var text = new StringBuilder(256);
            for (int code = 0x20; code <= 0x7E; code++)
            {
                text.Append((char)code);
            }

            for (int code = 0x0410; code <= 0x044F; code++)
            {
                text.Append((char)code);
            }

            // Ё ё, ruble, multiplication, infinity, dashes, guillemets, numero, star, check mark, ellipsis, dot, degree.
            text.Append("Ёё₽×∞—–«»№★✓…·°");
            return text.ToString();
        }

        private static string Describe(string characters)
        {
            var text = new StringBuilder();
            foreach (char c in characters)
            {
                text.Append(c).Append(" (U+").Append(((int)c).ToString("X4")).Append(") ");
            }

            return text.ToString();
        }

        // ------------------------------------------------------------------ settings

        private static void WireSettings(TMP_FontAsset heading, TMP_FontAsset body)
        {
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationAssetPath);
            var cyrillic = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpCyrillicFontGenerator.AssetPath);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpCyrillicFontGenerator.SettingsPath);
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                SerializedProperty defaultFont = serialized.FindProperty("m_defaultFontAsset");
                if (defaultFont != null)
                {
                    defaultFont.objectReferenceValue = body;
                }

                SetList(serialized.FindProperty("m_fallbackFontAssets"), liberation, cyrillic);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            foreach (TMP_FontAsset font in new[] { heading, body })
            {
                var serialized = new SerializedObject(font);
                SetList(serialized.FindProperty("m_FallbackFontAssetTable"), liberation, cyrillic);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(font);
            }
        }

        private static void SetList(SerializedProperty list, params TMP_FontAsset[] fonts)
        {
            if (list == null || !list.isArray)
            {
                return;
            }

            var values = new List<TMP_FontAsset>();
            foreach (TMP_FontAsset font in fonts)
            {
                if (font != null && !values.Contains(font))
                {
                    values.Add(font);
                }
            }

            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // ------------------------------------------------------------------ migration

        private static int MigratePrefabs(TMP_FontAsset heading, TMP_FontAsset body)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                int count = 0;
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (Migrate(text, heading, body))
                    {
                        count++;
                    }
                }

                if (count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed += count;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }

        private static int MigrateGameScene(TMP_FontAsset heading, TMP_FontAsset body)
        {
            if (!File.Exists(GameScenePath))
            {
                return 0;
            }

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            int changed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (Migrate(text, heading, body))
                    {
                        changed++;
                    }
                }
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            EditorSceneManager.CloseScene(scene, true);
            return changed;
        }

        /// <summary>Bold or outlined-menu text becomes a heading, the rest body text. Returns true when it changed.</summary>
        private static bool Migrate(TMP_Text text, TMP_FontAsset heading, TMP_FontAsset body)
        {
            bool bold = (text.fontStyle & FontStyles.Bold) != 0;
            Material current = text.fontSharedMaterial;
            bool menu = current != null && current.name == "MenuTextMaterial";

            TMP_FontAsset target = text.font;
            if (!IsMontserrat(target))
            {
                target = bold || menu ? heading : body;
            }

            bool changed = false;
            if (text.font != target)
            {
                text.font = target;
                changed = true;
            }

            if (bold)
            {
                text.fontStyle &= ~FontStyles.Bold;
                changed = true;
            }

            if (menu)
            {
                var menuMaterial = AssetDatabase.LoadAssetAtPath<Material>(MenuTextMaterialGenerator.AssetPath);
                if (menuMaterial != null && target == heading)
                {
                    text.fontSharedMaterial = menuMaterial;
                }
            }
            else if (current != null && target.material != null && current.mainTexture != target.atlasTexture)
            {
                if (!current.name.StartsWith("Liberation") && !current.name.StartsWith("Montserrat"))
                {
                    Debug.LogWarning("[Block Puzzle] Custom text material '" + current.name + "' on " + PathOf(text) + " was reset.");
                }

                text.fontSharedMaterial = target.material;
                changed = true;
            }

            changed |= DropStaleMaterialInstance(text);
            changed |= AllowShrink(text);

            if (changed)
            {
                EditorUtility.SetDirty(text);
            }

            return changed;
        }

        /// <summary>
        /// A scene can hold a serialized material instance (outlined badges) made from the old font's atlas.
        /// TMP would keep using it next to the new font and fail on the first outline change, so it is cleared.
        /// </summary>
        private static bool DropStaleMaterialInstance(TMP_Text text)
        {
            var serialized = new SerializedObject(text);
            SerializedProperty instance = serialized.FindProperty("m_fontMaterial");
            if (instance == null || instance.objectReferenceValue == null)
            {
                return false;
            }

            var material = (Material)instance.objectReferenceValue;
            if (text.font != null && material.mainTexture == text.font.atlasTexture)
            {
                return false;
            }

            instance.objectReferenceValue = null;
            SerializedProperty materials = serialized.FindProperty("m_fontMaterials");
            if (materials != null && materials.isArray)
            {
                materials.arraySize = 0;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// Montserrat is wider than LiberationSans, so a label that used to fit may now run out of its box.
        /// Lets it shrink down to 60% of its size, but only for boxes that are tall enough for the text: a
        /// label that overflows on purpose (or wraps over several lines) is left as it is.
        /// </summary>
        private static bool AllowShrink(TMP_Text text)
        {
            if (text.enableAutoSizing || text.GetComponent<ContentSizeFitter>() != null)
            {
                return false;
            }

            Rect rect = text.rectTransform.rect;
            if (rect.width <= 8f || rect.height <= 8f)
            {
                return false;
            }

            float needed = string.IsNullOrEmpty(text.text)
                ? text.fontSize * 1.1f
                : text.GetPreferredValues(text.text, rect.width, 0f).y;
            if (needed > rect.height + 1f)
            {
                return false;
            }

            float size = text.fontSize;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.6f;
            return true;
        }

        private static bool IsMontserrat(TMP_FontAsset font)
        {
            return font != null && font.name.StartsWith("Montserrat");
        }

        // ------------------------------------------------------------------ check

        [MenuItem("Tools/Block Puzzle/Check Fonts", priority = 44)]
        public static void CheckFonts()
        {
            int bad = 0;
            int total = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    total++;
                    bad += Report(text, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }

            if (File.Exists(GameScenePath))
            {
                bool wasLoaded = SceneManager.GetSceneByPath(GameScenePath).isLoaded;
                Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        total++;
                        bad += Report(text, GameScenePath);
                    }
                }

                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (bad == 0)
            {
                Debug.Log("[Block Puzzle] Check Fonts: all " + total + " TMP texts use Montserrat.");
            }
            else
            {
                Debug.LogWarning("[Block Puzzle] Check Fonts: " + bad + " of " + total
                                 + " TMP texts are not Montserrat. Run Tools/Block Puzzle/Ensure Montserrat TMP Fonts.");
            }
        }

        private static int Report(TMP_Text text, string container)
        {
            if (IsMontserrat(text.font))
            {
                return 0;
            }

            string fontName = text.font != null ? text.font.name : "<none>";
            Debug.LogWarning("[Block Puzzle] Not Montserrat (" + fontName + "): " + container + " > " + PathOf(text), text);
            return 1;
        }

        private static string PathOf(Component component)
        {
            var names = new List<string>();
            for (Transform t = component.transform; t != null; t = t.parent)
            {
                names.Add(t.name);
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
