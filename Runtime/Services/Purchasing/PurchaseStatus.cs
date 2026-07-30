namespace AK.Services
{
	public struct PurchaseStatus
	{
		public enum ErrorCode
		{
			None,
			InternalError,
			InsufficientCurrency,
			Cancelled,
			IAPNotInitialized,
			IAPProductUnavailable,
			IAPPaymentDeclined,
			IAPStoreError,
			IAPDuplicateTransaction,
			IAPTimeout,
			IAPUnknownError
		}

		public ErrorCode Error;
	}
}