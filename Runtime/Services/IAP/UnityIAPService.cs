#if IAP
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

namespace AK.Services
{
	/// <summary>
	/// Implementation of <see cref="IIAPService"/> using Unity In-App Purchasing 5.x V5 API.
	/// Uses <see cref="StoreController"/> obtained via <see cref="UnityIAPServices.StoreController()"/>
	/// with event-based flow bridged to async UniTask operations.
	/// </summary>
	public class UnityIAPService : IIAPService
	{
		private StoreController _storeController;
		private CatalogProvider _catalogProvider;

		private UniTaskCompletionSource<IAPPurchaseResult> _purchaseTcs;
		private string                                     _pendingProductId;

		private readonly Dictionary<string, IAPProductInfo> _productCache = new();
		private readonly Dictionary<string, SubscriptionInfo> _subscriptionInfoCache = new();
		private readonly HashSet<string> _ownedProductsCache = new(); // Tracks owned non-consumable products

		public bool IsInitialized { get; private set; }

		/// <summary>
		/// Fired when the store connection is lost after a successful initialization.
		/// Consumers can subscribe to react to connectivity issues.
		/// </summary>
		public event Action<string> OnStoreDisconnected;

		/// <inheritdoc />
		public event Action<IAPPurchaseResult> OnExternalPurchaseConfirmed;

		// ─────────────────────────────────────────────
		// Initialization
		// ─────────────────────────────────────────────

		public async UniTask<bool> InitializeAsync(IEnumerable<IAPProductRegistration> products)
		{
			if (IsInitialized)
			{
				Debug.LogWarning("[UnityIAPService] Already initialized.");
				return true;
			}

			try
			{
				// 1. Define products via CatalogProvider
				_catalogProvider = new CatalogProvider();
				foreach (var reg in products)
				{
					ProductType unityType = MapProductType(reg.ProductType);
					_catalogProvider.AddProduct(reg.ProductId, unityType);
					Debug.Log($"[UnityIAPService] Registered product in catalog: {reg.ProductId} ({unityType})");
				}

				// 2. Get StoreController via the static factory (as per Unity docs)
				_storeController = UnityIAPServices.StoreController();

				// 3. Attach all required event handlers BEFORE connecting
				_storeController.OnStoreDisconnected += HandleStoreDisconnected;
				_storeController.OnProductsFetched += HandleProductsFetched;
				_storeController.OnProductsFetchFailed += HandleProductsFetchFailed;
				_storeController.OnPurchasesFetched += HandlePurchasesFetched;
				_storeController.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;
				_storeController.OnPurchasePending += HandlePurchasePending;
				_storeController.OnPurchaseFailed += HandlePurchaseFailed;
				_storeController.OnPurchaseConfirmed += HandlePurchaseConfirmed;

				// 4. Connect to the store
				Debug.Log("[UnityIAPService] Connecting to store...");
				await _storeController.Connect();

				// 5. Fetch products via CatalogProvider → StoreController pipeline
				var fetchProductsTcs = new UniTaskCompletionSource<bool>();
				_fetchProductsTcs = fetchProductsTcs;

				_catalogProvider.FetchProducts(list => _storeController.FetchProducts(list));

				var fetchResult = await fetchProductsTcs.Task;
				_fetchProductsTcs = null;

				if (!fetchResult)
				{
					Debug.LogError("[UnityIAPService] Product fetch failed — initialization incomplete.");
					return false;
				}

				// 6. Fetch existing purchases (restores pending orders on Android/iOS)
				var fetchPurchasesTcs = new UniTaskCompletionSource<bool>();
				_fetchPurchasesTcs = fetchPurchasesTcs;

				_storeController.FetchPurchases();

				var purchasesFetchResult = await fetchPurchasesTcs.Task;
				_fetchPurchasesTcs = null;

				if (!purchasesFetchResult)
				{
					// Non-fatal: purchases fetch can fail but we can still proceed
					Debug.LogWarning("[UnityIAPService] Purchases fetch failed — proceeding without restored purchases.");
				}

				IsInitialized = true;
				Debug.Log("[UnityIAPService] Initialization complete.");
				return true;
			}
			catch (Exception e)
			{
				Debug.LogError($"[UnityIAPService] Initialization failed with exception: {e.Message}\n{e.StackTrace}");
				return false;
			}
		}

