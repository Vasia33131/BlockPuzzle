using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Editor helpers that prepare a freshly cloned project: importing the TextMeshPro
    /// runtime resources and generating the game scene. Both are exposed as menu items
    /// and as batch-mode entry points.
    /// </summary>
    public static class ProjectSetup
    {
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const double ImportTimeoutSeconds = 300d;

        private static double importDeadline;

        public static bool HasTextMeshProResources => File.Exists(TmpSettingsPath);

        [MenuItem("Tools/Block Puzzle/Import TextMeshPro Essentials", priority = 40)]
        public static void ImportTextMeshProEssentialsMenu()
        {
            if (HasTextMeshProResources)
            {
                TmpCyrillicFontGenerator.EnsureAsset();
                Debug.Log("[Block Puzzle] TextMeshPro resources are already present.");
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        /// <summary>
        /// Batch-mode entry point. Package import is asynchronous, so the editor loop is
        /// polled until the resources appear and only then the process exits.
        /// </summary>
        public static void ImportTextMeshProEssentialsBatch()
        {
            if (HasTextMeshProResources)
            {
                Debug.Log("[Block Puzzle] TextMeshPro resources already imported.");
                EditorApplication.Exit(0);
                return;
            }

            TMP_PackageResourceImporter.ImportResources(true, false, false);

            importDeadline = EditorApplication.timeSinceStartup + ImportTimeoutSeconds;
            EditorApplication.update += WaitForImport;
        }

        private static void WaitForImport()
        {
            if (HasTextMeshProResources)
            {
                EditorApplication.update -= WaitForImport;
                AssetDatabase.Refresh();
                Debug.Log("[Block Puzzle] TextMeshPro resources imported.");
                EditorApplication.Exit(0);
                return;
            }

            if (EditorApplication.timeSinceStartup > importDeadline)
            {
                EditorApplication.update -= WaitForImport;
                Debug.LogError("[Block Puzzle] Timed out while importing TextMeshPro resources.");
                EditorApplication.Exit(1);
            }
        }
    }
}
