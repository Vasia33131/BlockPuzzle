using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Small pieces shared by the meta screens: the coin glyph (drawn in code, so it needs
    /// no art and no emoji in the font) and the reward row of booster icons with counts.
    /// </summary>
    public static class MetaUi
    {
        public static readonly Color CoinGold = GameTheme.FromHex("#F7C948");
        public static readonly Color CoinRim = GameTheme.FromHex("#C98A12");
        public static readonly Color DarkLabel = GameTheme.FromHex("#1a1a2e");

        private const int CoinTextureSize = 64;

        private static Sprite coinSprite;

        /// <summary>Gold disc with a darker rim and a small shine.</summary>
        public static Sprite CoinSprite
        {
            get
            {
                if (coinSprite != null)
                {
                    return coinSprite;
                }

                var texture = new Texture2D(CoinTextureSize, CoinTextureSize, TextureFormat.RGBA32, false)
                {
                    name = "MetaCoin",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                float center = (CoinTextureSize - 1) * 0.5f;
                float outer = CoinTextureSize * 0.5f - 1f;
                float inner = outer * 0.78f;
                var shine = new Vector2(center - outer * 0.35f, center + outer * 0.35f);
                var pixels = new Color[CoinTextureSize * CoinTextureSize];
                for (int y = 0; y < CoinTextureSize; y++)
                {
                    for (int x = 0; x < CoinTextureSize; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        Color c = d <= inner ? CoinGold : CoinRim;
                        if (Vector2.Distance(new Vector2(x, y), shine) < outer * 0.18f)
                        {
                            c = Color.Lerp(c, Color.white, 0.6f);
                        }

                        c.a = Mathf.Clamp01(outer + 0.5f - d);
                        pixels[y * CoinTextureSize + x] = c;
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply(false, true);
                coinSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, CoinTextureSize, CoinTextureSize),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                return coinSprite;
            }
        }

        public static Image CreateCoinIcon(string name, Transform parent, float size)
        {
            Image icon = UIFactory.CreateImage(name, parent, Color.white, rounded: false);
            icon.sprite = CoinSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            return icon;
        }

        /// <summary>
        /// Centred "[coin] 120" pair. The text keeps its width to the content so the
        /// icon hugs it whatever the number is.
        /// </summary>
        public static TMP_Text CreateCoinAmount(string name, Transform parent, float fontSize, Color color)
        {
            RectTransform root = UIFactory.CreateRect(name, parent);
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = fontSize * 0.2f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image icon = CreateCoinIcon("Coin", root, fontSize);
            var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = fontSize;
            iconLayout.preferredHeight = fontSize;

            TextMeshProUGUI amount = UIFactory.CreateText(
                "Amount", root, "0", fontSize, color, TextAlignmentOptions.Left, FontStyles.Bold);
            amount.overflowMode = TextOverflowModes.Overflow;
            return amount;
        }

        /// <summary>
        /// Row of booster icons with "xN" under each (and coins last) for a reward.
        /// Clears <paramref name="row"/> first, so it can be refilled.
        /// </summary>
        public static void FillRewardRow(RectTransform row, MetaReward reward, float iconSize, float fontSize, Color textColor)
        {
            if (row == null)
            {
                return;
            }

            for (int i = row.childCount - 1; i >= 0; i--)
            {
                // Detach first: Destroy is deferred and the layout would still count it.
                Transform child = row.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = iconSize * 0.12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            AddRewardItem(row, reward.Undo, BoosterBar.IconFor(FreeBoosterType.Undo), iconSize, fontSize, textColor);
            AddRewardItem(row, reward.Extra, BoosterBar.IconFor(FreeBoosterType.Extra), iconSize, fontSize, textColor);
            AddRewardItem(row, reward.Clear, BoosterBar.IconFor(FreeBoosterType.Clear), iconSize, fontSize, textColor);
            AddRewardItem(row, reward.Coins, CoinSprite, iconSize * 0.72f, fontSize, textColor);
        }

        private static void AddRewardItem(
            RectTransform row, int count, Sprite sprite, float iconSize, float fontSize, Color textColor)
        {
            if (count <= 0)
            {
                return;
            }

            RectTransform item = UIFactory.CreateRect("Item", row);
            var itemLayout = item.gameObject.AddComponent<LayoutElement>();
            float width = Mathf.Max(iconSize, fontSize * 2.4f);
            itemLayout.preferredWidth = width;
            itemLayout.preferredHeight = iconSize + fontSize;

            Image icon = UIFactory.CreateImage("Icon", item, Color.white, rounded: false);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIFactory.Anchor(
                icon.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(iconSize, iconSize));

            TextMeshProUGUI label = UIFactory.CreateText(
                "Count", item, "x" + count, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(
                label.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                new Vector2(width, fontSize * 1.1f));
        }
    }
}
