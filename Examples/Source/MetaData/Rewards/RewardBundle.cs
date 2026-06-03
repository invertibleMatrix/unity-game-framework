using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Examples.Rewards
{
	[Serializable]
	public class RewardBundle
	{
		public List<RewardDefinition> Rewards;

		/// <summary>
		/// Collect all leaf rewards as IReward (interface-based, used by services).
		/// </summary>
		public void CollectRewards(List<IReward> rewards)
		{
			var visited = new HashSet<RewardBundle>();
			CollectRewardsInternal(rewards, visited);
		}

		private void CollectRewardsInternal(List<IReward> rewards, HashSet<RewardBundle> visited)
		{
			if (!visited.Add(this))
			{
				Debug.LogWarning("Circular reference detected in RewardBundle");
				return;
			}

			foreach (RewardDefinition reward in Rewards)
			{
				if (reward.Bundle != null && reward.Bundle.Rewards.Count > 0)
				{
					reward.Bundle.CollectRewardsInternal(rewards, visited);
				}
				else
				{
					rewards.Add(reward);
				}
			}
		}

		public void GetAllRewardsRecursive(in List<RewardDefinition> rewards)
		{
			var visited = new HashSet<RewardBundle>();
			GetAllFixedRewardsInternal(rewards, visited);
		}

		public List<RewardDefinition> GetAllRewardsRecursive()
		{
			List<RewardDefinition> rewards = new();
			var visited = new HashSet<RewardBundle>();
			GetAllFixedRewardsInternal(rewards, visited);

			return rewards;
		}

		private void GetAllFixedRewardsInternal(in List<RewardDefinition> rewards, HashSet<RewardBundle> visited)
		{
			if (!visited.Add(this))
			{
				Debug.LogWarning("Circular reference detected in RewardBundle");
				return;
			}

			foreach (RewardDefinition reward in Rewards)
			{
				if (reward.Bundle != null && reward.Bundle.Rewards.Count > 0)
				{
					reward.Bundle.GetAllFixedRewardsInternal(rewards, visited);
				}
				else
				{
					rewards.Add(reward);
				}
			}
		}
	}
}