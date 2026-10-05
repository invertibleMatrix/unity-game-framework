using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.Services.Transactions
{
	[Serializable]
	public class TransactionLedgerState : PersistableState<TransactionLedgerState>
	{
		protected override string SaveKey => "UGFW_TRANSACTION_LEDGER";

		public List<TypeCountEntry>            Counts  = new();
		public List<PersistedTransactionEntry> Entries = new();
	}

	[Serializable]
	public class TypeCountEntry
	{
		public Uid TypeId;
		public int Count;
	}

	[Serializable]
	public class PersistedTransactionEntry
	{
		public Uid    Id;
		public Uid    TypeId;

		// A whole number. Ledgers saved when it was a float load with it truncated toward zero;
		// every amount the framework recorded then was 1.
		public long   Amount;

		public string Source;
		public string Time;
		public int    Status;

		// Unique across the ledger when set, such as a store's transaction id.
		public string ExternalId;

		// A pending purchase recorded before its payment. Saves without the field load false:
		// nothing was awaiting payment then.
		public bool   AwaitingPayment;

		// Reward identities for crash recovery of pending transactions, one per reward of the
		// transaction and in its order; None for a reward that has no identity. Identity only —
		// a reward that no longer resolves is reported, never guessed at by name.
		public List<Uid> Rewards = new();

		// How many of Rewards, from the first, have been granted. Crediting grants in order and
		// stops at the first failure, so a retry resumes after the last grant that succeeded.
		public int Granted;
	}
}
