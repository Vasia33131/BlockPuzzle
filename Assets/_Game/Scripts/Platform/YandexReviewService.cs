using System.Collections;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Asks for a store review through the platform dialog, once per account.
    ///
    /// The ask happens only on the Game Over screen (never mid-run), after the
    /// <see cref="GamesBeforeAsk"/>-th finished run or right after a run that beat an
    /// existing record, and only when the platform says the dialog can open
    /// (<c>YG2.reviewCanShow</c>). Needs the Review module (<c>Review_yg</c>); without
    /// it the service only keeps the finished-run counter.
    /// Lives outside game asmdefs so it can reference PluginYG2 (Assembly-CSharp).
    /// </summary>
    [DefaultExecutionOrder(115)]
    public sealed class YandexReviewService : MonoBehaviour
    {
        public const int GamesBeforeAsk = 3;

        private GameManager gameManager;

        /// <summary>Set once the current run was counted, so a continue booster does not count it twice.</summary>
        private bool currentRunCounted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexReviewService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexReviewService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexReviewService>();
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
            gameManager.RunStarting += HandleRunStarting;
        }

        private void Unbind()
        {
            if (gameManager == null)
            {
                return;
            }

            gameManager.StateChanged -= HandleStateChanged;
            gameManager.RunStarting -= HandleRunStarting;
            gameManager = null;
        }

        private void HandleRunStarting()
        {
            currentRunCounted = false;
            StopAllCoroutines();
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.GameOver || currentRunCounted)
            {
                return;
            }

            currentRunCounted = true;
            bool newRecord = gameManager.Score != null
                && gameManager.Score.RunStartRecord > 0
                && gameManager.Score.IsNewRecord;

            SavesYG saves = YG2.isSDKEnabled ? YG2.saves : null;
            if (saves == null)
            {
                return;
            }

            saves.finishedGames++;
            CloudSaveGate.Request();

            if (saves.reviewAsked || (saves.finishedGames < GamesBeforeAsk && !newRecord))
            {
                return;
            }

            StartCoroutine(AskAfterDelay());
        }

        private IEnumerator AskAfterDelay()
        {
            yield return new WaitForSecondsRealtime(YandexPromptGate.DelaySeconds);

            if (!YandexPromptGate.CanOpen(gameManager) || YG2.saves.reviewAsked)
            {
                yield break;
            }

#if Review_yg
            if (!YG2.reviewCanShow)
            {
                yield break;
            }

            YandexPromptGate.Claim();
            YG2.saves.reviewAsked = true;
            CloudSaveGate.Request();
            YG2.ReviewShow();
#endif
        }
    }
}
