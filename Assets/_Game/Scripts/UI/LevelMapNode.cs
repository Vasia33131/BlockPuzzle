using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Levels;

namespace BlockPuzzle.UI
{
    /// <summary>How a node of the level map is drawn.</summary>
    public enum LevelMapNodeState
    {
        /// <summary>Closed: grey with a padlock.</summary>
        Locked,

        /// <summary>Open but never passed, and not the one the player is up to.</summary>
        Open,

        /// <summary>Passed: coloured, with the best stars underneath.</summary>
        Passed,

        /// <summary>The level the player is up to: bigger, pulsing.</summary>
        Current,

        /// <summary>The final trophy, reached.</summary>
        Trophy,

        /// <summary>The final trophy while the path to it is still being drawn.</summary>
        TrophyLocked
    }

    /// <summary>
    /// One pooled node of the level map: a round face with a rim, the level number (or a padlock, or the
    /// trophy), and the three stars underneath. <see cref="LevelMapPanel"/> binds it to a level while that
    /// level is on screen and gives it back to the pool afterwards, so 100 levels cost a dozen of these.
    /// </summary>
    public sealed class LevelMapNode : MonoBehaviour
    {
        public const float NormalDiameter = 140f;
        public const float CurrentDiameter = 176f;
        public const float TrophyDiameter = 210f;

        private const float HitSize = 200f;
        private const float RimWidth = 9f;
        private const float SpecialRimWidth = 14f;
        private const float StarSize = 44f;
        private const float StarSpacing = 50f;
        private const float PulseSpeed = 3.4f;

        private static readonly Color SpecialGold = GameTheme.FromHex("#FFC83D");
        private static readonly Color SpecialGoldDim = GameTheme.FromHex("#8C7A3E");
        private static readonly Color LockedFace = GameTheme.FromHex("#525888");
        private static readonly Color LockedRim = GameTheme.FromHex("#2C3157");
        private static readonly Color LockedIcon = GameTheme.FromHex("#C8CCE8");
        private static readonly Color TrophyFace = GameTheme.FromHex("#3B2A86");
        private static readonly Color EmptyStar = new Color(1f, 1f, 1f, 0.2f);

        private Action<int> clicked;

        private RectTransform rect;
        private RectTransform visual;
        private Image pulseRing;
        private Image burst;
        private Image shadow;
        private Image rim;
        private Image face;
        private Image gloss;
        private Image lockIcon;
        private Image trophyIcon;
        private TMP_Text number;
        private RectTransform starsRow;
        private readonly Image[] stars = new Image[LevelProgress.MaxStars];

        private float pulseSuspendedUntil;
        private Color chapterColor;
        private int earnedStars;

        /// <summary>1-based level, or <see cref="LevelMapLayout.TrophyIndex"/>.</summary>
        public int Index { get; private set; }

        public LevelMapNodeState State { get; private set; }

        /// <summary>Scaled part of the node; punch and pulse animate this, never the hit area.</summary>
        public RectTransform Visual => visual;

        public RectTransform Rect => rect;

        /// <summary>Radius of the drawn disc for a state, so the avatar can stand on top of it.</summary>
        public static float RadiusOf(LevelMapNodeState state)
        {
            switch (state)
            {
                case LevelMapNodeState.Current:
                    return CurrentDiameter * 0.5f;
                case LevelMapNodeState.Trophy:
                case LevelMapNodeState.TrophyLocked:
                    return TrophyDiameter * 0.5f;
                default:
                    return NormalDiameter * 0.5f;
            }
        }

        public static LevelMapNode Create(Transform parent, Action<int> onClicked)
        {
            RectTransform root = UIFactory.CreateRect("Node", parent);
            root.sizeDelta = new Vector2(HitSize, HitSize);
            var node = root.gameObject.AddComponent<LevelMapNode>();
            node.clicked = onClicked;
            node.Build(root);
            return node;
        }

