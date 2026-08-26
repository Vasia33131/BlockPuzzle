using System.Collections;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Managers;
using BlockPuzzle.UI;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Fullscreen (interstitial) ads for Yandex Games, plus the GameplayAPI flag.
    ///
    /// Block Puzzle is a turn-based puzzle. Yandex requirement 4.4 says ads in such
    /// games must open immediately after a player action, without a countdown and
    /// without a warning: dropping a figure, or a restart tap. The delay cap is
    /// 0.33s, which leaves no room for an "ad is coming" card, so there is none.
    ///
    /// Frequency is owned by the platform / PluginYG2 timer (interAdvInterval).
    /// Calling <see cref="YG2.InterstitialAdvShow"/> before the interval is over is a no-op.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class YandexInterstitialService : MonoBehaviour
    {
        private GameManager gameManager;
        private GridManager grid;
        private ShopPanel shopPanel;
        private BoosterConfirmPanel boosterConfirm;

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

            if (shopPanel == null || boosterConfirm == null)
            {
                TryBindOverlays();
            }

            SyncGameplayApi();
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
            TryBindOverlays();
            SyncGameplayApi();
        }

        private void TryBindOverlays()
        {
            if (shopPanel == null)
            {
                shopPanel = FindObjectOfType<ShopPanel>(true);
            }

            if (boosterConfirm == null)
            {
                boosterConfirm = FindObjectOfType<BoosterConfirmPanel>(true);
            }
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

            SyncGameplayApi();
        }

        /// <summary>
        /// Restart is a non-gameplay tap (Game Over / Pause). Yandex wants the ad on
        /// that same tap, with nothing in between.
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
        /// Requirement 1.19.3: the gameplay flag may be on only while the player can
        /// really act on the board. Both calls are cheap — PluginYG2 ignores a repeat
        /// of the state it is already in — so the check runs every frame and covers
        /// overlays that do not change <see cref="GameState"/>, like the shop.
        /// </summary>
        private void SyncGameplayApi()
        {
            if (IsPlayerOnBoard())
            {
                YG2.GameplayStart();
            }
            else
            {
                YG2.GameplayStop();
            }
        }

        private bool IsPlayerOnBoard()
        {
            // Pause and Game Over live in the state; window focus is handled by the
            // SDK pause, which we only read here so we do not fight it.
            if (gameManager == null || gameManager.State != GameState.Playing)
            {
                return false;
            }

            if (YG2.nowAdsShow || YG2.isPauseGame || !YG2.isFocusWindowGame)
            {
                return false;
            }

            if (shopPanel != null && shopPanel.IsOpen)
            {
                return false;
            }

            return boosterConfirm == null || !boosterConfirm.IsOpen;
        }
    }
}
