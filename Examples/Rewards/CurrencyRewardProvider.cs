using AK.CoreDomain.Rewards;
using AK.CoreDomain.Currency;
using UnityEngine;

namespace AK.Examples.Rewards
{
	/// <summary>
	/// Example RewardProvider that grants currency rewards.
	/// When a RewardDefinition with Type matching this provider's Type is granted,
	/// this provider adds the specified amount to the matching CurrencyModel.
	///
	/// Setup:
	/// 1. Create a RewardType asset named "CurrencyReward" (Create → Gameplay/MetaData/Rewards/RewardType)
	/// 2. Create this provider asset (Create → Examples/Rewards/CurrencyRewardProvider)
	/// 3. Assign the "CurrencyReward" RewardType to the provider's Type field
	/// 4. Register this provider in your GameBindings (see ExampleGameBindings.cs)
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyRewardProvider", menuName = "Examples/Rewards/CurrencyRewardProvider")]
	public class CurrencyRewardProvider : RewardProvider
	{
		/// <summary>
		/// The game's model reference. Set at runtime via Init().
		/// In a real game, you'd inject this via DI.
		/// </summary>
		private GameModel _gameModel;

		/// <summary>
		/// Initialize with the runtime game model.
		/// </summary>
		public void Init(GameModel gameModel)
		{
			_gameModel = gameModel;
		}

		public override void GrantReward(RewardDefinition reward)
		{
			if (_gameModel == null)
			{
				Debug.LogWarning("[CurrencyRewardProvider] GameModel not initialized. Call Init() first.");
				return;
			}

			if (reward.CurrencyDefinition == null)
			{
				Debug.LogWarning($"[CurrencyRewardProvider] Reward '{reward.DisplayName}' has no CurrencyDefinition assigned.");
				return;
			}

			var currencyModel = _gameModel.GetCurrencyModel(reward.CurrencyDefinition);
			if (currencyModel == null)
			{
				Debug.LogWarning($"[CurrencyRewardProvider] No CurrencyModel found for '{reward.CurrencyDefinition.DisplayName}'.");
				return;
			}

			currencyModel.Add(reward.Amount);
			Debug.Log($"[CurrencyRewardProvider] Granted {reward.Amount} {reward.CurrencyDefinition.DisplayName}. Total: {currencyModel.Amount}");
		}
	}

	/// <summary>
	/// Stub GameModel class for the example. In a real game, this would be
	/// your actual GameModel from GameplayCore.Models.
	/// </summary>
	public class GameModel
	{
		public CurrencyModel GetCurrencyModel(CurrencyDefinition definition) => null;
	}
}
