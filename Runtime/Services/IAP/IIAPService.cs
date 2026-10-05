using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace AK.Services
{
	/// <summary>
	/// A platform store: its products, its purchases and the paid orders it delivers. Works with
	/// store product IDs as strings, decoupled from any metadata layer.
	///
	/// Every paid order goes to the order handler (<see cref="SetOrderHandler"/>), whichever way it
	/// arrives: bought with <see cref="PurchaseAsync"/>, restored, approved after a deferral, bought
	/// from the store's own page, or left unfinished by an earlier session. The store finishes an
	/// order (Unity IAP's ConfirmPurchase) only once the handler reports it
	/// <see cref="OrderFulfilment.Fulfilled"/>. Until then it stays unfinished, and the store
	/// delivers it again at the next launch, so a crash can't lose a paid order.
	/// </summary>
	public interface IIAPService
	{
		/// <summary>
		/// Whether the IAP service has been successfully initialized and is ready for purchases.
		/// </summary>
		bool IsInitialized { get; }

		/// <summary>
		/// Sets who fulfils paid orders. Orders that arrived before a handler was set go to it now.
		/// </summary>
		void SetOrderHandler(IStoreOrderHandler handler);

		/// <summary>
		/// Connects to the store and fetches the given products, then the purchases the store holds.
		/// Must be called before any other operations. Unfinished orders reach the handler from here on.
		/// </summary>
		/// <returns>True if initialization succeeded. A failed initialization can be tried again.</returns>
		UniTask<bool> InitializeAsync(IEnumerable<IAPProductRegistration> products, CancellationToken ct = default);

		/// <summary>
		/// Buys a product. Completes once the store has delivered the paid order and the handler has
		/// had it, with Success; the handler reports the fulfilment. Otherwise with the store's
		/// failure: <see cref="IAPFailureType.Deferred"/> when the store holds the purchase for
		/// approval, and <see cref="IAPFailureType.Timeout"/> when it didn't answer in time. Either
		/// purchase can still complete, and its order then goes to the handler.
		/// </summary>
		UniTask<IAPPurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default);

		/// <summary>
		/// Hands the orders still unfinished in this session to the handler again: ones it couldn't
		/// fulfil yet, and ones the store failed to finish. Rejected orders aren't retried.
		/// </summary>
		UniTask RetryUnfinishedOrdersAsync(CancellationToken ct = default);

		/// <summary>
		/// Gets cached product information fetched from the store during initialization.
		/// Returns null if the product is not found or the service is not initialized.
		/// </summary>
		IAPProductInfo GetProductInfo(string productId);

		/// <summary>
		/// Gets all registered product infos.
		/// </summary>
		IReadOnlyList<IAPProductInfo> GetAllProducts();

		/// <summary>
		/// Restores previously completed purchases (primarily needed on iOS) and refreshes what is
		/// owned. Non-consumables and subscriptions are entitlements: check
		/// <see cref="IsProductOwned"/> and <see cref="IsSubscribed"/>.
		/// On Android, purchases are restored automatically during initialization.
		/// </summary>
		/// <returns>True if restore completed successfully.</returns>
		UniTask<bool> RestorePurchasesAsync(CancellationToken ct = default);

		/// <summary>
		/// Checks whether a non-consumable or subscription product is currently owned.
		/// For subscriptions, this checks if the subscription is currently active (not expired).
		/// </summary>
		bool IsProductOwned(string productId);

		/// <summary>
		/// Checks whether a subscription product is currently active (subscribed and not expired).
		/// Returns false for non-subscription products.
		/// </summary>
		bool IsSubscribed(string productId);

		/// <summary>
		/// Gets the subscription expiration date for a subscription product.
		/// Returns null for non-subscription products or if not subscribed.
		/// </summary>
		/// <returns>UTC expiration date or null.</returns>
		DateTime? GetSubscriptionExpirationDate(string productId);
	}

	/// <summary>What kind of product a store sells.</summary>
	public enum IAPProductType
	{
		/// <summary>Bought again and again, such as a pack of coins.</summary>
		Consumable = 0,

		/// <summary>Bought once and owned for good, such as removing ads.</summary>
		NonConsumable = 1,

		/// <summary>Owned while it renews.</summary>
		Subscription = 2,
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

		public IAPProductType ProductType;

		public IAPProductRegistration(string productId, IAPProductType productType)
		{
			ProductId   = productId;
			ProductType = productType;
		}
	}
}
