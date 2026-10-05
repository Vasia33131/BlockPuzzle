using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using BlockPuzzle.UI;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Fullscreen (interstitial) ads for Yandex Games, plus the GameplayAPI flag.
    ///
    /// Ads never interrupt a run: the triggers are a restart tap (Game Over or Pause)
    /// and the Play tap in the main menu, which are natural breaks between runs. Yandex requirement 4.4 says the
    /// ad must open on that same tap, without a countdown and without a warning
    /// (delay cap 0.33s), so the call is made synchronously from the restart event.
    ///
    /// New players get their first <see cref="AdFreeGames"/> runs without ads. The
    /// finished-run counter lives in <see cref="SavesYG.gamesPlayed"/> and is mirrored
    /// in PlayerPrefs; the larger of the two wins.
    ///
    /// Frequency is owned by the platform / PluginYG2 timer (interAdvInterval).
    /// Calling <see cref="YG2.InterstitialAdvShow"/> before the interval is over is a no-op.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class YandexInterstitialService : MonoBehaviour
    {
        /// <summary>Runs a new player finishes before the first interstitial may show.</summary>
        public const int AdFreeGames = 2;

        /// <summary>Levels up to this number never open an interstitial when they start from "Next" / "Retry" / the map.</summary>
        public const int AdFreeLevels = 5;

        public const string GamesPlayedKey = "BlockPuzzle.GamesPlayed";

        private GameManager gameManager;
        private ShopPanel shopPanel;
        private BoosterConfirmPanel boosterConfirm;

        /// <summary>
        /// Set once the current run was added to the counter, so a Game Over that is
        /// followed by a continue booster and a second Game Over is counted once.
        /// </summary>
        private bool currentRunCounted;

        /// <summary>How long after a request the fullscreen ad may still take to open, seconds.</summary>
        private const float AdOpenGrace = 1.5f;

        /// <summary>
        /// An ad was requested on the last tap and has not opened yet. The run behind the tap is already
        /// dealt, but the gameplay flag must not flash on for the frames before the ad covers it.
        /// </summary>
        private bool adOpening;

        private float adOpeningUntil;

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

        private void OnEnable()
        {
            // The level goal card must not run out behind a fullscreen ad.
            LevelRunController.IntroHold = () => YG2.nowAdsShow;
            TryBind();
        }

        private void OnDisable()
        {
            LevelRunController.IntroHold = null;
            Unbind();
        }

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

            if (adOpening && (YG2.nowAdsShow || Time.unscaledTime > adOpeningUntil))
            {
                adOpening = false;
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
            gameManager.EndlessRequested += HandleEndlessRequested;
            gameManager.LevelAdBreakRequested += HandleLevelAdBreakRequested;
            gameManager.RunStarting += HandleRunStarting;
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
            if (gameManager == null)
            {
                return;
            }

            gameManager.StateChanged -= HandleStateChanged;
            gameManager.RestartRequested -= HandleRestartRequested;
            gameManager.EndlessRequested -= HandleEndlessRequested;
            gameManager.LevelAdBreakRequested -= HandleLevelAdBreakRequested;
            gameManager.RunStarting -= HandleRunStarting;
            gameManager = null;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                CountCurrentRun();
            }

            SyncGameplayApi();
        }

        /// <summary>
        /// Restart is a non-gameplay tap (Game Over / Pause). Yandex wants the ad on
        /// that same tap, with nothing in between, so everything here is synchronous.
        /// A restart from pause ends the run too, so it is counted before the check.
        /// </summary>
        private void HandleRestartRequested()
        {
            CountCurrentRun();
            currentRunCounted = false;

            if (ReadGamesPlayed() > AdFreeGames)
            {
                TryShowInterstitial();
            }
        }

        /// <summary>
        /// The Play / Continue tap in the main menu is a player action between runs, so it
        /// gets the same ad on the same tap and under the same free-games rule. Leaving for
        /// the menu (Home) never comes here and never shows an ad.
        /// </summary>
        private void HandleEndlessRequested()
        {
            if (ReadGamesPlayed() > AdFreeGames)
            {
                TryShowInterstitial();
            }
        }

        /// <summary>
        /// The "Next" / "Retry" tap of a level result and the "Play" tap of the level map card are player
        /// actions between attempts, so the ad opens on that same tap, synchronously. The first
        /// <see cref="AdFreeLevels"/> levels stay free of it.
        /// </summary>
        private void HandleLevelAdBreakRequested(int levelNumber)
        {
            if (levelNumber > AdFreeLevels)
            {
                TryShowInterstitial();
            }
        }

        /// <summary>A new run began (after a restart or from the menu): it has not been counted yet.</summary>
        private void HandleRunStarting() => currentRunCounted = false;

        private void CountCurrentRun()
        {
            if (currentRunCounted)
            {
                return;
            }

            currentRunCounted = true;
            WriteGamesPlayed(ReadGamesPlayed() + 1);
        }

        /// <summary>Finished runs: the larger of the local cache and the Yandex save.</summary>
        private static int ReadGamesPlayed()
        {
            int played = PlayerPrefs.GetInt(GamesPlayedKey, 0);
            if (YG2.isSDKEnabled && YG2.saves != null)
            {
                played = Mathf.Max(played, YG2.saves.gamesPlayed);
            }

            return played;
        }

        private static void WriteGamesPlayed(int played)
        {
            PlayerPrefs.SetInt(GamesPlayedKey, played);
            PlayerPrefs.Save();

            if (!YG2.isSDKEnabled || YG2.saves == null || YG2.saves.gamesPlayed == played)
            {
                return;
            }

            YG2.saves.gamesPlayed = played;
            CloudSaveGate.Request();
        }

        private void TryShowInterstitial()
        {
            if (PlayerProgress.AdsRemoved || YG2.nowAdsShow || TutorialProgress.IsActive)
            {
                return;
            }

            // The SDK ignores a call before the interval is over; only a real request delays the flag.
            bool willOpen = YG2.isTimerAdvCompleted;
            YG2.InterstitialAdvShow();
            if (willOpen)
            {
                adOpening = true;
                adOpeningUntil = Time.unscaledTime + AdOpenGrace;
            }
        }

        /// <summary>
        /// Requirement 1.19.3: the gameplay flag may be on only while the player can
        /// really act on the board. Both calls are cheap — PluginYG2 ignores a repeat
        /// of the state it is already in — so the check runs every frame and covers
        /// overlays that do not change <see cref="GameState"/>, like the shop.
        /// </summary>
        private void SyncGameplayApi()
        {
            if (!YG2.isSDKEnabled)
            {
                return;
            }

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
            // Main menu, pause and Game Over live in the state (anything but Playing is
            // GameplayStop); window focus is handled by the
            // SDK pause, which we only read here so we do not fight it.
            if (gameManager == null || gameManager.State != GameState.Playing)
            {
                return false;
            }

            if (adOpening || YG2.nowAdsShow || YG2.isPauseGame || !YG2.isFocusWindowGame)
            {
                return false;
            }

            // The goal card before the first move of a level is not gameplay yet.
            if (gameManager.LevelRun != null && gameManager.LevelRun.IsIntro)
            {
                return false;
            }

            if (shopPanel != null && shopPanel.IsOpen)
            {
                return false;
            }

            if (DailyRewardPanel.IsShowing)
            {
                return false;
            }

            return boosterConfirm == null || !boosterConfirm.IsOpen;
        }
    }
}
