using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Provider contract for reward dispatch. Services only depend on this interface,
	/// not on the concrete RewardProvider ScriptableObject subclass.
	/// </summary>
	public interface IRewardProvider
	{
		/// <summary>Identity of the reward type this provider handles. Used for dispatch.</summary>
		Uid RewardType { get; }

		/// <summary>Whether this provider can handle the given reward.</summary>
		bool CanProvide(IReward reward);

		/// <summary>
		/// Grant the reward. The provider may downcast IReward to access
		/// game-specific fields on the concrete definition type.
		/// </summary>
		void GrantReward(IReward reward);
	}
}
