using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Provider contract for cost dispatch. Services only depend on this interface,
	/// not on the concrete CostProvider ScriptableObject subclass.
	/// Game implementations can access game-specific systems as needed.
	/// </summary>
	public interface ICostProvider
	{
		/// <summary>
		/// The UID of the CostType this provider handles. Used for dispatch.
		/// </summary>
		UID CostTypeUID { get; }

		/// <summary>
		/// Whether the player can afford the given cost.
		/// </summary>
		bool CanAfford(ICostInfo cost);

		/// <summary>
		/// Deduct the cost from the player's resources. Returns true on success.
		/// </summary>
		bool Deduct(ICostInfo cost);
	}
}
