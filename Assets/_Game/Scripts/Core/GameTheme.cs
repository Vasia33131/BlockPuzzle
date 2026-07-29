using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Single source of truth for every colour and metric used by the game so that
    /// the visual identity can be re-skinned from one place.
    /// </summary>
    public static class GameTheme
    {
        public static readonly Color BackgroundTop = FromHex("#1a1a2e");
        public static readonly Color BackgroundBottom = FromHex("#16213e");

        public static readonly Color EmptyCell = FromHex("#2a2a4a");
        public static readonly Color CellBorder = FromHex("#3a3a5a");

        /// <summary>
        /// Dim grey-blue of the blocks the board is pre-filled with when a run starts. Figures the
        /// player places keep their own colour, so this shade marks what was there from the start.
        /// </summary>
        public static readonly Color StartingBlock = FromHex("#4a4a6a");

        /// <summary>Lead pastel of the playable figures, and the fallback for anything unpainted.</summary>
        public static readonly Color ShapePrimary = FromHex("#5dade2");

        /// <summary>
        /// Pastel range the playable figures are painted from. Every entry is light and low in
        /// saturation, so the figures read as a single family and stay clearly apart from the
        /// dim <see cref="StartingBlock"/> filling of the board.
        /// </summary>
        public static readonly Color[] PastelPalette =
        {
            FromHex("#5dade2"), // blue
            FromHex("#aed6f1"), // sky
            FromHex("#a3e4d7"), // aqua
            FromHex("#a9dfbf"), // mint
            FromHex("#f9e79f"), // butter
            FromHex("#fad7a0"), // peach
            FromHex("#f5b7b1"), // rose
            FromHex("#d7bde2")  // lavender
        };

        public static readonly Color PanelBackground = new Color(1f, 1f, 1f, 0.04f);
        public static readonly Color CardBackground = FromHex("#20203c");
        public static readonly Color TextPrimary = FromHex("#e8e8f5");
        public static readonly Color TextSecondary = FromHex("#8b8bb0");
        public static readonly Color Accent = FromHex("#f2b134");

        /// <summary>Muted button used where the accent would shout, such as "Restart" while paused.</summary>
        public static readonly Color ButtonSecondary = FromHex("#3a3a5f");

        /// <summary>Drop highlight when every cell under the dragged figure is free.</summary>
        public static readonly Color HighlightValid = WithAlpha(FromHex("#00ff88"), 0.5f);

        /// <summary>Drop highlight when a cell is taken or the figure hangs off the board.</summary>
        public static readonly Color HighlightInvalid = WithAlpha(FromHex("#ff3366"), 0.5f);

        /// <summary>Outline drawn around a highlighted cell, kept denser than its fill.</summary>
        public const float HighlightOutlineAlpha = 0.85f;

        public const int GridSize = 8;
        public const float CellSize = 90f;
        public const float CellSpacing = 4f;
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        /// <summary>Full outer size of the 8x8 board in reference pixels.</summary>
        public static float BoardSize => GridSize * CellSize + (GridSize - 1) * CellSpacing;

        public static Color FromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }

        /// <summary>Entry <paramref name="index"/> of the pastel range, wrapping around the end.</summary>
        public static Color Pastel(int index)
        {
            int count = PastelPalette.Length;
            return PastelPalette[((index % count) + count) % count];
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        /// <summary>Slightly brighter variant used for the top highlight of a filled block.</summary>
        public static Color Lighten(Color color, float amount = 0.22f)
        {
            return Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
        }

        /// <summary>Slightly darker variant used for the bottom shade of a filled block.</summary>
        public static Color Darken(Color color, float amount = 0.28f)
        {
            Color dark = Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
            dark.a = color.a;
            return dark;
        }
    }
}
