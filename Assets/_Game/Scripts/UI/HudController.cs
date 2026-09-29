using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Top panel of the screen: score on the left, best in the center. The score counts up
    /// to each new value instead of jumping, and the moment the run passes the old record a
    /// short "New best!" banner pops under the panel. Clear praise lives over the board
    /// (<see cref="BlockPuzzle.Vfx.BoardFeedback"/>). Everything here runs on unscaled time,
    /// so the counter settles even if the game is paused mid-roll.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        /// <summary>Name of the crown icon child in the record section.</summary>
        public const string CrownName = "Crown";

        private static readonly Color RecordColor = GameTheme.FromHex("#FFD54A");

        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private TMP_Text scoreValue;
        [SerializeField] private TMP_Text bestValue;

        [Tooltip("Banner under the panel announcing a new record during the run.")]
        [FormerlySerializedAs("comboLabel")]
        [SerializeField] private TMP_Text recordLabel;

        /// <summary>Time the counter takes to reach a new score.</summary>
        private const float RollDuration = 0.45f;

        private const float PopDuration = 0.18f;
        private const float RecordHold = 1.4f;
        private const float RecordFade = 0.5f;

        private int shownScore;
        private int rollFrom;
        private int rollTo;
        private float rollElapsed;
        private bool rolling;
        private bool recordPending;

        private void Awake()
        {
            if (scoreManager != null)
            {
                Bind(scoreManager);
            }
        }

        public void Bind(ScoreManager manager, TMP_Text score, TMP_Text best, TMP_Text record)
        {
            scoreValue = score;
            bestValue = best;
            recordLabel = record;
            Bind(manager);
        }

        public void Bind(ScoreManager manager)
        {
            Unbind();
            scoreManager = manager;

            if (scoreManager == null)
            {
                return;
            }

            scoreManager.ScoreChanged += HandleScoreChanged;
            scoreManager.BestScoreChanged += HandleBestScoreChanged;
            scoreManager.RecordBroken += HandleRecordBroken;
            GameLocalization.LanguageChanged += HandleLanguageChanged;

            FitHudNumber(scoreValue);
            FitHudNumber(bestValue);
            if (bestValue != null)
            {
                EnsureCrown(bestValue.transform.parent);
            }

            SnapScore(scoreManager.Score);
        }

        private static void FitHudNumber(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Min(text.fontSizeMin, text.fontSizeMax);
            text.fontSizeMax = Mathf.Max(text.fontSizeMax, text.fontSize);
        }

        /// <summary>
        /// Finds or adds the gold crown that marks the record row, and gives it its sprite.
        /// The sprite is generated at runtime, so it is assigned here rather than baked into the scene.
        /// </summary>
        public static RectTransform EnsureCrown(Transform bestSection)
        {
            if (bestSection == null)
            {
                return null;
            }

            var crown = bestSection.Find(CrownName) as RectTransform;
            Image image = crown != null ? crown.GetComponent<Image>() : null;
            if (image == null)
            {
                image = UIFactory.CreateImage(CrownName, bestSection, RecordColor, false);
                image.preserveAspect = true;
                image.raycastTarget = false;
                crown = image.rectTransform;
            }

            if (Application.isPlaying)
            {
                image.sprite = HudIcons.Crown;
            }

            image.color = RecordColor;
            return crown;
        }

        private void OnDestroy() => Unbind();

        private void OnDisable()
        {
            // A roll left half-way would freeze on a stale number.
            if (rolling)
            {
                SetShownScore(rollTo, force: true);
                rolling = false;
            }
        }

        private void Unbind()
        {
            GameLocalization.LanguageChanged -= HandleLanguageChanged;
            if (scoreManager == null)
            {
                return;
            }

            scoreManager.ScoreChanged -= HandleScoreChanged;
            scoreManager.BestScoreChanged -= HandleBestScoreChanged;
            scoreManager.RecordBroken -= HandleRecordBroken;
            scoreManager = null;
        }

        private void Update()
        {
            if (!rolling)
            {
                return;
            }

            rollElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(rollElapsed / RollDuration);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);

            SetShownScore(Mathf.RoundToInt(Mathf.Lerp(rollFrom, rollTo, eased)));

            if (t >= 1f)
            {
                rolling = false;
            }
        }

        private void HandleLanguageChanged()
        {
            SetShownScore(shownScore, force: true);

            if (recordLabel != null && recordLabel.gameObject.activeSelf)
            {
                recordLabel.text = GameLocalization.NewBest;
            }
        }

        /// <summary>
        /// Gains are counted up to; anything else (a new run, a restored board) is shown at once.
        /// </summary>
        private void HandleScoreChanged(int value)
        {
            if (value < shownScore || !Application.isPlaying || !isActiveAndEnabled)
            {
                SnapScore(value);
                return;
            }

            rollFrom = shownScore;
            rollTo = value;
            rollElapsed = 0f;
            rolling = true;

            if (scoreValue != null)
            {
                GameTween.Punch(scoreValue.rectTransform, 0.18f, PopDuration, unscaled: true);
            }
        }

        private void SnapScore(int value)
        {
            rolling = false;
            rollTo = value;

            if (value < shownScore)
            {
                HideRecord();
            }

            SetShownScore(value, force: true);
        }

        private void SetShownScore(int value, bool force = false)
        {
            if (value == shownScore && !force)
            {
                return;
            }

            shownScore = value;
            if (scoreValue != null)
            {
                // Bare number: the size says "score", the crown next to the record says "best".
                scoreValue.text = value.ToString();
            }

            RefreshBest();
            TryPlayRecord();
        }

        private void HandleBestScoreChanged(int value) => RefreshBest();

        /// <summary>
        /// While the run holds the record the best counter climbs together with the score,
        /// instead of jumping ahead of it on every gain.
        /// </summary>
        private void RefreshBest()
        {
            if (bestValue == null || scoreManager == null)
            {
                return;
            }

            int best = scoreManager.IsNewRecord
                ? Mathf.Max(scoreManager.RunStartRecord, shownScore)
                : scoreManager.BestScore;

            bestValue.text = best.ToString();
        }

        private void HandleRecordBroken(int score)
        {
            recordPending = true;
            TryPlayRecord();
        }

        /// <summary>Holds the banner back until the counter itself passes the old record.</summary>
        private void TryPlayRecord()
        {
            if (!recordPending || scoreManager == null || shownScore <= scoreManager.RunStartRecord)
            {
                return;
            }

            recordPending = false;
            PlayRecord();
        }

        private void PlayRecord()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                return;
            }

            if (bestValue != null)
            {
                GameTween.Punch(bestValue.rectTransform, 0.3f, 0.35f, unscaled: true);
            }

            TMP_Text label = EnsureRecordLabel();
            RectTransform labelRect = label.rectTransform;

            GameTween.Kill(label);
            GameTween.Kill(labelRect);

            label.text = GameLocalization.NewBest;
            label.color = RecordColor;
            label.gameObject.SetActive(true);
            labelRect.localScale = Vector3.one * 0.5f;

            GameTween.Scale(labelRect, Vector3.one, 0.32f, TweenEase.OutBack, unscaled: true);
            GameTween.Fade(label, 0f, RecordFade, TweenEase.InQuad, RecordHold, unscaled: true);
        }

        private void HideRecord()
        {
            recordPending = false;
            if (recordLabel == null)
            {
                return;
            }

            GameTween.Kill(recordLabel);
            GameTween.Kill(recordLabel.rectTransform);
            recordLabel.rectTransform.localScale = Vector3.one;
            recordLabel.color = GameTheme.WithAlpha(RecordColor, 0f);
        }

        /// <summary>Uses the label the scene was built with, or hangs a new one under the panel.</summary>
        private TMP_Text EnsureRecordLabel()
        {
            if (recordLabel != null)
            {
                return recordLabel;
            }

            TextMeshProUGUI label = UIFactory.CreateText(
                "RecordLabel", transform, string.Empty, 36f, RecordColor, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(
                label.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -8f),
                new Vector2(600f, 48f));

            recordLabel = label;
            return recordLabel;
        }
    }
}
