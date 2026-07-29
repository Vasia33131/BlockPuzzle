using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Recreates GameOver/Pause prefabs when they are missing on disk (e.g. after a
    /// language/layout update deleted them while the editor was open).
    /// </summary>
    [InitializeOnLoad]
    public static class OverlayPrefabRepair
    {
        private const string SessionKey = "BlockPuzzle.OverlayPrefabRepair.Ran";

        static OverlayPrefabRepair()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (Application.isBatchMode ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            bool missingGameOver = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabGenerator.GameOverPanelPath) == null;
            bool missingPause = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabGenerator.PausePanelPath) == null;
            if (!missingGameOver && !missingPause)
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            PrefabGenerator.RegenerateOverlayPanels();
            AssetDatabase.SaveAssets();
            Debug.Log("[Block Puzzle] Restored missing overlay prefabs (GameOverPanel, PausePanel).");
        }
    }
}
