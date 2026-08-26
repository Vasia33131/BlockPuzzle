using System;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Mirrors the durable progress between PlayerPrefs and the Yandex save
    /// (requirements 1.9, 1.11, 1.13.3): no-ads, owned palettes and figure packs, the
    /// selected palette, the record and the sound switch.
    ///
    /// PlayerPrefs stays as the local cache and as the guest fallback, but the account
    /// copy is what makes a purchase reappear on another device. Restoring happens on
    /// <see cref="YG2.onGetSDKData"/> — that is after the cloud save is parsed and
    /// before the shop or the sticky banner is allowed to act on it.
    /// </summary>
    [DefaultExecutionOrder(80)]
    public sealed class YandexCloudProgressService : MonoBehaviour
    {
        /// <summary>True once the account copy was merged into the local progress.</summary>
        public static bool IsRestored { get; private set; }

        /// <summary>
        /// Raised every time a save was merged in — the first one at init, and any later
        /// one, for instance after a sign-in switched the account. The shop and the
        /// sticky banner listen so they never act on a stale purchase list.
        /// </summary>
        public static event Action Restored;

        private ScoreManager scoreManager;
        private AudioManager audioManager;
        private int cloudBestScore;
        private bool hasCloudData;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsRestored = false;
            Restored = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexCloudProgressService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexCloudProgressService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexCloudProgressService>();
        }

        private void OnEnable()
        {
            YG2.onGetSDKData += HandleSdkData;
            PlayerProgress.Changed += PushProgress;
            BindManagers();

            if (YG2.isSDKEnabled)
            {
                HandleSdkData();
            }
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= HandleSdkData;
            PlayerProgress.Changed -= PushProgress;
            UnbindManagers();
        }

        private void Update()
        {
            if (scoreManager == null || audioManager == null)
            {
                BindManagers();
            }
        }

        /// <summary>
        /// A new save arrived (init, or a sign-in that switched accounts). Merge it into
        /// the local progress, then push the merge back so a purchase made as a guest is
        /// not lost and a failed cloud write is repaired.
        /// </summary>
        private void HandleSdkData()
        {
            SavesYG saves = YG2.saves;
            if (saves == null)
            {
                return;
            }

            hasCloudData = true;
            cloudBestScore = saves.bestScore;

            PlayerProgress.Restore(saves.adsRemoved, saves.ownedThemes, saves.ownedPacks, saves.themeId);
            ApplyCloudBestScore();

            // A save that was never written holds default flags, so it must not
            // overwrite a choice the player already made locally.
            if (saves.idSave > 0)
            {
                ApplyCloudMuted(saves.muted);
            }

            GameTheme.ApplyFromProgress();

            IsRestored = true;
            PushProgress();
            Restored?.Invoke();
        }

        /// <summary>Writes the local progress into the save. Silent when nothing changed.</summary>
        private void PushProgress()
        {
            if (!YG2.isSDKEnabled)
            {
                return;
            }

            SavesYG saves = YG2.saves;
            if (saves == null || !ApplyToSaves(saves))
            {
                return;
            }

            YG2.SaveProgress();
        }

        private bool ApplyToSaves(SavesYG saves)
        {
            bool changed = false;

            if (PlayerProgress.AdsRemoved && !saves.adsRemoved)
            {
                saves.adsRemoved = true;
                changed = true;
            }

            changed |= Write(ref saves.themeId, PlayerProgress.ThemeId);
            changed |= Write(ref saves.ownedThemes, PlayerProgress.OwnedThemesCsv);
            changed |= Write(ref saves.ownedPacks, PlayerProgress.OwnedPacksCsv);

            int best = ResolveLocalBestScore();
            if (best > saves.bestScore)
            {
                saves.bestScore = best;
                changed = true;
            }

            bool muted = ResolveLocalMuted();
            if (saves.muted != muted)
            {
                saves.muted = muted;
                changed = true;
            }

            return changed;
        }

        private static bool Write(ref string field, string value)
        {
            value = value ?? string.Empty;
            if (field == value)
            {
                return false;
            }

            field = value;
            return true;
        }

        private void ApplyCloudBestScore()
        {
            if (cloudBestScore <= 0)
            {
                return;
            }

            // The manager may not exist yet; PlayerPrefs is what it reads on Awake.
            if (cloudBestScore > PlayerPrefs.GetInt(ScoreManager.BestScoreKey, 0))
            {
                PlayerPrefs.SetInt(ScoreManager.BestScoreKey, cloudBestScore);
                PlayerPrefs.Save();
            }

            scoreManager?.RestoreBestScore(cloudBestScore);
        }

        private void ApplyCloudMuted(bool muted)
        {
            PlayerPrefs.SetInt(AudioManager.MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            audioManager?.RestoreMuted(muted);
        }

        private int ResolveLocalBestScore()
        {
            return scoreManager != null
                ? scoreManager.BestScore
                : PlayerPrefs.GetInt(ScoreManager.BestScoreKey, 0);
        }

        private bool ResolveLocalMuted()
        {
            return audioManager != null
                ? audioManager.IsMuted
                : PlayerPrefs.GetInt(AudioManager.MutedKey, 0) == 1;
        }

        private void BindManagers()
        {
            if (scoreManager == null)
            {
                ScoreManager score = GameManager.Instance != null
                    ? GameManager.Instance.Score
                    : FindObjectOfType<ScoreManager>(true);

                if (score != null)
                {
                    scoreManager = score;
                    scoreManager.BestScoreSaved += HandleBestScoreSaved;
                    if (hasCloudData)
                    {
                        ApplyCloudBestScore();
                    }
                }
            }

            if (audioManager != null)
            {
                return;
            }

            AudioManager audio = GameManager.Instance != null
                ? GameManager.Instance.Audio
                : FindObjectOfType<AudioManager>(true);

            if (audio == null)
            {
                return;
            }

            audioManager = audio;
            audioManager.MutedChanged += HandleMutedChanged;
        }

        private void UnbindManagers()
        {
            if (scoreManager != null)
            {
                scoreManager.BestScoreSaved -= HandleBestScoreSaved;
                scoreManager = null;
            }

            if (audioManager != null)
            {
                audioManager.MutedChanged -= HandleMutedChanged;
                audioManager = null;
            }
        }

        private void HandleBestScoreSaved(int best) => PushProgress();

        private void HandleMutedChanged(bool muted) => PushProgress();
    }
}
