using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Examples.Rewards
{
	/// <summary>
	/// Abstract base for reward providers. Each provider handles granting
	/// rewards for a specific RewardType. Extend this for each RewardType your game supports.
	/// </summary>
	public abstract class RewardProvider : MetaDataAsset, IRewardProvider
	{
		[Tooltip("The RewardType asset this provider handles. Used by RewardService for dispatch.")]
		public RewardType Type;

		Uid IRewardProvider.RewardType => Type != null ? Type.Id : Uid.None;

		/// <summary>
		/// Whether this provider can handle the given reward.
		/// Default: matches by reward type identity. Override for custom matching logic.
		/// </summary>
		public virtual bool CanProvide(IReward reward)
		{
			return reward != null && Type != null && reward.RewardType == Type.Id;
		}

		/// <summary>
		/// Grant the reward. Override in game-specific implementations — the framework
		/// doesn't dictate how rewards are applied. Your implementation can downcast
		/// IReward to access game-specific fields on the concrete definition type.
		/// </summary>
		public abstract void GrantReward(IReward reward);
	}
}
