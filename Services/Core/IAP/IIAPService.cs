using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace AK.Services
{
	/// <summary>
	/// Abstraction over platform In-App Purchase services.
	/// Works with store product IDs as strings, decoupled from any metadata layer.
	/// </summary>
	public interface IIAPService
	{
		/// <summary>
		/// Whether the IAP service has been successfully initialized and is ready for purchases.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Initializes the IAP service with the given product definitions.
		/// Must be called before any other operations.
		/// </summary>
		/// <param name="products">Collection of (productId, productType) tuples to register with the store.</param>
		/// <returns>True if initialization succeeded.</returns>
		UniTask<bool> InitializeAsync(IEnumerable<IAPProductRegistration> products);

		/// <summary>
		/// Initiates a purchase for the given product ID and awaits the result.
		/// </summary>
		/// <param name="productId">The store product ID to purchase.</param>
		/// <returns>The result of the purchase attempt.</returns>
		UniTask<IAPPurchaseResult> PurchaseAsync(string productId);

		/// <summary>
		/// Gets cached product information fetched from the store during initialization.
		/// Returns null if the product is not found or the service is not initialized.
		/// </summary>
		/// <param name="productId">The store product ID.</param>
		/// <returns>Product info or null.</returns>
		IAPProductInfo GetProductInfo(string productId);

		/// <summary>
		/// Gets all registered product infos.
		/// </summary>
		IReadOnlyList<IAPProductInfo> GetAllProducts();

		/// <summary>
		/// Restores previously completed purchases (primarily needed on iOS).
		/// On Android, purchases are restored automatically during initialization.
		/// </summary>
		/// <returns>True if restore completed successfully.</returns>
		UniTask<bool> RestorePurchasesAsync();

		/// <summary>
		/// Checks whether a non-consumable or subscription product is currently owned.
		/// For subscriptions, this checks if the subscription is currently active (not expired).
		/// </summary>
		/// <param name="productId">The store product ID.</param>
		/// <returns>True if the user owns this product (or has an active subscription).</returns>
		bool IsProductOwned(string productId);

		/// <summary>
		/// Checks whether a subscription product is currently active (subscribed and not expired).
		/// Returns false for non-subscription products.
		/// </summary>
		/// <param name="productId">The store product ID.</param>
		/// <returns>True if the user has an active subscription.</returns>
		bool IsSubscribed(string productId);

		/// <summary>
		/// Gets the subscription expiration date for a subscription product.
		/// Returns null for non-subscription products or if not subscribed.
		/// </summary>
		/// <param name="productId">The store product ID.</param>
		/// <returns>UTC expiration date or null.</returns>
		System.DateTime? GetSubscriptionExpirationDate(string productId);
	}

	/// <summary>
	/// Registration data for a single IAP product to be sent to the store.
	/// </summary>
	public struct IAPProductRegistration
	{
		/// <summary>
		/// The store product ID (e.g., "com.company.game.coins_100").
		/// </summary>
		public string ProductId;

		/// <summary>
		/// The type of product (Consumable, NonConsumable, Subscription).
		/// Uses an int mapping to avoid coupling to Unity.Purchasing enums:
		/// 0 = Consumable, 1 = NonConsumable, 2 = Subscription
		/// </summary>
		public int ProductType;

		public IAPProductRegistration(string productId, int productType)
		{
			ProductId = productId;
			ProductType = productType;
		}
	}
}

