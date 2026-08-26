using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Bootstrap;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Pieces;
using BlockPuzzle.UI;

namespace BlockPuzzle.EditorTools
{
    /// <summary>
    /// Bakes the prefabs the game uses into <c>Assets/_Game/Prefabs</c>. They are
    /// generated rather than authored so that the project stays reproducible from source.
    /// </summary>
    public static class PrefabGenerator
    {
        public const string PrefabFolder = "Assets/_Game/Prefabs";
        public const string SparkPath = PrefabFolder + "/Spark.prefab";
        public const string GridCellPath = PrefabFolder + "/GridCell.prefab";
        public const string BlockPiecePath = PrefabFolder + "/BlockPiece.prefab";
        public const string GameOverPanelPath = PrefabFolder + "/GameOverPanel.prefab";
        public const string PausePanelPath = PrefabFolder + "/PausePanel.prefab";
        public const string ShopPanelPath = PrefabFolder + "/ShopPanel.prefab";

        private const float SparkSize = 15f;

        public sealed class PrefabAssets
        {
            public GridCellView GridCell;
            public BlockPiece BlockPiece;
            public GameOverPanel GameOverPanel;
            public PausePanel PausePanel;
            public ShopPanel ShopPanel;
            public Image Spark;
        }

        [MenuItem("Tools/Block Puzzle/Regenerate Prefabs", priority = 30)]
        public static void Regenerate()
        {
            DeleteIfExists(SparkPath);
            DeleteIfExists(GridCellPath);
            DeleteIfExists(BlockPiecePath);
            DeleteIfExists(GameOverPanelPath);
            DeleteIfExists(PausePanelPath);
            DeleteIfExists(ShopPanelPath);
            EnsureAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[Block Puzzle] Prefabs regenerated: GridCell, BlockPiece, GameOverPanel, PausePanel, ShopPanel, Spark.");
        }

        /// <summary>Creates every gameplay/UI prefab and returns their loaded components.</summary>
        public static PrefabAssets EnsureAll()
        {
            RoundedSpriteGenerator.EnsureAsset();
            PatternSpriteGenerator.EnsureAssets();
            UIFactory.ClearCache();
            Directory.CreateDirectory(PrefabFolder);

            return new PrefabAssets
            {
                GridCell = EnsureGridCell(),
                BlockPiece = EnsureBlockPiece(),
                GameOverPanel = EnsureGameOverPanel(),
                PausePanel = EnsurePausePanel(),
                ShopPanel = EnsureShopPanel(),
                Spark = EnsureSpark()
            };
        }

        /// <summary>
        /// Rebuilds the overlay panels from <see cref="GameSceneFactory"/> so copy changes
        /// (language, layout) reach baked prefabs even when older files already exist.
        /// </summary>
        public static void RegenerateOverlayPanels()
        {
            DeleteIfExists(GameOverPanelPath);
            DeleteIfExists(PausePanelPath);
            DeleteIfExists(ShopPanelPath);
            RoundedSpriteGenerator.EnsureAsset();
            UIFactory.ClearCache();
            Directory.CreateDirectory(PrefabFolder);
            EnsureGameOverPanel();
            EnsurePausePanel();
            EnsureShopPanel();
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        public static Image EnsureSpark()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SparkPath);
            if (existing != null)
            {
                return existing.GetComponent<Image>();
            }

            var source = new GameObject("Spark", typeof(RectTransform));

            var rect = (RectTransform)source.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(SparkSize, SparkSize);

            var image = source.AddComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            ApplyRounded(image);

            return SaveComponent<Image>(source, SparkPath);
        }

