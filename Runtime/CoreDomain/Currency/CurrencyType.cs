namespace AK.CoreDomain.Currency
{
	/// <summary>
	/// Defines the type of currency.
	/// </summary>
	public enum CurrencyType
	{
		/// <summary>
		/// Primary soft currency (e.g., coins, gold).
		/// </summary>
		Soft = 0,

		/// <summary>
		/// Premium hard currency (e.g., gems, diamonds).
		/// </summary>
		Hard = 1,

		/// <summary>
		/// Event-specific currency (e.g., seasonal tokens).
		/// </summary>
		Event = 2,

		/// <summary>
		/// Social currency (e.g., friend points, guild currency).
		/// </summary>
		Social = 3,

		/// <summary>
		/// Special currency for specific features (e.g., energy, stamina).
		/// </summary>
		Special = 4,

		Real = 5
	}
}