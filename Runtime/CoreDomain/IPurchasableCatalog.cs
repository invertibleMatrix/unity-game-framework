namespace AK.CoreDomain
{
	/// <summary>
	/// What the game sells as each store product. Store orders that arrive without a purchase
	/// under way are fulfilled through it: restored ones, ones approved after a deferral, ones
	/// bought from the store's own page, and ones an earlier session left unfinished.
	/// </summary>
	public interface IPurchasableCatalog
	{
		/// <summary>The item sold as the store product; false when the game sells none as it.</summary>
		bool TryGetByProductId(string productId, out IPurchasable item);
	}
}
