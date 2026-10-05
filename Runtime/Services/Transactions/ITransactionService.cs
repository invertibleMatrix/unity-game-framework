using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using AK.Kernel.Results;
using Cysharp.Threading.Tasks;

namespace AK.Services.Transactions
{
	/// <summary>
	/// The transaction ledger: records transactions with a credit lifecycle and answers
	/// credited-only queries over them. Counts persist across sessions; entry-level Query
	/// is session-scoped.
	///
	/// Types are addressed by <see cref="Uid{T}"/>; the asset overloads read <c>type.Id</c>.
	/// Expected failures come back as <see cref="Result"/> codes, never as null or exceptions.
	///
	/// Purchases are sagas on the ledger (<see cref="AK.Kernel.Purchasing.PurchaseSaga"/>): recorded
	/// unpaid, marked paid, granted reward by reward, credited. Every step is written to disk
	/// before the next, so a purchase cut off by a crash resumes or is abandoned, never repeated.
	///
	/// The ledger belongs to the bound account (<see cref="IAccountScoped"/>). Binding another
	/// account switches to its ledger and clears the session entries; pending transactions of
	/// the new account come back through <see cref="GetPendingTransactions"/>.
	/// </summary>
	public interface ITransactionService : IAccountScoped
	{
		event Action<Transaction> Recorded;
		event Action<Transaction> Credited;
		event Action<Transaction> Reversed;

		/// <summary>
		/// Records a fact — the transaction is born Credited. Fires Recorded and Credited.
		/// <paramref name="amount"/> is a whole quantity in the type's own unit.
		/// </summary>
		Result<Transaction> Record(TransactionType type, long amount = 1, string source = null);
		Result<Transaction> Record(Uid<TransactionType> type, long amount = 1, string source = null);

		/// <summary>
		/// Records a transaction awaiting credit with nothing left to pay: a deferred grant, or a
		/// purchase a store was paid for. Fires Recorded. <see cref="ErrorCode.NullArgument"/> when
		/// a reward is null. A reward without an identity is granted in this session but can't be
		/// recovered after a restart.
		///
		/// <paramref name="externalId"/>, when given, is an id from outside the game the transaction
		/// is unique by, such as a store's transaction id: a second record with an id on record fails
		/// with <see cref="ErrorCode.DuplicateTransaction"/>. Find the first with
		/// <see cref="TryFindByExternalId"/>. An id is remembered as long as its entry is kept:
		/// every pending entry, and the newest entries of its type.
		/// </summary>
		Result<Transaction> RecordPending(TransactionType type, IReadOnlyList<IReward> rewards = null, string source = null, string externalId = null);
		Result<Transaction> RecordPending(Uid<TransactionType> type, IReadOnlyList<IReward> rewards = null, string source = null, string externalId = null);

		/// <summary>
		/// Records a purchase before its payment is taken: pending and awaiting payment. Fires
		/// Recorded. Take the payment at once, then report it with <see cref="MarkPaid"/>, or
		/// <see cref="Abandon"/> the purchase when it was declined. A purchase still awaiting payment
		/// after a restart is abandoned by <see cref="GetPendingTransactions"/>: whether its payment
		/// went through is unknown, and nothing is granted without one.
		/// </summary>
		Result<Transaction> RecordUnpaid(TransactionType type, IReadOnlyList<IReward> rewards = null, string source = null);
		Result<Transaction> RecordUnpaid(Uid<TransactionType> type, IReadOnlyList<IReward> rewards = null, string source = null);

		/// <summary>
		/// Records that a purchase awaiting payment was paid for; it can be credited now. Ok when it
		/// was paid for already. <see cref="ErrorCode.TransactionNotPending"/> for one that was
		/// abandoned or reversed; <see cref="ErrorCode.NotFound"/> for one that isn't in the bound
		/// account's ledger.
		/// </summary>
		Result MarkPaid(Transaction transaction);

		/// <summary>
		/// Abandons a purchase whose payment was declined: it becomes Failed, with nothing paid
		/// and nothing owed. Ok when it was abandoned already. <see cref="ErrorCode.TransactionPaid"/>
		/// for one that was paid for, which is owed; <see cref="ErrorCode.NotFound"/> for one that
		/// isn't in the bound account's ledger.
		/// </summary>
		Result Abandon(Transaction transaction);

		/// <summary>
		/// Credits a pending transaction: grants its rewards via IRewardService (when
		/// available), marks it Credited, fires Credited. Ok for already-credited
		/// transactions; <see cref="ErrorCode.TransactionNotPaid"/> for a purchase still
		/// awaiting payment; <see cref="ErrorCode.TransactionNotPending"/> for Failed/Reversed ones;
		/// <see cref="ErrorCode.NotFound"/> for one that isn't in the bound account's ledger.
		///
		/// Rewards are granted in order, and each grant is recorded before the next. When one
		/// fails, its failure comes back and the transaction stays Pending; a later credit, in
		/// this session or after recovery, resumes from that reward. A recovered transaction
		/// whose rewards on record couldn't all be resolved stays Pending with
		/// <see cref="ErrorCode.RewardUnresolved"/>, so they aren't lost.
		/// </summary>
		UniTask<Result> CreditAsync(Transaction transaction, CancellationToken ct = default);

		/// <summary>Convenience: RecordPending + CreditAsync in one call.</summary>
		UniTask<Result<Transaction>> CreditAsync(Uid<TransactionType> type, IReadOnlyList<IReward> rewards, string source = null, CancellationToken ct = default);

		/// <summary>
		/// Takes back a credited transaction, from this session or an earlier one: marks it
		/// Reversed, decrements the count so conditions stop counting it, and fires Reversed.
		/// Reward-level revoke is a provider concern. <see cref="ErrorCode.NotFound"/> for an
		/// id the ledger doesn't hold, <see cref="ErrorCode.TransactionNotCredited"/> otherwise.
		/// </summary>
		Result Reverse(Uid transactionId);

		/// <summary>Net credited count for a type (credits minus reversals). Persists across sessions.</summary>
		int  Count(TransactionType type);
		int  Count(Uid<TransactionType> type);
		bool HasOccurred(TransactionType type);
		bool HasOccurred(Uid<TransactionType> type);

		/// <summary>
		/// Session entries, optionally filtered by type and/or status. Pass None for any type.
		/// The session keeps the newest entries of each type, plus every pending one.
		/// </summary>
		IReadOnlyList<Transaction> Query(Uid<TransactionType> type, TransactionStatus? status = null);

		/// <summary>
		/// The transaction recorded with this external id, from this session or an earlier one, in
		/// the bound account's ledger. One from an earlier session joins the session entries.
		/// </summary>
		bool TryFindByExternalId(string externalId, out Transaction transaction);

		/// <summary>
		/// The pending transactions that are owed: this session's, and those left Pending on disk
		/// (e.g. after a crash), with reward identities resolved via the resolver. Recovered
		/// transactions join the session entries, ready to be credited or reversed. A purchase
		/// left awaiting payment by an earlier session is abandoned here, with a warning, and
		/// isn't returned. Resolving rewards requires the service to have been constructed with
		/// an IUidResolver.
		/// </summary>
		IReadOnlyList<Transaction> GetPendingTransactions();

		/// <summary>
		/// Defers ledger writes until the returned scope is disposed, so a burst of records
		/// costs one serialization and one disk flush instead of one per call. Nested scopes
		/// flush once at the outermost dispose. A purchase's steps are written at once only
		/// outside a batch.
		/// </summary>
		LedgerBatch BeginBatch();
	}
}
