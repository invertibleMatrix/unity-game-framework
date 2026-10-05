using AK.Kernel.RemoteConfig;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote 64-bit whole number, such as <c>-12</c>, in the invariant culture.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteLong_", menuName = "AK/MetaData/RemoteConfig/Remote Long")]
	public class RemoteLong : RemoteVariable<long>
	{
		protected override bool TryParseValue(string text, out long value) => RemoteValueText.TryParseLong(text, out value);

		protected override string FormatValue(long value) => RemoteValueText.Format(value);
	}
}
