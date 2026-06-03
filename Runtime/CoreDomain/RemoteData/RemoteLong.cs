using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Remote variable for long integer values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteLong_", menuName = "AK/MetaData/RemoteConfig/Remote Long")]
	public class RemoteLong : RemoteVariable<long>
	{
	}
}