using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// One menu button. <see cref="Slot"/> is what the menu lays out and pops in; the
    /// <see cref="Button"/> below it takes the press squash; <see cref="Visual"/> is what the idle
    /// animation (pulse) drives, so the three never fight over one transform's scale.
    /// </summary>
    public sealed class MenuButton
    {
        public RectTransform Slot;
        public RectTransform Visual;
        public Button Button;
        public TMP_Text Label;

        /// <summary>The moving highlight of a shiny button; null otherwise.</summary>
        public RectTransform Shine;

        /// <summary>Icon and caption row of a wide button; null for a round one.</summary>
        public RectTransform Content;

        /// <summary>Small line under the caption, added by <see cref="MenuButtonFactory.AddSubtitle"/>; null otherwise.</summary>
        public TMP_Text Subtitle;

        public void SetText(string content) => UIFactory.SetText(Label, content);

        /// <summary>
        /// Writes the small line under the caption. An empty one gives its room back, so the icon
        /// and caption row sits in the middle of the face instead of hanging above a blank strip.
        /// </summary>
        public void SetSubtitle(string content)
        {
            if (Subtitle == null)
            {
                return;
            }

            bool shown = !string.IsNullOrEmpty(content);
            UIFactory.SetText(Subtitle, content);
            Subtitle.gameObject.SetActive(shown);
            if (Content != null)
            {
                Content.offsetMin = new Vector2(0f, shown ? MenuButtonFactory.SubtitleReserve : 0f);
            }
        }
    }

    /// <summary>Builds the chunky menu buttons: a coloured face over a darker lower edge, with a gloss strip.</summary>
    public static class MenuButtonFactory
    {
        private const float EdgeHeight = 14f;
        private const float IconGap = 28f;
        private const float LabelSize = 62f;
        private const float ShineWidth = 70f;
        private const float SubtitleHeight = 56f;
        private const float SubtitleBottom = 18f;

        /// <summary>Room the subtitle takes at the bottom of the face, under the icon and caption row.</summary>
        public const float SubtitleReserve = SubtitleBottom + SubtitleHeight - 8f;

        /// <summary>
        /// Wide rounded button with an optional icon left of a white outlined caption.
        /// <paramref name="animated"/> moves the visuals onto their own canvas (for the pulse and the
        /// shine) and adds the moving highlight.
        /// </summary>
        public static MenuButton CreateWide(
            string name,
            Transform parent,
            Vector2 size,
            Color top,
            Color bottom,
            Color edge,
            Sprite icon,
            Vector2 iconSize,
            bool animated)
        {
            MenuButton result = CreateShell(name, parent, size, animated);
            RectTransform visual = result.Visual;

            Image edgeImage = UIFactory.CreateImage("Edge", visual, edge);
            edgeImage.raycastTarget = false;
            UIFactory.Stretch(edgeImage.rectTransform);

            Image face = UIFactory.CreateImage("Face", visual, Color.white);
            face.raycastTarget = false;
            UIFactory.Stretch(face.rectTransform);
            face.rectTransform.offsetMin = new Vector2(0f, EdgeHeight);
            face.gameObject.AddComponent<VerticalGradient>().SetColors(top, bottom);

            Image gloss = UIFactory.CreateImage("Gloss", face.rectTransform, new Color(1f, 1f, 1f, 0.2f));
            gloss.raycastTarget = false;
            gloss.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            gloss.rectTransform.anchorMax = Vector2.one;
            gloss.rectTransform.offsetMin = new Vector2(12f, 0f);
            gloss.rectTransform.offsetMax = new Vector2(-12f, -10f);

            if (animated)
            {
                RectTransform clip = UIFactory.CreateRect("Clip", face.rectTransform);
                UIFactory.Stretch(clip, 8f);
                clip.gameObject.AddComponent<RectMask2D>();

                Image shine = UIFactory.CreateImage("Shine", clip, new Color(1f, 1f, 1f, 0.34f), false);
                shine.raycastTarget = false;
                UIFactory.Anchor(
                    shine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(ShineWidth, size.y * 1.8f));
                shine.rectTransform.localEulerAngles = new Vector3(0f, 0f, 18f);
                result.Shine = shine.rectTransform;
            }

            RectTransform content = UIFactory.CreateRect("Content", face.rectTransform);
            UIFactory.Stretch(content);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = IconGap;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (icon != null)
            {
                Image iconImage = UIFactory.CreateImage("Icon", content, Color.white, false);
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                var iconLayout = iconImage.gameObject.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = iconSize.x;
                iconLayout.preferredHeight = iconSize.y;
            }

            TextMeshProUGUI label = UIFactory.CreateText(
                "Label", content, string.Empty, LabelSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuArt.ApplyOutlinedMaterial(label);
            result.Label = label;
            result.Content = content;
            return result;
        }

        /// <summary>
        /// Adds a small caption under the main label of a wide button ("Level 13" under "LEVELS"): the icon
        /// and label row moves up to make room and the subtitle sits on the lower part of the face.
        /// </summary>
        public static void AddSubtitle(MenuButton button, float fontSize, Color color)
        {
            if (button == null || button.Content == null || button.Subtitle != null)
            {
                return;
            }

            button.Content.offsetMin = new Vector2(0f, SubtitleReserve);

            TextMeshProUGUI subtitle = UIFactory.CreateText(
                "Subtitle", button.Content.parent, string.Empty, fontSize, color, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuArt.ApplyOutlinedMaterial(subtitle);
            // Overflow, not Ellipsis: with the outline padding a line can be a hair taller than the
            // rect, and Ellipsis then drops the whole line and the subtitle vanishes. Auto-size keeps it inside.
            subtitle.overflowMode = TextOverflowModes.Overflow;
            subtitle.enableWordWrapping = false;
            UIFactory.FitText(subtitle);
            RectTransform rect = subtitle.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, SubtitleBottom);
            rect.sizeDelta = new Vector2(-48f, SubtitleHeight);
            button.Subtitle = subtitle;
        }

        /// <summary>Round button with a white icon (the settings gear).</summary>
        public static MenuButton CreateRound(
            string name, Transform parent, float size, Color top, Color bottom, Color edge, Sprite icon)
        {
            MenuButton result = CreateShell(name, parent, new Vector2(size, size), false);
            RectTransform visual = result.Visual;

            Image edgeImage = UIFactory.CreateImage("Edge", visual, edge, false);
            edgeImage.sprite = ProfileUi.CircleSprite;
            edgeImage.raycastTarget = false;
            UIFactory.Stretch(edgeImage.rectTransform);

            Image face = UIFactory.CreateImage("Face", visual, Color.white, false);
            face.sprite = ProfileUi.CircleSprite;
            face.raycastTarget = false;
            UIFactory.Stretch(face.rectTransform);
            face.rectTransform.offsetMin = new Vector2(0f, EdgeHeight * 0.6f);
            face.gameObject.AddComponent<VerticalGradient>().SetColors(top, bottom);

            Image iconImage = UIFactory.CreateImage("Icon", face.rectTransform, Color.white, false);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            UIFactory.Anchor(
                iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size * 0.58f, size * 0.58f));
            return result;
        }

        /// <summary>Slot, invisible hit area with the Button, and the visual holder.</summary>
        private static MenuButton CreateShell(string name, Transform parent, Vector2 size, bool ownCanvas)
        {
            RectTransform slot = UIFactory.CreateRect(name + "Slot", parent);
            slot.sizeDelta = size;

            Image hit = UIFactory.CreateImage(name, slot, new Color(1f, 1f, 1f, 0f), false);
            hit.canvasRenderer.cullTransparentMesh = false;
            UIFactory.Stretch(hit.rectTransform);

            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            ButtonPressAnimator.Attach(button);

            RectTransform visual = UIFactory.CreateRect("Visual", hit.rectTransform);
            UIFactory.Stretch(visual);
            if (ownCanvas)
            {
                visual.gameObject.AddComponent<Canvas>();
            }

            return new MenuButton { Slot = slot, Visual = visual, Button = button };
        }
    }
}
