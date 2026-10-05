using System;
using System.Collections.Generic;
using System.Threading;
using AK.Kernel.Purchasing;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace AK.Services
{
	/// <summary>
	/// <see cref="IIAPService"/> on Unity In-App Purchasing 5 (<see cref="StoreController"/>),
	/// its events bridged to async UniTask operations. Compiled only when com.unity.purchasing 5
	/// is installed; create it through <see cref="IAPServiceFactory.Create"/>.
	///
	/// No order is confirmed (finished) before the order handler fulfils it. Orders that arrive
	/// before a handler is set wait for one. Orders it doesn't fulfil stay unfinished: retried by
	/// <see cref="RetryUnfinishedOrdersAsync"/>, and delivered again by the store at the next
	/// launch. One product per order. Main thread only.
	/// </summary>
	public sealed class UnityIAPService : IIAPService, IDisposable
	{
		/// <summary>
		/// How long <see cref="PurchaseAsync"/> waits for the store, in real time. A purchase still
		/// under way then reaches the order handler when it completes.
		/// </summary>
		public static readonly TimeSpan PurchaseTimeout = TimeSpan.FromMinutes(2);

		private readonly Dictionary<string, IAPProductInfo>   _productCache          = new();
		private readonly List<IAPProductInfo>                 _products              = new();
		private readonly Dictionary<string, SubscriptionInfo> _subscriptionInfoCache = new();
		private readonly HashSet<string>                      _ownedProductsCache    = new();

		// Paid orders the store hasn't finished, by transaction id.
		private readonly Dictionary<string, HeldOrder> _orders = new();

		// Ends the order handler's work when the service is disposed.
		private readonly CancellationTokenSource _lifetime = new();

		private StoreController    _storeController;
		private IStoreOrderHandler _handler;
		private bool               _initializing;
		private bool               _disposed;

		private UniTaskCompletionSource<bool> _fetchProductsTcs;
		private UniTaskCompletionSource<bool> _fetchPurchasesTcs;
		private PurchaseWait                  _purchase;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			IAPServiceFactory.Unity = static () => new UnityIAPService();
		}

		public bool IsInitialized { get; private set; }

		/// <summary>
		/// Fired when the store connection is lost after a successful initialization.
		/// Consumers can subscribe to react to connectivity issues.
		/// </summary>
		public event Action<string> OnStoreDisconnected;

		public void SetOrderHandler(IStoreOrderHandler handler)
		{
			ThrowIfDisposed();

			_handler = handler;
			if (handler == null) return;

			foreach (HeldOrder held in WaitingOrders())
			{
				Fulfil(held, _lifetime.Token).Forget();
			}
		}

		// ─────────────────────────────────────────────
		// Initialization
		// ─────────────────────────────────────────────

		public async UniTask<bool> InitializeAsync(IEnumerable<IAPProductRegistration> products, CancellationToken ct = default)
		{
			if (products == null) throw new ArgumentNullException(nameof(products));
			ThrowIfDisposed();

			if (IsInitialized) return true;

			if (_initializing)
			{
				Debug.LogWarning("[UnityIAPService] Initialization is already under way.");
				return false;
			}

			var catalogProvider = new CatalogProvider();
			foreach (IAPProductRegistration product in products)
			{
				catalogProvider.AddProduct(product.ProductId, MapProductType(product.ProductType));
			}

			_initializing = true;
			try
			{
				// Attached once, before the first connection, as Unity IAP requires. A failed
				// initialization keeps them for the next try.
				if (_storeController == null)
				{
					_storeController = UnityIAPServices.StoreController();
					Attach(_storeController);
				}

				await _storeController.Connect().AsUniTask().AttachExternalCancellation(ct);

				_fetchProductsTcs = new UniTaskCompletionSource<bool>();
				catalogProvider.FetchProducts(list => _storeController.FetchProducts(list));

				if (!await _fetchProductsTcs.Task.AttachExternalCancellation(ct))
				{
					Debug.LogError("[UnityIAPService] Product fetch failed — initialization incomplete.");
					return false;
				}

				// The purchases the store holds. Unfinished orders among them arrive as pending
				// orders, and go to the order handler.
				_fetchPurchasesTcs = new UniTaskCompletionSource<bool>();
				_storeController.FetchPurchases();

				if (!await _fetchPurchasesTcs.Task.AttachExternalCancellation(ct))
				{
					// Non-fatal: purchases fetch can fail but we can still proceed
					Debug.LogWarning("[UnityIAPService] Purchases fetch failed — proceeding without restored purchases.");
				}

				IsInitialized = true;
				return true;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception e)
			{
				Debug.LogError($"[UnityIAPService] Initialization failed: {e}");
				return false;
			}
			finally
			{
				_fetchProductsTcs  = null;
				_fetchPurchasesTcs = null;
				_initializing      = false;
			}
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			_lifetime.Cancel();

			if (_storeController != null) Detach(_storeController);

			_purchase?.Completion.TrySetResult(IAPPurchaseResult.Failed(_purchase.ProductId, IAPFailureType.NotInitialized, "The IAP service was disposed."));
			_handler      = null;
			IsInitialized = false;
		}

		// ─────────────────────────────────────────────
		// Purchase
		// ─────────────────────────────────────────────

		public async UniTask<IAPPurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default)
		{
			ThrowIfDisposed();

			if (!IsInitialized)
			{
				return IAPPurchaseResult.Failed(productId, IAPFailureType.NotInitialized, "IAP service is not initialized.");
			}

			if (_handler == null)
			{
				return IAPPurchaseResult.Failed(productId, IAPFailureType.StoreError,
					"No order handler is set, so the paid order would wait. Set one with SetOrderHandler first.");
			}

			if (_purchase != null)
			{
				return IAPPurchaseResult.Failed(productId, IAPFailureType.ExistingPurchasePending, "Another purchase is already in progress.");
			}

			Product product = _storeController.GetProductById(productId);
			if (product == null || !product.availableToPurchase)
			{
				return IAPPurchaseResult.Failed(productId, IAPFailureType.ProductUnavailable, $"Product '{productId}' is not available for purchase.");
			}

			var wait = new PurchaseWait(productId);
			_purchase = wait;

			try
			{
				_storeController.PurchaseProduct(product);

				// Real time, so a game paused at timeScale 0 behind the store's sheet still times out.
				return await wait.Completion.Task.AttachExternalCancellation(ct).Timeout(PurchaseTimeout, DelayType.Realtime);
			}
			catch (TimeoutException)
			{
				Debug.LogWarning($"[UnityIAPService] The store didn't answer the purchase of '{productId}' in time. It may still complete; its order then goes to the order handler.");
				return IAPPurchaseResult.Failed(productId, IAPFailureType.Timeout,
					"The store did not respond in time. The purchase may still complete.");
			}
			finally
			{
				if (_purchase == wait) _purchase = null;
			}
		}

		public async UniTask RetryUnfinishedOrdersAsync(CancellationToken ct = default)
		{
			if (_handler == null) return;

			foreach (HeldOrder held in WaitingOrders())
			{
				ct.ThrowIfCancellationRequested();
				await Fulfil(held, ct);
			}
		}

		// ─────────────────────────────────────────────
		// Product Info
		// ─────────────────────────────────────────────

		public IAPProductInfo GetProductInfo(string productId)
		{
			if (!IsInitialized || productId == null) return null;
			return _productCache.GetValueOrDefault(productId);
		}

		public IReadOnlyList<IAPProductInfo> GetAllProducts() => _products;

		public bool IsProductOwned(string productId)
		{
			if (!IsInitialized) return false;

			Product product = _storeController.GetProductById(productId);
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

			IAPProductInfo productInfo = GetProductInfo(productId);
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

			IAPProductInfo productInfo = GetProductInfo(productId);
			if (productInfo == null || !productInfo.IsSubscription) return null;

			return productInfo.SubscriptionExpireDate;
		}

		// ─────────────────────────────────────────────
		// Restore Purchases
		// ─────────────────────────────────────────────

		public UniTask<bool> RestorePurchasesAsync(CancellationToken ct = default)
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
					RefreshAllProductCache();
				}
				else
				{
					Debug.LogWarning($"[UnityIAPService] Restore transactions failed: {error}");
				}

				restoreTcs.TrySetResult(success);
			});

			return restoreTcs.Task.AttachExternalCancellation(ct);
		}

		// ═════════════════════════════════════════════
		// Orders
		// ═════════════════════════════════════════════

		/// <summary>Hands an order to the handler, then finishes it at the store if it was fulfilled.</summary>
		private async UniTask Fulfil(HeldOrder held, CancellationToken ct)
		{
			if (held.State != OrderState.Waiting || _handler == null) return;

			held.State = OrderState.Fulfilling;

			StoreOrder info  = held.Info;
			StoreOrder order = AnsweredBy(held) != null
				? new StoreOrder(info.ProductId, info.TransactionId, info.Receipt, info.Price, answersPurchase: true)
				: info;

			OrderFulfilment fulfilment;
			try
			{
				fulfilment = await _handler.FulfilAsync(order, ct);
			}
			catch (OperationCanceledException)
			{
				held.State = OrderState.Waiting;
				return;
			}
			catch (Exception e)
			{
				held.State = OrderState.Waiting;
				Debug.LogException(e);
				AnsweredBy(held)?.Completion.TrySetResult(
					IAPPurchaseResult.Failed(info.ProductId, IAPFailureType.Unknown, $"The order handler failed: {e.Message}"));
				return;
			}

			if (_disposed) return;

			switch (fulfilment)
			{
				case OrderFulfilment.Fulfilled:
					held.State = OrderState.Confirming;
					_storeController.ConfirmPurchase(held.Order);
					break;

				case OrderFulfilment.Rejected:
					held.State = OrderState.Rejected;
					break;

				default:
					held.State = OrderState.Waiting;
					break;
			}

			AnsweredBy(held)?.Completion.TrySetResult(IAPPurchaseResult.Succeeded(info.ProductId, info.Receipt, info.TransactionId));
		}

		// A copy, since fulfilling an order can finish it.
		private List<HeldOrder> WaitingOrders()
		{
			var waiting = new List<HeldOrder>();
			foreach (HeldOrder held in _orders.Values)
			{
				if (held.State == OrderState.Waiting) waiting.Add(held);
			}

			return waiting;
		}

		// The purchase under way, if the order answers it.
		private PurchaseWait AnsweredBy(HeldOrder held) => _purchase != null && _purchase.Order == held ? _purchase : null;

		// A failure names only the product, so it ends the purchase of that product while no order answers it.
		private void FailPurchase(string productId, IAPFailureType failure, string message)
		{
			if (_purchase != null && _purchase.Order == null && _purchase.ProductId == productId)
			{
				_purchase.Completion.TrySetResult(IAPPurchaseResult.Failed(productId, failure, message));
			}
		}

		private StoreOrder Describe(Order order)
		{
			string  productId = ProductIdOf(order);
			Product product   = productId != null ? _storeController.GetProductById(productId) : null;

			Money price = Money.None;
			if (product?.metadata != null)
			{
				StorePrices.TryToMoney(product.metadata.localizedPrice, product.metadata.isoCurrencyCode, out price);
			}

			return new StoreOrder(productId, order.Info?.TransactionID, order.Info?.Receipt, price);
		}

		private static string ProductIdOf(Order order)
		{
			IReadOnlyList<CartItem> items = order?.CartOrdered?.Items();
			if (items == null) return null;

			for (int i = 0; i < items.Count; i++)
			{
				string id = items[i]?.Product?.definition?.id;
				if (id != null) return id;
			}

			return null;
		}

		// ═════════════════════════════════════════════
		// Store Event Handlers
		// ═════════════════════════════════════════════

		private void Attach(StoreController controller)
		{
			controller.OnStoreDisconnected    += HandleStoreDisconnected;
			controller.OnProductsFetched      += HandleProductsFetched;
			controller.OnProductsFetchFailed  += HandleProductsFetchFailed;
			controller.OnPurchasesFetched     += HandlePurchasesFetched;
			controller.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;
			controller.OnPurchasePending      += HandlePurchasePending;
			controller.OnPurchaseConfirmed    += HandlePurchaseConfirmed;
			controller.OnPurchaseFailed       += HandlePurchaseFailed;
			controller.OnPurchaseDeferred     += HandlePurchaseDeferred;
		}

		private void Detach(StoreController controller)
		{
			controller.OnStoreDisconnected    -= HandleStoreDisconnected;
			controller.OnProductsFetched      -= HandleProductsFetched;
			controller.OnProductsFetchFailed  -= HandleProductsFetchFailed;
			controller.OnPurchasesFetched     -= HandlePurchasesFetched;
			controller.OnPurchasesFetchFailed -= HandlePurchasesFetchFailed;
			controller.OnPurchasePending      -= HandlePurchasePending;
			controller.OnPurchaseConfirmed    -= HandlePurchaseConfirmed;
			controller.OnPurchaseFailed       -= HandlePurchaseFailed;
			controller.OnPurchaseDeferred     -= HandlePurchaseDeferred;
		}

		private void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
		{
			Debug.LogError($"[UnityIAPService] Store disconnected: {failure.message}");
			OnStoreDisconnected?.Invoke(failure.message);
		}

		// ─── Product Fetch ───

		private void HandleProductsFetched(List<Product> products)
		{
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
			// Process all confirmed orders to extract subscription info
			ProcessOrdersForSubscriptionInfo(orders);

			// Refresh product cache with subscription data
			RefreshAllProductCache();

			// Pending orders are delivered via OnPurchasePending
			_fetchPurchasesTcs?.TrySetResult(true);
		}

		private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
		{
			Debug.LogWarning($"[UnityIAPService] Purchases fetch failed: {failure.message}");
			_fetchPurchasesTcs?.TrySetResult(false);
		}

		// ─── Purchase Flow ───

		// A paid order, bought now or delivered again: held until the handler fulfils it. Never
		// confirmed here.
		private void HandlePurchasePending(PendingOrder pendingOrder)
		{
			StoreOrder info = Describe(pendingOrder);

			if (string.IsNullOrEmpty(info.TransactionId))
			{
				// Unity IAP can't confirm such an order either.
				Debug.LogWarning($"[UnityIAPService] The store delivered an order for '{info.ProductId}' without a transaction id. It stays unfinished.");
				FailPurchase(info.ProductId, IAPFailureType.StoreError, "The order has no transaction id.");
				return;
			}

			if (_orders.TryGetValue(info.TransactionId, out HeldOrder held))
			{
				// Delivered again: confirm it through the latest delivery.
				held.Order = pendingOrder;
			}
			else
			{
				held = new HeldOrder(pendingOrder, info);
				_orders.Add(info.TransactionId, held);

				// The first new order of the product being bought answers the purchase. One held
				// from before isn't its answer, even for the same product.
				if (_purchase != null && _purchase.Order == null && _purchase.ProductId == info.ProductId) _purchase.Order = held;
			}

			if (_handler != null) Fulfil(held, _lifetime.Token).Forget();
		}

		private void HandlePurchaseConfirmed(Order order)
		{
			string transactionId = order.Info?.TransactionID;

			if (order is ConfirmedOrder confirmedOrder)
			{
				if (transactionId != null) _orders.Remove(transactionId);

				// Extract subscription info and ownership from the order
				ExtractProductInfoFromOrder(confirmedOrder);

				// Refresh the cached product info
				string  productId = ProductIdOf(order);
				Product product   = productId != null ? _storeController.GetProductById(productId) : null;
				if (product != null)
				{
					CacheProduct(product);
					RebuildProductList();
				}
			}
			else if (order is FailedOrder failedOrder)
			{
				Debug.LogWarning($"[UnityIAPService] Order {transactionId} for '{ProductIdOf(order)}' was fulfilled but the store couldn't finish it — " +
				                 $"{failedOrder.FailureReason}: {failedOrder.Details}. It stays unfinished, and is finished when retried or at the next launch.");

				if (transactionId != null && _orders.TryGetValue(transactionId, out HeldOrder held) && held.State == OrderState.Confirming)
				{
					held.State = OrderState.Waiting;
				}
			}
		}

		private void HandlePurchaseFailed(FailedOrder failedOrder)
		{
			string productId = ProductIdOf(failedOrder);
			Debug.LogWarning($"[UnityIAPService] Purchase failed for: {productId} — {failedOrder.FailureReason}: {failedOrder.Details}");

			FailPurchase(productId, MapFailureReason(failedOrder.FailureReason), failedOrder.Details);
		}

		private void HandlePurchaseDeferred(DeferredOrder deferredOrder)
		{
			string productId = ProductIdOf(deferredOrder);
			Debug.Log($"[UnityIAPService] The store holds the purchase of '{productId}' for approval.");

			FailPurchase(productId, IAPFailureType.Deferred, "The store holds the purchase for approval. If approved, its order goes to the order handler.");
		}

		// ─────────────────────────────────────────────
		// Internal Helpers
		// ─────────────────────────────────────────────

		private void ThrowIfDisposed()
		{
			if (_disposed) throw new ObjectDisposedException(nameof(UnityIAPService));
		}

		private void RefreshProductCache(List<Product> fetchedProducts)
		{
			_productCache.Clear();
			foreach (Product product in fetchedProducts)
			{
				CacheProduct(product);
			}

			RebuildProductList();
		}

		private void RefreshAllProductCache()
		{
			_productCache.Clear();
			foreach (Product product in _storeController.GetProducts())
			{
				CacheProduct(product);
			}

			RebuildProductList();
		}

		private void RebuildProductList()
		{
			_products.Clear();
			foreach (IAPProductInfo info in _productCache.Values)
			{
				_products.Add(info);
			}
		}

		private void CacheProduct(Product product)
		{
			bool isSubscription  = product.definition.type == ProductType.Subscription;
			bool isNonConsumable = product.definition.type == ProductType.NonConsumable;

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

			Money price = Money.None;
			if (product.metadata != null)
			{
				StorePrices.TryToMoney(product.metadata.localizedPrice, product.metadata.isoCurrencyCode, out price);
			}

			var info = new IAPProductInfo
			{
				ProductId            = product.definition.id,
				LocalizedTitle       = product.metadata?.localizedTitle,
				LocalizedDescription = product.metadata?.localizedDescription,
				LocalizedPrice       = product.metadata?.localizedPriceString,
				Price                = price,
				AvailableToPurchase  = product.availableToPurchase,
				HasReceipt           = hasOwnership,
				// Subscription fields
				IsSubscription       = isSubscription
			};

			// Populate subscription-specific metadata from cached subscription info
			if (isSubscription && _subscriptionInfoCache.TryGetValue(product.definition.id, out SubscriptionInfo subscriptionInfo))
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
			foreach (ConfirmedOrder order in orders.ConfirmedOrders)
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

			foreach (IPurchasedProductInfo purchasedProduct in order.Info.PurchasedProductInfo)
			{
				string productId = purchasedProduct.productId;

				// Get the product to determine its type
				Product product = _storeController.GetProductById(productId);
				if (product == null) continue;

				// For subscriptions, cache the subscription info
				if (product.definition.type == ProductType.Subscription && purchasedProduct.subscriptionInfo != null)
				{
					_subscriptionInfoCache[productId] = purchasedProduct.subscriptionInfo;
				}
				// For non-consumables, track ownership
				else if (product.definition.type == ProductType.NonConsumable)
				{
					_ownedProductsCache.Add(productId);
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
				info.IsSubscribed = subscriptionInfo.IsSubscribed() == Result.True;

				// Check if expired
				if (subscriptionInfo.IsExpired() == Result.True)
				{
					info.IsSubscribed = false;
				}

				// Get expiration date
				try
				{
					DateTime expireDate = subscriptionInfo.GetExpireDate();
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
				info.WillAutoRenew = subscriptionInfo.IsAutoRenewing() == Result.True;

				// Check if cancelled
				if (subscriptionInfo.IsCancelled() == Result.True)
				{
					info.WillAutoRenew = false;
				}

				// Get remaining time
				try
				{
					TimeSpan remainingTime = subscriptionInfo.GetRemainingTime();
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
				TimeSpan period = subscriptionInfo.GetSubscriptionPeriod();
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
				if (subscriptionInfo.IsIntroductoryPricePeriod() == Result.True)
				{
					info.IsIntroductoryPrice = true;

					// Get introductory price string
					string introPrice = subscriptionInfo.GetIntroductoryPrice();
					if (introPrice != "not available")
					{
						info.LocalizedIntroductoryPrice = introPrice;
					}

					// Get introductory price period cycles
					info.IntroductoryPricePeriodCount = (int)subscriptionInfo.GetIntroductoryPricePeriodCycles();
				}

				// Check if in free trial
				if (subscriptionInfo.IsFreeTrial() == Result.True)
				{
					info.IsFreeTrial = true;
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[UnityIAPService] Failed to extract introductory offer info: {e.Message}");
			}
		}

		private static ProductType MapProductType(IAPProductType type)
		{
			return type switch
			{
				IAPProductType.Consumable    => ProductType.Consumable,
				IAPProductType.NonConsumable => ProductType.NonConsumable,
				IAPProductType.Subscription  => ProductType.Subscription,
				_                            => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a product type.")
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
				PurchaseFailureReason.ValidationFailure       => IAPFailureType.StoreError,
				PurchaseFailureReason.StoreNotConnected       => IAPFailureType.StoreError,
				PurchaseFailureReason.PurchaseMissing         => IAPFailureType.StoreError,
				_                                             => IAPFailureType.Unknown
			};
		}

		private enum OrderState : byte
		{
			/// <summary>Waiting for the handler, or for another try.</summary>
			Waiting,

			/// <summary>With the handler now.</summary>
			Fulfilling,

			/// <summary>Fulfilled; the store is finishing it.</summary>
			Confirming,

			/// <summary>Refused by the handler. Not retried in this session.</summary>
			Rejected,
		}

		private sealed class HeldOrder
		{
			public readonly StoreOrder Info;
			public PendingOrder        Order;
			public OrderState          State;

			public HeldOrder(PendingOrder order, StoreOrder info)
			{
				Order = order;
				Info  = info;
			}
		}

		private sealed class PurchaseWait
		{
			public readonly string                                     ProductId;
			public readonly UniTaskCompletionSource<IAPPurchaseResult> Completion = new();

			// The order that answers it, once the store delivers one.
			public HeldOrder Order;

			public PurchaseWait(string productId) => ProductId = productId;
		}
	}
}
