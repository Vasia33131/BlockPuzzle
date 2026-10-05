using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The two icon buttons of the "Our games" block, drawn in code from simple shapes (no logos
    /// from outside): a white paper plane on a Telegram-blue disc and a white triangle on a
    /// YouTube-red plate. Brand colours are fixed and do not follow the theme.
    /// </summary>
    public static class SocialIcons
    {
        public static readonly Color TelegramColor = GameTheme.FromHex("#2AABEE");
        public static readonly Color YouTubeColor = GameTheme.FromHex("#FF0000");

        private const int PlaneSize = 128;

        private static Sprite planeSprite;

        /// <summary>White paper plane pointing up and to the right, tinted through the Image colour.</summary>
        public static Sprite PlaneSprite =>
            planeSprite != null ? planeSprite : planeSprite = MenuArt.Bake("SocialPlane", PlaneSize, PlaneSize, PlaneCoverage, 3);

        /// <summary>Builds a round Telegram button of <paramref name="size"/> pixels.</summary>
        public static Button CreateTelegram(Transform parent, float size)
        {
            Image disc = UIFactory.CreateImage("TelegramButton", parent, TelegramColor, false);
            disc.sprite = ProfileUi.CircleSprite;
            Button button = Finish(disc);
            AddGlyph(disc.rectTransform, PlaneSprite, size * 0.6f, new Vector2(-size * 0.02f, size * 0.02f));
            return button;
        }

        /// <summary>Builds a red rounded-plate YouTube button, <paramref name="height"/> pixels tall.</summary>
        public static Button CreateYouTube(Transform parent, float height)
        {
            Image plate = UIFactory.CreateImage("YouTubeButton", parent, YouTubeColor);
            Button button = Finish(plate);
            AddGlyph(plate.rectTransform, MenuArt.PlaySprite, height * 0.5f, new Vector2(height * 0.03f, 0f));
            return button;
        }

        private static Button Finish(Image face)
        {
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            ButtonPressAnimator.Attach(button);
            return button;
        }

        private static void AddGlyph(RectTransform parent, Sprite sprite, float size, Vector2 offset)
        {
            Image glyph = UIFactory.CreateImage("Glyph", parent, Color.white, false);
            glyph.sprite = sprite;
            glyph.preserveAspect = true;
            glyph.raycastTarget = false;
            UIFactory.Anchor(
                glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), offset, new Vector2(size, size));
        }

        /// <summary>Two wings and a small fold under them, as one silhouette.</summary>
        private static float PlaneCoverage(float u, float v)
        {
            var p = new Vector2(u, v);
            bool inside =
                InTriangle(p, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.82f), new Vector2(0.42f, 0.36f))
                || InTriangle(p, new Vector2(0.92f, 0.82f), new Vector2(0.68f, 0.16f), new Vector2(0.42f, 0.36f))
                || InTriangle(p, new Vector2(0.42f, 0.36f), new Vector2(0.45f, 0.12f), new Vector2(0.60f, 0.27f));
            return inside ? 1f : 0f;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Side(a, b, p);
            float d2 = Side(b, c, p);
            float d3 = Side(c, a, p);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Side(Vector2 a, Vector2 b, Vector2 p)
        {
            return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        }
    }
}
