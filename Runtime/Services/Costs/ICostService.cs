using AK.Core;
using AK.CoreDomain;

namespace AK.Services.Costs
{
	/// <summary>
	/// Dispatches cost checking and deduction to the appropriate ICostProvider based on CostTypeUID.
	/// </summary>
	public interface ICostService
	{
		/// <summary>
		/// Register a cost provider. Replaces any existing provider for the same CostTypeUID.
		/// </summary>
		void RegisterProvider(ICostProvider provider);

		/// <summary>
		/// Remove a registered provider.
		/// </summary>
		bool UnregisterProvider(ICostProvider provider);

		/// <summary>
		/// Check if the player can afford the given cost.
		/// Dispatches to the registered ICostProvider for the cost's CostTypeUID.
		/// Returns true if no provider is registered (treat unknown cost types as free)
		/// or if the provider confirms affordability.
		/// </summary>
		bool CanAfford(ICostInfo cost);

		/// <summary>
		/// Deduct the cost from the player's resources.
		/// Dispatches to the registered ICostProvider for the cost's CostTypeUID.
		/// Returns true if deduction succeeded or no provider was registered.
		/// </summary>
		bool Deduct(ICostInfo cost);

		/// <summary>
		/// Get the provider for a given UID, or null if none registered.
		/// </summary>
		ICostProvider GetProvider(UID costTypeUID);
	}
}
