using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Levels;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The HUD of a level, drawn over the top panel in place of the endless score and record: the
    /// level number, the goal with its progress ("3/10" beside a crystal, "Score 1200/2000") and the
    /// moves left, which turn red at <see cref="LowMoves"/> or fewer. The pause and shop buttons of the
    /// panel stay as they are. Collected crystals fly from their cells to the goal counter, and the
    /// counter only counts them when they arrive.
    /// </summary>
    public sealed class LevelHudView : MonoBehaviour
    {
        public const string ObjectName = "LevelHud";

        /// <summary>Moves left at which the counter warns.</summary>
        public const int LowMoves = 3;

        private const float LeftWidth = 360f;
        private const float GoalIconSize = 54f;
        private const float FlightDuration = 0.55f;
        private const float FlightStagger = 0.06f;

        private static readonly Color WarningColor = GameTheme.FromHex("#FF5A5A");

        private GameManager game;
        private LevelRunController levelRun;
        private RectTransform topPanel;

        private TMP_Text title;
        private TMP_Text goalText;
        private Image goalIcon;
        private RectTransform goalRow;
        private TMP_Text movesText;

        private readonly List<RectTransform> flights = new List<RectTransform>();
        private int pendingCrystals;
        private int shownMoves = int.MinValue;
        private bool visible;

        /// <summary>Builds the level HUD inside <paramref name="topPanel"/>, or rebinds the one already there.</summary>
        public static LevelHudView Ensure(RectTransform topPanel, GameManager manager)
        {
            if (topPanel == null)
            {
                return null;
            }

            LevelHudView existing = topPanel.GetComponentInChildren<LevelHudView>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                return existing;
            }

            RectTransform root = UIFactory.CreateRect(ObjectName, topPanel);
            UIFactory.Stretch(root);
            root.SetAsFirstSibling();

            var view = root.gameObject.AddComponent<LevelHudView>();
            view.topPanel = topPanel;
            view.Build(root);
            view.Bind(manager);
            return view;
        }

        private void Build(RectTransform root)
        {
            // Left: the level number over the goal.
            RectTransform left = UIFactory.CreateRect("Left", root);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, 0.5f);
            left.sizeDelta = new Vector2(LeftWidth, 0f);
            left.anchoredPosition = new Vector2(20f, 0f);

            title = CreateHudText("Title", left, 44f, GameTheme.TextPrimary, TextAlignmentOptions.BottomLeft);
            SetRows(title.rectTransform, 0.5f, 1f);

            goalRow = UIFactory.CreateRect("GoalRow", left);
            SetRows(goalRow, 0f, 0.5f);

            goalIcon = UIFactory.CreateImage("GoalIcon", goalRow, Color.white, rounded: false);
            goalIcon.preserveAspect = true;
            goalIcon.raycastTarget = false;
            UIFactory.Anchor(
                goalIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                new Vector2(GoalIconSize, GoalIconSize));

            goalText = CreateHudText("GoalText", goalRow, 46f, GameTheme.Accent, TextAlignmentOptions.MidlineLeft);
            goalText.rectTransform.anchorMin = Vector2.zero;
            goalText.rectTransform.anchorMax = Vector2.one;
            goalText.rectTransform.offsetMin = Vector2.zero;
            goalText.rectTransform.offsetMax = Vector2.zero;

            // Under the goal: the moves left ("ХОДЫ 12"). The centre of the strip belongs to the shop button.
            movesText = CreateHudText("Moves", left, 40f, GameTheme.TextPrimary, TextAlignmentOptions.TopLeft);
            movesText.rectTransform.pivot = new Vector2(0f, 0.5f);
        }

        /// <summary>
        /// Two rows (level, goal) without a move limit; three rows (level, goal, moves) with one.
        /// </summary>
        private void ApplyRows(bool limited)
        {
            if (limited)
            {
                SetRows(title.rectTransform, 0.66f, 1f);
                SetRows(goalRow, 0.33f, 0.66f);
                SetRows(movesText.rectTransform, 0f, 0.33f);
                movesText.rectTransform.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                SetRows(title.rectTransform, 0.5f, 1f);
                SetRows(goalRow, 0f, 0.5f);
            }
        }

        private static TMP_Text CreateHudText(
            string objectName, Transform parent, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = UIFactory.CreateText(objectName, parent, string.Empty, size, color, alignment, FontStyles.Bold);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = size;
            return text;
        }

        /// <summary>Stretches a rect over a horizontal band of its parent, from <paramref name="from"/> to <paramref name="to"/> (0 = bottom, 1 = top).</summary>
        private static void SetRows(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(0f, from);
            rect.anchorMax = new Vector2(1f, to);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void Bind(GameManager manager)
        {
            Unbind();
            game = manager;
            levelRun = game != null ? game.LevelRun : null;
            if (game == null || levelRun == null)
            {
                return;
            }

            game.StateChanged += HandleStateChanged;
            levelRun.LevelStarted += HandleLevelStarted;
            levelRun.MovesChanged += HandleMovesChanged;
            levelRun.GoalProgressChanged += RefreshGoal;
            levelRun.CrystalsCollected += HandleCrystalsCollected;
            GameLocalization.LanguageChanged += RefreshAll;
            GameTheme.Changed += RefreshAll;

            UpdateVisibility();
            RefreshAll();
        }

        private void Unbind()
        {
            if (game != null)
            {
                game.StateChanged -= HandleStateChanged;
            }

            if (levelRun != null)
            {
                levelRun.LevelStarted -= HandleLevelStarted;
                levelRun.MovesChanged -= HandleMovesChanged;
                levelRun.GoalProgressChanged -= RefreshGoal;
                levelRun.CrystalsCollected -= HandleCrystalsCollected;
            }

            GameLocalization.LanguageChanged -= RefreshAll;
            GameTheme.Changed -= RefreshAll;
            game = null;
            levelRun = null;
        }

        private void OnDestroy()
        {
            Unbind();
            ClearFlights();
        }

        private void HandleStateChanged(GameState state) => UpdateVisibility();

        private void HandleLevelStarted(LevelDefinition definition)
        {
            ClearFlights();
            shownMoves = int.MinValue;
            UpdateVisibility();
            RefreshAll();
        }

        /// <summary>Shows the level HUD instead of the endless score while a level is on screen.</summary>
        private void UpdateVisibility()
        {
            bool show = game != null
                && game.Mode == GameMode.Level
                && game.State != GameState.MainMenu
                && game.State != GameState.Boot
                && levelRun != null
                && levelRun.Level != null;

            if (show == visible && transform.GetChild(0).gameObject.activeSelf == show)
            {
                return;
            }

            visible = show;
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(show);
            }

            SetEndlessSectionsActive(!show);

            // The moves block is only wanted by a level with a move limit.
            if (show)
            {
                RefreshAll();
            }
        }

        /// <summary>The endless score and record share the strip with the level HUD, so they step aside.</summary>
        private void SetEndlessSectionsActive(bool active)
        {
            if (topPanel == null)
            {
                return;
            }

            string[] names = { "ScoreSection", "ScoreGroup", "BestSection", "BestGroup" };
            for (int i = 0; i < names.Length; i++)
            {
                Transform section = topPanel.Find(names[i]);
                if (section != null)
                {
                    section.gameObject.SetActive(active);
                }
            }
        }

        // ---------------------------------------------------------------- content

        private void RefreshAll()
        {
            if (levelRun == null || levelRun.Level == null || title == null)
            {
                return;
            }

            title.text = GameLocalization.LevelTitle(levelRun.LevelNumber).ToUpperInvariant();
            title.color = GameTheme.TextPrimary;

            RefreshGoal();
            RefreshMoves(false);
        }

        private void RefreshGoal()
        {
            if (levelRun == null || levelRun.Level == null || goalText == null)
            {
                return;
            }

            LevelGoalType goal = levelRun.GoalType;
            int progress = levelRun.GoalProgress;
            if (goal == LevelGoalType.Gems)
            {
                // A crystal is counted when it lands, not when its line is cleared.
                progress = Mathf.Max(0, progress - pendingCrystals);
            }

            goalText.text = GameLocalization.LevelGoalCounter(goal, progress, levelRun.GoalTarget);
            goalText.color = GameTheme.Accent;

            bool icon = goal == LevelGoalType.Gems || goal == LevelGoalType.ClearMarked;
            goalIcon.gameObject.SetActive(icon);
            if (icon)
            {
                goalIcon.sprite = goal == LevelGoalType.Gems ? LevelArt.CrystalSprite : LevelArt.FrameSprite;
                goalIcon.type = goal == LevelGoalType.Gems ? Image.Type.Simple : Image.Type.Sliced;
                goalIcon.color = goal == LevelGoalType.Gems ? Color.white : LevelArt.MarkColor;
            }

            RectTransform textRect = goalText.rectTransform;
            textRect.offsetMin = new Vector2(icon ? GoalIconSize + 12f : 0f, 0f);
        }

        private void HandleMovesChanged() => RefreshMoves(true);

        private void RefreshMoves(bool animate)
        {
            if (levelRun == null || movesText == null)
            {
                return;
            }

            bool limited = levelRun.HasMoveLimit;
            ApplyRows(limited);
            movesText.gameObject.SetActive(limited && visible);
            if (!limited)
            {
                return;
            }

            int left = levelRun.MovesLeft;
            Color valueColor = left <= LowMoves ? WarningColor : GameTheme.TextPrimary;
            movesText.color = GameTheme.TextSecondary;
            movesText.text = $"{GameLocalization.MovesCaption} <color=#{ColorUtility.ToHtmlStringRGBA(valueColor)}>{left}</color>";

            if (animate && left != shownMoves && shownMoves != int.MinValue)
            {
                GameTween.Punch(movesText.rectTransform, left <= LowMoves ? 0.3f : 0.15f, 0.25f, unscaled: true);
            }

            shownMoves = left;
        }

        // ---------------------------------------------------------------- crystal flight

        private void HandleCrystalsCollected(IReadOnlyList<Vector2Int> cells)
        {
            GridLayer(out RectTransform layer, out float cellSize);
            if (layer == null || game == null || game.Grid == null || goalIcon == null || !visible)
            {
                return;
            }

            Vector3 target = layer.InverseTransformPoint(goalIcon.transform.position);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3 start = layer.InverseTransformPoint(game.Grid.GetCellWorldPosition(cells[i]));
                pendingCrystals++;
                SpawnFlight(layer, start, target, cellSize * 0.66f, i * FlightStagger);
            }
        }

        private void SpawnFlight(RectTransform layer, Vector3 start, Vector3 target, float size, float delay)
        {
            Image icon = UIFactory.CreateImage("CrystalFlight", layer, Color.white, rounded: false);
            icon.sprite = LevelArt.CrystalSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            RectTransform rect = icon.rectTransform;
            UIFactory.Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), start, new Vector2(size, size));
            flights.Add(rect);

            GameTween.Scale(rect, Vector3.one * 0.6f, FlightDuration, TweenEase.InQuad, delay);
            GameTween.MoveAnchored(rect, target, FlightDuration, TweenEase.InQuad, delay, onComplete: () => LandFlight(rect));
        }

        private void LandFlight(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            flights.Remove(rect);
            Destroy(rect.gameObject);
            pendingCrystals = Mathf.Max(0, pendingCrystals - 1);
            RefreshGoal();

            if (goalIcon != null)
            {
                GameTween.Punch(goalIcon.rectTransform, 0.35f, 0.25f);
            }
        }

        /// <summary>The full-screen layer the flights travel on, and the current size of a board cell.</summary>
        private void GridLayer(out RectTransform layer, out float cellSize)
        {
            layer = game != null && game.Spawner != null ? game.Spawner.DragLayer : null;
            cellSize = game != null && game.Grid != null ? game.Grid.CellSize : GameTheme.CellSize;
        }

        private void ClearFlights()
        {
            for (int i = 0; i < flights.Count; i++)
            {
                if (flights[i] != null)
                {
                    GameTween.Kill(flights[i]);
                    Destroy(flights[i].gameObject);
                }
            }

            flights.Clear();
            pendingCrystals = 0;
        }
    }
}
