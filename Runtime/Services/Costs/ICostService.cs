using AK.CoreDomain.Costs;

namespace AK.Services.Costs
{
	/// <summary>
	/// Dispatches cost checking and deduction to the appropriate CostProvider based on CostType.
	/// </summary>
	public interface ICostService
	{
		/// <summary>
		/// Register a cost provider. Replaces any existing provider for the same CostType.
		/// </summary>
		void RegisterProvider(CostProvider provider);

		/// <summary>
		/// Remove a registered provider.
		/// </summary>
		bool UnregisterProvider(CostProvider provider);

		/// <summary>
		/// Check if the player can afford the given cost option.
		/// Dispatches to the registered CostProvider for the cost's CostType.
		/// Returns true if no provider is registered (treat unknown cost types as free)
		/// or if the provider confirms affordability.
		/// </summary>
		bool CanAfford(CostOption costOption);

		/// <summary>
		/// Deduct the cost from the player's resources.
		/// Dispatches to the registered CostProvider for the cost's CostType.
		/// Returns true if deduction succeeded or no provider was registered.
		/// </summary>
		bool Deduct(CostOption costOption);

		/// <summary>
		/// Get the provider for a given CostType, or null if none registered.
		/// </summary>
		CostProvider GetProvider(CostType type);
	}
}
