using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using Cysharp.Threading.Tasks;

namespace AK.Services.Transactions
{
	/// <summary>
	/// The transaction ledger: records transactions with a credit lifecycle and
	/// answers credited-only queries over them. Counts persist across sessions;
	/// entry-level Query is session-scoped (Type references are runtime objects).
	/// </summary>
	public interface ITransactionService
	{
		event Action<Transaction> Recorded;
		event Action<Transaction> Credited;
		event Action<Transaction> Reversed;

		/// <summary>Records a fact — the transaction is born Credited. Fires Recorded and Credited.</summary>
		Transaction Record(UID type, float amount = 1f, string source = null);

		/// <summary>Records a transaction awaiting credit (deferred grants, IAP). Fires Recorded.</summary>
		Transaction RecordPending(UID type, IReadOnlyList<IReward> rewards = null, string source = null);

		/// <summary>
		/// Credits a pending transaction: grants its rewards via IRewardService (when
		/// available), marks it Credited, fires Credited. Idempotent for already-credited
		/// transactions; fails for Failed/Reversed ones.
		/// </summary>
		UniTask<bool> CreditAsync(Transaction transaction, CancellationToken ct = default);

		/// <summary>Convenience: RecordPending + CreditAsync in one call.</summary>
		UniTask<bool> CreditAsync(UID type, IReadOnlyList<IReward> rewards, string source = null, CancellationToken ct = default);

		/// <summary>
		/// Takes back a credited transaction: marks it Reversed and decrements the count,
		/// so conditions stop counting it. Reward-level revoke is a provider concern.
		/// </summary>
		bool Reverse(string transactionId);

		/// <summary>Net credited count for a type (credits minus reversals). Persists across sessions.</summary>
		int Count(UID type);

		bool HasOccurred(UID type);

		/// <summary>Session entries, optionally filtered by type and/or status.</summary>
		IReadOnlyList<Transaction> Query(UID type, TransactionStatus? status = null);

		/// <summary>
		/// Recovers transactions left Pending on disk (e.g. after a crash), resolving type
		/// and reward references via the meta data repository. Recovered transactions join
		/// the session entries, ready to be credited or reversed. Requires the service to
		/// have been constructed with an IMetaDataRepository.
		/// </summary>
		IReadOnlyList<Transaction> GetPendingTransactions();
	}
}
