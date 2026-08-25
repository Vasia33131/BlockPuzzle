using System.Collections;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Fullscreen (interstitial) ads for Yandex Games.
    ///
    /// Block Puzzle is a turn-based puzzle. Yandex requirement 4.4 says ads in such
    /// games must open immediately after a player action, without a countdown:
    /// restart tap, or dropping a figure. A 2-second "ads in 2, 1" warning is only
    /// for real-time games with levels longer than 5 minutes; using it here would
    /// exceed the 0.33s delay cap and fail moderation.
    ///
    /// Frequency is owned by the platform / PluginYG2 timer. Calling
    /// <see cref="YG2.InterstitialAdvShow"/> when the interval is not over is a no-op.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class YandexInterstitialService : MonoBehaviour
    {
        private GameManager gameManager;
        private GridManager grid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexInterstitialService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexInterstitialService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexInterstitialService>();
        }

        private void OnEnable() => TryBind();

        private void OnDisable() => Unbind();

        private void Update()
        {
            if (gameManager == null)
            {
                TryBind();
            }
        }

        private void TryBind()
        {
            GameManager manager = GameManager.Instance != null
                ? GameManager.Instance
                : FindObjectOfType<GameManager>(true);

            if (manager == null || manager == gameManager)
            {
                return;
            }

            Unbind();
            gameManager = manager;
            gameManager.StateChanged += HandleStateChanged;
            gameManager.RestartRequested += HandleRestartRequested;
            BindGrid(gameManager.Grid);
            SyncGameplayApi(gameManager.State);
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager.RestartRequested -= HandleRestartRequested;
                gameManager = null;
            }

            UnbindGrid();
        }

        private void BindGrid(GridManager next)
        {
            if (next == grid)
            {
                return;
            }

            UnbindGrid();
            grid = next;
            if (grid != null)
            {
                grid.ShapePlaced += HandleShapePlaced;
            }
        }

        private void UnbindGrid()
        {
            if (grid == null)
            {
                return;
            }

            grid.ShapePlaced -= HandleShapePlaced;
            grid = null;
        }

        private void HandleStateChanged(GameState state)
        {
            if (gameManager != null)
            {
                BindGrid(gameManager.Grid);
            }

            SyncGameplayApi(state);
        }

        /// <summary>
        /// Restart is a non-gameplay tap (Game Over / Pause). Yandex wants the ad
        /// on that same tap, with no warning overlay.
        /// </summary>
        private void HandleRestartRequested() => TryShowInterstitial();

        /// <summary>
        /// Dropping a figure is a turn-based game action. Wait one frame so game-over
        /// evaluation can run first; that is still far below Yandex's 0.33s delay cap.
        /// If the PluginYG2 interval has not elapsed, the call is ignored.
        /// </summary>
        private void HandleShapePlaced(PlacementResult _)
        {
            StopCoroutine(nameof(ShowAfterTurn));
            StartCoroutine(ShowAfterTurn());
        }

        private IEnumerator ShowAfterTurn()
        {
            yield return null;

            if (gameManager == null || !gameManager.IsPlaying)
            {
                yield break;
            }

            TryShowInterstitial();
        }

        private static void TryShowInterstitial()
        {
            if (PlayerProgress.AdsRemoved || YG2.nowAdsShow)
            {
                return;
            }

            YG2.InterstitialAdvShow();
        }

        /// <summary>
        /// Tells Yandex when the player is actually playing, so session metrics stay
        /// accurate. Ads already pause via <see cref="YG2.PauseGame"/>.
        /// </summary>
        private static void SyncGameplayApi(GameState state)
        {
            if (state == GameState.Playing)
            {
                YG2.GameplayStart();
            }
            else
            {
                YG2.GameplayStop();
            }
        }
    }
}
