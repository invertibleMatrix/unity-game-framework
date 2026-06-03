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
	/// IAP is optional — pass null for iapService in games without IAP.
	/// IAP items are identified by having a non-empty ProductID.
	/// </summary>
	public class PurchaseService : IPurchaseService
	{
		private readonly IIAPService    _iapService;
		private readonly ICostService   _costService;
		private readonly IRewardService _rewardService;

		public IIAPService IAPService => _iapService;

		/// <summary>
		/// Create a PurchaseService.
		/// </summary>
		/// <param name="costService">The cost service for checking affordability and deducting costs.</param>
		/// <param name="rewardService">The reward service for granting purchase rewards.</param>
		/// <param name="iapService">Optional IAP service for platform store operations. Pass null for games without IAP.</param>
		public PurchaseService(
			ICostService costService,
			IRewardService rewardService,
			IIAPService iapService = null)
		{
			_costService   = costService;
			_rewardService = rewardService;
			_iapService    = iapService;
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

			// IAP flow — when the item has a ProductID and IAP is available
			if (!string.IsNullOrEmpty(item.ProductID) && _iapService != null)
			{
				return await HandleInAppPurchase(item, immediateCredit);
			}

			// All other purchases delegate to CostService → CostProvider
			if (!_costService.CanAfford(item.Cost))
			{
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InsufficientCurrency };
			}

			if (!_costService.Deduct(item.Cost))
			{
				Debug.LogWarning($"[PurchaseService] Failed to deduct cost for item '{item.DisplayName}'.");
				return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.InternalError };
			}

			GrantRewards(item, immediateCredit);
			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
		}

		private async UniTask<PurchaseStatus> HandleInAppPurchase(PurchasableItemDefinition item, bool immediateCredit)
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
			GrantRewards(item, immediateCredit);
			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
		}

		/// <summary>
		/// Grant rewards via the RewardService.
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
