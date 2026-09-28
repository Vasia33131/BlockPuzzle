using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// "New level!" banner. A level reached at the end of a run (the experience is paid out on
    /// Game Over or a restart) is held back until the player is out of the game: it appears on
    /// the game-over screen or in the main menu, never over the board. It does not take input,
    /// so it never gets between the player and the game-over buttons, and starting a new run
    /// dismisses it. Runs on unscaled time.
    /// </summary>
    public class LevelUpPopup : MonoBehaviour
    {
        public const string ObjectName = "LevelUpPopup";

        private const float TopOffset = 120f;
        private const float Width = 720f;
        private const float Height = 250f;
        private const float InDuration = 0.3f;
        private const float Hold = 2.6f;
        private const float OutDuration = 0.35f;

        /// <summary>Lets the game-over card finish its own reveal (0.7 s wait + 0.3 s pop) first.</summary>
        private const float GameOverDelay = 1.3f;
        private const float MenuDelay = 0.5f;

        private static readonly Vector2 ShieldSize = new Vector2(120f, 138f);

        private readonly object scheduleKey = new object();
        private GameManager gameManager;
        private CanvasGroup group;
        private RectTransform pill;
        private RectTransform shield;
        private TMP_Text levelLabel;
        private TMP_Text titleLabel;
        private TMP_Text captionLabel;
        private ConfettiBurst confetti;
        private int pendingLevel;
        private int shownLevel;
        private bool built;

        /// <summary>Builds the popup on the canvas (or rebinds the existing one).</summary>
        public static LevelUpPopup Ensure(RectTransform canvasRect, GameManager manager)
        {
            LevelUpPopup existing = FindObjectOfType<LevelUpPopup>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            RectTransform root = UIFactory.CreateRect(ObjectName, canvasRect);
            UIFactory.Stretch(root);
            var popup = root.gameObject.AddComponent<LevelUpPopup>();
            popup.Build();
            popup.Bind(manager);
            return popup;
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            if (gameManager != null && isActiveAndEnabled)
            {
                gameManager.StateChanged += HandleStateChanged;
            }
        }

        private void OnEnable()
        {
            PlayerLevel.LevelUp += HandleLevelUp;
            GameLocalization.LanguageChanged += RefreshTexts;
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager.StateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            PlayerLevel.LevelUp -= HandleLevelUp;
            GameLocalization.LanguageChanged -= RefreshTexts;
            Unbind();
            GameTween.Kill(scheduleKey);
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
            }
        }

        private void HandleLevelUp(int newLevel)
        {
            pendingLevel = Mathf.Max(pendingLevel, newLevel);

            // Experience is paid out as the state flips to Game Over; a restart from pause pays it
            // while the run is still "Paused", and then the banner waits for the next run to end.
            if (IsOutOfGame(gameManager != null ? gameManager.State : GameState.Boot))
            {
                Schedule(gameManager.State);
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (IsOutOfGame(state))
            {
                if (pendingLevel > 0)
                {
                    Schedule(state);
                }

                return;
            }

            if (state == GameState.Playing)
            {
                GameTween.Kill(scheduleKey);
                HideNow();
            }
        }

        private static bool IsOutOfGame(GameState state)
        {
            return state == GameState.GameOver || state == GameState.MainMenu;
        }

        private void Schedule(GameState state)
        {
            GameTween.Kill(scheduleKey);
            float delay = state == GameState.GameOver ? GameOverDelay : MenuDelay;
            GameTween.Delay(scheduleKey, delay, true, () =>
            {
                if (pendingLevel > 0 && gameManager != null && IsOutOfGame(gameManager.State))
                {
                    Play();
                }
            });
        }

        private void Play()
        {
            shownLevel = pendingLevel;
            pendingLevel = 0;
            RefreshTexts();

            transform.SetAsLastSibling();
            GameTween.Kill(group);
            GameTween.Kill(pill);
            GameTween.Kill(shield);
            group.alpha = 0f;
            pill.localScale = Vector3.one * 0.7f;
            GameTween.Scale(pill, Vector3.one, InDuration, TweenEase.OutBack, unscaled: true);
            GameTween.Fade(group, 1f, InDuration, TweenEase.OutQuad, unscaled: true, onComplete: () =>
                GameTween.Fade(group, 0f, OutDuration, TweenEase.InQuad, Hold, unscaled: true));
            GameTween.Punch(shield, 0.5f, 0.6f, unscaled: true);

            if (confetti != null)
            {
                RectTransform layer = (RectTransform)confetti.transform;
                Vector2 origin = layer.InverseTransformPoint(pill.TransformPoint(Vector3.zero));
                confetti.Play(origin);
            }
        }

        private void HideNow()
        {
            if (group == null)
            {
                return;
            }

            GameTween.Kill(group);
            GameTween.Kill(pill);
            group.alpha = 0f;
            confetti?.Stop();
        }

        private void RefreshTexts()
        {
            if (!built)
            {
                return;
            }

            int level = shownLevel > 0 ? shownLevel : PlayerLevel.Level;
            levelLabel.text = level.ToString();
            levelLabel.fontSize = level >= 100 ? 40f : 56f;
            titleLabel.text = GameLocalization.NewLevelTitle;
            captionLabel.text = GameLocalization.PlayerLevelCaption(level);
        }

        private void Build()
        {
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image background = UIFactory.CreateImage("Pill", transform, GameTheme.WithAlpha(GameTheme.CardBackground, 0.97f));
            background.raycastTarget = false;
            pill = background.rectTransform;
            UIFactory.Anchor(
                pill, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -TopOffset), new Vector2(Width, Height));

            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = GameTheme.WithAlpha(MetaUi.CoinGold, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);

            Image rim = UIFactory.CreateImage("Shield", pill, MetaUi.DarkLabel, rounded: false);
            rim.sprite = ProfileUi.ShieldSprite;
            rim.raycastTarget = false;
            shield = rim.rectTransform;
            UIFactory.Anchor(
                shield, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(56f, 0f), ShieldSize);

            Image fill = UIFactory.CreateImage("Fill", shield, GameTheme.Accent, rounded: false);
            fill.sprite = ProfileUi.ShieldSprite;
            fill.raycastTarget = false;
            UIFactory.Stretch(fill.rectTransform, 8f);

            levelLabel = UIFactory.CreateText(
                "Level", shield, "1", 56f, MetaUi.DarkLabel, TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Stretch(levelLabel.rectTransform);
            levelLabel.rectTransform.offsetMin = new Vector2(0f, 18f);

            float textLeft = 56f + ShieldSize.x + 30f;
            titleLabel = UIFactory.CreateText(
                "Title", pill, string.Empty, 50f, MetaUi.CoinGold, TextAlignmentOptions.Left, FontStyles.Bold);
            titleLabel.enableAutoSizing = true;
            titleLabel.fontSizeMin = 30f;
            titleLabel.fontSizeMax = 50f;
            UIFactory.Anchor(
                titleLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0f),
                new Vector2(textLeft, 4f),
                new Vector2(Width - textLeft - 30f, 70f));

            captionLabel = UIFactory.CreateText(
                "Caption", pill, string.Empty, 38f, GameTheme.TextPrimary, TextAlignmentOptions.Left, FontStyles.Normal, FontRole.Body);
            UIFactory.Anchor(
                captionLabel.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 1f),
                new Vector2(textLeft, -2f),
                new Vector2(Width - textLeft - 30f, 54f));

            confetti = ConfettiBurst.Create(transform);

            built = true;
            RefreshTexts();
        }
    }
}
