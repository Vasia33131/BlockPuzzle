using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Daily reward gift for the bottom of the menu, drawn from a few rounded shapes: a box with a
    /// ribbon and a bow, a caption, and a red "!" badge while today's reward can be claimed. While it
    /// can, the box wobbles in bursts (<see cref="Tick"/>). The art sits on its own canvas so the wobble
    /// does not rebatch the rest of the menu.
    /// </summary>
    public sealed class MenuGiftButton : MonoBehaviour
    {
        public const string ObjectName = "GiftButton";

        /// <summary>Overall size of the button: the box and its caption.</summary>
        public static readonly Vector2 Size = new Vector2(360f, 240f);

        private const float ArtWidth = 170f;
        private const float ArtHeight = 156f;
        private const float WobbleCycle = 2.6f;
        private const float WobbleBurst = 0.9f;
        private const float WobbleFrequency = 22f;
        private const float WobbleDegrees = 9f;
        private const float BadgeSize = 56f;
        private const float DimmedAlpha = 0.6f;

        private static readonly Color BoxColor = GameTheme.FromHex("#8E5CF7");
        private static readonly Color LidColor = GameTheme.FromHex("#A97BFF");
        private static readonly Color RibbonColor = GameTheme.FromHex("#FFD23F");
        private static readonly Color BowColor = GameTheme.FromHex("#FFC107");
        private static readonly Color BadgeColor = GameTheme.FromHex("#FF3B30");

        private RectTransform art;
        private CanvasGroup artGroup;
        private RectTransform badge;
        private TMP_Text caption;
        private bool available;

        public Button Button { get; private set; }

        /// <summary>The rect the menu lays out and pops in.</summary>
        public RectTransform Slot => (RectTransform)transform;

        public static MenuGiftButton Create(Transform parent)
        {
            RectTransform slot = UIFactory.CreateRect(ObjectName + "Slot", parent);
            slot.sizeDelta = Size;
            var gift = slot.gameObject.AddComponent<MenuGiftButton>();
            gift.Build(slot);
            return gift;
        }

        /// <summary>True while today's reward can be claimed: shows the badge and starts the wobble.</summary>
        public void SetAvailable(bool isAvailable)
        {
            available = isAvailable;
            if (badge != null)
            {
                badge.gameObject.SetActive(isAvailable);
            }

            if (artGroup != null)
            {
                artGroup.alpha = isAvailable ? 1f : DimmedAlpha;
            }

            if (!isAvailable && art != null)
            {
                art.localEulerAngles = Vector3.zero;
                art.anchoredPosition = new Vector2(0f, CaptionHeight);
            }
        }

        public void RefreshText()
        {
            UIFactory.SetText(caption, GameLocalization.DailyRewardTitle);
        }

        /// <summary>Wobble and badge pulse; called once per frame by the menu, allocation free.</summary>
        public void Tick(float time)
        {
            if (!available || art == null)
            {
                return;
            }

            float cycle = Mathf.Repeat(time, WobbleCycle);
            float envelope = cycle < WobbleBurst ? 1f - cycle / WobbleBurst : 0f;
            float lift = Mathf.Abs(Mathf.Sin(cycle * 6f)) * 8f * envelope;
            art.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(cycle * WobbleFrequency) * WobbleDegrees * envelope);
            art.anchoredPosition = new Vector2(0f, CaptionHeight + lift);

            float pulse = 1f + Mathf.Sin(time * 6f) * 0.1f;
            badge.localScale = new Vector3(pulse, pulse, 1f);
        }

        private const float CaptionHeight = 62f;

        private void Build(RectTransform slot)
        {
            Image hit = UIFactory.CreateImage(ObjectName, slot, new Color(1f, 1f, 1f, 0f), false);
            hit.canvasRenderer.cullTransparentMesh = false;
            UIFactory.Stretch(hit.rectTransform);
            Button = hit.gameObject.AddComponent<Button>();
            Button.targetGraphic = hit;
            Button.transition = Selectable.Transition.None;
            ButtonPressAnimator.Attach(Button);

            art = UIFactory.CreateRect("Art", hit.rectTransform);
            UIFactory.Anchor(
                art, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, CaptionHeight),
                new Vector2(ArtWidth, ArtHeight));
            art.gameObject.AddComponent<Canvas>();
            artGroup = art.gameObject.AddComponent<CanvasGroup>();
            artGroup.blocksRaycasts = false;
            BuildBox(art);

            TextMeshProUGUI text = UIFactory.CreateText(
                "Caption", hit.rectTransform, GameLocalization.DailyRewardTitle, 30f, Color.white,
                TextAlignmentOptions.Center, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 30f;
            UIFactory.Anchor(
                text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f),
                new Vector2(Size.x, 48f));
            caption = text;

            BuildBadge(art);
            SetAvailable(false);
        }

        private static void BuildBox(RectTransform parent)
        {
            var bottom = new Vector2(0.5f, 0f);

            // The painted gift when it is in the project, the box from rounded shapes otherwise.
            Sprite painted = GameArt.Gift;
            if (painted != null)
            {
                Image picture = UIFactory.CreateImage("Gift", parent, Color.white, false);
                picture.sprite = painted;
                picture.preserveAspect = true;
                picture.raycastTarget = false;
                UIFactory.Anchor(picture.rectTransform, bottom, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(ArtWidth, ArtHeight));
                return;
            }

            Part("Body", parent, BoxColor, bottom, new Vector2(0f, 4f), new Vector2(128f, 88f), true);
            Part("BodyRibbon", parent, RibbonColor, bottom, new Vector2(0f, 4f), new Vector2(28f, 88f), true);
            Part("Lid", parent, LidColor, bottom, new Vector2(0f, 84f), new Vector2(150f, 44f), true);
            Part("LidRibbon", parent, RibbonColor, bottom, new Vector2(0f, 84f), new Vector2(28f, 44f), true);

            Image left = Part("BowLeft", parent, BowColor, bottom, new Vector2(-24f, 128f), new Vector2(48f, 40f), false);
            Image right = Part("BowRight", parent, BowColor, bottom, new Vector2(24f, 128f), new Vector2(48f, 40f), false);
            left.sprite = ProfileUi.CircleSprite;
            right.sprite = ProfileUi.CircleSprite;
            left.rectTransform.localEulerAngles = new Vector3(0f, 0f, 20f);
            right.rectTransform.localEulerAngles = new Vector3(0f, 0f, -20f);

            Image knot = Part("BowKnot", parent, GameTheme.Darken(BowColor, 0.12f), bottom, new Vector2(0f, 120f), new Vector2(26f, 26f), false);
            knot.sprite = ProfileUi.CircleSprite;
        }

        /// <summary>One rounded piece of the box, its bottom-centre at <paramref name="position"/>.</summary>
        private static Image Part(
            string name, RectTransform parent, Color color, Vector2 anchor, Vector2 position, Vector2 size, bool rounded)
        {
            Image image = UIFactory.CreateImage(name, parent, color, rounded);
            image.raycastTarget = false;
            UIFactory.Anchor(image.rectTransform, anchor, new Vector2(0.5f, 0f), position, size);
            return image;
        }

        private void BuildBadge(RectTransform parent)
        {
            Image ring = UIFactory.CreateImage("Badge", parent, Color.white, false);
            ring.sprite = ProfileUi.CircleSprite;
            ring.raycastTarget = false;
            UIFactory.Anchor(
                ring.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6f, -14f),
                new Vector2(BadgeSize + 8f, BadgeSize + 8f));
            badge = ring.rectTransform;

            Image fill = UIFactory.CreateImage("Fill", badge, BadgeColor, false);
            fill.sprite = ProfileUi.CircleSprite;
            fill.raycastTarget = false;
            UIFactory.Stretch(fill.rectTransform, 4f);

            TextMeshProUGUI mark = UIFactory.CreateText(
                "Mark", badge, "!", 44f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(mark.rectTransform);
        }
    }
}
