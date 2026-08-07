using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.CoreDomain.Transactions
{
	/// <summary>
	/// A record of something that transpired, with a credit lifecycle.
	/// Facts are transactions born Credited; grants and purchases ride the
	/// Pending → Credited path; Reversed marks a credit that was taken back.
	/// </summary>
	public class Transaction
	{
		public string Id;
		public UID Type;
		public float Amount;
		public string Source;
		public string Time;
		public TransactionStatus Status;

		// Runtime-only payload. Persisted entries store UID references instead
		// (PersistedRewardRef), resolvable via IMetaDataRepository on recovery.
		public IReadOnlyList<IReward> Rewards;

		public DateTime TimeDT => PersistableState.GetDateTimeFromString(Time);
	}
}
