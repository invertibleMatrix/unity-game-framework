using AK.Core;
using UnityEngine;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "IAPMeta", menuName = "Gameplay/MetaData/IAP/IAPMeta")]
	public class IAPMeta : MetaDataAsset, IMeta
	{
		[SerializeField] private IAPProductsRegistry _registry;

		public IAPProductsRegistry ProductsRegistry => _registry;

		public override void InitializeMeta()
		{
			_registry.Initialize();
		}
	}
}