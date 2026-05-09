namespace AK.Services
{
	/// <summary>
	/// Result of an IAP purchase attempt. Returned by IIAPService.PurchaseAsync.
	/// </summary>
	public struct IAPPurchaseResult
	{
		/// <summary>
		/// Whether the purchase completed successfully.
		/// </summary>
		public bool Success;

		/// <summary>
		/// The store product ID that was purchased.
		/// </summary>
		public string ProductId;

		/// <summary>
		/// Human-readable failure reason if Success is false. Null on success.
		/// </summary>
		public string FailureReason;

		/// <summary>
		/// The purchase failure type for programmatic handling.
		/// </summary>
		public IAPFailureType FailureType;

		/// <summary>
		/// The raw receipt string from the store, if available.
		/// Useful for server-side validation.
		/// </summary>
		public string Receipt;

		/// <summary>
		/// The transaction ID from the store, if available.
		/// </summary>
		public string TransactionId;

		public static IAPPurchaseResult Succeeded(string productId, string receipt, string transactionId) => new()
		{
			Success = true,
			ProductId = productId,
			FailureReason = null,
			FailureType = IAPFailureType.None,
			Receipt = receipt,
			TransactionId = transactionId
		};

		public static IAPPurchaseResult Failed(string productId, IAPFailureType failureType, string reason) => new()
		{
			Success = false,
			ProductId = productId,
			FailureReason = reason,
			FailureType = failureType,
			Receipt = null,
			TransactionId = null
		};
	}

	/// <summary>
	/// Categorized failure types for IAP purchases.
	/// </summary>
	public enum IAPFailureType
	{
		None = 0,

		/// <summary>Store connection or configuration issue.</summary>
		StoreError,

		/// <summary>User cancelled the purchase dialog.</summary>
		UserCancelled,

		/// <summary>Payment was declined by the payment provider.</summary>
		PaymentDeclined,

		/// <summary>Product is not available in the store.</summary>
		ProductUnavailable,

		/// <summary>A purchase is already in progress.</summary>
		ExistingPurchasePending,

		/// <summary>Duplicate transaction detected.</summary>
		DuplicateTransaction,

		/// <summary>IAP service is not initialized.</summary>
		NotInitialized,

		/// <summary>Unknown or unclassified failure.</summary>
		Unknown
	}
}

