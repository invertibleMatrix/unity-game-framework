namespace GameplayCore.MetaData
{
    public enum CostType
    {
        None = 0,
        Free = 1,
        Coin = 2,
        Gem = 3,
        Ad = 4,
        InAppPurchase = 5,
        Resource = 6, // For consuming other inventory items (e.g. keys, tickets)
        Currency = 7
    }
}