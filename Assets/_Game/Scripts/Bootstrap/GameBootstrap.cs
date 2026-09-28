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

            // Baked scenes may predate UIManager / OrientationHandler / BoosterBar / ShopPanel /
            // BoosterConfirmPanel.
            UIManager.Ensure();
            OrientationHandler.Ensure();

            RectTransform safeArea = GameObject.Find("SafeArea")?.GetComponent<RectTransform>();
            GameManager gameManager = FindObjectOfType<GameManager>();
            GameSceneFactory.EnsureBoosterBar(safeArea, gameManager);
            TutorialController.Ensure(gameManager);

            RectTransform canvasRect = FindObjectOfType<Canvas>()?.GetComponent<RectTransform>();
            RectTransform topPanel = GameObject.Find("TopPanel")?.GetComponent<RectTransform>();
            Button hudShop = GameSceneFactory.EnsureHudShopButton(topPanel);
            GameSceneFactory.EnsureShopPanel(canvasRect, hudShop);
            GameSceneFactory.EnsureBoosterConfirmPanel(canvasRect, gameManager);

            // Levels: the HUD replaces the endless score while a level is on, the overlays sit above the
            // pause screen and below the menu. All of them are built in code.
            LevelHudView.Ensure(topPanel, gameManager);
            LevelIntroPanel.Ensure(canvasRect, gameManager);
            LevelWinPanel.Ensure(canvasRect, gameManager);
            LevelFailPanel.Ensure(canvasRect, gameManager);
            LevelExitConfirmPanel.Ensure(canvasRect, gameManager);

            // Meta layer overlays are always built in code. The menu goes first: the daily
            // reward pops up over it and lifts itself to the top.
            GameSceneFactory.EnsureMainMenuPanel(canvasRect, gameManager);
            LevelMapPanel.Ensure(canvasRect, gameManager);
            DailyRewardPanel.Ensure(canvasRect, gameManager);
            SettingsPanel.Ensure(canvasRect, gameManager);
            MetaToast.Ensure(canvasRect);
            LevelUpPopup.Ensure(canvasRect, gameManager);
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
