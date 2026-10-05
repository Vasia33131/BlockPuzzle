using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>A daily task of today and how far the player got.</summary>
    public sealed class DailyQuest
    {
        public DailyQuest(QuestDef def, int progress, bool done)
        {
            Def = def;
            Progress = Mathf.Clamp(progress, 0, def.Target);
            Done = done || Progress >= def.Target;
        }

        public QuestDef Def { get; }
        public int Progress { get; internal set; }
        public bool Done { get; internal set; }
        public float Ratio => Def.Target > 0 ? Mathf.Clamp01((float)Progress / Def.Target) : 1f;
    }

    /// <summary>Plain copy of the meta state, moved between PlayerPrefs, the Yandex save and Core.</summary>
    public sealed class MetaSnapshot
    {
        public int Revision;
        public int Coins;
        public int Undo;
        public int Extra;
        public int Clear;
        public int DailyStreak;
        public int DailyLastClaimDay;
        public int QuestsDay;
        public string Quests;
        public int RunAwardedScore;
    }

    /// <summary>
    /// Meta layer over the endless mode: booster stock, coins, the 7-day login reward and
    /// the daily tasks. Lives in Core like <see cref="PlayerProgress"/>: PlayerPrefs is the
    /// local cache, the platform layer mirrors every <see cref="Changed"/> into the Yandex
    /// save and feeds the account copy back through <see cref="Restore"/>.
    ///
    /// Unlike purchases, stock and coins are spent, so a union merge would duplicate them.
    /// Every local change bumps <see cref="Revision"/> instead, and the copy with the higher
    /// revision wins.
    ///
    /// Nothing that depends on the calendar runs before <see cref="IsReady"/>: the account
    /// copy must be merged first, and the day comes from <see cref="MetaClock"/>.
    /// </summary>
    public static class MetaProgress
    {
        public const int DailyCycleLength = 7;
        public const int QuestsPerDay = 3;
        public const int ScorePerCoin = 100;
        public const int MaxBoosterStock = 99;

        private const string Prefix = "BlockPuzzle.Meta.";
        private const string RevisionKey = Prefix + "Revision";
        private const string CoinsKey = Prefix + "Coins";
        private const string UndoKey = Prefix + "Undo";
        private const string ExtraKey = Prefix + "Extra";
        private const string ClearKey = Prefix + "Clear";
        private const string DailyStreakKey = Prefix + "DailyStreak";
        private const string DailyLastClaimKey = Prefix + "DailyLastClaim";
        private const string QuestsDayKey = Prefix + "QuestsDay";
        private const string QuestsKey = Prefix + "Quests";
        private const string RunAwardedKey = Prefix + "RunAwarded";
        private const string MaxSeenDayKey = Prefix + "MaxSeenDay";

        /// <summary>Login rewards, day 1 … day 7. The seventh day is the big one.</summary>
        private static readonly MetaReward[] dailyRewards =
        {
            new MetaReward(1, 0, 0),
            new MetaReward(0, 1, 0),
            new MetaReward(0, 0, 1),
            new MetaReward(2, 0, 0),
            new MetaReward(0, 2, 0),
            new MetaReward(0, 0, 2),
            new MetaReward(2, 2, 2, 100)
        };

        private static readonly int[] boosters = new int[3];
        private static readonly List<DailyQuest> quests = new List<DailyQuest>(QuestsPerDay);

        public static bool IsReady { get; private set; }
        public static int Revision { get; private set; }
        public static int Coins { get; private set; }

        /// <summary>Days claimed in the current chain (1…7); 0 before the first claim.</summary>
        public static int DailyStreak { get; private set; }

        /// <summary><see cref="MetaClock"/> day of the last claim; 0 = never.</summary>
        public static int DailyLastClaimDay { get; private set; }

        public static int QuestsDay { get; private set; }

        /// <summary>Score of the current run that was already paid out in coins.</summary>
        public static int RunAwardedScore { get; private set; }

        /// <summary>Coins paid for the last finished (part of a) run. Not saved.</summary>
        public static int LastRunCoins { get; private set; }

        /// <summary>Latest day ever seen. Keeps a device-clock fallback from going back in time.</summary>
        private static int maxSeenDay;

        public static IReadOnlyList<DailyQuest> Quests => quests;

        public static IReadOnlyList<MetaReward> DailyRewards => dailyRewards;

        /// <summary>Raised after any change (local or restored), for the UI and the cloud mirror.</summary>
        public static event Action Changed;

        /// <summary>Raised once, when the account copy was merged and the meta may run.</summary>
        public static event Action Ready;

        /// <summary>Raised when a task was finished and its reward granted.</summary>
        public static event Action<DailyQuest> QuestCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            // Static state outlives a play session when the domain is not reloaded.
            Changed = null;
            Ready = null;
            QuestCompleted = null;
            IsReady = false;
            LastRunCoins = 0;
            Load();
        }

        public static void Load()
        {
            Apply(new MetaSnapshot
            {
                Revision = PlayerPrefs.GetInt(RevisionKey, 0),
                Coins = PlayerPrefs.GetInt(CoinsKey, 0),
                Undo = PlayerPrefs.GetInt(UndoKey, 0),
                Extra = PlayerPrefs.GetInt(ExtraKey, 0),
                Clear = PlayerPrefs.GetInt(ClearKey, 0),
                DailyStreak = PlayerPrefs.GetInt(DailyStreakKey, 0),
                DailyLastClaimDay = PlayerPrefs.GetInt(DailyLastClaimKey, 0),
                QuestsDay = PlayerPrefs.GetInt(QuestsDayKey, 0),
                Quests = PlayerPrefs.GetString(QuestsKey, string.Empty),
                RunAwardedScore = PlayerPrefs.GetInt(RunAwardedKey, 0)
            });
            maxSeenDay = PlayerPrefs.GetInt(MaxSeenDayKey, 0);
        }

        public static MetaSnapshot Capture()
        {
            return new MetaSnapshot
            {
                Revision = Revision,
                Coins = Coins,
                Undo = boosters[(int)MetaBooster.Undo],
                Extra = boosters[(int)MetaBooster.Extra],
                Clear = boosters[(int)MetaBooster.Clear],
                DailyStreak = DailyStreak,
                DailyLastClaimDay = DailyLastClaimDay,
                QuestsDay = QuestsDay,
                Quests = SerializeQuests(),
                RunAwardedScore = RunAwardedScore
            };
        }

        /// <summary>
        /// Merges the account copy. The newer revision wins; true when the cloud copy was
        /// taken. When the local copy wins the platform layer pushes it back.
        /// </summary>
        public static bool Restore(MetaSnapshot cloud)
        {
            if (cloud == null || cloud.Revision <= Revision)
            {
                return false;
            }

            Apply(cloud);
            WritePrefs();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Called by the platform layer once the save is merged and the clock is set.</summary>
        public static void MarkReady()
        {
            if (IsReady)
            {
                EnsureToday();
                return;
            }

            IsReady = true;
            EnsureToday();
            Ready?.Invoke();
        }

        // ---------------------------------------------------------------- boosters

        public static int BoosterCount(MetaBooster booster) => boosters[Index(booster)];

        /// <summary>Takes one booster from the stock. False when there is none.</summary>
        public static bool TrySpendBooster(MetaBooster booster)
        {
            int index = Index(booster);
            if (boosters[index] <= 0)
            {
                return false;
            }

            boosters[index]--;
            Save();
            return true;
        }

        // ---------------------------------------------------------------- coins

        public static bool TrySpendCoins(int amount)
        {
            if (amount < 0 || Coins < amount)
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            Coins -= amount;
            Save();
            return true;
        }

        /// <summary>Adds coins earned outside a run payout (a passed level, a doubled reward).</summary>
        public static void AddCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Coins = (int)Math.Min((long)Coins + amount, int.MaxValue);
            Save();
        }

        /// <summary>Coins a run with <paramref name="score"/> points earns.</summary>
        public static int CoinsForScore(int score) => Mathf.Max(0, score) / ScorePerCoin;

        /// <summary>Called when a new run starts: nothing of it has been paid yet.</summary>
        public static void BeginRun()
        {
            LastRunCoins = 0;
            if (RunAwardedScore == 0)
            {
                return;
            }

            RunAwardedScore = 0;
            Save();
        }

        /// <summary>
        /// Pays the run's score out in coins (Game Over, or a restart from pause). Only the
        /// part not paid before counts, so a continue after Game Over does not pay twice.
        /// Returns the coins added.
        /// </summary>
        public static int AwardRunCoins(int score)
        {
            int gained = CoinsForScore(score) - CoinsForScore(RunAwardedScore);
            LastRunCoins = Mathf.Max(0, gained);
            if (score <= RunAwardedScore)
            {
                return 0;
            }

            RunAwardedScore = score;
            Coins += LastRunCoins;
            Save();
            return LastRunCoins;
        }

        // ---------------------------------------------------------------- daily reward

        /// <summary>True when today's login reward has not been taken yet.</summary>
        public static bool CanClaimDaily
        {
            get
            {
                if (!IsReady || !TryGetDay(out int today))
                {
                    return false;
                }

                return today > DailyLastClaimDay;
            }
        }

        /// <summary>
        /// Index (0…6) of the reward on offer today. A chain continues only when the last
        /// claim was yesterday; a missed day starts again from day 1.
        /// </summary>
        public static int DailyOfferIndex
        {
            get
            {
                if (!TryGetDay(out int today))
                {
                    return 0;
                }

                bool continues = DailyLastClaimDay > 0 && today == DailyLastClaimDay + 1;
                return continues ? DailyStreak % DailyCycleLength : 0;
            }
        }

        public static MetaReward DailyReward(int index)
        {
            return dailyRewards[Mathf.Clamp(index, 0, dailyRewards.Length - 1)];
        }

        /// <summary>Grants today's login reward. False when it was already taken or the day is unknown.</summary>
        public static bool TryClaimDaily(out int index, out MetaReward reward)
        {
            index = 0;
            reward = default;
            if (!CanClaimDaily || !TryGetDay(out int today))
            {
                return false;
            }

            index = DailyOfferIndex;
            reward = DailyReward(index);
            DailyStreak = index + 1;
            DailyLastClaimDay = today;
            GrantSilently(reward);
            Save();
            return true;
        }

        // ---------------------------------------------------------------- daily tasks

        /// <summary>Rolls new tasks when the day changed. True when today's tasks are known.</summary>
        public static bool EnsureToday()
        {
            if (!IsReady || !TryGetDay(out int today))
            {
                return false;
            }

            if (QuestsDay == today && quests.Count > 0)
            {
                return true;
            }

            QuestsDay = today;
            quests.Clear();
            List<QuestDef> picked = DailyQuestCatalog.PickForDay(today, QuestsPerDay);
            for (int i = 0; i < picked.Count; i++)
            {
                quests.Add(new DailyQuest(picked[i], 0, false));
            }

            Save();
            return true;
        }

        public static void ReportLinesCleared(int lines) => Report(QuestKind.ClearLines, lines);

        public static void ReportRunScore(int score) => Report(QuestKind.ScoreInRun, score);

        public static void ReportCombo(int combo) => Report(QuestKind.Combo, combo);

        public static void ReportBoardCleared() => Report(QuestKind.BoardClear, 1);

        private static void Report(QuestKind kind, int value)
        {
            if (value <= 0 || !EnsureToday())
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < quests.Count; i++)
            {
                DailyQuest quest = quests[i];
                if (quest.Done || quest.Def.Kind != kind)
                {
                    continue;
                }

                int next = quest.Def.Accumulates ? quest.Progress + value : Mathf.Max(quest.Progress, value);
                next = Mathf.Min(next, quest.Def.Target);
                if (next == quest.Progress)
                {
                    continue;
                }

                quest.Progress = next;
                changed = true;
                if (next >= quest.Def.Target)
                {
                    quest.Done = true;
                    GrantSilently(quest.Def.Reward);
                    Save();
                    QuestCompleted?.Invoke(quest);
                    changed = false;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        // ---------------------------------------------------------------- internals

        private static bool TryGetDay(out int day)
        {
            if (!MetaClock.TryGetToday(out day))
            {
                return false;
            }

            // A clock that jumps back (device fallback, a bad sync) must not reopen a day.
            if (day < maxSeenDay)
            {
                day = maxSeenDay;
            }
            else if (day > maxSeenDay)
            {
                maxSeenDay = day;
                PlayerPrefs.SetInt(MaxSeenDayKey, maxSeenDay);
            }

            return true;
        }

        private static void GrantSilently(MetaReward reward)
        {
            AddBooster(MetaBooster.Undo, reward.Undo);
            AddBooster(MetaBooster.Extra, reward.Extra);
            AddBooster(MetaBooster.Clear, reward.Clear);
            Coins = Mathf.Max(0, Coins + reward.Coins);
        }

        private static void AddBooster(MetaBooster booster, int amount)
        {
            int index = Index(booster);
            boosters[index] = Mathf.Clamp(boosters[index] + amount, 0, MaxBoosterStock);
        }

        private static int Index(MetaBooster booster)
        {
            int index = (int)booster;
            return index >= 0 && index < boosters.Length ? index : 0;
        }

        /// <summary>A local change: new revision, local cache, then listeners (cloud mirror, UI).</summary>
        private static void Save()
        {
            Revision++;
            WritePrefs();
            Changed?.Invoke();
        }

        private static void WritePrefs()
        {
            PlayerPrefs.SetInt(RevisionKey, Revision);
            PlayerPrefs.SetInt(CoinsKey, Coins);
            PlayerPrefs.SetInt(UndoKey, boosters[(int)MetaBooster.Undo]);
            PlayerPrefs.SetInt(ExtraKey, boosters[(int)MetaBooster.Extra]);
            PlayerPrefs.SetInt(ClearKey, boosters[(int)MetaBooster.Clear]);
            PlayerPrefs.SetInt(DailyStreakKey, DailyStreak);
            PlayerPrefs.SetInt(DailyLastClaimKey, DailyLastClaimDay);
            PlayerPrefs.SetInt(QuestsDayKey, QuestsDay);
            PlayerPrefs.SetString(QuestsKey, SerializeQuests());
            PlayerPrefs.SetInt(RunAwardedKey, RunAwardedScore);
            PlayerPrefs.Save();
        }

        private static void Apply(MetaSnapshot snapshot)
        {
            Revision = Mathf.Max(0, snapshot.Revision);
            Coins = Mathf.Max(0, snapshot.Coins);
            boosters[(int)MetaBooster.Undo] = Mathf.Clamp(snapshot.Undo, 0, MaxBoosterStock);
            boosters[(int)MetaBooster.Extra] = Mathf.Clamp(snapshot.Extra, 0, MaxBoosterStock);
            boosters[(int)MetaBooster.Clear] = Mathf.Clamp(snapshot.Clear, 0, MaxBoosterStock);
            DailyStreak = Mathf.Clamp(snapshot.DailyStreak, 0, DailyCycleLength);
            DailyLastClaimDay = Mathf.Max(0, snapshot.DailyLastClaimDay);
            QuestsDay = Mathf.Max(0, snapshot.QuestsDay);
            RunAwardedScore = Mathf.Max(0, snapshot.RunAwardedScore);
            ParseQuests(snapshot.Quests);
        }

        /// <summary>Compact form: <c>id:progress:done</c> joined by <c>|</c>.</summary>
        private static string SerializeQuests()
        {
            if (quests.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < quests.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                DailyQuest quest = quests[i];
                builder.Append(quest.Def.Id).Append(':').Append(quest.Progress).Append(':').Append(quest.Done ? 1 : 0);
            }

            return builder.ToString();
        }

        private static void ParseQuests(string raw)
        {
            quests.Clear();
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length && quests.Count < QuestsPerDay; i++)
            {
                string[] parts = entries[i].Split(':');
                QuestDef def = parts.Length > 0 ? DailyQuestCatalog.Find(parts[0]) : null;
                if (def == null)
                {
                    continue;
                }

                int progress = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 0;
                bool done = parts.Length > 2 && parts[2] == "1";
                quests.Add(new DailyQuest(def, progress, done));
            }
        }
    }
}
