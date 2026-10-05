using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>Price / state button of a shop offer: [tick] caption [currency icon].</summary>
    internal sealed class ShopPriceButton
    {
        public Button Button;
        public Image Background;
        public TMP_Text Label;
        public Image CurrencyIcon;
        public RectTransform Tick;

        /// <summary>Product this button currently sells for real money; null while it shows a state caption.</summary>
        public string BuyingProductId;

        /// <summary>Whether the currency icon was shown the last time the button was painted.</summary>
        public bool IconShown;
    }

    /// <summary>Gold "[coin] 1500" button that spends the in-game coins.</summary>
    internal sealed class ShopCoinButton
    {
        public Button Button;
        public Image Background;
        public TMP_Text Label;
        public GameObject CoinIcon;
    }

    internal sealed class ShopThemeCard
    {
        public string Id;
        public Image Frame;
        public TMP_Text Title;
        public TMP_Text Caption;
        public Button Preview;
        public TMP_Text TryOn;
        public ShopPriceButton Action;
        public ShopCoinButton Coin;
        public float CoinConfirmUntil;
    }

    internal sealed class ShopPackCard
    {
        public string Id;
        public TMP_Text Hint;
        public ShopPriceButton Action;
    }

    /// <summary>A card of the Coins section: "[coin] +500" with a caption and a Buy (or Watch) button.</summary>
    internal sealed class ShopCoinOfferCard
    {
        /// <summary>Catalog product id; null for the card that pays in a rewarded video.</summary>
        public string ProductId;
        public GameObject Root;
        public TMP_Text Amount;
        public TMP_Text Caption;
        public ShopPriceButton Action;
    }

    internal sealed class ShopNoAdsCard
    {
        public TMP_Text Title;
        public TMP_Text Caption;
        public ShopPriceButton Action;
    }

    /// <summary>Everything ShopPanel needs to drive the screen built by <see cref="ShopLayout"/>.</summary>
    internal sealed class ShopViews
    {
        public RectTransform Root;
        public CanvasGroup RootGroup;
        public RectTransform Viewport;
        public RectTransform Content;
        public VerticalLayoutGroup ContentLayout;
        public ScrollRect Scroll;
        public TMP_Text Title;
        public TMP_Text CoinBalance;
        public Button Close;

        public TMP_Text ThemesHeader;
        public TMP_Text ThemesHint;
        public GridLayoutGroup ThemesGrid;
        public TMP_Text CoinsHeader;
        public TMP_Text CoinsHint;
        public GridLayoutGroup CoinsGrid;
        public TMP_Text PackHeader;
        public TMP_Text PackSectionHint;
        public GridLayoutGroup PackGrid;
        public TMP_Text NoAdsHeader;
        public TMP_Text NoAdsHint;
        public GridLayoutGroup NoAdsGrid;

        public readonly List<ShopThemeCard> Themes = new List<ShopThemeCard>(3);
        public ShopCoinOfferCard CoinAd;
        public readonly List<ShopCoinOfferCard> CoinPacks = new List<ShopCoinOfferCard>(3);
        public ShopPackCard Pack;
        public ShopNoAdsCard NoAds;

        public Button PreviewCatcher;
        public TMP_Text PreviewHint;
    }

    /// <summary>
    /// Builds the full-screen shop: title bar with the coin balance and a 140x140 close button, then a
    /// vertically scrolling list of sections (themes, figure pack, no ads). Sizes are in units of the
    /// 1080x1920 reference: buttons are at least 140 high with captions of 48 and more, section titles 56,
    /// section hints 36. The screen is laid out in code so an old baked prefab and a new one look the same.
    /// </summary>
    internal static class ShopLayout
    {
        public const string RootName = "ShopRoot";

        public const float BarHeight = 180f;
        public const float SidePadding = 32f;
        public const float SectionGap = 44f;
        public const float CardGap = 28f;
        public const float ThemeCardHeight = 400f;
        public const float PackCardHeight = 474f;
        public const float NoAdsCardHeight = 260f;
        public const float CoinCardHeight = 220f;
        public const float ActionWidth = 320f;
        public const float ActionHeight = 140f;

        private const float CardInset = 6f;
        private const float CardPadding = 28f;
        private const float PreviewSize = 270f;
        private const float ThemeTitleFont = 60f;
        private const float ThemeTaglineFont = 36f;
        private const float ThemeActionBottom = 34f;
        private const float CardPictureSize = 160f;
        private const float CoinPictureSize = 150f;
        private const float HeaderFont = 56f;
        private const float HintFont = 36f;
        private const float CardTitleFont = 56f;
        private const float CardCaptionFont = 38f;
        private const float ActionFont = 52f;

        private static readonly Color CardFrame = GameTheme.FromHex("#3a3a5f");
        private static readonly Color CardFill = GameTheme.FromHex("#2a2a4c");
        private static readonly Color SlotFill = GameTheme.FromHex("#202040");

        /// <summary>
        /// Colour index per cell of the 5x5 theme sample; '.' is an empty cell. The middle row is full:
        /// the sample looks like a line the moment before it clears.
        /// </summary>
        private static readonly string[] MiniBoardPattern = { "0.1..", "22.3.", "44445", ".6.57", "..677" };

        private static IReadOnlyList<BlockShape> packShapes;

        /// <summary>Colour of the frame around the theme that is in use.</summary>
        public static Color SelectedFrame => GameTheme.ShopBuy;

        public static ShopViews Build(RectTransform card)
        {
            var views = new ShopViews();
            views.Root = UIFactory.CreateRect(RootName, card);
            UIFactory.Stretch(views.Root);
            views.RootGroup = views.Root.gameObject.AddComponent<CanvasGroup>();

            RectTransform safe = UIFactory.CreateRect("Safe", views.Root);
            UIFactory.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildScroll(views, safe);
            BuildTopBar(views, safe);
            BuildCoinsSection(views);
            BuildThemeSection(views);
            BuildPackSection(views);
            BuildNoAdsSection(views);
            BuildPreviewCatcher(views, card);
            return views;
        }

        // ------------------------------------------------------------------ frame

        private static void BuildTopBar(ShopViews views, RectTransform safe)
        {
            RectTransform bar = UIFactory.CreateRect("TopBar", safe);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, BarHeight);
            bar.anchoredPosition = Vector2.zero;

            // Opaque, so the list scrolls under the bar instead of showing through it.
            Image barBackground = UIFactory.CreateImage("Background", bar, GameTheme.CardBackground, false);
            UIFactory.Stretch(barBackground.rectTransform);
            barBackground.raycastTarget = true;

            Image divider = UIFactory.CreateImage("Divider", bar, new Color(1f, 1f, 1f, 0.1f), false);
            divider.raycastTarget = false;
            divider.rectTransform.anchorMin = new Vector2(0f, 0f);
            divider.rectTransform.anchorMax = new Vector2(1f, 0f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0f);
            divider.rectTransform.sizeDelta = new Vector2(0f, 4f);
            divider.rectTransform.anchoredPosition = Vector2.zero;

            views.Title = UIFactory.CreateHeading(
                "Title", bar, GameLocalization.ShopTitle, 72f, GameTheme.TextPrimary, TextAlignmentOptions.Left);
            UIFactory.Anchor(
                views.Title.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(SidePadding, 0f),
                new Vector2(420f, 110f));
            UIFactory.FitText(views.Title, 0.6f);

            views.CoinBalance = MetaUi.CreateCoinAmount("CoinBalance", bar, ActionFont, MetaUi.CoinGold);
            var balanceRoot = (RectTransform)views.CoinBalance.transform.parent;
            balanceRoot.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            UIFactory.Anchor(
                balanceRoot,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-(SidePadding + 140f + 24f), 0f),
                new Vector2(300f, 80f));

            views.Close = UIFactory.CreateButton(
                "CloseButton", bar, string.Empty, GameTheme.ButtonSecondary, GameTheme.TextPrimary, ActionFont);
            var closeRect = (RectTransform)views.Close.transform;
            UIFactory.Anchor(
                closeRect,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-SidePadding, 0f),
                new Vector2(140f, 140f));
            AddCross(closeRect, 64f, 10f, GameTheme.TextPrimary);
        }

        /// <summary>An X drawn from two bars: the font has no cross glyph.</summary>
        private static void AddCross(RectTransform parent, float length, float thickness, Color color)
        {
            for (int i = 0; i < 2; i++)
            {
                Image bar = UIFactory.CreateImage(i == 0 ? "CrossA" : "CrossB", parent, color);
                bar.raycastTarget = false;
                UIFactory.Anchor(
                    bar.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(length, thickness));
                bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }
        }

        private static void BuildScroll(ShopViews views, RectTransform safe)
        {
            RectTransform viewport = UIFactory.CreateRect("Viewport", safe);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(0f, -BarHeight);
            viewport.gameObject.AddComponent<RectMask2D>();

            // A transparent hit area, so a drag that starts between the cards still scrolls.
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            RectTransform content = UIFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = SectionGap;
            layout.padding = new RectOffset((int)SidePadding, (int)SidePadding, 36, 36);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 60f;

            views.Viewport = viewport;
            views.Content = content;
            views.ContentLayout = layout;
            views.Scroll = scroll;
        }

        private static GridLayoutGroup CreateSection(
            RectTransform content, string name, out TMP_Text header, out TMP_Text hint)
        {
            RectTransform section = UIFactory.CreateRect(name, content);
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;

            header = UIFactory.CreateHeading("Header", section, string.Empty, HeaderFont, GameTheme.TextPrimary, TextAlignmentOptions.Left);
            hint = UIFactory.CreateBody("Hint", section, string.Empty, HintFont, GameTheme.TextSecondary, TextAlignmentOptions.TopLeft);
            hint.enableWordWrapping = true;

            RectTransform cards = UIFactory.CreateRect("Cards", section);
            var grid = cards.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(0, 0, 14, 0);
            grid.spacing = new Vector2(CardGap, CardGap);
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            grid.cellSize = new Vector2(900f, ThemeCardHeight);
            return grid;
        }

        // ------------------------------------------------------------------ sections

        private static void BuildCoinsSection(ShopViews views)
        {
            views.CoinsGrid = CreateSection(views.Content, "CoinsSection", out views.CoinsHeader, out views.CoinsHint);
            views.CoinsGrid.cellSize = new Vector2(900f, CoinCardHeight);

            views.CoinAd = BuildCoinCard(views, null, CoinPackCatalog.AdReward);
            AdChip.Ensure(views.CoinAd.Action.Button);
            for (int i = 0; i < CoinPackCatalog.ProductIds.Length; i++)
            {
                string id = CoinPackCatalog.ProductIds[i];
                views.CoinPacks.Add(BuildCoinCard(views, id, CoinPackCatalog.Amount(id)));
            }
        }

        private static ShopCoinOfferCard BuildCoinCard(ShopViews views, string productId, int amount)
        {
            RectTransform root = CreateCardRoot("Coins_" + (productId ?? "ad"), views.CoinsGrid.transform, out _);
            float textRight = CardPadding + ActionWidth + 20f;
            Sprite picture = productId == null ? GameArt.Coin : GameArt.CoinPack(productId);
            float textLeft = AddCardPicture(root, picture, CoinPictureSize);

            TMP_Text amountLabel = MetaUi.CreateCoinAmount("Amount", root, CardTitleFont, GameTheme.TextPrimary);
            amountLabel.text = "+" + amount;
            var row = (RectTransform)amountLabel.transform.parent;
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            PlaceMiddle(row, textLeft, textRight, 30f, 76f);

            TMP_Text caption = UIFactory.CreateBody(
                "Caption", root, string.Empty, CardCaptionFont, GameTheme.TextSecondary, TextAlignmentOptions.Left);
            PlaceMiddle(caption.rectTransform, textLeft, textRight, -40f, 52f);
            UIFactory.FitText(caption, 0.9f);

            ShopPriceButton action = CreatePriceButton(root, "Action", ActionWidth);
            UIFactory.Anchor(
                (RectTransform)action.Button.transform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-CardPadding, 0f),
                new Vector2(ActionWidth, ActionHeight));

            return new ShopCoinOfferCard
            {
                ProductId = productId,
                Root = root.gameObject,
                Amount = amountLabel,
                Caption = caption,
                Action = action
            };
        }

        private static void BuildThemeSection(ShopViews views)
        {
            views.ThemesGrid = CreateSection(views.Content, "ThemesSection", out views.ThemesHeader, out views.ThemesHint);
            views.ThemesGrid.cellSize = new Vector2(900f, ThemeCardHeight);

            BuildThemeCard(views, ThemeConfig.DefaultId);
            BuildThemeCard(views, ThemeConfig.OceanId);
            BuildThemeCard(views, ThemeConfig.CandyId);
        }

        /// <summary>
        /// A theme card dressed in the theme itself, so the player sees what they would get: the card
        /// has the theme's backdrop (gradient and pattern), the sample board uses its blocks with their
        /// pattern and shows a line about to clear, a line of copy sells the mood, a ribbon marks the
        /// hot offers and "Try on" on the sample puts the theme on the real board for a few seconds.
        /// </summary>
        private static void BuildThemeCard(ShopViews views, string themeId)
        {
            ThemeConfig theme = GameTheme.Get(themeId);
            bool coinOffer = theme.CoinPrice > 0;

            RectTransform root = CreateCardRoot("Theme_" + themeId, views.ThemesGrid.transform, out Image frame);
            DressInTheme(root, theme);

            Button preview = UIFactory.CreateButton(
                "Preview", root, string.Empty, GameTheme.Darken(theme.BackgroundBottom, 0.25f), Color.white, 30f);
            var previewRect = (RectTransform)preview.transform;
            UIFactory.Anchor(
                previewRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(CardPadding, 0f),
                new Vector2(PreviewSize, PreviewSize));
            preview.GetComponentInChildren<TMP_Text>(true).gameObject.SetActive(false);
            BuildMiniBoard(previewRect, theme);
            TMP_Text tryOn = AddTryOnPill(previewRect);

            float textLeft = CardPadding + PreviewSize + 28f;

            TMP_Text title = UIFactory.CreateHeading(
                "Title", root, GameLocalization.ThemeName(themeId), ThemeTitleFont, Color.white, TextAlignmentOptions.Left);
            PlaceTop(title.rectTransform, textLeft, CardPadding, 34f, 76f);
            UIFactory.FitText(title, 0.75f);

            TMP_Text caption = UIFactory.CreateBody(
                "Caption", root, GameLocalization.ThemeTagline(themeId), ThemeTaglineFont,
                new Color(1f, 1f, 1f, 0.82f), TextAlignmentOptions.TopLeft);
            caption.enableWordWrapping = true;
            PlaceTop(caption.rectTransform, textLeft, CardPadding, 112f, 96f);
            UIFactory.FitText(caption, 0.8f);

            AddThemeRibbon(root, themeId, theme);

            ShopPriceButton action = CreatePriceButton(root, "Action", ActionWidth);
            UIFactory.Anchor(
                (RectTransform)action.Button.transform,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-CardPadding, ThemeActionBottom),
                new Vector2(ActionWidth, ActionHeight));

            // Coins only: the coin button takes the place of the action until the theme is bought,
            // then the action comes back there as Select.
            ShopCoinButton coin = null;
            if (coinOffer)
            {
                coin = CreateCoinButton(root, ActionWidth);
                UIFactory.Anchor(
                    (RectTransform)coin.Button.transform,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(-CardPadding, ThemeActionBottom),
                    new Vector2(ActionWidth, ActionHeight));
            }

            views.Themes.Add(new ShopThemeCard
            {
                Id = themeId,
                Frame = frame,
                Title = title,
                Caption = caption,
                Preview = preview,
                TryOn = tryOn,
                Action = action,
                Coin = coin
            });
        }

        private static void BuildPackSection(ShopViews views)
        {
            views.PackGrid = CreateSection(views.Content, "PackSection", out views.PackHeader, out views.PackSectionHint);
            views.PackGrid.cellSize = new Vector2(900f, PackCardHeight);
            views.PackSectionHint.gameObject.SetActive(false);

            // The section header already says "+4 new shapes", so the card opens straight with the figures.
            RectTransform root = CreateCardRoot("Pack", views.PackGrid.transform, out _);

            RectTransform figures = UIFactory.CreateRect("Figures", root);
            UIFactory.Anchor(
                figures,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                new Vector2(800f, 150f));
            PaintPackFigures(figures);

            TMP_Text hint = UIFactory.CreateBody(
                "Hint", root, GameLocalization.ShopPackHint, CardCaptionFont, GameTheme.TextSecondary);
            PlaceTop(hint.rectTransform, CardPadding, CardPadding, 204f, 52f);
            UIFactory.FitText(hint, 0.9f);

            ShopPriceButton action = CreatePriceButton(root, "Action", 620f);
            UIFactory.Anchor(
                (RectTransform)action.Button.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 34f),
                new Vector2(620f, ActionHeight));

            views.Pack = new ShopPackCard
            {
                Id = PlayerProgress.ShapesPack1Id,
                Hint = hint,
                Action = action
            };
        }

        private static void BuildNoAdsSection(ShopViews views)
        {
            views.NoAdsGrid = CreateSection(views.Content, "NoAdsSection", out views.NoAdsHeader, out views.NoAdsHint);
            views.NoAdsGrid.cellSize = new Vector2(900f, NoAdsCardHeight);

            RectTransform root = CreateCardRoot("NoAds", views.NoAdsGrid.transform, out _);
            float textRight = CardPadding + ActionWidth + 20f;
            float textLeft = AddCardPicture(root, GameArt.NoAds, CardPictureSize);

            TMP_Text title = UIFactory.CreateHeading(
                "Title", root, GameLocalization.NoAds, CardTitleFont, GameTheme.TextPrimary, TextAlignmentOptions.Left);
            PlaceMiddle(title.rectTransform, textLeft, textRight, 26f, 76f);
            UIFactory.FitText(title, 0.75f);

            TMP_Text caption = UIFactory.CreateBody(
                "Caption", root, GameLocalization.ShopNoAdsCaption, CardCaptionFont, GameTheme.TextSecondary, TextAlignmentOptions.Left);
            PlaceMiddle(caption.rectTransform, textLeft, textRight, -38f, 52f);
            UIFactory.FitText(caption, 0.9f);

            ShopPriceButton action = CreatePriceButton(root, "Action", ActionWidth);
            UIFactory.Anchor(
                (RectTransform)action.Button.transform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-CardPadding, 0f),
                new Vector2(ActionWidth, ActionHeight));

            views.NoAds = new ShopNoAdsCard { Title = title, Caption = caption, Action = action };
        }

        private static void BuildPreviewCatcher(ShopViews views, RectTransform card)
        {
            Image catcherImage = UIFactory.CreateImage("PreviewCatcher", card, new Color(0f, 0f, 0f, 0f), false);
            UIFactory.Stretch(catcherImage.rectTransform);

            var catcher = catcherImage.gameObject.AddComponent<Button>();
            catcher.targetGraphic = catcherImage;
            catcher.transition = Selectable.Transition.None;

            Image pill = UIFactory.CreateImage("Hint", catcherImage.rectTransform, new Color(0.03f, 0.03f, 0.08f, 0.72f));
            pill.raycastTarget = false;
            UIFactory.Anchor(
                pill.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 90f + GameTheme.ActiveBannerReserve),
                new Vector2(860f, 120f));

            TMP_Text hint = UIFactory.CreateBody(
                "Label", pill.rectTransform, GameLocalization.ThemePreviewHint, 42f, Color.white);
            UIFactory.Stretch(hint.rectTransform, 16f);
            UIFactory.FitText(hint, 0.8f);

            catcherImage.gameObject.SetActive(false);
            views.PreviewCatcher = catcher;
            views.PreviewHint = hint;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>
        /// Picture on the left of a card, vertically centred. Returns where the text column starts:
        /// right of the picture, or at the card padding when there is no picture.
        /// </summary>
        private static float AddCardPicture(RectTransform card, Sprite sprite, float size)
        {
            if (sprite == null)
            {
                return CardPadding;
            }

            Image picture = UIFactory.CreateImage("Picture", card, Color.white, false);
            picture.sprite = sprite;
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            UIFactory.Anchor(
                picture.rectTransform,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(CardPadding, 0f),
                new Vector2(size, size));
            return CardPadding + size + 20f;
        }

        private static RectTransform CreateCardRoot(string name, Transform parent, out Image frame)
        {
            frame = UIFactory.CreateImage(name, parent, CardFrame);
            Image fill = UIFactory.CreateImage("Fill", frame.rectTransform, CardFill);
            UIFactory.Stretch(fill.rectTransform, CardInset);
            return frame.rectTransform;
        }

        /// <summary>Stretches across the card between two side margins, centred on a given offset from the middle.</summary>
        private static void PlaceMiddle(RectTransform rect, float left, float right, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, y - height * 0.5f);
            rect.offsetMax = new Vector2(-right, y + height * 0.5f);
        }

        private static void PlaceTop(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static ShopPriceButton CreatePriceButton(RectTransform parent, string name, float width)
        {
            Button button = UIFactory.CreateButton(
                name, parent, string.Empty, GameTheme.ShopBuy, GameTheme.ShopBuyLabel, ActionFont);
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(width, ActionHeight);

            // "Unavailable" or "Purchased" beside a tick is wider than a price: let the caption shrink to 65% (34)
            // inside the button instead of spilling over its edge.
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            UIFactory.FitText(label, 0.65f);
            label.overflowMode = TextOverflowModes.Overflow;

            RectTransform content = UIFactory.CreateRect("Content", rect);
            UIFactory.Stretch(content, 12f);
            var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            RectTransform tick = CreateTick(content, 44f, GameTheme.TextPrimary);
            tick.gameObject.SetActive(false);

            label.transform.SetParent(content, false);

            Image icon = UIFactory.CreateImage("CurrencyIcon", content, Color.white, rounded: false);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;
            var iconSize = icon.gameObject.AddComponent<LayoutElement>();
            iconSize.minWidth = ActionFont;
            iconSize.preferredWidth = ActionFont;
            iconSize.preferredHeight = ActionFont;
            // Shown only once the platform delivered the catalog icon.
            icon.gameObject.SetActive(false);

            return new ShopPriceButton
            {
                Button = button,
                Background = button.targetGraphic as Image,
                Label = label,
                CurrencyIcon = icon,
                Tick = tick
            };
        }

        private static ShopCoinButton CreateCoinButton(RectTransform parent, float width)
        {
            Button button = UIFactory.CreateButton(
                "CoinBuy", parent, string.Empty, MetaUi.CoinGold, MetaUi.DarkLabel, ActionFont);
            ((RectTransform)button.transform).sizeDelta = new Vector2(width, ActionHeight);
            UIFactory.SetButtonText(button, string.Empty);

            TMP_Text amount = MetaUi.CreateCoinAmount("Price", button.transform, ActionFont, MetaUi.DarkLabel);
            UIFactory.Stretch((RectTransform)amount.transform.parent);
            UIFactory.FitText(amount, 0.85f);

            return new ShopCoinButton
            {
                Button = button,
                Background = button.targetGraphic as Image,
                Label = amount,
                CoinIcon = amount.transform.parent.Find("Coin").gameObject
            };
        }

        /// <summary>A check mark drawn from two bars: the font has no tick glyph.</summary>
        private static RectTransform CreateTick(RectTransform parent, float size, Color color)
        {
            RectTransform tick = UIFactory.CreateRect("Tick", parent);
            tick.sizeDelta = new Vector2(size, size);
            var element = tick.gameObject.AddComponent<LayoutElement>();
            element.minWidth = size;
            element.preferredWidth = size;
            element.preferredHeight = size;

            float unit = size / 40f;
            AddTickBar(tick, "Short", new Vector2(-10f, -4f) * unit, 14f * unit, 6f * unit, -45f, color);
            AddTickBar(tick, "Long", new Vector2(5f, 1f) * unit, 29f * unit, 6f * unit, 45f, color);
            return tick;
        }

        private static void AddTickBar(
            RectTransform parent, string name, Vector2 position, float length, float thickness, float angle, Color color)
        {
            Image bar = UIFactory.CreateImage(name, parent, color);
            bar.raycastTarget = false;
            UIFactory.Anchor(
                bar.rectTransform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                position,
                new Vector2(length, thickness));
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>5x5 sample board in the colours of the theme, its cubes wearing the theme's block pattern.</summary>
        private static void BuildMiniBoard(RectTransform parent, ThemeConfig theme)
        {
            const float cell = 44f;
            const float gap = 6f;
            int size = MiniBoardPattern.Length;
            float start = -(cell + gap) * (size - 1) * 0.5f;
            Color[] palette = theme.BlockPalette;
            Sprite pattern = theme.BlockPattern;

            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < MiniBoardPattern[row].Length; column++)
                {
                    char code = MiniBoardPattern[row][column];
                    bool filled = code != '.';
                    Color color = filled ? palette[(code - '0') % palette.Length] : theme.EmptyCell;
                    Image block = UIFactory.CreateImage($"Cell_{row}_{column}", parent, color);
                    block.raycastTarget = false;
                    UIFactory.Anchor(
                        block.rectTransform,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(start + column * (cell + gap), -(start + row * (cell + gap))),
                        new Vector2(cell, cell));

                    if (filled && pattern != null)
                    {
                        ThemePattern.EnsureRoundedMask(block.gameObject);
                        Image overlay = ThemePattern.EnsureChild(block.transform, ThemePattern.BlockChildName, 0);
                        ThemePattern.ConfigureTiled(overlay, pattern, ThemePattern.BlockOverlayTint, true);
                    }
                }
            }
        }

        /// <summary>The card's fill takes the theme's backdrop: its gradient and, faintly, its pattern.</summary>
        private static void DressInTheme(RectTransform card, ThemeConfig theme)
        {
            Image fill = card.Find("Fill")?.GetComponent<Image>();
            if (fill == null)
            {
                return;
            }

            fill.color = Color.white;
            fill.gameObject.AddComponent<VerticalGradient>().SetColors(
                GameTheme.Lighten(theme.BackgroundTop, 0.08f), theme.BackgroundBottom);

            Sprite pattern = theme.BackgroundPattern;
            if (pattern == null)
            {
                return;
            }

            ThemePattern.EnsureRoundedMask(fill.gameObject);
            Image overlay = ThemePattern.EnsureChild(fill.transform, ThemePattern.BackgroundChildName, 0);
            ThemePattern.ConfigureTiled(overlay, pattern, new Color(1f, 1f, 1f, 0.1f), true);
        }

        /// <summary>"Try on" pill over the bottom of the sample: tapping the sample previews the theme.</summary>
        private static TMP_Text AddTryOnPill(RectTransform preview)
        {
            Image pill = UIFactory.CreateImage("TryOn", preview, new Color(0.03f, 0.03f, 0.1f, 0.72f));
            pill.raycastTarget = false;
            UIFactory.Anchor(
                pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f),
                new Vector2(PreviewSize * 0.78f, 54f));

            TMP_Text label = UIFactory.CreateBody(
                "Label", pill.rectTransform, GameLocalization.ShopTryOn, 32f, Color.white);
            UIFactory.Stretch(label.rectTransform, 6f);
            UIFactory.FitText(label, 0.7f);
            return label;
        }

        /// <summary>Corner ribbon ("HOT", "NEW") in the theme's accent; the classic theme has none.</summary>
        private static void AddThemeRibbon(RectTransform card, string themeId, ThemeConfig theme)
        {
            string text = GameLocalization.ThemeBadge(themeId);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Image ribbon = UIFactory.CreateImage("Ribbon", card, theme.Accent);
            ribbon.raycastTarget = false;
            UIFactory.Anchor(
                ribbon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CardPadding - 14f, 12f),
                new Vector2(170f, 54f));
            ribbon.rectTransform.localEulerAngles = new Vector3(0f, 0f, 6f);

            TMP_Text label = UIFactory.CreateHeading(
                "Label", ribbon.rectTransform, text, 32f, Color.white, TextAlignmentOptions.Center);
            UIFactory.Stretch(label.rectTransform, 4f);
            UIFactory.FitText(label, 0.7f);
            MenuArt.ApplyOutlinedMaterial(label);
        }

        /// <summary>Frame of a theme card that is not in use: the theme's accent, so each card has its own glow.</summary>
        public static Color ThemeFrame(string themeId) => GameTheme.Get(themeId).Accent;

        /// <summary>The four figures of the pack, drawn with blocks in a row of slots.</summary>
        private static void PaintPackFigures(RectTransform parent)
        {
            packShapes ??= ShapeCatalog.CreatePack1Shapes();
            const float slotWidth = 180f;
            const float slotHeight = 150f;
            const float slotGap = 16f;
            const float cell = 40f;
            const float pitch = 44f;

            int count = packShapes.Count;
            float total = count * slotWidth + (count - 1) * slotGap;
            float startX = -total * 0.5f + slotWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                BlockShape shape = packShapes[i];
                Image slot = UIFactory.CreateImage($"Figure_{i}", parent, SlotFill);
                slot.raycastTarget = false;
                UIFactory.Anchor(
                    slot.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (slotWidth + slotGap), 0f),
                    new Vector2(slotWidth, slotHeight));

                IReadOnlyList<Vector2Int> cells = shape.Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    Vector2Int at = cells[c];
                    Image block = UIFactory.CreateImage($"Cell_{at.y}_{at.x}", slot.rectTransform, shape.Color);
                    block.raycastTarget = false;
                    UIFactory.Anchor(
                        block.rectTransform,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(
                            (at.x - (shape.Width - 1) * 0.5f) * pitch,
                            -(at.y - (shape.Height - 1) * 0.5f) * pitch),
                        new Vector2(cell, cell));
                }
            }
        }
    }
}
