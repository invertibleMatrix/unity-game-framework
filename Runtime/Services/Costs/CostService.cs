using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using AK.Kernel.Results;
using UnityEngine;

namespace AK.Services.Costs
{
	/// <summary>
	/// Dispatches cost checking and deduction to the ICostProvider registered for the cost's type.
	/// </summary>
	public class CostService : ICostService
	{
		private readonly Dictionary<Uid, ICostProvider> _providers = new();

		public void RegisterProvider(ICostProvider provider)
		{
			if (provider == null)
			{
				Debug.LogWarning("[CostService] Cannot register null provider.");
				return;
			}

			if (provider.CostType.IsNone)
			{
				Debug.LogWarning("[CostService] Cannot register a provider whose cost type has no identity.");
				return;
			}

			if (_providers.ContainsKey(provider.CostType))
			{
				Debug.LogWarning($"[CostService] Replacing existing provider for cost type {UidDebugNames.Describe(provider.CostType)}.");
			}

			_providers[provider.CostType] = provider;
		}

		public bool UnregisterProvider(ICostProvider provider)
		{
			if (provider == null || provider.CostType.IsNone) return false;

			if (_providers.TryGetValue(provider.CostType, out ICostProvider existing) && existing == provider)
			{
				return _providers.Remove(provider.CostType);
			}

			return false;
		}

		public Result CanAfford(ICostInfo cost)
		{
			if (cost == null || cost.CostType.IsNone) return Result.Ok;

			if (!_providers.TryGetValue(cost.CostType, out ICostProvider provider))
			{
				return Result.Fail(ErrorCode.NoProvider, UidDebugNames.Describe(cost.CostType));
			}

			return provider.CanAfford(cost) ? Result.Ok : Result.Fail(ErrorCode.CannotAfford);
		}

		public Result Deduct(ICostInfo cost)
		{
			if (cost == null || cost.CostType.IsNone) return Result.Ok;

			if (!_providers.TryGetValue(cost.CostType, out ICostProvider provider))
			{
				return Result.Fail(ErrorCode.NoProvider, UidDebugNames.Describe(cost.CostType));
			}

			return provider.Deduct(cost) ? Result.Ok : Result.Fail(ErrorCode.DeductDeclined);
		}

		public ICostProvider GetProvider(Uid costType)
		{
			return costType.IsSet && _providers.TryGetValue(costType, out ICostProvider provider) ? provider : null;
		}
	}
}
