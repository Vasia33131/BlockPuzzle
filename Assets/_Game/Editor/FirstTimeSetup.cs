using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Runs in a fresh clone, and again whenever the generated scene falls behind the
    /// builder: it pulls in the TextMeshPro resources and bakes the game scene. Otherwise
    /// it stays out of the way, and everything remains re-runnable from the Tools menu.
    /// </summary>
    [InitializeOnLoad]
    public static class FirstTimeSetup
    {
        private const string SessionKey = "BlockPuzzle.FirstTimeSetup.Ran";
        private const double TimeoutSeconds = 300d;

        private static double deadline;

        static FirstTimeSetup()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (Application.isBatchMode ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                return;
            }

            // Rebuild whenever the baked scene lags behind SceneVersion, even if setup
            // already ran earlier in this editor session for an older stamp.
            if (BlockPuzzleSceneBuilder.IsSceneUpToDate)
            {
                return;
            }

            string attemptKey = $"{SessionKey}.{BlockPuzzleSceneBuilder.SceneVersion}";
            if (SessionState.GetBool(attemptKey, false))
            {
                return;
            }

            SessionState.SetBool(attemptKey, true);
            SessionState.SetBool(SessionKey, true);

            if (ProjectSetup.HasTextMeshProResources)
            {
                BuildScene();
                return;
            }

            Debug.Log("[Block Puzzle] Importing TextMeshPro resources, the game scene will be generated right after.");
            TMP_PackageResourceImporter.ImportResources(true, false, false);

            deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += WaitForTextMeshPro;
        }

        private static void WaitForTextMeshPro()
        {
            if (ProjectSetup.HasTextMeshProResources)
            {
                EditorApplication.update -= WaitForTextMeshPro;
                EditorApplication.delayCall += BuildScene;
                return;
            }

            if (EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= WaitForTextMeshPro;
                Debug.LogWarning(
                    "[Block Puzzle] TextMeshPro resources were not imported in time. " +
                    "Run Tools > Block Puzzle > Build Game Scene once they are available.");
            }
        }

        private static void BuildScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning(
                    "[Block Puzzle] The open scene has unsaved changes, so it was left alone. " +
                    "Use Tools > Block Puzzle > Build Game Scene when you are ready.");
                return;
            }

            BlockPuzzleSceneBuilder.BuildAll();
            EditorSceneManager.OpenScene(BlockPuzzleSceneBuilder.ScenePath, OpenSceneMode.Single);
            Debug.Log($"[Block Puzzle] Ready. Press Play in {BlockPuzzleSceneBuilder.ScenePath}");
        }
    }
}
