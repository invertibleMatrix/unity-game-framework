using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Remote variable for string values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteString_", menuName = "Gameplay/MetaData/RemoteConfig/Remote String")]
	public class RemoteString : RemoteVariable<string>
	{
	}
}