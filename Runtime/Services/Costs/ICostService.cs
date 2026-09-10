using AK.Core;
using AK.CoreDomain;

namespace AK.Services.Costs
{
	/// <summary>
	/// Dispatches cost checking and deduction to the ICostProvider registered for the cost's type.
	/// </summary>
	public interface ICostService
	{
		/// <summary>Register a cost provider. Replaces any existing provider for the same cost type.</summary>
		void RegisterProvider(ICostProvider provider);

		/// <summary>Remove a registered provider.</summary>
		bool UnregisterProvider(ICostProvider provider);

		/// <summary>
		/// Check if the player can afford the given cost. A cost with no type is free.
		/// Returns false when no provider is registered for the type.
		/// </summary>
		bool CanAfford(ICostInfo cost);

		/// <summary>Deduct the cost from the player's resources.</summary>
		bool Deduct(ICostInfo cost);

		/// <summary>Get the provider for a cost type, or null if none registered.</summary>
		ICostProvider GetProvider(Uid costType);
	}
}
