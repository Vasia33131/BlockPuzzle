using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using BlockPuzzle.Core;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Feeds <see cref="PlayerProfile"/> from the Yandex player: name, and the photo of a
    /// signed-in player (downloaded once per address and kept in memory). A guest, a
    /// missing photo, a timeout or a failed download all end on the default character
    /// <c>Resources/Avatars/DefaultAvatar</c>; without that file the view draws its own "?" disc.
    ///
    /// Everything is reloaded on <see cref="YG2.onGetSDKData"/>, which fires after init and
    /// again after a sign-in, so the avatar changes right after the player authorizes.
    /// </summary>
    [DefaultExecutionOrder(82)]
    public sealed class PlayerAvatarService : MonoBehaviour
    {
        private const string DefaultAvatarPath = "Avatars/DefaultAvatar";
        private const int PhotoTimeoutSeconds = 10;

        private static readonly Dictionary<string, Texture2D> photoCache = new Dictionary<string, Texture2D>();

        private Texture2D defaultAvatar;
        private bool defaultLoaded;
        private Coroutine loading;
        private string loadingUrl;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            photoCache.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<PlayerAvatarService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(PlayerAvatarService));
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerAvatarService>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += Refresh;
            PlayerProfile.AuthRequested += HandleAuthRequested;

            // Until the SDK answers the player is a guest with the default character.
            Publish(false, null, DefaultAvatar());
            if (YG2.isSDKEnabled)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= Refresh;
            PlayerProfile.AuthRequested -= HandleAuthRequested;
            StopLoading();
        }

        private void HandleAuthRequested()
        {
            if (!YG2.player.auth)
            {
                YG2.OpenAuthDialog();
            }
        }

        private void Refresh()
        {
            YG2.PlayerData player = YG2.player;
            if (player == null || !player.auth)
            {
                StopLoading();
                Publish(false, null, DefaultAvatar());
                return;
            }

            string url = player.photo;
            if (!IsWebAddress(url))
            {
                StopLoading();
                Publish(true, player.name, DefaultAvatar());
                return;
            }

            if (photoCache.TryGetValue(url, out Texture2D cached) && cached != null)
            {
                StopLoading();
                Publish(true, player.name, cached);
                return;
            }

            // The name is known at once; the default character stands in until the photo lands.
            Publish(true, player.name, DefaultAvatar());
            if (loading != null && loadingUrl == url)
            {
                return;
            }

            StopLoading();
            loadingUrl = url;
            loading = StartCoroutine(LoadPhoto(url));
        }

        private IEnumerator LoadPhoto(string url)
        {
            Texture2D photo = null;
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = PhotoTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    photo = DownloadHandlerTexture.GetContent(request);
                }
                else
                {
                    Debug.LogWarning($"[Avatar] Photo not loaded ({request.result}): {request.error}");
                }
            }

            loading = null;
            loadingUrl = null;

            // The player may have signed out or switched account while the photo was on its way.
            if (!YG2.player.auth || YG2.player.photo != url)
            {
                yield break;
            }

            if (photo != null)
            {
                photo.wrapMode = TextureWrapMode.Clamp;
                photoCache[url] = photo;
                PlayerProfile.SetAvatar(photo);
            }
            else
            {
                PlayerProfile.SetAvatar(DefaultAvatar());
            }
        }

        private void StopLoading()
        {
            if (loading != null)
            {
                StopCoroutine(loading);
            }

            loading = null;
            loadingUrl = null;
        }

        private Texture2D DefaultAvatar()
        {
            if (!defaultLoaded)
            {
                defaultLoaded = true;
                defaultAvatar = Resources.Load<Texture2D>(DefaultAvatarPath);
            }

            return defaultAvatar;
        }

        private static void Publish(bool authorized, string name, Texture2D avatar)
        {
            PlayerProfile.Set(authorized, name == InfoYG.ANONYMOUS ? null : name, avatar);
        }

        private static bool IsWebAddress(string url)
        {
            return !string.IsNullOrEmpty(url)
                && (url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
