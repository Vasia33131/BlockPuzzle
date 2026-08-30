using UnityEngine;
using BlockPuzzle.Core;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Pushes <c>ysdk.environment.i18n.lang</c> into <see cref="GameLocalization"/> at
    /// launch. Reads the SDK directly so PluginYG AutoTranslate cannot remap a CIS
    /// code to English before the UI sees it. Copy is hand-written (ru/en only).
    /// </summary>
    [DefaultExecutionOrder(-700)]
    public sealed class YandexLanguageService : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexLanguageService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexLanguageService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexLanguageService>();
        }

        private void OnEnable()
        {
            YG2.onCorrectLang += HandleLanguage;
            YG2.onSwitchLang += HandleLanguage;
            YG2.onGetSDKData += HandleSdkData;
            Apply();
        }

        private void OnDisable()
        {
            YG2.onCorrectLang -= HandleLanguage;
            YG2.onSwitchLang -= HandleLanguage;
            YG2.onGetSDKData -= HandleSdkData;
        }

        private static void HandleLanguage(string lang)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(lang))
            {
                GameLocalization.ApplyPlatformLanguage(lang);
                return;
            }
#endif
            // Player builds always re-read the portal. PluginYG AutoTranslate may
            // already have rewritten YG2.lang (and the onSwitchLang payload) to en.
            Apply();
        }

        private static void HandleSdkData() => Apply();

        private static void Apply()
        {
            GameLocalization.ApplyPlatformLanguage(ReadPlatformLanguage());
        }

        /// <summary>
        /// Raw portal language, not the AutoTranslate remap. In the editor the YG2
        /// simulation field is the stand-in for the SDK.
        /// </summary>
        private static string ReadPlatformLanguage()
        {
#if UNITY_EDITOR
            return YG2.lang;
#else
            if (YG2.iPlatform == null)
            {
                return YG2.lang;
            }

            string platformLang = YG2.iPlatform.GetLanguage();
            return string.IsNullOrEmpty(platformLang) ? YG2.lang : platformLang;
#endif
        }
    }
}
