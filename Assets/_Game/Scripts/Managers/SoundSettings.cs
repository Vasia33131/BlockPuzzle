using System;
using UnityEngine;

namespace BlockPuzzle.Managers
{
    /// <summary>
    /// The two independent sound switches — music and sound effects — with their PlayerPrefs
    /// cache. The Yandex save mirrors them (see YandexCloudProgressService); every listener
    /// (<see cref="AudioManager"/>, <see cref="MusicManager"/>, the switches on screen)
    /// reacts to <see cref="Changed"/>, whoever made the change.
    /// </summary>
    public static class SoundSettings
    {
        public const string MusicMutedKey = "BlockPuzzle.MusicMuted";
        public const string SfxMutedKey = "BlockPuzzle.SfxMuted";

        /// <summary>The single "muted" switch of older versions; it silenced everything.</summary>
        private const string LegacyMutedKey = "BlockPuzzle.Muted";

        private static bool loaded;
        private static bool musicMuted;
        private static bool sfxMuted;

        /// <summary>Raised after either switch changed, from a tap or from the cloud save.</summary>
        public static event Action Changed;

        public static bool MusicMuted
        {
            get
            {
                Load();
                return musicMuted;
            }
        }

        public static bool SfxMuted
        {
            get
            {
                Load();
                return sfxMuted;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        public static void SetMusicMuted(bool muted) => Apply(muted, SfxMuted);

        public static void SetSfxMuted(bool muted) => Apply(MusicMuted, muted);

        /// <summary>Applies the choice that came back from the platform save.</summary>
        public static void Restore(bool music, bool sfx) => Apply(music, sfx);

        private static void Apply(bool music, bool sfx)
        {
            Load();
            if (musicMuted == music && sfxMuted == sfx)
            {
                return;
            }

            musicMuted = music;
            sfxMuted = sfx;
            Save();
            Changed?.Invoke();
        }

        /// <summary>Reads the switches; a player who had the old single switch off gets both off.</summary>
        private static void Load()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            if (PlayerPrefs.HasKey(MusicMutedKey) || PlayerPrefs.HasKey(SfxMutedKey))
            {
                musicMuted = PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
                sfxMuted = PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;
                return;
            }

            bool legacy = PlayerPrefs.GetInt(LegacyMutedKey, 0) == 1;
            musicMuted = legacy;
            sfxMuted = legacy;
            if (PlayerPrefs.HasKey(LegacyMutedKey))
            {
                Save();
                PlayerPrefs.DeleteKey(LegacyMutedKey);
                PlayerPrefs.Save();
            }
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(MusicMutedKey, musicMuted ? 1 : 0);
            PlayerPrefs.SetInt(SfxMutedKey, sfxMuted ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
