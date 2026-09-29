using UnityEngine;
using BlockPuzzle.Core;
using YG;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Bridge between <see cref="MetaProgress"/> (booster stock, coins, login streak, daily
    /// tasks) and the Yandex save, plus the server clock for <see cref="MetaClock"/>.
    ///
    /// The day comes from <c>YG2.ServerTime()</c> (PluginYG2 "ServerTime" module,
    /// <c>ServerTime_yg</c>) so that moving the device clock gives nothing. Without the
    /// module a build disables the daily reward and tasks (<see cref="MetaClock.DailyFeaturesEnabled"/>);
    /// only the editor falls back to the device clock.
    ///
    /// The save fields are written at once on every change, so any other
    /// <c>YG2.SaveProgress()</c> carries them; the cloud write itself is throttled
    /// because task progress changes on every clear.
    /// </summary>
    [DefaultExecutionOrder(81)]
    public sealed class YandexMetaProgressService : MonoBehaviour
    {
        private const float PushInterval = 4f;

        private bool dirty;
        private float lastPush = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexMetaProgressService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexMetaProgressService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexMetaProgressService>();
        }

        private void OnEnable()
        {
            InstallClock();
            YG2.onGetSDKData += HandleSdkData;
            MetaProgress.Changed += HandleMetaChanged;

            if (YG2.isSDKEnabled)
            {
                HandleSdkData();
            }
        }

        private void OnDisable()
        {
            YG2.onGetSDKData -= HandleSdkData;
            MetaProgress.Changed -= HandleMetaChanged;
            Flush();
        }

        private void Update()
        {
            if (dirty && Time.unscaledTime - lastPush >= PushInterval)
            {
                Flush();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Flush();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Flush();
            }
        }

        private static void InstallClock()
        {
#if ServerTime_yg
            MetaClock.SetServerTimeSource(() => YG2.ServerTime());
#elif UNITY_EDITOR
            Debug.LogWarning(
                "[Meta] PluginYG2 ServerTime module is not installed: in the editor the daily reward and tasks use the device clock; a build hides them.");
            MetaClock.UseDeviceTimeFallback();
#else
            // No trusted clock: leave it unset, so MetaClock.TryGetToday fails and nothing calendar-based pays out.
            Debug.LogWarning(
                "[Meta] PluginYG2 ServerTime module is not installed: daily reward and tasks are disabled.");
#endif
        }

        /// <summary>
        /// A save arrived (init, or a sign-in that switched accounts): the newer copy wins,
        /// and the result is written back so a guest's progress is not lost on first sign-in.
        /// </summary>
        private void HandleSdkData()
        {
            SavesYG saves = YG2.saves;
            if (saves == null)
            {
                return;
            }

            MetaProgress.Restore(Read(saves));
            if (Write(saves))
            {
                dirty = true;
            }

            MetaProgress.MarkReady();
            Flush();
        }

        private void HandleMetaChanged()
        {
            if (!YG2.isSDKEnabled || YG2.saves == null)
            {
                return;
            }

            if (Write(YG2.saves))
            {
                dirty = true;
            }
        }

        private void Flush()
        {
            if (!dirty || !YG2.isSDKEnabled)
            {
                return;
            }

            dirty = false;
            lastPush = Time.unscaledTime;
            CloudSaveGate.Request();
        }

        private static MetaSnapshot Read(SavesYG saves)
        {
            return new MetaSnapshot
            {
                Revision = saves.metaRevision,
                Coins = saves.coins,
                Undo = saves.boosterUndo,
                Extra = saves.boosterExtra,
                Clear = saves.boosterClear,
                DailyStreak = saves.dailyStreak,
                DailyLastClaimDay = saves.dailyLastClaimDay,
                QuestsDay = saves.questsDay,
                Quests = saves.quests,
                RunAwardedScore = saves.runAwardedScore
            };
        }

        /// <summary>Copies the local meta into the save. True when a field changed.</summary>
        private static bool Write(SavesYG saves)
        {
            MetaSnapshot local = MetaProgress.Capture();
            bool changed = false;
            changed |= Set(ref saves.metaRevision, local.Revision);
            changed |= Set(ref saves.coins, local.Coins);
            changed |= Set(ref saves.boosterUndo, local.Undo);
            changed |= Set(ref saves.boosterExtra, local.Extra);
            changed |= Set(ref saves.boosterClear, local.Clear);
            changed |= Set(ref saves.dailyStreak, local.DailyStreak);
            changed |= Set(ref saves.dailyLastClaimDay, local.DailyLastClaimDay);
            changed |= Set(ref saves.questsDay, local.QuestsDay);
            changed |= Set(ref saves.runAwardedScore, local.RunAwardedScore);

            string quests = local.Quests ?? string.Empty;
            if (saves.quests != quests)
            {
                saves.quests = quests;
                changed = true;
            }

            return changed;
        }

        private static bool Set(ref int field, int value)
        {
            if (field == value)
            {
                return false;
            }

            field = value;
            return true;
        }
    }
}
