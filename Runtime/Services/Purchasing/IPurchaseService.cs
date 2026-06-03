using Cysharp.Threading.Tasks;
using AK.CoreDomain;

namespace AK.Services
{
	public interface IPurchaseService
	{
		public IIAPService IAPService { get; }

		public UniTask<PurchaseStatus> Purchase(PurchasableItemDefinition purchasableItemDefinition, bool immediateCredit);
	}
}
