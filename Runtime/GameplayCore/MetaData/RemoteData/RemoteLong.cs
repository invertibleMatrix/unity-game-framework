using UnityEngine;

namespace GameplayCore.MetaData.RemoteConfig
{
	/// <summary>
	/// Remote variable for long integer values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteLong_", menuName = "Gameplay/MetaData/RemoteConfig/Remote Long")]
	public class RemoteLong : RemoteVariable<long>
	{
	}
}