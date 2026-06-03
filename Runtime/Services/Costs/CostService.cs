using System.Collections.Generic;
using AK.CoreDomain.Costs;
using UnityEngine;

namespace AK.Services.Costs
{
	/// <summary>
	/// Dispatches cost checking and deduction to the appropriate CostProvider based on CostType.
	/// </summary>
	public class CostService : ICostService
	{
		private readonly Dictionary<CostType, CostProvider> _providers = new();

		/// <inheritdoc />
		public void RegisterProvider(CostProvider provider)
		{
			if (provider == null)
			{
				Debug.LogWarning("[CostService] Cannot register null provider.");
				return;
			}

			if (provider.Type == null)
			{
				Debug.LogWarning($"[CostService] Cannot register provider '{provider.name}' with null CostType.");
				return;
			}

			if (_providers.ContainsKey(provider.Type))
			{
				Debug.LogWarning($"[CostService] Replacing existing provider for CostType '{provider.Type.name}'.");
			}

			_providers[provider.Type] = provider;
		}

		/// <inheritdoc />
		public bool UnregisterProvider(CostProvider provider)
		{
			if (provider?.Type == null) return false;

			if (_providers.TryGetValue(provider.Type, out var existing) && existing == provider)
			{
				return _providers.Remove(provider.Type);
			}

			return false;
		}

		/// <inheritdoc />
		public bool CanAfford(CostOption costOption)
		{
			if (costOption?.Type == null) return true;

			if (_providers.TryGetValue(costOption.Type, out var provider))
			{
				return provider.CanAfford(costOption);
			}

			Debug.LogWarning($"[CostService] No provider registered for CostType '{costOption.Type.name}'. " +
			                 $"Register a CostProvider for this type. Defaulting to unaffordable.");
			return false;
		}

		/// <inheritdoc />
		public bool Deduct(CostOption costOption)
		{
			if (costOption?.Type == null) return true;

			if (_providers.TryGetValue(costOption.Type, out var provider))
			{
				return provider.Deduct(costOption);
			}

			Debug.LogWarning($"[CostService] No provider registered for CostType '{costOption.Type.name}'. " +
			                 $"Cannot deduct. Register a CostProvider for this type.");
			return false;
		}

		/// <inheritdoc />
		public CostProvider GetProvider(CostType type)
		{
			return type != null && _providers.TryGetValue(type, out var provider) ? provider : null;
		}
	}
}
