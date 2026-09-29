using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Gold "play + AD" chip pinned to the top-right corner of a button. It marks the one button that
    /// starts a rewarded ad (Watch on the booster question), so the player learns about the ad only
    /// at the moment of choosing it, never on the booster bar itself.
    /// </summary>
    public static class AdChip
    {
        public const string ObjectName = "AdChip";

        private static readonly Color ChipColor = GameTheme.FromHex("#FFC83D");
        private static readonly Color ContentColor = GameTheme.FromHex("#1a1a2e");
        private static readonly Vector2 ChipSize = new Vector2(108f, 58f);

        /// <summary>Adds the chip to <paramref name="button"/> once and returns it.</summary>
        public static Image Ensure(Button button)
        {
            if (button == null)
            {
                return null;
            }

            Transform existing = button.transform.Find(ObjectName);
            Image chip = existing != null ? existing.GetComponent<Image>() : null;
            if (chip == null)
            {
                chip = UIFactory.CreateImage(ObjectName, button.transform, ChipColor);
                chip.raycastTarget = false;

                Image play = UIFactory.CreateImage("Play", chip.rectTransform, ContentColor, false);
                play.raycastTarget = false;
                play.preserveAspect = true;
                UIFactory.Anchor(
                    play.rectTransform,
                    new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f),
                    new Vector2(16f, 0f),
                    new Vector2(28f, 28f));

                TextMeshProUGUI text = UIFactory.CreateText(
                    "Text",
                    chip.rectTransform,
                    GameLocalization.AdChip,
                    34f,
                    ContentColor,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold);
                text.overflowMode = TextOverflowModes.Overflow;
                UIFactory.Stretch(text.rectTransform);
                text.rectTransform.offsetMin = new Vector2(46f, 0f);
                text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            }

            chip.transform.SetAsLastSibling();
            UIFactory.Anchor(
                chip.rectTransform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(12f, 18f),
                ChipSize);

            // The play triangle is generated at runtime, so it is never part of a baked scene.
            Transform playTransform = chip.transform.Find("Play");
            Image playImage = playTransform != null ? playTransform.GetComponent<Image>() : null;
            if (playImage != null && Application.isPlaying)
            {
                playImage.sprite = HudIcons.Play;
            }

            return chip;
        }

        /// <summary>Removes a chip an older build left on <paramref name="button"/>.</summary>
        public static void Remove(Button button)
        {
            Transform existing = button != null ? button.transform.Find(ObjectName) : null;
            if (existing == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(existing.gameObject);
            }
            else
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }
    }
}
