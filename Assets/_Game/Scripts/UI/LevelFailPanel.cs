using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Result screen of a lost level. It says why the attempt ended, shows how far the goal got and
    /// offers three ways on: the rewarded continue (once per attempt: 5 more moves after "out of moves",
    /// two cleared lines after "no room"), "Retry" and "Menu". It waits a moment before it appears so the
    /// player can see the jammed board. Platform code listens to <see cref="ContinueRequested"/> and
    /// shows the ad; nothing here depends on the ad SDK.
    /// </summary>
    public sealed class LevelFailPanel : LevelOverlayPanel
    {
        public const string ObjectName = "LevelFailPanel";

        /// <summary>Pause before the card appears, so the board that ran out stays readable.</summary>
        private const float RevealDelay = 0.7f;

        private const float CardWidth = 860f;
        private const float TopBlockHeight = 440f;
        private const float BottomGap = 30f;

        private static readonly Vector2 ContinueSize = new Vector2(640f, 140f);
        private static readonly Vector2 RetrySize = new Vector2(620f, 120f);
        private static readonly Vector2 MenuSize = new Vector2(440f, 88f);

        private LevelRunController levelRun;

        private TMP_Text title;
        private TMP_Text subtitle;
        private TMP_Text reason;
        private TMP_Text goal;
        private TMP_Text progress;
        private Button continueButton;
        private Button retryButton;
        private Button menuButton;
        private Image continueGlow;

        private Coroutine reveal;
        private bool continueVisible;

        /// <summary>Raised when the player taps the rewarded continue. Platform code shows the ad.</summary>
        public event Action ContinueRequested;

        /// <summary>Builds the panel under <paramref name="canvasRect"/>, or rebinds the one already there.</summary>
        public static LevelFailPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            LevelFailPanel existing = FindObjectOfType<LevelFailPanel>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            var panel = CreateOverlay<LevelFailPanel>(canvasRect, ObjectName, new Vector2(CardWidth, 900f), 0.82f);
            panel.Build();
            panel.Bind(manager);
            return panel;
        }

        private void Build()
        {
            title = AddText("Title", string.Empty, 60f, GameTheme.TextPrimary, -44f, new Vector2(800f, 80f));
            subtitle = AddText("Subtitle", string.Empty, 38f, GameTheme.TextSecondary, -126f, new Vector2(800f, 46f));
            reason = AddText("Reason", string.Empty, 46f, GameTheme.Accent, -196f, new Vector2(800f, 60f));

            goal = AddText("Goal", string.Empty, 34f, GameTheme.TextSecondary, -268f, new Vector2(760f, 100f), FontStyles.Normal);
            goal.enableWordWrapping = true;
            progress = AddText("Progress", string.Empty, 56f, GameTheme.TextPrimary, -360f, new Vector2(800f, 70f));

            menuButton = AddButton("MenuButton", string.Empty, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 32f, 40f, MenuSize);
            retryButton = AddButton("RetryButton", string.Empty, GameTheme.Accent, DarkLabel, 46f, 150f, RetrySize);

            continueGlow = UIFactory.CreateImage("ContinueGlow", Card, GameTheme.WithAlpha(GameTheme.ShopBuy, 0.3f));
            continueGlow.raycastTarget = false;
            UIFactory.Anchor(
                continueGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 300f + ContinueSize.y * 0.5f), ContinueSize + new Vector2(28f, 28f));

            continueButton = AddButton("ContinueButton", string.Empty, GameTheme.ShopBuy, DarkLabel, 40f, 300f, ContinueSize);
            BuildContinueHint();
            continueGlow.transform.SetSiblingIndex(continueButton.transform.GetSiblingIndex());
        }

        private void BuildContinueHint()
        {
            TMP_Text label = continueButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                UIFactory.Anchor(
                    label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                    new Vector2(ContinueSize.x - 20f, 52f));
            }

            TextMeshProUGUI hint = UIFactory.CreateText("Hint", continueButton.transform, string.Empty, 26f, DarkLabel);
            UIFactory.Anchor(
                hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f),
                new Vector2(ContinueSize.x - 20f, 36f));
        }

        private void Bind(GameManager manager)
        {
            Listen(continueButton, HandleContinueClicked);
            Listen(retryButton, HandleRetryClicked);
            Listen(menuButton, HandleMenuClicked);
            BindGame(manager);
            levelRun = manager != null ? manager.LevelRun : null;
        }

        protected override void HandleStateChanged(GameState state)
        {
            if (state == GameState.LevelFailed)
            {
                BeginReveal();
            }
            else if (IsOpen || reveal != null)
            {
                StopReveal();
                Close();
            }
        }

        protected override void HandleLanguageChanged()
        {
            if (IsOpen)
            {
                RefreshTexts();
            }
        }

        protected override void ApplyTheme()
        {
            base.ApplyTheme();
            title.color = GameTheme.TextPrimary;
            subtitle.color = GameTheme.TextSecondary;
            reason.color = GameTheme.Accent;
            goal.color = GameTheme.TextSecondary;
            progress.color = GameTheme.TextPrimary;
        }

        private void BeginReveal()
        {
            StopReveal();
            reveal = StartCoroutine(RevealRoutine());
        }

        private IEnumerator RevealRoutine()
        {
            float waited = 0f;
            while (waited < RevealDelay)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            reveal = null;
            if (Game == null || Game.State != GameState.LevelFailed || levelRun == null)
            {
                yield break;
            }

            RefreshTexts();
            Open();
        }

        private void StopReveal()
        {
            if (reveal != null)
            {
                StopCoroutine(reveal);
                reveal = null;
            }
        }

        // ---------------------------------------------------------------- content

        private void RefreshTexts()
        {
            if (levelRun == null || levelRun.Level == null || title == null)
            {
                return;
            }

            bool outOfMoves = levelRun.FailReason == LevelFailReason.OutOfMoves;
            title.text = GameLocalization.LevelFailedTitle;
            subtitle.text = GameLocalization.LevelTitle(levelRun.LevelNumber);
            reason.text = outOfMoves ? GameLocalization.FailOutOfMoves : GameLocalization.FailNoSpace;
            goal.text = GameLocalization.LevelGoalCard(levelRun.Level);
            progress.text = GameLocalization.LevelGoalCounter(levelRun.GoalType, levelRun.GoalProgress, levelRun.GoalTarget);

            StyleButton(menuButton, GameLocalization.Home, GameTheme.ButtonSecondary, GameTheme.TextPrimary);
            StyleButton(retryButton, GameLocalization.Retry, GameTheme.Accent, DarkLabel);
            StyleButton(
                continueButton,
                outOfMoves ? GameLocalization.MoreMovesAd : GameLocalization.ContinueAd,
                GameTheme.ShopBuy,
                DarkLabel);

            Transform hint = continueButton.transform.Find("Hint");
            TMP_Text hintText = hint != null ? hint.GetComponent<TMP_Text>() : null;
            if (hintText != null)
            {
                hintText.text = outOfMoves ? GameLocalization.MoreMovesHint : GameLocalization.ContinueHint;
                hintText.color = GameTheme.WithAlpha(DarkLabel, 0.75f);
            }

            continueVisible = levelRun.CanContinue;
            continueButton.gameObject.SetActive(continueVisible);
            continueGlow.gameObject.SetActive(continueVisible);

            float stack = continueVisible ? 300f + ContinueSize.y : 150f + RetrySize.y;
            ResizeCard(TopBlockHeight + BottomGap + stack);
        }

        /// <summary>Gentle breathing glow behind the continue button to draw the eye to it.</summary>
        private void Update()
        {
            if (!IsOpen || !continueVisible || continueGlow == null)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * 4f) * 0.5f + 0.5f;
            Color color = GameTheme.ShopBuy;
            color.a = Mathf.Lerp(0.15f, 0.45f, wave);
            continueGlow.color = color;
            continueGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.05f, wave);
        }

        // ---------------------------------------------------------------- clicks

        private void HandleContinueClicked()
        {
            if (levelRun != null && levelRun.CanContinue)
            {
                ContinueRequested?.Invoke();
            }
        }

        private void HandleRetryClicked() => Game?.RestartLevel();

        private void HandleMenuClicked() => Game?.OpenMainMenu();
    }
}
