using AK.Core;
using AK.CoreDomain;
using UnityEngine;

namespace AK.Examples.Costs
{
	/// <summary>
	/// Abstract base for cost providers. Each provider handles checking affordability
	/// and deducting costs for a specific CostType.
	/// Extend this for each CostType your game supports and register with CostService.
	/// </summary>
	public abstract class CostProvider : MetaDataAsset, ICostProvider
	{
		[Tooltip("The CostType UID asset this provider handles. Used by CostService for dispatch.")]
		public CostType Type;

		// ICostProvider explicit implementation
		UID ICostProvider.CostTypeUID => Type;

		/// <summary>
		/// Whether this provider can handle the given cost.
		/// Default: matches by CostTypeUID reference equality.
		/// Override for custom matching logic.
		/// </summary>
		public virtual bool CanProvide(ICostInfo cost)
		{
			return cost != null && cost.CostTypeUID == Type;
		}

		/// <summary>
		/// Check if the player can afford this cost. Game-specific —
		/// the framework doesn't know about currencies, inventory, stamina, etc.
		/// Return true if the player has sufficient resources.
		/// </summary>
		public abstract bool CanAfford(ICostInfo cost);

		/// <summary>
		/// Deduct the cost from the player's resources. Game-specific.
		/// Return true if deduction succeeded, false if it failed (e.g., insufficient funds).
		/// Only call this after CanAfford returns true.
		/// </summary>
		public abstract bool Deduct(ICostInfo cost);
	}
}