        public static GridCellView EnsureGridCell()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GridCellPath);
            if (existing != null && existing.transform.Find("Fill/Pattern") != null)
            {
                return existing.GetComponent<GridCellView>();
            }

            if (existing != null)
            {
                DeleteIfExists(GridCellPath);
            }

            // Built under a throwaway parent so Create can position a real template cell.
            var host = new GameObject("GridCellHost", typeof(RectTransform));
            GridCellView view = GridCellView.Create(
                host.transform,
                Vector2Int.zero,
                GameTheme.CellSize,
                GameTheme.CellSize + GameTheme.CellSpacing);
            view.gameObject.name = "GridCell";
            view.transform.SetParent(null, false);

            GridCellView saved = SaveComponent<GridCellView>(view.gameObject, GridCellPath);
            Object.DestroyImmediate(host);
            return saved;
        }

        public static BlockPiece EnsureBlockPiece()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPiecePath);
            if (existing != null && existing.transform.Find("Pattern") != null)
            {
                return existing.GetComponent<BlockPiece>();
            }

            if (existing != null)
            {
                DeleteIfExists(BlockPiecePath);
            }

            var host = new GameObject("BlockPieceHost", typeof(RectTransform));
            BlockPiece piece = BlockPiece.Create(
                host.transform,
                Vector2Int.zero,
                Vector2Int.one,
                GameTheme.CellSize,
                GameTheme.CellSize + GameTheme.CellSpacing,
                GameTheme.ShapePrimary);
            piece.gameObject.name = "BlockPiece";
            piece.transform.SetParent(null, false);

            BlockPiece saved = SaveComponent<BlockPiece>(piece.gameObject, BlockPiecePath);
            Object.DestroyImmediate(host);
            return saved;
        }

        public static GameOverPanel EnsureGameOverPanel()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GameOverPanelPath);
            if (existing != null)
            {
                return existing.GetComponent<GameOverPanel>();
            }

            var host = new GameObject("PanelHost", typeof(RectTransform));
            GameOverPanel panel = GameSceneFactory.BuildGameOverPanelHierarchy((RectTransform)host.transform);
            panel.gameObject.name = "GameOverPanel";
            panel.transform.SetParent(null, false);

            GameOverPanel saved = SaveComponent<GameOverPanel>(panel.gameObject, GameOverPanelPath);
            Object.DestroyImmediate(host);
            return saved;
        }

        public static PausePanel EnsurePausePanel()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PausePanelPath);
            if (existing != null &&
                existing.transform.Find("Card/SoundButton") != null &&
                existing.transform.Find("Card/ShopButton") == null)
            {
                return existing.GetComponent<PausePanel>();
            }

            if (existing != null)
            {
                DeleteIfExists(PausePanelPath);
            }

            var host = new GameObject("PanelHost", typeof(RectTransform));
            PausePanel panel = GameSceneFactory.BuildPausePanelHierarchy((RectTransform)host.transform);
            panel.gameObject.name = "PausePanel";
            panel.transform.SetParent(null, false);

            PausePanel saved = SaveComponent<PausePanel>(panel.gameObject, PausePanelPath);
            Object.DestroyImmediate(host);
            return saved;
        }

        public static ShopPanel EnsureShopPanel()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPanelPath);
            if (existing != null &&
                existing.transform.Find("Card/NoAdsCard/BuyButton") != null &&
                (existing.transform.Find("Card/ThemeClassicCard/BuyButton") != null ||
                 existing.transform.Find("Card/ThemeDefaultCard/BuyButton") != null) &&
                existing.transform.Find("Card/ThemeOceanCard/BuyButton") != null &&
                existing.transform.Find("Card/ThemeCandyCard/BuyButton") != null &&
                existing.transform.Find("Card/ShapesPack1Card/BuyButton") != null)
            {
                return existing.GetComponent<ShopPanel>();
            }

            if (existing != null)
            {
                DeleteIfExists(ShopPanelPath);
            }

            var host = new GameObject("PanelHost", typeof(RectTransform));
            ShopPanel panel = GameSceneFactory.BuildShopPanelHierarchy((RectTransform)host.transform);
            panel.gameObject.name = "ShopPanel";
            panel.transform.SetParent(null, false);

            ShopPanel saved = SaveComponent<ShopPanel>(panel.gameObject, ShopPanelPath);
            Object.DestroyImmediate(host);
            return saved;
        }

        private static T SaveComponent<T>(GameObject source, string path) where T : Component
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            return saved != null ? saved.GetComponent<T>() : null;
        }

        private static void ApplyRounded(Image image)
        {
            if (UIFactory.RoundedSprite == null)
            {
                return;
            }

            image.sprite = UIFactory.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }
    }
}
