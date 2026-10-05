using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Levels;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// The card that opens over the level map when an open level is tapped: its number, the goal with
    /// an icon, the move limit, the best stars and the "Play" button. Play starts the level through
    /// <see cref="GameManager.StartLevelFromMap"/>, which is also where the level ad break happens.
    /// The card belongs to the main menu state: any other state closes it.
    /// </summary>
    public sealed class LevelCardPanel : LevelOverlayPanel
    {
        public const string ObjectName = "LevelCardPanel";

        private const int StarCount = LevelProgress.MaxStars;
        private const float StarSize = 110f;
        private const float StarGap = 24f;
        private const float GoalIconSize = 130f;

        private static readonly Vector2 CardSize = new Vector2(860f, 1120f);
        private static readonly Vector2 PlaySize = new Vector2(620f, 130f);
        private static readonly Vector2 CloseSize = new Vector2(440f, 88f);

        private TMP_Text title;
        private TMP_Text special;
        private TMP_Text goalHeader;
        private Image goalIcon;
        private TMP_Text goalText;
        private TMP_Text movesText;
        private TMP_Text bestCaption;
        private readonly Image[] stars = new Image[StarCount];
        private Button playButton;
        private Button closeButton;

        private int levelNumber;

        /// <summary>Builds the card under <paramref name="canvasRect"/>, or rebinds the one already there.</summary>
        public static LevelCardPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            LevelCardPanel existing = FindObjectOfType<LevelCardPanel>(true);
            if (existing != null)
            {
                existing.BindGame(manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            var panel = CreateOverlay<LevelCardPanel>(canvasRect, ObjectName, CardSize, 0.65f);
            panel.Build();
            panel.BindGame(manager);
            return panel;
        }

        private void Build()
        {
            title = AddText("Title", string.Empty, 70f, GameTheme.TextPrimary, -44f, new Vector2(800f, 90f));
            special = AddText("Special", string.Empty, 34f, LevelArt.StarGold, -136f, new Vector2(800f, 44f));
            goalHeader = AddText("GoalHeader", string.Empty, 34f, GameTheme.TextSecondary, -212f, new Vector2(800f, 44f));

            goalIcon = UIFactory.CreateImage("GoalIcon", Card, Color.white, false);
            goalIcon.preserveAspect = true;
            goalIcon.raycastTarget = false;
            UIFactory.Anchor(
                goalIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -268f),
                new Vector2(GoalIconSize, GoalIconSize));

            goalText = AddText("GoalText", string.Empty, 46f, GameTheme.Accent, -412f, new Vector2(760f, 130f));
            goalText.enableWordWrapping = true;
            goalText.enableAutoSizing = true;
            goalText.fontSizeMin = 30f;
            goalText.fontSizeMax = 46f;

            movesText = AddText("Moves", string.Empty, 40f, GameTheme.TextPrimary, -556f, new Vector2(760f, 56f));
            bestCaption = AddText("BestCaption", string.Empty, 30f, GameTheme.TextSecondary, -650f, new Vector2(800f, 40f));

            RectTransform starsRow = UIFactory.CreateRect("Stars", Card);
            UIFactory.Anchor(
                starsRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -700f),
                new Vector2(StarCount * StarSize + (StarCount - 1) * StarGap, StarSize));
            for (int i = 0; i < StarCount; i++)
            {
                Image star = UIFactory.CreateImage($"Star_{i}", starsRow, Color.white, false);
                star.sprite = LevelArt.StarSprite;
                star.preserveAspect = true;
                star.raycastTarget = false;
                UIFactory.Anchor(
                    star.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(i * (StarSize + StarGap), 0f), new Vector2(StarSize, StarSize));
                stars[i] = star;
            }

            closeButton = AddButton("CloseButton", string.Empty, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 32f, 40f, CloseSize);
            playButton = AddButton("PlayButton", string.Empty, GameTheme.ShopBuy, DarkLabel, 52f, 170f, PlaySize);

            Listen(playButton, HandlePlayClicked);
            Listen(closeButton, HandleCloseClicked);

            // A tap on the dim outside the card closes it too.
            Transform dim = transform.Find("Dim");
            if (dim != null)
            {
                var dimButton = dim.gameObject.AddComponent<Button>();
                dimButton.transition = Selectable.Transition.None;
                dimButton.targetGraphic = dim.GetComponent<Image>();
                dimButton.onClick.AddListener(HandleCloseClicked);
            }
        }

        /// <summary>Fills the card for <paramref name="number"/> and opens it.</summary>
        public void Show(int number)
        {
            LevelDefinition level = LevelDatabase.Load()?.GetByNumber(number);
            if (level == null)
            {
                return;
            }

            levelNumber = number;
            Fill(level);
            Open();
        }

        public void Hide()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        protected override void HandleStateChanged(GameState state)
        {
            // The card sits on the map, which lives in the main menu state; anything else closes it.
            if (IsOpen && state != GameState.MainMenu)
            {
                Close();
            }
        }

        protected override void HandleLanguageChanged()
        {
            LevelDefinition level = IsOpen ? LevelDatabase.Load()?.GetByNumber(levelNumber) : null;
            if (level != null)
            {
                Fill(level);
            }
        }

        protected override void ApplyTheme()
        {
            base.ApplyTheme();
            if (title == null)
            {
                return;
            }

            title.color = GameTheme.TextPrimary;
            goalHeader.color = GameTheme.TextSecondary;
            goalText.color = GameTheme.Accent;
            movesText.color = GameTheme.TextPrimary;
            bestCaption.color = GameTheme.TextSecondary;
            StyleButton(closeButton, GameLocalization.Close, GameTheme.ButtonSecondary, GameTheme.TextPrimary);
            StyleButton(playButton, GameLocalization.LevelPlay, GameTheme.ShopBuy, DarkLabel);
        }

        private void Fill(LevelDefinition level)
        {
            title.text = GameLocalization.LevelTitle(level.Number);
            special.gameObject.SetActive(level.IsSpecial);
            special.text = GameLocalization.SpecialLevel;
            goalHeader.text = GameLocalization.LevelGoalHeader;
            goalText.text = GameLocalization.LevelGoalShort(level);
            movesText.text = GameLocalization.MoveLimitLine(level.MoveLimit);
            bestCaption.text = GameLocalization.BestResult;

            ApplyGoalIcon(level.GoalType);

            int best = LevelProgress.GetStars(level.Number);
            for (int i = 0; i < StarCount; i++)
            {
                stars[i].color = i < best ? LevelArt.StarGold : new Color(1f, 1f, 1f, 0.16f);
            }

            StyleButton(closeButton, GameLocalization.Close, GameTheme.ButtonSecondary, GameTheme.TextPrimary);
            StyleButton(playButton, GameLocalization.LevelPlay, GameTheme.ShopBuy, DarkLabel);
        }

        private void ApplyGoalIcon(LevelGoalType goal)
        {
            switch (goal)
            {
                case LevelGoalType.Gems:
                    goalIcon.sprite = LevelArt.CrystalSprite;
                    goalIcon.color = Color.white;
                    break;
                case LevelGoalType.ClearMarked:
                    goalIcon.sprite = LevelArt.FrameSprite;
                    goalIcon.color = LevelArt.MarkColor;
                    break;
                case LevelGoalType.Lines:
                    goalIcon.sprite = LevelMapArt.LinesSprite;
                    goalIcon.color = GameTheme.Accent;
                    break;
                default:
                    goalIcon.sprite = LevelMapArt.TargetSprite;
                    goalIcon.color = GameTheme.Accent;
                    break;
            }
        }

        private void HandlePlayClicked()
        {
            if (!IsOpen)
            {
                return;
            }

            int number = levelNumber;

            // The level starts on this very tap (the ad, if any, opens from the game manager's event).
            Close();
            Game?.StartLevelFromMap(number);
        }

        private void HandleCloseClicked()
        {
            if (IsOpen)
            {
                Close();
            }
        }
    }
}
