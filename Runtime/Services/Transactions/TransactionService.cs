using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain;
using AK.CoreDomain.Transactions;
using AK.Services.Rewards;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services.Transactions
{
	/// <summary>
	/// Default ITransactionService. Records every transaction into a persisted ledger
	/// (counts permanent, entries capped per type) and credits reward payloads through
	/// IRewardService when one is provided — games using their own grant path can pass
	/// null and listen to the lifecycle events instead.
	///
	/// Every identity in the ledger is a value. Recovery resolves reward identities through
	/// the IUidResolver; a reward that no longer resolves is reported and dropped from the
	/// recovered payload — never substituted.
	///
	/// Every mutation commits the ledger to disk unless inside a <see cref="BeginBatch"/> scope.
	/// </summary>
	public class TransactionService : ITransactionService, IBatchable
	{
		private const int MaxEntriesPerType = 100;

		private readonly IRewardService         _rewardService;
		private readonly IUidResolver           _resolver;
		private readonly TransactionLedgerState _state;
		private readonly DeferredCommit         _commit;
		private readonly Dictionary<Uid, int>   _counts         = new();
		private readonly List<Transaction>      _sessionEntries = new();

		public event Action<Transaction> Recorded;
		public event Action<Transaction> Credited;
		public event Action<Transaction> Reversed;

		public TransactionService(IRewardService rewardService = null, IUidResolver resolver = null, UidRedirectTable redirects = null)
		{
			_rewardService = rewardService;
			_resolver      = resolver;
			_state         = TransactionLedgerState.Load();
			_commit        = new DeferredCommit(_state.Commit);

			bool rewritten = ApplyRedirects(redirects);

			foreach (TypeCountEntry entry in _state.Counts)
			{
				if (entry.TypeId.IsSet) _counts[entry.TypeId] = entry.Count;
			}

			if (rewritten) _commit.Commit();
		}

		// ---------------------------------------------------------------- batching

		public LedgerBatch BeginBatch() => new(this);

		void IBatchable.Suspend() => _commit.Suspend();
		void IBatchable.Resume()  => _commit.Resume();

		// ---------------------------------------------------------------- record

		public Result<Transaction> Record(TransactionType type, float amount = 1f, string source = null)
		{
			return type != null ? Record(type.IdAs<TransactionType>(), amount, source) : Result.Fail(ErrorCode.NullArgument, "type");
		}

		public Result<Transaction> Record(Uid<TransactionType> type, float amount = 1f, string source = null)
		{
			if (type.IsNone) return Result.Fail(ErrorCode.NoIdentity, "type");

			Transaction transaction = CreateTransaction(type, amount, source, TransactionStatus.Credited, null);
			Persist(transaction, creditDelta: 1);

			Recorded?.Invoke(transaction);
			Credited?.Invoke(transaction);
			return transaction;
		}

		public Result<Transaction> RecordPending(TransactionType type, IReadOnlyList<IReward> rewards = null, string source = null)
		{
			return type != null ? RecordPending(type.IdAs<TransactionType>(), rewards, source) : Result.Fail(ErrorCode.NullArgument, "type");
		}

		public Result<Transaction> RecordPending(Uid<TransactionType> type, IReadOnlyList<IReward> rewards = null, string source = null)
		{
			if (type.IsNone) return Result.Fail(ErrorCode.NoIdentity, "type");

			Transaction transaction = CreateTransaction(type, 1f, source, TransactionStatus.Pending, rewards);
			Persist(transaction, creditDelta: 0);

			Recorded?.Invoke(transaction);
			return transaction;
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
			if (transaction == null) return UniTask.FromResult(Result.Fail(ErrorCode.NullArgument, "transaction"));

			if (transaction.Status == TransactionStatus.Credited) return UniTask.FromResult(Result.Ok);

			if (transaction.Status != TransactionStatus.Pending)
			{
				return UniTask.FromResult(Result.Fail(ErrorCode.TransactionNotPending, transaction.Status.ToString()));
			}

			if (transaction.Rewards != null && transaction.Rewards.Count > 0)
			{
				if (_rewardService == null)
				{
					Debug.LogWarning($"[TransactionService] Transaction {transaction.Id.ToShortString()} carries rewards but no IRewardService was provided — marking credited without granting.");
				}
				else
				{
					for (int i = 0; i < transaction.Rewards.Count; i++)
					{
						Result granted = _rewardService.Grant(transaction.Rewards[i]);
						if (granted.IsFailed)
						{
							Debug.LogWarning($"[TransactionService] Reward {i} of transaction {transaction.Id.ToShortString()} was not granted: {granted}.");
						}
					}
				}
			}

			transaction.Status = TransactionStatus.Credited;
			UpdatePersistedStatus(transaction, creditDelta: 1);

			Credited?.Invoke(transaction);
			return UniTask.FromResult(Result.Ok);
		}

		public Result Reverse(Uid transactionId)
		{
			PersistedTransactionEntry entry = FindEntry(transactionId);
			if (entry == null) return Result.Fail(ErrorCode.NotFound, transactionId.ToShortString());

			if (entry.Status != (int)TransactionStatus.Credited)
			{
				return Result.Fail(ErrorCode.TransactionNotCredited, ((TransactionStatus)entry.Status).ToString());
			}

			entry.Status = (int)TransactionStatus.Reversed;
			AddCount(entry.TypeId, -1);
			_commit.Commit();

			Transaction transaction = FindSession(transactionId);
			if (transaction != null)
			{
				transaction.Status = TransactionStatus.Reversed;
				Reversed?.Invoke(transaction);
			}

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
					result.Add(existing);
					continue;
				}

				if (entry.TypeId.IsNone)
				{
					Debug.LogWarning($"[TransactionService] Pending transaction {entry.Id.ToShortString()} has no type identity — marking failed.");
					entry.Status = (int)TransactionStatus.Failed;
					dirty = true;
					continue;
				}

				var transaction = new Transaction
				{
					Id      = entry.Id,
					Type    = new Uid<TransactionType>(entry.TypeId),
					Amount  = entry.Amount,
					Source  = entry.Source,
					Time    = entry.Time,
					Status  = TransactionStatus.Pending,
					Rewards = ResolveRewards(entry.Rewards)
				};

				_sessionEntries.Add(transaction);
				result.Add(transaction);
			}

			if (dirty) _commit.Commit();

			return result;
		}

		// ---------------------------------------------------------------- internals

		private int Count(Uid id)
		{
			return id.IsSet && _counts.TryGetValue(id, out int count) ? count : 0;
		}

		private PersistedTransactionEntry FindEntry(Uid id)
		{
			for (int i = 0; i < _state.Entries.Count; i++)
			{
				if (_state.Entries[i].Id == id) return _state.Entries[i];
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

		private List<IReward> ResolveRewards(List<Uid> ids)
		{
			if (ids == null || ids.Count == 0) return null;

			if (_resolver == null)
			{
				Debug.LogWarning("[TransactionService] Pending transaction carries rewards but no IUidResolver was provided — rewards cannot be recovered.");
				return null;
			}

			var rewards = new List<IReward>(ids.Count);
			for (int i = 0; i < ids.Count; i++)
			{
				if (_resolver.TryResolve(ids[i], out UID asset) && asset is IReward reward)
				{
					rewards.Add(reward);
				}
				else
				{
					Debug.LogWarning($"[TransactionService] Reward {UidDebugNames.Describe(ids[i])} could not be resolved — the definition was removed without a redirect. Dropped from recovery.");
				}
			}

			return rewards.Count > 0 ? rewards : null;
		}

		private Transaction CreateTransaction(Uid<TransactionType> type, float amount, string source,
		                                      TransactionStatus status, IReadOnlyList<IReward> rewards)
		{
			var transaction = new Transaction
			{
				Id      = Uid.NewRandom(),
				Type    = type,
				Amount  = amount,
				Source  = source,
				Time    = PersistableState.GetFormattedTime(DateTime.UtcNow),
				Status  = status,
				Rewards = rewards
			};

			_sessionEntries.Add(transaction);
			return transaction;
		}

		private void Persist(Transaction transaction, int creditDelta)
		{
			_state.Entries.Add(new PersistedTransactionEntry
			{
				Id      = transaction.Id,
				TypeId  = transaction.Type.Value,
				Amount  = transaction.Amount,
				Source  = transaction.Source,
				Time    = transaction.Time,
				Status  = (int)transaction.Status,
				Rewards = ExtractRewardIds(transaction.Rewards)
			});

			TrimEntries(transaction.Type.Value);

			if (creditDelta != 0)
			{
				AddCount(transaction.Type.Value, creditDelta);
			}

			_commit.Commit();
		}

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
				}
			}

			return ids;
		}

		private void UpdatePersistedStatus(Transaction transaction, int creditDelta)
		{
			PersistedTransactionEntry entry = FindEntry(transaction.Id);
			if (entry != null)
			{
				entry.Status = (int)transaction.Status;
			}

			if (creditDelta != 0)
			{
				AddCount(transaction.Type.Value, creditDelta);
			}

			_commit.Commit();
		}

		private void TrimEntries(Uid typeId)
		{
			int count = 0;
			for (int i = _state.Entries.Count - 1; i >= 0; i--)
			{
				if (_state.Entries[i].TypeId != typeId) continue;

				count++;
				if (count > MaxEntriesPerType)
				{
					_state.Entries.RemoveAt(i);
				}
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
	}
}
