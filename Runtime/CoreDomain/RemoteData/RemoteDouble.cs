using AK.Kernel.RemoteConfig;
using UnityEngine;

namespace AK.CoreDomain.RemoteConfig
{
	/// <summary>
	/// A remote double-precision number, such as <c>1.5</c> or <c>2e-3</c>, in the invariant
	/// culture on every device. NaN and infinities are no value.
	/// </summary>
	[CreateAssetMenu(fileName = "RemoteDouble_", menuName = "AK/MetaData/RemoteConfig/Remote Double")]
	public class RemoteDouble : RemoteVariable<double>
	{
		protected override bool TryParseValue(string text, out double value) => RemoteValueText.TryParseDouble(text, out value);

		protected override string FormatValue(double value) => RemoteValueText.Format(value);
	}
}