		// Completion sources for initialization flow
		private UniTaskCompletionSource<bool> _fetchProductsTcs;
		private UniTaskCompletionSource<bool> _fetchPurchasesTcs;

		// ─────────────────────────────────────────────
		// Purchase
		// ─────────────────────────────────────────────

		public async UniTask<IAPPurchaseResult> PurchaseAsync(string productId)
		{
			if (!IsInitialized)
			{
				Debug.LogError("[UnityIAPService] Cannot purchase — service not initialized.");
				return IAPPurchaseResult.Failed(productId, IAPFailureType.NotInitialized,
					"IAP service is not initialized.");
			}

			if (_purchaseTcs != null)
			{
				Debug.LogWarning("[UnityIAPService] A purchase is already in progress.");
				return IAPPurchaseResult.Failed(productId, IAPFailureType.ExistingPurchasePending,
					"Another purchase is already in progress.");
			}

			var product = _storeController.GetProductById(productId);
			if (product == null || !product.availableToPurchase)
			{
				Debug.LogError($"[UnityIAPService] Product '{productId}' not found or not available.");
				return IAPPurchaseResult.Failed(productId, IAPFailureType.ProductUnavailable,
					$"Product '{productId}' is not available for purchase.");
			}

			_purchaseTcs = new UniTaskCompletionSource<IAPPurchaseResult>();
			_pendingProductId = productId;

			Debug.Log($"[UnityIAPService] Initiating purchase for: {productId}");
			_storeController.PurchaseProduct(product);

			try
			{
				// Timeout guard: if the store never delivers a callback, don't hang forever.
				return await _purchaseTcs.Task.Timeout(TimeSpan.FromMinutes(2));
			}
			catch (TimeoutException)
			{
				// NOT a definite failure: the store may still confirm this purchase, in which
				// case HandlePurchaseConfirmed finds no PurchaseAsync in flight and grants it
				// via OnExternalPurchaseConfirmed. Report a distinct Timeout so callers can
				// show "processing" instead of a false "purchase failed".
				Debug.LogWarning($"[UnityIAPService] Purchase timed out for: {productId} - " +
				                 "the store may still confirm it via OnExternalPurchaseConfirmed.");
				return IAPPurchaseResult.Failed(productId, IAPFailureType.Timeout,
					"The store did not respond in time. The purchase may still complete.");
			}
			finally
			{
				_purchaseTcs = null;
				_pendingProductId = null;
			}
		}

		// ─────────────────────────────────────────────
		// Product Info
		// ─────────────────────────────────────────────

		public IAPProductInfo GetProductInfo(string productId)
		{
			if (!IsInitialized) return null;
			return _productCache.GetValueOrDefault(productId);
		}

		public IReadOnlyList<IAPProductInfo> GetAllProducts()
		{
			return new List<IAPProductInfo>(_productCache.Values);
		}

		public bool IsProductOwned(string productId)
		{
			if (!IsInitialized) return false;
			
			var product = _storeController.GetProductById(productId);
			if (product == null) return false;

			// For subscriptions, check if actively subscribed
			if (product.definition.type == ProductType.Subscription)
			{
				return IsSubscribed(productId);
			}

			// For non-consumables, check the owned products cache
			if (product.definition.type == ProductType.NonConsumable)
			{
				return _ownedProductsCache.Contains(productId);
			}

			// Consumables are not "owned" persistently
			return false;
		}

