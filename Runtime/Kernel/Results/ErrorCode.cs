namespace AK.Kernel.Results
{
	/// <summary>
	/// Every expected failure a UGFW service can report, as a value the caller can switch on.
	/// Grouped by domain in blocks of 100 so a code's family is readable at a glance and
	/// new codes slot in without renumbering. Unexpected failures (bugs) still throw.
	///
	/// Games extend the vocabulary by declaring their own enum and converting through
	/// <see cref="Result.Fail(int, string)"/> from a reserved block at 10000+.
	/// </summary>
	public enum ErrorCode
	{
		None = 0,

		// -------------------------------------------------- 1xx: argument / state
		NullArgument           = 100,
		NoIdentity             = 101,
		InvalidState           = 102,
		NotFound               = 103,
		AlreadyDone            = 104,
		NotInitialized         = 105,

		// -------------------------------------------------- 2xx: providers / registration
		NoProvider             = 200,

		// -------------------------------------------------- 3xx: cost
		CannotAfford           = 300,
		DeductDeclined         = 301,

		// -------------------------------------------------- 4xx: reward
		RewardDeclined         = 400,
		RewardUnresolved       = 401,

		// -------------------------------------------------- 5xx: transaction
		TransactionNotPending  = 500,
		TransactionNotCredited = 501,

		/// <summary>The purchase is still awaiting its payment, so it can't be credited yet.</summary>
		TransactionNotPaid     = 502,

		/// <summary>The purchase was paid for, so it is owed and can't be abandoned.</summary>
		TransactionPaid        = 503,

		// -------------------------------------------------- 6xx: store / IAP
		Cancelled              = 600,
		StoreNotInitialized    = 601,
		ProductUnavailable     = 602,
		PaymentDeclined        = 603,
		StoreError             = 604,
		DuplicateTransaction   = 605,
		Timeout                = 606,

		/// <summary>Another purchase is under way.</summary>
		PurchaseInProgress     = 607,

		/// <summary>The store holds the purchase for approval (Ask to Buy, a pending payment). If approved, its order arrives later.</summary>
		PurchaseDeferred       = 608,

		/// <summary>The receipt validator found the order's receipt invalid. Nothing was granted.</summary>
		ReceiptRejected        = 609,

		/// <summary>The receipt couldn't be validated now. The order stays unfinished and is tried again.</summary>
		ReceiptUnverified      = 610,

		StoreUnknown           = 699,

		// -------------------------------------------------- 9xx: catch-all
		Internal               = 900,

		/// <summary>First value games may use for their own codes. See <see cref="Result.Fail(int, string)"/>.</summary>
		GameDefined            = 10000,
	}
}
