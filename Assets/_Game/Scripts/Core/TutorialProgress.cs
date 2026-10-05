using System;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// First-run tutorial status shared between the game and the platform layer.
    /// The game asmdefs cannot see PluginYG2, so the tutorial only talks to this class:
    /// the local flag lives in PlayerPrefs, and the Yandex bridge mirrors it into
    /// <c>SavesYG.tutorialDone</c>, sends the Metrica goal and keeps ads away while
    /// <see cref="IsActive"/> is set.
    /// </summary>
    public static class TutorialProgress
    {
        public const string DoneKey = "BlockPuzzle.TutorialDone";

        /// <summary>True once the player has finished the tutorial on this device or account.</summary>
        public static bool IsDone => PlayerPrefs.GetInt(DoneKey, 0) != 0;

        /// <summary>True while the tutorial is on screen. Ads of every kind must wait.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>
        /// True after the player asked to see the tutorial again ("How to play"). The next run
        /// stages the tutorial even though it is done; <see cref="ConsumeReplay"/> clears it.
        /// </summary>
        public static bool ReplayRequested { get; private set; }

        /// <summary>Raised once when the player completes the tutorial (not when it is restored).</summary>
        public static event Action Completed;

        /// <summary>Raised when <see cref="IsActive"/> flips, so ad services can react at once.</summary>
        public static event Action<bool> ActiveChanged;

        /// <summary>Raised when a save says the tutorial is already done while it is running.</summary>
        public static event Action RestoredAsDone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsActive = false;
            ReplayRequested = false;
            Completed = null;
            ActiveChanged = null;
            RestoredAsDone = null;
        }

        /// <summary>Asks for the tutorial to run again at the start of the next run.</summary>
        public static void RequestReplay() => ReplayRequested = true;

        /// <summary>True once when a replay was requested; the caller then stages the tutorial.</summary>
        public static bool ConsumeReplay()
        {
            bool requested = ReplayRequested;
            ReplayRequested = false;
            return requested;
        }

        public static void SetActive(bool active)
        {
            if (IsActive == active)
            {
                return;
            }

            IsActive = active;
            ActiveChanged?.Invoke(active);
        }

        /// <summary>The player cleared the tutorial line.</summary>
        public static void MarkDone()
        {
            bool wasDone = IsDone;
            WriteDone();
            SetActive(false);

            if (!wasDone)
            {
                Completed?.Invoke();
            }
        }

        /// <summary>
        /// A save (or a returning player's history) says the tutorial is not needed.
        /// Stops a running tutorial without counting it as a completion.
        /// </summary>
        public static void RestoreDone()
        {
            if (IsDone)
            {
                return;
            }

            WriteDone();
            if (IsActive)
            {
                RestoredAsDone?.Invoke();
                SetActive(false);
            }
        }

        private static void WriteDone()
        {
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }
    }
}
