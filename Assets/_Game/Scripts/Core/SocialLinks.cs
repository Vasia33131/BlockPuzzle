using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// The "Our games" links of the settings panel and the one place that opens them. A link is
    /// only ever opened from a player's tap (<see cref="Open"/> is called by the button handler),
    /// in a new tab, and the game is never paused: only the sound is held back while the player
    /// is away, then restored when focus returns.
    /// </summary>
    public static class SocialLinks
    {
        public const string Telegram = "telegram";
        public const string YouTube = "youtube";

        /// <summary>
        /// Hard switch over the config asset. Yandex Games forbids links to other platforms, so a
        /// release build never shows or opens them, whatever <see cref="SocialLinksConfig.enabled"/> says.
        /// </summary>
        public static bool ExternalLinksAllowed => false;

        private static SocialLinksConfig config;
        private static bool configLoaded;

        /// <summary>Raised with the network id when a link is opened. Platform code forwards it to Metrica.</summary>
        public static event Action<string> Clicked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            config = null;
            configLoaded = false;
            Clicked = null;
        }

        /// <summary>Telegram address, or null when the block is off or the field is empty.</summary>
        public static string TelegramUrl => Resolve(c => c.telegramUrl);

        /// <summary>YouTube address, or null when the block is off or the field is empty.</summary>
        public static string YouTubeUrl => Resolve(c => c.youtubeUrl);

        public static bool AnyAvailable => TelegramUrl != null || YouTubeUrl != null;

        public static string UrlFor(string network)
        {
            switch (network)
            {
                case Telegram:
                    return TelegramUrl;
                case YouTube:
                    return YouTubeUrl;
                default:
                    return null;
            }
        }

        /// <summary>Opens the link of <paramref name="network"/> in a new tab. Call from a tap handler only.</summary>
        public static void Open(string network)
        {
            string url = UrlFor(network);
            if (url == null)
            {
                return;
            }

            Clicked?.Invoke(network);
            ExternalLinkWatcher.HoldSound();
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenLink_Open(url);
#else
            Application.OpenURL(url);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void OpenLink_Open(string url);
#endif

        private static string Resolve(Func<SocialLinksConfig, string> field)
        {
            if (!ExternalLinksAllowed)
            {
                return null;
            }

            if (!configLoaded)
            {
                config = Resources.Load<SocialLinksConfig>(SocialLinksConfig.ResourcePath);
                configLoaded = true;
            }

            if (config == null || !config.enabled)
            {
                return null;
            }

            string url = field(config);
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            url = url.Trim();
            bool web = url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            return web ? url : null;
        }
    }

    /// <summary>
    /// Silences the game while the player is in the opened tab and brings the sound back when focus
    /// returns. If the page never loses focus (the tab was blocked) the sound comes back after a
    /// few seconds, so it can never stay off.
    /// </summary>
    internal sealed class ExternalLinkWatcher : MonoBehaviour
    {
        private const float GiveUpSeconds = 4f;

        private static ExternalLinkWatcher instance;

        private bool heldByUs;
        private bool focusLost;
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public static void HoldSound()
        {
            if (instance == null)
            {
                var go = new GameObject(nameof(ExternalLinkWatcher));
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ExternalLinkWatcher>();
            }

            instance.Hold();
        }

        private void Hold()
        {
            if (!AudioListener.pause)
            {
                AudioListener.pause = true;
                heldByUs = true;
            }

            focusLost = false;
            deadline = Time.unscaledTime + GiveUpSeconds;
            enabled = heldByUs;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!heldByUs)
            {
                return;
            }

            if (!focus)
            {
                focusLost = true;
            }
            else if (focusLost)
            {
                Release();
            }
        }

        private void Update()
        {
            if (heldByUs && !focusLost && Time.unscaledTime >= deadline)
            {
                Release();
            }
        }

        private void Release()
        {
            if (heldByUs)
            {
                AudioListener.pause = false;
            }

            heldByUs = false;
            focusLost = false;
            enabled = false;
        }
    }
}
