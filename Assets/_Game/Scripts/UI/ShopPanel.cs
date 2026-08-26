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
    /// hand-written amount and no placeholder that could pass for one.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const string CurrencyIconName = "CurrencyIcon";
        private const float CurrencyIconGap = 6f;
        private const string BuyLabel = "Купить";
        private const string OwnedLabel = "Куплено";
        private const string SelectLabel = "Выбрать";
        private const string SelectedLabel = "Выбрано";
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
        private bool visible;

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

            Listen(hudShopButton, HandleHudShopClicked);
            Listen(buyButton, HandleBuyClicked);
            Listen(backButton, HandleBackClicked);
            BindThemeCards();
            BindPackCard();

            RefreshPurchaseState();
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
        /// Currency icon slot next to the product price, created on first request.
        /// Platform code loads <c>purchase.currencyImageURL</c> into it.
        /// </summary>
        public Image ResolveCurrencyIcon(string productId)
        {
            ResolveRefs();
            return EnsureCurrencyIcon(PriceLabelFor(productId));
        }

        public void RefreshPurchaseState()
        {
            ResolveRefs();

            bool owned = PlayerProgress.AdsRemoved;
            bool sellable = !owned && HasOffer(NoAdsProductId);
            ApplyPrice(priceLabel, sellable ? PriceText(NoAdsProductId) : null);

            if (buyLabel == null && buyButton != null)
            {
                buyLabel = buyButton.GetComponentInChildren<TMP_Text>(true);
            }

            if (buyLabel != null)
            {
                buyLabel.text = owned ? OwnedLabel : BuyLabel;
            }

            if (buyButton != null)
            {
                buyButton.interactable = sellable;
            }

            for (int i = 0; i < themeCards.Count; i++)
            {
                RefreshThemeCard(themeCards[i]);
            }

            RefreshPackCard();
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= HandleStateChanged;
                gameManager = null;
            }

            hudShopButton?.onClick.RemoveListener(HandleHudShopClicked);
            buyButton?.onClick.RemoveListener(HandleBuyClicked);
            backButton?.onClick.RemoveListener(HandleBackClicked);
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

            PackBuyRequested?.Invoke(packId);
        }

        private void HandleBackClicked() => Hide();

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
            themeCards.Add(new ThemeCard
            {
                Id = themeId,
                Root = root as RectTransform,
                Price = root.Find("Price")?.GetComponent<TMP_Text>(),
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

            ApplyPrice(themeCard.Price, sellable ? PriceText(themeCard.Id) : null);

            if (themeCard.ActionLabel != null)
            {
                if (!owned)
                {
                    themeCard.ActionLabel.text = BuyLabel;
                }
                else
                {
                    themeCard.ActionLabel.text = selected ? SelectedLabel : SelectLabel;
                }
            }

            if (themeCard.ActionButton != null)
            {
                themeCard.ActionButton.interactable = owned ? !selected : sellable;
            }
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
            packCard = new ThemeCard
            {
                Id = PlayerProgress.ShapesPack1Id,
                Root = root as RectTransform,
                Price = root.Find("Price")?.GetComponent<TMP_Text>(),
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

            ApplyPrice(packCard.Price, sellable ? PriceText(packCard.Id) : null);

            if (packCard.ActionLabel != null)
            {
                packCard.ActionLabel.text = owned ? OwnedLabel : BuyLabel;
            }

            if (packCard.ActionButton != null)
            {
                packCard.ActionButton.interactable = sellable;
            }
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
        /// Shows the catalog price, or hides the whole label — with its currency icon —
        /// when there is nothing legitimate to show.
        /// </summary>
        private static void ApplyPrice(TMP_Text label, string price)
        {
            if (label == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(price))
            {
                label.gameObject.SetActive(false);
                return;
            }

            label.gameObject.SetActive(true);
            label.text = price;
            LayoutCurrencyIcon(label);
        }

        private TMP_Text PriceLabelFor(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            if (productId == NoAdsProductId)
            {
                return priceLabel;
            }

            for (int i = 0; i < themeCards.Count; i++)
            {
                if (themeCards[i].Id == productId)
                {
                    return themeCards[i].Price;
                }
            }

            return packCard != null && packCard.Id == productId ? packCard.Price : null;
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

            float size = label.fontSize;
            float textWidth = label.GetPreferredValues(label.text).x;
            UIFactory.Anchor(
                icon,
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(textWidth * 0.5f + CurrencyIconGap, 0f),
                new Vector2(size, size));
        }

        private sealed class ThemeCard
        {
            public string Id;
            public RectTransform Root;
            public TMP_Text Price;
            public Button ActionButton;
            public TMP_Text ActionLabel;
            public UnityEngine.Events.UnityAction ClickHandler;
        }
    }
}
