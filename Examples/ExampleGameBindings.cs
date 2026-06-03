using System;
using AK.CoreDomain;
using AK.CoreDomain.Costs;
using AK.CoreDomain.Currency;
using AK.CoreDomain.Rewards;
using AK.Examples.Costs;
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
	/// Complete example of how to wire up the CostProvider/RewardProvider/PurchaseService
	/// system in your game's GameBindings (DI installer) using the updated Reflex API.
	///
	/// This replaces the old pattern where PurchaseService directly depended on GameModel.
	/// Now the game registers providers that know about game-specific systems,
	/// while the framework services remain completely decoupled.
	///
	/// Key principle: The framework doesn't know about currencies, inventory, or game state.
	/// Your CostProviders and RewardProviders bridge the gap.
	/// </summary>
	public class ExampleGameBindings : MonoBehaviour, IInstaller
	{
		[Header("Meta Data")]
		[SerializeField] private MetaDataRepository _metaDataRepository;

		[Header("Cost Type Assets — Create via Create → Gameplay/MetaData/Costs/CostType")]
		[Tooltip("The CostType asset that represents In-App Purchases. PurchaseService uses this to identify IAP items.")]
		[SerializeField] private CostType _iapCostType;

		[Tooltip("The CostType asset for soft currency (coins) purchases.")]
		[SerializeField] private CostType _softCurrencyCostType;

		[Tooltip("The CostType asset for hard currency (gems) purchases.")]
		[SerializeField] private CostType _hardCurrencyCostType;

		[Header("Cost Providers — Create via their CreateAssetMenu paths")]
		[SerializeField] private SoftCurrencyCostProvider _softCurrencyCostProvider;

		[Header("Reward Type Assets — Create via Create → Gameplay/MetaData/Rewards/RewardType")]
		[SerializeField] private RewardType _currencyRewardType;

		[Header("Reward Providers")]
		[SerializeField] private CurrencyRewardProvider _currencyRewardProvider;

		[Header("IAP")]
		[SerializeField] private IIAPService _iapService;

		public void InstallBindings(ContainerBuilder builder)
		{
			// ──────────────────────────────────────────────────────────────────
			// 1. REGISTER META DATA
			//    RegisterValue: registers a pre-existing object instance as a singleton.
			//    The object is resolved by all its contract types.
			// ──────────────────────────────────────────────────────────────────
			builder.RegisterValue(_metaDataRepository, new[] { typeof(MetaDataRepository), typeof(IMetaDataRepository) });

			// ──────────────────────────────────────────────────────────────────
			// 2. CREATE AND REGISTER COST SERVICE
			//    This is the new pattern — CostService dispatches to CostProviders
			//    based on CostType SO assets. No switch statements, no enums.
			// ──────────────────────────────────────────────────────────────────
			var costService = new CostService();

			// Register providers for each cost type your game supports.
			// Each provider knows how to check affordability and deduct costs
			// for its specific CostType.
			costService.RegisterProvider(_softCurrencyCostProvider);
			// costService.RegisterProvider(_hardCurrencyCostProvider);
			// costService.RegisterProvider(_adCostProvider);
			// costService.RegisterProvider(_staminaCostProvider);
			// ... add as many as your game needs

			builder.RegisterValue(costService, new[] { typeof(ICostService) });

			// ──────────────────────────────────────────────────────────────────
			// 3. CREATE AND REGISTER REWARD SERVICE
			//    Same pattern — RewardService dispatches to RewardProviders
			//    based on RewardType SO assets.
			// ──────────────────────────────────────────────────────────────────
			var rewardService = new RewardService();

			// Register providers for each reward type your game supports.
			rewardService.RegisterProvider(_currencyRewardProvider);
			// rewardService.RegisterProvider(_skinRewardProvider);
			// rewardService.RegisterProvider(_powerupRewardProvider);
			// ... add as many as your game needs

			builder.RegisterValue(rewardService, new[] { typeof(IRewardService) });

			// ──────────────────────────────────────────────────────────────────
			// 4. CREATE PURCHASE SERVICE
			//    Now takes ICostService + IRewardService + the IAP CostType.
			//    No GameModel dependency! The framework is fully decoupled.
			//
			//    Purchase flow:
			//    - If item.Cost.Type == _iapCostType → IAP flow (platform SDK)
			//    - Otherwise → costService.CanAfford() → costService.Deduct() → rewardService.GrantRewards()
			// ──────────────────────────────────────────────────────────────────
			var purchaseService = new PurchaseService(
				_iapService,
				costService,
				rewardService,
				_iapCostType  // The CostType SO that identifies IAP items
			);

			builder.RegisterValue(purchaseService, new[] { typeof(IPurchaseService) });

			// ──────────────────────────────────────────────────────────────────
			// 5. REGISTER IAP SERVICE
			// ──────────────────────────────────────────────────────────────────
			builder.RegisterValue(_iapService, new[] { typeof(IIAPService) });

			// ──────────────────────────────────────────────────────────────────
			// 6. REGISTER COST/REWARD TYPE ASSETS
			//    Useful if any code needs to resolve the IAP CostType via DI
			// ──────────────────────────────────────────────────────────────────
			builder.RegisterValue(_iapCostType, new[] { typeof(CostType) });

			// ──────────────────────────────────────────────────────────────────
			// 7. INITIALIZE PROVIDERS WITH RUNTIME DATA
			//    CostProviders and RewardProviders need access to your game's
			//    runtime model. Initialize them after the model is loaded.
			//    (In a real game, GameModel.Load() happens during BootState)
			//
			//    Example using RegisterFactory for GameModel:
			// ──────────────────────────────────────────────────────────────────
			// builder.RegisterFactory(container =>
			// {
			//     var gameModel = GameModel.Load();
			//     var metaDataRepo = container.Resolve<IMetaDataRepository>();
			//     gameModel.Initialize(metaDataRepo, out bool isFirstLaunch);
			//
			//     // Initialize providers with runtime data
			//     var coinsModel = gameModel.GetCurrencyModel(CurrencyType.Soft);
			//     _softCurrencyCostProvider.Init(coinsModel);
			//     _currencyRewardProvider.Init(gameModel);
			//
			//     return gameModel;
			// }, typeof(GameModel), Lifetime.Singleton, Resolution.Eager);
		}
	}
}
