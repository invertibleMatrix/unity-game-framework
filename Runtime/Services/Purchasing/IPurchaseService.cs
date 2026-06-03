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
		/// </summary>
		public UniTask<PurchaseStatus> Purchase(IPurchasable item, bool immediateCredit);
	}
}
