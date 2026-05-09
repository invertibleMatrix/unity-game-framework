using System;

namespace AK.Services
{
	/// <summary>
	/// Lightweight product information decoupled from Unity IAP types.
	/// Provides store-fetched metadata for display in the UI.
	/// </summary>
	public class IAPProductInfo
	{
		/// <summary>
		/// The store product ID (e.g., "com.company.game.coins_100").
		/// </summary>
		public string ProductId { get; set; }

		/// <summary>
		/// Localized price string from the store (e.g., "$0.99", "€0,99").
		/// </summary>
		public string LocalizedPrice { get; set; }

		/// <summary>
		/// Localized product title from the store.
		/// </summary>
		public string LocalizedTitle { get; set; }

		/// <summary>
		/// Localized product description from the store.
		/// </summary>
		public string LocalizedDescription { get; set; }

		/// <summary>
		/// Numeric price in the local currency (e.g., 0.99m).
		/// </summary>
		public decimal Price { get; set; }

		/// <summary>
		/// ISO 4217 currency code (e.g., "USD", "EUR").
		/// </summary>
		public string IsoCurrencyCode { get; set; }

		/// <summary>
		/// Whether this product is currently available for purchase on the store.
		/// </summary>
		public bool AvailableToPurchase { get; set; }

		/// <summary>
		/// Whether this product has an active receipt (owned / not yet consumed).
		/// For subscriptions, prefer using IsSubscribed property.
		/// </summary>
		public bool HasReceipt { get; set; }

		// ─────────────────────────────────────────────
		// Subscription-Specific Properties
		// ─────────────────────────────────────────────

		/// <summary>
		/// Whether this product is a subscription type.
		/// </summary>
		public bool IsSubscription { get; set; }

		/// <summary>
		/// Whether the user is currently subscribed to this product.
		/// Only valid for subscription products. Returns false for non-subscriptions.
		/// This is determined by checking if the subscription has not expired.
		/// </summary>
		public bool IsSubscribed { get; set; }

		/// <summary>
		/// UTC expiration date of the subscription.
		/// Only valid for subscription products. Null for non-subscriptions or if not subscribed.
		/// </summary>
		public DateTime? SubscriptionExpireDate { get; set; }

		/// <summary>
		/// Whether the subscription is currently in a free trial period.
		/// Only valid for active subscriptions.
		/// </summary>
		public bool IsFreeTrial { get; set; }

		/// <summary>
		/// Whether the subscription is currently in an introductory price period.
		/// Only valid for active subscriptions.
		/// </summary>
		public bool IsIntroductoryPrice { get; set; }

		/// <summary>
		/// The number of units in the subscription period (e.g., 1 for 1 month, 7 for 1 week).
		/// Only valid for subscription products.
		/// </summary>
		public int SubscriptionPeriodUnitCount { get; set; }

		/// <summary>
		/// The unit of the subscription period (e.g., Day, Week, Month, Year).
		/// Only valid for subscription products.
		/// </summary>
		public SubscriptionPeriodUnit SubscriptionPeriod { get; set; }

		/// <summary>
		/// Localized introductory price string, if available.
		/// Null if no introductory offer is available.
		/// </summary>
		public string LocalizedIntroductoryPrice { get; set; }

		/// <summary>
		/// Numeric introductory price in the local currency.
		/// Null if no introductory offer is available.
		/// </summary>
		public decimal? IntroductoryPrice { get; set; }

		/// <summary>
		/// The number of periods the introductory price is available for.
		/// E.g., 3 means the introductory price applies for 3 subscription periods.
		/// </summary>
		public int IntroductoryPricePeriodCount { get; set; }

		/// <summary>
		/// Whether the subscription will auto-renew at the end of the current period.
		/// Only valid for active subscriptions. May not be accurate on all platforms.
		/// </summary>
		public bool WillAutoRenew { get; set; }

		/// <summary>
		/// Whether the subscription is in a grace period (payment failed but still active).
		/// </summary>
		public bool IsInGracePeriod { get; set; }

		/// <summary>
		/// Whether the subscription is in a billing retry period.
		/// Apple: up to 60 days, Google: account hold up to 30 days.
		/// </summary>
		public bool IsInBillingRetry { get; set; }
	}

	/// <summary>
	/// Represents the unit of time for a subscription period.
	/// </summary>
	public enum SubscriptionPeriodUnit
	{
		Day = 0,
		Week = 1,
		Month = 2,
		Year = 3
	}
}

