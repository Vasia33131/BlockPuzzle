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
            // Core first: the board, pieces and GameManager must come up even when a UI widget below throws.
            Step("ApplyMobileSettings", () => ApplyMobileSettings());
            Step("GameTween.Initialize", () => GameTween.Initialize());

            Step("GameSceneFactory.Build", () =>
            {
                if (buildSceneIfMissing && FindObjectOfType<GameManager>() == null)
                {
                    GameSceneFactory.Build(shapeLibrary);
                }
            });

            // Baked scenes may predate UIManager / OrientationHandler / BoosterBar / ShopPanel /
            // BoosterConfirmPanel.
            Step("UIManager.Ensure", () => UIManager.Ensure());
            Step("OrientationHandler.Ensure", () => OrientationHandler.Ensure());

            RectTransform safeArea = null;
            GameManager gameManager = null;
            RectTransform canvasRect = null;
            RectTransform topPanel = null;
            Button hudShop = null;
            Step("FindSceneRoots", () =>
            {
                safeArea = GameObject.Find("SafeArea")?.GetComponent<RectTransform>();
                gameManager = FindObjectOfType<GameManager>();
                canvasRect = FindObjectOfType<Canvas>()?.GetComponent<RectTransform>();
                topPanel = GameObject.Find("TopPanel")?.GetComponent<RectTransform>();
            });

            Step("EnsureBoosterBar", () => GameSceneFactory.EnsureBoosterBar(safeArea, gameManager));
            Step("TutorialController.Ensure", () => TutorialController.Ensure(gameManager));

            Step("EnsureHudShopButton", () => hudShop = GameSceneFactory.EnsureHudShopButton(topPanel));
            Step("EnsureShopPanel", () => GameSceneFactory.EnsureShopPanel(canvasRect, hudShop));
            Step("EnsureBoosterConfirmPanel", () => GameSceneFactory.EnsureBoosterConfirmPanel(canvasRect, gameManager));

            // Levels: the HUD replaces the endless score while a level is on, the overlays sit above the
            // pause screen and below the menu. All of them are built in code.
            Step("LevelHudView.Ensure", () => LevelHudView.Ensure(topPanel, gameManager));
            Step("LevelIntroPanel.Ensure", () => LevelIntroPanel.Ensure(canvasRect, gameManager));
            Step("LevelWinPanel.Ensure", () => LevelWinPanel.Ensure(canvasRect, gameManager));
            Step("LevelFailPanel.Ensure", () => LevelFailPanel.Ensure(canvasRect, gameManager));
            Step("LevelExitConfirmPanel.Ensure", () => LevelExitConfirmPanel.Ensure(canvasRect, gameManager));

            // Meta layer overlays are always built in code. The menu goes first: the daily
            // reward pops up over it and lifts itself to the top.
            Step("EnsureMainMenuPanel", () => GameSceneFactory.EnsureMainMenuPanel(canvasRect, gameManager));
            Step("LevelMapPanel.Ensure", () => LevelMapPanel.Ensure(canvasRect, gameManager));
            Step("DailyRewardPanel.Ensure", () => DailyRewardPanel.Ensure(canvasRect, gameManager));
            Step("SettingsPanel.Ensure", () => SettingsPanel.Ensure(canvasRect, gameManager));
            Step("MetaToast.Ensure", () => MetaToast.Ensure(canvasRect));
            Step("LevelUpPopup.Ensure", () => LevelUpPopup.Ensure(canvasRect, gameManager));
            Step("ThemeBinder.Ensure", () => ThemeBinder.Ensure());
            Step("GameTheme.ApplyFromProgress", () => GameTheme.ApplyFromProgress());
            Step("OrientationHandler.RefreshNow", () => FindObjectOfType<OrientationHandler>()?.RefreshNow());
            Step("UIManager.FixLayoutForPC", () => FindObjectOfType<UIManager>()?.FixLayoutForPC());
            Step("ButtonPressAnimator.AttachAll", () => ButtonPressAnimator.AttachAll(FindObjectOfType<Canvas>()?.transform));
        }

        /// <summary>Runs one start-up step; a failure is logged with the step name and does not stop the next steps.</summary>
        private static void Step(string name, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[GameBootstrap] Step failed: " + name);
                Debug.LogException(exception);
            }
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
