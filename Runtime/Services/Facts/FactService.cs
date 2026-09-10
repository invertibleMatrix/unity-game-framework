using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.Facts;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services.Facts
{
	/// <summary>
	/// Default IFactService. Persists one count row per fact — the entire disk footprint
	/// of the fact domain. Identity redirects are applied once at load and the file is
	/// rewritten, so a redirected fact costs nothing after the first launch.
	/// </summary>
	public class FactService : IFactService
	{
		private readonly FactLedgerState              _state;
		private readonly Dictionary<Uid, int>         _counts = new();

		public event Action<Uid<FactType>> Changed;

		public FactService(UidRedirectTable redirects = null)
		{
			_state = FactLedgerState.Load();

			bool rewritten = ApplyRedirects(redirects);

			foreach (FactCountEntry entry in _state.Counts)
			{
				if (entry.FactId.IsSet)
				{
					_counts[entry.FactId] = entry.Count;
				}
			}

			if (rewritten)
			{
				_state.Commit();
			}
		}

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
				_state.Counts.RemoveAll(e => e.FactId == id);
				_state.Commit();
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
			_state.Commit();
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

		private void Write(Uid id, int count)
		{
			_counts[id] = count;

			FactCountEntry entry = _state.Counts.Find(e => e.FactId == id);
			if (entry != null)
			{
				entry.Count = count;
			}
			else
			{
				_state.Counts.Add(new FactCountEntry { FactId = id, Count = count });
			}

			_state.Commit();
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
	}
}
