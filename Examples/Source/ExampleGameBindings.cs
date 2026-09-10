using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Ads;
using AK.CoreDomain.Notifications;
using AK.CoreDomain.Transactions;
using AK.Examples.Costs;
using AK.Examples.Models;
using AK.Examples.Rewards;
using AK.Examples.Store;
using AK.Services;
using AK.Services.Costs;
using AK.Services.Rewards;
using AK.Services.Transactions;
using AK.Systems;
using Reflex.Core;
using UnityEngine;

namespace AK.Examples
{
	/// <summary>
	/// Example DI installer showing the full bootstrap pattern:
	/// meta registration, registry initialization, game model loading, provider
	/// initialization, and optional IAP.
	///
	/// Order matters: registries are initialized before the game model loads so
	/// persisted identities resolve, and the same repository is handed to services
	/// that must turn persisted identities back into assets.
	/// </summary>
	public class ExampleGameBindings : MonoBehaviour, IInstaller
	{
		[Header("Meta Data")]
		[SerializeField] private MetaDataRepository _metaDataRepository;

		[SerializeField] private AppStateMachine   _appStateMachine;
		[SerializeField] private BootState         _bootState;
		[SerializeField] private MainMenuState     _mainMenuState;
		[SerializeField] private CameraSystem      _cameraSystem;
		[SerializeField] private UISystem          _uiSystem;

		[Header("Custom Meta — register game-specific domains")]
		[SerializeField] private AdsMeta           _adsMeta;
		[SerializeField] private ShopMeta          _shopMeta;
		[SerializeField] private CurrencyMeta      _currencyMeta;
		[SerializeField] private NotificationsMeta _notificationsMeta;

		[Header("Cost Providers")]
		[SerializeField] private SoftCurrencyCostProvider _softCurrencyCostProvider;

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
			if (_adsMeta != null) _metaDataRepository.RegisterMeta(_adsMeta);
			if (_shopMeta != null) _metaDataRepository.RegisterMeta(_shopMeta);
			if (_currencyMeta != null) _metaDataRepository.RegisterMeta(_currencyMeta);
			if (_notificationsMeta != null) _metaDataRepository.RegisterMeta(_notificationsMeta);

			_metaDataRepository.InitializeRegistries();

			builder.RegisterValue(_metaDataRepository, new[] { typeof(MetaDataRepository), typeof(IMetaDataRepository), typeof(IUidResolver) });

			GameModel = ExampleGameModel.Load();
			GameModel.SetMetaDataRepository(_metaDataRepository);
			GameModel.Initialize(out bool isFirstLaunch);
			builder.RegisterValue(GameModel, new[] { typeof(ExampleGameModel) });

			var softCurrency = GameModel.GetOrCreateCurrencyModel(_softCurrencyCostProvider.CurrencyDefinition);
			_softCurrencyCostProvider.Init(softCurrency);
			var costService = new CostService();
			costService.RegisterProvider(_softCurrencyCostProvider);
			builder.RegisterValue(costService, new[] { typeof(ICostService) });

			_currencyRewardProvider.Init(GameModel);
			var rewardService = new RewardService();
			rewardService.RegisterProvider(_currencyRewardProvider);
			builder.RegisterValue(rewardService, new[] { typeof(IRewardService) });

			var transactionService = new TransactionService(rewardService, _metaDataRepository, _metaDataRepository.Redirects);
			builder.RegisterValue(transactionService, new[] { typeof(ITransactionService) });

			var purchaseService = new PurchaseService(costService, rewardService, null, transactionService);
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
