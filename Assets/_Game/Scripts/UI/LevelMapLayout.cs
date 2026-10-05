using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Levels;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Geometry of the level map, pure maths with no scene objects: the nodes ride a sine wave from the
    /// bottom up (level 1 at the bottom, the trophy above level 100), a chapter starts every
    /// <see cref="ChapterSize"/> levels, and the dotted trail between two nodes follows the same wave.
    /// Positions are content coordinates: x from the centre of the map, y from its bottom edge.
    /// </summary>
    public static class LevelMapLayout
    {
        public const int LevelCount = LevelDatabase.LevelCount;

        /// <summary>The final node, one step above level 100.</summary>
        public const int TrophyIndex = LevelCount + 1;

        public const int ChapterSize = 20;
        public const int ChapterCount = LevelCount / ChapterSize;

        public const float Step = 320f;
        public const float BottomPadding = 340f;
        public const float TopPadding = 560f;
        public const float Amplitude = 290f;

        /// <summary>Nodes per full swing of the wave.</summary>
        private const float Period = 7.2f;

        public const int MaxDotsPerSegment = 12;
        private const float DotSpacing = 38f;
        private const float KeepOutRadius = 105f;
        private const float StripHalfWidth = 95f;
        private const float StripDepth = 150f;
        private const int CurveSamples = 64;

        public static readonly Color[] ChapterColors =
        {
            GameTheme.FromHex("#4F7CFF"),
            GameTheme.FromHex("#27C99A"),
            GameTheme.FromHex("#FF9B3D"),
            GameTheme.FromHex("#E9509E"),
            GameTheme.FromHex("#9B6BFF")
        };

        private static readonly Vector2[][] dotCache = new Vector2[LevelCount + 1][];
        private static readonly List<Vector2> scratch = new List<Vector2>(MaxDotsPerSegment);

        public static float ContentHeight => BottomPadding + (TrophyIndex - 1) * Step + TopPadding;

        /// <summary>Centre of a node on the wave. Fractions give the points of the trail between two nodes.</summary>
        public static Vector2 Point(float index)
        {
            float x = Amplitude * Mathf.Sin((index - 1f) * 2f * Mathf.PI / Period);
            float y = BottomPadding + (index - 1f) * Step;
            return new Vector2(x, y);
        }

        /// <summary>Zero-based chapter of a level (the trophy belongs to the last one).</summary>
        public static int ChapterOf(int index) => Mathf.Clamp((index - 1) / ChapterSize, 0, ChapterCount - 1);

        public static Color ChapterColor(int chapter) => ChapterColors[Mathf.Clamp(chapter, 0, ChapterColors.Length - 1)];

        /// <summary>
        /// The trail from level 20, 40, 60 and 80 to the next level carries the chapter banner instead of dots.
        /// </summary>
        public static bool IsGate(int segment) => segment > 0 && segment < LevelCount && segment % ChapterSize == 0;

        /// <summary>
        /// Dots of the trail from node <paramref name="segment"/> to the next one, spaced along the curve and
        /// clear of both nodes and of the star strip under the upper node. Cached, so callers must not edit it.
        /// </summary>
        public static Vector2[] DotPositions(int segment)
        {
            if (segment < 1 || segment > LevelCount || IsGate(segment))
            {
                return System.Array.Empty<Vector2>();
            }

            Vector2[] cached = dotCache[segment];
            if (cached != null)
            {
                return cached;
            }

            Vector2 lower = Point(segment);
            Vector2 upper = Point(segment + 1);
            scratch.Clear();

            Vector2 previous = lower;
            float travelled = 0f;
            float nextDot = DotSpacing * 0.5f;
            for (int i = 1; i <= CurveSamples; i++)
            {
                Vector2 point = Point(segment + i / (float)CurveSamples);
                float length = Vector2.Distance(previous, point);
                while (length > 0f && travelled + length >= nextDot && scratch.Count < MaxDotsPerSegment)
                {
                    Vector2 dot = Vector2.Lerp(previous, point, (nextDot - travelled) / length);
                    nextDot += DotSpacing;

                    bool clear = Vector2.Distance(dot, lower) >= KeepOutRadius
                        && Vector2.Distance(dot, upper) >= KeepOutRadius
                        && !(Mathf.Abs(dot.x - upper.x) < StripHalfWidth && dot.y < upper.y && dot.y > upper.y - StripDepth);
                    if (clear)
                    {
                        scratch.Add(dot);
                    }
                }

                travelled += length;
                previous = point;
            }

            dotCache[segment] = scratch.ToArray();
            return dotCache[segment];
        }
    }
}
