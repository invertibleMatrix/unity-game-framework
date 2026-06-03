using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Remote variable for integer values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteInt_", menuName = "AK/MetaData/RemoteConfig/Remote Int")]
	public class RemoteInt : RemoteVariable<int>
	{
	}
}