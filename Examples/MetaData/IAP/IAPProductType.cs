namespace AK.Examples.IAP
{
	/// <summary>
	/// Defines the type of In-App Purchase product.
	/// </summary>
	public enum IAPProductType
	{
		/// <summary>
		/// Can be purchased multiple times (e.g., coins, powerups, boosters).
		/// </summary>
		Consumable = 0,
		
		/// <summary>
		/// Purchased once and permanently owned (e.g., remove ads, unlock theme).
		/// </summary>
		NonConsumable = 1,
		
		/// <summary>
		/// Auto-renewable subscription (e.g., premium membership, VIP pass).
		/// </summary>
		Subscription = 2,
		
		/// <summary>
		/// Non-renewing subscription with fixed duration (e.g., 7-day pass, 30-day pass).
		/// </summary>
		NonRenewingSubscription = 3
	}
}