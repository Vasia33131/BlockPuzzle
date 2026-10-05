using System;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>Where a grant of experience came from.</summary>
    public enum XpReason
    {
        EndlessRun,
        LevelComplete,
        Other
    }

    /// <summary>
    /// Player experience and level. Lives in Core like <see cref="MetaProgress"/>: PlayerPrefs is
    /// the local cache, the platform layer mirrors every <see cref="Changed"/> into the Yandex
    /// save and feeds the account copy back through <see cref="Restore"/>.
    ///
    /// Experience only ever grows, so the merge keeps the copy that is further along
    /// (higher level, then higher experience within it) — the same "never lose progress"
    /// rule as the best score.
    /// </summary>
    public static class PlayerLevel
    {
        /// <summary>Experience needed to leave level 1.</summary>
        public const int BaseXpToNext = 100;

        /// <summary>Every further level asks this much more than the one before.</summary>
        public const int XpToNextStep = 50;

        /// <summary>An endless run pays its score divided by this…</summary>
        public const int ScorePerXp = 100;

        /// <summary>…but never less than this (a run that scored anything at all).</summary>
        public const int MinRunXp = 5;

        public const int LevelCompleteBaseXp = 50;
        public const int LevelCompleteXpPerStar = 10;
        public const int MaxStars = 3;

        /// <summary>Guards the level-up loop against a corrupted save.</summary>
        public const int MaxLevel = 999;

        private const string Prefix = "BlockPuzzle.Player.";
        private const string LevelKey = Prefix + "Level";
        private const string XpKey = Prefix + "Xp";

        private static int runAwardedScore;
        private static int runAwardedXp;

        /// <summary>Current level, 1-based.</summary>
        public static int Level { get; private set; } = 1;

        /// <summary>Experience gathered towards the next level (0 … <see cref="XpToNext"/> - 1).</summary>
        public static int Xp { get; private set; }

        /// <summary>Experience the current level needs in total.</summary>
        public static int XpToNext => XpForLevel(Level);

        /// <summary>Progress bar fill of the current level, 0…1.</summary>
        public static float Progress => XpToNext > 0 ? Mathf.Clamp01((float)Xp / XpToNext) : 1f;

        /// <summary>Raised after any change (local or restored), for the UI and the cloud mirror.</summary>
        public static event Action Changed;

        /// <summary>Raised once per grant that crossed at least one level border, with the level reached.</summary>
        public static event Action<int> LevelUp;

        /// <summary>Experience a level asks to reach the next one: 100 + 50 × (level - 1).</summary>
        public static int XpForLevel(int level) => BaseXpToNext + XpToNextStep * (Mathf.Max(1, level) - 1);

        /// <summary>Experience of a finished endless run.</summary>
        public static int XpForRun(int score) => score > 0 ? Mathf.Max(MinRunXp, score / ScorePerXp) : 0;

        /// <summary>Experience of a passed level: 50 + 10 per star.</summary>
        public static int XpForLevelComplete(int stars) =>
            LevelCompleteBaseXp + LevelCompleteXpPerStar * Mathf.Clamp(stars, 0, MaxStars);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            // Static state outlives a play session when the domain is not reloaded.
            Changed = null;
            LevelUp = null;
            runAwardedScore = 0;
            runAwardedXp = 0;
            Load();
        }

        public static void Load()
        {
            Apply(PlayerPrefs.GetInt(LevelKey, 1), PlayerPrefs.GetInt(XpKey, 0));
        }

        /// <summary>
        /// Grants experience. Returns the amount added. Levels are called from here too:
        /// <c>AddXp(PlayerLevel.XpForLevelComplete(stars), XpReason.LevelComplete)</c>.
        /// </summary>
        public static int AddXp(int amount, XpReason reason)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int startLevel = Level;
            long xp = (long)Xp + amount;
            while (Level < MaxLevel && xp >= XpToNext)
            {
                xp -= XpToNext;
                Level++;
            }

            Xp = Level >= MaxLevel ? Mathf.Min((int)Math.Min(xp, int.MaxValue), XpToNext - 1) : (int)xp;
            WritePrefs();
            Changed?.Invoke();

            if (Level > startLevel)
            {
                LevelUp?.Invoke(Level);
            }

            return amount;
        }

        /// <summary>Called when a new run starts, or the player leaves for the menu: nothing of it has been paid yet.</summary>
        public static void BeginRun()
        {
            runAwardedScore = 0;
            runAwardedXp = 0;
        }

        /// <summary>
        /// Pays an endless run out (Game Over, or a restart from pause). Only the part not paid
        /// before counts, so a continue after Game Over does not pay the minimum twice.
        /// Returns the experience added.
        /// </summary>
        public static int AwardRunXp(int score)
        {
            if (score <= runAwardedScore)
            {
                return 0;
            }

            int total = XpForRun(score);
            int gained = total - runAwardedXp;
            runAwardedScore = score;
            runAwardedXp = total;
            return AddXp(gained, XpReason.EndlessRun);
        }

        /// <summary>Convenience for the levels mode.</summary>
        public static int AwardLevelComplete(int stars)
        {
            return AddXp(XpForLevelComplete(stars), XpReason.LevelComplete);
        }

        /// <summary>
        /// Merges the account copy. The copy that is further along wins; true when the cloud
        /// copy was taken. When the local copy wins the platform layer pushes it back.
        /// </summary>
        public static bool Restore(int cloudLevel, int cloudXp)
        {
            cloudLevel = Mathf.Clamp(cloudLevel, 1, MaxLevel);
            cloudXp = Mathf.Max(0, cloudXp);
            if (cloudLevel < Level || (cloudLevel == Level && cloudXp <= Xp))
            {
                return false;
            }

            Apply(cloudLevel, cloudXp);
            WritePrefs();
            Changed?.Invoke();
            return true;
        }

        private static void Apply(int level, int xp)
        {
            Level = Mathf.Clamp(level, 1, MaxLevel);
            Xp = Mathf.Clamp(xp, 0, XpToNext - 1);
        }

        private static void WritePrefs()
        {
            PlayerPrefs.SetInt(LevelKey, Level);
            PlayerPrefs.SetInt(XpKey, Xp);
            PlayerPrefs.Save();
        }
    }
}
