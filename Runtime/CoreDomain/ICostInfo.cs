using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Minimal cost contract for affordability checks and deduction.
	/// Services only depend on this interface, not on concrete CostOption.
	/// </summary>
	public interface ICostInfo
	{
		/// <summary>Identity of the cost type used for provider dispatch. None means "free".</summary>
		Uid CostType { get; }

		/// <summary>The amount to check or deduct.</summary>
		int Amount { get; }
	}
}
