using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Services.Rewards
{
	/// <summary>
	/// Dispatches reward granting to the appropriate IRewardProvider based on RewardTypeUID.
	/// </summary>
	public class RewardService : IRewardService
	{
		private readonly Dictionary<UID, IRewardProvider> _providers = new();

		/// <inheritdoc />
		public void RegisterProvider(IRewardProvider provider)
		{
			if (provider == null)
			{
				Debug.LogWarning("[RewardService] Cannot register null provider.");
				return;
			}

			if (provider.RewardTypeUID == null)
			{
				Debug.LogWarning($"[RewardService] Cannot register provider with null RewardTypeUID.");
				return;
			}

			if (_providers.ContainsKey(provider.RewardTypeUID))
			{
				Debug.LogWarning($"[RewardService] Replacing existing provider for RewardTypeUID '{provider.RewardTypeUID.name}'.");
			}

			_providers[provider.RewardTypeUID] = provider;
		}

		/// <inheritdoc />
		public bool UnregisterProvider(IRewardProvider provider)
		{
			if (provider?.RewardTypeUID == null) return false;

			if (_providers.TryGetValue(provider.RewardTypeUID, out var existing) && existing == provider)
			{
				return _providers.Remove(provider.RewardTypeUID);
			}

			return false;
		}

		/// <inheritdoc />
		public bool TryGrantReward(IReward reward)
		{
			if (reward?.RewardTypeUID == null) return false;

			if (_providers.TryGetValue(reward.RewardTypeUID, out var provider))
			{
				provider.GrantReward(reward);
				return true;
			}

			Debug.LogWarning($"[RewardService] No provider registered for RewardTypeUID '{reward.RewardTypeUID.name}'. " +
			                 $"Register an IRewardProvider for this type before granting rewards.");
			return false;
		}

		/// <inheritdoc />
		public IRewardProvider GetProvider(UID rewardTypeUID)
		{
			return rewardTypeUID != null && _providers.TryGetValue(rewardTypeUID, out var provider) ? provider : null;
		}
	}
}
