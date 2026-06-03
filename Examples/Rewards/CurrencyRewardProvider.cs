using AK.CoreDomain;
using AK.CoreDomain.Currency;
using AK.CoreDomain.Rewards;
using AK.Examples;
using UnityEngine;

namespace AK.Examples.Rewards
{
	/// <summary>
	/// Example RewardProvider that grants currency rewards via the game model.
	/// </summary>
	[CreateAssetMenu(fileName = "CurrencyRewardProvider", menuName = "Game/Rewards/CurrencyRewardProvider")]
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

		public override void GrantReward(RewardDefinition reward)
		{
			if (_gameModel == null)
			{
				Debug.LogWarning("[CurrencyRewardProvider] GameModel not initialized. Call Init() first.");
				return;
			}

			if (reward.CurrencyDefinition == null)
			{
				Debug.LogWarning("[CurrencyRewardProvider] Reward has no CurrencyDefinition assigned.");
				return;
			}

			var currencyModel = _gameModel.GetCurrencyModel(reward.CurrencyDefinition);
			if (currencyModel == null)
			{
				Debug.LogWarning($"[CurrencyRewardProvider] No CurrencyModel found for '{reward.CurrencyDefinition.DisplayName}'.");
				return;
			}

			currencyModel.Add(reward.Amount);
			Debug.Log($"[CurrencyRewardProvider] Granted {reward.Amount} {reward.CurrencyDefinition.DisplayName}.");
		}
	}
}
