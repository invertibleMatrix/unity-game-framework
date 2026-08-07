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
	/// </summary>
	public class TransactionService : ITransactionService
	{
		private const int MaxEntriesPerType = 100;

		private readonly IRewardService          _rewardService;
		private readonly IMetaDataRepository     _metaDataRepository;
		private readonly TransactionLedgerState  _state;
		private readonly Dictionary<string, int> _counts         = new();
		private readonly List<Transaction>       _sessionEntries = new();

		public event Action<Transaction> Recorded;
		public event Action<Transaction> Credited;
		public event Action<Transaction> Reversed;

		public TransactionService(IRewardService rewardService = null, IMetaDataRepository metaDataRepository = null)
		{
			_rewardService = rewardService;
			_metaDataRepository = metaDataRepository;
			_state = TransactionLedgerState.Load();

			foreach (var entry in _state.Counts)
			{
				_counts[entry.TypeId] = entry.Count;
			}
		}

		public Transaction Record(UID type, float amount = 1f, string source = null)
		{
			var transaction = CreateTransaction(type, amount, source, TransactionStatus.Credited, null);
			if (transaction == null) return null;

			Persist(transaction, creditDelta: 1);

			Recorded?.Invoke(transaction);
			Credited?.Invoke(transaction);
			return transaction;
		}

		public Transaction RecordPending(UID type, IReadOnlyList<IReward> rewards = null, string source = null)
		{
			var transaction = CreateTransaction(type, 1f, source, TransactionStatus.Pending, rewards);
			if (transaction == null) return null;

			Persist(transaction, creditDelta: 0);

			Recorded?.Invoke(transaction);
			return transaction;
		}

		public UniTask<bool> CreditAsync(UID type, IReadOnlyList<IReward> rewards, string source = null, CancellationToken ct = default)
		{
			return CreditAsync(RecordPending(type, rewards, source), ct);
		}

		public UniTask<bool> CreditAsync(Transaction transaction, CancellationToken ct = default)
		{
			if (transaction == null)
			{
				Debug.LogError("[TransactionService] Cannot credit a null transaction.");
				return UniTask.FromResult(false);
			}

			if (transaction.Status == TransactionStatus.Credited)
			{
				return UniTask.FromResult(true);
			}

			if (transaction.Status != TransactionStatus.Pending)
			{
				Debug.LogWarning($"[TransactionService] Cannot credit transaction '{transaction.Id}' — status is {transaction.Status}.");
				return UniTask.FromResult(false);
			}

			if (transaction.Rewards != null && transaction.Rewards.Count > 0)
			{
				if (_rewardService == null)
				{
					Debug.LogWarning(
						$"[TransactionService] Transaction '{transaction.Id}' carries rewards but no IRewardService was provided — marking credited without granting.");
				}
				else
				{
					foreach (var reward in transaction.Rewards)
					{
						_rewardService.TryGrantReward(reward);
					}
				}
			}

			transaction.Status = TransactionStatus.Credited;
			UpdatePersistedStatus(transaction, creditDelta: 1);

			Credited?.Invoke(transaction);
			return UniTask.FromResult(true);
		}

		public bool Reverse(string transactionId)
		{
			var entry = _state.Entries.Find(e => e.Id == transactionId);
			if (entry == null)
			{
				Debug.LogWarning($"[TransactionService] Cannot reverse unknown transaction '{transactionId}'.");
				return false;
			}

			if (entry.Status != (int)TransactionStatus.Credited)
			{
				Debug.LogWarning($"[TransactionService] Cannot reverse transaction '{transactionId}' — only credited transactions can be reversed.");
				return false;
			}

			entry.Status = (int)TransactionStatus.Reversed;
			AddCount(entry.TypeId, -1);
			_state.Commit();

			var transaction = _sessionEntries.Find(t => t.Id == transactionId);
			if (transaction != null)
			{
				transaction.Status = TransactionStatus.Reversed;
				Reversed?.Invoke(transaction);
			}

			return true;
		}

		public int Count(UID type)
		{
			return type != null && _counts.TryGetValue(type.Id, out int count) ? count : 0;
		}

		public bool HasOccurred(UID type)
		{
			return Count(type) > 0;
		}

		public IReadOnlyList<Transaction> Query(UID type, TransactionStatus? status = null)
		{
			var result = new List<Transaction>();
			foreach (var transaction in _sessionEntries)
			{
				if (type != null && transaction.Type.Id != type.Id) continue;
				if (status.HasValue && transaction.Status != status.Value) continue;
				result.Add(transaction);
			}

			return result;
		}

		public IReadOnlyList<Transaction> GetPendingTransactions()
		{
			var result = new List<Transaction>();

			if (_metaDataRepository == null)
			{
				Debug.LogWarning("[TransactionService] GetPendingTransactions requires an IMetaDataRepository for UID resolution.");
				return result;
			}

			bool dirty = false;

			foreach (var entry in _state.Entries)
			{
				if (entry.Status != (int)TransactionStatus.Pending) continue;

				var existing = _sessionEntries.Find(t => t.Id == entry.Id);
				if (existing != null)
				{
					result.Add(existing);
					continue;
				}

				UID type = _metaDataRepository.UIDRegistry.GetUID(entry.TypeId);
				if (type == null)
				{
					Debug.LogWarning(
						$"[TransactionService] Pending transaction '{entry.Id}' has unresolvable type '{entry.TypeId}' — marking failed.");
					entry.Status = (int)TransactionStatus.Failed;
					dirty = true;
					continue;
				}

				var transaction = new Transaction
				{
					Id = entry.Id,
					Type = type,
					Amount = entry.Amount,
					Source = entry.Source,
					Time = entry.Time,
					Status = TransactionStatus.Pending,
					Rewards = ResolveRewards(entry.Rewards)
				};

				_sessionEntries.Add(transaction);
				result.Add(transaction);
			}

			if (dirty)
			{
				_state.Commit();
			}

			return result;
		}

		private List<IReward> ResolveRewards(List<PersistedRewardRef> refs)
		{
			if (refs == null || refs.Count == 0) return null;

			var rewards = new List<IReward>();
			foreach (var rewardRef in refs)
			{
				UID uid = _metaDataRepository.UIDRegistry.GetUID(rewardRef.Id);

				if (uid == null && !string.IsNullOrEmpty(rewardRef.Name))
				{
					uid = _metaDataRepository.UIDRegistry.GetUIDByName(rewardRef.Name);
					if (uid != null)
					{
						Debug.LogWarning(
							$"[TransactionService] Reward resolved via name fallback: '{rewardRef.Name}'. Update the saved GUID '{rewardRef.Id}' to '{uid.Id}'.");
					}
				}

				if (uid is IReward reward)
				{
					rewards.Add(reward);
				}
				else
				{
					Debug.LogWarning(
						$"[TransactionService] Reward could not be resolved. GUID: '{rewardRef.Id}', Name: '{rewardRef.Name}'. The asset may have been deleted.");
				}
			}

			return rewards.Count > 0 ? rewards : null;
		}

		private Transaction CreateTransaction(UID type, float amount, string source,
		                                      TransactionStatus status, IReadOnlyList<IReward> rewards)
		{
			if (type == null)
			{
				Debug.LogError("[TransactionService] Cannot record a transaction with a null type.");
				return null;
			}

			var transaction = new Transaction
			{
				Id = Guid.NewGuid().ToString(),
				Type = type,
				Amount = amount,
				Source = source,
				Time = PersistableState.GetFormattedTime(DateTime.UtcNow),
				Status = status,
				Rewards = rewards
			};

			_sessionEntries.Add(transaction);
			return transaction;
		}

		private void Persist(Transaction transaction, int creditDelta)
		{
			_state.Entries.Add(new PersistedTransactionEntry
			{
				Id = transaction.Id,
				TypeId = transaction.Type.Id,
				Amount = transaction.Amount,
				Source = transaction.Source,
				Time = transaction.Time,
				Status = (int)transaction.Status,
				Rewards = ExtractRewardRefs(transaction.Rewards)
			});

			TrimEntries(transaction.Type.Id);

			if (creditDelta != 0)
			{
				AddCount(transaction.Type.Id, creditDelta);
			}

			_state.Commit();
		}

		private static List<PersistedRewardRef> ExtractRewardRefs(IReadOnlyList<IReward> rewards)
		{
			var refs = new List<PersistedRewardRef>();
			if (rewards == null) return refs;

			foreach (var reward in rewards)
			{
				if (reward is UID uid && !uid.IsEmpty())
				{
					refs.Add(new PersistedRewardRef { Id = uid.Id, Name = uid.name });
				}
				else
				{
					Debug.LogWarning(
						$"[TransactionService] Reward '{reward}' is not a UID-bearing asset — it will not be persisted for crash recovery.");
				}
			}

			return refs;
		}

		private void UpdatePersistedStatus(Transaction transaction, int creditDelta)
		{
			var entry = _state.Entries.Find(e => e.Id == transaction.Id);
			if (entry != null)
			{
				entry.Status = (int)transaction.Status;
			}

			if (creditDelta != 0)
			{
				AddCount(transaction.Type.Id, creditDelta);
			}

			_state.Commit();
		}

		private void TrimEntries(string typeId)
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

		private void AddCount(string typeId, int delta)
		{
			_counts[typeId] = _counts.TryGetValue(typeId, out int current) ? current + delta : delta;

			var entry = _state.Counts.Find(e => e.TypeId == typeId);
			if (entry != null)
			{
				entry.Count = _counts[typeId];
			}
			else
			{
				_state.Counts.Add(new TypeCountEntry { TypeId = typeId, Count = _counts[typeId] });
			}
		}
	}
}