		public bool IsSubscribed(string productId)
		{
			if (!IsInitialized) return false;

			var productInfo = GetProductInfo(productId);
			if (productInfo == null || !productInfo.IsSubscription) return false;

			// Check if we have an active subscription based on expiration date
			if (productInfo.IsSubscribed && productInfo.SubscriptionExpireDate.HasValue)
			{
				return productInfo.SubscriptionExpireDate.Value > DateTime.UtcNow;
			}

			return false;
		}

		public DateTime? GetSubscriptionExpirationDate(string productId)
		{
			if (!IsInitialized) return null;

			var productInfo = GetProductInfo(productId);
			if (productInfo == null || !productInfo.IsSubscription) return null;

			return productInfo.SubscriptionExpireDate;
		}

		// ─────────────────────────────────────────────
		// Restore Purchases
		// ─────────────────────────────────────────────

		public UniTask<bool> RestorePurchasesAsync()
		{
			if (!IsInitialized)
			{
				Debug.LogError("[UnityIAPService] Cannot restore — service not initialized.");
				return UniTask.FromResult(false);
			}

			var restoreTcs = new UniTaskCompletionSource<bool>();

			_storeController.RestoreTransactions((success, error) =>
			{
				if (success)
				{
					Debug.Log("[UnityIAPService] Restore transactions succeeded.");
					RefreshAllProductCache();
				}
				else
				{
					Debug.LogWarning($"[UnityIAPService] Restore transactions failed: {error}");
				}

				restoreTcs.TrySetResult(success);
			});

			return restoreTcs.Task;
		}

		// ═════════════════════════════════════════════
		// Store Event Handlers
		// ═════════════════════════════════════════════

		private void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
		{
			Debug.LogError($"[UnityIAPService] Store disconnected: {failure.message}");
			OnStoreDisconnected?.Invoke(failure.message);
		}

		// ─── Product Fetch ───

		private void HandleProductsFetched(List<Product> products)
		{
			Debug.Log($"[UnityIAPService] Products fetched successfully: {products.Count}");
			RefreshProductCache(products);
			_fetchProductsTcs?.TrySetResult(true);
		}

		private void HandleProductsFetchFailed(ProductFetchFailed failure)
		{
			Debug.LogError($"[UnityIAPService] Product fetch failed: {failure.FailureReason}");
			_fetchProductsTcs?.TrySetResult(false);
		}

		// ─── Purchase Fetch ───

		private void HandlePurchasesFetched(Orders orders)
		{
			Debug.Log("[UnityIAPService] Purchases fetched successfully.");
			
			// Process all confirmed orders to extract subscription info
			ProcessOrdersForSubscriptionInfo(orders);
			
			// Refresh product cache with subscription data
			RefreshAllProductCache();
			
			// Pending orders will be delivered via OnPurchasePending automatically
			_fetchPurchasesTcs?.TrySetResult(true);
		}

		private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
		{
			Debug.LogWarning($"[UnityIAPService] Purchases fetch failed: {failure.message}");
			_fetchPurchasesTcs?.TrySetResult(false);
		}

		// ─── Purchase Flow ───

		private void HandlePurchasePending(PendingOrder pendingOrder)
		{
			var productId = GetProductIdFromOrder(pendingOrder);
			Debug.Log($"[UnityIAPService] Purchase pending for: {productId}");

			// Auto-confirm the purchase.
			// NOTE: In a production environment with server-side receipt validation,
			// you would validate the receipt BEFORE confirming. For now, we auto-confirm.
			_storeController.ConfirmPurchase(pendingOrder);
		}