        private void Build(RectTransform root)
        {
            rect = root;

            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.canvasRenderer.cullTransparentMesh = false;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(HandleClicked);
            ButtonPressAnimator.Attach(button);

            visual = UIFactory.CreateRect("Visual", root);
            UIFactory.Stretch(visual);

            pulseRing = CreateDisc("PulseRing", visual);
            burst = CreateDisc("Burst", visual);
            burst.gameObject.SetActive(false);
            shadow = CreateDisc("Shadow", visual);
            rim = CreateDisc("Rim", visual);
            face = CreateDisc("Face", visual);

            gloss = CreateDisc("Gloss", face.rectTransform);
            gloss.color = new Color(1f, 1f, 1f, 0.22f);

            number = UIFactory.CreateText(
                "Number", visual, string.Empty, 58f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            MenuArt.ApplyOutlinedMaterial(number);
            UIFactory.Anchor(
                number.rectTransform, Half, Half, Vector2.zero, new Vector2(NormalDiameter, NormalDiameter));

            lockIcon = CreateIcon("Lock", visual, LevelMapArt.LockSprite);
            trophyIcon = CreateIcon("Trophy", visual, LevelMapArt.TrophySprite);

            starsRow = UIFactory.CreateRect("Stars", root);
            UIFactory.Anchor(
                starsRow, Half, Half, new Vector2(0f, -(NormalDiameter * 0.5f + 8f + StarSize * 0.5f)), new Vector2(StarSpacing * 3f, StarSize));
            for (int i = 0; i < stars.Length; i++)
            {
                Image star = UIFactory.CreateImage($"Star_{i}", starsRow, Color.white, false);
                star.sprite = LevelArt.StarSprite;
                star.preserveAspect = true;
                star.raycastTarget = false;
                UIFactory.Anchor(
                    star.rectTransform, Half, Half, new Vector2((i - 1) * StarSpacing, 0f), new Vector2(StarSize, StarSize));
                stars[i] = star;
            }
        }

        private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

        private static Image CreateDisc(string name, Transform parent)
        {
            Image disc = UIFactory.CreateImage(name, parent, Color.white, false);
            disc.sprite = ProfileUi.CircleSprite;
            disc.raycastTarget = false;
            return disc;
        }

        private static Image CreateIcon(string name, Transform parent, Sprite sprite)
        {
            Image icon = UIFactory.CreateImage(name, parent, Color.white, false);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private static void Place(Graphic graphic, Vector2 position, float width, float height)
        {
            UIFactory.Anchor(graphic.rectTransform, Half, Half, position, new Vector2(width, height));
        }

        /// <summary>Puts the node on the map and dresses it for <paramref name="state"/>.</summary>
        public void Bind(int index, LevelMapNodeState state, int bestStars, bool special, Color chapter)
        {
            Index = index;
            State = state;
            earnedStars = bestStars;
            chapterColor = chapter;

            GameTween.Kill(visual);
            GameTween.Kill(pulseRing);
            GameTween.Kill(pulseRing.rectTransform);
            GameTween.Kill(burst);
            GameTween.Kill(burst.rectTransform);
            visual.localScale = Vector3.one;
            burst.gameObject.SetActive(false);
            pulseSuspendedUntil = 0f;

            rect.anchoredPosition = LevelMapLayout.Point(index);
            gameObject.SetActive(true);

            bool locked = state == LevelMapNodeState.Locked;
            bool current = state == LevelMapNodeState.Current;
            bool trophy = state == LevelMapNodeState.Trophy || state == LevelMapNodeState.TrophyLocked;
            bool showStars = state == LevelMapNodeState.Passed || state == LevelMapNodeState.Open;

            float diameter = RadiusOf(state) * 2f;
            float rimWidth = special ? SpecialRimWidth : RimWidth;

            Place(shadow, new Vector2(0f, -10f), diameter + 8f, diameter + 8f);
            Place(rim, Vector2.zero, diameter, diameter);
            Place(face, Vector2.zero, diameter - rimWidth * 2f, diameter - rimWidth * 2f);
            UIFactory.Anchor(
                gloss.rectTransform, Half, Half, new Vector2(0f, face.rectTransform.sizeDelta.y * 0.24f),
                new Vector2(face.rectTransform.sizeDelta.x * 0.62f, face.rectTransform.sizeDelta.y * 0.3f));
            Place(pulseRing, Vector2.zero, diameter + 44f, diameter + 44f);
            Place(burst, Vector2.zero, diameter, diameter);

            shadow.color = new Color(0f, 0f, 0.05f, 0.35f);
            pulseRing.gameObject.SetActive(current);
            pulseRing.color = GameTheme.WithAlpha(chapter, 0.4f);

            Color faceColor;
            Color rimColor;
            if (locked)
            {
                faceColor = LockedFace;
                rimColor = special ? SpecialGoldDim : LockedRim;
            }
            else if (trophy)
            {
                faceColor = state == LevelMapNodeState.Trophy ? TrophyFace : LockedFace;
                rimColor = state == LevelMapNodeState.Trophy ? SpecialGold : SpecialGoldDim;
            }
            else
            {
                faceColor = current ? GameTheme.Lighten(chapter, 0.12f) : chapter;
                rimColor = special ? SpecialGold : (current ? Color.white : GameTheme.Darken(chapter, 0.35f));
            }

            face.color = faceColor;
            rim.color = rimColor;
            gloss.gameObject.SetActive(!locked);

            number.gameObject.SetActive(!locked && !trophy);
            number.text = index.ToString();
            number.fontSize = current ? 74f : 58f;

            lockIcon.gameObject.SetActive(locked);
            UIFactory.Anchor(lockIcon.rectTransform, Half, Half, Vector2.zero, Vector2.one * diameter * 0.52f);
            lockIcon.color = LockedIcon;

            trophyIcon.gameObject.SetActive(trophy);
            UIFactory.Anchor(trophyIcon.rectTransform, Half, Half, Vector2.zero, Vector2.one * diameter * 0.62f);
            trophyIcon.color = state == LevelMapNodeState.Trophy ? SpecialGold : LockedIcon;

            starsRow.gameObject.SetActive(showStars);
            for (int i = 0; i < stars.Length; i++)
            {
                GameTween.Kill(stars[i].rectTransform);
                stars[i].rectTransform.localScale = Vector3.one;
                stars[i].color = i < earnedStars ? LevelArt.StarGold : EmptyStar;
            }
        }

        /// <summary>Hands the node back to the pool.</summary>
        public void Release()
        {
            GameTween.Kill(visual);
            GameTween.Kill(pulseRing);
            GameTween.Kill(burst);
            GameTween.Kill(burst.rectTransform);
            for (int i = 0; i < stars.Length; i++)
            {
                GameTween.Kill(stars[i].rectTransform);
            }

            gameObject.SetActive(false);
        }

        /// <summary>Breathing of the current node: the disc swells a little and its ring fades in and out.</summary>
        public void Tick(float time)
        {
            if (State != LevelMapNodeState.Current || time < pulseSuspendedUntil)
            {
                return;
            }

            float wave = Mathf.Sin(time * PulseSpeed) * 0.5f + 0.5f;
            visual.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, wave);
            pulseRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.12f, wave);
            pulseRing.color = GameTheme.WithAlpha(chapterColor, Mathf.Lerp(0.45f, 0.1f, wave));
        }

