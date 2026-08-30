using UnityEngine;

namespace BlockPuzzle.Core
{
    /// <summary>
    /// Palette for one visual theme: background ramp, empty cells, starting blocks, accent,
    /// the eight playable figure colours, and the tiled overlays used on cubes and the backdrop.
    /// </summary>
    [CreateAssetMenu(fileName = "ThemeConfig", menuName = "Block Puzzle/Theme Config")]
    public class ThemeConfig : ScriptableObject
    {
        public const string DefaultId = "default";
        public const string OceanId = "theme_ocean";
        public const string CandyId = "theme_candy";

        public const string DefaultBlockPatternPath = "UI/Patterns/BlockClassic";
        public const string OceanBlockPatternPath = "UI/Patterns/BlockOcean";
        public const string CandyBlockPatternPath = "UI/Patterns/BlockCandy";
        public const string DefaultBackgroundPatternPath = "UI/Patterns/BgClassic";
        public const string OceanBackgroundPatternPath = "UI/Patterns/BgOcean";
        public const string CandyBackgroundPatternPath = "UI/Patterns/BgCandy";

        public const int PaletteSize = 8;

        [SerializeField] private string id = DefaultId;
        [SerializeField] private string displayName = "Классика";
        [SerializeField] private Color backgroundTop = new Color(0.10196079f, 0.10196079f, 0.18039216f, 1f);
        [SerializeField] private Color backgroundBottom = new Color(0.08627451f, 0.12941177f, 0.24313726f, 1f);
        [SerializeField] private Color emptyCell = new Color(0.16470589f, 0.16470589f, 0.2901961f, 1f);
        [SerializeField] private Color startingBlock = new Color(0.2901961f, 0.2901961f, 0.41568628f, 1f);
        [SerializeField] private Color accent = new Color(0.9490196f, 0.69411767f, 0.20392157f, 1f);
        [SerializeField] private Color[] blockPalette;
        [SerializeField] private Sprite blockPattern;
        [SerializeField] private Sprite backgroundPattern;
        [SerializeField] private float backgroundPatternAlpha = 0.16f;

        public string Id => string.IsNullOrEmpty(id) ? DefaultId : id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? Id : displayName;
        public Color BackgroundTop => backgroundTop;
        public Color BackgroundBottom => backgroundBottom;
        public Color EmptyCell => emptyCell;
        public Color StartingBlock => startingBlock;
        public Color Accent => accent;
        public Sprite BlockPattern => blockPattern != null ? blockPattern : LoadSprite(ResolveBlockPatternPath());
        public Sprite BackgroundPattern => backgroundPattern != null ? backgroundPattern : LoadSprite(ResolveBackgroundPatternPath());
        public float BackgroundPatternAlpha => Mathf.Clamp01(backgroundPatternAlpha);

        /// <summary>
        /// Eight figure colours for this theme, indexed like the classic set:
        /// blue, sky, aqua, mint, butter, peach, rose, lavender.
        /// </summary>
        public Color[] BlockPalette
        {
            get
            {
                if (blockPalette != null && blockPalette.Length >= PaletteSize)
                {
                    return blockPalette;
                }

                return GameTheme.ClassicPastelPalette;
            }
        }

        /// <summary>Built-in classic palette — the colours GameTheme shipped with.</summary>
        public static ThemeConfig CreateDefault()
        {
            return Create(
                DefaultId,
                "Классика",
                GameTheme.FromHex("#1a1a2e"),
                GameTheme.FromHex("#16213e"),
                GameTheme.FromHex("#2a2a4a"),
                GameTheme.FromHex("#4a4a6a"),
                GameTheme.FromHex("#f2b134"),
                CopyPalette(GameTheme.ClassicPastelPalette),
                LoadSprite(DefaultBlockPatternPath),
                LoadSprite(DefaultBackgroundPatternPath),
                0.14f);
        }

