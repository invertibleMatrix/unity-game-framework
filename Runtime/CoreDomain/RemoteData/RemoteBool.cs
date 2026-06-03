using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Remote variable for boolean values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteBool_", menuName = "AK/MetaData/RemoteConfig/Remote Bool")]
	public class RemoteBool : RemoteVariable<bool>
	{
	}
}