using System.Collections.Generic;
using System.Linq;
using AK.CoreDomain;
using AK.CoreDomain.Costs;
using AK.CoreDomain.Rewards;
using AK.Services.Costs;
using AK.Services.Rewards;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services
{
	/// <summary>
	/// Orchestrates the purchase flow: affordability check → cost deduction → reward granting.
	/// IAP is handled intrinsically (platform SDK). All other cost types delegate to CostService.
	/// </summary>
	public class PurchaseService : IPurchaseService
	{
		private readonly IIAPService    _iapService;
		private readonly ICostService   _costService;
		private readonly IRewardService _rewardService;
		private readonly CostType       _iapCostType;

		public IIAPService IAPService => _iapService;

		/// <summary>
		/// Create a new PurchaseService.
		/// </summary>
		/// <param name="iapService">The IAP service for platform store operations.</param>
		/// <param name="costService">The cost service for checking affordability and deducting costs.</param>
		/// <param name="rewardService">The reward service for granting purchase rewards.</param>
		/// <param name="iapCostType">The CostType SO asset that represents In-App Purchases.
		/// When an item's Cost.Type matches this, the IAP flow is used instead of CostService.</param>
		public PurchaseService(
			IIAPService iapService,
			ICostService costService,
			IRewardService rewardService,
			CostType iapCostType)
		{
			_iapService    = iapService;
			_costService   = costService;
			_rewardService = rewardService;
			_iapCostType   = iapCostType;
		}

		public async UniTask<PurchaseStatus> Purchase(PurchasableItemDefinition item, bool immediateCredit)
		{
			if (item == null)
			{
				Debug.LogError("[PurchaseService] Cannot purchase null item.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

			if (item.Cost == null || item.Cost.Type == null)
			{
				Debug.LogError($"[PurchaseService] Item '{item.DisplayName}' has no Cost or CostType assigned.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

			// IAP flow — handled intrinsically because it involves the platform SDK
			if (item.Cost.Type == _iapCostType)
			{
				return await HandleInAppPurchase(item, immediateCredit);
			}

			// All other cost types — delegate to CostService → CostProvider
			if (!_costService.CanAfford(item.Cost))
			{
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InsufficientCurrency };
			}

			if (!_costService.Deduct(item.Cost))
			{
				Debug.LogWarning($"[PurchaseService] Failed to deduct cost for item '{item.DisplayName}'.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

			// Cost deducted — grant rewards
			GrantRewards(item, immediateCredit);
			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
		}

		private async UniTask<PurchaseStatus> HandleInAppPurchase(PurchasableItemDefinition item, bool immediateCredit)
		{
			if (string.IsNullOrEmpty(item.ProductID))
			{
				Debug.LogError($"[PurchaseService] IAP ProductID is empty for item '{item.DisplayName}'.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

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

			// Purchase succeeded — grant rewards
			Debug.Log($"[PurchaseService] IAP purchase succeeded for '{item.ProductID}' (tx: {iapResult.TransactionId})");
			GrantRewards(item, immediateCredit);
			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
		}

		/// <summary>
		/// Grant rewards via the RewardService. Dispatches each reward to the
		/// appropriate RewardProvider based on its RewardType UID asset.
		/// </summary>
		private void GrantRewards(PurchasableItemDefinition item, bool immediateCredit)
		{
			if (!immediateCredit) return;

			List<RewardDefinition> rewards = new();

			if (item.Reward != null)
				rewards.Add(item.Reward);

			if (item.HasAnyBundle())
			{
				item.RewardBundle?.GetAllRewardsRecursive(rewards);
			}

			foreach (RewardDefinition reward in rewards)
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
				IAPFailureType.ExistingPurchasePending => PurchaseStatus.ErrorCode.IAPStoreError,
				_                                      => PurchaseStatus.ErrorCode.IAPUnknownError
			};
		}
	}
}
