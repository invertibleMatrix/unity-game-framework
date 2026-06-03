using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Provider contract for reward dispatch. Services only depend on this interface,
	/// not on the concrete RewardProvider ScriptableObject subclass.
	/// Game implementations downcast IReward to their specific definition type as needed.
	/// </summary>
	public interface IRewardProvider
	{
		/// <summary>
		/// The UID of the RewardType this provider handles. Used for dispatch.
		/// </summary>
		UID RewardTypeUID { get; }

		/// <summary>
		/// Whether this provider can handle the given reward.
		/// </summary>
		bool CanProvide(IReward reward);

		/// <summary>
		/// Grant the reward. The provider may downcast IReward to access
		/// game-specific fields on the concrete definition type.
		/// </summary>
		void GrantReward(IReward reward);
	}
}
