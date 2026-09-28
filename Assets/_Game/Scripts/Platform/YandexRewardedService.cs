using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using BlockPuzzle.UI;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Rewarded ads for in-run boosters and Game Over continue. Lives outside game
    /// asmdefs so it can reference PluginYG2 (Assembly-CSharp). Closing the video
    /// without a reward grants nothing. Ad removal does not block these placements:
    /// the player opts into the video for a bonus.
    /// </summary>
    [DefaultExecutionOrder(105)]
    public sealed class YandexRewardedService : MonoBehaviour
    {
        public const string ContinueRewardId = "continue";
        public const string UndoRewardId = "undo";
        public const string ExtraPieceRewardId = "extra_piece";
        public const string ClearLineRewardId = "clear_line";
        public const string LevelContinueRewardId = "level_continue";
        public const string LevelDoubleRewardId = "level_double";

        private GameOverPanel gameOverPanel;
        private LevelFailPanel levelFailPanel;
        private LevelWinPanel levelWinPanel;
        private BoosterBar boosterBar;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexRewardedService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexRewardedService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexRewardedService>();
        }

        private void OnEnable()
        {
            YG2.onRewardAdv += HandleReward;
            TryBindGameOverPanel();
            TryBindLevelPanels();
            TryBindBoosterBar();
        }

        private void OnDisable()
        {
            YG2.onRewardAdv -= HandleReward;
            UnbindGameOverPanel();
            UnbindLevelPanels();
            UnbindBoosterBar();
        }

        private void Update()
        {
            if (gameOverPanel == null)
            {
                TryBindGameOverPanel();
            }

            if (levelFailPanel == null || levelWinPanel == null)
            {
                TryBindLevelPanels();
            }

            if (boosterBar == null)
            {
                TryBindBoosterBar();
            }
        }

        private void TryBindGameOverPanel()
        {
            GameOverPanel panel = FindObjectOfType<GameOverPanel>(true);
            if (panel == null || panel == gameOverPanel)
            {
                return;
            }

            UnbindGameOverPanel();
            gameOverPanel = panel;
            gameOverPanel.ContinueRequested += HandleContinueRequested;
        }

        private void UnbindGameOverPanel()
        {
            if (gameOverPanel == null)
            {
                return;
            }

            gameOverPanel.ContinueRequested -= HandleContinueRequested;
            gameOverPanel = null;
        }

        private void TryBindLevelPanels()
        {
            if (levelFailPanel == null)
            {
                levelFailPanel = FindObjectOfType<LevelFailPanel>(true);
                if (levelFailPanel != null)
                {
                    levelFailPanel.ContinueRequested += HandleLevelContinueRequested;
                }
            }

            if (levelWinPanel == null)
            {
                levelWinPanel = FindObjectOfType<LevelWinPanel>(true);
                if (levelWinPanel != null)
                {
                    levelWinPanel.DoubleRequested += HandleLevelDoubleRequested;
                }
            }
        }

        private void UnbindLevelPanels()
        {
            if (levelFailPanel != null)
            {
                levelFailPanel.ContinueRequested -= HandleLevelContinueRequested;
                levelFailPanel = null;
            }

            if (levelWinPanel != null)
            {
                levelWinPanel.DoubleRequested -= HandleLevelDoubleRequested;
                levelWinPanel = null;
            }
        }

        private void HandleLevelContinueRequested()
        {
            ShowRewarded(LevelContinueRewardId);
        }

        private void HandleLevelDoubleRequested()
        {
            ShowRewarded(LevelDoubleRewardId);
        }

        private void TryBindBoosterBar()
        {
            BoosterBar bar = FindObjectOfType<BoosterBar>(true);
            if (bar == null || bar == boosterBar)
            {
                return;
            }

            UnbindBoosterBar();
            boosterBar = bar;
            boosterBar.UndoRequested += HandleUndoRequested;
            boosterBar.ExtraRequested += HandleExtraRequested;
            boosterBar.ClearRequested += HandleClearRequested;
        }

        private void UnbindBoosterBar()
        {
            if (boosterBar == null)
            {
                return;
            }

            boosterBar.UndoRequested -= HandleUndoRequested;
            boosterBar.ExtraRequested -= HandleExtraRequested;
            boosterBar.ClearRequested -= HandleClearRequested;
            boosterBar = null;
        }

        private void HandleContinueRequested()
        {
            ShowRewarded(ContinueRewardId);
        }

        private void HandleUndoRequested()
        {
            ShowRewarded(UndoRewardId);
        }

        private void HandleExtraRequested()
        {
            ShowRewarded(ExtraPieceRewardId);
        }

        private void HandleClearRequested()
        {
            ShowRewarded(ClearLineRewardId);
        }

        /// <summary>No ads of any kind while the first-run tutorial is on screen.</summary>
        private static void ShowRewarded(string id)
        {
            if (TutorialProgress.IsActive)
            {
                return;
            }

            YandexMetricaService.Send(YandexMetricaService.RewardedRequest, "type", id);
            YG2.RewardedAdvShow(id);
        }

        /// <summary>The level rewards act on the level controller; a stale grant (the screen is gone) does nothing.</summary>
        private static void HandleLevelReward(string id)
        {
            LevelRunController levelRun = GameManager.Instance != null
                ? GameManager.Instance.LevelRun
                : FindObjectOfType<LevelRunController>(true);

            if (levelRun == null)
            {
                return;
            }

            if (id == LevelContinueRewardId)
            {
                levelRun.TryContinue();
            }
            else
            {
                levelRun.TryDoubleCoins();
            }
        }

        private static void HandleReward(string id)
        {
            YandexMetricaService.Send(YandexMetricaService.RewardedSuccess, "type", id);

            if (id == LevelContinueRewardId || id == LevelDoubleRewardId)
            {
                HandleLevelReward(id);
                return;
            }

            BoosterController boosters = GameManager.Instance != null
                ? GameManager.Instance.Boosters
                : FindObjectOfType<BoosterController>(true);

            if (boosters == null)
            {
                return;
            }

            switch (id)
            {
                case ContinueRewardId:
                    boosters.TryContinue();
                    break;
                case UndoRewardId:
                    boosters.TryUndo();
                    break;
                case ExtraPieceRewardId:
                    boosters.TryExtraPiece();
                    break;
                case ClearLineRewardId:
                    boosters.TryClearFullestLine();
                    break;
            }
        }
    }
}
