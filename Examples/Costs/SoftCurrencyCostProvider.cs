using AK.Core;
using AK.CoreDomain.Costs;
using AK.CoreDomain.Currency;
using AK.Examples.Rewards;
using UnityEngine;

namespace AK.Examples.Costs
{
	/// <summary>
	/// Example CostProvider that handles soft currency (coins) costs.
	/// Checks and deducts from a CurrencyModel at runtime.
	///
	/// Setup:
	/// 1. Create a CostType asset named "SoftCurrency" (Create → Gameplay/MetaData/Costs/CostType)
	/// 2. Create this provider asset (Create → Examples/Costs/SoftCurrencyCostProvider)
	/// 3. Assign the "SoftCurrency" CostType to the provider's Type field
	/// 4. Register this provider in your GameBindings (see ExampleGameBindings.cs)
	/// </summary>
	[CreateAssetMenu(fileName = "SoftCurrencyCostProvider", menuName = "Examples/Costs/SoftCurrencyCostProvider")]
	public class SoftCurrencyCostProvider : CostProvider
	{
		/// <summary>
		/// The CurrencyDefinition this provider checks against.
		/// Set in the Inspector — references your "Coins" CurrencyDefinition asset.
		/// </summary>
		[Tooltip("The currency definition this provider deducts from")]
		public CurrencyDefinition CurrencyDefinition;

		/// <summary>
		/// Reference to the game's currency model. Set at runtime via Init().
		/// In a real game, you'd inject this via DI or set it during bootstrap.
		/// </summary>
		private CurrencyModel _currencyModel;

		/// <summary>
		/// Initialize this provider with the runtime currency model.
		/// Called from your bootstrap/game bindings code.
		/// </summary>
		public void Init(CurrencyModel currencyModel)
		{
			_currencyModel = currencyModel;
		}

		public override bool CanAfford(CostOption costOption)
		{
			if (_currencyModel == null)
			{
				Debug.LogWarning("[SoftCurrencyCostProvider] CurrencyModel not initialized. Call Init() first.");
				return false;
			}

			return _currencyModel.Amount >= costOption.Amount;
		}

		public override bool Deduct(CostOption costOption)
		{
			if (_currencyModel == null)
			{
				Debug.LogWarning("[SoftCurrencyCostProvider] CurrencyModel not initialized. Call Init() first.");
				return false;
			}

			if (_currencyModel.Amount < costOption.Amount)
			{
				Debug.LogWarning($"[SoftCurrencyCostProvider] Insufficient funds. Have: {_currencyModel.Amount}, Need: {costOption.Amount}");
				return false;
			}

			_currencyModel.Deduct(costOption.Amount);
			Debug.Log($"[SoftCurrencyCostProvider] Deducted {costOption.Amount} coins. Remaining: {_currencyModel.Amount}");
			return true;
		}
	}
}