		private void HandlePurchaseConfirmed(Order order)
		{
			var productId = GetProductIdFromOrder(order);

			if (order is ConfirmedOrder confirmedOrder)
			{
				Debug.Log($"[UnityIAPService] Purchase confirmed for: {productId}");

				var receipt = confirmedOrder.Info.Receipt ?? string.Empty;
				var transactionId = confirmedOrder.Info.TransactionID ?? string.Empty;

				// Extract subscription info and ownership from the order
				ExtractProductInfoFromOrder(confirmedOrder);

				// Refresh the cached product info
				var product = _storeController.GetProductById(productId);
				if (product != null) CacheProduct(product);

				if (_purchaseTcs != null && _pendingProductId == productId)
				{
					_purchaseTcs.TrySetResult(IAPPurchaseResult.Succeeded(productId, receipt, transactionId));
				}
				else
				{
					// Restored/deferred/promotional purchase with no PurchaseAsync in flight.
					// It was already auto-confirmed in HandlePurchasePending, so this event is
					// the only chance for the game to actually grant the product.
					Debug.Log($"[UnityIAPService] External purchase confirmed for: {productId} - forwarding to OnExternalPurchaseConfirmed");
					OnExternalPurchaseConfirmed?.Invoke(IAPPurchaseResult.Succeeded(productId, receipt, transactionId));
				}
			}
			else if (order is FailedOrder failedOrder)
			{
				Debug.LogWarning(
					$"[UnityIAPService] Purchase confirmation failed for: {productId} — {failedOrder.FailureReason}: {failedOrder.Details}");

				if (_purchaseTcs != null && _pendingProductId == productId)
				{
					var mappedType = MapFailureReason(failedOrder.FailureReason);
					_purchaseTcs.TrySetResult(IAPPurchaseResult.Failed(productId, mappedType, failedOrder.Details));
				}
			}
		}

		private void HandlePurchaseFailed(FailedOrder failedOrder)
		{
			var productId = GetProductIdFromOrder(failedOrder);
			Debug.LogWarning(
				$"[UnityIAPService] Purchase failed for: {productId} — {failedOrder.FailureReason}: {failedOrder.Details}");

			if (_purchaseTcs != null && (_pendingProductId == productId || productId == "unknown"))
			{
				var mappedType = MapFailureReason(failedOrder.FailureReason);
				_purchaseTcs.TrySetResult(IAPPurchaseResult.Failed(productId, mappedType, failedOrder.Details));
			}
		}

		// ─────────────────────────────────────────────
		// Internal Helpers
		// ─────────────────────────────────────────────

		private static string GetProductIdFromOrder(Order order)
		{
			var cartItems = order.CartOrdered?.Items();
			return cartItems?.FirstOrDefault()?.Product?.definition?.id ?? "unknown";
		}

		private void RefreshProductCache(List<Product> fetchedProducts)
		{
			_productCache.Clear();
			foreach (var product in fetchedProducts)
			{
				CacheProduct(product);
			}
		}

		private void RefreshAllProductCache()
		{
			var products = _storeController.GetProducts();
			_productCache.Clear();
			foreach (Product product in products)
			{
				CacheProduct(product);
			}
		}

		private void CacheProduct(Product product)
		{
			var isSubscription = product.definition.type == ProductType.Subscription;
			var isNonConsumable = product.definition.type == ProductType.NonConsumable;
			
			// In V5, ownership is tracked separately:
			// - Subscriptions: via _subscriptionInfoCache
			// - Non-consumables: via _ownedProductsCache
			bool hasOwnership;
			if (isSubscription)
			{
				hasOwnership = _subscriptionInfoCache.ContainsKey(product.definition.id);
			}
			else if (isNonConsumable)
			{
				hasOwnership = _ownedProductsCache.Contains(product.definition.id);
			}
			else
			{
				// Consumables don't have persistent ownership
				hasOwnership = false;
			}

			var info = new IAPProductInfo
			{
				ProductId            = product.definition.id,
				LocalizedTitle       = product.metadata.localizedTitle,
				LocalizedDescription = product.metadata.localizedDescription,
				LocalizedPrice       = product.metadata.localizedPriceString,
				Price                = product.metadata.localizedPrice,
				IsoCurrencyCode      = product.metadata.isoCurrencyCode,
				AvailableToPurchase  = product.availableToPurchase,
				HasReceipt           = hasOwnership,
				// Subscription fields
				IsSubscription       = isSubscription
			};

			// Populate subscription-specific metadata from cached subscription info
			if (isSubscription && _subscriptionInfoCache.TryGetValue(product.definition.id, out var subscriptionInfo))
			{
				PopulateSubscriptionInfo(info, subscriptionInfo);
			}

			_productCache[info.ProductId] = info;
		}

