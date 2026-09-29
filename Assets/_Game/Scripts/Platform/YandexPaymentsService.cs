using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;
using BlockPuzzle.UI;
using YG;
using YG.Utils.Pay;

namespace BlockPuzzle.Platform
{
    /// <summary>
    /// Yandex Games payments. Products:
    /// <see cref="NoAdsProductId"/> removes sticky and interstitial ads;
    /// <see cref="OceanThemeProductId"/> and <see cref="CandyThemeProductId"/> unlock palettes;
    /// <see cref="ShapesPack1ProductId"/> mixes extra figures into the tray;
    /// the <see cref="CoinPackCatalog"/> packs are consumables that add coins.
    /// Rewarded placements stay available — the player opts into those videos for a bonus.
    ///
    /// The shop only ever shows what the catalog returned: <c>purchase.price</c> as the
    /// amount and <c>purchase.currencyImageURL</c> as the icon next to it (1.13.2, 1.13.4).
    ///
    /// All four products are permanent: they are bought through <c>BP_BuyPermanent</c>
    /// (PermanentPurchase.jslib) and never consumed, so each stays in <c>getPurchases()</c>
    /// for good and ownership is restored from the platform on any device — the plugin's own
    /// <c>BuyPayments</c> would consume them. A permanent product counts as owned when the
    /// catalog lists it unconsumed (<c>consumed == false</c>) or when the save already says so;
    /// the save path keeps players who bought before this change (their purchase was consumed
    /// back then; <see cref="PlayerProgress"/> is mirrored into the Yandex save, 1.13.3).
    ///
    /// Consumable products (<see cref="ConsumableProductIds"/>) go through the plugin as before:
    /// it consumes them first and only then raises <c>onPurchaseSuccess</c>, so the handler
    /// grants and never consumes a second time (that would race and pay twice).
    /// </summary>
    [DefaultExecutionOrder(85)]
    public sealed class YandexPaymentsService : MonoBehaviour
    {
        public const string NoAdsProductId = "no_ads";
        public const string OceanThemeProductId = ThemeConfig.OceanId;
        public const string CandyThemeProductId = ThemeConfig.CandyId;
        public const string ShapesPack1ProductId = PlayerProgress.ShapesPack1Id;

        private static readonly string[] ThemeProductIds =
        {
            OceanThemeProductId,
            CandyThemeProductId
        };

        private static readonly string[] PackProductIds =
        {
            ShapesPack1ProductId
        };

        /// <summary>Spent products (coin packs). Granted once per purchase, consumed by the plugin.</summary>
        private static readonly string[] ConsumableProductIds = CoinPackCatalog.ProductIds;

        /// <summary>
        /// The theme products are no longer on sale (themes cost coins only), but they stay here so a
        /// player who bought one earlier still gets it restored from the platform.
        /// </summary>
        private static readonly string[] AllProductIds =
        {
            NoAdsProductId,
            OceanThemeProductId,
            CandyThemeProductId,
            ShapesPack1ProductId,
            CoinPackCatalog.SmallId,
            CoinPackCatalog.MediumId,
            CoinPackCatalog.LargeId
        };

        private ShopPanel shopPanel;
        private bool pendingConsumablesHandled;
        private float nextBindTime;

        // Loaders live on this always-active object, not on the shop card, so a
        // download is never cut short by the overlay being hidden.
        private readonly Dictionary<string, ImageLoadYG> currencyLoaders =
            new Dictionary<string, ImageLoadYG>(StringComparer.Ordinal);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindObjectOfType<YandexPaymentsService>() != null)
            {
                return;
            }

