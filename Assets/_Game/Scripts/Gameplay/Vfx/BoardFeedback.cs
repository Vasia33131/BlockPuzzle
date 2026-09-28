using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Grid;

namespace BlockPuzzle.Vfx
{
    /// <summary>
    /// Everything that celebrates a clear over the board: a flash along each completed line,
    /// a shake for doubles and up, the points rising from where the lines were, a praise
    /// banner ("Great!", "Combo x3") and a firework when the whole board is emptied.
    /// Like <see cref="SparkBurst"/> it is a uGUI layer on top of the cells, and it runs on
    /// scaled time together with the cell animations, so a pause freezes the whole board.
    /// The board calls <see cref="PlayLineClear"/>; the scores are forwarded by the game
    /// manager, which is the only object that knows both sides.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BoardFeedback : MonoBehaviour
    {
        private static readonly Color GoodColor = GameTheme.FromHex("#8BE9FD");
        private static readonly Color GreatColor = GameTheme.FromHex("#7CF29C");
        private static readonly Color SuperColor = GameTheme.FromHex("#FFD54A");
        private static readonly Color IncredibleColor = GameTheme.FromHex("#FF7AB6");
        private static readonly Color OutlineColor = new Color(0.08f, 0.06f, 0.18f, 0.9f);

        [Header("Line flash")]
        [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.85f;
        [SerializeField] private float flashDuration = 0.32f;

        [Header("Shake")]
        [Tooltip("Lines cleared at once that start shaking the board.")]
        [SerializeField, Min(1)] private int shakeFromLines = 2;
        [SerializeField] private float shakeStrength = 7f;
        [SerializeField] private float shakeStrengthPerLine = 3f;
        [SerializeField] private float shakeDuration = 0.22f;

        [Header("Score popup")]
        [SerializeField] private float popupRise = 70f;
        [SerializeField] private float popupDuration = 0.8f;

        [Header("Banner")]
        [SerializeField] private float bannerHold = 0.6f;
        [SerializeField] private float bannerFade = 0.3f;

        [Header("Board clear")]
        [SerializeField, Min(0)] private int boardClearSparks = 90;
        [SerializeField] private float boardClearShake = 16f;

        private readonly Stack<Image> flashPool = new Stack<Image>();
        private readonly List<Image> liveFlashes = new List<Image>();
        private readonly Stack<TextMeshProUGUI> popupPool = new Stack<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> livePopups = new List<TextMeshProUGUI>();

        private GridManager grid;
        private RectTransform rect;
        private Vector2 lastClearCenter;

        private RectTransform banner;
        private CanvasGroup bannerGroup;
        private TextMeshProUGUI bannerTitle;
        private TextMeshProUGUI bannerSubtitle;
        private Image boardFlash;

        private RectTransform shakeTarget;
        private Vector3 shakeWritten;
        private Vector3 shakeOffset;
        private float shakeLeft;
        private float shakeTotal;
        private float shakeAmplitude;

        /// <summary>Adds the effect layer on top of the board it draws over.</summary>
        public static BoardFeedback Create(RectTransform boardRoot)
        {
            RectTransform root = UIFactory.CreateRect("FeedbackLayer", boardRoot);
            UIFactory.Stretch(root);

            // Pure decoration: it must never swallow a drag.
            root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            return root.gameObject.AddComponent<BoardFeedback>();
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            grid = GetComponentInParent<GridManager>();
        }

        private void OnDisable()
        {
            StopShake();
        }

        /// <summary>
        /// Flashes every completed line, shakes the board when several went at once and
        /// remembers where the clear happened, so the points can rise from there.
        /// </summary>
        public void PlayLineClear(LineClearResult lines)
        {
            if (!Application.isPlaying || lines == null || !lines.HasLines || grid == null)
            {
                return;
            }

            lastClearCenter = CenterOf(lines.Cells);

            for (int i = 0; i < lines.Rows.Count; i++)
            {
                FlashLine(lines.Rows[i], horizontal: true);
            }

            for (int i = 0; i < lines.Columns.Count; i++)
            {
                FlashLine(lines.Columns[i], horizontal: false);
            }

            if (lines.LineCount >= shakeFromLines)
            {
                Shake(shakeStrength + (lines.LineCount - shakeFromLines) * shakeStrengthPerLine, shakeDuration);
            }
        }

        /// <summary>
        /// Points of a clear rising from the cleared cells, plus a banner that grows with the
        /// number of lines and names the combo once a streak is running.
        /// </summary>
        public void ShowClearScore(int lines, int points, int combo)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ShowPopup(lastClearCenter, GameLocalization.Points(points), combo > 1 ? GameTheme.Accent : Color.white);

            string praise = GameLocalization.Praise(lines);
            string comboText = combo > 1 ? GameLocalization.ComboBanner(combo) : null;

            if (praise != null)
            {
                ShowBanner(praise, PraiseColor(lines), comboText, 1f + 0.08f * Mathf.Min(lines - 2, 3));
            }
            else if (comboText != null)
            {
                ShowBanner(comboText, GameTheme.Accent, null, 0.9f);
            }
        }

        /// <summary>The whole board went empty: white flash, firework and the bonus banner.</summary>
        public void ShowBoardCleared(int bonus)
        {
            if (!Application.isPlaying || grid == null)
            {
                return;
            }

            Image flash = EnsureBoardFlash();
            GameTween.Kill(flash);
            flash.color = new Color(1f, 1f, 1f, 0.75f);
            flash.gameObject.SetActive(true);
            flash.transform.SetAsFirstSibling();
            GameTween.Fade(flash, 0f, 0.6f, TweenEase.OutQuad, onComplete: () => flash.gameObject.SetActive(false));

            Vector2 center = new Vector2(rect.rect.width * 0.5f, -rect.rect.height * 0.5f);
            grid.Sparks?.Burst(center, GameTheme.PastelPalette, boardClearSparks, 3.2f);

            Shake(boardClearShake, 0.4f);
            ShowBanner(GameLocalization.BoardClearedTitle, SuperColor, GameLocalization.Points(bonus), 1.1f);
        }

        /// <summary>Free-standing praise over the board, e.g. the end of the tutorial.</summary>
        public void ShowMessage(string title)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(title))
            {
                return;
            }

