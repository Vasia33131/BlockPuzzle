using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Today's three tasks on the pause card: the goal, a progress bar with "7/20" (or
    /// "Done") and the booster reward. Built in code into whatever card hosts it, so a
    /// pause prefab baked before the meta layer picks it up too.
    /// </summary>
    public class DailyQuestsView : MonoBehaviour
    {
        public const string ObjectName = "DailyQuests";
        public const float Width = 700f;
        public const float HeaderHeight = 56f;
        public const float RowHeight = 96f;
        public const float RowGap = 10f;

        public static float BlockHeight =>
            HeaderHeight + MetaProgress.QuestsPerDay * RowHeight + (MetaProgress.QuestsPerDay - 1) * RowGap;

        private readonly List<Row> rows = new List<Row>(MetaProgress.QuestsPerDay);
        private TMP_Text header;
        private TMP_Text waiting;

        private sealed class Row
        {
            public RectTransform Root;
            public Image Background;
            public TMP_Text Goal;
            public TMP_Text Progress;
            public RectTransform Fill;
            public RectTransform Reward;
            public string ShownQuestId;
        }

        /// <summary>Finds or builds the block as a child of <paramref name="card"/>.</summary>
        public static DailyQuestsView Ensure(RectTransform card)
        {
            if (card == null)
            {
                return null;
            }

            Transform existing = card.Find(ObjectName);
            DailyQuestsView view = existing != null ? existing.GetComponent<DailyQuestsView>() : null;
            if (view != null)
            {
                return view;
            }

            RectTransform root = UIFactory.CreateRect(ObjectName, card);
            view = root.gameObject.AddComponent<DailyQuestsView>();
            view.Build();
            return view;
        }

        private void OnEnable()
        {
            MetaProgress.Changed += Refresh;
            MetaProgress.Ready += Refresh;
            GameLocalization.LanguageChanged += HandleLanguageChanged;
            Refresh();
        }

        private void OnDisable()
        {
            MetaProgress.Changed -= Refresh;
            MetaProgress.Ready -= Refresh;
            GameLocalization.LanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].ShownQuestId = null;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (header == null)
            {
                return;
            }

            header.text = GameLocalization.QuestsTitle;
            waiting.text = GameLocalization.QuestsWaiting;

            bool known = MetaProgress.EnsureToday();
            IReadOnlyList<DailyQuest> quests = MetaProgress.Quests;
            waiting.gameObject.SetActive(!known || quests.Count == 0);

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                DailyQuest quest = known && i < quests.Count ? quests[i] : null;
                row.Root.gameObject.SetActive(quest != null);
                if (quest != null)
                {
                    ApplyRow(row, quest);
                }
            }
        }

        private static void ApplyRow(Row row, DailyQuest quest)
        {
            if (row.ShownQuestId != quest.Def.Id)
            {
                row.ShownQuestId = quest.Def.Id;
                row.Goal.text = GameLocalization.QuestText(quest.Def);
                MetaUi.FillRewardRow(row.Reward, quest.Def.Reward, 48f, 22f, GameTheme.TextPrimary);
            }

            row.Progress.text = quest.Done
                ? GameLocalization.QuestDone
                : $"{quest.Progress}/{quest.Def.Target}";
            row.Progress.color = quest.Done ? GameTheme.ShopBuy : GameTheme.TextSecondary;
            row.Background.color = quest.Done
                ? GameTheme.WithAlpha(GameTheme.ShopBuy, 0.18f)
                : GameTheme.EmptyCell;

            float ratio = quest.Ratio;
            row.Fill.anchorMin = Vector2.zero;
            row.Fill.anchorMax = new Vector2(ratio, 1f);
            row.Fill.offsetMin = Vector2.zero;
            row.Fill.offsetMax = Vector2.zero;
            row.Fill.gameObject.SetActive(ratio > 0f);
            Image fill = row.Fill.GetComponent<Image>();
            if (fill != null)
            {
                fill.color = quest.Done ? GameTheme.ShopBuy : GameTheme.Accent;
            }
        }

        private void Build()
        {
            RectTransform root = (RectTransform)transform;
            root.sizeDelta = new Vector2(Width, BlockHeight);

            TextMeshProUGUI title = UIFactory.CreateText(
                "Header", root, GameLocalization.QuestsTitle, 36f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            title.characterSpacing = 4f;
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width, HeaderHeight));
            header = title;

            TextMeshProUGUI wait = UIFactory.CreateText(
                "Waiting", root, GameLocalization.QuestsWaiting, 30f, GameTheme.TextSecondary);
            UIFactory.Anchor(wait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -HeaderHeight - 20f), new Vector2(Width, 50f));
            waiting = wait;

            for (int i = 0; i < MetaProgress.QuestsPerDay; i++)
            {
                rows.Add(CreateRow(root, -HeaderHeight - i * (RowHeight + RowGap)));
            }

            // OnEnable ran before the rows existed.
            Refresh();
        }

        private static Row CreateRow(RectTransform parent, float y)
        {
            Image background = UIFactory.CreateImage("Quest", parent, GameTheme.EmptyCell);
            background.raycastTarget = false;
            RectTransform rect = background.rectTransform;
            UIFactory.Anchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(Width, RowHeight));

            const float textLeft = 22f;
            const float textWidth = 420f;

            TextMeshProUGUI goal = UIFactory.CreateText(
                "Goal", rect, string.Empty, 30f, GameTheme.TextPrimary, TextAlignmentOptions.Left, FontStyles.Normal, FontRole.Body);
            goal.enableAutoSizing = true;
            goal.fontSizeMin = 20f;
            goal.fontSizeMax = 30f;
            UIFactory.Anchor(goal.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textLeft, -10f), new Vector2(textWidth, 44f));

            Image track = UIFactory.CreateImage("Track", rect, GameTheme.ButtonSecondary);
            track.raycastTarget = false;
            UIFactory.Anchor(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(textLeft, 18f), new Vector2(300f, 16f));

            Image fill = UIFactory.CreateImage("Fill", track.rectTransform, GameTheme.Accent);
            fill.raycastTarget = false;

            TextMeshProUGUI progress = UIFactory.CreateText(
                "Progress", rect, string.Empty, 26f, GameTheme.TextSecondary, TextAlignmentOptions.Left, FontStyles.Normal, FontRole.Body);
            UIFactory.Anchor(progress.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(textLeft + 312f, 8f), new Vector2(130f, 36f));

            RectTransform reward = UIFactory.CreateRect("Reward", rect);
            UIFactory.Anchor(reward, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(Width - textLeft - textWidth - 24f, RowHeight - 12f));

            return new Row
            {
                Root = rect,
                Background = background,
                Goal = goal,
                Progress = progress,
                Fill = fill.rectTransform,
                Reward = reward
            };
        }
    }
}
