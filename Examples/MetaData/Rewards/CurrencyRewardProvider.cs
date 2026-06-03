using UnityEngine;
using IReward = AK.CoreDomain.IReward;

namespace AK.Examples.Rewards
{
	/// <summary>
	/// Example RewardProvider that grants currency rewards via the game model.
	/// Demonstrates downcasting IReward to RewardDefinition to access game-specific fields.
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyRewardProvider", menuName = "AK/Examples/Rewards/CurrencyRewardProvider")]
	public class CurrencyRewardProvider : RewardProvider
	{
		private ExampleGameModel _gameModel;

		/// <summary>
		/// Initialize with the game model. Call during boot after the model is loaded.
		/// </summary>
		public void Init(ExampleGameModel gameModel)
		{
			_gameModel = gameModel;
		}

		public override void GrantReward(IReward reward)
		{
			if (_gameModel == null)
			{
				Debug.LogWarning("[CurrencyRewardProvider] GameModel not initialized. Call Init() first.");
				return;
			}

			// Downcast to access game-specific fields on RewardDefinition
			if (reward is not RewardDefinition rd)
			{
				Debug.LogWarning("[CurrencyRewardProvider] Expected RewardDefinition, got " + reward.GetType().Name);
				return;
			}

			if (rd.CurrencyDefinition == null)
			{
				Debug.LogWarning("[CurrencyRewardProvider] Reward has no CurrencyDefinition assigned.");
				return;
			}

			var currencyModel = _gameModel.GetCurrencyModel(rd.CurrencyDefinition);
			if (currencyModel == null)
			{
				Debug.LogWarning($"[CurrencyRewardProvider] No CurrencyModel found for '{rd.CurrencyDefinition.DisplayName}'.");
				return;
			}

			currencyModel.Add(rd.Amount);
			Debug.Log($"[CurrencyRewardProvider] Granted {rd.Amount} {rd.CurrencyDefinition.DisplayName}.");
		}
	}
}
