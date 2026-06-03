using Cysharp.Threading.Tasks;
using AK.CoreDomain;

namespace AK.Services
{
	public interface IPurchaseService
	{
		/// <summary>
		/// The IAP service, or null if IAP is not enabled for this game.
		/// </summary>
		public IIAPService IAPService { get; }

		public UniTask<PurchaseStatus> Purchase(PurchasableItemDefinition purchasableItemDefinition, bool immediateCredit);
	}
}
