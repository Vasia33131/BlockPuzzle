using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Shared rules for the native platform prompts (review, desktop shortcut). Both are
    /// offered only on the Game Over screen, never mid-run, never over an ad or the
    /// tutorial, and never both after the same run: whoever claims the gate first wins.
    /// </summary>
    public static class YandexPromptGate
    {
        /// <summary>Pause before a prompt opens, so the Game Over screen is seen first.</summary>
        public const float DelaySeconds = 1.2f;

        private static bool claimed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => claimed = false;

        /// <summary>Call when a run starts, so the next Game Over may offer a prompt again.</summary>
        public static void Release() => claimed = false;

        public static bool CanOpen(GameManager manager)
        {
            return !claimed
                && manager != null
                && manager.State == GameState.GameOver
                && YG2.isSDKEnabled
                && YG2.saves != null
                && !YG2.nowAdsShow
                && !TutorialProgress.IsActive;
        }

        public static void Claim() => claimed = true;
    }
}
