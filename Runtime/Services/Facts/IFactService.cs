using System;
using System.Collections.Generic;
using System.Threading;
using AK.CoreDomain.Facts;
using Cysharp.Threading.Tasks;

namespace AK.Services.Facts
{
	/// <summary>
	/// The fact store: records monotonic occurrences ("GoPressed happened") and answers
	/// count queries over them. Facts have no lifecycle — no pending, no reversal, no
	/// entry log; only counts persist. For exchanges that can go wrong (rewards, IAP),
	/// use ITransactionService instead.
	/// </summary>
	public interface IFactService
	{
		/// <summary>Fires after a fact's count changes.</summary>
		event Action<FactType> Changed;

		/// <summary>Records one occurrence. Facts are born counted — there is no pending state.</summary>
		void Record(FactType fact);

		/// <summary>Clears all fact counts (e.g. a fresh life restarts tutorials).</summary>
		void ResetAll();

		int  Count(FactType fact);
		int  Count(string factGuid);
		bool HasOccurred(FactType fact);

		/// <summary>True when every condition's count meets its minimum. An unset condition fails closed.</summary>
		bool AreMet(IReadOnlyList<FactCondition> conditions);

		/// <summary>Event-driven wait until a fact's count reaches minCount. GUID-keyed — no asset resolution.</summary>
		UniTask WaitForCountAsync(string factGuid, int minCount = 1, CancellationToken ct = default);
	}
}
