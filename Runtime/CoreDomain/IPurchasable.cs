using System.Collections.Generic;
using AK.Core;

namespace AK.CoreDomain
{
	/// <summary>
	/// Minimal purchasable item contract for the purchase flow.
	/// Services only depend on this interface, not on concrete PurchasableItemDefinition.
	/// Any game's item definition can implement this to work with IPurchaseService.
	/// </summary>
	public interface IPurchasable
	{
		/// <summary>
		/// Display name for logging and error messages.
		/// </summary>
		string DisplayName { get; }

		/// <summary>
		/// Platform store product ID. Non-empty means this is an IAP item.
		/// </summary>
		string ProductID { get; }

		/// <summary>
		/// The cost to purchase this item. Null if free or misconfigured.
		/// </summary>
		ICostInfo Cost { get; }

		/// <summary>
		/// UID used to type this purchase in the transaction ledger (per-product
		/// counting and queries). MetaData items can return their own UniqueID.
		/// </summary>
		UID TransactionTypeUID { get; }

		/// <summary>
		/// Collect all rewards from this item (flattens bundles recursively).
		/// Used by PurchaseService to grant rewards after a successful purchase.
		/// </summary>
		void CollectRewards(List<IReward> rewards);
	}
}
