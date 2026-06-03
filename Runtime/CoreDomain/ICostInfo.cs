using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Minimal cost contract for affordability checks and deduction.
	/// Services only depend on this interface, not on concrete CostOption.
	/// </summary>
	public interface ICostInfo
	{
		/// <summary>
		/// The UID of the CostType SO asset used for provider dispatch.
		/// Maps to CostType (which extends UID) in the default implementation.
		/// </summary>
		UID CostTypeUID { get; }

		/// <summary>
		/// The amount to check or deduct.
		/// </summary>
		int Amount { get; }
	}
}
