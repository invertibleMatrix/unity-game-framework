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
		public float  Amount;
		public string Source;
		public string Time;
		public int    Status;

		// Reward identities for crash recovery of pending transactions. Identity only —
		// a reward that no longer resolves is reported, never guessed at by name.
		public List<Uid> Rewards = new();
	}
}
