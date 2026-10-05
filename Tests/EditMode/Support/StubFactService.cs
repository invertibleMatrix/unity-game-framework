using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.Facts;
using AK.Services.Facts;
using Cysharp.Threading.Tasks;

namespace AK.Tests.Support
{
	/// <summary>
	/// A fact ledger for tutorial runner tests: every condition is met, and every fact counts
	/// the records made so far, all facts together. A provider records only its progress fact,
	/// so <see cref="Recorded"/> is its step pointer.
	/// </summary>
	internal sealed class StubFactService : IFactService
	{
		public int Recorded;

		public event Action<Uid<FactType>> Changed { add { } remove { } }

		public string AccountId { get; private set; }
		public void BindAccount(string accountId) => AccountId = string.IsNullOrEmpty(accountId) ? null : accountId;

		public void Record(FactType fact) => Recorded++;
		public void Record(Uid<FactType> fact) => Recorded++;
		public void SetCount(FactType fact, int count) { }
		public void SetCount(Uid<FactType> fact, int count) { }
		public void ResetAll() { }
		public int Count(FactType fact) => Recorded;
		public int Count(Uid<FactType> fact) => Recorded;
		public bool HasOccurred(FactType fact) => false;
		public bool HasOccurred(Uid<FactType> fact) => false;
		public bool AreMet(IReadOnlyList<FactCondition> conditions) => true;
		public UniTask WaitForCountAsync(Uid<FactType> fact, int minCount = 1, CancellationToken ct = default) => UniTask.CompletedTask;
		public IReadOnlyList<Uid> FindOrphans(IUidResolver resolver) => Array.Empty<Uid>();
		public LedgerBatch BeginBatch() => default;
	}
}
