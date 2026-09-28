using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Short pill under the HUD for meta events: a finished daily task (with its reward)
    /// or a claimed login reward. Messages queue up and never take input. Runs on unscaled
    /// time so it also plays over the pause screen.
    /// </summary>
    public class MetaToast : MonoBehaviour
    {
        public const string ObjectName = "MetaToast";

        private const float TopOffset = 200f;
        private const float Width = 760f;
        private const float Height = 190f;
        private const float TextOnlyHeight = 110f;
        private const float InDuration = 0.25f;
        private const float Hold = 1.8f;
        private const float OutDuration = 0.3f;

        private static MetaToast instance;

        private readonly Queue<Message> queue = new Queue<Message>();
        private CanvasGroup group;
        private RectTransform pill;
        private TMP_Text title;
        private RectTransform rewardRow;
        private float timer;
        private bool showing;

        private struct Message
        {
            public string Title;
            public MetaReward Reward;
            public bool TextOnly;
        }

        public static MetaToast Ensure(RectTransform canvasRect)
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<MetaToast>(true);
            if (instance != null || canvasRect == null)
            {
                return instance;
            }

            RectTransform root = UIFactory.CreateRect(ObjectName, canvasRect);
            UIFactory.Stretch(root);
            instance = root.gameObject.AddComponent<MetaToast>();
            instance.Build();
            return instance;
        }

        public static void Show(string text, MetaReward reward)
        {
            if (instance == null)
            {
                return;
            }

            instance.queue.Enqueue(new Message { Title = text, Reward = reward });
            instance.transform.SetAsLastSibling();
        }

        /// <summary>A plain one-line hint: the same pill without the reward row.</summary>
        public static void ShowText(string text)
        {
            if (instance == null)
            {
                return;
            }

            instance.queue.Enqueue(new Message { Title = text, TextOnly = true });
            instance.transform.SetAsLastSibling();
        }

        private void OnEnable()
        {
            MetaProgress.QuestCompleted += HandleQuestCompleted;
        }

        private void OnDisable()
        {
            MetaProgress.QuestCompleted -= HandleQuestCompleted;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private static void HandleQuestCompleted(DailyQuest quest)
        {
            Show(GameLocalization.QuestCompletedToast + "\n" + GameLocalization.QuestText(quest.Def), quest.Def.Reward);
        }

        private void Update()
        {
            if (group == null)
            {
                return;
            }

            if (!showing)
            {
                if (queue.Count > 0)
                {
                    Begin(queue.Dequeue());
                }

                return;
            }

            timer += Time.unscaledDeltaTime;
            if (timer >= InDuration + Hold + OutDuration)
            {
                showing = false;
                group.alpha = 0f;
            }
        }

        private void Begin(Message message)
        {
            showing = true;
            timer = 0f;
            title.text = message.Title;
            rewardRow.gameObject.SetActive(!message.TextOnly);
            pill.sizeDelta = new Vector2(Width, message.TextOnly ? TextOnlyHeight : Height);
            if (!message.TextOnly)
            {
                MetaUi.FillRewardRow(rewardRow, message.Reward, 64f, 26f, GameTheme.TextPrimary);
            }

            GameTween.Kill(group);
            GameTween.Kill(pill);
            group.alpha = 0f;
            pill.localScale = Vector3.one * 0.7f;
            GameTween.Scale(pill, Vector3.one, InDuration, TweenEase.OutBack, unscaled: true);
            GameTween.Fade(group, 1f, InDuration, TweenEase.OutQuad, unscaled: true, onComplete: () =>
                GameTween.Fade(group, 0f, OutDuration, TweenEase.InQuad, Hold, unscaled: true));
        }

        private void Build()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image background = UIFactory.CreateImage("Pill", transform, GameTheme.WithAlpha(GameTheme.CardBackground, 0.96f));
            background.raycastTarget = false;
            pill = background.rectTransform;
            UIFactory.Anchor(pill, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -TopOffset), new Vector2(Width, Height));

            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = GameTheme.WithAlpha(MetaUi.CoinGold, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);

            TextMeshProUGUI label = UIFactory.CreateText(
                "Title", pill, string.Empty, 32f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            label.enableWordWrapping = true;
            UIFactory.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(Width - 40f, 84f));
            title = label;

            rewardRow = UIFactory.CreateRect("Reward", pill);
            UIFactory.Anchor(rewardRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(Width - 40f, 92f));
        }
    }
}
