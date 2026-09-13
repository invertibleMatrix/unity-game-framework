using AK.Core;
using UnityEngine;

namespace AK.Examples.Store
{
	/// <summary>
	/// Registry of shop item definitions with identity-keyed lookup.
	/// </summary>
	[CreateAssetMenu(fileName = "ShopRegistry", menuName = "AK/MetaData/Store/ShopRegistry")]
	public class ShopRegistry : UidRegistryAsset<ShopItemDefinition> { }
}
