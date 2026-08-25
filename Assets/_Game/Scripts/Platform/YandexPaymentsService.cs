using UnityEngine;
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

        private ShopPanel shopPanel;
        private bool restoreRequested;

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
            PushCatalogPrices();
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

            YG2.BuyPayments(NoAdsProductId);
        }

        private void HandleThemeBuyRequested(string id)
        {
            if (string.IsNullOrEmpty(id) || PlayerProgress.OwnsTheme(id))
            {
                return;
            }

            YG2.BuyPayments(id);
        }

        private void HandlePackBuyRequested(string id)
        {
            if (string.IsNullOrEmpty(id) || PlayerProgress.OwnsPack(id))
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

        private void HandlePaymentsReady()
        {
            YG2.ConsumePurchases();
            PushCatalogPrices();
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

        private void PushCatalogPrices()
        {
            if (shopPanel == null)
            {
                return;
            }

            shopPanel.SetProductPrice(NoAdsProductId, ReadCatalogPrice(NoAdsProductId));
            for (int i = 0; i < ThemeProductIds.Length; i++)
            {
                string themeId = ThemeProductIds[i];
                shopPanel.SetProductPrice(themeId, ReadCatalogPrice(themeId));
            }

            for (int i = 0; i < PackProductIds.Length; i++)
            {
                string packId = PackProductIds[i];
                shopPanel.SetProductPrice(packId, ReadCatalogPrice(packId));
            }
        }

        private static string ReadCatalogPrice(string productId)
        {
            Purchase purchase = YG2.PurchaseByID(productId);
            if (purchase == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(purchase.price))
            {
                return purchase.price;
            }

            if (!string.IsNullOrEmpty(purchase.priceValue))
            {
                return purchase.priceValue;
            }

            return null;
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