            var go = new GameObject(nameof(YandexPaymentsService));
            DontDestroyOnLoad(go);
            go.AddComponent<YandexPaymentsService>();
        }

        private void OnEnable()
        {
            YG2.onPurchaseSuccess += HandlePurchaseSuccess;
            YG2.onGetPayments += HandlePaymentsReady;
            YG2.onGetSDKData += HandleSdkData;
            YandexCloudProgressService.Restored += HandleCloudRestored;
            TryBindShop();

            if (YG2.isSDKEnabled)
            {
                HandleSdkData();
            }
            else if (PlayerProgress.AdsRemoved)
            {
                ApplyAdsRemoved();
            }
        }

        private void OnDisable()
        {
            YG2.onPurchaseSuccess -= HandlePurchaseSuccess;
            YG2.onGetPayments -= HandlePaymentsReady;
            YG2.onGetSDKData -= HandleSdkData;
            YandexCloudProgressService.Restored -= HandleCloudRestored;
            UnbindShop();
        }

        private void Update()
        {
            // A scene-wide search is too heavy for every frame while the shop does not exist yet.
            if (shopPanel == null && Time.unscaledTime >= nextBindTime)
            {
                nextBindTime = Time.unscaledTime + 0.5f;
                TryBindShop();
            }
        }

        private void TryBindShop()
        {
            ShopPanel panel = FindObjectOfType<ShopPanel>(true);
            if (panel == null || panel == shopPanel)
            {
                return;
            }

            UnbindShop();
            shopPanel = panel;
            shopPanel.NoAdsBuyRequested += HandleNoAdsBuyRequested;
            shopPanel.ThemeBuyRequested += HandleThemeBuyRequested;
            shopPanel.PackBuyRequested += HandlePackBuyRequested;
            shopPanel.CoinPackBuyRequested += HandleCoinPackBuyRequested;
            PushCatalogOffers();
            shopPanel.RefreshPurchaseState();
        }

        private void UnbindShop()
        {
            if (shopPanel == null)
            {
                return;
            }

            shopPanel.NoAdsBuyRequested -= HandleNoAdsBuyRequested;
            shopPanel.ThemeBuyRequested -= HandleThemeBuyRequested;
            shopPanel.PackBuyRequested -= HandlePackBuyRequested;
            shopPanel.CoinPackBuyRequested -= HandleCoinPackBuyRequested;
            shopPanel = null;
        }

        private void HandleNoAdsBuyRequested()
        {
            if (PlayerProgress.AdsRemoved)
            {
                return;
            }

            TryBuy(NoAdsProductId);
        }

        private void HandleThemeBuyRequested(string id)
        {
            if (string.IsNullOrEmpty(id) || PlayerProgress.OwnsTheme(id))
            {
                return;
            }

            TryBuy(id);
        }

        private void HandlePackBuyRequested(string id)
        {
            if (string.IsNullOrEmpty(id) || PlayerProgress.OwnsPack(id))
            {
                return;
            }

            TryBuy(id);
        }

        private void HandleCoinPackBuyRequested(string id)
        {
            if (IsConsumableProduct(id))
            {
                TryBuy(id);
            }
        }

        /// <summary>A product the catalog does not list cannot be sold, so it is not offered.</summary>
        private static void TryBuy(string id)
        {
            if (YG2.PurchaseByID(id) == null)
            {
                return;
            }

            if (!IsPermanentProduct(id))
            {
                YG2.BuyPayments(id);
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            // Same pause as the plugin's BuyPayments; OnPurchaseSuccess / OnPurchaseFailed lift it.
            YG2.PauseGame(true);
            BP_BuyPermanent(id);
#else
            YG2.BuyPayments(id);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void BP_BuyPermanent(string id);
#endif

        private void HandlePurchaseSuccess(string id)
        {
            if (IsPermanentProduct(id))
            {
                // The plugin marks every success as consumed; a permanent product stays in getPurchases.
                Purchase purchase = YG2.PurchaseByID(id);
                if (purchase != null)
                {
                    purchase.consumed = false;
                }

                GrantPermanent(id);
                GameTheme.ApplyFromProgress();
                return;
            }

            if (IsConsumableProduct(id))
            {
                GrantConsumable(id);
            }
        }

        private void HandleSdkData() => HandlePaymentsReady();

        /// <summary>
        /// The account copy of the purchases is in. Redraw the shop with it before the
        /// player can act on a card (1.13.3).
        /// </summary>
        private void HandleCloudRestored()
        {
            PushCatalogOffers();
            shopPanel?.RefreshPurchaseState();

            if (PlayerProgress.AdsRemoved)
            {
                ApplyAdsRemoved();
            }
        }

        /// <summary>Runs on the save and on the catalog: whichever arrives last sees both.</summary>
        private void HandlePaymentsReady()
        {
            PushCatalogOffers();
            if (!PlayerProgress.ForgetPurchasesOnPlay)
            {
                RestorePermanentFromCatalog();
                DeliverPendingConsumables();
            }
        }

        /// <summary>
        /// Every permanent product the platform still lists (never consumed) is owned — on a new
        /// device, after cleared storage or a lost save. Nothing is consumed here.
        /// </summary>
        private void RestorePermanentFromCatalog()
        {
            for (int i = 0; i < AllProductIds.Length; i++)
            {
                string id = AllProductIds[i];
                Purchase purchase = YG2.PurchaseByID(id);
                if (purchase != null && !purchase.consumed && IsPermanentProduct(id))
                {
                    GrantPermanent(id);
                }
            }

            if (PlayerProgress.AdsRemoved)
            {
                ApplyAdsRemoved();
            }

            GameTheme.ApplyFromProgress();
        }

        /// <summary>
        /// A paid consumable whose delivery never finished (tab closed mid-purchase) is still in
        /// getPurchases: consume it once per launch, and the plugin's success callback grants it.
        /// </summary>
        private void DeliverPendingConsumables()
        {
            if (pendingConsumablesHandled || YG2.purchases == null || YG2.purchases.Length == 0)
            {
                return;
            }

            pendingConsumablesHandled = true;
            for (int i = 0; i < ConsumableProductIds.Length; i++)
            {
                string id = ConsumableProductIds[i];
                Purchase purchase = YG2.PurchaseByID(id);
                if (purchase != null && !purchase.consumed)
                {
                    YG2.ConsumePurchaseByID(id);
                }
            }
        }

        /// <summary>Grants a permanent product; granting one already owned changes nothing.</summary>
        private void GrantPermanent(string id)
        {
            if (id == NoAdsProductId)
            {
                if (!PlayerProgress.AdsRemoved)
                {
                    GrantNoAds();
                }

                return;
            }

            if (IsThemeProduct(id))
            {
                if (!PlayerProgress.OwnsTheme(id))
                {
                    GrantTheme(id);
                }

                return;
            }

            if (IsPackProduct(id) && !PlayerProgress.OwnsPack(id))
            {
                GrantPack(id);
            }
        }

        /// <summary>Coin packs: the plugin already consumed the purchase, so the coins are simply added.</summary>
        private static void GrantConsumable(string id)
        {
            int amount = CoinPackCatalog.Amount(id);
            if (amount <= 0)
            {
                Debug.LogWarning($"[Payments] Consumable '{id}' has no grant.");
                return;
            }

            MetaProgress.AddCoins(amount);
        }

        private static bool IsPermanentProduct(string id) => Contains(AllProductIds, id) && !IsConsumableProduct(id);

        private static bool IsConsumableProduct(string id) => Contains(ConsumableProductIds, id);

        private static bool Contains(string[] ids, string id)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private void GrantNoAds()
        {
            PlayerProgress.SetAdsRemoved(true);
            ApplyAdsRemoved();
        }

        private void GrantTheme(string id)
        {
            PlayerProgress.GrantTheme(id);
            GameTheme.ApplyFromProgress();
            shopPanel?.RefreshPurchaseState();
        }

        private void GrantPack(string id)
        {
            PlayerProgress.GrantPack(id);
            shopPanel?.RefreshPurchaseState();
        }

        private void ApplyAdsRemoved()
        {
            YG2.StickyAdActivity(false);
            shopPanel?.RefreshPurchaseState();
            RefreshBannerLayout();
        }

        /// <summary>
        /// Feeds the shop the catalog price of every product plus the currency icon.
        /// A product missing from the catalog is pushed as no offer at all, which turns
        /// its card off instead of showing an amount the player could not pay.
        /// </summary>
        private void PushCatalogOffers()
        {
            if (shopPanel == null)
            {
                return;
            }

            for (int i = 0; i < AllProductIds.Length; i++)
            {
                string productId = AllProductIds[i];
                Purchase purchase = YG2.PurchaseByID(productId);
                shopPanel.SetProductOffer(productId, ReadCatalogPrice(purchase));
                LoadCurrencyIcon(productId, purchase);
            }
        }

        /// <summary>
        /// Loads <c>purchase.currencyImageURL</c> into the slot next to the price on
        /// the Buy button, the same way <see cref="PurchaseYG"/> does for its own cards.
        /// A mocked currency on the debug panel therefore changes both the amount and the icon.
        /// </summary>
        private void LoadCurrencyIcon(string productId, Purchase purchase)
        {
            string url = purchase != null ? purchase.currencyImageURL : null;
            if (string.IsNullOrEmpty(url) || url == "null")
            {
                return;
            }

            Image icon = shopPanel.ResolveCurrencyIcon(productId);
            if (icon == null)
            {
                return;
            }

            if (!currencyLoaders.TryGetValue(productId, out ImageLoadYG loader) || loader == null)
            {
                loader = gameObject.AddComponent<ImageLoadYG>();
                currencyLoaders[productId] = loader;
            }

            if (loader.spriteImage == icon && loader.urlImage == url)
            {
                return;
            }

            loader.spriteImage = icon;
            loader.urlImage = url;
            loader.Load();
        }

        private static string ReadCatalogPrice(Purchase purchase)
        {
            if (purchase == null)
            {
                return null;
            }

            // price already carries the portal currency; priceValue is the bare amount
            // and is only used when the platform left price empty.
            if (!string.IsNullOrEmpty(purchase.price))
            {
                return purchase.price;
            }

            return string.IsNullOrEmpty(purchase.priceValue) ? null : purchase.priceValue;
        }

        private static bool IsThemeProduct(string id)
        {
            for (int i = 0; i < ThemeProductIds.Length; i++)
            {
                if (ThemeProductIds[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPackProduct(string id)
        {
            for (int i = 0; i < PackProductIds.Length; i++)
            {
                if (PackProductIds[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RefreshBannerLayout()
        {
            UIManager ui = FindObjectOfType<UIManager>();
            ui?.FixLayoutForPC();

            OrientationHandler orientation = FindObjectOfType<OrientationHandler>();
            orientation?.RefreshNow();
        }
    }
}
