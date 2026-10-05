using UnityEngine;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Keeps the unfinished run in <see cref="SavesYG.currentRun"/>. The game saves its
    /// run after every move; PlayerPrefs gets each one immediately, but the account copy
    /// is written at most once per <see cref="CloudInterval"/> — the latest run wins, the
    /// ones in between are skipped. When the tab is hidden or the app pauses the queued
    /// run is written at once, whatever the timer says.
    ///
    /// The SDK loads the account save asynchronously, so the game may already be running
    /// when it arrives; the run it holds is then offered to <see cref="RunSaveController"/>
    /// before anything local is written over it.
    /// </summary>
    [DefaultExecutionOrder(85)]
    public sealed class YandexRunCloudStore : MonoBehaviour, IRunCloudStore
    {
        /// <summary>Shortest gap between two account writes caused by moves, seconds.</summary>
        private const float CloudInterval = 7f;

        private string pending;
        private bool hasPending;
        private float lastCloudWrite = float.NegativeInfinity;

        // Before the scene, so the store is in place when GameManager.Start looks for a run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexRunCloudStore>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexRunCloudStore));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexRunCloudStore>();
        }

        private void Awake()
        {
            RunSaveController.CloudStore = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(RunSaveController.CloudStore, this))
            {
                RunSaveController.CloudStore = null;
            }
        }

        private void OnEnable() => Subscribe();

        /// <summary>
        /// Subscribes again: in the Editor YG2 clears its events in its own BeforeSceneLoad
        /// hook, which may run after ours. Data that came in before this point was already
        /// read by GameManager.Start; offering it again is harmless.
        /// </summary>
        private void Start()
        {
            Subscribe();
            if (YG2.isSDKEnabled)
            {
                HandleSdkData();
            }
        }

        private void Subscribe()
        {
            YG2.onGetSDKData -= HandleSdkData;
            YG2.onGetSDKData += HandleSdkData;
            YG2.onHideWindowGame -= HandleHidden;
            YG2.onHideWindowGame += HandleHidden;
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= HandleSdkData;
            YG2.onHideWindowGame -= HandleHidden;
        }

        private void Update()
        {
            if (hasPending && Time.realtimeSinceStartup - lastCloudWrite >= CloudInterval)
            {
                Push();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                HandleHidden();
            }
        }

        public string ReadRun()
        {
            return YG2.isSDKEnabled && YG2.saves != null ? YG2.saves.currentRun : null;
        }

        public void WriteRun(string data)
        {
            pending = data ?? string.Empty;
            hasPending = true;

            // Straight into the save object, so any other save (a purchase, a record)
            // carries the latest run too; only the write itself is throttled.
            if (YG2.isSDKEnabled && YG2.saves != null)
            {
                YG2.saves.currentRun = pending;
            }
        }

        public void Flush()
        {
            if (hasPending)
            {
                Push();
            }
        }

        private void Push()
        {
            if (!YG2.isSDKEnabled || YG2.saves == null)
            {
                return;
            }

            YG2.saves.currentRun = pending;
            hasPending = false;
            lastCloudWrite = Time.realtimeSinceStartup;
            CloudSaveGate.Request();
        }

        /// <summary>
        /// The account save is in (at init, or after a sign-in switched accounts). Its run
        /// may be newer than the one on screen; only if the game keeps its own run does
        /// the local one go over it.
        /// </summary>
        private void HandleSdkData()
        {
            if (YG2.saves == null)
            {
                return;
            }

            RunSaveController runSaves = GameManager.Instance != null ? GameManager.Instance.RunSaves : null;
            if (runSaves != null && runSaves.OfferCloudRun())
            {
                // The account run is on screen now and gets saved from there.
                hasPending = false;
                return;
            }

            if (hasPending)
            {
                YG2.saves.currentRun = pending;
            }
        }

        /// <summary>The tab is going to the background: it may never come back.</summary>
        private void HandleHidden()
        {
            RunSaveController runSaves = GameManager.Instance != null ? GameManager.Instance.RunSaves : null;
            if (runSaves != null)
            {
                runSaves.FlushNow();
            }
            else
            {
                Flush();
            }
        }
    }
}
