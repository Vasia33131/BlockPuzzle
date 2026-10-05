using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Yandex side of the first-run tutorial. Mirrors <see cref="TutorialProgress"/> into
    /// <see cref="SavesYG.tutorialDone"/> both ways and sends the <c>tutorial_done</c>
    /// Metrica goal when the Metrica module of PluginYG2 is connected.
    ///
    /// Players who already scored before the tutorial existed (a local or cloud record
    /// above zero) are treated as done, so an update never drops a returning player into
    /// the tutorial. The tutorial run itself cannot score before its line is cleared.
    /// </summary>
    [DefaultExecutionOrder(85)]
    public sealed class YandexTutorialService : MonoBehaviour
    {
        public const string MetricaGoal = "tutorial_done";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexTutorialService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexTutorialService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexTutorialService>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += HandleSdkData;
            YandexCloudProgressService.Restored += HandleSdkData;
            TutorialProgress.Completed += HandleCompleted;

            if (PlayerPrefs.GetInt(ScoreManager.BestScoreKey, 0) > 0)
            {
                TutorialProgress.RestoreDone();
            }

            if (YG2.isSDKEnabled)
            {
                HandleSdkData();
            }
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= HandleSdkData;
            YandexCloudProgressService.Restored -= HandleSdkData;
            TutorialProgress.Completed -= HandleCompleted;
        }

        private void HandleSdkData()
        {
            SavesYG saves = YG2.saves;
            if (saves == null)
            {
                return;
            }

            if (saves.tutorialDone || saves.bestScore > 0)
            {
                TutorialProgress.RestoreDone();
            }

            // A guest who finished the tutorial before the account copy arrived keeps it.
            if (TutorialProgress.IsDone && !saves.tutorialDone)
            {
                saves.tutorialDone = true;
                CloudSaveGate.Request();
            }
        }

        private static void HandleCompleted()
        {
            if (YG2.isSDKEnabled && YG2.saves != null && !YG2.saves.tutorialDone)
            {
                YG2.saves.tutorialDone = true;
                CloudSaveGate.Request();
            }

#if Metrica_yg
            YG2.MetricaSend(MetricaGoal);
#endif
        }
    }
}
