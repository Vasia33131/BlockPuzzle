using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Login reward popup: the seven days of the cycle, the ones already taken dimmed and
    /// today's highlighted, with one Claim button. It opens when the game is entered (the
    /// save and the server day are known) and at the start of a later run if the day has
    /// changed meanwhile — never over the tutorial, the shop or another overlay. Like the
    /// booster confirm it freezes the tray while it covers the board.
    /// </summary>
    public class DailyRewardPanel : MonoBehaviour
    {
        public const string ObjectName = "DailyRewardPanel";

        private const float ShowDuration = 0.26f;
        private const float HideDuration = 0.18f;
        private const float CardWidth = 900f;
        private const float CardHeight = 1080f;
        private const float TileWidth = 190f;
        private const float TileHeight = 250f;
        private const float TileGap = 16f;
        private const float CheckInterval = 0.5f;

        private readonly List<Tile> tiles = new List<Tile>(MetaProgress.DailyCycleLength);

        private GameManager gameManager;
        private CanvasGroup canvasGroup;
        private RectTransform card;
        private TMP_Text titleLabel;
        private TMP_Text hintLabel;
        private Button claimButton;
        private bool visible;
        private bool pendingOffer;
        private bool figuresBlocked;
        private float nextCheck;
        private float cardFitScale = 1f;

        /// <summary>True while the popup covers the board. Platform code stops GameplayAPI on it.</summary>
        public static bool IsShowing { get; private set; }

        private sealed class DimTap : MonoBehaviour, IPointerClickHandler
        {
            public System.Action Clicked;

            public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
        }

        private sealed class Tile
        {
            public Image Background;
            public TMP_Text Day;
            public RectTransform Rewards;
            public TMP_Text State;
        }

        public static DailyRewardPanel Ensure(RectTransform canvasRect, GameManager manager)
        {
            DailyRewardPanel panel = FindObjectOfType<DailyRewardPanel>(true);
            if (panel == null)
            {
                if (canvasRect == null)
                {
                    return null;
                }

                RectTransform root = UIFactory.CreateRect(ObjectName, canvasRect);
                UIFactory.Stretch(root);
                panel = root.gameObject.AddComponent<DailyRewardPanel>();
                panel.Build();
            }

            panel.Bind(manager);
            return panel;
        }

        public void Bind(GameManager manager)
        {
            Unbind();
            gameManager = manager;
            if (gameManager != null)
            {
                gameManager.StateChanged += HandleStateChanged;
            }

            MetaProgress.Ready += HandleReady;
            GameLocalization.LanguageChanged += RefreshTexts;
            pendingOffer = true;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            Unbind();
            IsShowing = false;
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            MetaProgress.Ready -= HandleReady;
            GameLocalization.LanguageChanged -= RefreshTexts;
            UnblockFigures();
        }

        private void HandleReady() => pendingOffer = true;

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.MainMenu)
            {
                // Every visit to the menu is a chance to offer it (the server day may have changed).
                pendingOffer = true;
            }
            else if (visible)
            {
                // The popup lives on the menu only; a run that starts under it closes it.
                Hide();
            }
        }

        private void Update()
        {
            if (visible || !pendingOffer || Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + CheckInterval;
            if (!MetaProgress.IsReady || !CanCoverBoard())
            {
                return;
            }

            pendingOffer = false;
            if (MetaProgress.CanClaimDaily)
            {
                Show();
            }
        }

        private bool CanCoverBoard()
        {
            if (gameManager == null || gameManager.State != GameState.MainMenu)
            {
                return false;
            }

            ShopPanel shop = FindObjectOfType<ShopPanel>(true);
            if (shop != null && shop.IsOpen)
            {
                return false;
            }

            BoosterConfirmPanel confirm = FindObjectOfType<BoosterConfirmPanel>(true);
            return confirm == null || !confirm.IsOpen;
        }

        private void HandleClaimClicked()
        {
            if (!MetaProgress.TryClaimDaily(out int index, out MetaReward reward))
            {
                Hide();
                return;
            }

            if (index >= 0 && index < tiles.Count)
            {
                GameTween.Punch(tiles[index].Background.transform, 0.2f, 0.3f, unscaled: true);
            }

            MetaToast.Show(GameLocalization.DailyClaimedToast, reward);
            claimButton.interactable = false;
            GameTween.Delay(this, 0.35f, true, Hide);
        }

        /// <summary>
        /// Opens the popup on request (the menu's gift button). Unlike the automatic offer it also
        /// opens when today's reward is already taken; Claim is then disabled and a tap outside closes it.
        /// </summary>
        public void Open()
        {
            if (visible || gameManager == null || gameManager.State != GameState.MainMenu || !CanCoverBoard())
            {
                return;
            }

            pendingOffer = false;
            Show();
        }

        private void HandleDimClicked() => Hide();

        private void Show()
        {
            RefreshTiles();
            RefreshTexts();
            transform.SetAsLastSibling();
            visible = true;
            IsShowing = true;
            claimButton.interactable = MetaProgress.CanClaimDaily;
            BlockFigures();
            UpdateFitScale();

            GameTween.Kill(canvasGroup);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            SfxHub.Play(SfxId.UiOpen);
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            GameTween.Kill(card);
            card.localScale = Vector3.one * (cardFitScale * 0.85f);
            GameTween.Scale(card, Vector3.one * cardFitScale, ShowDuration, TweenEase.OutBack, unscaled: true);
        }

        private void Hide()
        {
            if (!visible)
            {
                return;
            }

            visible = false;
            IsShowing = false;
            UnblockFigures();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            GameTween.Kill(canvasGroup);
            SfxHub.Play(SfxId.UiClose);
            GameTween.Fade(canvasGroup, 0f, HideDuration, TweenEase.InQuad, unscaled: true);
            GameTween.Kill(card);
            GameTween.Scale(card, Vector3.one * (cardFitScale * 0.85f), HideDuration, TweenEase.InQuad, unscaled: true);
        }

        private void SetVisible(bool isVisible)
        {
            visible = isVisible;
            IsShowing = isVisible;
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = isVisible;
            canvasGroup.interactable = isVisible;
        }

        private void BlockFigures()
        {
            if (figuresBlocked)
            {
                return;
            }

            figuresBlocked = true;
            gameManager?.Spawner?.SetInteractable(false);
        }

        private void UnblockFigures()
        {
            if (!figuresBlocked)
            {
                return;
            }

            figuresBlocked = false;
            if (gameManager != null && gameManager.State == GameState.Playing)
            {
                gameManager.Spawner?.SetInteractable(true);
            }
        }

        /// <summary>Shrinks the card on short (landscape) screens so the button stays on screen.</summary>
        private void UpdateFitScale()
        {
            Rect area = ((RectTransform)transform).rect;
            float fit = 1f;
            if (area.height > 1f && area.width > 1f)
            {
                fit = Mathf.Min(1f, (area.height - 60f) / CardHeight, (area.width - 40f) / CardWidth);
            }

            cardFitScale = Mathf.Max(0.5f, fit);
        }

        private void RefreshTexts()
        {
            UIFactory.SetText(titleLabel, GameLocalization.DailyRewardTitle);
            UIFactory.SetText(hintLabel, GameLocalization.DailyRewardHint);
            UIFactory.SetButtonText(claimButton, GameLocalization.Claim);
            if (visible)
            {
                RefreshTiles();
            }
        }

        private void RefreshTiles()
        {
            int today = MetaProgress.DailyOfferIndex;
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];
                bool claimed = i < today;
                bool current = i == today;

                tile.Background.color = current
                    ? GameTheme.WithAlpha(GameTheme.Accent, 0.9f)
                    : GameTheme.WithAlpha(GameTheme.EmptyCell, claimed ? 0.45f : 1f);
                Color text = current ? MetaUi.DarkLabel : GameTheme.TextPrimary;
                tile.Day.text = GameLocalization.DayLabel(i + 1);
                tile.Day.color = claimed ? GameTheme.TextSecondary : text;
                tile.State.text = claimed ? GameLocalization.Claimed : string.Empty;
                tile.State.gameObject.SetActive(claimed);
                tile.Rewards.gameObject.SetActive(!claimed);
                MetaUi.FillRewardRow(
                    tile.Rewards,
                    MetaProgress.DailyReward(i),
                    i == MetaProgress.DailyCycleLength - 1 ? 80f : 104f,
                    30f,
                    text);
            }
        }

        private void Build()
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            Image dim = UIFactory.CreateImage("Dim", transform, new Color(0.03f, 0.03f, 0.08f, 0.78f), false);
            UIFactory.Stretch(dim.rectTransform);

            // A tap outside the card closes it. Not a Button on purpose: ButtonPressAnimator.AttachAll would squash it.
            dim.gameObject.AddComponent<DimTap>().Clicked = HandleDimClicked;

            Image cardImage = UIFactory.CreateImage("Card", transform, GameTheme.CardBackground);
            card = cardImage.rectTransform;
            UIFactory.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));

            TextMeshProUGUI title = UIFactory.CreateText(
                "Title", card, GameLocalization.DailyRewardTitle, 60f, GameTheme.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 36f;
            title.fontSizeMax = 60f;
            UIFactory.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 80f));
            titleLabel = title;

            TextMeshProUGUI hint = UIFactory.CreateText(
                "Hint", card, GameLocalization.DailyRewardHint, 30f, GameTheme.TextSecondary, TextAlignmentOptions.Center);
            hint.enableWordWrapping = true;
            UIFactory.Anchor(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(800f, 90f));
            hintLabel = hint;

            // Days 1–4 on the first row, 5–6 plus the wide day 7 on the second.
            float rowTop = -240f;
            float firstRowWidth = 4f * TileWidth + 3f * TileGap;
            for (int i = 0; i < 4; i++)
            {
                float x = -firstRowWidth * 0.5f + TileWidth * 0.5f + i * (TileWidth + TileGap);
                tiles.Add(CreateTile(i, new Vector2(x, rowTop), new Vector2(TileWidth, TileHeight)));
            }

            float secondTop = rowTop - TileHeight - TileGap;
            float wideWidth = 2f * TileWidth + TileGap;
            for (int i = 4; i < 6; i++)
            {
                float x = -firstRowWidth * 0.5f + TileWidth * 0.5f + (i - 4) * (TileWidth + TileGap);
                tiles.Add(CreateTile(i, new Vector2(x, secondTop), new Vector2(TileWidth, TileHeight)));
            }

            float wideX = firstRowWidth * 0.5f - wideWidth * 0.5f;
            tiles.Add(CreateTile(6, new Vector2(wideX, secondTop), new Vector2(wideWidth, TileHeight)));

            claimButton = UIFactory.CreateButton(
                "ClaimButton", card, GameLocalization.Claim, GameTheme.ShopBuy, GameTheme.ShopBuyLabel, 52f);
            UIFactory.Anchor(
                (RectTransform)claimButton.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 48f),
                new Vector2(640f, 140f));
            claimButton.onClick.AddListener(HandleClaimClicked);

            SetVisible(false);
            card.localScale = Vector3.one * 0.85f;
        }

        private Tile CreateTile(int index, Vector2 topCenter, Vector2 size)
        {
            Image background = UIFactory.CreateImage($"Day{index + 1}", card, GameTheme.EmptyCell);
            background.raycastTarget = false;
            UIFactory.Anchor(background.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), topCenter, size);

            TextMeshProUGUI day = UIFactory.CreateText(
                "Day", background.rectTransform, GameLocalization.DayLabel(index + 1), 32f, GameTheme.TextPrimary,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(size.x - 12f, 44f));

            RectTransform rewards = UIFactory.CreateRect("Rewards", background.rectTransform);
            UIFactory.Anchor(rewards, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(size.x - 12f, size.y - 70f));

            TextMeshProUGUI state = UIFactory.CreateText(
                "State", background.rectTransform, GameLocalization.Claimed, 28f, GameTheme.TextSecondary,
                TextAlignmentOptions.Center, FontStyles.Bold);
            UIFactory.Anchor(state.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(size.x - 12f, 44f));
            state.gameObject.SetActive(false);

            return new Tile { Background = background, Day = day, Rewards = rewards, State = state };
        }
    }
}
