using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// "Leave the level?" confirmation. An unfinished level is never saved, so leaving it for the menu
    /// burns the attempt; the pause screen asks first once the player has made at least one move.
    /// Opens over the pause screen and closes when the game leaves the paused state.
    /// </summary>
    public sealed class LevelExitConfirmPanel : LevelOverlayPanel
    {
        public const string ObjectName = "LevelExitConfirmPanel";

        private static readonly Vector2 CardSize = new Vector2(780f, 560f);
        private static readonly Vector2 StaySize = new Vector2(620f, 120f);
        private static readonly Vector2 LeaveSize = new Vector2(620f, 100f);

        private TMP_Text title;
        private TMP_Text body;
        private Button stayButton;
        private Button leaveButton;
        private Action onLeave;

        /// <summary>Builds the panel under <paramref name="canvasRect"/>, or rebinds the one already there.</summary>
        public static LevelExitConfirmPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            LevelExitConfirmPanel existing = FindObjectOfType<LevelExitConfirmPanel>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            var panel = CreateOverlay<LevelExitConfirmPanel>(canvasRect, ObjectName, CardSize, 0.6f);
            panel.Build();
            panel.Bind(manager);
            return panel;
        }

        /// <summary>Opens the question; <paramref name="confirmed"/> runs only when the player chooses to leave.</summary>
        public void Show(Action confirmed)
        {
            onLeave = confirmed;
            RefreshTexts();
            Open();
        }

        private void Build()
        {
            title = AddText("Title", string.Empty, 58f, GameTheme.TextPrimary, -44f, new Vector2(700f, 80f));
            body = AddText("Body", string.Empty, 34f, GameTheme.TextSecondary, -150f, new Vector2(680f, 130f), FontStyles.Normal);
            body.enableWordWrapping = true;

            leaveButton = AddButton("LeaveButton", string.Empty, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 36f, 40f, LeaveSize);
            stayButton = AddButton("StayButton", string.Empty, GameTheme.Accent, DarkLabel, 44f, 160f, StaySize);
        }

        private void Bind(GameManager manager)
        {
            Listen(stayButton, HandleStayClicked);
            Listen(leaveButton, HandleLeaveClicked);
            BindGame(manager);
        }

        protected override void HandleStateChanged(GameState state)
        {
            if (IsOpen && state != GameState.Paused)
            {
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
            if (title != null)
            {
                title.color = GameTheme.TextPrimary;
                body.color = GameTheme.TextSecondary;
            }
        }

        private void RefreshTexts()
        {
            title.text = GameLocalization.LeaveLevelTitle;
            body.text = GameLocalization.LeaveLevelBody;
            StyleButton(leaveButton, GameLocalization.LeaveLevelYes, GameTheme.ButtonSecondary, GameTheme.TextPrimary);
            StyleButton(stayButton, GameLocalization.LeaveLevelNo, GameTheme.Accent, DarkLabel);
        }

        private void HandleStayClicked()
        {
            onLeave = null;
            Close();
        }

        private void HandleLeaveClicked()
        {
            Action leave = onLeave;
            onLeave = null;
            Close();
            leave?.Invoke();
        }
    }
}
