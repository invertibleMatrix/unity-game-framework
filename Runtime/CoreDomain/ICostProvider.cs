using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Provider contract for cost dispatch. Services only depend on this interface,
	/// not on the concrete CostProvider ScriptableObject subclass.
	/// </summary>
	public interface ICostProvider
	{
		/// <summary>Identity of the cost type this provider handles. Used for dispatch.</summary>
		Uid CostType { get; }

		/// <summary>Whether the player can afford the given cost.</summary>
		bool CanAfford(ICostInfo cost);

		/// <summary>
		/// Deduct the cost from the player's resources. Returns true on success.
		///
		/// Change the balance in memory and save it on the game's own schedule, not to disk from
		/// here. A purchase records its payment right after the deduction, and one found unpaid
		/// after a restart is abandoned, so a deduction saved before that record would be lost.
		/// </summary>
		bool Deduct(ICostInfo cost);
	}
}
