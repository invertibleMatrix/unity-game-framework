using System.Collections.Generic;
using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Minimal contract for reward dispatch. Services only depend on this interface,
	/// not on concrete RewardDefinition subclasses. Game definitions implement this
	/// to be compatible with IRewardService without the service knowing their type.
	/// </summary>
	public interface IReward
	{
		/// <summary>
		/// The UID of the RewardType SO asset used for provider dispatch.
		/// Maps to RewardType (which extends UID) in the default implementation.
		/// </summary>
		UID RewardTypeUID { get; }

		/// <summary>
		/// Collect all leaf rewards from this reward (flattens bundles recursively).
		/// Used by PurchaseService to grant all rewards from a single purchasable item.
		/// </summary>
		void CollectRewards(List<IReward> rewards);
	}
}
