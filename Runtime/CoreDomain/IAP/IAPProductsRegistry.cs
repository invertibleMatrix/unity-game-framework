using AK.Core;
using AK.CoreDomain.IAP;
using UnityEngine;

namespace AK.CoreDomain
{
	/// <summary>
	/// Registry for all IAP product definitions.
	/// Similar to RewardsRegistry but for IAP products.
	/// </summary>
	[CreateAssetMenu(fileName = "IAPProductsRegistry", menuName = "AK/MetaData/IAP/IAPProductsRegistry")]
	public class IAPProductsRegistry : TypedUIDRegistryAsset<IAPProductDefinition> { }
}