        public static ThemeConfig CreateOcean()
        {
            return Create(
                OceanId,
                "Океан",
                GameTheme.FromHex("#0b3d5c"),
                GameTheme.FromHex("#021e2b"),
                GameTheme.FromHex("#1a5570"),
                GameTheme.FromHex("#2a8aaa"),
                GameTheme.FromHex("#4ecdc4"),
                new[]
                {
                    GameTheme.FromHex("#5eead4"), // turquoise
                    GameTheme.FromHex("#7dd3fc"), // azure
                    GameTheme.FromHex("#e0f2fe"), // foam
                    GameTheme.FromHex("#34d399"), // emerald
                    GameTheme.FromHex("#fde68a"), // sand
                    GameTheme.FromHex("#fb7185"), // coral
                    GameTheme.FromHex("#f9a8d4"), // shell pink
                    GameTheme.FromHex("#818cf8")  // indigo
                },
                LoadSprite(OceanBlockPatternPath),
                LoadSprite(OceanBackgroundPatternPath),
                0.18f);
        }

        public static ThemeConfig CreateCandy()
        {
            return Create(
                CandyId,
                "Конфеты",
                GameTheme.FromHex("#4a1942"),
                GameTheme.FromHex("#2b0f24"),
                GameTheme.FromHex("#6d3a62"),
                GameTheme.FromHex("#d478a5"),
                GameTheme.FromHex("#ff8dc7"),
                new[]
                {
                    GameTheme.FromHex("#ff6b9d"), // strawberry
                    GameTheme.FromHex("#ffb4e6"), // bubblegum
                    GameTheme.FromHex("#ffe566"), // lemon
                    GameTheme.FromHex("#7af0c3"), // mint
                    GameTheme.FromHex("#a78bfa"), // blueberry
                    GameTheme.FromHex("#f4b942"), // caramel
                    GameTheme.FromHex("#fff5f7"), // marshmallow
                    GameTheme.FromHex("#c084fc")  // grape
                },
                LoadSprite(CandyBlockPatternPath),
                LoadSprite(CandyBackgroundPatternPath),
                0.16f);
        }

        public static ThemeConfig Create(
            string themeId,
            string title,
            Color top,
            Color bottom,
            Color empty,
            Color start,
            Color accentColor,
            Color[] palette,
            Sprite cubePattern,
            Sprite backdropPattern,
            float backdropAlpha)
        {
            ThemeConfig config = CreateInstance<ThemeConfig>();
            config.name = string.IsNullOrEmpty(title) ? themeId : title;
            config.id = themeId;
            config.displayName = title;
            config.backgroundTop = top;
            config.backgroundBottom = bottom;
            config.emptyCell = empty;
            config.startingBlock = start;
            config.accent = accentColor;
            config.blockPalette = palette != null && palette.Length > 0
                ? palette
                : CopyPalette(GameTheme.ClassicPastelPalette);
            config.blockPattern = cubePattern;
            config.backgroundPattern = backdropPattern;
            config.backgroundPatternAlpha = Mathf.Clamp01(backdropAlpha);
            return config;
        }

        private static string ResolveBlockPatternPath(string themeId)
        {
            if (themeId == OceanId)
            {
                return OceanBlockPatternPath;
            }

            if (themeId == CandyId)
            {
                return CandyBlockPatternPath;
            }

            return DefaultBlockPatternPath;
        }

        private static string ResolveBackgroundPatternPath(string themeId)
        {
            if (themeId == OceanId)
            {
                return OceanBackgroundPatternPath;
            }

            if (themeId == CandyId)
            {
                return CandyBackgroundPatternPath;
            }

            return DefaultBackgroundPatternPath;
        }

        private string ResolveBlockPatternPath() => ResolveBlockPatternPath(Id);

        private string ResolveBackgroundPatternPath() => ResolveBackgroundPatternPath(Id);

        private static Color[] CopyPalette(Color[] source)
        {
            var copy = new Color[PaletteSize];
            if (source == null)
            {
                return copy;
            }

            int count = Mathf.Min(PaletteSize, source.Length);
            for (int i = 0; i < count; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            return string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
        }
    }
}
