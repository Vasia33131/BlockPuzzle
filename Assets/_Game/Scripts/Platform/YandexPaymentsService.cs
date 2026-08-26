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
    /// <see cref="ShapesPack1ProductId"/> mixes extra figures into the tray.
    /// Rewarded placements stay available — the player opts into those videos for a bonus.
    ///
    /// The shop only ever shows what the catalog returned: <c>purchase.price</c> as the
    /// amount and <c>purchase.currencyImageURL</c> as the icon next to it (1.13.2, 1.13.4).
    /// Grants are written through <see cref="PlayerProgress"/>, which the cloud service
    /// mirrors into the Yandex save, so a consumed purchase survives another device (1.13.3).
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

        private static readonly string[] AllProductIds =
        {
            NoAdsProductId,
            OceanThemeProductId,
            CandyThemeProductId,
            ShapesPack1ProductId
        };

        private ShopPanel shopPanel;
        private bool restoreRequested;

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
            if (shopPanel == null)
            {
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

        /// <summary>A product the catalog does not list cannot be sold, so it is not offered.</summary>
        private static void TryBuy(string id)
        {
            if (YG2.PurchaseByID(id) == null)
            {
                return;
            }

            YG2.BuyPayments(id);
        }

        private void HandlePurchaseSuccess(string id)
        {
            if (id == NoAdsProductId)
            {
                YG2.ConsumePurchaseByID(id);
                GrantNoAds();
                return;
            }

            if (IsThemeProduct(id))
            {
                YG2.ConsumePurchaseByID(id);
                GrantTheme(id);
                return;
            }

            if (IsPackProduct(id))
            {
                YG2.ConsumePurchaseByID(id);
                GrantPack(id);
            }
        }

        private void HandleSdkData()
        {
            RestorePurchases();
            HandlePaymentsReady();
        }

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

        private void HandlePaymentsReady()
        {
            YG2.ConsumePurchases();
            PushCatalogOffers();
            TryGrantFromCatalog();
        }

        private void RestorePurchases()
        {
            if (restoreRequested)
            {
                YG2.ConsumePurchases();
                return;
            }

            restoreRequested = true;
            YG2.ConsumePurchases();
            TryGrantFromCatalog();
        }

        /// <summary>
        /// Unconsumed catalog entries mean the player already paid and delivery never
        /// finished — grant so a reinstall or a failed consume does not take the goods back.
        /// </summary>
        private void TryGrantFromCatalog()
        {
            Purchase noAds = YG2.PurchaseByID(NoAdsProductId);
            if (noAds != null && !noAds.consumed)
            {
                YG2.ConsumePurchaseByID(NoAdsProductId);
                GrantNoAds();
            }
            else if (PlayerProgress.AdsRemoved)
            {
                ApplyAdsRemoved();
            }

            for (int i = 0; i < ThemeProductIds.Length; i++)
            {
                string themeId = ThemeProductIds[i];
                Purchase purchase = YG2.PurchaseByID(themeId);
                if (purchase != null && !purchase.consumed)
                {
                    YG2.ConsumePurchaseByID(themeId);
                    GrantTheme(themeId);
                }
            }

            for (int i = 0; i < PackProductIds.Length; i++)
            {
                string packId = PackProductIds[i];
                Purchase purchase = YG2.PurchaseByID(packId);
                if (purchase != null && !purchase.consumed)
                {
                    YG2.ConsumePurchaseByID(packId);
                    GrantPack(packId);
                }
            }

            GameTheme.ApplyFromProgress();
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
        /// Loads <c>purchase.currencyImageURL</c> into the slot next to the price, the
        /// same way <see cref="PurchaseYG"/> does for its own cards. A mocked currency
        /// on the debug panel therefore changes both the amount and the icon.
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
