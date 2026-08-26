using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Shared setup for the tiled overlays painted on cubes and the scene backdrop.
    /// Patterns are white/grey with alpha; tinting is applied through <see cref="Image.color"/>.
    /// </summary>
    public static class ThemePattern
    {
        public const string BlockChildName = "Pattern";
        public const string BackgroundChildName = "BackgroundPattern";
        public const float BlockOverlayAlpha = 0.42f;

        /// <summary>White overlay so the theme's block pattern reads on top of the cube tint.</summary>
        public static Color BlockOverlayTint => new Color(1f, 1f, 1f, BlockOverlayAlpha);

        public static Image EnsureChild(Transform parent, string name, int siblingIndex)
        {
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find(name);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image == null)
            {
                image = UIFactory.CreateImage(name, parent, Color.white, rounded: false);
            }

            image.raycastTarget = false;
            image.maskable = true;
            int childCount = parent.childCount;
            image.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, Mathf.Max(0, childCount - 1)));
            UIFactory.Stretch(image.rectTransform);
            return image;
        }

        public static void ApplyBlockOverlay(Image image, bool visible)
        {
            if (image == null)
            {
                return;
            }

            Sprite sprite = GameTheme.Active != null ? GameTheme.Active.BlockPattern : null;
            ConfigureTiled(image, sprite, BlockOverlayTint, visible && sprite != null);
        }

        public static void ApplyBackgroundOverlay(Image image)
        {
            if (image == null)
            {
                return;
            }

            ThemeConfig theme = GameTheme.Active;
            Sprite sprite = theme != null ? theme.BackgroundPattern : null;
            Color tint = GameTheme.BackgroundBottom;
            tint.a = theme != null ? theme.BackgroundPatternAlpha : 0.16f;
            ConfigureTiled(image, sprite, tint, sprite != null);
        }

        public static Mask EnsureRoundedMask(GameObject host)
        {
            if (host == null)
            {
                return null;
            }

            Mask mask = host.GetComponent<Mask>();
            if (mask == null)
            {
                mask = host.AddComponent<Mask>();
            }

            mask.showMaskGraphic = true;
            return mask;
        }

        public static void ConfigureTiled(Image image, Sprite sprite, Color color, bool enabled)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = TileMultiplier(sprite);
            image.color = color;
            image.raycastTarget = false;
            image.enabled = enabled && sprite != null;
        }

        /// <summary>
        /// Patterns were authored as 1024px (backdrop) and 256px (cubes). Exported
        /// copies are smaller; this keeps the on-screen repeat size the same.
        /// </summary>
        private static float TileMultiplier(Sprite sprite)
        {
            if (sprite == null)
            {
                return 1f;
            }

            float design = sprite.name.StartsWith("Bg", System.StringComparison.Ordinal)
                ? PatternTile.BackgroundPixels
                : PatternTile.BlockPixels;
            float width = sprite.rect.width;
            if (width < 1f)
            {
                return 1f;
            }

            return Mathf.Max(0.05f, width / design);
        }

        private static class PatternTile
        {
            public const float BackgroundPixels = 1024f;
            public const float BlockPixels = 256f;
        }
    }
}
