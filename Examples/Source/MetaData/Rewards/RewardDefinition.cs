using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.Examples.Currency;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.Examples.Rewards
{
	[CreateAssetMenu(fileName = "RewardDefinition", menuName = "AK/MetaData/Rewards/RewardDefinition")]
	public class RewardDefinition : MetaDataAsset, IReward
	{
		[Tooltip("The type of this reward. This determines which fields below are used.")]
		public RewardType Type;

		[Tooltip("The amount for Coin, Star, or Powerup rewards.")]
		public int Amount;

		[Header("Bundle Reward")] [Tooltip("Used only if Type is Bundle. Defines a fixed list of rewards inside.")]
		public RewardBundle Bundle;

		[Header("Gacha Bundle Reward")] [Tooltip("Used only if Type is Gacha Bundle. Defines a probabilistic list of rewards inside.")] [InlineEditor]
		public GachaBundle GachaBundle;

		[Header("Unlockable Reward")]
		[Tooltip("Used only if Type is Unlockable. A unique ID for the item or feature to unlock (e.g., 'Skins or stuff').")]
		public string UnlockableID;

		[Header("Currency Reward")] [Tooltip("Used only if Type is Currency. A reference to the Currency to be granted")]
		public CurrencyDefinition CurrencyDefinition;

		[Header("Subscription Reward")] [Tooltip("Used only if Type is Subscription. A reference to the SubscriptionReward to be granted")]
		public SubscriptionReward SubscriptionReward;

		// IReward explicit implementation
		UID IReward.RewardTypeUID => Type;

		/// <summary>
		/// Collect all leaf rewards from this definition (flattens bundles recursively).
		/// </summary>
		public void CollectRewards(List<IReward> rewards)
		{
			if (Bundle != null && Bundle.Rewards.Count > 0)
			{
				Bundle.CollectRewards(rewards);
			}
			else
			{
				rewards.Add(this);
			}
		}

		public List<RewardDefinition> GetAllRewardsFromDefinition()
		{
			List<RewardDefinition> rewards = new();
			if (Bundle != null && Bundle.Rewards.Count > 0)
			{
				rewards.AddRange(Bundle.GetAllRewardsRecursive());
			}
			else
			{
				rewards.Add(this);
			}

			return rewards;
		}
	}
}
