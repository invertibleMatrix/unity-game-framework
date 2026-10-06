using AK.Kernel.RemoteConfig;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote single-precision number, such as <c>1.5</c> or <c>2e-3</c>, in the invariant
	/// culture on every device. NaN and infinities are no value.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteFloat_", menuName = "AK/MetaData/RemoteConfig/Remote Float")]
	public class RemoteFloat : RemoteVariable<float>
	{
		protected override bool TryParseValue(string text, out float value) => RemoteValueText.TryParseFloat(text, out value);

		protected override string FormatValue(float value) => RemoteValueText.Format(value);
	}
}
