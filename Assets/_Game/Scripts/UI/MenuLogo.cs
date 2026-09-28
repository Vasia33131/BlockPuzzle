using System.Text;
using TMPro;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The "Block Puzzle 8x8" logo: chunky letters, each in its own bright colour, with a thick
    /// dark outline and a drop shadow to the lower right (the shared outline + underlay material of
    /// <see cref="MenuArt"/>). The letters are coloured through rich text, so it is one TextMeshPro
    /// object; <see cref="Tick"/> makes it breathe. It sits on its own canvas so the per-frame scale
    /// does not rebatch the rest of the menu.
    /// </summary>
    public sealed class MenuLogo : MonoBehaviour
    {
        public const string ObjectName = "MenuLogo";

        private const string Tagline = "8×8";
        private const float SecondLineSize = 0.62f;
        private const float BreathSpeed = 1.7f;
        private const float BreathAmount = 0.035f;

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

            var text = new StringBuilder(256);
            int index = 0;
            AppendColoured(text, GameLocalization.MenuTitle, ref index);
            text.Append("\n<size=").Append(Mathf.RoundToInt(SecondLineSize * 100f)).Append("%>");
            AppendColoured(text, Tagline, ref index);
            text.Append("</size>");
            label.text = text.ToString();
        }

        /// <summary>Slow "breathing" scale; called once per frame by the menu, allocation free.</summary>
        public void Tick(float time)
        {
            if (label == null)
            {
                return;
            }

            float scale = 1f + Mathf.Sin(time * BreathSpeed) * BreathAmount;
            label.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Build(RectTransform root)
        {
            TextMeshProUGUI text = UIFactory.CreateText(
                "Title", root, string.Empty, 200f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
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
