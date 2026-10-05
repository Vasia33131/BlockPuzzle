using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Levels;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Result screen of a passed level. It plays a short sequence on unscaled time: the unused
    /// moves turn into bonus points one by one, the stars pop in (1–3), the coins and the experience
    /// appear, and only then the buttons wake up. "Next" starts the following level, "Menu" leaves, and
    /// the rewarded "x2 coins" button doubles the coins once. The reward itself is paid by
    /// <see cref="LevelRunController"/> the moment the goal is reached; this panel only shows it.
    /// </summary>
    public sealed class LevelWinPanel : LevelOverlayPanel
    {
        public const string ObjectName = "LevelWinPanel";

        private const float RevealDelay = 0.9f;
        private const float BonusDuration = 1.0f;
        private const float MaxBonusStep = 0.14f;
        private const float StarStep = 0.32f;
        private const int StarCount = LevelProgress.MaxStars;

        private static readonly Vector2 CardSize = new Vector2(860f, 1120f);
        private static readonly Vector2 NextSize = new Vector2(620f, 120f);
        private static readonly Vector2 DoubleSize = new Vector2(640f, 130f);
        private static readonly Vector2 MenuSize = new Vector2(440f, 88f);
        private const float StarSize = 180f;
        private const float StarGap = 30f;

        private LevelRunController levelRun;

        private TMP_Text title;
        private TMP_Text subtitle;
        private RectTransform starsRow;
        private readonly Image[] stars = new Image[StarCount];
        private TMP_Text bonusLabel;
        private RectTransform bonusRow;
        private TMP_Text movesText;
        private TMP_Text bonusText;
        private RectTransform coinsRoot;
        private TMP_Text coinsText;
        private TMP_Text xpText;
        private Button nextButton;
        private Button doubleButton;
        private Button menuButton;
        private Image doubleGlow;
        private ConfettiBurst confetti;

        private Coroutine sequence;
        private bool buttonsLive;

        /// <summary>Raised when the player taps "x2 coins". Platform code shows the rewarded ad.</summary>
        public event Action DoubleRequested;

        /// <summary>Builds the panel under <paramref name="canvasRect"/>, or rebinds the one already there.</summary>
        public static LevelWinPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            LevelWinPanel existing = FindObjectOfType<LevelWinPanel>(true);
            if (existing != null)
            {
                existing.Bind(manager);
                return existing;
            }

            if (canvasRect == null)
            {
                return null;
            }

            var panel = CreateOverlay<LevelWinPanel>(canvasRect, ObjectName, CardSize, 0.8f);
            panel.Build();
            panel.Bind(manager);
            return panel;
        }

        private void Build()
        {
            title = AddText("Title", string.Empty, 62f, GameTheme.TextPrimary, -44f, new Vector2(800f, 80f));
            subtitle = AddText("Subtitle", string.Empty, 38f, GameTheme.TextSecondary, -126f, new Vector2(800f, 46f));

            starsRow = UIFactory.CreateRect("Stars", Card);
            UIFactory.Anchor(
                starsRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -192f),
                new Vector2(StarCount * StarSize + (StarCount - 1) * StarGap, StarSize));
            for (int i = 0; i < StarCount; i++)
            {
                Image star = UIFactory.CreateImage($"Star_{i}", starsRow, Color.white, rounded: false);
                star.sprite = LevelArt.StarSprite;
                star.preserveAspect = true;
                star.raycastTarget = false;
                UIFactory.Anchor(
                    star.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(i * (StarSize + StarGap), 0f), new Vector2(StarSize, StarSize));
                stars[i] = star;
            }

            bonusLabel = AddText("BonusLabel", string.Empty, 32f, GameTheme.TextSecondary, -394f, new Vector2(800f, 40f));

            bonusRow = UIFactory.CreateRect("BonusRow", Card);
            UIFactory.Anchor(bonusRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -436f), new Vector2(760f, 70f));
            movesText = CreateRowText("Moves", TextAlignmentOptions.MidlineRight, 0f, 0.5f, GameTheme.TextPrimary);
            bonusText = CreateRowText("Bonus", TextAlignmentOptions.MidlineLeft, 0.5f, 1f, MetaUi.CoinGold);

            coinsText = MetaUi.CreateCoinAmount("Coins", Card, 60f, MetaUi.CoinGold);
            coinsRoot = (RectTransform)coinsText.transform.parent;
            UIFactory.Anchor(coinsRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -530f), new Vector2(520f, 76f));

            xpText = AddText("Xp", string.Empty, 36f, GameTheme.Accent, -614f, new Vector2(800f, 46f));

            menuButton = AddButton("MenuButton", string.Empty, GameTheme.ButtonSecondary, GameTheme.TextPrimary, 32f, 40f, MenuSize);
            nextButton = AddButton("NextButton", string.Empty, GameTheme.Accent, DarkLabel, 46f, 150f, NextSize);

            doubleGlow = UIFactory.CreateImage("DoubleGlow", Card, GameTheme.WithAlpha(GameTheme.ShopBuy, 0.3f));
            doubleGlow.raycastTarget = false;
            UIFactory.Anchor(
                doubleGlow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 300f + DoubleSize.y * 0.5f), DoubleSize + new Vector2(28f, 28f));

            doubleButton = AddButton("DoubleButton", string.Empty, GameTheme.ShopBuy, DarkLabel, 40f, 300f, DoubleSize);
            BuildDoubleButtonHint();
            doubleGlow.transform.SetSiblingIndex(doubleButton.transform.GetSiblingIndex());

            confetti = ConfettiBurst.Create(transform);
        }

        private TMP_Text CreateRowText(string objectName, TextAlignmentOptions alignment, float from, float to, Color color)
        {
            TextMeshProUGUI text = UIFactory.CreateText(objectName, bonusRow, string.Empty, 50f, color, alignment, FontStyles.Bold);
            text.rectTransform.anchorMin = new Vector2(from, 0f);
            text.rectTransform.anchorMax = new Vector2(to, 1f);
            text.rectTransform.offsetMin = new Vector2(from > 0f ? 16f : 0f, 0f);
            text.rectTransform.offsetMax = new Vector2(from > 0f ? 0f : -16f, 0f);
            UIFactory.FitText(text);
            return text;
        }

        /// <summary>The two-line ad button: the action on top, what the ad gives underneath.</summary>
        private void BuildDoubleButtonHint()
        {
            TMP_Text label = doubleButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                UIFactory.Anchor(
                    label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                    new Vector2(DoubleSize.x - 20f, 52f));
            }

            TextMeshProUGUI hint = UIFactory.CreateText("Hint", doubleButton.transform, string.Empty, 26f, DarkLabel);
            UIFactory.Anchor(
                hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -28f),
                new Vector2(DoubleSize.x - 20f, 36f));
        }

        private void Bind(GameManager manager)
        {
            if (levelRun != null)
            {
                levelRun.CoinsDoubled -= HandleCoinsDoubled;
            }

            Listen(nextButton, HandleNextClicked);
            Listen(menuButton, HandleMenuClicked);
            Listen(doubleButton, HandleDoubleClicked);

            BindGame(manager);
            levelRun = manager != null ? manager.LevelRun : null;
            if (levelRun != null)
            {
                levelRun.CoinsDoubled += HandleCoinsDoubled;
            }
        }

        protected override void OnDestroy()
        {
            if (levelRun != null)
            {
                levelRun.CoinsDoubled -= HandleCoinsDoubled;
                levelRun = null;
            }

            base.OnDestroy();
        }

        protected override void HandleStateChanged(GameState state)
        {
            if (state == GameState.LevelWon)
            {
                BeginSequence();
            }
            else if (IsOpen)
            {
                StopSequence();
                Close();
            }
        }

        protected override void HandleLanguageChanged() => RefreshTexts();

        protected override void ApplyTheme()
        {
            base.ApplyTheme();
            title.color = GameTheme.TextPrimary;
            subtitle.color = GameTheme.TextSecondary;
            bonusLabel.color = GameTheme.TextSecondary;
            movesText.color = GameTheme.TextPrimary;
            xpText.color = GameTheme.Accent;
            StyleButton(menuButton, GameLocalization.Home, GameTheme.ButtonSecondary, GameTheme.TextPrimary);
            StyleButton(nextButton, GameLocalization.NextLevel, GameTheme.Accent, DarkLabel);
            StyleButton(doubleButton, GameLocalization.DoubleCoinsAd, GameTheme.ShopBuy, DarkLabel);
            RefreshDoubleHint();
        }

        // ---------------------------------------------------------------- sequence

        private void BeginSequence()
        {
            StopSequence();
            LevelResult result = levelRun != null ? levelRun.Result : null;
            if (result == null)
            {
                return;
            }

            RefreshTexts();
            PrepareForSequence(result);
            sequence = StartCoroutine(PlaySequence(result));
        }

        /// <summary>Everything the sequence reveals starts hidden; the buttons wait.</summary>
        private void PrepareForSequence(LevelResult result)
        {
            buttonsLive = false;
            bool hasBonus = result.MovesLeft > 0;

            for (int i = 0; i < StarCount; i++)
            {
                GameTween.Kill(stars[i]);
                GameTween.Kill(stars[i].rectTransform);
                stars[i].color = EmptyStarColor;
                stars[i].rectTransform.localScale = Vector3.one;
            }

            bonusLabel.gameObject.SetActive(hasBonus);
            bonusRow.gameObject.SetActive(hasBonus);
            movesText.text = GameLocalization.MovesCaption + " " + result.MovesLeft;
            bonusText.text = "+0";

            coinsRoot.gameObject.SetActive(false);
            xpText.gameObject.SetActive(false);

            bool hasNext = result.Number < LevelDatabase.LevelCount;
            nextButton.gameObject.SetActive(hasNext);
            doubleButton.gameObject.SetActive(!result.CoinsDoubled);
            doubleGlow.gameObject.SetActive(!result.CoinsDoubled);
            SetButtonsInteractable(false);

            coinsText.text = "+" + result.Coins;
            xpText.text = GameLocalization.XpEarned(result.Xp);
            Open();
        }

        private IEnumerator PlaySequence(LevelResult result)
        {
            yield return WaitUnscaled(RevealDelay * 0.4f);

            // Unused moves become points, one by one.
            if (result.MovesLeft > 0)
            {
                float step = Mathf.Min(MaxBonusStep, BonusDuration / result.MovesLeft);
                for (int left = result.MovesLeft - 1; left >= 0; left--)
                {
                    int converted = result.MovesLeft - left;
                    movesText.text = GameLocalization.MovesCaption + " " + left;
                    bonusText.text = "+" + converted * LevelRunController.MoveBonusPoints;
                    GameTween.Punch(bonusText.rectTransform, 0.18f, 0.12f, unscaled: true);
                    Game.Audio?.PlayPlacement();
                    yield return WaitUnscaled(step);
                }

                yield return WaitUnscaled(0.25f);
            }

            // Stars.
            for (int i = 0; i < StarCount; i++)
            {
                if (i < result.Stars)
                {
                    PopStar(stars[i], i);
                    yield return WaitUnscaled(StarStep);
                }
            }

            if (result.Stars >= StarCount)
            {
                Game.Audio?.PlayBoardClear();
                PlayConfetti();
            }

            yield return WaitUnscaled(0.2f);

            coinsRoot.gameObject.SetActive(true);
            coinsRoot.localScale = Vector3.zero;
            GameTween.Scale(coinsRoot, Vector3.one, 0.3f, TweenEase.OutBack, unscaled: true);
            xpText.gameObject.SetActive(true);
            xpText.rectTransform.localScale = Vector3.zero;
            GameTween.Scale(xpText.rectTransform, Vector3.one, 0.3f, TweenEase.OutBack, 0.1f, unscaled: true);

            yield return WaitUnscaled(0.35f);

            sequence = null;
            buttonsLive = true;
            SetButtonsInteractable(true);
        }

        private void PopStar(Image star, int index)
        {
            star.color = LevelArt.StarGold;
            star.rectTransform.localScale = Vector3.zero;
            GameTween.Scale(star.rectTransform, Vector3.one, 0.35f, TweenEase.OutBack, unscaled: true);
            Game.Audio?.PlayLineClear(1, index + 1);
        }

        private void PlayConfetti()
        {
            if (confetti == null)
            {
                return;
            }

            RectTransform layer = (RectTransform)confetti.transform;
            Vector2 origin = layer.InverseTransformPoint(starsRow.TransformPoint(Vector3.zero));
            confetti.Play(origin);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void StopSequence()
        {
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            if (confetti != null)
            {
                confetti.Stop();
            }

            buttonsLive = false;
        }

        private static Color EmptyStarColor => GameTheme.WithAlpha(Color.white, 0.16f);

        private void SetButtonsInteractable(bool value)
        {
            if (nextButton != null)
            {
                nextButton.interactable = value;
            }

            if (menuButton != null)
            {
                menuButton.interactable = value;
            }

            if (doubleButton != null)
            {
                doubleButton.interactable = value;
            }
        }

        /// <summary>Gentle breathing glow behind the rewarded button, like the continue button of Game Over.</summary>
        private void Update()
        {
            if (!IsOpen || !buttonsLive || doubleGlow == null || !doubleGlow.gameObject.activeSelf)
            {
                return;
            }

            float wave = Mathf.Sin(Time.unscaledTime * 4f) * 0.5f + 0.5f;
            Color color = GameTheme.ShopBuy;
            color.a = Mathf.Lerp(0.15f, 0.45f, wave);
            doubleGlow.color = color;
            doubleGlow.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.05f, wave);
        }

        // ---------------------------------------------------------------- content

        private void RefreshTexts()
        {
            LevelResult result = levelRun != null ? levelRun.Result : null;
            if (title == null || result == null)
            {
                return;
            }

            title.text = GameLocalization.LevelCompleteTitle;
            subtitle.text = GameLocalization.LevelTitle(result.Number);
            bonusLabel.text = GameLocalization.MovesBonus;
            xpText.text = GameLocalization.XpEarned(result.Xp);
            StyleButton(menuButton, GameLocalization.Home, GameTheme.ButtonSecondary, GameTheme.TextPrimary);
            StyleButton(nextButton, GameLocalization.NextLevel, GameTheme.Accent, DarkLabel);
            StyleButton(doubleButton, GameLocalization.DoubleCoinsAd, GameTheme.ShopBuy, DarkLabel);
            RefreshDoubleHint();
        }

        private void RefreshDoubleHint()
        {
            Transform hint = doubleButton != null ? doubleButton.transform.Find("Hint") : null;
            TMP_Text text = hint != null ? hint.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                text.text = GameLocalization.DoubleCoinsHint;
                text.color = GameTheme.WithAlpha(DarkLabel, 0.75f);
            }
        }

        // ---------------------------------------------------------------- clicks

        private void HandleNextClicked()
        {
            if (buttonsLive)
            {
                Game?.NextLevel();
            }
        }

        private void HandleMenuClicked()
        {
            if (buttonsLive)
            {
                Game?.OpenMainMenu();
            }
        }

        private void HandleDoubleClicked()
        {
            if (buttonsLive)
            {
                DoubleRequested?.Invoke();
            }
        }

        /// <summary>The ad was watched: the coins on screen double and the button goes away.</summary>
        private void HandleCoinsDoubled()
        {
            LevelResult result = levelRun != null ? levelRun.Result : null;
            if (result == null || !IsOpen)
            {
                return;
            }

            coinsText.text = "+" + result.Coins * 2;
            GameTween.Punch(coinsRoot, 0.3f, 0.35f, unscaled: true);
            doubleButton.gameObject.SetActive(false);
            doubleGlow.gameObject.SetActive(false);
        }
    }
}
