using System.Collections.Generic;
using UnityEngine;
using BlockPuzzle.Core;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Painted art from <c>Resources/UI/Art</c>: the logo, the menu background and the icons. Every
    /// getter returns null when the file is missing, and the callers then fall back to the art they
    /// draw in code, so a build without the pictures still looks complete.
    /// </summary>
    public static class GameArt
    {
        private const string Folder = "UI/Art/";

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Logo lettering in the language the game is in now.</summary>
        public static Sprite Logo => Get(GameLocalization.IsEnglish ? "LogoEn" : "LogoRu");

        public static Sprite MenuBackground => Get("MenuBackground");
        public static Sprite ShopIcon => Get("IconShop");
        public static Sprite Coin => Get("IconCoin");
        public static Sprite Gift => Get("IconGift");
        public static Sprite NoAds => Get("IconNoAds");

        /// <summary>Pointing hand of the first-run tutorial.</summary>
        public static Sprite TutorialHand => Get("TutorialHand");

        /// <summary>
        /// Where the fingertip is on <see cref="TutorialHand"/>, as a share of its width and height from
        /// the bottom-left corner. Measured on the picture: the tip is a third of the way in from the left, just
        /// under the dark outline at the top edge (the texture has a 1 px transparent column on the right, so the
        /// width is a multiple of 4 and the picture can be compressed).
        /// </summary>
        public static readonly Vector2 TutorialHandTip = new Vector2(0.328f, 0.97f);

        /// <summary>Picture of a coin pack in the shop: a small pile, a big pile or a chest.</summary>
        public static Sprite CoinPack(string productId)
        {
            switch (productId)
            {
                case CoinPackCatalog.LargeId:
                    return Get("CoinPackLarge");
                case CoinPackCatalog.MediumId:
                    return Get("CoinPackMedium");
                default:
                    return Get("CoinPackSmall");
            }
        }

        private static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(Folder + name);
            if (sprite == null)
            {
                // Imported as a plain texture: wrap it, like the booster icons do.
                Texture2D texture = Resources.Load<Texture2D>(Folder + name);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f,
                        0,
                        SpriteMeshType.FullRect);
                }
            }

            cache[name] = sprite;
            return sprite;
        }
    }
}
