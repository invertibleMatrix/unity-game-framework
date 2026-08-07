using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.Services.Costs;
using AK.Services.Rewards;
using AK.Services.Transactions;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Orchestrates the purchase flow: affordability check → cost deduction → reward granting.
	/// IAP is optional — pass null for iapService in games without IAP.
	/// IAP items are identified by having a non-empty ProductID.
	/// </summary>
	public class PurchaseService : IPurchaseService
	{
		private readonly IIAPService         _iapService;
		private readonly ICostService        _costService;
		private readonly IRewardService      _rewardService;
		private readonly ITransactionService _transactionService;

		// Items purchased with immediateCredit: false, waiting to be granted later.
		// Used only on the legacy path (no ITransactionService provided).
		private readonly List<IPurchasable> _pendingCredit = new();

		public IIAPService IAPService => _iapService;

		/// <summary>
		/// Number of purchased items whose rewards are still pending (immediateCredit was false).
		/// </summary>
		public int PendingCreditCount => _pendingCredit.Count;

		/// <summary>
		/// Create a PurchaseService.
		/// </summary>
		/// <param name="costService">The cost service for checking affordability and deducting costs.</param>
		/// <param name="rewardService">The reward service for granting purchase rewards.</param>
		/// <param name="iapService">Optional IAP service for platform store operations. Pass null for games without IAP.</param>
		public PurchaseService(
			ICostService costService,
			IRewardService rewardService,
			IIAPService iapService = null,
			ITransactionService transactionService = null)
		{
			_costService        = costService;
			_rewardService      = rewardService;
			_iapService         = iapService;
			_transactionService = transactionService;
		}

		public async UniTask<PurchaseStatus> Purchase(IPurchasable item, bool immediateCredit)
		{
			if (item == null)
			{
				Debug.LogError("[PurchaseService] Cannot purchase null item.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

			if (item.Cost == null || item.Cost.CostTypeUID == null)
			{
				Debug.LogError($"[PurchaseService] Item '{item.DisplayName}' has no Cost or CostTypeUID assigned.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

			// IAP flow — when the item has a ProductID and IAP is available
			if (!string.IsNullOrEmpty(item.ProductID))
			{
				if (_iapService == null)
				{
					// Never silently charge currency for a store product.
					Debug.LogError($"[PurchaseService] Item '{item.DisplayName}' has a ProductID but no IIAPService was provided.");
					return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.IAPNotInitialized };
				}

				return await HandleInAppPurchase(item, immediateCredit);
			}

			// All other purchases delegate to CostService → ICostProvider
			if (!_costService.CanAfford(item.Cost))
			{
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InsufficientCurrency };
			}

			if (!_costService.Deduct(item.Cost))
			{
				Debug.LogWarning($"[PurchaseService] Failed to deduct cost for item '{item.DisplayName}'.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

			if (_transactionService != null)
			{
				return await CreditWithTransaction(item, immediateCredit);
			}

			GrantRewards(item, immediateCredit);
			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
		}

		private async UniTask<PurchaseStatus> HandleInAppPurchase(IPurchasable item, bool immediateCredit)
		{
			if (!_iapService.IsInitialized)
			{
				Debug.LogError("[PurchaseService] IIAPService is not initialized.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.IAPNotInitialized };
			}

			var iapResult = await _iapService.PurchaseAsync(item.ProductID);

			if (!iapResult.Success)
			{
				Debug.LogWarning($"[PurchaseService] IAP purchase failed for '{item.ProductID}': {iapResult.FailureReason}");
				return new PurchaseStatus { Error = MapIAPFailure(iapResult.FailureType) };
			}

			Debug.Log($"[PurchaseService] IAP purchase succeeded for '{item.ProductID}' (tx: {iapResult.TransactionId})");

			if (_transactionService != null)
			{
				return await CreditWithTransaction(item, immediateCredit);
			}

			GrantRewards(item, immediateCredit);
			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
		}

		// Purchases are ledgered as transactions: recorded pending with their reward
		// payload, credited immediately or left for a later GrantPendingCredits.
		private async UniTask<PurchaseStatus> CreditWithTransaction(IPurchasable item, bool immediateCredit)
		{
			var rewards = new List<IReward>();
			item.CollectRewards(rewards);

			var transaction = _transactionService.RecordPending(item.TransactionTypeUID, rewards, item.ProductID);

			if (immediateCredit)
			{
				bool credited = await _transactionService.CreditAsync(transaction);
				if (!credited)
				{
					return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
				}
			}

			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
		}

		/// <summary>
		/// Grant rewards via the RewardService using the IPurchasable.CollectRewards interface method.
		/// With immediateCredit=false the item is queued for a later <see cref="GrantPendingCredits"/>
		/// instead of vanishing silently.
		/// </summary>
		private void GrantRewards(IPurchasable item, bool immediateCredit)
		{
			if (!immediateCredit)
			{
				_pendingCredit.Add(item);
				return;
			}

			GrantItemRewards(item);
		}

		/// <summary>
		/// Grants all rewards that were deferred with immediateCredit=false. Returns the number of
		/// items credited. On the transaction path this recovers pending transactions from
		/// disk, so deferred purchases survive crashes.
		/// </summary>
		public async UniTask<int> GrantPendingCredits()
		{
			if (_transactionService != null)
			{
				int credited = 0;
				foreach (var transaction in _transactionService.GetPendingTransactions())
				{
					if (await _transactionService.CreditAsync(transaction))
					{
						credited++;
					}
				}

				return credited;
			}

			var count = _pendingCredit.Count;

			foreach (var item in _pendingCredit)
			{
				GrantItemRewards(item);
			}

			_pendingCredit.Clear();
			return count;
		}

		private void GrantItemRewards(IPurchasable item)
		{
			List<IReward> rewards = new();
			item.CollectRewards(rewards);

			foreach (IReward reward in rewards)
			{
				_rewardService.TryGrantReward(reward);
			}
		}

		private static PurchaseStatus.ErrorCode MapIAPFailure(IAPFailureType failureType)
		{
			return failureType switch
			{
				IAPFailureType.UserCancelled           => PurchaseStatus.ErrorCode.Cancelled,
				IAPFailureType.NotInitialized          => PurchaseStatus.ErrorCode.IAPNotInitialized,
				IAPFailureType.ProductUnavailable      => PurchaseStatus.ErrorCode.IAPProductUnavailable,
				IAPFailureType.PaymentDeclined         => PurchaseStatus.ErrorCode.IAPPaymentDeclined,
				IAPFailureType.StoreError              => PurchaseStatus.ErrorCode.IAPStoreError,
				IAPFailureType.DuplicateTransaction    => PurchaseStatus.ErrorCode.IAPDuplicateTransaction,
				IAPFailureType.Timeout                 => PurchaseStatus.ErrorCode.IAPTimeout,
				IAPFailureType.ExistingPurchasePending => PurchaseStatus.ErrorCode.IAPStoreError,
				_                                      => PurchaseStatus.ErrorCode.IAPUnknownError
			};
		}
	}
}
