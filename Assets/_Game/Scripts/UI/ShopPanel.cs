using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.Managers;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Full-screen shop opened from the HUD shop button or the coin counter. Sections: colour themes
    /// (the free classic palette <see cref="ThemeConfig.DefaultId"/>, the paid <see cref="ThemeConfig.OceanId"/>
    /// and <see cref="ThemeConfig.CandyId"/>), the extra-figure pack <see cref="PlayerProgress.ShapesPack1Id"/>
    /// and the no-ads product. The layout itself lives in <see cref="ShopLayout"/>.
    ///
    /// Prices come from the payments catalog only (Yandex 1.13.2 / 1.13.4): a button shows the catalog
    /// amount with the catalog currency icon, or the whole catalog string while the icon has not loaded.
    /// A product the catalog does not list gets an inactive "Unavailable" button and a log warning; there is
    /// no hand-written amount. Prices in coins show the coin glyph and the number.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const float ShowScale = 0.94f;
        private const string NoAdsProductId = "no_ads";
        private const string LegacyPackPreviewName = "PackPreview";
        private const float CoinConfirmSeconds = 3f;
        private const float PreviewSeconds = 3f;

        // Canvas units of the 1080x1920 reference: only a landscape screen is wide enough for two columns.
        private const float TwoColumnMinWidth = 1500f;
        private const float PortraitMaxWidth = 1000f;
        private const float LandscapeMaxWidth = 1900f;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button hudShopButton;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;

        private ShopViews views;
        private Image cardBackground;
        private Image dim;
        private readonly List<ShopPriceButton> priceButtons = new List<ShopPriceButton>(5);
        private readonly Dictionary<string, string> catalogPrices = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> warnedMissing = new HashSet<string>(StringComparer.Ordinal);
        private bool visible;
        private bool previewing;
        private float previewUntil;
        private float layoutWidth;
        private int restoreSiblingIndex = -1;

        private enum ButtonKind
        {
            /// <summary>Green: pay for it.</summary>
            Buy,

            /// <summary>Muted but tappable: pick an owned theme.</summary>
            Choose,

            /// <summary>Muted with a check mark: already selected or bought.</summary>
            Done,

            /// <summary>Muted and dead: cannot be bought right now.</summary>
            Off
        }

        /// <summary>True while the shop covers the board. Platform code stops GameplayAPI on it.</summary>
        public bool IsOpen => visible;

        /// <summary>Raised when the player taps Buy on the no-ads card. Platform code starts payment.</summary>
        public event Action NoAdsBuyRequested;

        /// <summary>Raised when the player taps Buy on a theme they do not own yet.</summary>
        public event Action<string> ThemeBuyRequested;

        /// <summary>Raised when the player taps Watch on the coins-for-an-ad card. Platform code shows the video.</summary>
        public event Action CoinAdRequested;

        /// <summary>Raised when the player taps Buy on a coin pack. Platform code starts payment.</summary>
        public event Action<string> CoinPackBuyRequested;

        /// <summary>Raised when the player taps Buy on a figure pack they do not own yet.</summary>
        public event Action<string> PackBuyRequested;

        private void Awake()
        {
            ResolveRefs();
            if (hudShopButton != null || gameManager != null)
            {
                Bind(gameManager, hudShopButton);
            }
            else
            {
                SetVisible(false);
            }
        }

        public void Bind(
            GameManager manager,
            Button hudShop,
            CanvasGroup group,
            RectTransform cardRect)
        {
            canvasGroup = group;
            card = cardRect;
            Bind(manager, hudShop);
        }

        public void Bind(GameManager manager, Button hudShop)
        {
            Unbind();
            gameManager = manager;
            hudShopButton = hudShop != null ? hudShop : hudShopButton;
            ResolveRefs();

            if (gameManager != null)
            {
                gameManager.StateChanged += HandleStateChanged;
            }

            Listen(hudShopButton, HandleHudShopClicked);
            GameLocalization.LanguageChanged += HandleLanguageChanged;
            MetaProgress.Changed += HandleMetaChanged;

            RefreshLocalizedTexts();
            SetVisible(false);
            if (gameManager != null)
            {
                HandleStateChanged(gameManager.State);
            }
        }

        /// <summary>
        /// Price of a product exactly as the payments catalog returned it (digits plus
        /// the portal currency). Pass null or empty when the product is missing from the
        /// catalog or the catalog has not arrived yet: its button then reads "Unavailable".
        /// </summary>
        public void SetProductOffer(string productId, string price)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return;
            }

            catalogPrices[productId] = price;
            RefreshPurchaseState();
        }

        /// <summary>
        /// Currency icon slot next to the catalog price on the Buy button.
        /// Platform code loads <c>purchase.currencyImageURL</c> into it.
        /// </summary>
        public Image ResolveCurrencyIcon(string productId)
        {
            ResolveRefs();
            return PriceButtonFor(productId)?.CurrencyIcon;
        }

        public void RefreshPurchaseState()
        {
            ResolveRefs();
            if (views == null)
            {
                return;
            }

            ApplyOffer(views.NoAds.Action, NoAdsProductId, PlayerProgress.AdsRemoved);
            Paint(views.CoinAd.Action, GameLocalization.ShopCoinAdAction, ButtonKind.Buy, showIcon: false, tick: false);
            for (int i = 0; i < views.CoinPacks.Count; i++)
            {
                // A pack the catalog does not list is hidden rather than shown as "Unavailable".
                ShopCoinOfferCard pack = views.CoinPacks[i];
                bool onSale = HasOffer(pack.ProductId);
                pack.Root.SetActive(onSale);
                if (onSale)
                {
                    ApplyOffer(pack.Action, pack.ProductId, false);
                }
            }

            for (int i = 0; i < views.Themes.Count; i++)
            {
                RefreshThemeCard(views.Themes[i]);
            }

            ApplyOffer(views.Pack.Action, views.Pack.Id, PlayerProgress.OwnsPack(views.Pack.Id));
            RefreshCoinBalance();
        }

        private void OnDestroy()
        {
            EndPreview();
            Unbind();
        }

        private void Update()
        {
            if (!visible || views == null)
            {
                return;
            }

            if (previewing && Time.unscaledTime >= previewUntil)
            {
                EndPreview();
            }

            ApplyResponsiveLayout(force: false);

            for (int i = 0; i < views.Themes.Count; i++)
            {
                // Drops a pending "Buy?" confirmation once it timed out.
                ShopThemeCard themeCard = views.Themes[i];
                if (themeCard.CoinConfirmUntil > 0f && Time.unscaledTime > themeCard.CoinConfirmUntil)
                {
                    themeCard.CoinConfirmUntil = 0f;
                    RefreshThemeCard(themeCard);
                }
            }

            // The currency icon arrives some time after the price: swap "49 RUB" for "49 [icon]" then.
            for (int i = 0; i < priceButtons.Count; i++)
            {
                ShopPriceButton priceButton = priceButtons[i];
                if (priceButton.BuyingProductId != null && IconReady(priceButton) != priceButton.IconShown)
                {
                    RefreshPurchaseState();
                    break;
                }
            }
        }

        private void HandleMetaChanged()
        {
            if (visible)
            {
                RefreshPurchaseState();
            }
            else
            {
                RefreshCoinBalance();
            }
        }

        private void RefreshCoinBalance()
        {
            if (views != null && views.CoinBalance != null)
            {
                views.CoinBalance.text = MetaProgress.Coins.ToString();
            }
        }

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            GameLocalization.LanguageChanged -= HandleLanguageChanged;
            MetaProgress.Changed -= HandleMetaChanged;

            hudShopButton?.onClick.RemoveListener(HandleHudShopClicked);
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void HandleHudShopClicked()
        {
            if (gameManager != null && gameManager.State != GameState.Playing)
            {
                return;
            }

            Show();
        }

        /// <summary>
        /// Opens the shop from outside the HUD (the coin counter's "+"), in any state. The panel
        /// rises above every other overlay, such as the main menu, and drops back on close.
        /// </summary>
        public void Open()
        {
            if (visible)
            {
                return;
            }

            if (transform.parent != null)
            {
                restoreSiblingIndex = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
            }

            Show();
        }

        // ------------------------------------------------------------------ taps

        private void HandleNoAdsClicked()
        {
            if (PlayerProgress.AdsRemoved || !HasOffer(NoAdsProductId))
            {
                return;
            }

            NoAdsBuyRequested?.Invoke();
        }

        private void HandleThemeClicked(string themeId)
        {
            if (string.IsNullOrEmpty(themeId))
            {
                return;
            }

            if (PlayerProgress.OwnsTheme(themeId))
            {
                PlayerProgress.SetThemeId(themeId);
                GameTheme.ApplyFromProgress();
                RefreshPurchaseState();
                return;
            }

            if (!HasOffer(themeId))
            {
                return;
            }

            ThemeBuyRequested?.Invoke(themeId);
        }

        private void HandleThemeCoinClicked(ShopThemeCard themeCard)
        {
            if (themeCard == null || PlayerProgress.OwnsTheme(themeCard.Id))
            {
                return;
            }

            int price = GameTheme.Get(themeCard.Id).CoinPrice;
            if (price <= 0 || MetaProgress.Coins < price)
            {
                return;
            }

            // First tap arms, the second one within a few seconds spends the coins.
            if (themeCard.CoinConfirmUntil <= 0f || Time.unscaledTime > themeCard.CoinConfirmUntil)
            {
                themeCard.CoinConfirmUntil = Time.unscaledTime + CoinConfirmSeconds;
                RefreshThemeCard(themeCard);
                return;
            }

            themeCard.CoinConfirmUntil = 0f;
            if (!MetaProgress.TrySpendCoins(price))
            {
                RefreshPurchaseState();
                return;
            }

            PlayerProgress.GrantTheme(themeCard.Id);
            GameTheme.ApplyFromProgress();
            RefreshPurchaseState();
        }

        private void HandleCoinAdClicked() => CoinAdRequested?.Invoke();

        private void HandleCoinPackClicked(string productId)
        {
            if (HasOffer(productId))
            {
                CoinPackBuyRequested?.Invoke(productId);
            }
        }

        private void HandlePackClicked()
        {
            string packId = views != null ? views.Pack.Id : PlayerProgress.ShapesPack1Id;
            if (string.IsNullOrEmpty(packId) || PlayerProgress.OwnsPack(packId) || !HasOffer(packId))
            {
                return;
            }

            PackBuyRequested?.Invoke(packId);
        }

        /// <summary>
        /// Tap on a theme sample: shows that palette on the board behind the shop for a few seconds.
        /// Only while a game is running, because that is the board there is to look at.
        /// </summary>
        private void HandlePreviewClicked(string themeId)
        {
            if (views == null || gameManager == null || gameManager.State != GameState.Playing)
            {
                return;
            }

            previewing = true;
            previewUntil = Time.unscaledTime + PreviewSeconds;
            GameTheme.Preview(themeId);
            SetPreviewChrome(true);
        }

        private void EndPreview()
        {
            if (!previewing)
            {
                return;
            }

            previewing = false;
            GameTheme.ApplyFromProgress();
            SetPreviewChrome(false);
        }

        /// <summary>Hides the shop screen (and its dim) so the board shows through, or brings it back.</summary>
        private void SetPreviewChrome(bool preview)
        {
            if (views == null)
            {
                return;
            }

            views.RootGroup.alpha = preview ? 0f : 1f;
            views.RootGroup.blocksRaycasts = !preview;
            if (cardBackground != null)
            {
                cardBackground.enabled = !preview;
            }

            if (dim != null)
            {
                dim.enabled = !preview;
            }

            views.PreviewCatcher.gameObject.SetActive(preview);
        }

        // ------------------------------------------------------------------ texts

        private void HandleLanguageChanged() => RefreshLocalizedTexts();

        private void RefreshLocalizedTexts()
        {
            ResolveRefs();
            if (views == null)
            {
                return;
            }

            UIFactory.SetText(views.Title, GameLocalization.ShopTitle);
            UIFactory.SetText(views.CoinsHeader, GameLocalization.ShopCoinsTitle);
            UIFactory.SetText(views.CoinsHint, GameLocalization.ShopCoinsHint);
            UIFactory.SetText(views.CoinAd.Caption, GameLocalization.ShopCoinAdCaption);
            for (int i = 0; i < views.CoinPacks.Count; i++)
            {
                ShopCoinOfferCard pack = views.CoinPacks[i];
                UIFactory.SetText(pack.Caption, GameLocalization.ShopCoinPackCaption(pack.ProductId));
            }

            UIFactory.SetText(views.ThemesHeader, GameLocalization.ShopThemesTitle);
            UIFactory.SetText(views.ThemesHint, GameLocalization.ShopThemesHint);
            UIFactory.SetText(views.PackHeader, GameLocalization.ShopPackTitle);
            UIFactory.SetText(views.NoAdsHeader, GameLocalization.ShopNoAdsTitle);
            UIFactory.SetText(views.NoAdsHint, GameLocalization.ShopNoAdsHint);
            UIFactory.SetText(views.NoAds.Title, GameLocalization.NoAds);
            UIFactory.SetText(views.NoAds.Caption, GameLocalization.ShopNoAdsCaption);
            UIFactory.SetText(views.Pack.Hint, GameLocalization.ShopPackHint);
            UIFactory.SetText(views.PreviewHint, GameLocalization.ThemePreviewHint);

            for (int i = 0; i < views.Themes.Count; i++)
            {
                ShopThemeCard themeCard = views.Themes[i];
                UIFactory.SetText(themeCard.Title, GameLocalization.ThemeName(themeCard.Id));
                UIFactory.SetText(themeCard.Caption, GameLocalization.ThemeTagline(themeCard.Id));
                UIFactory.SetText(themeCard.TryOn, GameLocalization.ShopTryOn);
            }

            RefreshPurchaseState();
        }

        // ------------------------------------------------------------------ show / hide

        private void HandleStateChanged(GameState state)
        {
            if (hudShopButton != null)
            {
                hudShopButton.interactable = state == GameState.Playing;
            }

            if (state != GameState.Playing)
            {
                Hide();
            }
        }

        private void Show()
        {
            ResolveRefs();
            EndPreview();
            ApplyResponsiveLayout(force: true);
            RefreshPurchaseState();
            WarnMissingOffers();
            visible = true;
            MusicManager.SetShopOpen(true);

            if (views != null)
            {
                views.Scroll.StopMovement();
                views.Scroll.verticalNormalizedPosition = 1f;
            }

            if (canvasGroup == null)
            {
                gameObject.SetActive(true);
                return;
            }

            GameTween.Kill(canvasGroup);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            SfxHub.Play(SfxId.UiOpen);
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            if (views != null)
            {
                GameTween.Kill(views.Root);
                views.Root.localScale = Vector3.one * ShowScale;
                GameTween.Scale(views.Root, Vector3.one, ShowDuration, TweenEase.OutQuad, unscaled: true);
            }
        }

        private void Hide()
        {
            if (restoreSiblingIndex >= 0)
            {
                transform.SetSiblingIndex(restoreSiblingIndex);
                restoreSiblingIndex = -1;
            }

            ResolveRefs();
            EndPreview();
            if (!visible && (canvasGroup == null || canvasGroup.alpha <= 0f))
            {
                return;
            }

            visible = false;
            MusicManager.SetShopOpen(false);

            if (canvasGroup == null)
            {
                gameObject.SetActive(false);
                return;
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (canvasGroup.alpha <= 0f)
            {
                return;
            }

            GameTween.Kill(canvasGroup);
            SfxHub.Play(SfxId.UiClose);
            GameTween.Fade(canvasGroup, 0f, HideDuration, TweenEase.InQuad, unscaled: true);

            if (views != null)
            {
                GameTween.Kill(views.Root);
                GameTween.Scale(views.Root, Vector3.one * ShowScale, HideDuration, TweenEase.InQuad, unscaled: true);
            }
        }

        private void SetVisible(bool isVisible)
        {
            visible = isVisible;
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            ResolveRefs();

            if (!isVisible)
            {
                EndPreview();
            }

            if (views != null)
            {
                views.Root.localScale = isVisible ? Vector3.one : Vector3.one * ShowScale;
            }

            if (canvasGroup == null)
            {
                gameObject.SetActive(isVisible);
                return;
            }

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = isVisible;
            canvasGroup.interactable = isVisible;
        }

        // ------------------------------------------------------------------ layout

        private void ResolveRefs()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (card == null)
            {
                card = transform.Find("Card") as RectTransform;
            }

            if (hudShopButton == null)
            {
                Transform top = GameObject.Find("TopPanel")?.transform;
                hudShopButton = top != null ? top.Find("ShopButton")?.GetComponent<Button>() : null;
            }

            EnsureLayout();
        }

        /// <summary>
        /// Builds the screen inside the card on first use. Whatever an older baked prefab or scene put there
        /// (fixed cards, the pack popup) is dropped, so every build ends up with the same layout.
        /// </summary>
        private void EnsureLayout()
        {
            if (views != null || card == null || !Application.isPlaying)
            {
                return;
            }

            Transform legacyPreview = transform.Find(LegacyPackPreviewName);
            if (legacyPreview != null)
            {
                legacyPreview.SetParent(null, false);
                Destroy(legacyPreview.gameObject);
            }

            for (int i = card.childCount - 1; i >= 0; i--)
            {
                // Detach first: Destroy is deferred and the names would still be found.
                Transform child = card.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            UIFactory.Stretch(card);
            card.localScale = Vector3.one;
            cardBackground = card.GetComponent<Image>();
            if (cardBackground != null)
            {
                cardBackground.sprite = null;
                cardBackground.type = Image.Type.Simple;
                cardBackground.color = GameTheme.CardBackground;
                cardBackground.raycastTarget = true;
            }

            dim = transform.Find("Dim")?.GetComponent<Image>();
            views = ShopLayout.Build(card);
            WireViews();
        }

        private void WireViews()
        {
            views.Close.onClick.AddListener(Hide);
            views.PreviewCatcher.onClick.AddListener(EndPreview);
            views.NoAds.Action.Button.onClick.AddListener(HandleNoAdsClicked);
            views.Pack.Action.Button.onClick.AddListener(HandlePackClicked);

            views.CoinAd.Action.Button.onClick.AddListener(HandleCoinAdClicked);

            priceButtons.Clear();
            priceButtons.Add(views.NoAds.Action);
            priceButtons.Add(views.Pack.Action);

            for (int i = 0; i < views.CoinPacks.Count; i++)
            {
                string id = views.CoinPacks[i].ProductId;
                priceButtons.Add(views.CoinPacks[i].Action);
                views.CoinPacks[i].Action.Button.onClick.AddListener(() => HandleCoinPackClicked(id));
            }

            for (int i = 0; i < views.Themes.Count; i++)
            {
                ShopThemeCard themeCard = views.Themes[i];
                string id = themeCard.Id;
                priceButtons.Add(themeCard.Action);
                themeCard.Action.Button.onClick.AddListener(() => HandleThemeClicked(id));
                themeCard.Preview.onClick.AddListener(() => HandlePreviewClicked(id));
                if (themeCard.Coin != null)
                {
                    themeCard.Coin.Button.onClick.AddListener(() => HandleThemeCoinClicked(themeCard));
                }
            }
        }

        /// <summary>One column on a phone in portrait, two on a landscape screen; the list stays centred.</summary>
        private void ApplyResponsiveLayout(bool force)
        {
            if (views == null)
            {
                return;
            }

            float width = views.Viewport.rect.width;
            if (width <= 1f && card != null)
            {
                width = card.rect.width;
            }

            if (width <= 1f || (!force && Mathf.Abs(width - layoutWidth) < 1f))
            {
                return;
            }

            layoutWidth = width;
            int columns = width >= TwoColumnMinWidth ? 2 : 1;
            float maxWidth = columns == 2 ? LandscapeMaxWidth : PortraitMaxWidth;
            float contentWidth = Mathf.Min(width - ShopLayout.SidePadding * 2f, maxWidth);
            int side = Mathf.RoundToInt(Mathf.Max(ShopLayout.SidePadding, (width - contentWidth) * 0.5f));
            float cellWidth = Mathf.Floor((contentWidth - (columns - 1) * ShopLayout.CardGap) / columns);

            // Room under the list for the sticky banner that sits over the bottom of the canvas.
            int bottom = 36 + Mathf.RoundToInt(GameTheme.ActiveBannerReserve);
            views.ContentLayout.padding = new RectOffset(side, side, 36, bottom);

            SetGrid(views.CoinsGrid, columns, cellWidth, ShopLayout.CoinCardHeight);
            SetGrid(views.ThemesGrid, columns, cellWidth, ShopLayout.ThemeCardHeight);
            SetGrid(views.PackGrid, columns, cellWidth, ShopLayout.PackCardHeight);
            SetGrid(views.NoAdsGrid, columns, cellWidth, ShopLayout.NoAdsCardHeight);
            LayoutRebuilder.MarkLayoutForRebuild(views.Content);
        }

        private static void SetGrid(GridLayoutGroup grid, int columns, float cellWidth, float cellHeight)
        {
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cellWidth, cellHeight);
        }

        // ------------------------------------------------------------------ offers

        /// <summary>True once the payments catalog returned a price for the product.</summary>
        private bool HasOffer(string productId)
        {
            return !string.IsNullOrEmpty(productId)
                && catalogPrices.TryGetValue(productId, out string price)
                && !string.IsNullOrEmpty(price);
        }

        private ShopPriceButton PriceButtonFor(string productId)
        {
            if (views == null || string.IsNullOrEmpty(productId))
            {
                return null;
            }

            if (productId == NoAdsProductId)
            {
                return views.NoAds.Action;
            }

            if (productId == views.Pack.Id)
            {
                return views.Pack.Action;
            }

            for (int i = 0; i < views.CoinPacks.Count; i++)
            {
                if (views.CoinPacks[i].ProductId == productId)
                {
                    return views.CoinPacks[i].Action;
                }
            }

            for (int i = 0; i < views.Themes.Count; i++)
            {
                if (views.Themes[i].Id == productId)
                {
                    return views.Themes[i].Action;
                }
            }

            return null;
        }

        /// <summary>
        /// A paid product not in the catalog is most likely not created in the Yandex Games console:
        /// say so once per product instead of failing quietly.
        /// </summary>
        private void WarnMissingOffers()
        {
            WarnIfMissing(NoAdsProductId, PlayerProgress.AdsRemoved);
            WarnIfMissing(PlayerProgress.ShapesPack1Id, PlayerProgress.OwnsPack(PlayerProgress.ShapesPack1Id));
            for (int i = 0; i < CoinPackCatalog.ProductIds.Length; i++)
            {
                WarnIfMissing(CoinPackCatalog.ProductIds[i], false);
            }
        }

        private void WarnIfMissing(string productId, bool owned)
        {
            if (owned || HasOffer(productId) || !warnedMissing.Add(productId))
            {
                return;
            }

            Debug.LogWarning(
                $"[Block Puzzle] Shop: product '{productId}' is not in the payments catalog, so its button shows " +
                "\"Unavailable\". Check that this id exists in the Yandex Games console (In-game purchases).");
        }

        /// <summary>Real-money product: owned, on sale (catalog price) or unavailable.</summary>
        private void ApplyOffer(ShopPriceButton button, string productId, bool owned)
        {
            if (owned)
            {
                Paint(button, GameLocalization.Purchased, ButtonKind.Done, showIcon: false, tick: true);
                return;
            }

            if (!HasOffer(productId))
            {
                Paint(button, GameLocalization.Unavailable, ButtonKind.Off, showIcon: false, tick: false);
                return;
            }

            // Amount and icon, never "49 RUB" plus an icon. Without the icon the catalog string keeps its currency.
            // The static Montserrat atlas has no narrow no-break space (U+202F), which prices can carry as the thousands separator.
            string full = catalogPrices[productId].Replace('\u202F', '\u00A0');
            bool iconReady = IconReady(button);
            Paint(button, iconReady ? ExtractAmount(full) : full, ButtonKind.Buy, showIcon: iconReady, tick: false);
            button.BuyingProductId = productId;
        }

        private static bool IconReady(ShopPriceButton button)
        {
            return button.CurrencyIcon != null && button.CurrencyIcon.sprite != null && button.CurrencyIcon.enabled;
        }

        /// <summary>The numeric part of a catalog price: "1 490 RUB" becomes "1 490".</summary>
        private static string ExtractAmount(string price)
        {
            int start = -1;
            for (int i = 0; i < price.Length; i++)
            {
                if (char.IsDigit(price[i]))
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
            {
                return price;
            }

            int end = start;
            for (int i = start; i < price.Length; i++)
            {
                char c = price[i];
                if (char.IsDigit(c) || c == '.' || c == ',')
                {
                    end = i + 1;
                }
                else if ((c == ' ' || c == ' ' || c == ' ') && i + 1 < price.Length && char.IsDigit(price[i + 1]))
                {
                    continue;
                }
                else
                {
                    break;
                }
            }

            return price.Substring(start, end - start);
        }

        private static void Paint(ShopPriceButton button, string caption, ButtonKind kind, bool showIcon, bool tick)
        {
            bool buy = kind == ButtonKind.Buy;
            button.BuyingProductId = null;
            button.Label.text = caption ?? string.Empty;
            button.Label.color = buy
                ? GameTheme.ShopBuyLabel
                : (kind == ButtonKind.Off ? GameTheme.TextSecondary : GameTheme.TextPrimary);

            if (button.Background != null)
            {
                button.Background.color = buy ? GameTheme.ShopBuy : GameTheme.ButtonSecondary;
            }

            // The muted look is painted above, so the disabled state must not tint it a second time.
            ColorBlock colors = button.Button.colors;
            colors.disabledColor = Color.white;
            button.Button.colors = colors;
            button.Button.interactable = kind == ButtonKind.Buy || kind == ButtonKind.Choose;

            button.Tick.gameObject.SetActive(tick);
            button.CurrencyIcon.gameObject.SetActive(showIcon);
            button.IconShown = showIcon;
        }

        private void RefreshThemeCard(ShopThemeCard themeCard)
        {
            if (themeCard == null)
            {
                return;
            }

            bool owned = PlayerProgress.OwnsTheme(themeCard.Id);
            bool selected = owned && PlayerProgress.ThemeId == themeCard.Id;

            // A theme sold for coins is never sold for money too: until it is owned only the coin
            // button shows, in the same place where Select appears afterwards.
            bool coinOnly = !owned && GameTheme.Get(themeCard.Id).CoinPrice > 0;
            themeCard.Action.Button.gameObject.SetActive(!coinOnly);

            if (coinOnly)
            {
                themeCard.Action.BuyingProductId = null;
            }
            else if (!owned)
            {
                ApplyOffer(themeCard.Action, themeCard.Id, false);
            }
            else if (selected)
            {
                Paint(themeCard.Action, GameLocalization.Selected, ButtonKind.Done, showIcon: false, tick: true);
            }
            else
            {
                Paint(themeCard.Action, GameLocalization.Select, ButtonKind.Choose, showIcon: false, tick: false);
            }

            themeCard.Frame.color = selected ? ShopLayout.SelectedFrame : ShopLayout.ThemeFrame(themeCard.Id);
            RefreshCoinButton(themeCard, owned);
        }

        /// <summary>Coin offer under the paid one: hidden once owned, dimmed while the player is short.</summary>
        private static void RefreshCoinButton(ShopThemeCard themeCard, bool owned)
        {
            ShopCoinButton coin = themeCard.Coin;
            if (coin == null)
            {
                return;
            }

            int price = GameTheme.Get(themeCard.Id).CoinPrice;
            bool offered = !owned && price > 0;
            coin.Button.gameObject.SetActive(offered);
            if (!offered)
            {
                themeCard.CoinConfirmUntil = 0f;
                return;
            }

            bool affordable = MetaProgress.Coins >= price;
            bool confirming = affordable && themeCard.CoinConfirmUntil > 0f;
            coin.Button.interactable = affordable;

            ColorBlock colors = coin.Button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            coin.Button.colors = colors;

            coin.Label.text = confirming ? GameLocalization.BuyForCoinsConfirm : price.ToString();
            coin.CoinIcon.SetActive(!confirming);
            coin.Background.color = confirming ? GameTheme.ShopBuy : MetaUi.CoinGold;
        }
    }
}
