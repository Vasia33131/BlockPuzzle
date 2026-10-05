using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using BlockPuzzle.UI;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Reports Game Ready to Yandex (requirement 1.19.2) at the moment the game is
    /// actually interactive: the scene is built and the main menu is on screen and
    /// accepts taps. autoGRA is off in SettingsYG2, so this is the only call — it never
    /// fires on a black screen, and it is not made from Awake before the UI exists.
    /// A game that skips the menu (GameManager.openMenuOnStart off) reports once it is playing.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class YandexGameReadyService : MonoBehaviour
    {
        private bool reported;
        private bool interactiveLastFrame;

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

            if (!IsInteractive())
            {
                interactiveLastFrame = false;
                return;
            }

            if (!interactiveLastFrame)
            {
                // One frame of margin, so the screen is drawn and not just in memory.
                interactiveLastFrame = true;
                return;
            }

            reported = true;
            YG2.GameReadyAPI();
            enabled = false;
        }

        private static bool IsInteractive()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                return false;
            }

            return manager.State == GameState.MainMenu
                ? MainMenuPanel.IsInteractive
                : manager.State == GameState.Playing;
        }
    }
}
