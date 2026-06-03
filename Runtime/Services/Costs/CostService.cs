using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Services.Costs
{
	/// <summary>
	/// Dispatches cost checking and deduction to the appropriate ICostProvider based on CostTypeUID.
	/// </summary>
	public class CostService : ICostService
	{
		private readonly Dictionary<UID, ICostProvider> _providers = new();

		/// <inheritdoc />
		public void RegisterProvider(ICostProvider provider)
		{
			if (provider == null)
			{
				Debug.LogWarning("[CostService] Cannot register null provider.");
				return;
			}

			if (provider.CostTypeUID == null)
			{
				Debug.LogWarning($"[CostService] Cannot register provider with null CostTypeUID.");
				return;
			}

			if (_providers.ContainsKey(provider.CostTypeUID))
			{
				Debug.LogWarning($"[CostService] Replacing existing provider for CostTypeUID '{provider.CostTypeUID.name}'.");
			}

			_providers[provider.CostTypeUID] = provider;
		}

		/// <inheritdoc />
		public bool UnregisterProvider(ICostProvider provider)
		{
			if (provider?.CostTypeUID == null) return false;

			if (_providers.TryGetValue(provider.CostTypeUID, out var existing) && existing == provider)
			{
				return _providers.Remove(provider.CostTypeUID);
			}

			return false;
		}

		/// <inheritdoc />
		public bool CanAfford(ICostInfo cost)
		{
			if (cost?.CostTypeUID == null) return true;

			if (_providers.TryGetValue(cost.CostTypeUID, out var provider))
			{
				return provider.CanAfford(cost);
			}

			Debug.LogWarning($"[CostService] No provider registered for CostTypeUID '{cost.CostTypeUID.name}'. " +
			                 $"Register an ICostProvider for this type. Defaulting to unaffordable.");
			return false;
		}

		/// <inheritdoc />
		public bool Deduct(ICostInfo cost)
		{
			if (cost?.CostTypeUID == null) return true;

			if (_providers.TryGetValue(cost.CostTypeUID, out var provider))
			{
				return provider.Deduct(cost);
			}

			Debug.LogWarning($"[CostService] No provider registered for CostTypeUID '{cost.CostTypeUID.name}'. " +
			                 $"Cannot deduct. Register an ICostProvider for this type.");
			return false;
		}

		/// <inheritdoc />
		public ICostProvider GetProvider(UID costTypeUID)
		{
			return costTypeUID != null && _providers.TryGetValue(costTypeUID, out var provider) ? provider : null;
		}
	}
}
