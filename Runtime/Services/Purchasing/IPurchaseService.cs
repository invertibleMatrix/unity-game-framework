using AK.Core;
using AK.CoreDomain;
using Cysharp.Threading.Tasks;

namespace AK.Services
{
	public interface IPurchaseService
	{
		/// <summary>
		/// The IAP service, or null if IAP is not enabled for this game.
		/// </summary>
		public IIAPService IAPService { get; }

		/// <summary>
		/// Purchase an item. Handles affordability check, cost deduction, and reward granting.
		/// IAP items are identified by having a non-empty ProductID and IAPService being available.
		/// Ok on success; otherwise a code the UI can map to a message — <see cref="ErrorCode.CannotAfford"/>,
		/// <see cref="ErrorCode.Cancelled"/>, <see cref="ErrorCode.StoreNotInitialized"/>, and the other 6xx store codes.
		/// </summary>
		public UniTask<Result> Purchase(IPurchasable item, bool immediateCredit);
	}
}
