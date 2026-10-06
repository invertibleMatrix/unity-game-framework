using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using AK.Kernel.Persistence;
using AK.Kernel.Purchasing;
using AK.Kernel.Results;
using AK.Services.Rewards;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services.Transactions
{
	/// <summary>
	/// Default ITransactionService. Records every transaction into a persisted ledger
	/// (counts permanent; entries capped per type, pending ones always kept) and credits reward
	/// payloads through IRewardService when one is provided — games using their own grant path
	/// can pass null and listen to the lifecycle events instead.
	///
	/// Every identity in the ledger is a value. Recovery resolves reward identities through
	/// the IUidResolver; a reward that no longer resolves is reported and dropped from the
	/// recovered payload — never substituted.
	///
	/// Each account has its own ledger (<see cref="IAccountScoped"/>); until one is bound the
	/// device's ledger is used. A transaction records an exchange, so every mutation commits
	/// and flushes the ledger to disk at once, unless inside a <see cref="BeginBatch"/> scope.
	///
	/// A pending transaction is a purchase saga (<see cref="PurchaseSaga"/>): its entry holds the
	/// stage (awaiting payment or owed) and how many rewards were granted.
	/// </summary>
	public class TransactionService : ITransactionService, IBatchable
	{
		/// <summary>The most entries of one type kept, on disk and in the session. Pending entries are kept beyond it.</summary>
		public const int MaxEntriesPerType = 100;

		private readonly IRewardService       _rewardService;
		private readonly IUidResolver         _resolver;
		private readonly UidRedirectTable     _redirects;
		private readonly PrefsStore           _store;
		private readonly DeferredCommit       _commit;
		private readonly ResetListener        _resetListener;
		private readonly Dictionary<Uid, int> _counts         = new();
		private readonly List<Transaction>    _sessionEntries = new();

		private TransactionLedgerState _state;
		private string                 _accountId;

		public event Action<Transaction> Recorded;
		public event Action<Transaction> Credited;
		public event Action<Transaction> Reversed;

		/// <param name="rewardService">Grants credited rewards; null when the game grants them itself.</param>
		/// <param name="resolver">Resolves persisted reward identities when pending transactions are recovered.</param>
		/// <param name="redirects">Identity redirects applied to each ledger as it loads.</param>
		/// <param name="store">Where the ledgers live; <see cref="UniPrefs.Store"/> when null.</param>
		public TransactionService(IRewardService rewardService = null, IUidResolver resolver = null,
		                          UidRedirectTable redirects = null, PrefsStore store = null)
		{
			_rewardService = rewardService;
			_resolver      = resolver;
			_redirects     = redirects;
			_store         = store ?? UniPrefs.Store;
			_commit        = new DeferredCommit(CommitLedger);

			LoadLedger(null);

			_resetListener = new ResetListener(this);
			_store.AddResetListener(_resetListener);
		}

		// ---------------------------------------------------------------- account

		public string AccountId => _accountId;

		public void BindAccount(string accountId)
		{
			if (string.IsNullOrEmpty(accountId)) accountId = null;
			else StorageKeys.ValidateScope(accountId);

			if (accountId == _accountId) return;

			if (_commit.IsSuspended)
			{
				throw new InvalidOperationException("[TransactionService] Can't switch accounts inside a write batch: its writes belong to the account bound when it opened.");
			}

			if (accountId != null && TransactionLedgerState.AdoptDeviceSave(_store, accountId))
			{
				Debug.Log("[TransactionService] The device's transaction ledger moved to the account just bound, which had none of its own.");
			}

			_sessionEntries.Clear();
			LoadLedger(accountId);
		}

		// ---------------------------------------------------------------- batching

		public LedgerBatch BeginBatch() => new(this);

		void IBatchable.Suspend() => _commit.Suspend();
		void IBatchable.Resume()  => _commit.Resume();

		// ---------------------------------------------------------------- record

		public Result<Transaction> Record(TransactionType type, long amount = 1, string source = null)
		{
			return type != null ? Record(type.IdAs<TransactionType>(), amount, source) : Result<Transaction>.Fail(ErrorCode.NullArgument, "type");
		}

		public Result<Transaction> Record(Uid<TransactionType> type, long amount = 1, string source = null)
		{
			if (type.IsNone) return Result<Transaction>.Fail(ErrorCode.NoIdentity, "type");

			Transaction transaction = CreateTransaction(type, amount, source, TransactionStatus.Credited, null, null, false);
			Persist(transaction, creditDelta: 1);

			Recorded?.Invoke(transaction);
			Credited?.Invoke(transaction);
			return Result<Transaction>.Ok(transaction);
		}

		public Result<Transaction> RecordPending(TransactionType type, IReadOnlyList<IReward> rewards = null, string source = null, string externalId = null)
		{
			return type != null
				? RecordPending(type.IdAs<TransactionType>(), rewards, source, externalId)
				: Result<Transaction>.Fail(ErrorCode.NullArgument, "type");
		}

		public Result<Transaction> RecordPending(Uid<TransactionType> type, IReadOnlyList<IReward> rewards = null, string source = null, string externalId = null)
		{
			return RecordPending(type, rewards, source, externalId, awaitingPayment: false);
		}

		public Result<Transaction> RecordUnpaid(TransactionType type, IReadOnlyList<IReward> rewards = null, string source = null)
		{
			return type != null
				? RecordUnpaid(type.IdAs<TransactionType>(), rewards, source)
				: Result<Transaction>.Fail(ErrorCode.NullArgument, "type");
		}

		public Result<Transaction> RecordUnpaid(Uid<TransactionType> type, IReadOnlyList<IReward> rewards = null, string source = null)
		{
			return RecordPending(type, rewards, source, null, awaitingPayment: true);
		}

		public Result MarkPaid(Transaction transaction)
		{
			Result<PersistedTransactionEntry> found = FindPending(transaction);
			if (found.IsFailed)
			{
				// Credited means it was paid for long ago.
				return found.Code == ErrorCode.AlreadyDone && transaction.Status == TransactionStatus.Credited
					? Result.Ok
					: Result.Fail(found.Code == ErrorCode.AlreadyDone ? ErrorCode.TransactionNotPending : found.Code, found.Detail);
			}

			PersistedTransactionEntry entry = found.Value;
			if (!entry.AwaitingPayment) return Result.Ok;

			Store(entry, transaction, SagaOf(entry).Paid());
			_commit.Commit();
			return Result.Ok;
		}

		public Result Abandon(Transaction transaction)
		{
			Result<PersistedTransactionEntry> found = FindPending(transaction);
			if (found.IsFailed)
			{
				if (found.Code != ErrorCode.AlreadyDone) return found.Untyped;

				return transaction.Status == TransactionStatus.Failed
					? Result.Ok
					: Result.Fail(ErrorCode.TransactionPaid, $"transaction {transaction.Id.ToShortString()} is {transaction.Status}");
			}

			PersistedTransactionEntry entry = found.Value;
			if (!entry.AwaitingPayment)
			{
				return Result.Fail(ErrorCode.TransactionPaid, $"transaction {transaction.Id.ToShortString()} was paid for, so it is owed");
			}

			Store(entry, transaction, SagaOf(entry).Abandon());
			_commit.Commit();
			return Result.Ok;
		}

		public async UniTask<Result<Transaction>> CreditAsync(Uid<TransactionType> type, IReadOnlyList<IReward> rewards, string source = null, CancellationToken ct = default)
		{
			Result<Transaction> pending = RecordPending(type, rewards, source);
			if (pending.IsFailed) return pending;

			Result credited = await CreditAsync(pending.Value, ct);
			return credited.IsOk ? pending : credited.As<Transaction>();
		}

		public UniTask<Result> CreditAsync(Transaction transaction, CancellationToken ct = default)
		{
			return UniTask.FromResult(Credit(transaction));
		}

		public Result Reverse(Uid transactionId)
		{
			PersistedTransactionEntry entry = FindEntry(transactionId);
			if (entry == null) return Result.Fail(ErrorCode.NotFound, transactionId.ToShortString());

			if (entry.Status != (int)TransactionStatus.Credited)
			{
				return Result.Fail(ErrorCode.TransactionNotCredited, ((TransactionStatus)entry.Status).ToString());
			}

			// An entry from an earlier session, or one trimmed from this session's entries,
			// gets a transaction again, so Reversed can carry it.
			bool entryChanged = false;
			Transaction transaction = FindSession(transactionId) ?? Materialize(entry, ref entryChanged);

			entry.Status       = (int)TransactionStatus.Reversed;
			transaction.Status = TransactionStatus.Reversed;
			AddCount(entry.TypeId, -1);
			_commit.Commit();

			Reversed?.Invoke(transaction);
			return Result.Ok;
		}

		// ---------------------------------------------------------------- query

		public int Count(TransactionType type) => type != null ? Count(type.Id) : 0;

		public int Count(Uid<TransactionType> type) => Count(type.Value);

		public bool HasOccurred(TransactionType type) => Count(type) > 0;

		public bool HasOccurred(Uid<TransactionType> type) => Count(type) > 0;

		public IReadOnlyList<Transaction> Query(Uid<TransactionType> type, TransactionStatus? status = null)
		{
			var result = new List<Transaction>();
			for (int i = 0; i < _sessionEntries.Count; i++)
			{
				Transaction transaction = _sessionEntries[i];
				if (type.IsSet && transaction.Type != type) continue;
				if (status.HasValue && transaction.Status != status.Value) continue;
				result.Add(transaction);
			}

			return result;
		}

		public bool TryFindByExternalId(string externalId, out Transaction transaction)
		{
			transaction = null;

			PersistedTransactionEntry entry = FindEntryByExternalId(externalId);
			if (entry == null) return false;

			bool entryChanged = false;
			transaction = FindSession(entry.Id) ?? Materialize(entry, ref entryChanged);
			if (entryChanged) _commit.Commit();

			return true;
		}

		public IReadOnlyList<Transaction> GetPendingTransactions()
		{
			var result = new List<Transaction>();

			bool dirty = false;

			for (int i = 0; i < _state.Entries.Count; i++)
			{
				PersistedTransactionEntry entry = _state.Entries[i];
				if (entry.Status != (int)TransactionStatus.Pending) continue;

				Transaction existing = FindSession(entry.Id);
				if (existing != null)
				{
					// This session's purchase awaiting its payment is still under way, not owed.
					if (!entry.AwaitingPayment) result.Add(existing);
					continue;
				}

				if (entry.TypeId.IsNone)
				{
					Debug.LogWarning($"[TransactionService] Pending transaction {entry.Id.ToShortString()} has no type identity — marking failed.");
					entry.Status          = (int)TransactionStatus.Failed;
					entry.AwaitingPayment = false;
					dirty = true;
					continue;
				}

				if (SagaOf(entry).NextAfterRestart == PurchaseStep.Abandon)
				{
					Debug.LogWarning($"[TransactionService] Purchase {entry.Id.ToShortString()} was never marked paid before the app stopped, so it is abandoned: whether its payment went through is unknown, and nothing is granted without one.");
					entry.Status          = (int)TransactionStatus.Failed;
					entry.AwaitingPayment = false;
					dirty = true;
					continue;
				}

				result.Add(Materialize(entry, ref dirty));
			}

			if (dirty) _commit.Commit();

			return result;
		}

		// ---------------------------------------------------------------- credit

		private Result Credit(Transaction transaction)
		{
			if (transaction == null) return Result.Fail(ErrorCode.NullArgument, "transaction");

			if (transaction.Status == TransactionStatus.Credited) return Result.Ok;

			if (transaction.Status != TransactionStatus.Pending)
			{
				return Result.Fail(ErrorCode.TransactionNotPending, transaction.Status.ToString());
			}

			PersistedTransactionEntry entry = FindEntry(transaction.Id);
			if (entry == null)
			{
				return Result.Fail(ErrorCode.NotFound, $"transaction {transaction.Id.ToShortString()} isn't in the bound account's ledger");
			}

			// The ledger is the record. Another copy of this transaction may have moved it on.
			if (entry.Status != (int)TransactionStatus.Pending)
			{
				transaction.Status = (TransactionStatus)entry.Status;
				return transaction.Status == TransactionStatus.Credited
					? Result.Ok
					: Result.Fail(ErrorCode.TransactionNotPending, transaction.Status.ToString());
			}

			IReadOnlyList<IReward> rewards = transaction.Rewards;
			int count    = rewards?.Count ?? 0;
			int recorded = entry.Rewards?.Count ?? 0;

			// Recovery without a resolver leaves the rewards on record unresolved. Crediting now
			// would lose them.
			if (count != recorded)
			{
				return Result.Fail(ErrorCode.RewardUnresolved,
					$"transaction {transaction.Id.ToShortString()} has {recorded} rewards on record but {count} resolved; it stays pending");
			}

			// The rewards not granted yet are granted in order, each recorded before the next. The
			// first that fails stops the credit: the transaction stays pending, and a later credit
			// resumes from that reward.
			PurchaseSaga saga = SagaOf(entry);
			while (true)
			{
				switch (saga.Next)
				{
					case PurchaseStep.Pay:
						transaction.AwaitingPayment = true;
						return Result.Fail(ErrorCode.TransactionNotPaid, $"transaction {transaction.Id.ToShortString()} is awaiting its payment");

					case PurchaseStep.Grant when _rewardService == null:
						Debug.LogWarning($"[TransactionService] Transaction {transaction.Id.ToShortString()} carries rewards but no IRewardService was provided — marking credited without granting.");
						saga = PurchaseSaga.Restore(PurchaseStage.Owed, count, count);
						break;

					case PurchaseStep.Grant:
						int index = saga.Granted;
						Result granted = _rewardService.Grant(rewards[index]);
						if (granted.IsFailed)
						{
							return Result.Fail(granted.Code,
								$"reward {index} of transaction {transaction.Id.ToShortString()} was not granted, so the transaction stays pending: {granted.Detail}");
						}

						saga = saga.RewardGranted();
						Store(entry, transaction, saga);
						_commit.Commit();
						break;

					case PurchaseStep.Credit:
						Store(entry, transaction, saga.Credit());
						AddCount(entry.TypeId, 1);
						_commit.Commit();

						Credited?.Invoke(transaction);
						return Result.Ok;

					default:
						throw new InvalidOperationException($"[TransactionService] Pending transaction {transaction.Id.ToShortString()} is at {saga}, which a pending entry can't be.");
				}
			}
		}

		/// <summary>The purchase saga of a pending entry.</summary>
		private static PurchaseSaga SagaOf(PersistedTransactionEntry entry)
		{
			return PurchaseSaga.Restore(entry.AwaitingPayment ? PurchaseStage.Unpaid : PurchaseStage.Owed,
			                            entry.Granted, entry.Rewards?.Count ?? 0);
		}

		/// <summary>Writes a purchase saga's state to its entry and its transaction. The caller commits.</summary>
		private static void Store(PersistedTransactionEntry entry, Transaction transaction, PurchaseSaga saga)
		{
			entry.Status = (int)(saga.Stage switch
			{
				PurchaseStage.Credited  => TransactionStatus.Credited,
				PurchaseStage.Abandoned => TransactionStatus.Failed,
				_                       => TransactionStatus.Pending,
			});
			entry.AwaitingPayment = saga.Stage == PurchaseStage.Unpaid;
			entry.Granted         = saga.Granted;

			transaction.Status          = (TransactionStatus)entry.Status;
			transaction.AwaitingPayment = entry.AwaitingPayment;
		}

		/// <summary>
		/// The entry of a pending transaction in the bound account's ledger, with the transaction
		/// brought in step with it. <see cref="ErrorCode.AlreadyDone"/> when the entry is credited
		/// or failed, <see cref="ErrorCode.TransactionNotPending"/> when it is reversed.
		/// </summary>
		private Result<PersistedTransactionEntry> FindPending(Transaction transaction)
		{
			if (transaction == null) return Result<PersistedTransactionEntry>.Fail(ErrorCode.NullArgument, "transaction");

			PersistedTransactionEntry entry = FindEntry(transaction.Id);
			if (entry == null)
			{
				return Result<PersistedTransactionEntry>.Fail(ErrorCode.NotFound,
					$"transaction {transaction.Id.ToShortString()} isn't in the bound account's ledger");
			}

			transaction.Status          = (TransactionStatus)entry.Status;
			transaction.AwaitingPayment = entry.AwaitingPayment;

			switch (transaction.Status)
			{
				case TransactionStatus.Pending:
					return Result<PersistedTransactionEntry>.Ok(entry);

				case TransactionStatus.Credited:
				case TransactionStatus.Failed:
					return Result<PersistedTransactionEntry>.Fail(ErrorCode.AlreadyDone, transaction.Status.ToString());

				default:
					return Result<PersistedTransactionEntry>.Fail(ErrorCode.TransactionNotPending, transaction.Status.ToString());
			}
		}

		// ---------------------------------------------------------------- internals

		private int Count(Uid id)
		{
			return id.IsSet && _counts.TryGetValue(id, out int count) ? count : 0;
		}

		private void CommitLedger()
		{
			_state.Commit();
			_store.Flush();
		}

		private void LoadLedger(string accountId)
		{
			_state     = TransactionLedgerState.Load(_store, accountId);
			_accountId = accountId;

			bool rewritten = ApplyRedirects(_redirects);

			_counts.Clear();
			foreach (TypeCountEntry entry in _state.Counts)
			{
				if (entry.TypeId.IsSet) _counts[entry.TypeId] = entry.Count;
			}

			if (rewritten) _commit.Commit();
		}

		private PersistedTransactionEntry FindEntry(Uid id)
		{
			for (int i = 0; i < _state.Entries.Count; i++)
			{
				if (_state.Entries[i].Id == id) return _state.Entries[i];
			}

			return null;
		}

		private PersistedTransactionEntry FindEntryByExternalId(string externalId)
		{
			if (string.IsNullOrEmpty(externalId)) return null;

			for (int i = 0; i < _state.Entries.Count; i++)
			{
				if (string.Equals(_state.Entries[i].ExternalId, externalId, StringComparison.Ordinal)) return _state.Entries[i];
			}

			return null;
		}

		private Transaction FindSession(Uid id)
		{
			for (int i = 0; i < _sessionEntries.Count; i++)
			{
				if (_sessionEntries[i].Id == id) return _sessionEntries[i];
			}

			return null;
		}

		/// <summary>A session transaction for a persisted entry. Sets <paramref name="entryChanged"/> when rewards that can't be recovered were dropped from the entry.</summary>
		private Transaction Materialize(PersistedTransactionEntry entry, ref bool entryChanged)
		{
			var transaction = new Transaction
			{
				Id              = entry.Id,
				Type            = new Uid<TransactionType>(entry.TypeId),
				Amount          = entry.Amount,
				Source          = entry.Source,
				Time            = entry.Time,
				Status          = (TransactionStatus)entry.Status,
				ExternalId      = entry.ExternalId,
				AwaitingPayment = entry.AwaitingPayment,
				Rewards         = ResolveRewards(entry, ref entryChanged)
			};

			AddToSession(transaction);
			return transaction;
		}

		/// <summary>
		/// Resolves the entry's reward identities. A reward that doesn't resolve is reported and
		/// dropped from the entry as well, with <see cref="PersistedTransactionEntry.Granted"/>
		/// moved to match, so the entry's rewards stay in step with the transaction's.
		/// </summary>
		private List<IReward> ResolveRewards(PersistedTransactionEntry entry, ref bool entryChanged)
		{
			List<Uid> ids = entry.Rewards;
			if (ids == null || ids.Count == 0) return null;

			if (_resolver == null)
			{
				Debug.LogWarning($"[TransactionService] Transaction {entry.Id.ToShortString()} carries rewards but no IUidResolver was provided — they can't be recovered.");
				return null;
			}

			var rewards = new List<IReward>(ids.Count);
			for (int i = 0; i < ids.Count;)
			{
				if (_resolver.TryResolve(ids[i], out UID asset) && asset is IReward reward)
				{
					rewards.Add(reward);
					i++;
					continue;
				}

				Debug.LogWarning(ids[i].IsNone
					? $"[TransactionService] Reward {i} of transaction {entry.Id.ToShortString()} had no identity, so it can't be recovered. Dropped from recovery."
					: $"[TransactionService] Reward {UidDebugNames.Describe(ids[i])} could not be resolved — the definition was removed without a redirect. Dropped from recovery.");

				ids.RemoveAt(i);
				if (i < entry.Granted) entry.Granted--;
				entryChanged = true;
			}

			return rewards.Count > 0 ? rewards : null;
		}

		private Result<Transaction> RecordPending(Uid<TransactionType> type, IReadOnlyList<IReward> rewards, string source,
		                                          string externalId, bool awaitingPayment)
		{
			if (type.IsNone) return Result<Transaction>.Fail(ErrorCode.NoIdentity, "type");

			if (rewards != null)
			{
				for (int i = 0; i < rewards.Count; i++)
				{
					if (rewards[i] == null) return Result<Transaction>.Fail(ErrorCode.NullArgument, $"rewards[{i}]");
				}
			}

			if (string.IsNullOrEmpty(externalId))
			{
				externalId = null;
			}
			else if (FindEntryByExternalId(externalId) != null)
			{
				return Result<Transaction>.Fail(ErrorCode.DuplicateTransaction, $"a transaction with external id '{externalId}' is on record");
			}

			Transaction transaction = CreateTransaction(type, 1, source, TransactionStatus.Pending, rewards, externalId, awaitingPayment);
			Persist(transaction, creditDelta: 0);

			Recorded?.Invoke(transaction);
			return Result<Transaction>.Ok(transaction);
		}

		private Transaction CreateTransaction(Uid<TransactionType> type, long amount, string source, TransactionStatus status,
		                                      IReadOnlyList<IReward> rewards, string externalId, bool awaitingPayment)
		{
			var transaction = new Transaction
			{
				Id              = Uid.NewRandom(),
				Type            = type,
				Amount          = amount,
				Source          = source,
				Time            = PersistableState.GetFormattedTime(DateTime.UtcNow),
				Status          = status,
				ExternalId      = externalId,
				AwaitingPayment = awaitingPayment,
				Rewards         = rewards
			};

			AddToSession(transaction);
			return transaction;
		}

		private void AddToSession(Transaction transaction)
		{
			_sessionEntries.Add(transaction);

			// The newest entries of the type stay; older ones go, unless pending.
			Uid typeId = transaction.Type.Value;
			int kept   = 0;
			for (int i = _sessionEntries.Count - 1; i >= 0; i--)
			{
				Transaction candidate = _sessionEntries[i];
				if (candidate.Type.Value != typeId) continue;
				if (++kept <= MaxEntriesPerType || candidate.Status == TransactionStatus.Pending) continue;

				_sessionEntries.RemoveAt(i);
			}
		}

		private void Persist(Transaction transaction, int creditDelta)
		{
			_state.Entries.Add(new PersistedTransactionEntry
			{
				Id              = transaction.Id,
				TypeId          = transaction.Type.Value,
				Amount          = transaction.Amount,
				Source          = transaction.Source,
				Time            = transaction.Time,
				Status          = (int)transaction.Status,
				ExternalId      = transaction.ExternalId,
				AwaitingPayment = transaction.AwaitingPayment,
				Rewards         = ExtractRewardIds(transaction.Rewards)
			});

			TrimEntries(transaction.Type.Value);

			if (creditDelta != 0)
			{
				AddCount(transaction.Type.Value, creditDelta);
			}

			_commit.Commit();
		}

		/// <summary>One identity per reward, in order; None for a reward without one, which can't be recovered after a restart.</summary>
		private static List<Uid> ExtractRewardIds(IReadOnlyList<IReward> rewards)
		{
			var ids = new List<Uid>();
			if (rewards == null) return ids;

			for (int i = 0; i < rewards.Count; i++)
			{
				if (rewards[i] is UID asset && asset.HasIdentity)
				{
					ids.Add(asset.Id);
				}
				else
				{
					Debug.LogWarning($"[TransactionService] Reward '{rewards[i]}' has no identity — it will not be persisted for crash recovery.");
					ids.Add(Uid.None);
				}
			}

			return ids;
		}

		/// <summary>Keeps the newest entries of the type on disk; older ones go, unless pending.</summary>
		private void TrimEntries(Uid typeId)
		{
			int kept = 0;
			for (int i = _state.Entries.Count - 1; i >= 0; i--)
			{
				PersistedTransactionEntry entry = _state.Entries[i];
				if (entry.TypeId != typeId) continue;
				if (++kept <= MaxEntriesPerType || entry.Status == (int)TransactionStatus.Pending) continue;

				_state.Entries.RemoveAt(i);
			}
		}

		private void AddCount(Uid typeId, int delta)
		{
			int next = _counts.TryGetValue(typeId, out int current) ? current + delta : delta;
			_counts[typeId] = next;

			for (int i = 0; i < _state.Counts.Count; i++)
			{
				if (_state.Counts[i].TypeId == typeId)
				{
					_state.Counts[i].Count = next;
					return;
				}
			}

			_state.Counts.Add(new TypeCountEntry { TypeId = typeId, Count = next });
		}

		private bool ApplyRedirects(UidRedirectTable redirects)
		{
			if (redirects == null) return false;

			bool changed = false;

			foreach (PersistedTransactionEntry entry in _state.Entries)
			{
				if (redirects.TryFollow(entry.TypeId, out Uid newType))
				{
					entry.TypeId = newType;
					changed = true;
				}

				if (entry.Rewards == null) continue;

				for (int i = 0; i < entry.Rewards.Count; i++)
				{
					if (redirects.TryFollow(entry.Rewards[i], out Uid newReward))
					{
						entry.Rewards[i] = newReward;
						changed = true;
					}
				}
			}

			var merged = new Dictionary<Uid, int>(_state.Counts.Count);
			foreach (TypeCountEntry entry in _state.Counts)
			{
				if (entry.TypeId.IsNone) continue;

				Uid target = redirects.Follow(entry.TypeId);
				if (target != entry.TypeId) changed = true;
				merged[target] = merged.TryGetValue(target, out int existing) ? existing + entry.Count : entry.Count;
			}

			if (changed)
			{
				_state.Counts.Clear();
				foreach (KeyValuePair<Uid, int> kvp in merged)
				{
					_state.Counts.Add(new TypeCountEntry { TypeId = kvp.Key, Count = kvp.Value });
				}
			}

			return changed;
		}

		// Registered with the store in place of the service, which keeps it alive, so the
		// service's public type doesn't carry the kernel interface.
		private sealed class ResetListener : IStoreResetListener
		{
			private readonly TransactionService _service;

			public ResetListener(TransactionService service) => _service = service;

			public void OnStoreReset()
			{
				_service._sessionEntries.Clear();
				_service.LoadLedger(_service._accountId);
			}
		}
	}
}
