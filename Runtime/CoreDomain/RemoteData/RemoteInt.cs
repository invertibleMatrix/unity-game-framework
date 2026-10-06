using AK.Kernel.RemoteConfig;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote 32-bit whole number, such as <c>-12</c>, in the invariant culture.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteInt_", menuName = "AK/MetaData/RemoteConfig/Remote Int")]
	public class RemoteInt : RemoteVariable<int>
	{
		protected override bool TryParseValue(string text, out int value) => RemoteValueText.TryParseInt(text, out value);

		protected override string FormatValue(int value) => RemoteValueText.Format(value);
	}
}
