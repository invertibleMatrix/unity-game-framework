using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// Remote variable for double precision floating point values.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteDouble_", menuName = "AK/MetaData/RemoteConfig/Remote Double")]
	public class RemoteDouble : RemoteVariable<double>
	{
	}
}