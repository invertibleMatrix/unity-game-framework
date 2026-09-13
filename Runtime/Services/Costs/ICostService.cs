using AK.Core;
using AK.CoreDomain;

namespace AK.Services.Costs
{
	/// <summary>
	/// Dispatches cost checking and deduction to the ICostProvider registered for the cost's type.
	/// Outcomes are values the caller switches on: <see cref="ErrorCode.CannotAfford"/>,
	/// <see cref="ErrorCode.NoProvider"/>, <see cref="ErrorCode.DeductDeclined"/>.
	/// </summary>
	public interface ICostService
	{
		/// <summary>Register a cost provider. Replaces any existing provider for the same cost type.</summary>
		void RegisterProvider(ICostProvider provider);

		/// <summary>Remove a registered provider.</summary>
		bool UnregisterProvider(ICostProvider provider);

		/// <summary>
		/// Ok when the cost can be paid right now. A cost with no type is free.
		/// <see cref="ErrorCode.NoProvider"/> when nothing is registered for the type.
		/// </summary>
		Result CanAfford(ICostInfo cost);

		/// <summary>Pays the cost. Ok only when the resource was actually deducted.</summary>
		Result Deduct(ICostInfo cost);

		/// <summary>Get the provider for a cost type, or null if none registered.</summary>
		ICostProvider GetProvider(Uid costType);
	}
}
