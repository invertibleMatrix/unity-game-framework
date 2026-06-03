using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Remote variable for float values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteFloat_", menuName = "Gameplay/MetaData/RemoteConfig/Remote Float")]
	public class RemoteFloat : RemoteVariable<float>
	{
	}
}