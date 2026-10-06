using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.Transactions;

namespace AK.CoreDomain
{
	/// <summary>
	/// Minimal purchasable item contract for the purchase flow.
	/// Services only depend on this interface, not on concrete PurchasableItemDefinition.
	/// </summary>
	public interface IPurchasable
	{
		/// <summary>Display name for logging and error messages.</summary>
		string DisplayName { get; }

		/// <summary>
		/// Platform store product ID. Non-empty means the item is sold through the store, which
		/// takes the payment; <see cref="Cost"/> is then ignored.
		/// </summary>
		string ProductID { get; }

		/// <summary>
		/// The price in the game's currencies, for an item sold without a store. It needs a cost
		/// type: an item sold for currency with none is refused rather than given away.
		/// </summary>
		ICostInfo Cost { get; }

		/// <summary>
		/// Identity used to type this purchase in the transaction ledger (per-product
		/// counting and queries). MetaData items typically return their own Id.
		/// </summary>
		Uid<TransactionType> TransactionType { get; }

		/// <summary>
		/// Collect all rewards from this item (flattens bundles recursively), in the order they
		/// are granted. The purchase's transaction keeps the list.
		/// </summary>
		void CollectRewards(List<IReward> rewards);
	}
}
