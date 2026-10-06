using System.Threading;
using Cysharp.Threading.Tasks;

namespace AK.Services
{
	/// <summary>
	/// Checks a store order's receipt before anything is granted for it, usually on the game's
	/// server. <see cref="PurchaseService"/> asks it once per order it hasn't recorded yet.
	/// </summary>
	public interface IReceiptValidator
	{
		/// <summary>
		/// The verdict on the order's receipt. Answer <see cref="ReceiptVerdict.Unavailable"/>, not
		/// Invalid, when it can't be checked now (no network, a server error): the order then stays
		/// unfinished and is checked again later.
		/// </summary>
		UniTask<ReceiptVerdict> ValidateAsync(StoreOrder order, CancellationToken ct);
	}

	/// <summary>See <see cref="IReceiptValidator"/>.</summary>
	public enum ReceiptVerdict : byte
	{
		/// <summary>The receipt is genuine: the order is granted.</summary>
		Valid = 0,

		/// <summary>The receipt is forged or doesn't match the order: nothing is granted.</summary>
		Invalid = 1,

		/// <summary>It couldn't be checked now: the order stays unfinished and is checked again later.</summary>
		Unavailable = 2,
	}
}
