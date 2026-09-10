using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
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

		public bool CanAfford(ICostInfo cost)
		{
			if (cost == null || cost.CostType.IsNone) return true;

			if (_providers.TryGetValue(cost.CostType, out ICostProvider provider))
			{
				return provider.CanAfford(cost);
			}

			Debug.LogWarning($"[CostService] No provider registered for cost type {UidDebugNames.Describe(cost.CostType)}. Defaulting to unaffordable.");
			return false;
		}

		public bool Deduct(ICostInfo cost)
		{
			if (cost == null || cost.CostType.IsNone) return true;

			if (_providers.TryGetValue(cost.CostType, out ICostProvider provider))
			{
				return provider.Deduct(cost);
			}

			Debug.LogWarning($"[CostService] No provider registered for cost type {UidDebugNames.Describe(cost.CostType)}. Cannot deduct.");
			return false;
		}

		public ICostProvider GetProvider(Uid costType)
		{
			return costType.IsSet && _providers.TryGetValue(costType, out ICostProvider provider) ? provider : null;
		}
	}
}
