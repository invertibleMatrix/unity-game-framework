using AK.CoreDomain;
using AK.CoreDomain.Rewards;

namespace AK.Services.Rewards
{
	/// <summary>
	/// Dispatches reward granting to the appropriate RewardProvider based on RewardType.
	/// </summary>
	public interface IRewardService
	{
		/// <summary>
		/// Register a reward provider. Replaces any existing provider for the same RewardType.
		/// </summary>
		void RegisterProvider(RewardProvider provider);

		/// <summary>
		/// Remove a registered provider.
		/// </summary>
		bool UnregisterProvider(RewardProvider provider);

		/// <summary>
		/// Attempt to grant a reward using the registered provider for its RewardType.
		/// Returns true if a provider was found and the reward was granted.
		/// </summary>
		bool TryGrantReward(RewardDefinition reward);

		/// <summary>
		/// Get the provider for a given RewardType, or null if none registered.
		/// </summary>
		RewardProvider GetProvider(RewardType type);
	}
}
