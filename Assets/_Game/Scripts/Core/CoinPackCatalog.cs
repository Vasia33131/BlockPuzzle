namespace BlockPuzzle.Core
{
    /// <summary>
    /// What the "Coins" section of the shop sells: a handful of coins for a rewarded video and three
    /// consumable packs for real money. Coins are the only thing sold both ways; everything bought
    /// with coins (themes, boosters) is never sold for money directly.
    /// The product ids must exist in the Yandex Games console as consumable in-game purchases.
    /// </summary>
    public static class CoinPackCatalog
    {
        /// <summary>Coins granted for one rewarded video in the shop.</summary>
        public const int AdReward = 100;

        public const string SmallId = "coins_500";
        public const string MediumId = "coins_1500";
        public const string LargeId = "coins_5000";

        /// <summary>Real-money packs in the order the shop shows them.</summary>
        public static readonly string[] ProductIds = { SmallId, MediumId, LargeId };

        /// <summary>Coins a pack grants; 0 for an id that is not a coin pack.</summary>
        public static int Amount(string productId)
        {
            switch (productId)
            {
                case SmallId:
                    return 500;
                case MediumId:
                    return 1500;
                case LargeId:
                    return 5000;
                default:
                    return 0;
            }
        }
    }
}
