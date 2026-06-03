using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Costs;
using AK.CoreDomain.Currency;
using AK.CoreDomain.Rewards;
using AK.Examples.Costs;
using AK.Examples.Models;
using AK.Examples.Rewards;
using AK.Services;
using AK.Services.Costs;
using AK.Services.Rewards;
using Reflex.Core;
using Reflex.Enums;
using UnityEngine;

namespace AK.Examples
{
	/// <summary>
	/// Example DI installer showing the full bootstrap pattern:
	/// game model loading, provider initialization, and optional IAP.
	/// </summary>
	public class ExampleGameBindings : MonoBehaviour, IInstaller
	{
		[Header("Meta Data")]
		[SerializeField] private MetaDataRepository _metaDataRepository;

		[Header("IAP (optional — leave null for games without IAP)")]
		[SerializeField] private IIAPService _iapService;

		[Header("Cost Type Assets")]
		[SerializeField] private CostType _softCurrencyCostType;
		[SerializeField] private CostType _hardCurrencyCostType;

		[Header("Cost Providers")]
		[SerializeField] private SoftCurrencyCostProvider _softCurrencyCostProvider;

		[Header("Reward Type Assets")]
		[SerializeField] private RewardType _currencyRewardType;

		[Header("Reward Providers")]
		[SerializeField] private CurrencyRewardProvider _currencyRewardProvider;

		[Header("Transaction Type Assets")]
		[SerializeField] private TransactionType _levelCompleteTransactionType;

		/// <summary>
		/// The loaded game model instance, available after InstallBindings.
		/// </summary>
		public ExampleGameModel GameModel { get; private set; }

		public void InstallBindings(ContainerBuilder builder)
		{
			// Meta Data
			builder.RegisterValue(_metaDataRepository, new[] { typeof(MetaDataRepository), typeof(IMetaDataRepository) });

			// Game Model — load from save, initialize
			GameModel = ExampleGameModel.Load();
			GameModel.SetMetaDataRepository(_metaDataRepository);
			GameModel.Initialize(out bool isFirstLaunch);
			builder.RegisterValue(GameModel, new[] { typeof(ExampleGameModel) });

			// Cost Service — init providers with the currency model from the game model
			var softCurrency = GameModel.GetCurrencyModel(_softCurrencyCostProvider.CurrencyDefinition);
			_softCurrencyCostProvider.Init(softCurrency);
			var costService = new CostService();
			costService.RegisterProvider(_softCurrencyCostProvider);
			builder.RegisterValue(costService, new[] { typeof(ICostService) });

			// Reward Service — init providers with game model before registering
			_currencyRewardProvider.Init(GameModel);
			var rewardService = new RewardService();
			rewardService.RegisterProvider(_currencyRewardProvider);
			builder.RegisterValue(rewardService, new[] { typeof(IRewardService) });

			// Purchase Service — iapService is optional (null = no IAP)
			var purchaseService = new PurchaseService(costService, rewardService, _iapService);
			builder.RegisterValue(purchaseService, new[] { typeof(IPurchaseService) });

			if (_iapService != null)
			{
				builder.RegisterValue(_iapService, new[] { typeof(IIAPService) });
			}
		}

		private void OnApplicationPause(bool pauseStatus)
		{
			if (pauseStatus) GameModel?.Commit();
		}

		private void OnApplicationQuit()
		{
			GameModel?.Commit();
		}
	}
}
