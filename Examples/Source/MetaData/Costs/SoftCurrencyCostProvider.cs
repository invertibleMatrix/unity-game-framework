using AK.Core;
using AK.CoreDomain;
using AK.Examples.Costs;
using AK.Examples.Currency;
using AK.Examples.Models;
using UnityEngine;

namespace AK.Examples.Costs
{
	/// <summary>
	/// Example CostProvider that handles soft currency (coins) costs.
	/// Demonstrates downcasting ICostInfo to CostOption to access game-specific fields.
	///
	/// Setup:
	/// 1. Create a CostType asset named "SoftCurrency" (Create → AK/Gameplay/MetaData/Costs/CostType)
	/// 2. Create this provider asset (Create → AK/Examples/Costs/SoftCurrencyCostProvider)
	/// 3. Assign the "SoftCurrency" CostType to the provider's Type field
	/// 4. Register this provider in your GameBindings (see ExampleGameBindings.cs)
	/// </summary>
	[CreateAssetMenu(fileName = "SoftCurrencyCostProvider", menuName = "AK/Examples/Costs/SoftCurrencyCostProvider")]
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

		public override bool CanAfford(ICostInfo cost)
		{
			if (_currencyModel == null)
			{
				Debug.LogWarning("[SoftCurrencyCostProvider] CurrencyModel not initialized. Call Init() first.");
				return false;
			}

			return _currencyModel.Amount >= cost.Amount;
		}

		public override bool Deduct(ICostInfo cost)
		{
			if (_currencyModel == null)
			{
				Debug.LogWarning("[SoftCurrencyCostProvider] CurrencyModel not initialized. Call Init() first.");
				return false;
			}

			if (_currencyModel.Amount < cost.Amount)
			{
				Debug.LogWarning($"[SoftCurrencyCostProvider] Insufficient funds. Have: {_currencyModel.Amount}, Need: {cost.Amount}");
				return false;
			}

			_currencyModel.Deduct(cost.Amount);
			Debug.Log($"[SoftCurrencyCostProvider] Deducted {cost.Amount} coins. Remaining: {_currencyModel.Amount}");
			return true;
		}
	}
}
