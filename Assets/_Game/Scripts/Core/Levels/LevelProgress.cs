using System;
using System.Text;
using UnityEngine;

namespace BlockPuzzle.Levels
{
    /// <summary>
    /// How far the player got in the campaign: the highest open level and the best stars of every
    /// level. Lives in Core like <see cref="BlockPuzzle.Core.MetaProgress"/>: PlayerPrefs is the local
    /// cache, the platform layer mirrors every <see cref="Changed"/> into the Yandex save and feeds
    /// the account copy back through <see cref="Restore"/>.
    ///
    /// Progress only ever grows, so a merge keeps the maximum of both copies (open level and stars
    /// per level) — the same "never lose progress" rule as the best score.
    /// </summary>
    public static class LevelProgress
    {
        public const int MaxStars = 3;

        private const string Prefix = "BlockPuzzle.Levels.";
        private const string UnlockedKey = Prefix + "Unlocked";
        private const string StarsKey = Prefix + "Stars";

        /// <summary>Best stars by level number (index 0 is unused).</summary>
        private static readonly int[] stars = new int[LevelDatabase.LevelCount + 1];

        /// <summary>Best stars of all 100 levels added up: the total the level map shows next to the earned stars.</summary>
        public const int MaxTotalStars = LevelDatabase.LevelCount * MaxStars;

        /// <summary>Highest level the player may start, 1-based.</summary>
        public static int Unlocked { get; private set; } = 1;

        /// <summary>True once the last level of the campaign has been passed.</summary>
        public static bool IsCampaignComplete => stars[LevelDatabase.LevelCount] > 0;

        /// <summary>Raised after any change (local or restored), for the UI and the cloud mirror.</summary>
        public static event Action Changed;

        /// <summary>Best stars of all levels added up.</summary>
        public static int TotalStars
        {
            get
            {
                int total = 0;
                for (int i = 1; i < stars.Length; i++)
                {
                    total += stars[i];
                }

                return total;
            }
        }

        /// <summary>
        /// Best stars of every level, one digit per level starting with level 1 ("32001").
        /// Trailing zeros are dropped. Written into the cloud save.
        /// </summary>
        public static string StarsText
        {
            get
            {
                int last = 0;
                for (int i = stars.Length - 1; i >= 1; i--)
                {
                    if (stars[i] > 0)
                    {
                        last = i;
                        break;
                    }
                }

                var builder = new StringBuilder(last);
                for (int i = 1; i <= last; i++)
                {
                    builder.Append((char)('0' + stars[i]));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// The local stars and <paramref name="other"/> (a stars text from the account save) merged
        /// per level by maximum, in the save format. Writing this into the save can never lower a level.
        /// </summary>
        public static string MergedStarsText(string other)
        {
            string local = StarsText;
            if (string.IsNullOrEmpty(other))
            {
                return local;
            }

            int length = Mathf.Min(Mathf.Max(local.Length, other.Length), stars.Length - 1);
            var builder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int mine = i < local.Length ? local[i] - '0' : 0;
                int theirs = i < other.Length ? other[i] - '0' : 0;
                builder.Append((char)('0' + Mathf.Clamp(Mathf.Max(mine, theirs), 0, MaxStars)));
            }

            return builder.ToString().TrimEnd('0');
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            // Static state outlives a play session when the domain is not reloaded.
            Changed = null;
            Load();
        }

        public static void Load()
        {
            Array.Clear(stars, 0, stars.Length);
            Unlocked = 1;
            Merge(PlayerPrefs.GetInt(UnlockedKey, 1), PlayerPrefs.GetString(StarsKey, string.Empty));
        }

        /// <summary>Best stars (0 = never passed) of a level by its 1-based number.</summary>
        public static int GetStars(int level) => level >= 1 && level < stars.Length ? stars[level] : 0;

        public static bool IsUnlocked(int level) => level >= 1 && level <= Unlocked;

        /// <summary>
        /// Records a passed level: keeps the better stars and opens the next level.
        /// True when the stars improved (a first pass counts).
        /// </summary>
        public static bool RecordWin(int level, int earnedStars)
        {
            if (level < 1 || level >= stars.Length)
            {
                return false;
            }

            earnedStars = Mathf.Clamp(earnedStars, 1, MaxStars);
            bool improved = earnedStars > stars[level];
            bool opened = level + 1 > Unlocked && Unlocked < LevelDatabase.LevelCount;
            if (!improved && !opened)
            {
                return false;
            }

            stars[level] = Mathf.Max(stars[level], earnedStars);
            Unlocked = Mathf.Clamp(Mathf.Max(Unlocked, level + 1), 1, LevelDatabase.LevelCount);
            Save();
            return improved;
        }

        /// <summary>
        /// Merges the account copy by maximum. True when the local progress changed; the platform
        /// layer then pushes the merge back, so a level passed as a guest is not lost.
        /// </summary>
        public static bool Restore(int cloudUnlocked, string cloudStars)
        {
            if (!Merge(cloudUnlocked, cloudStars))
            {
                return false;
            }

            WritePrefs();
            Changed?.Invoke();
            return true;
        }

        private static bool Merge(int unlocked, string starsText)
        {
            bool changed = false;

            if (!string.IsNullOrEmpty(starsText))
            {
                int count = Mathf.Min(starsText.Length, stars.Length - 1);
                for (int i = 0; i < count; i++)
                {
                    int value = starsText[i] - '0';
                    if (value >= 1 && value <= MaxStars && value > stars[i + 1])
                    {
                        stars[i + 1] = value;
                        changed = true;
                    }
                }
            }

            // A passed level always opens the next one, whatever the stored counter says.
            int highest = 1;
            for (int i = stars.Length - 1; i >= 1; i--)
            {
                if (stars[i] > 0)
                {
                    highest = Mathf.Min(i + 1, LevelDatabase.LevelCount);
                    break;
                }
            }

            int merged = Mathf.Clamp(Mathf.Max(Unlocked, unlocked, highest), 1, LevelDatabase.LevelCount);
            if (merged != Unlocked)
            {
                Unlocked = merged;
                changed = true;
            }

            return changed;
        }

        private static void Save()
        {
            WritePrefs();
            Changed?.Invoke();
        }

        private static void WritePrefs()
        {
            PlayerPrefs.SetInt(UnlockedKey, Unlocked);
            PlayerPrefs.SetString(StarsKey, StarsText);
            PlayerPrefs.Save();
        }
    }
}
