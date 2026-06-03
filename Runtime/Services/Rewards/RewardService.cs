using System.Collections.Generic;
using AK.CoreDomain;
using AK.CoreDomain.Rewards;
using UnityEngine;

namespace AK.Services.Rewards
{
	/// <summary>
	/// Dispatches reward granting to the appropriate RewardProvider based on RewardType.
	/// </summary>
	public class RewardService : IRewardService
	{
		private readonly Dictionary<RewardType, RewardProvider> _providers = new();

		/// <inheritdoc />
		public void RegisterProvider(RewardProvider provider)
		{
			if (provider == null)
			{
				Debug.LogWarning("[RewardService] Cannot register null provider.");
				return;
			}

			if (provider.Type == null)
			{
				Debug.LogWarning($"[RewardService] Cannot register provider '{provider.name}' with null RewardType.");
				return;
			}

			if (_providers.ContainsKey(provider.Type))
			{
				Debug.LogWarning($"[RewardService] Replacing existing provider for RewardType '{provider.Type.name}'.");
			}

			_providers[provider.Type] = provider;
		}

		/// <inheritdoc />
		public bool UnregisterProvider(RewardProvider provider)
		{
			if (provider?.Type == null) return false;

			if (_providers.TryGetValue(provider.Type, out var existing) && existing == provider)
			{
				return _providers.Remove(provider.Type);
			}

			return false;
		}

		/// <inheritdoc />
		public bool TryGrantReward(RewardDefinition reward)
		{
			if (reward?.Type == null) return false;

			if (_providers.TryGetValue(reward.Type, out var provider))
			{
				provider.GrantReward(reward);
				return true;
			}

			Debug.LogWarning($"[RewardService] No provider registered for RewardType '{reward.Type.name}'. " +
			                 $"Register a RewardProvider for this type before granting rewards.");
			return false;
		}

		/// <inheritdoc />
		public RewardProvider GetProvider(RewardType type)
		{
			return type != null && _providers.TryGetValue(type, out var provider) ? provider : null;
		}
	}
}
