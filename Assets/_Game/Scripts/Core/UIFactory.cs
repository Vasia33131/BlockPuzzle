using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Small helper used by every view to build uGUI objects in code, so the whole
    /// interface can be generated either at edit time (baked into the scene) or at runtime.
    /// It lives in Core because the board and the figures build themselves out of uGUI
    /// too, and they must not depend on the game's own UI assembly.
    /// </summary>
    public static class UIFactory
    {
        private const string RoundedSpriteResourcePath = "UI/RoundedRect";

        private static Sprite roundedSprite;
        private static bool roundedSpriteLoaded;

        /// <summary>Nine-sliced rounded rectangle used for panels and blocks. May be null.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (!roundedSpriteLoaded)
                {
                    roundedSprite = Resources.Load<Sprite>(RoundedSpriteResourcePath);
                    roundedSpriteLoaded = true;
                }

                return roundedSprite;
            }
        }

        /// <summary>Drops the cached sprite so a freshly generated asset is picked up.</summary>
        public static void ClearCache()
        {
            roundedSprite = null;
            roundedSpriteLoaded = false;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Color color, bool rounded = true)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            if (rounded && RoundedSprite != null)
            {
                image.sprite = RoundedSprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
            }

            return image;
        }

        public static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Rounded button with a centred TextMeshPro caption. The click handler is not wired
        /// here on purpose: listeners added from code are not saved into a scene asset, so the
        /// components that own a button subscribe to it when they wake up.
        /// </summary>
        public static Button CreateButton(
            string name,
            Transform parent,
            string caption,
            Color background,
            Color labelColor,
            float fontSize = 48f)
        {
            Image image = CreateImage(name, parent, background);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            ButtonPressAnimator.Attach(button);

            TextMeshProUGUI label = CreateText(
                "Label", image.rectTransform, caption, fontSize, labelColor, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(label.rectTransform);

            return button;
        }

        public static void SetText(TMP_Text label, string content)
        {
            if (label != null)
            {
                label.text = content ?? string.Empty;
            }
        }

        public static void SetButtonText(Button button, string content)
        {
            if (button == null)
            {
                return;
            }

            SetText(button.GetComponentInChildren<TMP_Text>(true), content);
        }

        /// <summary>Makes the rect fill its parent, optionally inset by <paramref name="padding"/>.</summary>
        public static RectTransform Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
            return rect;
        }

        /// <summary>Anchors the rect to a single point of its parent and gives it a fixed size.</summary>
        public static RectTransform Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }
    }
}
