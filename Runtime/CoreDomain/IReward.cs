using System.Collections.Generic;
using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Minimal contract for reward dispatch. Services only depend on this interface,
	/// not on concrete RewardDefinition subclasses.
	/// </summary>
	public interface IReward
	{
		/// <summary>Identity of the reward type used for provider dispatch.</summary>
		Uid RewardType { get; }

		/// <summary>
		/// Collect all leaf rewards from this reward (flattens bundles recursively).
		/// Used by PurchaseService to grant all rewards from a single purchasable item.
		/// </summary>
		void CollectRewards(List<IReward> rewards);
	}
}
