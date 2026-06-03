using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Ads;
using AK.Examples.Costs;
using AK.Examples.Currency;
using AK.CoreDomain.Notifications;
using AK.Examples.Rewards;
using AK.Examples.Store;
using AK.Examples.Costs;
using AK.Examples.Models;
using AK.Examples.Rewards;
using AK.Services;
using AK.Services.Costs;
using AK.Services.Rewards;
using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using AK.Systems;

namespace AK.Examples
{
	/// <summary>
	/// Example DI installer showing the full bootstrap pattern:
	/// game model loading, provider initialization, meta registration, and optional IAP.
	/// </summary>
	public class ExampleGameBindings : MonoBehaviour, IInstaller
	{
		[Header("Meta Data")]
		[SerializeField] private MetaDataRepository _metaDataRepository;

		[SerializeField] private AppStateMachine   _appStateMachine;
		[SerializeField] private BootState         _bootState;
		[SerializeField] private MainMenuState     _mainMenuState;
		[SerializeField] private CameraSystem      _cameraSystem;
		[SerializeField] private UISystem _uiSystem;
		
		
		[Header("Custom Meta — register game-specific domains")]
		[SerializeField] private AdsMeta _adsMeta;
		[SerializeField] private ShopMeta _shopMeta;
		[SerializeField] private NotificationsMeta _notificationsMeta;
		
		[Header("Cost Type Assets")]
		[SerializeField] private CostType _softCurrencyCostType;

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
			// Meta Data — register custom domains before initializing
			if (_adsMeta != null) _metaDataRepository.RegisterMeta(_adsMeta);
			if (_shopMeta != null) _metaDataRepository.RegisterMeta(_shopMeta);
			if (_notificationsMeta != null) _metaDataRepository.RegisterMeta(_notificationsMeta);

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
			var purchaseService = new PurchaseService(costService, rewardService, null);
			builder.RegisterValue(purchaseService, new[] { typeof(IPurchaseService) });


			builder.RegisterValue(_cameraSystem, new[] { typeof(ICameraSystem) });
			builder.RegisterValue(_uiSystem, new[] { typeof(IUISystem) });
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
