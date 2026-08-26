using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using BlockPuzzle.UI;

namespace BlockPuzzle.Bootstrap
{
    /// <summary>
    /// Entry point of the game scene. Applies the mobile runtime settings and, if the
    /// scene does not already contain the hierarchy, generates it with
    /// <see cref="GameSceneFactory"/>. Dropping this single component into an empty
    /// scene is enough to get a playable build.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private ShapeLibrary shapeLibrary;
        [SerializeField] private bool buildSceneIfMissing = true;
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            
            ApplyMobileSettings();
            GameTween.Initialize();

            if (buildSceneIfMissing && FindObjectOfType<GameManager>() == null)
            {
                GameSceneFactory.Build(shapeLibrary);
            }

            // Baked scenes may predate UIManager / OrientationHandler / BoosterBar / ShopPanel.
            UIManager.Ensure();
            OrientationHandler.Ensure();

            RectTransform safeArea = GameObject.Find("SafeArea")?.GetComponent<RectTransform>();
            GameManager gameManager = FindObjectOfType<GameManager>();
            GameSceneFactory.EnsureBoosterBar(safeArea, gameManager);

            RectTransform canvasRect = FindObjectOfType<Canvas>()?.GetComponent<RectTransform>();
            RectTransform topPanel = GameObject.Find("TopPanel")?.GetComponent<RectTransform>();
            Button hudShop = GameSceneFactory.EnsureHudShopButton(topPanel);
            GameSceneFactory.EnsureShopPanel(canvasRect, hudShop);
            ThemeBinder.Ensure();
            GameTheme.ApplyFromProgress();
            FindObjectOfType<OrientationHandler>()?.RefreshNow();

            UIManager existingUi = FindObjectOfType<UIManager>();
            existingUi?.FixLayoutForPC();
            ButtonPressAnimator.AttachAll(FindObjectOfType<Canvas>()?.transform);
        }

        private void ApplyMobileSettings()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }
}
