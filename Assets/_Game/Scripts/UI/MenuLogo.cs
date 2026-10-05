using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The "Block Puzzle 8x8" logo. With the painted lettering in <see cref="GameArt"/> (Russian or
    /// English, by the game language) it is that picture with the "8x8" tagline under it; without it
    /// the whole logo is text: chunky letters, each in its own bright colour, with a thick dark outline
    /// and a drop shadow (the shared material of <see cref="MenuArt"/>). <see cref="Tick"/> makes it
    /// breathe. It sits on its own canvas so the per-frame scale does not rebatch the rest of the menu.
    /// </summary>
    public sealed class MenuLogo : MonoBehaviour
    {
        public const string ObjectName = "MenuLogo";

        private const string Tagline = "8×8";
        private const float SecondLineSize = 0.62f;
        private const float BreathSpeed = 1.7f;
        private const float BreathAmount = 0.035f;

        /// <summary>Share of the logo height the painted lettering takes; the tagline gets the rest.</summary>
        private const float ArtShare = 0.72f;

        /// <summary>Height of the "8x8" line, as a share of the logo height.</summary>
        private const float TaglineShare = 0.26f;

        /// <summary>The tagline tucks this far (share of the logo height) under the lettering: text has empty line padding.</summary>
        private const float TaglineOverlap = 0.03f;

        private RectTransform content;
        private Image art;
        private TMP_Text label;

        /// <summary>Builds the logo filling <paramref name="parent"/>; size and place the parent yourself.</summary>
        public static MenuLogo Create(RectTransform parent)
        {
            RectTransform root = UIFactory.CreateRect(ObjectName, parent);
            UIFactory.Stretch(root);
            root.gameObject.AddComponent<Canvas>();
            var logo = root.gameObject.AddComponent<MenuLogo>();
            logo.Build(root);
            return logo;
        }

        /// <summary>Rebuilds the coloured text, for the language the game is in now.</summary>
        public void Refresh()
        {
            if (label == null)
            {
                return;
            }

            Sprite lettering = GameArt.Logo;
            art.sprite = lettering;
            art.gameObject.SetActive(lettering != null);

            var text = new StringBuilder(256);
            int index = 0;
            if (lettering != null)
            {
                // The picture carries the name; the text below is only the tagline.
                AppendColoured(text, Tagline, ref index);
            }
            else
            {
                UIFactory.Stretch(label.rectTransform);
                AppendColoured(text, GameLocalization.MenuTitle, ref index);
                text.Append("\n<size=").Append(Mathf.RoundToInt(SecondLineSize * 100f)).Append("%>");
                AppendColoured(text, Tagline, ref index);
                text.Append("</size>");
            }

            label.text = text.ToString();
            LayoutLettering();
        }

        private void OnRectTransformDimensionsChange() => LayoutLettering();

        /// <summary>
        /// Sizes the lettering to the height the picture really takes (the Russian one is a single wide
        /// line, the English one two lines) and hangs the tagline right under it, so there is no empty
        /// band between the name and "8x8" whatever the picture's shape. The pair is centred in the slot.
        /// </summary>
        private void LayoutLettering()
        {
            if (content == null || art == null || art.sprite == null || !art.gameObject.activeSelf)
            {
                return;
            }

            float width = content.rect.width;
            float height = content.rect.height;
            if (width <= 1f || height <= 1f)
            {
                return;
            }

            Rect sprite = art.sprite.rect;
            float artHeight = Mathf.Min(height * ArtShare, width * sprite.height / Mathf.Max(1f, sprite.width));
            float taglineHeight = height * TaglineShare;
            float gap = -height * TaglineOverlap;
            float top = (height - (artHeight + gap + taglineHeight)) * 0.5f;

            var topCentre = new Vector2(0.5f, 1f);
            UIFactory.Anchor(art.rectTransform, topCentre, topCentre, new Vector2(0f, -top), new Vector2(width, artHeight));
            UIFactory.Anchor(
                label.rectTransform, topCentre, topCentre, new Vector2(0f, -(top + artHeight + gap)),
                new Vector2(width, taglineHeight));
        }

        /// <summary>Slow "breathing" scale; called once per frame by the menu, allocation free.</summary>
        public void Tick(float time)
        {
            if (content == null)
            {
                return;
            }

            float scale = 1f + Mathf.Sin(time * BreathSpeed) * BreathAmount;
            content.localScale = new Vector3(scale, scale, 1f);
        }

        private void Build(RectTransform root)
        {
            content = UIFactory.CreateRect("Content", root);
            UIFactory.Stretch(content);

            art = UIFactory.CreateImage("Lettering", content, Color.white, false);
            art.preserveAspect = true;
            art.raycastTarget = false;
            UIFactory.Stretch(art.rectTransform);
            art.rectTransform.anchorMin = new Vector2(0f, 1f - ArtShare);

            TextMeshProUGUI text = UIFactory.CreateText(
                "Title", content, string.Empty, 200f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.enableAutoSizing = true;
            text.fontSizeMin = 60f;
            text.fontSizeMax = 210f;
            text.lineSpacing = -24f;
            text.overflowMode = TextOverflowModes.Overflow;
            MenuArt.ApplyOutlinedMaterial(text);
            label = text;
            Refresh();
        }

        /// <summary>Wraps every visible character in the next logo colour; spaces stay plain.</summary>
        private static void AppendColoured(StringBuilder text, string content, ref int index)
        {
            Color[] palette = MenuArt.LogoColors;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == ' ')
                {
                    text.Append(c);
                    continue;
                }

                string hex = ColorUtility.ToHtmlStringRGB(palette[index % palette.Length]);
                text.Append("<color=#").Append(hex).Append('>').Append(c).Append("</color>");
                index++;
            }
        }
    }
}
