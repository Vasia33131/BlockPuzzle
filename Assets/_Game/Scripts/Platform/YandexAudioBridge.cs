using UnityEngine;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Silences the background music while an ad is open, while the SDK holds the game paused
    /// and while the tab is hidden (Yandex 1.3 / 4.7). It is event driven, because a hidden
    /// browser tab stops running <c>Update</c>; the frame check only repairs a missed event.
    /// </summary>
    public sealed class YandexAudioBridge : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexAudioBridge>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexAudioBridge));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexAudioBridge>();
        }

        private void OnEnable()
        {
            YG2.onOpenAnyAdv += Refresh;
            YG2.onCloseAnyAdv += Refresh;
            YG2.onErrorAnyAdv += Refresh;
            YG2.onPauseGame += HandlePause;
            YG2.onFocusWindowGame += HandleFocus;
            Refresh();
        }

        private void OnDisable()
        {
            YG2.onOpenAnyAdv -= Refresh;
            YG2.onCloseAnyAdv -= Refresh;
            YG2.onErrorAnyAdv -= Refresh;
            YG2.onPauseGame -= HandlePause;
            YG2.onFocusWindowGame -= HandleFocus;
        }

        private void Update() => Refresh();

        private void HandlePause(bool pause) => Refresh();

        private void HandleFocus(bool focus) => Refresh();

        private static void Refresh()
        {
            bool suspended = YG2.nowAdsShow || YG2.isPauseGame || !YG2.isFocusWindowGame;
            MusicManager.SetSuspended(suspended);
            AudioManager.SetSuspended(suspended);
        }
    }
}
