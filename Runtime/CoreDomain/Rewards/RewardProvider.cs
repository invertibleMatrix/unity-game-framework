using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Rewards
{
	/// <summary>
	/// Abstract base for reward providers. Each provider handles granting
	/// rewards for a specific RewardType. Extend this for each RewardType your game supports.
	/// </summary>
	public abstract class RewardProvider : MetaDataAsset
	{
		[Tooltip("The RewardType UID asset this provider handles. Used by RewardService for dispatch.")]
		public RewardType Type;

		/// <summary>
		/// Whether this provider can handle the given reward definition.
		/// Default: matches by RewardType reference equality.
		/// Override for custom matching logic.
		/// </summary>
		public virtual bool CanProvide(RewardDefinition reward)
		{
			return reward != null && reward.Type == Type;
		}

		/// <summary>
		/// Grant the reward. Override in game-specific implementations — the framework
		/// doesn't dictate how rewards are applied. Your implementation can access
		/// whatever game-specific systems it needs (game model, inventory, unlocks, etc.).
		/// </summary>
		public abstract void GrantReward(RewardDefinition reward);
	}
}
