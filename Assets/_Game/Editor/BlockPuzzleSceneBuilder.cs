using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BlockPuzzle.Bootstrap;
using BlockPuzzle.Core;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// One-click generator of the playable portrait scene. Everything the game needs
    /// (sprite, shape library, hierarchy, build settings) is produced from code.
    /// </summary>
    public static class BlockPuzzleSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Game.unity";
        public const string AndroidPackageName = "com.yourcompany.blockpuzzle";

        /// <summary>
        /// Bumped whenever the generated hierarchy changes. The scene is an asset, so a new
        /// pause button or effect layer would never reach a project that already has a scene
        /// on disk — this stamp is what tells the setup to bake it again.
        /// </summary>
        public const int SceneVersion = 9;

        private static string VersionKey => $"BlockPuzzle.SceneVersion.{Application.dataPath.GetHashCode():X}";

        /// <summary>True when the scene on disk was baked by this version of the builder.</summary>
        public static bool IsSceneUpToDate =>
            File.Exists(ScenePath) && EditorPrefs.GetInt(VersionKey, 0) == SceneVersion;

        [MenuItem("Tools/Block Puzzle/Build Game Scene", priority = 0)]
        public static void BuildGameSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildGameScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log($"[Block Puzzle] Scene generated at {ScenePath}");
        }

        /// <summary>Batch-mode entry point: builds every asset and configures the player.</summary>
        public static void BuildAll()
        {
            BuildGameScene();
            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Block Puzzle] Project setup finished.");
        }

        /// <summary>Batch-mode entry: full setup then Android APK build.</summary>
        public static void BuildAndroid()
        {
            BuildAll();

            string outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android"));
            Directory.CreateDirectory(outputDir);
            string apkPath = Path.Combine(outputDir, "BlockPuzzle.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception($"Android build failed: {report.summary.result}");
            }

            Debug.Log($"[Block Puzzle] Android APK built at {apkPath}");
        }

        public static void BuildGameScene()
        {
            RoundedSpriteGenerator.EnsureAsset();
            UIFactory.ClearCache();

            ShapeLibrary library = ShapeLibraryGenerator.EnsureAsset();
            PrefabGenerator.RegenerateOverlayPanels();
            PrefabGenerator.PrefabAssets prefabs = PrefabGenerator.EnsureAll();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var prefabSet = new GameSceneFactory.PrefabSet
            {
                GridCell = prefabs.GridCell,
                BlockPiece = prefabs.BlockPiece,
                GameOverPanel = prefabs.GameOverPanel,
                PausePanel = prefabs.PausePanel,
                Spark = prefabs.Spark
            };

            GameSceneFactory.BuildResult result = GameSceneFactory.Build(library, prefabSet);
            WireSparkPrefab(result, prefabs.Spark);
            AttachBootstrap(result, library);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorPrefs.SetInt(VersionKey, SceneVersion);
            RegisterSceneInBuildSettings();
        }

        private static void WireSparkPrefab(GameSceneFactory.BuildResult result, Image spark)
        {
            if (result?.GridManager?.Sparks == null || spark == null)
            {
                return;
            }

            result.GridManager.Sparks.SetPrefab(spark);

            var serialized = new SerializedObject(result.GridManager.Sparks);
            serialized.FindProperty("sparkPrefab").objectReferenceValue = spark;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(result.GridManager.Sparks);
        }

        private static void AttachBootstrap(GameSceneFactory.BuildResult result, ShapeLibrary library)
        {
            if (result.GameManager == null)
            {
                return;
            }

            var bootstrap = result.GameManager.gameObject.AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("shapeLibrary").objectReferenceValue = library;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath)
                {
                    scenes.Add(new EditorBuildSettingsScene(existing.path, false));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Tools/Block Puzzle/Apply Android Player Settings", priority = 20)]
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "YourCompany";
            PlayerSettings.productName = "Block Puzzle";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.defaultScreenWidth = (int)GameTheme.ReferenceWidth;
            PlayerSettings.defaultScreenHeight = (int)GameTheme.ReferenceHeight;
            PlayerSettings.runInBackground = false;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageName);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

            Texture2D icon = AppIconGenerator.EnsureIcon();
            AppIconGenerator.AssignAndroidIcon(icon);

            Debug.Log(
                $"[Block Puzzle] Android player settings applied: {AndroidPackageName}, " +
                "min API 24, target API 33, icon assigned.");
        }
    }
}
