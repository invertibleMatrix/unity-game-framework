using AK.Core;
using AK.Examples.IAP;
using UnityEngine;

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "IAPMeta", menuName = "AK/MetaData/IAP/IAPMeta")]
	public class IAPMeta : MetaDataAsset, IMetaWithRegistry
	{
		[SerializeField] private IAPProductsRegistry _registry;

		public IAPProductsRegistry ProductsRegistry => _registry;

		public UidRegistryAssetBase RegistryAsset => _registry;

		public override void InitializeMeta() { }

		public bool TryGetProduct(Uid<IAPProductDefinition> id, out IAPProductDefinition product)
		{
			product = null;
			return _registry != null && _registry.TryResolve(id, out product);
		}

		/// <summary>
		/// Store-facing lookup by the platform product id (the external key printed on receipts).
		/// </summary>
		public IAPProductDefinition GetProductByStoreId(string storeProductId)
		{
			if (_registry == null || string.IsNullOrEmpty(storeProductId)) return null;

			var products = _registry.Objects;
			for (int i = 0; i < products.Count; i++)
			{
				if (products[i].ProductID == storeProductId) return products[i];
			}

			return null;
		}
	}
}
