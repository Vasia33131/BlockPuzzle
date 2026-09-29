using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// "Start over?" confirmation of the pause screen. Restarting throws away the current run or
    /// level attempt, so the pause screen always asks first: red restarts, green keeps playing.
    /// Opens over the pause screen and closes when the game leaves the paused state.
    /// </summary>
    public sealed class RestartConfirmPanel : LevelOverlayPanel
    {
        public const string ObjectName = "RestartConfirmPanel";

        private static readonly Color DangerRed = GameTheme.FromHex("#E5484D");
        private static readonly Vector2 CardSize = new Vector2(780f, 560f);
        private static readonly Vector2 ButtonSize = new Vector2(300f, 120f);
        private const float ButtonOffset = 165f;

        private TMP_Text title;
        private TMP_Text body;
        private Button cancelButton;
        private Button restartButton;
        private Action onRestart;

        /// <summary>Builds the panel under <paramref name="canvasRect"/>, or rebinds the one already there.</summary>
        public static RestartConfirmPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            RestartConfirmPanel existing = FindObjectOfType<RestartConfirmPanel>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            var panel = CreateOverlay<RestartConfirmPanel>(canvasRect, ObjectName, CardSize, 0.6f);
            panel.Build();
            panel.Bind(manager);
            return panel;
        }

        /// <summary>Opens the question; <paramref name="confirmed"/> runs only when the player chooses to restart.</summary>
        public void Show(Action confirmed)
        {
            onRestart = confirmed;
            RefreshTexts();
            Open();
        }

        private void Build()
        {
            title = AddText("Title", string.Empty, 58f, GameTheme.TextPrimary, -44f, new Vector2(700f, 80f));
            body = AddText("Body", string.Empty, 34f, GameTheme.TextSecondary, -150f, new Vector2(680f, 130f), FontStyles.Normal);
            body.enableWordWrapping = true;

            // Side by side: the red restart on the left, the green cancel on the right.
            restartButton = AddButton("RestartButton", string.Empty, DangerRed, Color.white, 40f, 60f, ButtonSize);
            cancelButton = AddButton("CancelButton", string.Empty, GameTheme.ShopBuy, DarkLabel, 40f, 60f, ButtonSize);
            ((RectTransform)restartButton.transform).anchoredPosition = new Vector2(-ButtonOffset, 60f);
            ((RectTransform)cancelButton.transform).anchoredPosition = new Vector2(ButtonOffset, 60f);
        }

        private void Bind(GameManager manager)
        {
            Listen(cancelButton, HandleCancelClicked);
            Listen(restartButton, HandleRestartClicked);
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
            title.text = GameLocalization.RestartConfirmTitle;
            body.text = GameLocalization.RestartConfirmBody;
            StyleButton(restartButton, GameLocalization.RestartConfirmYes, DangerRed, Color.white);
            StyleButton(cancelButton, GameLocalization.RestartConfirmNo, GameTheme.ShopBuy, DarkLabel);
        }

        private void HandleCancelClicked()
        {
            onRestart = null;
            Close();
        }

        private void HandleRestartClicked()
        {
            Action restart = onRestart;
            onRestart = null;
            Close();
            restart?.Invoke();
        }
    }
}
