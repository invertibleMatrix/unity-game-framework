using AK.Kernel.RemoteConfig;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote true-or-false value. It reads the words Firebase accepts, in any case:
	/// <c>1 true t yes y on</c> and <c>0 false f no n off</c>; empty text is no value.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteBool_", menuName = "AK/MetaData/RemoteConfig/Remote Bool")]
	public class RemoteBool : RemoteVariable<bool>
	{
		protected override bool TryParseValue(string text, out bool value) => RemoteValueText.TryParseBool(text, out value);

		protected override string FormatValue(bool value) => RemoteValueText.Format(value);
	}
}
