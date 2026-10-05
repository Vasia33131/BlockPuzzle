using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// One pooled piece of the dotted trail between two nodes of the level map. It owns a fixed set of
    /// dots and shows as many as the trail needs. A trail to an open level is lit in the colour of its
    /// chapter; <see cref="PlayReveal"/> lights the dots one after another when the level has just opened.
    /// </summary>
    public sealed class LevelMapSegment : MonoBehaviour
    {
        private const float DotSize = 20f;
        private static readonly Color UnlitColor = new Color(1f, 1f, 1f, 0.2f);

        private readonly Image[] dots = new Image[LevelMapLayout.MaxDotsPerSegment];
        private int count;

        /// <summary>The node this trail leaves; it ends at <c>Index + 1</c>.</summary>
        public int Index { get; private set; }

        public int DotCount => count;

        public static LevelMapSegment Create(Transform parent)
        {
            RectTransform root = UIFactory.CreateRect("Segment", parent);
            var segment = root.gameObject.AddComponent<LevelMapSegment>();
            for (int i = 0; i < segment.dots.Length; i++)
            {
                Image dot = UIFactory.CreateImage($"Dot_{i}", root, Color.white, false);
                dot.sprite = ProfileUi.CircleSprite;
                dot.raycastTarget = false;
                dot.rectTransform.sizeDelta = new Vector2(DotSize, DotSize);
                dot.gameObject.SetActive(false);
                segment.dots[i] = dot;
            }

            return segment;
        }

        /// <summary>Lays the trail from node <paramref name="index"/> upwards; <paramref name="lit"/> paints it as travelled.</summary>
        public void Bind(int index, bool lit, Color litColor)
        {
            Index = index;
            Vector2[] positions = LevelMapLayout.DotPositions(index);
            count = Mathf.Min(positions.Length, dots.Length);
            gameObject.SetActive(true);

            for (int i = 0; i < dots.Length; i++)
            {
                Image dot = dots[i];
                GameTween.Kill(dot);
                GameTween.Kill(dot.rectTransform);
                dot.rectTransform.localScale = Vector3.one;

                bool used = i < count;
                dot.gameObject.SetActive(used);
                if (used)
                {
                    dot.rectTransform.anchoredPosition = positions[i];
                    dot.color = lit ? litColor : UnlitColor;
                }
            }
        }

        public void Release()
        {
            for (int i = 0; i < dots.Length; i++)
            {
                GameTween.Kill(dots[i]);
                GameTween.Kill(dots[i].rectTransform);
            }

            gameObject.SetActive(false);
        }

        /// <summary>Lights the dots one after another (unscaled time): each pops up while it takes the colour.</summary>
        public void PlayReveal(Color litColor, float step)
        {
            for (int i = 0; i < count; i++)
            {
                Image dot = dots[i];
                GameTween.Kill(dot);
                GameTween.Kill(dot.rectTransform);
                dot.color = UnlitColor;
                dot.rectTransform.localScale = Vector3.one;

                float delay = i * step;
                GameTween.Tint(dot, litColor, 0.18f, TweenEase.OutQuad, delay, unscaled: true);
                GameTween.Delay(dot, delay, true, () =>
                {
                    if (dot != null)
                    {
                        GameTween.Punch(dot.rectTransform, 0.9f, 0.3f, unscaled: true);
                    }
                });
            }
        }
    }
}
