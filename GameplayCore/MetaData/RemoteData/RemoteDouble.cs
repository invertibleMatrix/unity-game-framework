using UnityEngine;

namespace GameplayCore.MetaData.RemoteConfig
{
	/// <summary>
	/// Remote variable for double precision floating point values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteDouble_", menuName = "Gameplay/MetaData/RemoteConfig/Remote Double")]
	public class RemoteDouble : RemoteVariable<double>
	{
	}
}