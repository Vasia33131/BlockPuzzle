using System;
using System.Collections;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Counts sessions and offers the desktop shortcut (PluginYG2 "GameLabel" module,
    /// <c>GameLabel_yg</c>).
    ///
    /// A session is one launch with the save loaded. The offer is made on a Game Over
    /// screen once two earlier sessions have ended (so from the third launch on), never
    /// on the player's first day, at most once per <see cref="MinDaysBetweenPrompts"/>
    /// days, and only when the platform allows it (<c>YG2.gameLabelCanShow</c>).
    /// Lives outside game asmdefs so it can reference PluginYG2 (Assembly-CSharp).
    /// </summary>
    [DefaultExecutionOrder(116)]
    public sealed class YandexShortcutService : MonoBehaviour
    {
        /// <summary>Sessions that must have ended before the first offer.</summary>
        public const int SessionsBeforeAsk = 2;

        public const int MinDaysBetweenPrompts = 3;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private GameManager gameManager;
        private bool sessionCounted;
        private bool currentRunHandled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexShortcutService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexShortcutService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexShortcutService>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += HandleSdkData;
            TryBind();

            if (YG2.isSDKEnabled)
            {
                HandleSdkData();
            }
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

        private static int Today => (int)(DateTime.UtcNow - Epoch).TotalDays;

        /// <summary>The save is loaded: this launch counts as one session, once.</summary>
        private void HandleSdkData()
        {
            SavesYG saves = YG2.saves;
            if (sessionCounted || saves == null)
            {
                return;
            }

            sessionCounted = true;
            saves.sessionsCount++;
            if (saves.firstSessionDay <= 0)
            {
                saves.firstSessionDay = Today;
            }

            CloudSaveGate.Request();
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
            currentRunHandled = false;
            YandexPromptGate.Release();
            StopAllCoroutines();
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.GameOver || currentRunHandled)
            {
                return;
            }

            currentRunHandled = true;
            if (IsDue())
            {
                StartCoroutine(AskAfterDelay());
            }
        }

        private static bool IsDue()
        {
            SavesYG saves = YG2.isSDKEnabled ? YG2.saves : null;
            if (saves == null || saves.sessionsCount <= SessionsBeforeAsk)
            {
                return false;
            }

            int today = Today;
            if (saves.firstSessionDay <= 0 || today <= saves.firstSessionDay)
            {
                return false;
            }

            return saves.lastShortcutPromptDay <= 0
                || today - saves.lastShortcutPromptDay >= MinDaysBetweenPrompts;
        }

        private IEnumerator AskAfterDelay()
        {
            // Review has the same slot and goes first; its own delay is the same, so wait a bit longer.
            yield return new WaitForSecondsRealtime(YandexPromptGate.DelaySeconds + 0.2f);

            if (!YandexPromptGate.CanOpen(gameManager) || !IsDue())
            {
                yield break;
            }

#if GameLabel_yg
            if (!YG2.gameLabelCanShow)
            {
                yield break;
            }

            YandexPromptGate.Claim();
            YG2.saves.lastShortcutPromptDay = Today;
            CloudSaveGate.Request();
            YG2.GameLabelShowDialog();
#endif
        }
    }
}
