using System;
using System.Collections.Generic;
using System.Linq;
using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Rewards
{
	[CreateAssetMenu(fileName = "GachaBundle", menuName = "AK/MetaData/Rewards/GachaBundle")]
	public class GachaBundle : MetaDataAsset
	{
		[Serializable]
		public struct WeightedReward
		{
			public RewardDefinition Reward;

			[Tooltip("A higher weight means a higher chance of being selected.")]
			public float Weight;
		}

		[Tooltip("The pool of all possible rewards that can be dropped.")]
		public List<WeightedReward> PossibleRewards;

		[Tooltip("The minimum number of items that will be dropped.")]
		public int MinDrops = 1;

		[Tooltip("The maximum number of items that will be dropped.")]
		public int MaxDrops = 1;

		/// <summary>
		/// Evaluates the weighted rewards and returns a list of chosen rewards.
		/// </summary>
		/// <returns>A list of RewardDefinition objects.</returns>
		public List<RewardDefinition> EvaluateRewards()
		{
			var chosenRewards = new List<RewardDefinition>();
			if (PossibleRewards == null || PossibleRewards.Count == 0)
			{
				return chosenRewards;
			}

			int numberOfDrops = UnityEngine.Random.Range(MinDrops, MaxDrops + 1);
			float totalWeight = PossibleRewards.Sum(r => r.Weight);

			for (int i = 0; i < numberOfDrops; i++)
			{
				float randomValue = UnityEngine.Random.Range(0, totalWeight);
				float currentWeight = 0;

				foreach (var weightedReward in PossibleRewards)
				{
					currentWeight += weightedReward.Weight;
					if (randomValue <= currentWeight)
					{
						if (weightedReward.Reward.Bundle != null && weightedReward.Reward.Bundle.Rewards.Count > 0)
						{
							chosenRewards.AddRange(weightedReward.Reward.GetAllRewardsFromDefinition());
						}
						else
						{
							chosenRewards.Add(weightedReward.Reward);
						}

						break;
					}
				}
			}

			return chosenRewards;
		}
	}
}