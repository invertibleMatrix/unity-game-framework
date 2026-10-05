using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.Facts;
using AK.Kernel.Persistence;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services.Facts
{
	/// <summary>
	/// Default IFactService. Persists one count row per fact — the entire disk footprint
	/// of the fact domain. Identity redirects are applied whenever a ledger loads, and the
	/// ledger is rewritten, so a redirected fact costs nothing after the first launch.
	///
	/// Each account has its own ledger (<see cref="IAccountScoped"/>); until one is bound the
	/// device's ledger is used. Every mutation commits unless inside a <see cref="BeginBatch"/>
	/// scope; the store writes it to disk at the end of the frame. When the store deletes all
	/// its data, the counts go back to zero and <see cref="Changed"/> fires for each fact that
	/// had one.
	/// </summary>
	public class FactService : IFactService, IBatchable
	{
		private readonly PrefsStore           _store;
		private readonly UidRedirectTable     _redirects;
		private readonly DeferredCommit       _commit;
		private readonly ResetListener        _resetListener;
		private readonly Dictionary<Uid, int> _counts = new();

		private FactLedgerState _state;
		private string          _accountId;

		public event Action<Uid<FactType>> Changed;

		/// <param name="redirects">Identity redirects applied to each ledger as it loads.</param>
		/// <param name="store">Where the ledgers live; <see cref="UniPrefs.Store"/> when null.</param>
		public FactService(UidRedirectTable redirects = null, PrefsStore store = null)
		{
			_store     = store ?? UniPrefs.Store;
			_redirects = redirects;
			_commit    = new DeferredCommit(CommitLedger);

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
				throw new InvalidOperationException("[FactService] Can't switch accounts inside a write batch: its writes belong to the account bound when it opened.");
			}

			if (accountId != null && FactLedgerState.AdoptDeviceSave(_store, accountId))
			{
				Debug.Log("[FactService] The device's fact ledger moved to the account just bound, which had none of its own.");
			}

			Reload(accountId);
		}

		// ---------------------------------------------------------------- batching

		public LedgerBatch BeginBatch() => new(this);

		void IBatchable.Suspend() => _commit.Suspend();
		void IBatchable.Resume()  => _commit.Resume();

		// ---------------------------------------------------------------- record

		public void Record(FactType fact)
		{
			if (fact == null)
			{
				Debug.LogError("[FactService] Cannot record a null fact.");
				return;
			}

			Record(fact.IdAs<FactType>());
		}

		public void Record(Uid<FactType> fact)
		{
			if (fact.IsNone)
			{
				Debug.LogError("[FactService] Cannot record a fact with no identity.");
				return;
			}

			Uid id = fact.Value;
			int next = _counts.TryGetValue(id, out int current) ? current + 1 : 1;
			Write(id, next);
			Changed?.Invoke(fact);
		}

		public void SetCount(FactType fact, int count)
		{
			if (fact == null)
			{
				Debug.LogError("[FactService] Cannot set the count of a null fact.");
				return;
			}

			SetCount(fact.IdAs<FactType>(), count);
		}

		public void SetCount(Uid<FactType> fact, int count)
		{
			if (fact.IsNone)
			{
				Debug.LogError("[FactService] Cannot set the count of a fact with no identity.");
				return;
			}

			Uid id = fact.Value;
			if (count <= 0)
			{
				_counts.Remove(id);

				int row = FindRow(id);
				if (row >= 0) _state.Counts.RemoveAt(row);

				_commit.Commit();
			}
			else
			{
				Write(id, count);
			}

			Changed?.Invoke(fact);
		}

		public void ResetAll()
		{
			_counts.Clear();
			_state.Counts.Clear();
			_commit.Commit();
		}

		// ---------------------------------------------------------------- query

		public int Count(FactType fact) => fact != null ? Count(fact.Id) : 0;

		public int Count(Uid<FactType> fact) => Count(fact.Value);

		public bool HasOccurred(FactType fact) => Count(fact) > 0;

		public bool HasOccurred(Uid<FactType> fact) => Count(fact) > 0;

		public bool AreMet(IReadOnlyList<FactCondition> conditions)
		{
			if (conditions == null) return true;

			for (int i = 0; i < conditions.Count; i++)
			{
				FactCondition condition = conditions[i];
				if (condition == null) continue;

				if (condition.Type == null || condition.Type.Id.IsNone) return false;
				if (Count(condition.Type.Id) < condition.MinCount) return false;
			}

			return true;
		}

		public async UniTask WaitForCountAsync(Uid<FactType> fact, int minCount = 1, CancellationToken ct = default)
		{
			bool IsMet() => Count(fact) >= minCount;

			if (IsMet()) return;

			var completion = new UniTaskCompletionSource();

			void Handler(Uid<FactType> changed)
			{
				if (changed == fact && IsMet())
				{
					completion.TrySetResult();
				}
			}

			Changed += Handler;

			try
			{
				if (IsMet()) return;
				await completion.Task.AttachExternalCancellation(ct);
			}
			finally
			{
				Changed -= Handler;
			}
		}

		public IReadOnlyList<Uid> FindOrphans(IUidResolver resolver)
		{
			var orphans = new List<Uid>();
			if (resolver == null) return orphans;

			foreach (FactCountEntry entry in _state.Counts)
			{
				if (entry.FactId.IsSet && !resolver.TryResolve(entry.FactId, out UID _))
				{
					orphans.Add(entry.FactId);
				}
			}

			return orphans;
		}

		// ---------------------------------------------------------------- internals

		private int Count(Uid id)
		{
			return id.IsSet && _counts.TryGetValue(id, out int count) ? count : 0;
		}

		private void CommitLedger() => _state.Commit();

		private void Write(Uid id, int count)
		{
			_counts[id] = count;

			int row = FindRow(id);
			if (row >= 0)
			{
				_state.Counts[row].Count = count;
			}
			else
			{
				_state.Counts.Add(new FactCountEntry { FactId = id, Count = count });
			}

			_commit.Commit();
		}

		private int FindRow(Uid id)
		{
			List<FactCountEntry> rows = _state.Counts;
			for (int i = 0; i < rows.Count; i++)
			{
				if (rows[i].FactId == id) return i;
			}

			return -1;
		}

		/// <summary>Loads the ledger for <paramref name="accountId"/>, then raises Changed for each fact whose count differs from before.</summary>
		private void Reload(string accountId)
		{
			var before = new Dictionary<Uid, int>(_counts);
			LoadLedger(accountId);

			if (Changed == null) return;

			var changed = new List<Uid>();
			foreach (KeyValuePair<Uid, int> previous in before)
			{
				if (Count(previous.Key) != previous.Value) changed.Add(previous.Key);
			}

			foreach (KeyValuePair<Uid, int> current in _counts)
			{
				if (!before.ContainsKey(current.Key) && current.Value != 0) changed.Add(current.Key);
			}

			for (int i = 0; i < changed.Count; i++)
			{
				Changed?.Invoke(new Uid<FactType>(changed[i]));
			}
		}

		private void LoadLedger(string accountId)
		{
			_state     = FactLedgerState.Load(_store, accountId);
			_accountId = accountId;

			bool rewritten = ApplyRedirects(_redirects);

			_counts.Clear();
			foreach (FactCountEntry entry in _state.Counts)
			{
				if (entry.FactId.IsSet)
				{
					_counts[entry.FactId] = entry.Count;
				}
			}

			if (rewritten)
			{
				_commit.Commit();
			}
		}

		/// <summary>
		/// Rewrites rows whose identity has a redirect entry. When the target already has a
		/// row, the counts are summed — both rows described the same logical fact.
		/// </summary>
		private bool ApplyRedirects(UidRedirectTable redirects)
		{
			if (redirects == null || _state.Counts.Count == 0) return false;

			bool changed = false;
			var merged = new Dictionary<Uid, int>(_state.Counts.Count);

			foreach (FactCountEntry entry in _state.Counts)
			{
				if (entry.FactId.IsNone) continue;

				Uid target = redirects.Follow(entry.FactId);
				if (target != entry.FactId) changed = true;

				merged[target] = merged.TryGetValue(target, out int existing) ? existing + entry.Count : entry.Count;
			}

			if (!changed) return false;

			_state.Counts.Clear();
			foreach (KeyValuePair<Uid, int> kvp in merged)
			{
				_state.Counts.Add(new FactCountEntry { FactId = kvp.Key, Count = kvp.Value });
			}

			return true;
		}

		// Registered with the store in place of the service, which keeps it alive, so the
		// service's public type doesn't carry the kernel interface.
		private sealed class ResetListener : IStoreResetListener
		{
			private readonly FactService _service;

			public ResetListener(FactService service) => _service = service;

			public void OnStoreReset() => _service.Reload(_service._accountId);
		}
	}
}