		/// <summary>
		/// Processes orders to extract and cache subscription info and track ownership.
		/// In V5, subscription info is available through IPurchasedProductInfo from orders.
		/// </summary>
		private void ProcessOrdersForSubscriptionInfo(Orders orders)
		{
			if (orders == null) return;

			// Process confirmed orders
			foreach (var order in orders.ConfirmedOrders)
			{
				ExtractProductInfoFromOrder(order);
			}
		}

		/// <summary>
		/// Extracts subscription info and ownership from a confirmed order and caches it.
		/// </summary>
		private void ExtractProductInfoFromOrder(ConfirmedOrder order)
		{
			if (order?.Info?.PurchasedProductInfo == null) return;

			foreach (var purchasedProduct in order.Info.PurchasedProductInfo)
			{
				var productId = purchasedProduct.productId;
				
				// Get the product to determine its type
				var product = _storeController.GetProductById(productId);
				if (product == null) continue;

				// For subscriptions, cache the subscription info
				if (product.definition.type == ProductType.Subscription && purchasedProduct.subscriptionInfo != null)
				{
					_subscriptionInfoCache[productId] = purchasedProduct.subscriptionInfo;
					Debug.Log($"[UnityIAPService] Cached subscription info for: {productId}");
				}
				// For non-consumables, track ownership
				else if (product.definition.type == ProductType.NonConsumable)
				{
					_ownedProductsCache.Add(productId);
					Debug.Log($"[UnityIAPService] Tracked ownership for non-consumable: {productId}");
				}
			}
		}

		/// <summary>
		/// Populates IAPProductInfo from SubscriptionInfo.
		/// </summary>
		private static void PopulateSubscriptionInfo(IAPProductInfo info, SubscriptionInfo subscriptionInfo)
		{
			try
			{
				// Check if subscribed
				var subscribedResult = subscriptionInfo.IsSubscribed();
				info.IsSubscribed = subscribedResult == Result.True;

				// Check if expired
				var expiredResult = subscriptionInfo.IsExpired();
				if (expiredResult == Result.True)
				{
					info.IsSubscribed = false;
				}

				// Get expiration date
				try
				{
					var expireDate = subscriptionInfo.GetExpireDate();
					if (expireDate != DateTime.MinValue)
					{
						info.SubscriptionExpireDate = expireDate;
						
						// Double-check subscription status based on expiration date
						if (expireDate <= DateTime.UtcNow)
						{
							info.IsSubscribed = false;
						}
					}
				}
				catch
				{
					// GetExpireDate may not be supported on all platforms
				}

				// Check if auto-renewing
				var autoRenewingResult = subscriptionInfo.IsAutoRenewing();
				info.WillAutoRenew = autoRenewingResult == Result.True;

				// Check if cancelled
				var cancelledResult = subscriptionInfo.IsCancelled();
				if (cancelledResult == Result.True)
				{
					info.WillAutoRenew = false;
				}

				// Get remaining time
				try
				{
					var remainingTime = subscriptionInfo.GetRemainingTime();
					if (remainingTime != TimeSpan.Zero)
					{
						// We can use remaining time to estimate expiration if GetExpireDate is not available
						if (!info.SubscriptionExpireDate.HasValue || info.SubscriptionExpireDate.Value == DateTime.MinValue)
						{
							info.SubscriptionExpireDate = DateTime.UtcNow + remainingTime;
						}
					}
				}
				catch
				{
					// GetRemainingTime may return TimeSpan.Zero for unsupported platforms
				}

				// Extract subscription period info
				ExtractSubscriptionPeriodInfo(subscriptionInfo, info);
				
				// Extract introductory offer info
				ExtractIntroductoryOfferInfo(subscriptionInfo, info);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[UnityIAPService] Failed to populate subscription info: {e.Message}");
				info.IsSubscribed = false;
			}
		}

