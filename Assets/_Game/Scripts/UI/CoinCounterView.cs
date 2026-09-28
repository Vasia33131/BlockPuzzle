using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Reusable coin counter: coin icon, the <see cref="MetaProgress.Coins"/> balance that rolls
    /// to a new value, and a "+" button that opens the shop. Build it with <see cref="Create"/>
    /// and place the returned rect wherever it is needed.
    /// </summary>
    public class CoinCounterView : MonoBehaviour
    {
        public const string ObjectName = "CoinCounter";

        private const float IconSize = 60f;
        private const float PlusSize = 64f;
        private const float Padding = 14f;
        private const float CountDuration = 0.5f;
        private const float FontSize = 44f;

        /// <summary>Overall size of the counter.</summary>
        public static readonly Vector2 Size = new Vector2(330f, 88f);

        private RectTransform coinIcon;
        private TMP_Text amount;
        private Button plusButton;
        private ShopPanel shop;
        private Coroutine counting;
        private int shown;
        private bool built;

        /// <summary>Builds a counter under <paramref name="parent"/> and returns it. Position the <c>transform</c> yourself.</summary>
        public static CoinCounterView Create(Transform parent)
        {
            RectTransform root = UIFactory.CreateRect(ObjectName, parent);
            root.sizeDelta = Size;
            var view = root.gameObject.AddComponent<CoinCounterView>();
            view.Build();
            return view;
        }

        private void OnEnable()
        {
            MetaProgress.Changed += HandleMetaChanged;
            if (built)
            {
                // Coming back on screen shows the balance at once instead of rolling from an old value.
                StopCounting();
                SetShown(MetaProgress.Coins);
            }
        }

        private void OnDisable()
        {
            MetaProgress.Changed -= HandleMetaChanged;
            StopCounting();
        }

        private void HandleMetaChanged()
        {
            if (!built)
            {
                return;
            }

            int target = MetaProgress.Coins;
            if (target == shown && counting == null)
            {
                return;
            }

            StopCounting();
            counting = StartCoroutine(Count(shown, target));
        }

        private IEnumerator Count(int from, int to)
        {
            if (to > from)
            {
                GameTween.Punch(coinIcon, 0.3f, 0.35f, unscaled: true);
            }

            float elapsed = 0f;
            while (elapsed < CountDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CountDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetShown(Mathf.RoundToInt(Mathf.Lerp(from, to, eased)));
                yield return null;
            }

            counting = null;
            SetShown(to);
        }

        private void StopCounting()
        {
            if (counting != null)
            {
                StopCoroutine(counting);
                counting = null;
            }
        }

        private void SetShown(int value)
        {
            shown = value;
            amount.text = value.ToString();
        }

        private void HandlePlusClicked()
        {
            if (shop == null)
            {
                shop = FindObjectOfType<ShopPanel>(true);
            }

            shop?.Open();
        }

        private void Build()
        {
            var root = (RectTransform)transform;

            Image background = UIFactory.CreateImage("Background", root, GameTheme.WithAlpha(GameTheme.CardBackground, 0.92f));
            background.raycastTarget = false;
            UIFactory.Stretch(background.rectTransform);

            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = GameTheme.WithAlpha(MetaUi.CoinGold, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            Image icon = MetaUi.CreateCoinIcon("Coin", root, IconSize);
            coinIcon = icon.rectTransform;
            UIFactory.Anchor(coinIcon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Padding, 0f), new Vector2(IconSize, IconSize));

            amount = UIFactory.CreateText(
                "Amount", root, "0", FontSize, MetaUi.CoinGold, TextAlignmentOptions.Center, FontStyles.Bold);
            amount.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform amountRect = amount.rectTransform;
            UIFactory.Stretch(amountRect);
            amountRect.offsetMin = new Vector2(Padding * 2f + IconSize, 0f);
            amountRect.offsetMax = new Vector2(-(Padding * 2f + PlusSize), 0f);

            plusButton = UIFactory.CreateButton(
                "PlusButton", root, "+", GameTheme.ShopBuy, GameTheme.ShopBuyLabel, 52f);
            var plusImage = (Image)plusButton.targetGraphic;
            plusImage.sprite = ProfileUi.CircleSprite;
            plusImage.type = Image.Type.Simple;
            UIFactory.Anchor(
                (RectTransform)plusButton.transform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-Padding, 0f),
                new Vector2(PlusSize, PlusSize));
            plusButton.onClick.AddListener(HandlePlusClicked);

            built = true;
            SetShown(MetaProgress.Coins);
        }
    }
}
