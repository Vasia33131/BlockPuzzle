using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Submits local best scores to the Yandex Games leaderboard named "leader".
    /// Lives outside game asmdefs so it can reference PluginYG2 (Assembly-CSharp).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class YandexLeaderboardService : MonoBehaviour
    {
        public const string LeaderboardName = "leader";
        private const string LastSubmittedKey = "BlockPuzzle.LastSubmittedLeaderboardScore";

        private GameManager gameManager;
        private int pendingScore;
        private float lastSubmitTime = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexLeaderboardService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexLeaderboardService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexLeaderboardService>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += HandleSdkData;
            TryBind();
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= HandleSdkData;
            Unbind();
        }

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
        }

        private void Unbind()
        {
            if (gameManager == null)
            {
                return;
            }

            gameManager.StateChanged -= HandleStateChanged;
            gameManager = null;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.GameOver || gameManager == null)
            {
                return;
            }

            ScoreManager score = gameManager.Score;
            if (score == null)
            {
                return;
            }

            // Only push when this run beat the stored record, or when cloud is behind local best.
            bool record = gameManager.GameOver != null && gameManager.GameOver.WasRecord;
            int best = score.BestScore;
            int lastSubmitted = PlayerPrefs.GetInt(LastSubmittedKey, 0);

            if (record || best > lastSubmitted)
            {
                TrySubmit(best, promptAuth: true);
            }
        }

        private void HandleSdkData()
        {
            if (!YG2.player.auth)
            {
                return;
            }

            int localBest = ResolveLocalBest();
            int toSend = Mathf.Max(pendingScore, localBest);
            if (toSend > 0)
            {
                TrySubmit(toSend, promptAuth: false);
            }
        }

        private void FlushPending()
        {
            if (pendingScore > 0)
            {
                TrySubmit(pendingScore, promptAuth: false);
            }
        }

        private void TrySubmit(int score, bool promptAuth)
        {
            if (score <= 0)
            {
                return;
            }

            int lastSubmitted = PlayerPrefs.GetInt(LastSubmittedKey, 0);
            if (score <= lastSubmitted && pendingScore <= lastSubmitted)
            {
                pendingScore = 0;
                return;
            }

            if (!YG2.player.auth)
            {
                pendingScore = Mathf.Max(pendingScore, score);
                if (promptAuth)
                {
                    YG2.OpenAuthDialog();
                }

                return;
            }

            // Yandex rejects LB writes more often than once per second.
            if (lastSubmitTime >= 0f && Time.unscaledTime - lastSubmitTime < 1.05f)
            {
                pendingScore = Mathf.Max(pendingScore, score);
                CancelInvoke(nameof(FlushPending));
                Invoke(nameof(FlushPending), 1.1f);
                return;
            }

            int toSend = Mathf.Max(score, pendingScore);
            pendingScore = 0;
            lastSubmitTime = Time.unscaledTime;

            YG2.SetLeaderboard(LeaderboardName, toSend);
            PlayerPrefs.SetInt(LastSubmittedKey, toSend);
            PlayerPrefs.Save();
        }

        private int ResolveLocalBest()
        {
            if (gameManager != null && gameManager.Score != null)
            {
                return gameManager.Score.BestScore;
            }

            ScoreManager score = FindObjectOfType<ScoreManager>(true);
            if (score != null)
            {
                return score.BestScore;
            }

            return PlayerPrefs.GetInt(ScoreManager.BestScoreKey, 0);
        }
    }
}
