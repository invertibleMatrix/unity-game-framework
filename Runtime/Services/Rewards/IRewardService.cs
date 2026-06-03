using AK.Core;
using AK.CoreDomain;

namespace AK.Services.Rewards
{
	/// <summary>
	/// Dispatches reward granting to the appropriate IRewardProvider based on RewardTypeUID.
	/// </summary>
	public interface IRewardService
	{
		/// <summary>
		/// Register a reward provider. Replaces any existing provider for the same RewardTypeUID.
		/// </summary>
		void RegisterProvider(IRewardProvider provider);

		/// <summary>
		/// Remove a registered provider.
		/// </summary>
		bool UnregisterProvider(IRewardProvider provider);

		/// <summary>
		/// Attempt to grant a reward using the registered provider for its RewardTypeUID.
		/// Returns true if a provider was found and the reward was granted.
		/// </summary>
		bool TryGrantReward(IReward reward);

		/// <summary>
		/// Get the provider for a given UID, or null if none registered.
		/// </summary>
		IRewardProvider GetProvider(UID rewardTypeUID);
	}
}
