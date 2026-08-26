using UnityEditor;
using UnityEngine;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Recreates overlay prefabs when they are missing on disk (e.g. after a
    /// language/layout update deleted them while the editor was open).
    /// </summary>
    [InitializeOnLoad]
    public static class OverlayPrefabRepair
    {
        private const string SessionKey = "BlockPuzzle.OverlayPrefabRepair.ClassicTheme.Ran";

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
            GameObject shop = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabGenerator.ShopPanelPath);
            bool missingShop = shop == null;
            bool staleShop = shop != null &&
                ((shop.transform.Find("Card/ThemeClassicCard") == null &&
                  shop.transform.Find("Card/ThemeDefaultCard") == null) ||
                 shop.transform.Find("Card/ThemeOceanCard") == null ||
                 shop.transform.Find("Card/ShapesPack1Card") == null);
            if (!missingGameOver && !missingPause && !missingShop && !staleShop)
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            PrefabGenerator.RegenerateOverlayPanels();
            AssetDatabase.SaveAssets();
            Debug.Log("[Block Puzzle] Restored overlay prefabs (GameOverPanel, PausePanel, ShopPanel with classic theme, paid themes and shape pack).");
        }
    }
}
