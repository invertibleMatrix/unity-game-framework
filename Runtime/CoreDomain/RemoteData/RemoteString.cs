using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote string, taken as it is. Empty text is no value, so an empty remote string leaves
	/// the variable at its default.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteString_", menuName = "AK/MetaData/RemoteConfig/Remote String")]
	public class RemoteString : RemoteVariable<string>
	{
		protected override bool TryParseValue(string text, out string value)
		{
			value = text;
			return true;
		}

		protected override string FormatValue(string value) => value ?? string.Empty;
	}
}
