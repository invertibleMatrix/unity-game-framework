using Cysharp.Threading.Tasks;
using GameplayCore.MetaData;

namespace AK.Services
{
	public interface IPurchaseService
	{
		public IIAPService IAPService { get; }
		
		public async UniTask<PurchaseStatus> Purchase(PurchasableItemDefinition purchasableItemDefinition, bool immediateCredit)
		{
			return default;
		}
	}
}