        /// <summary>The stars that were earned pop in one after another.</summary>
        public void PlayStarsPop()
        {
            if (!starsRow.gameObject.activeSelf)
            {
                return;
            }

            for (int i = 0; i < earnedStars && i < stars.Length; i++)
            {
                RectTransform star = stars[i].rectTransform;
                GameTween.Kill(star);
                star.localScale = Vector3.zero;
                GameTween.Scale(star, Vector3.one, 0.35f, TweenEase.OutBack, i * 0.18f, unscaled: true);
            }
        }

        /// <summary>The node has just opened: the disc pops out of a smaller size and a ring spreads from it.</summary>
        public void PlayOpenEffect()
        {
            pulseSuspendedUntil = Time.unscaledTime + 0.6f;

            GameTween.Kill(visual);
            visual.localScale = Vector3.one * 0.5f;
            GameTween.Scale(visual, Vector3.one, 0.5f, TweenEase.OutBack, unscaled: true);

            GameTween.Kill(burst);
            GameTween.Kill(burst.rectTransform);
            burst.gameObject.SetActive(true);
            burst.rectTransform.localScale = Vector3.one;
            burst.color = new Color(1f, 1f, 1f, 0.75f);
            GameTween.Scale(burst.rectTransform, Vector3.one * 2.4f, 0.7f, TweenEase.OutQuad, unscaled: true);
            GameTween.Fade(burst, 0f, 0.7f, TweenEase.OutQuad, unscaled: true, onComplete: () =>
            {
                if (burst != null)
                {
                    burst.gameObject.SetActive(false);
                }
            });
        }

        /// <summary>Short pop, for a tap on a node that cannot be opened.</summary>
        public void Punch()
        {
            pulseSuspendedUntil = Time.unscaledTime + 0.35f;
            GameTween.Punch(visual, 0.18f, 0.3f, unscaled: true);
        }

        private void HandleClicked() => clicked?.Invoke(Index);
    }
}
