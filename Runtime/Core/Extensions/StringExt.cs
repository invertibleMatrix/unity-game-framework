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

		public static string ToSuffix(this long number)
		{
			return NumberFormatter.FormatDouble(number, true);
		}
		
		public static string ToSuffix(this int number)
		{
			return NumberFormatter.FormatDouble(number, true);
		}
	}
}
