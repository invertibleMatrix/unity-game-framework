using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.Store
{
	/// <summary>
	/// Registry for managing shop item definitions using UID-based lookup
	/// </summary>
	[CreateAssetMenu(fileName = "ShopRegistry", menuName = "AK/MetaData/Store/ShopRegistry")]
	public class ShopRegistry : TypedUIDRegistryAsset<ShopItemDefinition>
	{
		// Inherits from TypedUIDRegistryAsset which provides:
		// - UID-based lookup
		// - Validation
		// - Registry management
	}
}