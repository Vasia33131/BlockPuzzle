using UnityEngine;
using BlockPuzzle.Core;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Shows or hides the Yandex sticky (bottom) banner. Off after ad removal.
    /// Lives outside game asmdefs so it can reference PluginYG2 (Assembly-CSharp).
    /// </summary>
    [DefaultExecutionOrder(90)]
    public sealed class YandexStickyService : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexStickyService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexStickyService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexStickyService>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += HandleSdkData;

            if (PlayerProgress.AdsRemoved)
            {
                YG2.StickyAdActivity(false);
                return;
            }

            if (YG2.isSDKEnabled)
            {
                HandleSdkData();
            }
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= HandleSdkData;
        }

        private void HandleSdkData()
        {
            YG2.StickyAdActivity(!PlayerProgress.AdsRemoved);
        }
    }
}
