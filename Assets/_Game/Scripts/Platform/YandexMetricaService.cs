using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;
using BlockPuzzle.Levels;
using BlockPuzzle.Managers;
using BlockPuzzle.UI;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Yandex Metrica goals (PluginYG2 Metrica module, <c>Metrica_yg</c>; without it every
    /// call is a no-op). Each goal must exist in the Metrica counter as a JavaScript-event
    /// goal with the same identifier:
    /// first_move, game_start, game_over {score, moves, duration_sec, lines}, restart,
    /// new_record, rewarded_request {type}, rewarded_success {type}, interstitial_shown,
    /// shop_open, purchase {id}, social_click {network}, tutorial_done (sent by <see cref="YandexTutorialService"/>),
    /// and for campaign levels level_start {n}, level_win {n, stars, moves_left},
    /// level_fail {n, reason: moves | space | quit}, level_continue {n}.
    ///
    /// first_move is sent once per player (flag in the save), the rest on every occurrence.
    /// A run restored from the save sends no game_start and counts moves, lines and time
    /// from the moment it was restored.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class YandexMetricaService : MonoBehaviour
    {
        public const string FirstMove = "first_move";
        public const string GameStart = "game_start";
        public const string GameOver = "game_over";
        public const string Restart = "restart";
        public const string NewRecord = "new_record";
        public const string RewardedRequest = "rewarded_request";
        public const string RewardedSuccess = "rewarded_success";
        public const string InterstitialShown = "interstitial_shown";
        public const string ShopOpen = "shop_open";
        public const string Purchase = "purchase";
        public const string SocialClick = "social_click";
        public const string LevelStart = "level_start";
        public const string LevelWin = "level_win";
        public const string LevelFail = "level_fail";
        public const string LevelContinue = "level_continue";

        private GameManager gameManager;
        private GridManager grid;
        private ScoreManager score;
        private LevelRunController levelRun;
        private ShopPanel shopPanel;

        private int moves;
        private int lines;
        private float playSeconds;
        private bool gameOverSent;
        private bool shopWasOpen;
        private static bool firstMoveSentThisLaunch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => firstMoveSentThisLaunch = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexMetricaService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexMetricaService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexMetricaService>();
        }

        /// <summary>Sends a goal with no parameters. Safe to call from anywhere.</summary>
        public static void Send(string goal)
        {
#if Metrica_yg
            YG2.MetricaSend(goal);
#endif
        }

        /// <summary>Sends a goal with parameters (numbers stay numbers).</summary>
        public static void Send(string goal, Dictionary<string, object> data)
        {
#if Metrica_yg
            YG2.MetricaSend(goal, data);
#endif
        }

        /// <summary>Goal with a single string parameter, e.g. <c>type</c> or <c>id</c>.</summary>
        public static void Send(string goal, string key, string value)
        {
            Send(goal, new Dictionary<string, object> { { key, value } });
        }

        private void OnEnable()
        {
            YG2.onOpenInterAdv += HandleInterstitialOpened;
            YG2.onPurchaseSuccess += HandlePurchase;
            SocialLinks.Clicked += HandleSocialClicked;
            TryBind();
        }

        private void OnDisable()
        {
            YG2.onOpenInterAdv -= HandleInterstitialOpened;
            YG2.onPurchaseSuccess -= HandlePurchase;
            SocialLinks.Clicked -= HandleSocialClicked;
            Unbind();
        }

        private void Update()
        {
            if (gameManager == null)
            {
                TryBind();
            }

            if (shopPanel == null)
            {
                shopPanel = FindObjectOfType<ShopPanel>(true);
            }

            TrackShop();
            TrackPlayTime();
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
            grid = manager.Grid;
            score = manager.Score;

            gameManager.StateChanged += HandleStateChanged;
            gameManager.RestartRequested += HandleRestartRequested;
            gameManager.RunStarting += HandleRunStarting;
            if (grid != null)
            {
                grid.ShapePlaced += HandleShapePlaced;
            }

            if (score != null)
            {
                score.LinesCleared += HandleLinesCleared;
                score.RecordBroken += HandleRecordBroken;
            }

            levelRun = manager.LevelRun;
            if (levelRun != null)
            {
                levelRun.LevelStarted += HandleLevelStarted;
                levelRun.LevelWon += HandleLevelWon;
                levelRun.LevelFailed += HandleLevelFailed;
                levelRun.Continued += HandleLevelContinued;
                levelRun.LevelAbandoned += HandleLevelAbandoned;
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
            gameManager.RunStarting -= HandleRunStarting;
            if (grid != null)
            {
                grid.ShapePlaced -= HandleShapePlaced;
            }

            if (score != null)
            {
                score.LinesCleared -= HandleLinesCleared;
                score.RecordBroken -= HandleRecordBroken;
            }

            if (levelRun != null)
            {
                levelRun.LevelStarted -= HandleLevelStarted;
                levelRun.LevelWon -= HandleLevelWon;
                levelRun.LevelFailed -= HandleLevelFailed;
                levelRun.Continued -= HandleLevelContinued;
                levelRun.LevelAbandoned -= HandleLevelAbandoned;
            }

            gameManager = null;
            grid = null;
            score = null;
            levelRun = null;
        }

        // ---------------------------------------------------------------- levels

        private static void HandleLevelStarted(LevelDefinition level)
        {
            Send(LevelStart, new Dictionary<string, object> { { "n", level.Number } });
        }

        private static void HandleLevelWon(LevelResult result)
        {
            Send(LevelWin, new Dictionary<string, object>
            {
                { "n", result.Number },
                { "stars", result.Stars },
                { "moves_left", result.MovesLeft }
            });
        }

        private void HandleLevelFailed(LevelFailReason reason)
        {
            SendLevelFail(levelRun != null ? levelRun.LevelNumber : 0, reason == LevelFailReason.OutOfMoves ? "moves" : "space");
        }

        /// <summary>A level left from pause with at least one move made: the attempt burned.</summary>
        private static void HandleLevelAbandoned(int levelNumber, int moves) => SendLevelFail(levelNumber, "quit");

        private void HandleLevelContinued()
        {
            Send(LevelContinue, new Dictionary<string, object> { { "n", levelRun != null ? levelRun.LevelNumber : 0 } });
        }

        private static void SendLevelFail(int levelNumber, string reason)
        {
            Send(LevelFail, new Dictionary<string, object> { { "n", levelNumber }, { "reason", reason } });
        }

        private void HandleRunStarting()
        {
            moves = 0;
            lines = 0;
            playSeconds = 0f;
            gameOverSent = false;
            Send(GameStart);
        }

        private void HandleRestartRequested() => Send(Restart);

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.GameOver || gameOverSent)
            {
                return;
            }

            // A continue booster ends in a second Game Over of the same run: one goal per run.
            gameOverSent = true;
            Send(GameOver, new Dictionary<string, object>
            {
                { "score", score != null ? score.Score : 0 },
                { "moves", moves },
                { "duration_sec", Mathf.RoundToInt(playSeconds) },
                { "lines", lines }
            });
        }

        private void HandleShapePlaced(PlacementResult result)
        {
            if (!result.Success)
            {
                return;
            }

            moves++;
            TrySendFirstMove();
        }

        private void HandleLinesCleared(int cleared, int points, int combo) => lines += cleared;

        private static void HandleRecordBroken(int newBest) => Send(NewRecord);

        private static void TrySendFirstMove()
        {
            if (firstMoveSentThisLaunch)
            {
                return;
            }

            SavesYG saves = YG2.isSDKEnabled ? YG2.saves : null;
            if (saves == null || saves.firstMoveSent)
            {
                return;
            }

            firstMoveSentThisLaunch = true;
            saves.firstMoveSent = true;
            CloudSaveGate.Request();
            Send(FirstMove);
        }

        private static void HandleInterstitialOpened() => Send(InterstitialShown);

        private static void HandlePurchase(string id) => Send(Purchase, "id", id);

        private static void HandleSocialClicked(string network) => Send(SocialClick, "network", network);

        private void TrackShop()
        {
            bool open = shopPanel != null && shopPanel.IsOpen;
            if (open && !shopWasOpen)
            {
                Send(ShopOpen);
            }

            shopWasOpen = open;
        }

        /// <summary>Active play time of the run: paused, game-over and ad time do not count.</summary>
        private void TrackPlayTime()
        {
            if (gameManager != null && gameManager.State == GameState.Playing && !YG2.nowAdsShow)
            {
                playSeconds += Mathf.Min(Time.unscaledDeltaTime, 1f);
            }
        }
    }
}
