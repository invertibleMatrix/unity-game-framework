using System;
using System.Collections.Generic;
using AK.Core;

namespace AK.Services.Transactions
{
	[Serializable]
	public class TransactionLedgerState : PersistableState<TransactionLedgerState>
	{
		protected override string SaveKey => "UGFW_TRANSACTION_LEDGER";

		public List<TypeCountEntry>             Counts  = new();
		public List<PersistedTransactionEntry>  Entries = new();
	}

	[Serializable]
	public class TypeCountEntry
	{
		public string TypeId;
		public int    Count;
	}

	[Serializable]
	public class PersistedTransactionEntry
	{
		public string Id;
		public string TypeId;
		public float  Amount;
		public string Source;
		public string Time;
		public int    Status;

		// Reward payload references for crash recovery of pending transactions.
		public List<PersistedRewardRef> Rewards = new();
	}

	[Serializable]
	public class PersistedRewardRef
	{
		public string Id;
		public string Name;
	}
}
