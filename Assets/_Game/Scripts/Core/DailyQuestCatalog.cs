using System;
using System.Collections.Generic;

namespace BlockPuzzle.Core
{
    /// <summary>Booster kinds kept in the meta inventory. Same order as the in-run boosters.</summary>
    public enum MetaBooster
    {
        Undo = 0,
        Extra = 1,
        Clear = 2
    }

    /// <summary>What a daily task counts.</summary>
    public enum QuestKind
    {
        /// <summary>Lines cleared during the day, summed over every run.</summary>
        ClearLines,

        /// <summary>Score reached inside a single run.</summary>
        ScoreInRun,

        /// <summary>Combo streak reached.</summary>
        Combo,

        /// <summary>Placements that emptied the whole board.</summary>
        BoardClear
    }

    /// <summary>Boosters and coins handed out by the daily reward or a daily task.</summary>
    [Serializable]
    public struct MetaReward
    {
        public int Undo;
        public int Extra;
        public int Clear;
        public int Coins;

        public MetaReward(int undo, int extra, int clear, int coins = 0)
        {
            Undo = undo;
            Extra = extra;
            Clear = clear;
            Coins = coins;
        }

        public int Count(MetaBooster booster)
        {
            switch (booster)
            {
                case MetaBooster.Extra:
                    return Extra;
                case MetaBooster.Clear:
                    return Clear;
                default:
                    return Undo;
            }
        }
    }

    /// <summary>One entry of the daily task pool.</summary>
    public sealed class QuestDef
    {
        public QuestDef(string id, QuestKind kind, int target, MetaReward reward)
        {
            Id = id;
            Kind = kind;
            Target = Math.Max(1, target);
            Reward = reward;
        }

        public string Id { get; }
        public QuestKind Kind { get; }
        public int Target { get; }
        public MetaReward Reward { get; }

        /// <summary>
        /// Accumulating tasks add every report; the others keep the best value reached
        /// (a score or a combo is a height, not a sum).
        /// </summary>
        public bool Accumulates => Kind == QuestKind.ClearLines || Kind == QuestKind.BoardClear;
    }

    /// <summary>
    /// Pool of daily tasks. Every day gets <see cref="MetaProgress.QuestsPerDay"/> tasks of
    /// different kinds, picked from the day number, so the same day shows the same tasks
    /// on every device. Ids are stored in the save — never rename one that shipped.
    /// </summary>
    public static class DailyQuestCatalog
    {
        private static readonly QuestDef[] pool =
        {
            new QuestDef("lines_20", QuestKind.ClearLines, 20, new MetaReward(1, 0, 0)),
            new QuestDef("lines_40", QuestKind.ClearLines, 40, new MetaReward(0, 1, 0, 20)),
            new QuestDef("score_2000", QuestKind.ScoreInRun, 2000, new MetaReward(0, 1, 0)),
            new QuestDef("score_5000", QuestKind.ScoreInRun, 5000, new MetaReward(0, 0, 1, 20)),
            new QuestDef("combo_3", QuestKind.Combo, 3, new MetaReward(0, 0, 1)),
            new QuestDef("combo_5", QuestKind.Combo, 5, new MetaReward(1, 0, 1)),
            new QuestDef("board_1", QuestKind.BoardClear, 1, new MetaReward(1, 1, 1))
        };

        public static IReadOnlyList<QuestDef> Pool => pool;

        public static QuestDef Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].Id == id)
                {
                    return pool[i];
                }
            }

            return null;
        }

        /// <summary>Tasks of <paramref name="day"/>: different kinds, stable for that day.</summary>
        public static List<QuestDef> PickForDay(int day, int count)
        {
            var random = new Random(unchecked(day * 7919 + 17));
            var kinds = new List<QuestKind>((QuestKind[])Enum.GetValues(typeof(QuestKind)));
            Shuffle(kinds, random);

            var picked = new List<QuestDef>(count);
            var candidates = new List<QuestDef>(pool.Length);
            for (int k = 0; k < kinds.Count && picked.Count < count; k++)
            {
                candidates.Clear();
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].Kind == kinds[k])
                    {
                        candidates.Add(pool[i]);
                    }
                }

                if (candidates.Count > 0)
                {
                    picked.Add(candidates[random.Next(candidates.Count)]);
                }
            }

            return picked;
        }

        private static void Shuffle<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
