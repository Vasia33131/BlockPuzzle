using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Look and life of the HUD shop button: a green pill with a shop-front icon and a caption,
    /// a quick shine sweep every <see cref="ShineInterval"/> seconds and a red "!" dot while the
    /// player can already afford a theme with coins. Click handling stays in <see cref="ShopPanel"/>.
    /// <see cref="Setup"/> is idempotent, so it also upgrades the old square cart button of earlier bakes.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudShopButton : MonoBehaviour
    {
        public const float Height = 140f;
        public const float MinWidth = 300f;
        public const float MaxWidth = 420f;

        private const float IconSize = 60f;
        private const float IconGap = 10f;
        private const float SidePadding = 24f;
        private const float LabelFont = 52f;
        private const float ShadowDrop = 10f;
        private const float ShineInterval = 8f;
        private const float ShineDuration = 0.55f;
        private const float StateTick = 0.25f;
        private const float AlertSize = 56f;

        private static readonly Color FaceColor = GameTheme.ShopBuy;
        private static readonly Color ShadowColor = GameTheme.FromHex("#178A4A");
        private static readonly Color ContentColor = GameTheme.ShopBuyLabel;
        private static readonly Color AlertColor = GameTheme.FromHex("#FF2D3D");
        private static readonly string[] LegacyCartParts = { "CartBody", "Wheel_0", "Wheel_1", "CartHandle" };
        private static readonly string[] ThemeIds = { ThemeConfig.OceanId, ThemeConfig.CandyId };

        private Button button;
        private Image shadow;
        private Image face;
        private Image shineClip;
        private RectTransform shine;
        private Image icon;
        private TMP_Text label;
        private Image alert;
        private bool lastInteractable = true;

        /// <summary>Width the button wants at the current language, between <see cref="MinWidth"/> and <see cref="MaxWidth"/>.</summary>
        public float DesiredWidth
        {
            get
            {
                Resolve();
                float labelWidth = label != null ? label.GetPreferredValues(label.text).x : 0f;
                float content = IconSize + IconGap + labelWidth + SidePadding * 2f;
                return Mathf.Clamp(Mathf.Ceil(content), MinWidth, MaxWidth);
            }
        }

        /// <summary>Builds the missing parts on <paramref name="button"/> and returns its controller.</summary>
        public static HudShopButton Setup(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Transform root = button.transform;
            for (int i = 0; i < LegacyCartParts.Length; i++)
            {
                Transform old = root.Find(LegacyCartParts[i]);
                if (old != null)
                {
                    old.gameObject.SetActive(false);
                    old.name = "Retired" + LegacyCartParts[i];
                }
            }

            // The root only catches touches; the visible parts are children so the shadow can sit underneath.
            var rootImage = root.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.sprite = null;
                rootImage.color = Color.clear;
                rootImage.raycastTarget = true;
                button.targetGraphic = rootImage;
            }

            button.transition = Selectable.Transition.None;
            ButtonPressAnimator.Attach(button);

            RectTransform rootRect = (RectTransform)root;
            Image shadowImage = EnsureImage(rootRect, "Shadow", ShadowColor, 0);
            Image faceImage = EnsureImage(rootRect, "Face", FaceColor, 1);
            RectTransform faceRect = faceImage.rectTransform;

            Image clip = EnsureImage(faceRect, "ShineClip", Color.white, -1);
            if (clip.GetComponent<Mask>() == null)
            {
                clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            }

            if (clip.transform.Find("Shine") == null)
            {
                Image streak = UIFactory.CreateImage("Shine", clip.rectTransform, new Color(1f, 1f, 1f, 0.55f), false);
                streak.raycastTarget = false;
                streak.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                UIFactory.Anchor(
                    streak.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-MaxWidth, 0f),
                    new Vector2(56f, Height * 2f));
            }

            EnsureImage(faceRect, "Icon", ContentColor, -1).preserveAspect = true;

            if (faceRect.Find("Label") == null)
            {
                TextMeshProUGUI text = UIFactory.CreateText(
                    "Label",
                    faceRect,
                    GameLocalization.ShopTitle,
                    LabelFont,
                    ContentColor,
                    TextAlignmentOptions.MidlineLeft,
                    FontStyles.Bold);
                text.overflowMode = TextOverflowModes.Overflow;
            }

            if (rootRect.Find("Alert") == null)
            {
                Image dot = UIFactory.CreateImage("Alert", rootRect, AlertColor, false);
                dot.raycastTarget = false;
                UIFactory.Anchor(
                    dot.rectTransform,
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-22f, -6f),
                    new Vector2(AlertSize, AlertSize));

                TextMeshProUGUI mark = UIFactory.CreateText(
                    "Mark", dot.rectTransform, "!", 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                UIFactory.Stretch(mark.rectTransform);
                dot.gameObject.SetActive(false);
            }

            HudShopButton controller = button.GetComponent<HudShopButton>();
            if (controller == null)
            {
                controller = button.gameObject.AddComponent<HudShopButton>();
            }

            controller.Resolve();
            controller.ApplyVisuals();
            float width = controller.DesiredWidth;
            rootRect.sizeDelta = new Vector2(width, Height);
            controller.Layout(width);
            return controller;
        }

        /// <summary>Positions the shadow, face, icon and caption for a button that is <paramref name="width"/> wide.</summary>
        public void Layout(float width)
        {
            Resolve();
            if (face == null || shadow == null)
            {
                return;
            }

            UIFactory.Anchor(
                shadow.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -ShadowDrop),
                new Vector2(width, Height));
            UIFactory.Anchor(
                face.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(width, Height));
            UIFactory.Stretch(shineClip.rectTransform);

            float labelWidth = label != null ? label.GetPreferredValues(label.text).x : 0f;
            float pad = Mathf.Max(SidePadding, (width - IconSize - IconGap - labelWidth) * 0.5f);

            UIFactory.Anchor(
                icon.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(pad, 0f),
                new Vector2(IconSize, IconSize));

            if (label != null)
            {
                UIFactory.Anchor(
                    label.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(pad + IconSize + IconGap, 0f),
                    new Vector2(labelWidth + 8f, Height));
            }
        }

        private void Awake()
        {
            Resolve();
            ApplyVisuals();
        }

        private void OnEnable()
        {
            GameLocalization.LanguageChanged += HandleLanguageChanged;
            Resolve();
            ApplyVisuals();
            RefreshState();
            if (Application.isPlaying)
            {
                StartCoroutine(Run());
            }
        }

        private void OnDisable()
        {
            GameLocalization.LanguageChanged -= HandleLanguageChanged;
            if (shine != null)
            {
                shine.anchoredPosition = new Vector2(-MaxWidth, 0f);
            }
        }

        private void HandleLanguageChanged()
        {
            if (label != null)
            {
                label.text = GameLocalization.ShopTitle;
            }

            // The caption changed length: let the orientation pass resize the button and the score column.
            OrientationHandler handler = FindObjectOfType<OrientationHandler>();
            if (handler != null)
            {
                handler.RefreshNow();
            }
            else
            {
                ((RectTransform)transform).sizeDelta = new Vector2(DesiredWidth, Height);
                Layout(DesiredWidth);
            }
        }

        private void Resolve()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (face != null && shadow != null && icon != null && label != null)
            {
                return;
            }

            shadow = FindImage(transform, "Shadow");
            face = FindImage(transform, "Face");
            alert = FindImage(transform, "Alert");
            if (face == null)
            {
                return;
            }

            shineClip = FindImage(face.transform, "ShineClip");
            Transform streak = shineClip != null ? shineClip.transform.Find("Shine") : null;
            shine = streak as RectTransform;
            icon = FindImage(face.transform, "Icon");
            Transform text = face.transform.Find("Label");
            label = text != null ? text.GetComponent<TMP_Text>() : null;
        }

        /// <summary>Runtime-only sprites: generated textures must not be baked into the scene.</summary>
        private void ApplyVisuals()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Resolve();
            SetPill(shadow);
            SetPill(face);
            SetPill(shineClip);
            if (icon != null)
            {
                // The painted shop front keeps its own colours; the drawn fallback is a tinted silhouette.
                Sprite painted = GameArt.ShopIcon;
                icon.sprite = painted != null ? painted : HudIcons.Storefront;
                if (painted != null)
                {
                    icon.color = Color.white;
                }
            }

            if (alert != null)
            {
                alert.sprite = HudIcons.Dot;
            }

            if (label != null)
            {
                label.text = GameLocalization.ShopTitle;
            }
        }

        private static void SetPill(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = HudIcons.Pill;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
        }

        private IEnumerator Run()
        {
            float sinceShine = ShineInterval * 0.5f;
            while (true)
            {
                RefreshState();
                yield return new WaitForSecondsRealtime(StateTick);
                sinceShine += StateTick;

                if (sinceShine >= ShineInterval && CanShine())
                {
                    sinceShine = 0f;
                    yield return PlayShine();
                }
            }
        }

        private bool CanShine() => shine != null && button != null && button.interactable;

        private IEnumerator PlayShine()
        {
            float half = ((RectTransform)transform).rect.width * 0.5f + 60f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / ShineDuration)
            {
                float eased = t * t * (3f - 2f * t);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-half, half, eased), 0f);
                yield return null;
            }

            shine.anchoredPosition = new Vector2(-half, 0f);
        }

        private void RefreshState()
        {
            if (alert != null)
            {
                bool show = HasAffordableTheme();
                if (alert.gameObject.activeSelf != show)
                {
                    alert.gameObject.SetActive(show);
                }
            }

            // The shop only opens while playing; dim the face so a locked button does not look live.
            if (button != null && face != null && shadow != null && button.interactable != lastInteractable)
            {
                lastInteractable = button.interactable;
                float k = lastInteractable ? 1f : 0.55f;
                face.color = new Color(FaceColor.r * k, FaceColor.g * k, FaceColor.b * k, 1f);
                shadow.color = new Color(ShadowColor.r * k, ShadowColor.g * k, ShadowColor.b * k, 1f);
            }
        }

        /// <summary>True when a paid theme is still locked and the coin balance already covers it.</summary>
        private static bool HasAffordableTheme()
        {
            for (int i = 0; i < ThemeIds.Length; i++)
            {
                string id = ThemeIds[i];
                if (PlayerProgress.OwnsTheme(id))
                {
                    continue;
                }

                int price = GameTheme.Get(id).CoinPrice;
                if (price > 0 && MetaProgress.Coins >= price)
                {
                    return true;
                }
            }

            return false;
        }

        private static Image FindImage(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Image EnsureImage(RectTransform parent, string childName, Color color, int siblingIndex)
        {
            Image image = FindImage(parent, childName);
            if (image == null)
            {
                image = UIFactory.CreateImage(childName, parent, color, false);
            }

            image.color = color;
            image.raycastTarget = false;
            if (siblingIndex >= 0)
            {
                image.transform.SetSiblingIndex(siblingIndex);
            }

            return image;
        }
    }
}
