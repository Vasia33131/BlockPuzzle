using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using TMPro;
using Object = UnityEngine.Object;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Shrinks the WebGL build without changing how the game looks. Each step is a menu item under
    /// BlockPuzzle/Build Size and is safe to run again; "Apply Steps 1-5" runs them in order, "Build WebGL
    /// and Report" makes a build and writes the size report next to it.
    /// </summary>
    public static class BuildSizeOptimizer
    {
        private const string Menu = "BlockPuzzle/Build Size/";

        private const string RendererPath = "Assets/Settings/Renderer2D.asset";
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string GlobalSettingsPath = "Assets/UniversalRenderPipelineGlobalSettings.asset";
        private const string MusicFolder = "Assets/_Game/Audio/Music";
        private const string BuildFolder = "Builds/WebGL";
        private const string ReportPath = "Builds/WebGL-size-report.txt";

        private const string TmpResourceFonts = "Assets/TextMesh Pro/Resources/Fonts & Materials/";
        private const string TmpFonts = "Assets/TextMesh Pro/Fonts/";

        /// <summary>
        /// Everything in a Resources folder goes into the build whether it is used or not. These fonts are no
        /// longer used, so they move next to their source ttf (same GUIDs) and stay out of the build.
        /// </summary>
        private static readonly string[][] FontMoves =
        {
            new[] { TmpResourceFonts + "LiberationSans SDF.asset", TmpFonts + "LiberationSans SDF.asset" },
            new[] { TmpResourceFonts + "LiberationSans SDF - Fallback.asset", TmpFonts + "LiberationSans SDF - Fallback.asset" },
            new[] { TmpResourceFonts + "LiberationSans SDF - Outline.mat", TmpFonts + "LiberationSans SDF - Outline.mat" },
            new[] { TmpResourceFonts + "LiberationSans SDF - Drop Shadow.mat", TmpFonts + "LiberationSans SDF - Drop Shadow.mat" },
            new[] { "Assets/_Game/Resources/Fonts/LiberationSans-Cyrillic SDF.asset", TmpCyrillicFontGenerator.AssetPath }
        };

        // ------------------------------------------------------------------ all

        [MenuItem(Menu + "Apply Steps 1-5", priority = 1)]
        public static void ApplyAll()
        {
            Fonts();
            RenderPipeline();
            Music();
            UIArt();
            PlayerSettingsForWebGL();
            AssetDatabase.SaveAssets();
            Debug.Log("[Build Size] Steps 1-5 applied. Next: BlockPuzzle/Build Size/Build WebGL and Report.");
        }

        // ------------------------------------------------------------------ 1. fonts

        /// <summary>
        /// Rebakes both Montserrat atlases at 1024x1024 in place (same asset, atlas object and material, so the
        /// Outline/Shadow presets and MenuTextMaterial stay attached), takes the LiberationSans fonts out of
        /// Resources and leaves TMP without fallback fonts when Montserrat covers every character.
        /// </summary>
        [MenuItem(Menu + "1. Fonts: Rebake Montserrat 1024, Drop LiberationSans", priority = 20)]
        public static void Fonts()
        {
            MoveFontsOutOfResources();

            var heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MontserratFontGenerator.ExtraBoldAssetPath);
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MontserratFontGenerator.SemiBoldAssetPath);
            var headingSource = AssetDatabase.LoadAssetAtPath<Font>(MontserratFontGenerator.ExtraBoldSourcePath);
            var bodySource = AssetDatabase.LoadAssetAtPath<Font>(MontserratFontGenerator.SemiBoldSourcePath);
            if (heading == null || body == null || headingSource == null || bodySource == null)
            {
                Debug.LogError("[Build Size] Montserrat font assets or ttf files are missing; run Tools/Block Puzzle/Ensure Montserrat TMP Fonts first.");
                return;
            }

            bool baked = MontserratFontGenerator.Rebake(heading, headingSource);
            baked &= MontserratFontGenerator.Rebake(body, bodySource);
            MontserratFontGenerator.WireSettings(heading, body);
            AssetDatabase.SaveAssets();

            if (!baked)
            {
                Debug.LogError("[Build Size] A Montserrat atlas was not rebaked; see the errors above.");
            }

            CheckFonts();
        }

        private static void MoveFontsOutOfResources()
        {
            foreach (string[] move in FontMoves)
            {
                string from = move[0];
                string to = move[1];
                if (AssetDatabase.LoadMainAssetAtPath(from) == null)
                {
                    continue;
                }

                if (AssetDatabase.LoadMainAssetAtPath(to) != null)
                {
                    Debug.LogWarning("[Build Size] Not moved, " + to + " already exists: " + from);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(to));
                string error = AssetDatabase.MoveAsset(from, to);
                if (string.IsNullOrEmpty(error))
                {
                    Debug.Log("[Build Size] Moved out of Resources: " + from + " -> " + to);
                }
                else
                {
                    Debug.LogError("[Build Size] Could not move " + from + ": " + error);
                }
            }
        }

        /// <summary>Atlas sizes, glyph coverage, fallbacks, material links and stray LiberationSans references.</summary>
        [MenuItem(Menu + "Check Fonts and References", priority = 21)]
        public static void CheckFonts()
        {
            var report = new StringBuilder("[Build Size] Font check\n");
            int problems = 0;

            var heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MontserratFontGenerator.ExtraBoldAssetPath);
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MontserratFontGenerator.SemiBoldAssetPath);
            foreach (TMP_FontAsset font in new[] { heading, body })
            {
                if (font == null)
                {
                    report.AppendLine("  MISSING Montserrat font asset");
                    problems++;
                    continue;
                }

                report.AppendLine("  " + font.name + ": " + font.atlasWidth + "x" + font.atlasHeight + ", " + font.faceInfo.pointSize
                                  + " pt, padding " + font.atlasPadding + ", " + font.characterTable.Count + " glyphs, "
                                  + font.atlasPopulationMode + ", fallbacks: " + Names(font.fallbackFontAssetTable));
                if (font.atlasWidth != MontserratFontGenerator.AtlasSize || font.atlasHeight != MontserratFontGenerator.AtlasSize)
                {
                    problems++;
                }
            }

            if (heading != null && body != null)
            {
                string uncovered = MontserratFontGenerator.Uncovered(heading, body);
                report.AppendLine("  Characters the game prints that Montserrat lacks: "
                                  + (uncovered.Length == 0 ? "none" : MontserratFontGenerator.Describe(uncovered)));

                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Game/Resources/Fonts" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    Texture texture = material.HasProperty(ShaderUtilities.ID_MainTex) ? material.GetTexture(ShaderUtilities.ID_MainTex) : null;
                    bool linked = texture != null && (texture == heading.atlasTexture || texture == body.atlasTexture);
                    report.AppendLine("  " + (linked ? "ok      " : "BROKEN  ") + Path.GetFileName(path) + " -> "
                                      + (texture != null ? texture.name + " " + texture.width + "x" + texture.height : "no texture"));
                    if (!linked)
                    {
                        problems++;
                    }
                }
            }

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpCyrillicFontGenerator.SettingsPath);
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                Object defaultFont = serialized.FindProperty("m_defaultFontAsset").objectReferenceValue;
                SerializedProperty fallbacks = serialized.FindProperty("m_fallbackFontAssets");
                var names = new List<string>();
                for (int i = 0; i < fallbacks.arraySize; i++)
                {
                    Object value = fallbacks.GetArrayElementAtIndex(i).objectReferenceValue;
                    names.Add(value != null ? value.name : "<none>");
                }

                report.AppendLine("  TMP Settings: default " + (defaultFont != null ? defaultFont.name : "<none>")
                                  + ", fallbacks: " + (names.Count == 0 ? "none" : string.Join(", ", names)));
            }

            List<string> liberation = BuildDependencies().Where(p => p.Contains("LiberationSans")).ToList();
            report.AppendLine("  LiberationSans in the build (build scenes + Resources + their dependencies): "
                              + (liberation.Count == 0 ? "none" : string.Join(", ", liberation)));
            problems += liberation.Count(p => !p.Contains("Cyrillic"));

            if (problems == 0)
            {
                Debug.Log(report.ToString());
            }
            else
            {
                Debug.LogWarning(report + "  " + problems + " problem(s).");
            }
        }

        private static string Names(List<TMP_FontAsset> fonts)
        {
            return fonts == null || fonts.Count == 0 ? "none" : string.Join(", ", fonts.Select(f => f != null ? f.name : "<none>"));
        }

        /// <summary>Every asset the player build takes: the enabled scenes, all of Resources and what they reference.</summary>
        private static IEnumerable<string> BuildDependencies()
        {
            var roots = new List<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (path.Contains("/Resources/") && !path.Contains("/Editor/") && !AssetDatabase.IsValidFolder(path))
                {
                    roots.Add(path);
                }
            }

            return AssetDatabase.GetDependencies(roots.ToArray(), true);
        }

        // ------------------------------------------------------------------ 2. render pipeline

        /// <summary>
        /// The game has no Volume, no Light2D and post-processing is off on the camera, yet the renderer's post
        /// data drags the film grain textures, SMAA tables and every post shader into the build. HDR, MSAA and
        /// the depth/opaque copies are off too (nothing reads them); variant stripping is switched on.
        /// </summary>
        [MenuItem(Menu + "2. URP: Remove Post-processing, HDR and MSAA", priority = 22)]
        public static void RenderPipeline()
        {
            Edit(RendererPath, so => Set(so, "m_PostProcessData", p => p.objectReferenceValue = null));
            Edit(PipelinePath, so =>
            {
                Set(so, "m_SupportsHDR", p => p.boolValue = false);
                Set(so, "m_MSAA", p => p.intValue = 1);
                Set(so, "m_RequireDepthTexture", p => p.boolValue = false);
                Set(so, "m_RequireOpaqueTexture", p => p.boolValue = false);
                Set(so, "m_SupportsTerrainHoles", p => p.boolValue = false);
                Set(so, "m_SupportDataDrivenLensFlare", p => p.boolValue = false);
                Set(so, "m_UseAdaptivePerformance", p => p.boolValue = false);
            });
            Edit(GlobalSettingsPath, so =>
            {
                Set(so, "m_StripDebugVariants", p => p.boolValue = true);
                Set(so, "m_StripUnusedPostProcessingVariants", p => p.boolValue = true);
                Set(so, "m_StripUnusedVariants", p => p.boolValue = true);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[Build Size] URP: post-processing data removed from the 2D renderer; HDR, MSAA, depth and opaque textures off.");
        }

        private static void Edit(string path, Action<SerializedObject> change)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                Debug.LogError("[Build Size] Missing " + path);
                return;
            }

            var serialized = new SerializedObject(asset);
            change(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void Set(SerializedObject serialized, string name, Action<SerializedProperty> change)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("[Build Size] " + serialized.targetObject.name + " has no " + name);
                return;
            }

            change(property);
        }

        // ------------------------------------------------------------------ 3. music

        /// <summary>Reimports the music with the settings of AudioImportPostprocessor (Vorbis quality 30 %).</summary>
        [MenuItem(Menu + "3. Audio: Reimport Music", priority = 23)]
        public static void Music()
        {
            Reimport("t:AudioClip", MusicFolder);
        }

        // ------------------------------------------------------------------ 4. ui art

        /// <summary>Reimports Resources/UI/Art with the settings of UIArtImportPostprocessor (compressed, no mipmaps).</summary>
        [MenuItem(Menu + "4. UI Art: Reimport Compressed", priority = 24)]
        public static void UIArt()
        {
            Reimport("t:Texture2D", UIArtImportPostprocessor.Folder.TrimEnd('/'));
        }

        private static void Reimport(string filter, string folder)
        {
            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
            foreach (string guid in guids)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }

            Debug.Log("[Build Size] Reimported " + guids.Length + " file(s) in " + folder);
        }

        // ------------------------------------------------------------------ 5. player settings

        /// <summary>
        /// High managed stripping (link.xml in Assets/_Game keeps the Yandex plugin and our assemblies), exceptions
        /// only where they are thrown, IL2CPP code generation for size, Brotli, engine code stripping, no
        /// development build.
        /// </summary>
        [MenuItem(Menu + "5. WebGL Player Settings", priority = 25)]
        public static void PlayerSettingsForWebGL()
        {
            NamedBuildTarget webGL = NamedBuildTarget.WebGL;
            PlayerSettings.SetManagedStrippingLevel(webGL, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(webGL, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.stripEngineCode = true;
            EditorUserBuildSettings.development = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Build Size] WebGL: stripping High, IL2CPP Optimize Size, explicitly thrown exceptions only, Brotli, strip engine code.");
        }

        // ------------------------------------------------------------------ 6. build and report

        /// <summary>
        /// Builds the enabled scenes for WebGL into Builds/WebGL and writes Builds/WebGL-size-report.txt: the
        /// "Build Report" block Unity prints to Editor.log, the size of every file in Build/ and the biggest assets.
        /// </summary>
        [MenuItem(Menu + "Build WebGL and Report", priority = 40)]
        public static void BuildAndReport()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL
                && !EditorUtility.DisplayDialog("Build Size", "The active platform is not WebGL. Switch and build?", "Build", "Cancel"))
            {
                return;
            }

            long logStart = LogLength();
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = BuildFolder,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            string text = Describe(report, logStart);
            File.WriteAllText(ReportPath, text);
            Debug.Log("[Build Size] " + report.summary.result + ". Report: " + Path.GetFullPath(ReportPath) + "\n" + text);
        }

        private static string Describe(BuildReport report, long logStart)
        {
            var text = new StringBuilder();
            text.AppendLine("WebGL build, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ", Unity " + Application.unityVersion
                            + ", result " + report.summary.result);
            text.AppendLine();

            text.AppendLine("Files in " + BuildFolder + "/Build:");
            string buildDir = Path.Combine(BuildFolder, "Build");
            long total = 0;
            if (Directory.Exists(buildDir))
            {
                foreach (string file in Directory.GetFiles(buildDir).OrderByDescending(f => new FileInfo(f).Length))
                {
                    long size = new FileInfo(file).Length;
                    total += size;
                    text.AppendLine("  " + Mb(size).PadLeft(10) + "  " + Path.GetFileName(file));
                }
            }

            text.AppendLine("  " + Mb(total).PadLeft(10) + "  total");
            text.AppendLine();

            var assets = report.packedAssets.SelectMany(p => p.contents).ToList();
            text.AppendLine("Packed assets by type (uncompressed, before Brotli):");
            foreach (var group in assets.GroupBy(a => a.type != null ? a.type.Name : "?").OrderByDescending(g => g.Sum(a => (long)a.packedSize)))
            {
                text.AppendLine("  " + Mb(group.Sum(a => (long)a.packedSize)).PadLeft(10) + "  " + group.Key);
            }

            text.AppendLine();
            text.AppendLine("Biggest assets:");
            foreach (var group in assets.GroupBy(a => a.sourceAssetPath).OrderByDescending(g => g.Sum(a => (long)a.packedSize)).Take(40))
            {
                text.AppendLine("  " + Mb(group.Sum(a => (long)a.packedSize)).PadLeft(10) + "  " + group.Key);
            }

            text.AppendLine();
            text.AppendLine(BuildReportBlock(logStart));
            return text.ToString();
        }

        private static string Mb(long bytes)
        {
            return (bytes / 1048576.0).ToString("0.000") + " MB";
        }

        private static long LogLength()
        {
            string log = Application.consoleLogPath;
            return string.IsNullOrEmpty(log) || !File.Exists(log) ? 0 : new FileInfo(log).Length;
        }

        /// <summary>The last "Build Report" block written to Editor.log since <paramref name="start"/>.</summary>
        private static string BuildReportBlock(long start)
        {
            string log = Application.consoleLogPath;
            if (string.IsNullOrEmpty(log) || !File.Exists(log))
            {
                return "Editor.log not found.";
            }

            string tail;
            using (var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(Math.Min(start, stream.Length), SeekOrigin.Begin);
                using (var reader = new StreamReader(stream))
                {
                    tail = reader.ReadToEnd();
                }
            }

            int at = tail.LastIndexOf("Build Report", StringComparison.Ordinal);
            if (at < 0)
            {
                return "No \"Build Report\" block in Editor.log (the build may have failed).";
            }

            var block = new StringBuilder();
            foreach (string line in tail.Substring(at).Split('\n').Take(120))
            {
                string trimmed = line.TrimEnd('\r');
                if (block.Length > 0 && trimmed.StartsWith("-----"))
                {
                    break;
                }

                block.AppendLine(trimmed);
            }

            return block.ToString();
        }
    }
}
