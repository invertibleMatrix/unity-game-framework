using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.CoreDomain.Transactions
{
	/// <summary>
	/// A record of something that transpired, with a credit lifecycle. Facts are
	/// transactions born Credited; grants and purchases ride the Pending → Credited
	/// path; Failed marks a purchase abandoned before its payment; Reversed marks a
	/// credit that was taken back.
	///
	/// Both identities here are values: <see cref="Id"/> is minted at runtime for the
	/// record itself, <see cref="Type"/> names the TransactionType definition. Neither
	/// requires an asset to be loaded to be meaningful.
	/// </summary>
	public class Transaction
	{
		public Uid                  Id;
		public Uid<TransactionType> Type;

		/// <summary>A whole quantity in the type's own unit: 1 by default, or the coins spent, the levels cleared.</summary>
		public long                 Amount;

		public string               Source;
		public string               Time;
		public TransactionStatus    Status;

		/// <summary>
		/// An id from outside the game that the transaction is unique by, such as a store's
		/// transaction id. Null for most transactions.
		/// </summary>
		public string ExternalId;

		/// <summary>
		/// A pending purchase whose payment hasn't been taken yet. It can't be credited until it is
		/// marked paid, and one found after a restart is abandoned.
		/// </summary>
		public bool AwaitingPayment;

		public IReadOnlyList<IReward> Rewards;

		public DateTime TimeDT => PersistableState.GetDateTimeFromString(Time);
	}
}
