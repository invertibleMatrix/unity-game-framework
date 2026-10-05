using System;
using System.Collections.Generic;
using System.Threading;
using AK.Core;
using AK.CoreDomain.Facts;
using Cysharp.Threading.Tasks;

namespace AK.Services.Facts
{
	/// <summary>
	/// The fact store: records monotonic occurrences ("GoPressed happened") and answers
	/// count queries over them. Facts have no lifecycle — no pending, no reversal, no
	/// entry log; only counts persist. For exchanges that can go wrong (rewards, IAP),
	/// use ITransactionService instead.
	///
	/// Counts are keyed by <see cref="Uid{T}"/>. The asset overloads are conveniences that
	/// read <c>fact.Id</c>; nothing here ever resolves an identity back to an asset.
	///
	/// Counts belong to the bound account (<see cref="IAccountScoped"/>). Binding another
	/// account switches to its counts and raises <see cref="Changed"/> for each fact whose
	/// count differs.
	/// </summary>
	public interface IFactService : IAccountScoped
	{
		/// <summary>Fires after a fact's count changes, including through an account switch or a store reset.</summary>
		event Action<Uid<FactType>> Changed;

		/// <summary>Records one occurrence. Facts are born counted — there is no pending state.</summary>
		void Record(FactType fact);
		void Record(Uid<FactType> fact);

		/// <summary>[Editor/debug tooling] Overwrites a fact's count in place. Persists exactly like Record.</summary>
		void SetCount(FactType fact, int count);
		void SetCount(Uid<FactType> fact, int count);

		/// <summary>Clears all fact counts (e.g. a fresh life restarts tutorials).</summary>
		void ResetAll();

		int  Count(FactType fact);
		int  Count(Uid<FactType> fact);
		bool HasOccurred(FactType fact);
		bool HasOccurred(Uid<FactType> fact);

		/// <summary>True when every condition's count meets its minimum. An unset condition fails closed.</summary>
		bool AreMet(IReadOnlyList<FactCondition> conditions);

		/// <summary>Event-driven wait until a fact's count reaches minCount.</summary>
		UniTask WaitForCountAsync(Uid<FactType> fact, int minCount = 1, CancellationToken ct = default);

		/// <summary>
		/// Persisted rows whose identity does not resolve through the given resolver. Diagnostic:
		/// a non-empty result after a content patch means facts were removed or replaced without
		/// a redirect entry.
		/// </summary>
		IReadOnlyList<Uid> FindOrphans(IUidResolver resolver);

		/// <summary>
		/// Defers the disk write until the returned scope is disposed, so several Records in a
		/// row cost one serialization and one flush. Counts and <see cref="Changed"/> still
		/// update immediately. Nested scopes flush once at the outermost dispose.
		/// </summary>
		LedgerBatch BeginBatch();
	}
}
