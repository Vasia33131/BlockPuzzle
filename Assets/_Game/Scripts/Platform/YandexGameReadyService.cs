using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Reports Game Ready to Yandex (requirement 1.19.2) at the moment the game is
    /// actually playable: the scene is built, the run has started and the tray already
    /// holds figures the player can drag. autoGRA is off in SettingsYG2, so this is the
    /// only call — it never fires on a black screen, and it is not made from Awake
    /// before the UI exists.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class YandexGameReadyService : MonoBehaviour
    {
        /// <summary>How long we wait for the first batch of figures before reporting anyway.</summary>
        private const float FiguresTimeout = 3f;

        private bool reported;
        private bool playableLastFrame;
        private float playingSince = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexGameReadyService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexGameReadyService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexGameReadyService>();
        }

        private void Update()
        {
            if (reported || !YG2.isSDKEnabled)
            {
                return;
            }

            GameManager manager = GameManager.Instance;
            if (manager == null || manager.State != GameState.Playing)
            {
                playingSince = -1f;
                playableLastFrame = false;
                return;
            }

            if (playingSince < 0f)
            {
                playingSince = Time.realtimeSinceStartup;
            }

            bool figuresReady = manager.Spawner != null && manager.Spawner.RemainingCount > 0;

            // Safety net: the portal loader must never keep spinning because the tray
            // failed to fill for some reason of ours.
            if (!figuresReady && Time.realtimeSinceStartup - playingSince < FiguresTimeout)
            {
                playableLastFrame = false;
                return;
            }

            if (!playableLastFrame)
            {
                // One frame of margin, so the board is on screen and not just in memory.
                playableLastFrame = true;
                return;
            }

            reported = true;
            YG2.GameReadyAPI();
            enabled = false;
        }
    }
}