		/// <summary>
		/// Extracts subscription period information from SubscriptionInfo.
		/// </summary>
		private static void ExtractSubscriptionPeriodInfo(SubscriptionInfo subscriptionInfo, IAPProductInfo info)
		{
			try
			{
				// Get subscription period as TimeSpan
				var period = subscriptionInfo.GetSubscriptionPeriod();
				if (period != TimeSpan.Zero)
				{
					info.SubscriptionPeriodUnitCount = 1;
					
					// Determine the period unit based on the duration
					if (period.Days > 0)
					{
						if (period.Days >= 365)
						{
							info.SubscriptionPeriod = SubscriptionPeriodUnit.Year;
						}
						else if (period.Days >= 28)
						{
							info.SubscriptionPeriod = SubscriptionPeriodUnit.Month;
						}
						else if (period.Days >= 7)
						{
							info.SubscriptionPeriod = SubscriptionPeriodUnit.Week;
						}
						else
						{
							info.SubscriptionPeriod = SubscriptionPeriodUnit.Day;
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[UnityIAPService] Failed to extract subscription period: {e.Message}");
			}
		}

		/// <summary>
		/// Extracts introductory offer information from SubscriptionInfo.
		/// </summary>
		private static void ExtractIntroductoryOfferInfo(SubscriptionInfo subscriptionInfo, IAPProductInfo info)
		{
			try
			{
				// Check if in introductory price period
				var isIntroResult = subscriptionInfo.IsIntroductoryPricePeriod();
				if (isIntroResult == Result.True)
				{
					info.IsIntroductoryPrice = true;
					
					// Get introductory price string
					var introPrice = subscriptionInfo.GetIntroductoryPrice();
					if (introPrice != "not available")
					{
						info.LocalizedIntroductoryPrice = introPrice;
					}
					
					// Get introductory price period cycles
					var cycles = subscriptionInfo.GetIntroductoryPricePeriodCycles();
					info.IntroductoryPricePeriodCount = (int)cycles;
				}

				// Check if in free trial
				var isFreeTrialResult = subscriptionInfo.IsFreeTrial();
				if (isFreeTrialResult == Result.True)
				{
					info.IsFreeTrial = true;
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[UnityIAPService] Failed to extract introductory offer info: {e.Message}");
			}
		}

		private static ProductType MapProductType(int type)
		{
			return type switch
			{
				0 => ProductType.Consumable,
				1 => ProductType.NonConsumable,
				2 => ProductType.Subscription,
				_ => ProductType.Consumable
			};
		}

		private static IAPFailureType MapFailureReason(PurchaseFailureReason reason)
		{
			return reason switch
			{
				PurchaseFailureReason.PurchasingUnavailable   => IAPFailureType.StoreError,
				PurchaseFailureReason.ExistingPurchasePending => IAPFailureType.ExistingPurchasePending,
				PurchaseFailureReason.ProductUnavailable      => IAPFailureType.ProductUnavailable,
				PurchaseFailureReason.SignatureInvalid        => IAPFailureType.StoreError,
				PurchaseFailureReason.UserCancelled           => IAPFailureType.UserCancelled,
				PurchaseFailureReason.PaymentDeclined         => IAPFailureType.PaymentDeclined,
				PurchaseFailureReason.DuplicateTransaction    => IAPFailureType.DuplicateTransaction,
				_                                             => IAPFailureType.Unknown
			};
		}
	}
}
#endif