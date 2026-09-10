using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Services.Rewards
{
	/// <summary>
	/// Dispatches reward granting to the IRewardProvider registered for the reward's type.
	/// </summary>
	public class RewardService : IRewardService
	{
		private readonly Dictionary<Uid, IRewardProvider> _providers = new();

		public void RegisterProvider(IRewardProvider provider)
		{
			if (provider == null)
			{
				Debug.LogWarning("[RewardService] Cannot register null provider.");
				return;
			}

			if (provider.RewardType.IsNone)
			{
				Debug.LogWarning("[RewardService] Cannot register a provider whose reward type has no identity.");
				return;
			}

			if (_providers.ContainsKey(provider.RewardType))
			{
				Debug.LogWarning($"[RewardService] Replacing existing provider for reward type {UidDebugNames.Describe(provider.RewardType)}.");
			}

			_providers[provider.RewardType] = provider;
		}

		public bool UnregisterProvider(IRewardProvider provider)
		{
			if (provider == null || provider.RewardType.IsNone) return false;

			if (_providers.TryGetValue(provider.RewardType, out IRewardProvider existing) && existing == provider)
			{
				return _providers.Remove(provider.RewardType);
			}

			return false;
		}

		public Result Grant(IReward reward)
		{
			if (reward == null) return Result.Fail(ErrorCode.NullArgument);
			if (reward.RewardType.IsNone) return Result.Fail(ErrorCode.NoIdentity);

			if (!_providers.TryGetValue(reward.RewardType, out IRewardProvider provider))
			{
				return Result.Fail(ErrorCode.NoProvider, UidDebugNames.Describe(reward.RewardType));
			}

			if (!provider.CanProvide(reward))
			{
				return Result.Fail(ErrorCode.RewardDeclined);
			}

			provider.GrantReward(reward);
			return Result.Ok;
		}

		public IRewardProvider GetProvider(Uid rewardType)
		{
			return rewardType.IsSet && _providers.TryGetValue(rewardType, out IRewardProvider provider) ? provider : null;
		}
	}
}
