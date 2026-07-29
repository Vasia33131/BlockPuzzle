using UnityEngine;
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

            // Baked scenes may predate UIManager / OrientationHandler; ensure both run.
            UIManager.Ensure();
            OrientationHandler.Ensure();
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
