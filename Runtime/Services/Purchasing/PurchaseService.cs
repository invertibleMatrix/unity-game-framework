using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameplayCore.MetaData;
using GameplayCore.MetaData.Costs;
using GameplayCore.MetaData.Currency;
using GameplayCore.MetaData.Rewards;
using GameplayCore.Models;
using UnityEngine;

namespace AK.Services
{
	public class PurchaseService : IPurchaseService
	{
		private IMetaDataRepository _metaDataRepository;
		private GameModel           _gameModel;
		private IIAPService         _iapService;
		private CurrencyMeta        _currencyMeta;

		public IIAPService IAPService => _iapService;

		public PurchaseService(IMetaDataRepository metaDataRepository, GameModel gameModel, IIAPService iapService)
		{
			_metaDataRepository = metaDataRepository;
			_gameModel = gameModel;
			_iapService = iapService;
		}

		public async UniTask<PurchaseStatus> Purchase(PurchasableItemDefinition item, bool immediateCredit)
		{
			var costType = item.CostType;
			CurrencyType currencyType = item.CurrencyType;

			if (costType == CostType.InAppPurchase)
			{
				return await HandleInAppPurchase(item, immediateCredit);
			}
			else
			{
				switch (costType)
				{
					case CostType.None:
						Debug.LogError($"Invalid Cost for {item.DisplayName}");
						return new PurchaseStatus()
						{
							Error = PurchaseStatus.ErrorCode.InternalError
						};
					case CostType.Free:
						break;
					case CostType.Currency:
						var currencyDefinition = _metaDataRepository.CurrencyMeta.GetCurrencyByID(item.CurrencyUID);
						var currencyModel = _gameModel.GetCurrencyModel(currencyDefinition);
						if (currencyModel == null)
						{
							Debug.LogWarning($"Trying to purchase item with CoinModel that is not present!");
							return new PurchaseStatus()
							{
								Error = PurchaseStatus.ErrorCode.InternalError
							};
						}

						if (currencyModel.Amount >= item.Price)
						{
							List<RewardDefinition> rewards = new();
							rewards.Add(item.Reward);
							if (item.HasAnyBundle())
							{
								if (item.RewardBundle?.Rewards?.Count > 0)
								{
									item.RewardBundle.GetAllRewardsRecursive(rewards);
								}
							}

							currencyModel.Deduct(item.Price);
							_gameModel.AppendPurchasedItemTransaction(rewards.Select(x => x.UniqueID).ToList());
							if (immediateCredit)
							{
								_gameModel.CreditPendingTransactions(TransactionType.PurchasableItem);
							}

							return new PurchaseStatus()
							{
								Error = PurchaseStatus.ErrorCode.None
							};
						}

						return new PurchaseStatus()
						{
							Error = PurchaseStatus.ErrorCode.InsufficientCurrency
						};
					case CostType.Gem:
						break;
					case CostType.Ad:
						break;
					case CostType.Resource:
						break;
				}
			}

			return default;
		}

		private async UniTask<PurchaseStatus> HandleInAppPurchase(PurchasableItemDefinition item, bool immediateCredit)
		{
			if (string.IsNullOrEmpty(item.ProductID))
			{
				Debug.LogError($"[PurchaseService] IAPProductId is empty for item '{item.DisplayName}'.");
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

			// Purchase succeeded — collect rewards
			Debug.Log($"[PurchaseService] IAP purchase succeeded for '{item.ProductID}' (tx: {iapResult.TransactionId})");

			List<RewardDefinition> rewards = new();
			if (item.Reward != null)
				rewards.Add(item.Reward);

			if (item.HasAnyBundle())
			{
				item.RewardBundle?.GetAllRewardsRecursive(rewards);
			}

			if (rewards.Count > 0)
			{
				_gameModel.AppendPurchasedItemTransaction(rewards.Select(x => x.UniqueID).ToList());
				if (immediateCredit)
				{
					_gameModel.CreditPendingTransactions(TransactionType.PurchasableItem);
				}

				//Always Credit Currency for In Apps immediately so that it can be used in place if needed

				foreach (RewardDefinition reward in rewards)
				{
					if (reward.Type == RewardType.Currency)
					{
						_gameModel.CreditPendingReward(TransactionType.PurchasableItem, reward);
					}
				}
			}

			return new PurchaseStatus { Error = PurchaseStatus.ErrorCode.None };
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