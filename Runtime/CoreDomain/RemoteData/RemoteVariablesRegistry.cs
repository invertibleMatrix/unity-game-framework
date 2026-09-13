using AK.Core;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Simple registry for remote variables. Primarily used for UID-based lookup.
	/// All remote config operations are handled through RemoteConfigMeta.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteVariablesRegistry", menuName = "AK/MetaData/RemoteConfig/RemoteVariablesRegistry")]
	public class RemoteVariablesRegistry : UidRegistryAsset<RemoteVariableBase>
	{
		// Inherits all functionality from UidRegistryAsset<RemoteVariableBase>
		// - GetObjectByUID(UID) for UID-based lookup
		// - GetAllObjects() for iterating all variables
		// - RefreshAllObjects() for editor refresh
	}
}