using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Simple registry for remote variables. Primarily used for UID-based lookup.
	/// All remote config operations are handled through RemoteConfigMeta.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteVariablesRegistry", menuName = "Gameplay/MetaData/RemoteConfig/RemoteVariablesRegistry")]
	public class RemoteVariablesRegistry : TypedUIDRegistryAsset<RemoteVariableBase>
	{
		// Inherits all functionality from TypedUIDRegistryAsset<RemoteVariableBase>
		// - GetObjectByUID(UID) for UID-based lookup
		// - GetAllObjects() for iterating all variables
		// - RefreshAllObjects() for editor refresh
	}
}