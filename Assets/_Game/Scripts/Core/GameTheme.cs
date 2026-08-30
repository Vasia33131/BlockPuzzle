using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Single source of truth for every colour and metric used by the game so that
    /// the visual identity can be re-skinned from one place.
    /// Background, empty cells, starting blocks, accent and the playable figure palette
    /// come from the active <see cref="ThemeConfig"/> selected by <see cref="PlayerProgress.ThemeId"/>.
    /// </summary>
    public static class GameTheme
    {
        private const string ResourcesFolder = "Themes";
        private const float ColorMatchTolerance = 0.045f;

        private static readonly Dictionary<string, ThemeConfig> catalog =
            new Dictionary<string, ThemeConfig>(StringComparer.Ordinal);

        private static ThemeConfig active;
        private static bool catalogReady;

        /// <summary>Raised after the active theme changes so live views can recolor without a reload.</summary>
        public static event Action Changed;

        /// <summary>
        /// Classic figure palette the default theme ships with. Used as the fallback when a
        /// <see cref="ThemeConfig"/> has no block colours of its own, and as the match table
        /// for figures baked before themes owned a palette.
        /// </summary>
        public static readonly Color[] ClassicPastelPalette =
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

        public static ThemeConfig Active
        {
            get
            {
                EnsureCatalog();
                if (active == null)
                {
                    active = Resolve(PlayerProgress.ThemeId);
                }

                return active;
            }
        }

        public static Color BackgroundTop => Active.BackgroundTop;
        public static Color BackgroundBottom => Active.BackgroundBottom;
        public static Color EmptyCell => Active.EmptyCell;

        public static readonly Color CellBorder = FromHex("#3a3a5a");

        /// <summary>
        /// Dim grey-blue of the blocks the board is pre-filled with when a run starts. Figures the
        /// player places keep their own colour, so this shade marks what was there from the start.
        /// </summary>
        public static Color StartingBlock => Active.StartingBlock;

        /// <summary>Lead pastel of the playable figures, and the fallback for anything unpainted.</summary>
        public static Color ShapePrimary => Pastel(0);

        /// <summary>
        /// Figure colours of the active theme. Same indices as the classic set (blue … lavender),
        /// remapped onto that theme's own shades.
        /// </summary>
        public static Color[] PastelPalette
        {
            get
            {
                Color[] palette = Active != null ? Active.BlockPalette : null;
                return palette != null && palette.Length > 0 ? palette : ClassicPastelPalette;
            }
        }

        public static readonly Color PanelBackground = new Color(1f, 1f, 1f, 0.04f);
        public static readonly Color CardBackground = FromHex("#20203c");
        public static readonly Color TextPrimary = FromHex("#e8e8f5");
        public static readonly Color TextSecondary = FromHex("#8b8bb0");
        public static Color Accent => Active.Accent;

        /// <summary>
        /// Fill of the HUD shop and pause buttons. Follows the active theme accent:
        /// yellow on Classic, turquoise on Ocean, pink on Candy.
        /// </summary>
        public static Color HudButton => Accent;

        /// <summary>Dark cart / pause bars on <see cref="HudButton"/> so the glyphs stay readable.</summary>
        public static readonly Color HudButtonIcon = FromHex("#1a1a2e");

        /// <summary>Muted button used where the accent would shout, such as "Restart" while paused.</summary>
        public static readonly Color ButtonSecondary = FromHex("#3a3a5f");

        /// <summary>Purchase CTA on shop cards that still sell a product. Owned / select states stay muted.</summary>
        public static readonly Color ShopBuy = FromHex("#2ECC71");

        /// <summary>Dark caption on the green purchase CTA so the catalog price stays readable.</summary>
        public static readonly Color ShopBuyLabel = FromHex("#1a1a2e");

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

        /// <summary>
        /// Extra bottom inset for the figure tray so the Yandex sticky banner
        /// does not cover the pieces. Zero after ad removal.
        /// </summary>
        public const float BannerReserve = 120f;

        /// <summary>Active tray lift: <see cref="BannerReserve"/> while ads are shown, otherwise 0.</summary>
        public static float ActiveBannerReserve => PlayerProgress.AdsRemoved ? 0f : BannerReserve;

        /// <summary>Full outer size of the 8x8 board in reference pixels.</summary>
        public static float BoardSize => GridSize * CellSize + (GridSize - 1) * CellSpacing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadOnStartup()
        {
            PlayerProgress.Load();
            EnsureCatalog();
            active = Resolve(PlayerProgress.ThemeId);
        }

        /// <summary>Looks up a theme by product / config id, falling back to the default palette.</summary>
        public static ThemeConfig Get(string id)
        {
            EnsureCatalog();
            return Resolve(id);
        }

        /// <summary>Re-reads <see cref="PlayerProgress.ThemeId"/> and repaints live views.</summary>
        public static void ApplyFromProgress()
        {
            EnsureCatalog();
            active = Resolve(PlayerProgress.ThemeId);
            Changed?.Invoke();
        }

        public static Color FromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }

        /// <summary>Entry <paramref name="index"/> of the active theme palette, wrapping around the end.</summary>
        public static Color Pastel(int index)
        {
            Color[] palette = PastelPalette;
            int count = palette.Length;
            if (count <= 0)
            {
                return ClassicPastelPalette[0];
            }

            return palette[((index % count) + count) % count];
        }

        /// <summary>
        /// Maps a colour stored on the board (or in a snapshot) onto the active theme:
        /// starting blocks stay <see cref="StartingBlock"/>, playable cells keep their palette index.
        /// </summary>
        public static Color RemapPlacedColor(Color color)
        {
            EnsureCatalog();
            if (MatchesStartingBlock(color))
            {
                return StartingBlock;
            }

            int index = FindPaletteIndex(color);
            return index >= 0 ? Pastel(index) : color;
        }

        /// <summary>
        /// Maps a baked figure colour onto the active palette. Custom authored colours that
        /// are not part of any theme stay as they are.
        /// </summary>
        public static Color ResolvePlayableColor(Color stored)
        {
            int index = FindPaletteIndex(stored);
            return index >= 0 ? Pastel(index) : stored;
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

        private static bool MatchesStartingBlock(Color color)
        {
            foreach (ThemeConfig theme in catalog.Values)
            {
                if (theme != null && Approximately(color, theme.StartingBlock))
                {
                    return true;
                }
            }

            return Approximately(color, FromHex("#4a4a6a"));
        }

        private static int FindPaletteIndex(Color color)
        {
            foreach (ThemeConfig theme in catalog.Values)
            {
                int index = IndexInPalette(color, theme != null ? theme.BlockPalette : null);
                if (index >= 0)
                {
                    return index;
                }
            }

            return IndexInPalette(color, ClassicPastelPalette);
        }

        private static int IndexInPalette(Color color, Color[] palette)
        {
            if (palette == null)
            {
                return -1;
            }

            for (int i = 0; i < palette.Length; i++)
            {
                if (Approximately(color, palette[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= ColorMatchTolerance
                && Mathf.Abs(a.g - b.g) <= ColorMatchTolerance
                && Mathf.Abs(a.b - b.b) <= ColorMatchTolerance;
        }

        private static void EnsureCatalog()
        {
            if (catalogReady)
            {
                return;
            }

            catalogReady = true;
            catalog.Clear();

            ThemeConfig[] loaded = Resources.LoadAll<ThemeConfig>(ResourcesFolder);
            for (int i = 0; i < loaded.Length; i++)
            {
                Register(loaded[i]);
            }

            if (!catalog.ContainsKey(ThemeConfig.DefaultId))
            {
                Register(ThemeConfig.CreateDefault());
            }

            if (!catalog.ContainsKey(ThemeConfig.OceanId))
            {
                Register(ThemeConfig.CreateOcean());
            }

            if (!catalog.ContainsKey(ThemeConfig.CandyId))
            {
                Register(ThemeConfig.CreateCandy());
            }
        }

        private static void Register(ThemeConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Id) || catalog.ContainsKey(config.Id))
            {
                return;
            }

            catalog.Add(config.Id, config);
        }

        private static ThemeConfig Resolve(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = ThemeConfig.DefaultId;
            }

            if (catalog.TryGetValue(id, out ThemeConfig config) && config != null)
            {
                return config;
            }

            if (catalog.TryGetValue(ThemeConfig.DefaultId, out config) && config != null)
            {
                return config;
            }

            ThemeConfig fallback = ThemeConfig.CreateDefault();
            catalog[ThemeConfig.DefaultId] = fallback;
            return fallback;
        }
    }
}
