using System;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Calendar day for the meta layer (daily reward, daily tasks). The day comes from the
    /// platform server clock so that moving the device clock does not hand out rewards;
    /// Core cannot see PluginYG2, so the platform layer installs the source through
    /// <see cref="SetServerTimeSource"/>. Days switch at midnight Moscow time — most of
    /// the Yandex Games audience lives around UTC+3.
    /// </summary>
    public static class MetaClock
    {
        public const int DayOffsetHours = 3;

        private const long MillisecondsPerDay = 24L * 60L * 60L * 1000L;
        private const long DayOffsetMs = DayOffsetHours * 60L * 60L * 1000L;

        private static Func<long> serverUnixMs;
        private static bool deviceFallback;

        /// <summary>True once a server clock was installed.</summary>
        public static bool HasServerTime => serverUnixMs != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            serverUnixMs = null;
            deviceFallback = false;
        }

        /// <summary>Server Unix time in milliseconds; a value of 0 or less means "not synced yet".</summary>
        public static void SetServerTimeSource(Func<long> source)
        {
            serverUnixMs = source;
            deviceFallback = false;
        }

        /// <summary>
        /// Lets the day follow the device clock. Only for builds without a server clock
        /// (the ServerTime module is missing); <see cref="MetaProgress"/> still refuses to
        /// go back to an earlier day.
        /// </summary>
        public static void UseDeviceTimeFallback()
        {
            serverUnixMs = null;
            deviceFallback = true;
        }

        /// <summary>Day number since 1970-01-01 (Moscow midnight). False while no clock is known.</summary>
        public static bool TryGetToday(out int day)
        {
            day = 0;
            long ms;
            if (serverUnixMs != null)
            {
                try
                {
                    ms = serverUnixMs();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[MetaClock] Server time failed: {e.Message}");
                    return false;
                }
            }
            else if (deviceFallback)
            {
                ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            else
            {
                return false;
            }

            if (ms <= 0)
            {
                return false;
            }

            day = (int)((ms + DayOffsetMs) / MillisecondsPerDay);
            return true;
        }
    }
}
