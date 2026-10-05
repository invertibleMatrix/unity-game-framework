using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// No-op <see cref="IIAPService"/> used when the Unity Purchasing package is absent
	/// or on unsupported platforms. Lets consumer code hold an IIAPService reference without
	/// #if guards: everything degrades gracefully and logs clearly.
	/// </summary>
	public class NullIAPService : IIAPService
	{
		public bool IsInitialized => false;

		// No store, no orders.
		public void SetOrderHandler(IStoreOrderHandler handler) { }

		public UniTask<bool> InitializeAsync(IEnumerable<IAPProductRegistration> products, CancellationToken ct = default)
		{
			Debug.Log("[NullIAPService] IAP unavailable (package missing or unsupported platform) - purchases disabled.");
			return UniTask.FromResult(false);
		}

		public UniTask<IAPPurchaseResult> PurchaseAsync(string productId, CancellationToken ct = default)
		{
			Debug.LogWarning($"[NullIAPService] PurchaseAsync('{productId}') called with no IAP backend.");
			return UniTask.FromResult(IAPPurchaseResult.Failed(productId, IAPFailureType.NotInitialized,
				"IAP is not available (package missing or unsupported platform)."));
		}

		public UniTask RetryUnfinishedOrdersAsync(CancellationToken ct = default) => UniTask.CompletedTask;

		public IAPProductInfo GetProductInfo(string productId) => null;

		public IReadOnlyList<IAPProductInfo> GetAllProducts() => Array.Empty<IAPProductInfo>();

		public UniTask<bool> RestorePurchasesAsync(CancellationToken ct = default)
		{
			Debug.LogWarning("[NullIAPService] RestorePurchasesAsync called with no IAP backend.");
			return UniTask.FromResult(false);
		}

		public bool IsProductOwned(string productId) => false;

		public bool IsSubscribed(string productId) => false;

		public DateTime? GetSubscriptionExpirationDate(string productId) => null;
	}
}
