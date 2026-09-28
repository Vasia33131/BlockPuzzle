using UnityEngine;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// The one door to <c>YG2.SaveProgress()</c>. Every platform service used to write the cloud
    /// save on its own, so a level win (stars, XP, coins, record) or the first frames of a session
    /// produced a burst of writes. Here the requests are collected, and one write leaves at most
    /// once per <see cref="MinInterval"/> (Yandex asks for no more than one save every few seconds).
    ///
    /// The services still mutate <see cref="YG2.saves"/> at once, so whatever is written later
    /// carries the latest state; only the moment of the write moves. A request made while the
    /// window is closed is written as soon as it opens. When the tab is hidden or the app is
    /// paused the queued write goes out at once (the tab may never come back), but never closer
    /// than <see cref="HardFloor"/> to the previous write.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class CloudSaveGate : MonoBehaviour
    {
        /// <summary>Shortest gap between two cloud writes, seconds.</summary>
        public const float MinInterval = 5.5f;

        /// <summary>Shortest gap for the write forced by a hidden tab, seconds.</summary>
        public const float HardFloor = 1f;

        private static CloudSaveGate instance;

        private bool pending;
        private float lastWrite = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate() => Ensure();

        /// <summary>Asks for a cloud write. Coalesced and rate limited; safe to call from anywhere, any number of times.</summary>
        public static void Request()
        {
            CloudSaveGate gate = Ensure();
            if (gate != null)
            {
                gate.pending = true;
            }
        }

        /// <summary>Writes a queued request right away (subject to <see cref="HardFloor"/>): the game is about to go away.</summary>
        public static void FlushNow()
        {
            if (instance != null)
            {
                instance.Write(HardFloor);
            }
        }

        private static CloudSaveGate Ensure()
        {
            if (instance != null || !Application.isPlaying)
            {
                return instance;
            }

            var go = new GameObject(nameof(CloudSaveGate));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CloudSaveGate>();
            return instance;
        }

        private void OnEnable()
        {
            instance = this;
            YG2.onHideWindowGame -= HandleHidden;
            YG2.onHideWindowGame += HandleHidden;
        }

        /// <summary>Subscribes again: in the Editor YG2 clears its events in its own BeforeSceneLoad hook.</summary>
        private void Start()
        {
            YG2.onHideWindowGame -= HandleHidden;
            YG2.onHideWindowGame += HandleHidden;
        }

        private void OnDisable()
        {
            YG2.onHideWindowGame -= HandleHidden;
            if (instance == this)
            {
                instance = null;
            }
        }

        // End of the frame, so the several services that ask in the same frame share one write.
        private void LateUpdate()
        {
            if (pending)
            {
                Write(MinInterval);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Write(HardFloor);
            }
        }

        private void HandleHidden() => Write(HardFloor);

        private void Write(float minGap)
        {
            if (!pending || !YG2.isSDKEnabled || YG2.saves == null)
            {
                return;
            }

            if (Time.realtimeSinceStartup - lastWrite < minGap)
            {
                return;
            }

            pending = false;
            lastWrite = Time.realtimeSinceStartup;
            YG2.SaveProgress();
        }
    }
}
