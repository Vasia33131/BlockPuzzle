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
    /// Overlay opened from the HUD shop button. Products: remove ads, the two
    /// paid palettes <see cref="ThemeConfig.OceanId"/> and <see cref="ThemeConfig.CandyId"/>,
    /// and the extra-figure pack <see cref="PlayerProgress.ShapesPack1Id"/>.
    /// Buy labels stay "Покупка" until platform code supplies the catalog price from the SDK.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        private const float ShowDuration = 0.24f;
        private const float HideDuration = 0.16f;
        private const string FallbackPriceText = "Покупка";
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
        private readonly List<ThemeCard> themeCards = new List<ThemeCard>(2);
        private ThemeCard packCard;
        private bool visible;

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
        /// Catalog price string from the payments SDK. Empty keeps the "Покупка" placeholder.
        /// Never pass a hand-written ruble amount — only what the SDK already returned.
        /// </summary>
        public void SetCatalogPrice(string price) => SetProductPrice(NoAdsProductId, price);

        public void SetProductPrice(string productId, string price)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return;
            }

            catalogPrices[productId] = price;
            RefreshPurchaseState();
        }

        public void RefreshPurchaseState()
        {
            ResolveRefs();

            bool owned = PlayerProgress.AdsRemoved;
            if (priceLabel != null)
            {
                if (owned)
                {
                    priceLabel.gameObject.SetActive(false);
                }
                else
                {
                    priceLabel.gameObject.SetActive(true);
                    priceLabel.text = PriceText(NoAdsProductId);
                }
            }

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
                buyButton.interactable = !owned;
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
            if (PlayerProgress.AdsRemoved)
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

            ThemeBuyRequested?.Invoke(themeId);
        }

        private void HandlePackClicked(string packId)
        {
            if (string.IsNullOrEmpty(packId) || PlayerProgress.OwnsPack(packId))
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
            AddThemeCard(ThemeConfig.OceanId, "ThemeOceanCard");
            AddThemeCard(ThemeConfig.CandyId, "ThemeCandyCard");
        }

        private void AddThemeCard(string themeId, string objectName)
        {
            if (card == null)
            {
                return;
            }

            Transform root = card.Find(objectName);
            if (root == null)
            {
                return;
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

            if (themeCard.Price != null)
            {
                if (owned)
                {
                    themeCard.Price.gameObject.SetActive(false);
                }
                else
                {
                    themeCard.Price.gameObject.SetActive(true);
                    themeCard.Price.text = PriceText(themeCard.Id);
                }
            }

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
                themeCard.ActionButton.interactable = !owned || !selected;
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

            if (packCard.Price != null)
            {
                if (owned)
                {
                    packCard.Price.gameObject.SetActive(false);
                }
                else
                {
                    packCard.Price.gameObject.SetActive(true);
                    packCard.Price.text = PriceText(packCard.Id);
                }
            }

            if (packCard.ActionLabel != null)
            {
                packCard.ActionLabel.text = owned ? OwnedLabel : BuyLabel;
            }

            if (packCard.ActionButton != null)
            {
                packCard.ActionButton.interactable = !owned;
            }
        }

        private string PriceText(string productId)
        {
            if (catalogPrices.TryGetValue(productId, out string price) && !string.IsNullOrEmpty(price))
            {
                return price;
            }

            return FallbackPriceText;
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
