using System.Collections.Generic;
using AK.Core;
using AK.Core.Collections;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
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
	///
	/// Every expected failure is a <see cref="Result"/> code the UI can map to a message:
	/// <see cref="ErrorCode.CannotAfford"/>, <see cref="ErrorCode.Cancelled"/>, the 6xx store
	/// codes, and so on. Nothing here throws for a declined purchase.
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

		/// <param name="costService">The cost service for checking affordability and deducting costs.</param>
		/// <param name="rewardService">The reward service for granting purchase rewards.</param>
		/// <param name="iapService">Optional IAP service for platform store operations. Pass null for games without IAP.</param>
		/// <param name="transactionService">Optional ledger. When present, purchases are recorded as transactions and survive crashes.</param>
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

		public async UniTask<Result> Purchase(IPurchasable item, bool immediateCredit)
		{
			if (item == null) return Result.Fail(ErrorCode.NullArgument, "item");

			if (item.Cost == null || item.Cost.CostType.IsNone)
			{
				return Result.Fail(ErrorCode.NoIdentity, $"'{item.DisplayName}' has no Cost or cost type");
			}

			if (!string.IsNullOrEmpty(item.ProductID))
			{
				if (_iapService == null)
				{
					// Never silently charge currency for a store product.
					return Result.Fail(ErrorCode.StoreNotInitialized, $"'{item.DisplayName}' has a ProductID but no IIAPService was provided");
				}

				return await HandleInAppPurchase(item, immediateCredit);
			}

			Result affordable = _costService.CanAfford(item.Cost);
			if (affordable.IsFailed) return affordable;

			Result deducted = _costService.Deduct(item.Cost);
			if (deducted.IsFailed) return deducted;

			if (_transactionService != null)
			{
				return await CreditWithTransaction(item, immediateCredit);
			}

			GrantRewards(item, immediateCredit);
			return Result.Ok;
		}

		private async UniTask<Result> HandleInAppPurchase(IPurchasable item, bool immediateCredit)
		{
			if (!_iapService.IsInitialized) return Result.Fail(ErrorCode.StoreNotInitialized);

			IAPPurchaseResult iapResult = await _iapService.PurchaseAsync(item.ProductID);
			if (!iapResult.Success)
			{
				return Result.Fail(MapIAPFailure(iapResult.FailureType), iapResult.FailureReason);
			}

			Debug.Log($"[PurchaseService] IAP purchase succeeded for '{item.ProductID}' (tx: {iapResult.TransactionId})");

			if (_transactionService != null)
			{
				return await CreditWithTransaction(item, immediateCredit);
			}

			GrantRewards(item, immediateCredit);
			return Result.Ok;
		}

		// Purchases are ledgered as transactions: recorded pending with their reward
		// payload, credited immediately or left for a later GrantPendingCredits.
		private async UniTask<Result> CreditWithTransaction(IPurchasable item, bool immediateCredit)
		{
			Result<Transaction> pending = RecordPendingPurchase(item);
			if (pending.IsFailed) return pending.Untyped;

			if (!immediateCredit) return Result.Ok;

			return await _transactionService.CreditAsync(pending.Value);
		}

		// The transaction keeps the reward list for its lifetime, so it cannot come from the pool.
		private Result<Transaction> RecordPendingPurchase(IPurchasable item)
		{
			var rewards = new List<IReward>();
			item.CollectRewards(rewards);

			return _transactionService.RecordPending(item.TransactionType, rewards.Count > 0 ? rewards : null, item.ProductID);
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
				IReadOnlyList<Transaction> pending = _transactionService.GetPendingTransactions();
				for (int i = 0; i < pending.Count; i++)
				{
					if (await _transactionService.CreditAsync(pending[i])) credited++;
				}

				return credited;
			}

			int count = _pendingCredit.Count;
			for (int i = 0; i < _pendingCredit.Count; i++)
			{
				GrantItemRewards(_pendingCredit[i]);
			}

			_pendingCredit.Clear();
			return count;
		}

		private void GrantItemRewards(IPurchasable item)
		{
			using PooledList<IReward> rewards = ListPool<IReward>.Rent();
			item.CollectRewards(rewards.List);

			for (int i = 0; i < rewards.List.Count; i++)
			{
				Result granted = _rewardService.Grant(rewards.List[i]);
				if (granted.IsFailed)
				{
					Debug.LogWarning($"[PurchaseService] Reward {i} for '{item.DisplayName}' was not granted: {granted}.");
				}
			}
		}

		private static ErrorCode MapIAPFailure(IAPFailureType failureType)
		{
			return failureType switch
			{
				IAPFailureType.UserCancelled           => ErrorCode.Cancelled,
				IAPFailureType.NotInitialized          => ErrorCode.StoreNotInitialized,
				IAPFailureType.ProductUnavailable      => ErrorCode.ProductUnavailable,
				IAPFailureType.PaymentDeclined         => ErrorCode.PaymentDeclined,
				IAPFailureType.StoreError              => ErrorCode.StoreError,
				IAPFailureType.DuplicateTransaction    => ErrorCode.DuplicateTransaction,
				IAPFailureType.Timeout                 => ErrorCode.Timeout,
				IAPFailureType.ExistingPurchasePending => ErrorCode.StoreError,
				_                                      => ErrorCode.StoreUnknown
			};
		}
	}
}
