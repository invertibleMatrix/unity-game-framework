using AK.Core;
using AK.CoreDomain;
using AK.Kernel.Results;

namespace AK.Services.Rewards
{
	/// <summary>
	/// Dispatches reward granting to the IRewardProvider registered for the reward's type.
	/// </summary>
	public interface IRewardService
	{
		/// <summary>Register a reward provider. Replaces any existing provider for the same reward type.</summary>
		void RegisterProvider(IRewardProvider provider);

		/// <summary>Remove a registered provider.</summary>
		bool UnregisterProvider(IRewardProvider provider);

		/// <summary>
		/// Grants a reward through the provider registered for its type.
		/// <see cref="ErrorCode.NullArgument"/> / <see cref="ErrorCode.NoIdentity"/> for a bad reward,
		/// <see cref="ErrorCode.NoProvider"/> when nothing is registered for the type,
		/// <see cref="ErrorCode.RewardDeclined"/> when the provider reports it cannot provide it.
		/// </summary>
		Result Grant(IReward reward);

		/// <summary>Get the provider for a reward type, or null if none registered.</summary>
		IRewardProvider GetProvider(Uid rewardType);
	}
}
