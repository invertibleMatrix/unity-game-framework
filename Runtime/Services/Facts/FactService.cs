using System;
using System.Collections.Generic;
using System.Threading;
using AK.CoreDomain.Facts;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Services.Facts
{
	/// <summary>
	/// Default IFactService. Persists one count row per fact — the entire disk
	/// footprint of the fact domain.
	/// </summary>
	public class FactService : IFactService
	{
		private readonly FactLedgerState         _state;
		private readonly Dictionary<string, int> _counts = new();

		public event Action<FactType> Changed;

		public FactService()
		{
			_state = FactLedgerState.Load();

			foreach (var entry in _state.Counts)
			{
				_counts[entry.FactId] = entry.Count;
			}
		}

		public void Record(FactType fact)
		{
			if (fact == null)
			{
				Debug.LogError("[FactService] Cannot record a null fact.");
				return;
			}

			string id = fact.Id;
			_counts[id] = _counts.TryGetValue(id, out int current) ? current + 1 : 1;

			var entry = _state.Counts.Find(e => e.FactId == id);
			if (entry != null)
			{
				entry.Count = _counts[id];
			}
			else
			{
				_state.Counts.Add(new FactCountEntry { FactId = id, Count = _counts[id] });
			}

			_state.Commit();
			Changed?.Invoke(fact);
		}

		public void ResetAll()
		{
			Debug.Log("Facts Service Resetting");
			_counts.Clear();
			_state.Counts.Clear();
			_state.Commit();
		}

		public int Count(FactType fact)
		{
			return fact != null ? Count(fact.Id) : 0;
		}

		public int Count(string factGuid)
		{
			return factGuid != null && _counts.TryGetValue(factGuid, out int count) ? count : 0;
		}

		public bool HasOccurred(FactType fact)
		{
			return Count(fact) > 0;
		}

		public bool AreMet(IReadOnlyList<FactCondition> conditions)
		{
			if (conditions == null) return true;

			foreach (var condition in conditions)
			{
				if (condition == null) continue;

				// Counts are GUID-keyed; the direct FactType reference yields its
				// logical id. An unset condition fails closed.
				string factGuid = condition.Type != null ? condition.Type.Id : null;
				if (string.IsNullOrEmpty(factGuid)) return false;

				if (Count(factGuid) < condition.MinCount)
				{
					return false;
				}
			}

			return true;
		}

		public async UniTask WaitForCountAsync(string factGuid, int minCount = 1, CancellationToken ct = default)
		{
			bool IsMet() => Count(factGuid) >= minCount;

			if (IsMet()) return;

			var completion = new UniTaskCompletionSource();

			void Handler(FactType _)
			{
				if (IsMet())
				{
					completion.TrySetResult();
				}
			}

			Changed += Handler;

			try
			{
				// Re-check after subscribing to close the check/subscribe race.
				if (IsMet()) return;
				await completion.Task.AttachExternalCancellation(ct);
			}
			finally
			{
				Changed -= Handler;
			}
		}
	}
}