            ShowBanner(title, GreatColor, null, 1.05f);
        }

        /// <summary>Cancels everything in flight, used when a run restarts.</summary>
        public void Clear()
        {
            StopShake();

            for (int i = liveFlashes.Count - 1; i >= 0; i--)
            {
                Image flash = liveFlashes[i];
                if (flash != null)
                {
                    GameTween.Kill(flash);
                    GameTween.Kill(flash.rectTransform);
                    RecycleFlash(flash);
                }
            }

            liveFlashes.Clear();

            for (int i = livePopups.Count - 1; i >= 0; i--)
            {
                TextMeshProUGUI popup = livePopups[i];
                if (popup != null)
                {
                    GameTween.Kill(popup);
                    GameTween.Kill(popup.rectTransform);
                    RecyclePopup(popup);
                }
            }

            livePopups.Clear();

            if (banner != null)
            {
                GameTween.Kill(banner);
                GameTween.Kill(bannerGroup);
                banner.gameObject.SetActive(false);
            }

            if (boardFlash != null)
            {
                GameTween.Kill(boardFlash);
                boardFlash.gameObject.SetActive(false);
            }
        }

        private static Color PraiseColor(int lines)
        {
            switch (lines)
            {
                case 2: return GoodColor;
                case 3: return GreatColor;
                case 4: return SuperColor;
                default: return IncredibleColor;
            }
        }

        private void FlashLine(int index, bool horizontal)
        {
            float cell = grid.CellSize;
            float pitch = grid.Pitch;
            float length = grid.Size * pitch - (pitch - cell);
            float across = index * pitch + cell * 0.5f;

            Image flash = RentFlash();
            RectTransform flashRect = flash.rectTransform;
            flashRect.sizeDelta = horizontal ? new Vector2(length, cell) : new Vector2(cell, length);
            flashRect.anchoredPosition = horizontal
                ? new Vector2(length * 0.5f, -across)
                : new Vector2(across, -length * 0.5f);

            // The bar starts thin and swells past the line while it fades, like a light sweep.
            flashRect.localScale = horizontal ? new Vector3(1f, 0.7f, 1f) : new Vector3(0.7f, 1f, 1f);
            flash.color = new Color(1f, 1f, 1f, flashAlpha);
            flash.gameObject.SetActive(true);
            liveFlashes.Add(flash);

            Vector3 swollen = horizontal ? new Vector3(1.04f, 1.45f, 1f) : new Vector3(1.45f, 1.04f, 1f);
            GameTween.Scale(flashRect, swollen, flashDuration, TweenEase.OutCubic);
            GameTween.Fade(flash, 0f, flashDuration, TweenEase.InQuad, onComplete: () =>
            {
                liveFlashes.Remove(flash);
                RecycleFlash(flash);
            });
        }

        private void ShowPopup(Vector2 position, string text, Color color)
        {
            TextMeshProUGUI popup = RentPopup();
            RectTransform popupRect = popup.rectTransform;

            popup.text = text;
            popup.fontSize = Mathf.Max(28f, BoardWidth * 0.085f);
            popup.color = color;
            popupRect.anchoredPosition = position;
            popupRect.localScale = Vector3.one * 0.6f;
            popup.gameObject.SetActive(true);
            popup.transform.SetAsLastSibling();
            livePopups.Add(popup);

            GameTween.Scale(popupRect, Vector3.one, 0.22f, TweenEase.OutBack);
            GameTween.MoveAnchored(popupRect, position + Vector2.up * popupRise, popupDuration, TweenEase.OutCubic);
            GameTween.Fade(popup, 0f, popupDuration * 0.45f, TweenEase.InQuad, popupDuration * 0.55f, onComplete: () =>
            {
                livePopups.Remove(popup);
                RecyclePopup(popup);
            });
        }

        private void ShowBanner(string title, Color titleColor, string subtitle, float scale)
        {
            EnsureBanner();

            GameTween.Kill(banner);
            GameTween.Kill(bannerGroup);

            float width = BoardWidth;
            bannerTitle.fontSizeMax = width * 0.13f;
            bannerTitle.fontSizeMin = bannerTitle.fontSizeMax * 0.5f;
            bannerTitle.text = title;
            bannerTitle.color = titleColor;

            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
            bannerSubtitle.gameObject.SetActive(hasSubtitle);
            bannerSubtitle.fontSizeMax = width * 0.075f;
            bannerSubtitle.fontSizeMin = bannerSubtitle.fontSizeMax * 0.5f;
            bannerSubtitle.text = hasSubtitle ? subtitle : string.Empty;
            bannerSubtitle.color = GameTheme.Accent;

            banner.sizeDelta = new Vector2(width, width * 0.4f);
            banner.anchoredPosition = new Vector2(0f, width * 0.04f);
            banner.localScale = Vector3.one * (scale * 0.4f);
            bannerGroup.alpha = 1f;
            banner.gameObject.SetActive(true);
            banner.SetAsLastSibling();

            GameTween.Scale(banner, Vector3.one * scale, 0.28f, TweenEase.OutBack);
            GameTween.MoveAnchored(banner, new Vector2(0f, width * 0.1f), bannerHold + bannerFade, TweenEase.OutQuad);
            GameTween.Fade(bannerGroup, 0f, bannerFade, TweenEase.InQuad, 0.28f + bannerHold,
                onComplete: () => banner.gameObject.SetActive(false));
        }

        /// <summary>
        /// Shakes the frame the board sits in. The offset is added on top of whatever the
        /// layout put there, and if the layout moves the board mid-shake (a rotation) its new
        /// position is taken as the base instead of being overwritten.
        /// </summary>
        private void Shake(float strength, float duration)
        {
            RectTransform target = ShakeTarget;
            if (target == null || strength <= 0f || duration <= 0f)
            {
                return;
            }

            if (shakeLeft > 0f && strength < shakeAmplitude * (shakeLeft / shakeTotal))
            {
                return;
            }

            shakeAmplitude = strength;
            shakeTotal = duration;
            shakeLeft = duration;
        }

        private void LateUpdate()
        {
            if (shakeLeft <= 0f || shakeTarget == null)
            {
                return;
            }

            shakeLeft -= Time.deltaTime;
            Vector3 basePosition = CurrentShakeBase();

            if (shakeLeft <= 0f)
            {
                shakeTarget.localPosition = basePosition;
                shakeOffset = Vector3.zero;
                shakeLeft = 0f;
                return;
            }

            float falloff = shakeLeft / shakeTotal;
            Vector2 jitter = Random.insideUnitCircle * (shakeAmplitude * falloff * falloff);
            shakeOffset = new Vector3(jitter.x, jitter.y, 0f);
            shakeWritten = basePosition + shakeOffset;
            shakeTarget.localPosition = shakeWritten;
        }

        private void StopShake()
        {
            if (shakeTarget != null && shakeOffset != Vector3.zero)
            {
                shakeTarget.localPosition = CurrentShakeBase();
            }

            shakeOffset = Vector3.zero;
            shakeLeft = 0f;
        }

        private Vector3 CurrentShakeBase()
        {
            Vector3 current = shakeTarget.localPosition;
            return current == shakeWritten ? current - shakeOffset : current;
        }

        /// <summary>The panel framing the board when there is one, so the frame shakes along with the cells.</summary>
        private RectTransform ShakeTarget
        {
            get
            {
                if (shakeTarget == null && grid != null)
                {
                    RectTransform board = grid.BoardRoot;
                    shakeTarget = board != null && board.parent is RectTransform frame && frame.name == "BoardPanel"
                        ? frame
                        : board;
                    shakeOffset = Vector3.zero;
                    shakeWritten = shakeTarget != null ? shakeTarget.localPosition : Vector3.zero;
                }

                return shakeTarget;
            }
        }

        private float BoardWidth => rect != null && rect.rect.width > 1f ? rect.rect.width : GameTheme.BoardSize;

        private Vector2 CenterOf(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return new Vector2(BoardWidth * 0.5f, -BoardWidth * 0.5f);
            }

            Vector2 sum = Vector2.zero;
            for (int i = 0; i < cells.Count; i++)
            {
                sum += new Vector2(cells[i].x, cells[i].y);
            }

            Vector2 average = sum / cells.Count;
            float half = grid.CellSize * 0.5f;
            return new Vector2(average.x * grid.Pitch + half, -(average.y * grid.Pitch + half));
        }

        private Image RentFlash()
        {
            while (flashPool.Count > 0)
            {
                Image pooled = flashPool.Pop();
                if (pooled != null)
                {
                    GameTween.Kill(pooled);
                    GameTween.Kill(pooled.rectTransform);
                    return pooled;
                }
            }

            Image flash = UIFactory.CreateImage($"LineFlash_{liveFlashes.Count}", rect, Color.white);
            flash.raycastTarget = false;
            TopLeft(flash.rectTransform);
            return flash;
        }

        private void RecycleFlash(Image flash)
        {
            flash.gameObject.SetActive(false);
            flashPool.Push(flash);
        }

        private TextMeshProUGUI RentPopup()
        {
            while (popupPool.Count > 0)
            {
                TextMeshProUGUI pooled = popupPool.Pop();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            TextMeshProUGUI popup = CreateLabel($"ScorePopup_{livePopups.Count}", rect, 40f);
            TopLeft(popup.rectTransform);
            popup.rectTransform.sizeDelta = new Vector2(320f, 80f);
            return popup;
        }

        private void RecyclePopup(TextMeshProUGUI popup)
        {
            popup.gameObject.SetActive(false);
            popupPool.Push(popup);
        }

        private void EnsureBanner()
        {
            if (banner != null)
            {
                return;
            }

            banner = UIFactory.CreateRect("Banner", rect);
            UIFactory.Anchor(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 160f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;

            bannerTitle = CreateLabel("Title", banner, 56f);
            bannerTitle.enableAutoSizing = true;
            bannerTitle.gameObject.SetActive(true);
            UIFactory.Anchor(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -4f), Vector2.zero);
            bannerTitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bannerTitle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            bannerTitle.rectTransform.sizeDelta = new Vector2(0f, 90f);

            bannerSubtitle = CreateLabel("Subtitle", banner, 32f);
            bannerSubtitle.enableAutoSizing = true;
            UIFactory.Anchor(bannerSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), Vector2.zero);
            bannerSubtitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bannerSubtitle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            bannerSubtitle.rectTransform.sizeDelta = new Vector2(0f, 56f);

            banner.gameObject.SetActive(false);
        }

        private Image EnsureBoardFlash()
        {
            if (boardFlash == null)
            {
                boardFlash = UIFactory.CreateImage("BoardFlash", rect, Color.clear);
                boardFlash.raycastTarget = false;
                UIFactory.Stretch(boardFlash.rectTransform);
            }

            return boardFlash;
        }

        private static TextMeshProUGUI CreateLabel(string name, Transform parent, float fontSize)
        {
            TextMeshProUGUI label = UIFactory.CreateText(
                name, parent, string.Empty, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = true;

            // A dark rim keeps the text readable over any block colour.
            label.outlineWidth = 0.22f;
            label.outlineColor = OutlineColor;
            if (label.fontMaterial != null)
            {
                label.fontMaterial.EnableKeyword("OUTLINE_ON");
            }

            label.gameObject.SetActive(false);
            return label;
        }

        private static void TopLeft(RectTransform target)
        {
            target.anchorMin = new Vector2(0f, 1f);
            target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
