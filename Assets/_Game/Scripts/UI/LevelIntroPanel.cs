using TMPro;
using UnityEngine;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The goal card shown before the first move of a level ("Collect 10 crystals in 20 moves"). It
    /// stays for <see cref="LevelRunController.IntroDuration"/> over a light dim, so the authored board
    /// stays readable, and swallows every tap while it is up. The timing belongs to
    /// <see cref="LevelRunController"/>; this panel only follows its intro flag.
    /// </summary>
    public sealed class LevelIntroPanel : LevelOverlayPanel
    {
        public const string ObjectName = "LevelIntroPanel";

        private static readonly Vector2 CardSize = new Vector2(860f, 420f);

        private LevelRunController levelRun;
        private TMP_Text header;
        private TMP_Text title;
        private TMP_Text goal;

        /// <summary>Builds the panel under <paramref name="canvasRect"/>, or rebinds the one already there.</summary>
        public static LevelIntroPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            LevelIntroPanel existing = FindObjectOfType<LevelIntroPanel>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            var panel = CreateOverlay<LevelIntroPanel>(canvasRect, ObjectName, CardSize, 0.5f);
            panel.Build();
            panel.Bind(manager);
            return panel;
        }

        private void Build()
        {
            header = AddText("Header", GameLocalization.LevelGoalHeader, 38f, GameTheme.TextSecondary, -36f, new Vector2(800f, 50f));
            title = AddText("Title", string.Empty, 80f, GameTheme.TextPrimary, -92f, new Vector2(800f, 100f));
            goal = AddText("Goal", string.Empty, 46f, GameTheme.Accent, -210f, new Vector2(760f, 170f));
            goal.enableWordWrapping = true;
            goal.enableAutoSizing = true;
            goal.fontSizeMin = 30f;
            goal.fontSizeMax = 46f;
        }

        private void Bind(GameManager manager)
        {
            if (levelRun != null)
            {
                levelRun.IntroChanged -= HandleIntroChanged;
            }

            BindGame(manager);
            levelRun = manager != null ? manager.LevelRun : null;
            if (levelRun != null)
            {
                levelRun.IntroChanged += HandleIntroChanged;
            }
        }

        protected override void OnDestroy()
        {
            if (levelRun != null)
            {
                levelRun.IntroChanged -= HandleIntroChanged;
                levelRun = null;
            }

            base.OnDestroy();
        }

        protected override void HandleStateChanged(GameState state)
        {
            // The card belongs to a level that is being played; any other screen closes it.
            if (IsOpen && state != GameState.Playing)
            {
                Close();
            }
        }

        protected override void HandleLanguageChanged() => RefreshTexts();

        protected override void ApplyTheme()
        {
            base.ApplyTheme();
            if (header != null)
            {
                header.color = GameTheme.TextSecondary;
                title.color = GameTheme.TextPrimary;
                goal.color = GameTheme.Accent;
            }
        }

        private void HandleIntroChanged(bool intro)
        {
            if (!intro)
            {
                if (IsOpen)
                {
                    Close();
                }

                return;
            }

            RefreshTexts();
            Open();
        }

        private void RefreshTexts()
        {
            if (levelRun == null || levelRun.Level == null || header == null)
            {
                return;
            }

            header.text = GameLocalization.LevelGoalHeader;
            title.text = GameLocalization.LevelTitle(levelRun.LevelNumber).ToUpperInvariant();
            goal.text = GameLocalization.LevelGoalCard(levelRun.Level);
        }
    }
}
