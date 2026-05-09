using AK.Core;
using UnityEngine;

namespace GameplayCore.MetaData
{
	[CreateAssetMenu(fileName = "IAPMeta", menuName = "Gameplay/MetaData/IAP/IAPMeta")]
	public class IAPMeta : MetaDataAsset
	{
		[SerializeField] private IAPProductsRegistry _registry;

		public IAPProductsRegistry ProductsRegistry => _registry;

		public override void InitializeMeta()
		{
			_registry.Initialize();
		}
	}
}