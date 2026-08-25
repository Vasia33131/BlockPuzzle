using UnityEngine;
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

        private GameOverPanel gameOverPanel;
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
            TryBindBoosterBar();
        }

        private void OnDisable()
        {
            YG2.onRewardAdv -= HandleReward;
            UnbindGameOverPanel();
            UnbindBoosterBar();
        }

        private void Update()
        {
            if (gameOverPanel == null)
            {
                TryBindGameOverPanel();
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
            YG2.RewardedAdvShow(ContinueRewardId);
        }

        private void HandleUndoRequested()
        {
            YG2.RewardedAdvShow(UndoRewardId);
        }

        private void HandleExtraRequested()
        {
            YG2.RewardedAdvShow(ExtraPieceRewardId);
        }

        private void HandleClearRequested()
        {
            YG2.RewardedAdvShow(ClearLineRewardId);
        }

        private static void HandleReward(string id)
        {
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
