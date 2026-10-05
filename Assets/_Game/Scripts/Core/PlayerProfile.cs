using System;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Who the player is, as far as the UI is concerned: authorization state, name and avatar.
    /// Lives in Core so views can read it without a dependency on YG; the platform layer
    /// (<c>PlayerAvatarService</c>) fills it in and answers <see cref="AuthRequested"/>.
    /// </summary>
    public static class PlayerProfile
    {
        private static string rawName = string.Empty;

        /// <summary>True when the player is signed in to Yandex.</summary>
        public static bool IsAuthorized { get; private set; }

        /// <summary>
        /// Avatar picture: the Yandex photo, or the default character. Null when neither is
        /// available — the view then draws a "?" disc in the theme colour.
        /// </summary>
        public static Texture2D Avatar { get; private set; }

        /// <summary>Yandex name, or the localized "Player" for guests and hidden names.</summary>
        public static string DisplayName =>
            IsAuthorized && !string.IsNullOrWhiteSpace(rawName) ? rawName.Trim() : GameLocalization.PlayerFallbackName;

        /// <summary>Raised whenever the name, the picture or the authorization state changed.</summary>
        public static event Action Changed;

        /// <summary>Raised when the player taps the avatar while signed out. Platform code opens the dialog.</summary>
        public static event Action AuthRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Static state outlives a play session when the domain is not reloaded.
            Changed = null;
            AuthRequested = null;
            IsAuthorized = false;
            rawName = string.Empty;
            Avatar = null;
        }

        public static void Set(bool authorized, string name, Texture2D avatar)
        {
            IsAuthorized = authorized;
            rawName = name ?? string.Empty;
            Avatar = avatar;
            Changed?.Invoke();
        }

        /// <summary>Updates only the picture (the photo arrived after the name).</summary>
        public static void SetAvatar(Texture2D avatar)
        {
            if (Avatar == avatar)
            {
                return;
            }

            Avatar = avatar;
            Changed?.Invoke();
        }

        public static void RequestAuth() => AuthRequested?.Invoke();
    }
}
