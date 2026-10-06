using AK.Utilities;
using UnityEngine;

namespace AK.Core.Extensions
{
	public static class StringExt
	{
		public static string WithTint(this string source, Color tint)
		{
			return "<color=#" + ColorUtility.ToHtmlStringRGBA(tint) + ">" + source + "</color>";
		}

		/// <summary>
		/// The number with a suffix, rounded down, as <see cref="NumberFormatter.FormatDouble(double, bool)"/>
		/// writes it: 1500 → "1.5K", 999,999 → "999.99K". Taken exactly.
		/// </summary>
		public static string ToSuffix(this long number)
		{
			return NumberFormatter.FormatWhole(number, roundDown: true);
		}

		/// <inheritdoc cref="ToSuffix(long)"/>
		public static string ToSuffix(this int number)
		{
			return NumberFormatter.FormatWhole(number, roundDown: true);
		}
	}
}
