using AK.Core;
using UnityEngine;

namespace AK.Utilities
{
	/// <summary>
	/// Registry of <see cref="PoolableObjectDefinition"/>s. Pass to
	/// <see cref="IObjectPoolService.RegisterPools"/> to create and prewarm all pools at boot.
	/// Inherits identity lookup and editor validation/refresh from UidRegistryAsset.
	/// </summary>
	[CreateAssetMenu(fileName = "ObjectPoolRegistry", menuName = "AK/Pooling/Object Pool Registry")]
	public class ObjectPoolRegistry : UidRegistryAsset<PoolableObjectDefinition>
	{
	}
}
