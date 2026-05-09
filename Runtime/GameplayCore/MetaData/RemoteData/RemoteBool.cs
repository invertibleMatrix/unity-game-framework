using UnityEngine;

namespace GameplayCore.MetaData.RemoteConfig
{
	/// <summary>
	/// Remote variable for boolean values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteBool_", menuName = "Gameplay/MetaData/RemoteConfig/Remote Bool")]
	public class RemoteBool : RemoteVariable<bool>
	{
	}
}