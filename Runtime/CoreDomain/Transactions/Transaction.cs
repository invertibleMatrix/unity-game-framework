using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.CoreDomain.Transactions
{
	/// <summary>
	/// A record of something that transpired, with a credit lifecycle. Facts are
	/// transactions born Credited; grants and purchases ride the Pending → Credited
	/// path; Reversed marks a credit that was taken back.
	///
	/// Both identities here are values: <see cref="Id"/> is minted at runtime for the
	/// record itself, <see cref="Type"/> names the TransactionType definition. Neither
	/// requires an asset to be loaded to be meaningful.
	/// </summary>
	public class Transaction
	{
		public Uid                  Id;
		public Uid<TransactionType> Type;
		public float                Amount;
		public string               Source;
		public string               Time;
		public TransactionStatus    Status;

		public IReadOnlyList<IReward> Rewards;

		public DateTime TimeDT => PersistableState.GetDateTimeFromString(Time);
	}
}
