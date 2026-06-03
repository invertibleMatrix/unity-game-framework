using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AK.CoreDomain.Rewards
{
	[Serializable]
	public class RewardBundle
	{
		public List<RewardDefinition> Rewards;

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
			// Prevent infinite recursion from circular references
			if (!visited.Add(this))
			{
				Debug.LogWarning($"Circular reference detected in RewardBundle");
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