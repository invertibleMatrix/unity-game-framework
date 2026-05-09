using AK.Core;
using GameplayCore.MetaData.IAP;
using UnityEngine;

namespace GameplayCore.MetaData
{
	/// <summary>
	/// Registry for all IAP product definitions.
	/// Similar to RewardsRegistry but for IAP products.
	/// </summary>
	[CreateAssetMenu(fileName = "IAPProductsRegistry", menuName = "Gameplay/MetaData/IAP/IAPProductsRegistry")]
	public class IAPProductsRegistry : TypedUIDRegistryAsset<IAPProductDefinition> { }
}