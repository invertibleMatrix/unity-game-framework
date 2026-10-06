using System.Threading;
using AK.CoreDomain;
using AK.Kernel.Results;
using Cysharp.Threading.Tasks;

namespace AK.Services
{
	public interface IPurchaseService
	{
		/// <summary>
		/// The IAP service, or null if IAP is not enabled for this game.
		/// </summary>
		IIAPService IAPService { get; }

		/// <summary>
		/// Buys an item: through the store when it has a ProductID, otherwise for its cost. Ok once
		/// it is credited, or once paid for when <paramref name="immediateCredit"/> is false; it is
		/// then owed until <see cref="GrantPendingCredits"/> credits it. Otherwise a code the UI can
		/// map to a message — <see cref="ErrorCode.CannotAfford"/>, <see cref="ErrorCode.Cancelled"/>,
		/// <see cref="ErrorCode.StoreNotInitialized"/>, and the other 6xx store codes. A reward that
		/// wasn't granted comes back as its code, with the purchase still owed rather than lost.
		/// </summary>
		UniTask<Result> Purchase(IPurchasable item, bool immediateCredit = true, CancellationToken ct = default);

		/// <summary>
		/// Credits what is owed: purchases bought with immediateCredit false, ones a reward kept
		/// pending, and ones recovered after a restart. Then the store's unfinished orders go to the
		/// order handler again, which finishes the ones credited and grants the others. Returns how
		/// many purchases were credited, store orders included.
		/// </summary>
		UniTask<int> GrantPendingCredits(CancellationToken ct = default);
	}
}
