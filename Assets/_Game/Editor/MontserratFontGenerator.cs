using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
    /// makes SemiBold the TMP default with no fallback fonts (Montserrat covers every character the game
    /// prints), and moves the text of the existing prefabs and the game scene onto the new fonts. Safe to run
    /// repeatedly. A rebake keeps the asset, its atlas texture and its material, so every material preset and
    /// every text that points at them stays attached.
    /// </summary>
    [InitializeOnLoad]
    public static class MontserratFontGenerator
    {
        public const string ExtraBoldSourcePath = "Assets/_Game/Fonts/Montserrat-ExtraBold.ttf";
        public const string SemiBoldSourcePath = "Assets/_Game/Fonts/Montserrat-SemiBold.ttf";
        public const string ExtraBoldAssetPath = "Assets/_Game/Resources/Fonts/Montserrat-ExtraBold SDF.asset";
        public const string SemiBoldAssetPath = "Assets/_Game/Resources/Fonts/Montserrat-SemiBold SDF.asset";

        private const string PrefabFolder = "Assets/_Game/Prefabs";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string ScriptsFolder = "Assets/_Game/Scripts";
        public const int AtlasSize = 1024;
        private const string SessionKey = "BlockPuzzle.MontserratFonts.Applied";

        /// <summary>Sampling point sizes tried in turn; the first one whose glyphs all fit one atlas wins.</summary>
        private static readonly int[] SamplingSizes = { 64, 60, 56 };

        /// <summary>
        /// SDF spread per point of the first bake (padding 9 at 84 pt). Keeping the ratio keeps the outline,
        /// shadow and dilate values of the materials at the same distance around the letters.
        /// </summary>
        private const float PaddingPerPoint = 9f / 84f;

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
            if (fontAsset != null && !NeedsRebuild(fontAsset, source))
            {
                return fontAsset;
            }

            if (fontAsset == null)
            {
                fontAsset = CreateEmpty(source, assetPath, assetName);
                if (fontAsset == null)
                {
                    return null;
                }
            }

            return Rebake(fontAsset, source) ? fontAsset : null;
        }

        /// <summary>A new, still empty static font asset with its material and atlas saved inside it.</summary>
        private static TMP_FontAsset CreateEmpty(Font source, string assetPath, string assetName)
        {
            int size = SamplingSizes[0];
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                source, size, PaddingFor(size), GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
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

            Texture2D atlas = fontAsset.atlasTexture;
            if (atlas != null)
            {
                atlas.name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        /// <summary>
        /// Bakes the characters of <see cref="RequiredCharacters"/> into a single <see cref="AtlasSize"/> atlas at
        /// the largest of <see cref="SamplingSizes"/> that fits, and writes the result into <paramref name="font"/>
        /// in place: the asset, its atlas texture object and its material stay the same objects, so the GUID and
        /// every reference (texts, the Outline/Shadow presets, MenuTextMaterial) survive. Returns false when no
        /// size fits.
        /// </summary>
        public static bool Rebake(TMP_FontAsset font, Font source)
        {
            string absent;
            string wanted = InFont(source, RequiredCharacters(), out absent);
            if (absent.Length > 0)
            {
                Debug.LogWarning("[Block Puzzle] " + font.name + ": the font file has no glyphs for " + Describe(absent)
                                 + "- they would draw as a missing glyph.");
            }

            foreach (int size in SamplingSizes)
            {
                int padding = PaddingFor(size);
                TMP_FontAsset baked = TMP_FontAsset.CreateFontAsset(
                    source, size, padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
                if (baked == null)
                {
                    return false;
                }

                string missing;
                baked.TryAddCharacters(wanted, out missing);
                bool fits = string.IsNullOrEmpty(missing);
                if (fits)
                {
                    CopyBake(baked, font, padding);
                }

                Object.DestroyImmediate(baked.atlasTexture);
                Object.DestroyImmediate(baked.material);
                Object.DestroyImmediate(baked);

                if (fits)
                {
                    Debug.Log("[Block Puzzle] " + font.name + ": " + wanted.Length + " glyphs baked into " + AtlasSize + "x"
                              + AtlasSize + " at " + size + " pt, padding " + padding + ".");
                    return true;
                }
            }

            Debug.LogError("[Block Puzzle] " + font.name + ": " + wanted.Length + " glyphs do not fit a " + AtlasSize + "x"
                           + AtlasSize + " atlas even at " + SamplingSizes[SamplingSizes.Length - 1] + " pt. The asset is unchanged.");
            return false;
        }

        private static int PaddingFor(int size)
        {
            return Mathf.RoundToInt(size * PaddingPerPoint);
        }

        /// <summary>Moves the glyph tables, face metrics and atlas pixels of <paramref name="baked"/> into <paramref name="target"/>.</summary>
        private static void CopyBake(TMP_FontAsset baked, TMP_FontAsset target, int padding)
        {
            var from = new SerializedObject(baked);
            var to = new SerializedObject(target);
            foreach (string property in new[]
                     {
                         "m_FaceInfo", "m_GlyphTable", "m_CharacterTable", "m_UsedGlyphRects", "m_FreeGlyphRects",
                         "m_AtlasWidth", "m_AtlasHeight", "m_AtlasPadding", "m_AtlasRenderMode", "m_FontFeatureTable"
                     })
            {
                to.CopyFromSerializedProperty(from.FindProperty(property));
            }

            to.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Static;
            to.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = false;
            to.FindProperty("m_AtlasTextureIndex").intValue = 0;
            to.ApplyModifiedPropertiesWithoutUndo();

            Texture2D atlas = target.atlasTexture;
            Texture2D bakedAtlas = baked.atlasTexture;
            atlas.Reinitialize(bakedAtlas.width, bakedAtlas.height, bakedAtlas.format, false);
            atlas.LoadRawTextureData(bakedAtlas.GetRawTextureData());
            atlas.Apply(false, false);
            EditorUtility.SetDirty(atlas);

            foreach (Material material in MaterialsOf(target))
            {
                material.SetFloat(ShaderUtilities.ID_TextureWidth, atlas.width);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, atlas.height);
                material.SetFloat(ShaderUtilities.ID_GradientScale, padding + 1);
                ShaderUtilities.UpdateShaderRatios(material);
                EditorUtility.SetDirty(material);
            }

            target.ReadFontAssetDefinition();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The font's own material plus every .mat in the project that samples its atlas.</summary>
        private static List<Material> MaterialsOf(TMP_FontAsset font)
        {
            var materials = new List<Material>();
            if (font.material != null)
            {
                materials.Add(font.material);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material != null && !materials.Contains(material) && material.HasProperty(ShaderUtilities.ID_MainTex)
                    && material.GetTexture(ShaderUtilities.ID_MainTex) == font.atlasTexture)
                {
                    materials.Add(material);
                }
            }

            return materials;
        }

        private static bool NeedsRebuild(TMP_FontAsset fontAsset, Font source)
        {
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Static
                || fontAsset.atlasWidth != AtlasSize
                || fontAsset.atlasHeight != AtlasSize
                || fontAsset.atlasTexture == null)
            {
                return true;
            }

            string absent;
            string wanted = InFont(source, RequiredCharacters(), out absent);
            foreach (char c in wanted)
            {
                if (!fontAsset.HasCharacter(c, false, false))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Latin, Cyrillic (with Ё/ё), digits, punctuation, the symbols of the first bake, plus every other
        /// character written in a string of the game code (GameLocalization and the rest of Assets/_Game/Scripts)
        /// or in a text of the game scene and prefabs, so a new symbol in a translation is never left out.
        /// </summary>
        public static string RequiredCharacters()
        {
            var set = new SortedSet<char>();
            for (int code = 0x20; code <= 0x7E; code++)
            {
                set.Add((char)code);
            }

            for (int code = 0x0410; code <= 0x044F; code++)
            {
                set.Add((char)code);
            }

            // Ё ё, ruble, multiplication, infinity, dashes, guillemets, numero, star, check mark, ellipsis, dot, degree.
            foreach (char c in "Ёё₽×∞—–«»№★✓…·°")
            {
                set.Add(c);
            }

            foreach (char c in UsedCharacters())
            {
                set.Add(c);
            }

            var text = new StringBuilder(set.Count);
            foreach (char c in set)
            {
                text.Append(c);
            }

            return text.ToString();
        }

        private static readonly Regex Literal = new Regex("@?\\$?\"(?:[^\"\\\\\\n]|\\\\.)*\"|'(?:[^'\\\\\\n]|\\\\.)'");
        private static readonly Regex Escape = new Regex("\\\\u([0-9A-Fa-f]{4})");

        /// <summary>Non-ASCII characters in the string literals of the game code and in the serialized texts.</summary>
        private static IEnumerable<char> UsedCharacters()
        {
            var found = new HashSet<char>();
            foreach (string file in Directory.GetFiles(ScriptsFolder, "*.cs", SearchOption.AllDirectories))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    if (line.TrimStart().StartsWith("//"))
                    {
                        continue;
                    }

                    foreach (Match match in Literal.Matches(line))
                    {
                        AddUnusual(Unescape(match.Value), found);
                    }
                }
            }

            var serialized = new List<string>(Directory.GetFiles(PrefabFolder, "*.prefab", SearchOption.AllDirectories));
            if (File.Exists(GameScenePath))
            {
                serialized.Add(GameScenePath);
            }

            foreach (string file in serialized)
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    if (line.Contains("m_text:"))
                    {
                        AddUnusual(Unescape(line), found);
                    }
                }
            }

            return found;
        }

        private static string Unescape(string text)
        {
            return Escape.Replace(text, m => ((char)System.Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        }

        private static void AddUnusual(string text, HashSet<char> found)
        {
            foreach (char c in text)
            {
                if (c > 0x7E && !char.IsSurrogate(c) && !char.IsControl(c))
                {
                    found.Add(c);
                }
            }
        }

        /// <summary>The characters of <paramref name="characters"/> that the font file has; the rest go to <paramref name="absent"/>.</summary>
        public static string InFont(Font source, string characters, out string absent)
        {
            var present = new StringBuilder(characters.Length);
            var missing = new StringBuilder();
            FontEngine.LoadFontFace(source);
            foreach (char c in characters)
            {
                uint index;
                if (FontEngine.TryGetGlyphIndex(c, out index) && index != 0)
                {
                    present.Append(c);
                }
                else
                {
                    missing.Append(c);
                }
            }

            FontEngine.UnloadFontFace();
            absent = missing.ToString();
            return present.ToString();
        }

        public static string Describe(string characters)
        {
            var text = new StringBuilder();
            foreach (char c in characters)
            {
                text.Append(c).Append(" (U+").Append(((int)c).ToString("X4")).Append(") ");
            }

            return text.ToString();
        }

        // ------------------------------------------------------------------ settings

        /// <summary>
        /// SemiBold becomes the TMP default. Fallback fonts are left out (every fallback in the lists goes into
        /// the build with its atlas), unless Montserrat misses a character the game prints that the
        /// LiberationSans Cyrillic atlas has: then that atlas is kept as the one fallback.
        /// </summary>
        public static void WireSettings(TMP_FontAsset heading, TMP_FontAsset body)
        {
            TMP_FontAsset cyrillic = CyrillicFallbackIfNeeded(heading, body);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpCyrillicFontGenerator.SettingsPath);
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                SerializedProperty defaultFont = serialized.FindProperty("m_defaultFontAsset");
                if (defaultFont != null)
                {
                    defaultFont.objectReferenceValue = body;
                }

                SetList(serialized.FindProperty("m_fallbackFontAssets"), cyrillic);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            foreach (TMP_FontAsset font in new[] { heading, body })
            {
                var serialized = new SerializedObject(font);
                SetList(serialized.FindProperty("m_FallbackFontAssetTable"), cyrillic);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(font);
            }
        }

        /// <summary>
        /// Characters the game prints (ASCII, the Russian alphabet with Ё/ё and every symbol found in the code
        /// and the texts) that either Montserrat asset lacks.
        /// </summary>
        public static string Uncovered(TMP_FontAsset heading, TMP_FontAsset body)
        {
            var needed = new SortedSet<char>(UsedCharacters());
            for (int code = 0x20; code <= 0x7E; code++)
            {
                needed.Add((char)code);
            }

            for (int code = 0x0410; code <= 0x044F; code++)
            {
                needed.Add((char)code);
            }

            needed.Add('Ё');
            needed.Add('ё');

            var missing = new StringBuilder();
            foreach (char c in needed)
            {
                if (!heading.HasCharacter(c, false, false) || !body.HasCharacter(c, false, false))
                {
                    missing.Append(c);
                }
            }

            return missing.ToString();
        }

        /// <summary>The LiberationSans Cyrillic atlas when it draws a character Montserrat lacks; null otherwise.</summary>
        public static TMP_FontAsset CyrillicFallbackIfNeeded(TMP_FontAsset heading, TMP_FontAsset body)
        {
            string uncovered = Uncovered(heading, body);
            if (uncovered.Length == 0)
            {
                Debug.Log("[Block Puzzle] Montserrat covers every character the game prints; no TMP fallback font is needed.");
                return null;
            }

            TMP_FontAsset cyrillic = TmpCyrillicFontGenerator.EnsureAsset();
            var helped = new StringBuilder();
            foreach (char c in uncovered)
            {
                if (cyrillic != null && cyrillic.HasCharacter(c, false, false))
                {
                    helped.Append(c);
                }
            }

            if (helped.Length == 0)
            {
                Debug.LogWarning("[Block Puzzle] Montserrat has no glyphs for " + Describe(uncovered)
                                 + "- no fallback draws them either, so none is kept.");
                return null;
            }

            Debug.LogWarning("[Block Puzzle] Montserrat has no glyphs for " + Describe(helped.ToString())
                             + "- LiberationSans-Cyrillic SDF stays as the TMP fallback.");
            return cyrillic;
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
