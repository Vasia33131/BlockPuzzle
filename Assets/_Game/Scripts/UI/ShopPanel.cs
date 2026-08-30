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
    /// Overlay opened from the HUD shop button. Products: remove ads, the free
    /// classic palette <see cref="ThemeConfig.DefaultId"/>, the two paid palettes
    /// <see cref="ThemeConfig.OceanId"/> and <see cref="ThemeConfig.CandyId"/>,
    /// and the extra-figure pack <see cref="PlayerProgress.ShapesPack1Id"/>.
    ///
    /// Prices come from the payments catalog only (Yandex 1.13.2 / 1.13.4): until the
    /// SDK answers, a product shows no price and cannot be bought. There is no
    /// hand-written amount and no placeholder that could pass for one. No-ads and
    /// theme cards draw the catalog string on the Buy button itself. The figure pack
    /// first opens a preview of the extra shapes; the green CTA on that plaque shows
    /// the price. A leftover Price label is hidden.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const string CurrencyIconName = "CurrencyIcon";
        private const string PackPreviewName = "PackPreview";
        private const float CurrencyIconGap = 8f;
        private const string NoAdsProductId = "no_ads";

        [SerializeField] private GameManager gameManager;
        [SerializeField] private Button hudShopButton;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button backButton;

        private TMP_Text buyLabel;
        private readonly Dictionary<string, string> catalogPrices = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<ThemeCard> themeCards = new List<ThemeCard>(3);
        private ThemeCard packCard;
        private CanvasGroup packPreviewGroup;
        private RectTransform packPreviewCard;
        private RectTransform packPreviewFigures;
        private Button packPreviewBuy;
        private TMP_Text packPreviewBuyLabel;
        private Button packPreviewCancel;
        private bool visible;
        private bool packPreviewVisible;

        /// <summary>True while the shop covers the board. Platform code stops GameplayAPI on it.</summary>
        public bool IsOpen => visible;

        /// <summary>Raised when the player taps Buy on the no-ads card. Platform code starts payment.</summary>
        public event Action NoAdsBuyRequested;

        /// <summary>Raised when the player taps Buy on a theme they do not own yet.</summary>
        public event Action<string> ThemeBuyRequested;

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
            RectTransform cardRect,
            TMP_Text price,
            Button buy,
            Button back)
        {
            canvasGroup = group;
            card = cardRect;
            priceLabel = price;
            buyButton = buy;
            backButton = back;
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

            GameTheme.Changed += HandleThemeChanged;

            Listen(hudShopButton, HandleHudShopClicked);
            Listen(buyButton, HandleBuyClicked);
            Listen(backButton, HandleBackClicked);
            Listen(packPreviewBuy, HandlePackPreviewBuyClicked);
            Listen(packPreviewCancel, HandlePackPreviewCancelClicked);
            BindThemeCards();
            BindPackCard();
            GameLocalization.LanguageChanged += HandleLanguageChanged;

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
        /// catalog or the catalog has not arrived yet: the card then shows no price and
        /// its Buy button stays off.
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
        /// Currency icon slot next to the catalog price on the Buy button, created on
        /// first request. Platform code loads <c>purchase.currencyImageURL</c> into it.
        /// </summary>
        public Image ResolveCurrencyIcon(string productId)
        {
            ResolveRefs();
            return EnsureCurrencyIcon(ActionLabelFor(productId));
        }

        public void RefreshPurchaseState()
        {
            ResolveRefs();
            HideLegacyPrice(priceLabel);

            bool owned = PlayerProgress.AdsRemoved;
            bool sellable = !owned && HasOffer(NoAdsProductId);
            ApplyActionButton(
                buyButton,
                buyLabel,
                !owned,
                sellable,
                owned ? GameLocalization.Purchased : (sellable ? PriceText(NoAdsProductId) : string.Empty),
                showCurrencyIcon: sellable);

            for (int i = 0; i < themeCards.Count; i++)
            {
                RefreshThemeCard(themeCards[i]);
            }

            RefreshPackCard();
            RefreshPackPreview();
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            GameTheme.Changed -= HandleThemeChanged;
            GameLocalization.LanguageChanged -= HandleLanguageChanged;

            hudShopButton?.onClick.RemoveListener(HandleHudShopClicked);
            buyButton?.onClick.RemoveListener(HandleBuyClicked);
            backButton?.onClick.RemoveListener(HandleBackClicked);
            packPreviewBuy?.onClick.RemoveListener(HandlePackPreviewBuyClicked);
            packPreviewCancel?.onClick.RemoveListener(HandlePackPreviewCancelClicked);
            UnbindThemeCards();
            UnbindPackCard();
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

        private void HandleBuyClicked()
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

        private void HandlePackClicked(string packId)
        {
            if (string.IsNullOrEmpty(packId) || PlayerProgress.OwnsPack(packId) || !HasOffer(packId))
            {
                return;
            }

            ShowPackPreview();
        }

        private void HandlePackPreviewBuyClicked()
        {
            string packId = PlayerProgress.ShapesPack1Id;
            if (PlayerProgress.OwnsPack(packId) || !HasOffer(packId))
            {
                return;
            }

            PackBuyRequested?.Invoke(packId);
        }

        private void HandlePackPreviewCancelClicked() => HidePackPreview();

        private void HandleThemeChanged()
        {
            if (packPreviewVisible)
            {
                PaintPackPreviewFigures();
            }
        }

        private void HandleLanguageChanged() => RefreshLocalizedTexts();

        private void RefreshLocalizedTexts()
        {
            ResolveRefs();
            if (card != null)
            {
                UIFactory.SetText(card.Find("Title")?.GetComponent<TMP_Text>(), GameLocalization.ShopTitle);
                Transform noAds = card.Find("NoAdsCard/Title");
                UIFactory.SetText(noAds != null ? noAds.GetComponent<TMP_Text>() : null, GameLocalization.NoAds);
            }

            UIFactory.SetButtonText(backButton, GameLocalization.Back);

            if (packCard != null)
            {
                UIFactory.SetText(packCard.Title, GameLocalization.ShapePack);
            }

            for (int i = 0; i < themeCards.Count; i++)
            {
                ThemeCard themeCard = themeCards[i];
                UIFactory.SetText(themeCard.Title, GameLocalization.ThemeName(themeCard.Id));
            }

            if (packPreviewCard != null)
            {
                UIFactory.SetText(
                    packPreviewCard.Find("Title")?.GetComponent<TMP_Text>(),
                    GameLocalization.PackPreviewTitle);
                UIFactory.SetText(
                    packPreviewCard.Find("Body")?.GetComponent<TMP_Text>(),
                    GameLocalization.PackPreviewBody);
            }

            UIFactory.SetButtonText(packPreviewCancel, GameLocalization.Cancel);
            RefreshPurchaseState();
        }

        private void HandleBackClicked()
        {
            if (packPreviewVisible)
            {
                HidePackPreview();
                return;
            }

            Hide();
        }

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
            HidePackPreview(instant: true);
            RefreshPurchaseState();
            visible = true;

            if (canvasGroup == null)
            {
                gameObject.SetActive(true);
                return;
            }

            GameTween.Kill(canvasGroup);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            GameTween.Fade(canvasGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            if (card != null)
            {
                GameTween.Kill(card);
                card.localScale = Vector3.one * 0.85f;
                GameTween.Scale(card, Vector3.one, ShowDuration, TweenEase.OutBack, unscaled: true);
            }
        }

        private void Hide()
        {
            ResolveRefs();
            HidePackPreview(instant: true);
            if (!visible && (canvasGroup == null || canvasGroup.alpha <= 0f))
            {
                return;
            }

            visible = false;

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
            GameTween.Fade(canvasGroup, 0f, HideDuration, TweenEase.InQuad, unscaled: true);

            if (card != null)
            {
                GameTween.Kill(card);
                GameTween.Scale(card, Vector3.one * 0.85f, HideDuration, TweenEase.InQuad, unscaled: true);
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
                HidePackPreview(instant: true);
            }

            if (card != null)
            {
                card.localScale = isVisible ? Vector3.one : Vector3.one * 0.85f;
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

            if (priceLabel == null && card != null)
            {
                priceLabel = card.Find("NoAdsCard/Price")?.GetComponent<TMP_Text>();
            }

            if (buyButton == null && card != null)
            {
                buyButton = card.Find("NoAdsCard/BuyButton")?.GetComponent<Button>();
            }

            if (backButton == null && card != null)
            {
                backButton = card.Find("BackButton")?.GetComponent<Button>();
            }

            if (hudShopButton == null)
            {
                Transform top = GameObject.Find("TopPanel")?.transform;
                hudShopButton = top != null ? top.Find("ShopButton")?.GetComponent<Button>() : null;
            }

            if (buyLabel == null && buyButton != null)
            {
                buyLabel = buyButton.GetComponentInChildren<TMP_Text>(true);
            }

            EnsurePackPreview();
            HideLegacyPrice(priceLabel);
        }

        private void CollectThemeCards()
        {
            themeCards.Clear();
            if (!AddThemeCard(ThemeConfig.DefaultId, "ThemeClassicCard"))
            {
                AddThemeCard(ThemeConfig.DefaultId, "ThemeDefaultCard");
            }

            AddThemeCard(ThemeConfig.OceanId, "ThemeOceanCard");
            AddThemeCard(ThemeConfig.CandyId, "ThemeCandyCard");
        }

        private bool AddThemeCard(string themeId, string objectName)
        {
            if (card == null)
            {
                return false;
            }

            Transform root = card.Find(objectName);
            if (root == null)
            {
                return false;
            }

            Button action = root.Find("BuyButton")?.GetComponent<Button>();
            TMP_Text leftoverPrice = root.Find("Price")?.GetComponent<TMP_Text>();
            HideLegacyPrice(leftoverPrice);
            themeCards.Add(new ThemeCard
            {
                Id = themeId,
                Root = root as RectTransform,
                Title = root.Find("Title")?.GetComponent<TMP_Text>(),
                ActionButton = action,
                ActionLabel = action != null ? action.GetComponentInChildren<TMP_Text>(true) : null
            });
            return true;
        }

        private void BindThemeCards()
        {
            UnbindThemeCards();
            CollectThemeCards();
            for (int i = 0; i < themeCards.Count; i++)
            {
                ThemeCard themeCard = themeCards[i];
                if (themeCard.ActionButton == null)
                {
                    continue;
                }

                string id = themeCard.Id;
                themeCard.ClickHandler = () => HandleThemeClicked(id);
                themeCard.ActionButton.onClick.AddListener(themeCard.ClickHandler);
            }
        }

        private void UnbindThemeCards()
        {
            for (int i = 0; i < themeCards.Count; i++)
            {
                ThemeCard themeCard = themeCards[i];
                if (themeCard.ActionButton != null && themeCard.ClickHandler != null)
                {
                    themeCard.ActionButton.onClick.RemoveListener(themeCard.ClickHandler);
                }
            }

            themeCards.Clear();
        }

        private void RefreshThemeCard(ThemeCard themeCard)
        {
            if (themeCard == null)
            {
                return;
            }

            bool owned = PlayerProgress.OwnsTheme(themeCard.Id);
            bool selected = owned && PlayerProgress.ThemeId == themeCard.Id;
            bool sellable = !owned && HasOffer(themeCard.Id);

            string caption;
            if (!owned)
            {
                caption = sellable ? PriceText(themeCard.Id) : string.Empty;
            }
            else
            {
                caption = selected ? GameLocalization.Selected : GameLocalization.Select;
            }

            ApplyActionButton(
                themeCard.ActionButton,
                themeCard.ActionLabel,
                !owned,
                owned ? !selected : sellable,
                caption,
                showCurrencyIcon: sellable);
        }

        private void CollectPackCard()
        {
            packCard = null;
            if (card == null)
            {
                return;
            }

            Transform root = card.Find("ShapesPack1Card");
            if (root == null)
            {
                return;
            }

            Button action = root.Find("BuyButton")?.GetComponent<Button>();
            HideLegacyPrice(root.Find("Price")?.GetComponent<TMP_Text>());
            packCard = new ThemeCard
            {
                Id = PlayerProgress.ShapesPack1Id,
                Root = root as RectTransform,
                Title = root.Find("Title")?.GetComponent<TMP_Text>(),
                ActionButton = action,
                ActionLabel = action != null ? action.GetComponentInChildren<TMP_Text>(true) : null
            };
        }

        private void BindPackCard()
        {
            UnbindPackCard();
            CollectPackCard();
            if (packCard == null || packCard.ActionButton == null)
            {
                return;
            }

            string id = packCard.Id;
            packCard.ClickHandler = () => HandlePackClicked(id);
            packCard.ActionButton.onClick.AddListener(packCard.ClickHandler);
        }

        private void UnbindPackCard()
        {
            if (packCard != null && packCard.ActionButton != null && packCard.ClickHandler != null)
            {
                packCard.ActionButton.onClick.RemoveListener(packCard.ClickHandler);
            }

            packCard = null;
        }

        private void RefreshPackCard()
        {
            if (packCard == null)
            {
                return;
            }

            bool owned = PlayerProgress.OwnsPack(packCard.Id);
            bool sellable = !owned && HasOffer(packCard.Id);

            // A pack that the catalog does not list is not on sale at all: hide the card
            // instead of showing an offer the player cannot complete (Yandex 1.13.4).
            if (packCard.Root != null)
            {
                packCard.Root.gameObject.SetActive(owned || sellable);
            }

            ApplyActionButton(
                packCard.ActionButton,
                packCard.ActionLabel,
                !owned,
                sellable,
                owned ? GameLocalization.Purchased : (sellable ? GameLocalization.Buy : string.Empty),
                showCurrencyIcon: false);
        }

        /// <summary>True once the payments catalog returned a price for the product.</summary>
        private bool HasOffer(string productId)
        {
            return !string.IsNullOrEmpty(productId)
                && catalogPrices.TryGetValue(productId, out string price)
                && !string.IsNullOrEmpty(price);
        }

        private string PriceText(string productId)
        {
            return catalogPrices.TryGetValue(productId, out string price) ? price : null;
        }

        /// <summary>
        /// Green CTA. Catalog-priced buttons also get the currency icon; the pack card
        /// stays a plain Buy caption because the price lives on the preview plaque.
        /// Owned / select states stay muted and never show a price.
        /// </summary>
        private void ApplyActionButton(
            Button button,
            TMP_Text label,
            bool buyCta,
            bool interactable,
            string caption,
            bool showCurrencyIcon)
        {
            if (label != null)
            {
                label.text = caption ?? string.Empty;
                label.color = buyCta ? GameTheme.ShopBuyLabel : GameTheme.TextPrimary;
                label.overflowMode = TextOverflowModes.Overflow;
            }

            if (button != null)
            {
                Image background = button.targetGraphic as Image;
                if (background == null)
                {
                    background = button.GetComponent<Image>();
                }

                if (background != null)
                {
                    background.color = buyCta ? GameTheme.ShopBuy : GameTheme.ButtonSecondary;
                }

                ColorBlock colors = button.colors;
                colors.disabledColor = buyCta
                    ? new Color(1f, 1f, 1f, 0.65f)
                    : new Color(1f, 1f, 1f, 0.55f);
                button.colors = colors;
                button.interactable = interactable;
            }

            Image icon = showCurrencyIcon ? EnsureCurrencyIcon(label) : ExistingCurrencyIcon(label);
            if (icon != null)
            {
                icon.gameObject.SetActive(showCurrencyIcon && !string.IsNullOrEmpty(caption));
            }

            if (showCurrencyIcon && !string.IsNullOrEmpty(caption))
            {
                LayoutCurrencyIcon(label);
            }
        }

        private static Image ExistingCurrencyIcon(TMP_Text label)
        {
            if (label == null)
            {
                return null;
            }

            Transform existing = label.transform.Find(CurrencyIconName);
            return existing != null ? existing.GetComponent<Image>() : null;
        }

        private static void HideLegacyPrice(TMP_Text label)
        {
            if (label != null)
            {
                label.gameObject.SetActive(false);
            }
        }

        private TMP_Text ActionLabelFor(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            if (productId == NoAdsProductId)
            {
                return buyLabel;
            }

            for (int i = 0; i < themeCards.Count; i++)
            {
                if (themeCards[i].Id == productId)
                {
                    return themeCards[i].ActionLabel;
                }
            }

            if (productId == PlayerProgress.ShapesPack1Id)
            {
                return packPreviewBuyLabel != null
                    ? packPreviewBuyLabel
                    : packCard != null ? packCard.ActionLabel : null;
            }

            return null;
        }

        private static Image EnsureCurrencyIcon(TMP_Text label)
        {
            if (label == null)
            {
                return null;
            }

            Transform existing = label.transform.Find(CurrencyIconName);
            if (existing != null)
            {
                return existing.GetComponent<Image>();
            }

            Image icon = UIFactory.CreateImage(CurrencyIconName, label.transform, Color.white, rounded: false);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            // Stays off until the platform layer has the texture from currencyImageURL.
            icon.enabled = false;
            LayoutCurrencyIcon(label);
            return icon;
        }

        /// <summary>Keeps the currency icon glued to the right edge of the price text.</summary>
        private static void LayoutCurrencyIcon(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            RectTransform icon = label.transform.Find(CurrencyIconName) as RectTransform;
            if (icon == null)
            {
                return;
            }

            label.ForceMeshUpdate();
            float size = Mathf.Max(18f, label.fontSize);
            float textWidth = label.GetPreferredValues(label.text).x;
            UIFactory.Anchor(
                icon,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(textWidth * 0.5f + CurrencyIconGap, 0f),
                new Vector2(size, size));
        }

        private void RefreshPackPreview()
        {
            EnsurePackPreview();
            if (packPreviewBuy == null)
            {
                return;
            }

            string packId = PlayerProgress.ShapesPack1Id;
            bool owned = PlayerProgress.OwnsPack(packId);
            bool sellable = !owned && HasOffer(packId);
            ApplyActionButton(
                packPreviewBuy,
                packPreviewBuyLabel,
                true,
                sellable,
                sellable ? PriceText(packId) : string.Empty,
                showCurrencyIcon: sellable);

            if (owned)
            {
                HidePackPreview(instant: true);
            }
        }

        private void ShowPackPreview()
        {
            ResolveRefs();
            RefreshPackPreview();
            PaintPackPreviewFigures();
            packPreviewVisible = true;

            if (packPreviewGroup == null)
            {
                return;
            }

            packPreviewGroup.transform.SetAsLastSibling();
            GameTween.Kill(packPreviewGroup);
            packPreviewGroup.blocksRaycasts = true;
            packPreviewGroup.interactable = true;
            GameTween.Fade(packPreviewGroup, 1f, ShowDuration, TweenEase.OutQuad, unscaled: true);

            if (packPreviewCard != null)
            {
                GameTween.Kill(packPreviewCard);
                packPreviewCard.localScale = Vector3.one * 0.85f;
                GameTween.Scale(packPreviewCard, Vector3.one, ShowDuration, TweenEase.OutBack, unscaled: true);
            }
        }

        private void HidePackPreview() => HidePackPreview(instant: false);

        private void HidePackPreview(bool instant)
        {
            if (packPreviewGroup == null)
            {
                packPreviewVisible = false;
                return;
            }

            if (!packPreviewVisible && packPreviewGroup.alpha <= 0f)
            {
                SetPackPreviewVisible(false);
                return;
            }

            packPreviewVisible = false;
            packPreviewGroup.blocksRaycasts = false;
            packPreviewGroup.interactable = false;

            if (instant || packPreviewGroup.alpha <= 0f)
            {
                SetPackPreviewVisible(false);
                return;
            }

            GameTween.Kill(packPreviewGroup);
            GameTween.Fade(packPreviewGroup, 0f, HideDuration, TweenEase.InQuad, unscaled: true);

            if (packPreviewCard != null)
            {
                GameTween.Kill(packPreviewCard);
                GameTween.Scale(packPreviewCard, Vector3.one * 0.85f, HideDuration, TweenEase.InQuad, unscaled: true);
            }
        }

        private void SetPackPreviewVisible(bool isVisible)
        {
            packPreviewVisible = isVisible;
            if (packPreviewCard != null)
            {
                packPreviewCard.localScale = isVisible ? Vector3.one : Vector3.one * 0.85f;
            }

            if (packPreviewGroup == null)
            {
                return;
            }

            GameTween.Kill(packPreviewGroup);
            if (packPreviewCard != null)
            {
                GameTween.Kill(packPreviewCard);
            }

            packPreviewGroup.alpha = isVisible ? 1f : 0f;
            packPreviewGroup.blocksRaycasts = isVisible;
            packPreviewGroup.interactable = isVisible;
        }

        private void EnsurePackPreview()
        {
            if (packPreviewBuy != null && packPreviewGroup != null && packPreviewFigures != null)
            {
                return;
            }

            Transform root = transform.Find(PackPreviewName);
            if (root != null)
            {
                BindPackPreviewRefs(root);
            }

            if (packPreviewBuy == null || packPreviewGroup == null || packPreviewFigures == null)
            {
                if (root != null)
                {
                    DestroyImmediate(root.gameObject);
                }

                root = BuildPackPreview();
                BindPackPreviewRefs(root);
            }

            if (!packPreviewVisible)
            {
                SetPackPreviewVisible(false);
            }

            Listen(packPreviewBuy, HandlePackPreviewBuyClicked);
            Listen(packPreviewCancel, HandlePackPreviewCancelClicked);
        }

        private void BindPackPreviewRefs(Transform root)
        {
            if (root == null)
            {
                return;
            }

            packPreviewGroup = root.GetComponent<CanvasGroup>();
            packPreviewCard = root.Find("Card") as RectTransform;
            packPreviewFigures = packPreviewCard != null
                ? packPreviewCard.Find("Figures") as RectTransform
                : null;
            packPreviewBuy = packPreviewCard != null
                ? packPreviewCard.Find("BuyButton")?.GetComponent<Button>()
                : null;
            packPreviewCancel = packPreviewCard != null
                ? packPreviewCard.Find("CancelButton")?.GetComponent<Button>()
                : null;
            packPreviewBuyLabel = packPreviewBuy != null
                ? packPreviewBuy.GetComponentInChildren<TMP_Text>(true)
                : null;
        }

        private Transform BuildPackPreview()
        {
            RectTransform root = UIFactory.CreateRect(PackPreviewName, transform);
            UIFactory.Stretch(root);
            root.SetAsLastSibling();

            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image dim = UIFactory.CreateImage("Dim", root, new Color(0.03f, 0.03f, 0.08f, 0.72f), rounded: false);
            UIFactory.Stretch(dim.rectTransform);

            Image cardImage = UIFactory.CreateImage("Card", root, GameTheme.CardBackground);
            RectTransform previewCard = cardImage.rectTransform;
            UIFactory.Anchor(
                previewCard,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(720f, 920f));

            TMP_Text title = UIFactory.CreateText(
                "Title",
                previewCard,
                GameLocalization.PackPreviewTitle,
                48f,
                GameTheme.TextPrimary,
                TextAlignmentOptions.Center,
                FontStyles.Bold);
            UIFactory.Anchor(
                title.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -40f),
                new Vector2(640f, 80f));

            TMP_Text body = UIFactory.CreateText(
                "Body",
                previewCard,
                GameLocalization.PackPreviewBody,
                32f,
                GameTheme.TextSecondary,
                TextAlignmentOptions.Center,
                FontStyles.Normal);
            body.enableWordWrapping = true;
            UIFactory.Anchor(
                body.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -128f),
                new Vector2(640f, 80f));

            RectTransform figures = UIFactory.CreateRect("Figures", previewCard);
            UIFactory.Anchor(
                figures,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -330f),
                new Vector2(640f, 240f));

            Button buy = UIFactory.CreateButton(
                "BuyButton",
                previewCard,
                string.Empty,
                GameTheme.ShopBuy,
                GameTheme.ShopBuyLabel,
                48f);
            UIFactory.Anchor(
                (RectTransform)buy.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 190f),
                new Vector2(600f, 120f));

            Button cancel = UIFactory.CreateButton(
                "CancelButton",
                previewCard,
                GameLocalization.Cancel,
                GameTheme.ButtonSecondary,
                GameTheme.TextPrimary,
                38f);
            UIFactory.Anchor(
                (RectTransform)cancel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 44f),
                new Vector2(600f, 110f));

            return root;
        }

        private void PaintPackPreviewFigures()
        {
            if (packPreviewFigures == null)
            {
                return;
            }

            for (int i = packPreviewFigures.childCount - 1; i >= 0; i--)
            {
                Destroy(packPreviewFigures.GetChild(i).gameObject);
            }

            IReadOnlyList<BlockShape> shapes = PackPreviewShapes();
            int count = shapes.Count;
            if (count <= 0)
            {
                return;
            }

            float slotWidth = 148f;
            float slotHeight = 210f;
            float gap = 12f;
            float total = count * slotWidth + (count - 1) * gap;
            float startX = -total * 0.5f + slotWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                Image slot = UIFactory.CreateImage($"Figure_{i}", packPreviewFigures, GameTheme.EmptyCell);
                slot.raycastTarget = false;
                UIFactory.Anchor(
                    slot.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (slotWidth + gap), 0f),
                    new Vector2(slotWidth, slotHeight));
                PaintShapePreview(slot.rectTransform, shapes[i]);
            }
        }

        private static void PaintShapePreview(RectTransform slot, BlockShape shape)
        {
            if (slot == null || shape == null)
            {
                return;
            }

            const float cellSize = 28f;
            const float pitch = 32f;
            var bounds = new Vector2Int(shape.Width, shape.Height);
            IReadOnlyList<Vector2Int> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                Image block = UIFactory.CreateImage($"Cell_{cell.y}_{cell.x}", slot, shape.Color);
                block.raycastTarget = false;
                UIFactory.Anchor(
                    block.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(
                        (cell.x - (bounds.x - 1) * 0.5f) * pitch,
                        -(cell.y - (bounds.y - 1) * 0.5f) * pitch),
                    new Vector2(cellSize, cellSize));
            }
        }

        private static IReadOnlyList<BlockShape> cachedPackPreviewShapes;

        private static IReadOnlyList<BlockShape> PackPreviewShapes()
        {
            return cachedPackPreviewShapes ??= ShapeCatalog.CreatePack1Shapes();
        }

        private sealed class ThemeCard
        {
            public string Id;
            public RectTransform Root;
            public TMP_Text Title;
            public Button ActionButton;
            public TMP_Text ActionLabel;
            public UnityEngine.Events.UnityAction ClickHandler;
        }
    }
}
