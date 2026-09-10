namespace AK.Core
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

		// -------------------------------------------------- 6xx: store / IAP
		Cancelled              = 600,
		StoreNotInitialized    = 601,
		ProductUnavailable     = 602,
		PaymentDeclined        = 603,
		StoreError             = 604,
		DuplicateTransaction   = 605,
		Timeout                = 606,
		StoreUnknown           = 699,

		// -------------------------------------------------- 9xx: catch-all
		Internal               = 900,

		/// <summary>First value games may use for their own codes. See <see cref="Result.Fail(int, string)"/>.</summary>
		GameDefined            = 10000,
	}
}
